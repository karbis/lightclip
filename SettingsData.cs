using lightclip.Settings;
using ScreenRecorderLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lightclip {
	namespace Settings {
		public class SettingsCategory {
			public string Name;
			public List<SettingDefinition> List;
		}
		public class SettingDefinition {
			public string DisplayName;
			public string Name;
			public object Type;
			public Func<bool> VisibleCheck = null;
		}
		public class DropdownSettingType {
			public virtual string[] Values { get; set; }
		}
		public class FilePathSettingType { }
		public class KeybindSettingType { }
		public class BoolSettingType { }
		public class SeperatorSettingType { }
		public class NumberSettingType {
			public int Minimum;
			public int Maximum;
			public string Unit;
		}
		public class InputDeviceSettingType : DropdownSettingType {
			public override string[] Values {
				get {
					List<string> devices = ["None", "Default"];
					foreach (AudioDevice device in Recorder.GetSystemAudioDevices(AudioDeviceSource.InputDevices)) {
						devices.Add(device.FriendlyName);
					}

					return devices.ToArray();
				}
				set => base.Values = value;
			}
		}
	}
	public class SettingsData {
		static Properties.Settings settings = Properties.Settings.Default;
		public static List<SettingsCategory> Data = new List<SettingsCategory> {
			new SettingsCategory() {
				Name = "General",
				List = new List<SettingDefinition>() {
					new SettingDefinition() {
						DisplayName = "Output directory",
						Name = "OutputDirectory",
						Type = new FilePathSettingType()
					},
					new SettingDefinition() {
						DisplayName = "Keybind",
						Name = "Keybind",
						Type = new KeybindSettingType()
					},
					new SettingDefinition() {
						DisplayName = "Show clip notification",
						Name = "ShowNotification",
						Type = new BoolSettingType()
					},
					new SettingDefinition() {
						DisplayName = "Copy clip to clipboard",
						Name = "CopyToClipboard",
						Type = new BoolSettingType()
					},
					new SettingDefinition() {
						DisplayName = "Launch app on startup",
						Name = "AddAppToStartup",
						Type = new BoolSettingType()
					}
				}
			},
			new SettingsCategory() {
				Name = "Clipping",
				List = new List<SettingDefinition>() {
					new SettingDefinition() { DisplayName = "Video", Name = "Bitrate", Type = new SeperatorSettingType() },
					new SettingDefinition() {
						DisplayName = "Clip duration",
						Name = "ClipLength",
						Type = new NumberSettingType() {
							Minimum = 2,
							Maximum = 600,
							Unit = "sec"
						}
					},
					new SettingDefinition() {
						DisplayName = "Framerate",
						Name = "Framerate",
						Type = new NumberSettingType() {
							Minimum = 15,
							Maximum = 360,
							Unit = "fps"
						}
					},
					new SettingDefinition() {
						DisplayName = "Video resolution",
						Name = "ClipResolution",
						Type = new DropdownSettingType() {
							Values = ["Source", "1080p", "720p", "480p", "360p", "240p", "144p"]
						}
					},
					new SettingDefinition() {
						DisplayName = "Video quality type",
						Name = "VideoQualityType",
						Type = new DropdownSettingType() {
							Values = ["Constant bitrate", "Variable bitrate", "Quality"]
						}
					},
					new SettingDefinition() {
						DisplayName = "Video bitrate",
						Name = "Bitrate",
						Type = new NumberSettingType() {
							Minimum = 96,
							Maximum = 100000,
							Unit = "kbps"
						},
						VisibleCheck = () => settings.VideoQualityType == "Constant bitrate" || settings.VideoQualityType == "Variable bitrate"
					},
					new SettingDefinition() {
						DisplayName = "Video quality",
						Name = "VideoQualityPercent",
						Type = new NumberSettingType() {
							Minimum = 1,
							Maximum = 100,
							Unit = "%"
						},
						VisibleCheck = () => settings.VideoQualityType == "Quality"
					},

					new SettingDefinition() { DisplayName = "Audio", Name = "Bitrate", Type = new SeperatorSettingType() },
					new SettingDefinition() {
						DisplayName = "Audio bitrate",
						Name = "AudioBitrate",
						Type = new DropdownSettingType() {
							Values = ["96 kbps", "128 kbps", "160 kbps", "192 kbps"]
						}
					},
					new SettingDefinition() {
						DisplayName = "Input device",
						Name = "MicrophoneInputDevice",
						Type = new InputDeviceSettingType()
					}
				}
			}
		};
	}
}
