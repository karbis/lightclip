using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Threading;
using FFMpegCore;
using FFMpegCore.Pipes;
using lightclip.Windows;
using Microsoft.Win32;
using ScreenRecorderLib;

namespace lightclip {
	internal class ClipRecorder {
		static BaseVideoStream stream;
		static Recorder rec;
		static Properties.Settings settings = Properties.Settings.Default;
		static RecordingSourceBase source = null;
		static Timer sourceCheckTimer = null;
		static long startTime;
		public static bool Initialized = false;
		public static string LastClipPath;

		public static void Start() {
			CreateRecorder();

			for (int i = 1; i <= 2; i++) {
				foreach (Settings.SettingDefinition setting in SettingsData.Data[i].List) {
					if (setting.Type is Settings.SeperatorSettingType) continue;
					Properties.Settings.Default.PropertyChanged += (object _, PropertyChangedEventArgs e) => {
						if (e.PropertyName != setting.Name) return;
						Task.Run(CreateRecorder);
					};
				}
			}
			
			SystemEvents.PowerModeChanged += powerModeChanged;
			Application.Current.Exit += (_, _) => {
				SystemEvents.PowerModeChanged -= powerModeChanged;
			};

			sourceCheckTimer = new Timer((_) => {
				if (source == null) return;
				if (settings.MonitorInputSource != "Auto") return;
				if (!(source is DisplayRecordingSource display)) return;
				string newName = ClipSettings.GetCurrentSourceName();
				if (newName == display.DeviceName) return;

				display.DeviceName = newName;
				rec.GetDynamicOptionsBuilder().SetUpdatedRecordingSource(source).Apply();
				//Debug.WriteLine("udpated");
			}, null, 100, 100);
		}

		public static Recorder CreateRecorder() {
			Initialized = false;
			if (stream != null) {
				stream.OnOverflow -= OnOverflow;
				//stream.Dispose(); // should get automatically disposed by the recorder

				Dispatcher.CurrentDispatcher.Invoke(() => {
					rec.Dispose();
					if (stream is VideoDiskStream disk) {
						disk.CloseHandles();
					}
				});
			}

			stream = ClipSettings.GetVideoStream();
			stream.MaxFrameCount = settings.Framerate * settings.ClipLength;

			source = ClipSettings.GetCurrentSource();
			RecorderOptions options = new RecorderOptions() {
				VideoEncoderOptions = new VideoEncoderOptions() {
					IsFixedFramerate = true,
					Framerate = settings.Framerate,
					IsFragmentedMp4Enabled = true,
					Bitrate = settings.Bitrate * 1000,
					Quality = settings.VideoQualityPercent,
					Encoder = new H264VideoEncoder() {
						EncoderProfile = H264Profile.Main,
						BitrateMode = ClipSettings.GetVideoBitrateType()
					},
					IsHardwareEncodingEnabled = settings.EncodingType != "CPU"
				},
				AudioOptions = new AudioOptions() {
					IsAudioEnabled = true,
					IsOutputDeviceEnabled = settings.OutputDevice != "None",
					IsInputDeviceEnabled = settings.MicrophoneInputDevice != "None",
					AudioInputDevice = ClipSettings.GetInputDevice(),
					AudioOutputDevice = ClipSettings.GetOutputDevice(),
					Bitrate = ClipSettings.GetAudioBitrate(),
					Channels = AudioChannels.Stereo,
					InputVolume = ClipSettings.GetInputVolume(),
					OutputVolume = ClipSettings.GetOutputVolume()
				},
				SourceOptions = new SourceOptions() {
					RecordingSources = new List<RecordingSourceBase>() { source }
				},
				OutputOptions = new OutputOptions() {
					OutputFrameSize = ClipSettings.GetResolution(getBaseResolution()),
					Stretch = StretchMode.Fill
				}
			};

			rec = Recorder.CreateRecorder(options);
			rec.Record(stream);

			EventHandler startedHandler = null;
			startedHandler = (_, _) => {
				startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				stream.OnChunkWritten -= startedHandler;
				Initialized = true;
			};
			stream.OnChunkWritten += startedHandler;

			stream.OnOverflow += OnOverflow;

			return rec;
		}

		private static void OnOverflow(object _, EventArgs e) {
			Task.Run(() => CreateRecorder());
		}

		private static ScreenSize getBaseResolution() {
			Rect monitor = ExternMonitor.GetMonitorSize(ExternMonitor.GetMainMonitor());
			return new ScreenSize(monitor.Width, monitor.Height);
		}

		public async static Task EncodeClip(string path) {
			Debug.WriteLine("Encoding started");
			using Stream videoStream = stream.GetFinalStream();
			videoStream.Position = 0;

			Debug.WriteLine("ffmpeg started");
			//File.WriteAllBytes(path, videoStream.ToArray());
			await FFMpegArguments
				.FromPipeInput(new StreamPipeSource(videoStream))
				.OutputToFile(path, true, (FFMpegArgumentOptions options) => options.WithCustomArgument("-c copy")
					.Seek(TimeSpan.FromSeconds(Math.Max(0, stream.FrameCount / (double)settings.Framerate - settings.ClipLength))))
				.ProcessAsynchronously();

			Debug.WriteLine("video written");
		}

		public async static void Clip() {
			if (!Initialized) return;
			if (!Path.Exists(Properties.Settings.Default.OutputDirectory)) {
				MessageBox.Show("The output directory for clips does not exist", "Clip error");
				return;
			}

			ClipNotification notif = null;
			if (settings.ShowNotification) {
				notif = ClipNotification.CreateNotification("Clipping...");
			}

			// wait for a couple more chunks of data to be added
			int intendedFrameCount = (int)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime) / 1000 * settings.Framerate);
			TaskCompletionSource task = new TaskCompletionSource();
			EventHandler handler = null;

			byte calls = 0;
			handler = (_, _) => {
				if (stream.TotalFrameCount >= intendedFrameCount) {
					calls++;
					if (calls >= Math.Min(4, Math.Ceiling(stream.FrameCount / settings.Framerate * 0.1))) { // magic offset. idk it works kinda
						stream.OnChunkWritten -= handler;
						task.SetResult();
					}
				}
			};
			stream.OnChunkWritten += handler;
			await task.Task;

			string path = Path.Combine(Properties.Settings.Default.OutputDirectory, getDateString() + ".mp4");
			try {
				await EncodeClip(path);
			} catch (Exception e) {
				MessageBox.Show("Error while encoding clip\n" + e.ToString(), "Clip error");
				return;
			}

			LastClipPath = path;
			if (settings.CopyToClipboard) {
				Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection() { path });
			}
			if (notif != null) {
				double length = Math.Min(settings.ClipLength, Math.Round(stream.FrameCount / (double)settings.Framerate));
				string lengthStr = (length < 60) ? $"{length}s" : TimeSpan.FromSeconds(length).ToString(@"m\:ss");

				notif.Label.Text = $"Clipped {lengthStr}, click here to edit.";
				notif.StartFadeout();
				notif.AddClickEvent(() => {
					new ClipEditorWindow(path).Show();
				});
				notif = null;
			}
		}

		private static string getDateString() {
			DateTime now = DateTime.Now;
			return now.Year + "-" + now.Month.ToString().PadLeft(2, '0') + "-" + now.Day.ToString().PadLeft(2, '0') + " "
				+ now.Hour + "-" + now.Minute.ToString().PadLeft(2, '0') + "-" + now.Second.ToString().PadLeft(2, '0');
		}

		private static void powerModeChanged(object _, PowerModeChangedEventArgs e) {
			if (e.Mode == PowerModes.Resume) {
				startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)(stream.TotalFrameCount / (double)settings.Framerate * 1000);
				// bug fix. sleeping stops the recording
			}
		}
	}
}