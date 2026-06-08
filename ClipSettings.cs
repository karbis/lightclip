using ScreenRecorderLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.Security.Authentication.Web.Provider;

namespace lightclip {
	internal class ClipSettings {
		private static Properties.Settings settings = Properties.Settings.Default;

		public static AudioBitrate GetAudioBitrate() {
			return settings.AudioBitrate switch {
				"96 kbps" => AudioBitrate.bitrate_96kbps,
				"128 kbps" => AudioBitrate.bitrate_128kbps,
				"160 kbps" => AudioBitrate.bitrate_160kbps,
				"192 kbps" => AudioBitrate.bitrate_192kbps,
				_ => AudioBitrate.bitrate_128kbps,
			};
		}

		public static H264BitrateControlMode GetVideoBitrateType() {
			return settings.VideoQualityType switch {
				"Constant bitrate" => H264BitrateControlMode.CBR,
				"Variable bitrate" => H264BitrateControlMode.UnconstrainedVBR,
				"Quality" => H264BitrateControlMode.Quality,
				_ => H264BitrateControlMode.CBR
			};
		}

		public static Size GetResolution(Size baseResolution, string resolution) {
			if (resolution == "Source") return baseResolution;

			double maxResolution = getResolutionMinimum(resolution);
			double scaling = Math.Min(1, maxResolution / Math.Min(baseResolution.Width, baseResolution.Height));
			return new Size((int)(baseResolution.Width * scaling), (int)(baseResolution.Height * scaling));
		}

		public static ScreenSize GetClipResolution(ScreenSize baseResolution) {
			Size resolution = GetResolution(new Size(baseResolution.Width, baseResolution.Height), settings.ClipResolution);
			return new ScreenSize(resolution.Width, resolution.Height);
		}

		static double getResolutionMinimum(string resolution) {
			return resolution switch {
				"1080p" => 1080,
				"720p" => 720,
				"480p" => 480,
				"360p" => 360,
				"240p" => 240,
				"144p" => 144,
				_ => -1
			};
		}

		public static bool ShouldRescale(Size resolution, string scaledResolution) {
			if (scaledResolution == "Source") return false;
			double minimum = getResolutionMinimum(scaledResolution);
			return Math.Min(resolution.Width, resolution.Height) > minimum;
		}

		public static string GetInputDevice() {
			if (settings.MicrophoneInputDevice == "None" || settings.MicrophoneInputDevice == "Default") return null;

			foreach (AudioDevice device in Recorder.GetSystemAudioDevices(AudioDeviceSource.InputDevices)) {
				if (device.FriendlyName == settings.MicrophoneInputDevice) {
					return device.DeviceName;
				}
			}

			return null;
		}

		public static string GetOutputDevice() {
			if (settings.OutputDevice == "None" || settings.OutputDevice == "Default") return null;
			foreach (AudioDevice device in Recorder.GetSystemAudioDevices(AudioDeviceSource.OutputDevices)) {
				if (device.FriendlyName == settings.OutputDevice) {
					return device.DeviceName;
				}
			}
			return null;
		}

		public static string GetCurrentSourceName() {
			if (settings.MonitorInputSource == "Auto") {
				string monitor = ExternMonitor.GetCurMonitor();
				return monitor ?? DisplayRecordingSource.MainMonitor.DeviceName;
			} else if (settings.MonitorInputSource == "Default") {
				return DisplayRecordingSource.MainMonitor.DeviceName;
			} else {
				foreach (RecordableDisplay display in Recorder.GetDisplays()) {
					if (display.FriendlyName == settings.MonitorInputSource) {
						return display.DeviceName;
					}
				}

				return DisplayRecordingSource.MainMonitor.DeviceName;
			}
		}
		
		public static RecordingSourceBase GetCurrentSource() {
			return new DisplayRecordingSource(GetCurrentSourceName());
		}

		public static BaseVideoStream GetVideoStream() {
			return (settings.StreamType == "Disk") ? new VideoDiskStream() : new VideoMemoryStream();
		}

		public static float GetInputVolume() => settings.InputVolume / 100f;
		public static float GetOutputVolume() => settings.OutputVolume / 100f;
	}
}
