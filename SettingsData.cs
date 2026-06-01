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
		}
		public class DropdownSettingType {
			public string[] Values;
		}
		public class FilePathSettingType { }
		public class KeybindSettingType { }
		public class BoolSettingType { }
		public class NumberSettingType {
			public int Minimum;
			public int Maximum;
		}
	}
	public class SettingsData {
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
					}
				}
			},
			new SettingsCategory() {
				Name = "Clipping",
				List = new List<SettingDefinition>() {
					new SettingDefinition() {
						DisplayName = "Clip duration",
						Name = "ClipLength",
						Type = new NumberSettingType() {
							Minimum = 1,
							Maximum = 600
						}
					},
					new SettingDefinition() {
						DisplayName = "Framerate",
						Name = "Framerate",
						Type = new NumberSettingType() {
							Minimum = 15,
							Maximum = 360
						}
					},
					new SettingDefinition() {
						DisplayName = "Video bitrate (kbps)",
						Name = "Bitrate",
						Type = new NumberSettingType() {
							Minimum = 96,
							Maximum = 100000
						}
					},
					new SettingDefinition() {
						DisplayName = "Audio bitrate",
						Name = "AudioBitrate",
						Type = new DropdownSettingType() {
							Values = ["96 kbps", "128 kbps", "160 kbps", "192 kbps"]
						}
					},
					new SettingDefinition() {
						DisplayName = "Video resolution",
						Name = "ClipResolution",
						Type = new DropdownSettingType() {
							Values = ["Source", "1080p", "720p", "480p", "360p", "240p", "144p"]
						}
					},
				}
			}
		};
	}
}
