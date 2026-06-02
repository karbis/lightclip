using ScreenRecorderLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

		public static ScreenSize GetResolution(ScreenSize baseResolution) {
			double maxResolution = 1;
			switch (settings.ClipResolution) {
				default:
				case "Source":
					return baseResolution;
				case "1080p":
					maxResolution = 1080;
					break;
				case "720p":
					maxResolution = 720;
					break;
				case "480p":
					maxResolution = 480;
					break;
				case "360p":
					maxResolution = 360;
					break;
				case "240p":
					maxResolution = 240;
					break;
				case "144p":
					maxResolution = 144;
					break;
			}

			double scaling = Math.Min(1, maxResolution / Math.Min(baseResolution.Width, baseResolution.Height));
			return new ScreenSize((int)(baseResolution.Width * scaling), (int)(baseResolution.Height * scaling));
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
	}
}
