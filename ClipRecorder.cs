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
using Microsoft.Win32;
using ScreenRecorderLib;

namespace lightclip {
	internal class ClipRecorder {
		static VideoMemoryStream stream;
		static Recorder rec;
		static Properties.Settings settings = Properties.Settings.Default;
		static RecordingSourceBase source = null;
		static Timer monitorCheckTimer = null;
		static long startTime;

		public static void Start() {
			CreateRecorder();

			foreach (Settings.SettingDefinition setting in SettingsData.Data[1].List) {
				if (setting.Type is Settings.SeperatorSettingType) continue;
				Properties.Settings.Default.PropertyChanged += (object _, PropertyChangedEventArgs e) => {
					if (e.PropertyName != setting.Name) return;
					CreateRecorder();
				};
			}

			SystemEvents.PowerModeChanged += powerModeChanged;
			Application.Current.Exit += (_, _) => {
				SystemEvents.PowerModeChanged -= powerModeChanged;
			};

			monitorCheckTimer = new Timer((_) => {
				if (source == null) return;
				if (!(source is DisplayRecordingSource display)) return;
				string newMonitor = ExternMonitor.GetCurMonitor() ?? DisplayRecordingSource.MainMonitor.DeviceName;
				if (display.DeviceName == newMonitor) return;

				display.DeviceName = newMonitor;
				rec.GetDynamicOptionsBuilder().SetUpdatedRecordingSource(display).Apply();
				//Debug.WriteLine("udpated");
			}, null, 100, 100);
		}

		public static Recorder CreateRecorder() {
			if (stream != null) {
				stream.OnOverflow -= OnOverflow;
				//stream.Dispose(); // should get automatically disposed by the recorder

				Recorder oldRec = rec;
				VideoMemoryStream oldStream = stream;
				Application.Current.Dispatcher.BeginInvoke(() => {
					oldRec.Dispose();
					oldRec = null;
					oldStream.Dispose();
					oldStream = null;
				});
			}

			stream = new VideoMemoryStream();
			stream.MaxFrameCount = settings.Framerate * settings.ClipLength;

			source = new DisplayRecordingSource(DisplayRecordingSource.MainMonitor);
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
				},
				AudioOptions = new AudioOptions() {
					IsAudioEnabled = true,
					IsOutputDeviceEnabled = true,
					IsInputDeviceEnabled = settings.MicrophoneInputDevice != "None",
					AudioInputDevice = ClipSettings.GetInputDevice(),
					Bitrate = ClipSettings.GetAudioBitrate(),
					Channels = AudioChannels.Stereo
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
			};
			stream.OnChunkWritten += startedHandler;

			stream.OnOverflow += OnOverflow;

			return rec;
		}

		private static void OnOverflow(object _, EventArgs e) {
			CreateRecorder();
		}

		private static ScreenSize getBaseResolution() {
			Rect monitor = ExternMonitor.GetMonitorSize(ExternMonitor.GetMainMonitor());
			return new ScreenSize(monitor.Width, monitor.Height);
		}

		public async static Task EncodeClip(string path) {
			Debug.WriteLine("Encoding started");
			using MemoryStream videoStream = stream.GetFinalStream();
			videoStream.Position = 0;

			Debug.WriteLine("ffmpeg started");
			//File.WriteAllBytes(path, videoStream.ToArray());
			await FFMpegArguments
				.FromPipeInput(new StreamPipeSource(videoStream), (FFMpegArgumentOptions options) => options.WithCustomArgument($"-sseof -{settings.ClipLength}"))
				.OutputToFile(path, true, (FFMpegArgumentOptions options) => options.WithCustomArgument("-c copy"))
				.ProcessAsynchronously();

			Debug.WriteLine("video written");
		}

		public async static void Clip() {
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
					if (calls >= Math.Ceiling(stream.FrameCount / settings.Framerate * 0.1)) { // magic offset. idk it works kinda
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

			if (settings.CopyToClipboard) {
				Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection() { path });
			}
			if (notif != null) {
				notif.Label.Text = $"Clipped {Math.Round(stream.FrameCount / (double)settings.Framerate)} seconds";
				notif.StartFadeout();
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