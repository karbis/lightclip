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
			public string Description = null;
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
		public class OpenClipEditorSettingType { }
		public class NumberSettingType {
			public int Minimum;
			public int Maximum;
			public string Unit;
		}
		public class SliderSettingType {
			public int Minimum;
			public int Maximum;
			public int SliderMinimum;
			public int SliderMaximum;
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
		public class OutputDeviceSettingType : DropdownSettingType {
			public override string[] Values {
				get {
					List<string> devices = ["None", "Default"];
					foreach (AudioDevice device in Recorder.GetSystemAudioDevices(AudioDeviceSource.OutputDevices)) {
						devices.Add(device.FriendlyName);
					}
					return devices.ToArray();
				}
				set => base.Values = value;
			}
		}
		public class MonitorInputSettingType : DropdownSettingType {
			public override string[] Values {
				get {
					List<string> devices = ["Auto", "Default"];
					foreach (RecordableDisplay device in Recorder.GetDisplays()) {
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
						DisplayName = "Copy clip to clipboard",
						Name = "CopyToClipboard",
						Type = new BoolSettingType()
					},
					new SettingDefinition() {
						DisplayName = "Launch app on startup",
						Name = "AddAppToStartup",
						Type = new BoolSettingType()
					},

					new SettingDefinition() { DisplayName = "Notifications", Name = "Bitrate", Type = new SeperatorSettingType() },
					new SettingDefinition {
						DisplayName = "Notification duration",
						Name = "NotificationDuration",
						Type = new SliderSettingType() {
							Minimum = 1,
							Maximum = 30,
							SliderMaximum = 20,
							SliderMinimum = 1,
							Unit = "s"
						}
					},
					new SettingDefinition() {
						DisplayName = "Show clip notification",
						Name = "ShowNotification",
						Type = new BoolSettingType()
					}
				}
			},
			new SettingsCategory() {
				Name = "Video",
				List = new List<SettingDefinition>() {
					new SettingDefinition() {
						DisplayName = "Clip duration",
						Name = "ClipLength",
						Description = "Sets the length of clips.\nHigher clip durations will lead to higher memory usage (if buffer is on Memory).",
						Type = new NumberSettingType() {
							Minimum = 2,
							Maximum = 600,
							Unit = "sec"
						}
					},
					new SettingDefinition() {
						DisplayName = "Capture source",
						Name = "MonitorInputSource",
						Description = "Sets which monitor it records.\nAuto will automatically switch between monitors depending on mouse position.",
						Type = new MonitorInputSettingType(),
					},

					new SettingDefinition() { DisplayName = "Quality", Name = "Bitrate", Type = new SeperatorSettingType() },
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
						DisplayName = "Resolution",
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

					new SettingDefinition() { DisplayName = "Encoding", Name = "Bitrate", Type = new SeperatorSettingType() },
					new SettingDefinition() {
						DisplayName = "Encoding processor",
						Name = "EncodingType",
						Description = "Sets which processing unit to use to encode the video.\nCPU will lead to lower GPU usage, but higher CPU usage.",
						Type = new DropdownSettingType() {
							Values = ["GPU", "CPU"]
						}
					},
					new SettingDefinition() {
						DisplayName = "Buffer location",
						Name = "StreamType",
						Description = "Sets where to store the video buffer.\nMemory will lead to higher memory usage, but Disk might wear down your storage device.\nMemory is recommended for short clips, while Disk is recommended for long clips.",
						Type = new DropdownSettingType() {
							Values = ["Memory", "Disk"]
						}
					},
				}
			},
			new SettingsCategory() {
				Name = "Audio",
				List = new List<SettingDefinition>() {
					new SettingDefinition() {
						DisplayName = "Audio bitrate",
						Name = "AudioBitrate",
						Type = new DropdownSettingType() {
							Values = ["96 kbps", "128 kbps", "160 kbps", "192 kbps"]
						}
					},

					new SettingDefinition() { DisplayName = "Output", Name = "Bitrate", Type = new SeperatorSettingType() },
					new SettingDefinition() {
						DisplayName = "Output device",
						Name = "OutputDevice",
						Type = new OutputDeviceSettingType()
					},
					new SettingDefinition {
						DisplayName = "Output volume",
						Name = "OutputVolume",
						Type = new SliderSettingType() {
							Minimum = 0,
							Maximum = 200,
							SliderMaximum = 100,
							SliderMinimum = 0,
							Unit = "%"
						}
					},

					new SettingDefinition() { DisplayName = "Input", Name = "Bitrate", Type = new SeperatorSettingType() },
					new SettingDefinition() {
						DisplayName = "Input device",
						Name = "MicrophoneInputDevice",
						Type = new InputDeviceSettingType()
					},
					new SettingDefinition {
						DisplayName = "Input volume",
						Name = "InputVolume",
						Type = new SliderSettingType() {
							Minimum = 0,
							Maximum = 1000,
							SliderMaximum = 200,
							SliderMinimum = 0,
							Unit = "%"
						}
					}
				}
			},
			new SettingsCategory() {
				Name = "Clip editor",
				List = new List<SettingDefinition>() {
					new SettingDefinition() {
						DisplayName = "Save mode",
						Name = "ClipEditorSaveMode",
						Description = "Changes how saving behaves in the clip editor.\nOverride overides the clip file, Save new creates a new edited video file.",
						Type = new DropdownSettingType() {
							Values = ["Override", "Save new"]
						}
					},
					new SettingDefinition() {
						DisplayName = "Output resolution",
						Name = "ClipEditorResolution",
						Type = new DropdownSettingType() {
							Values = ["Source", "1080p", "720p", "480p", "360p", "240p", "144p"]
						}
					},
					new SettingDefinition() {
						DisplayName = "Output volume",
						Name = "ClipEditorVideoVolume",
						Description = "Changes the volume of the edited clip.",
						Type = new SliderSettingType() {
							Minimum = 0,
							Maximum = 200,
							SliderMaximum = 100,
							SliderMinimum = 0,
							Unit = "%"
						}
					},
					new SettingDefinition() {
						DisplayName = "Show crop editor",
						Description = "Changes whether to show the crop boundaries in the clip editor.",
						Name = "ClipEditorCropEditor",
						Type = new BoolSettingType()
					}
				}
			}
		};
	}
}
