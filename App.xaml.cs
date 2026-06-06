using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Extensions.Downloader;
using Hardcodet.Wpf.TaskbarNotification;
using lightclip.Windows;
using Microsoft.Win32;
using FFMpegCore.Extensions.Downloader.Enums;

namespace lightclip {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		GlobalKeyboardHook keyboardHook;
		lightclip.Properties.Settings settings = lightclip.Properties.Settings.Default;

		[STAThread]
		private void Start(object _, StartupEventArgs e) {
			downloadFfmpeg();
			ClipRecorder.Start();

			keyboardHook = new GlobalKeyboardHook();
			keyboardHook.KeyboardPressed += (object _, GlobalKeyboardHookEventArgs e) => {
				if (e.KeyboardState != GlobalKeyboardHook.KeyboardState.KeyDown) return;
				if (keyboardHook.GetFormattedKeyCode(e.KeyboardData) == settings.Keybind) {
					ClipRecorder.Clip();
				}
			};

			TrayIcon.Create(this);

			if (settings.OutputDirectory == "") {
				settings.OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
				settings.Save();
			}

			addAppToStartup(settings.AddAppToStartup);
			settings.PropertyChanged += (object _, PropertyChangedEventArgs e) => {
				if (e.PropertyName != "AddAppToStartup") return;
				addAppToStartup(settings.AddAppToStartup);
			};
		}

		private bool checkForExe(string name) {
			if (File.Exists(Path.Combine(GlobalFFOptions.Current.BinaryFolder, name))) return true;
			string pathEnv = Environment.GetEnvironmentVariable("Path");
			if (pathEnv != null) {
				foreach (string path in pathEnv.Split(Path.PathSeparator)) {
					try {
						if (File.Exists(Path.Combine(path, name))) return true;
					} catch {
						continue;
					}
				}
			}
			return false;
		}

		private async void downloadFfmpeg() {
			string binaryPath = Environment.ProcessPath ?? Directory.GetCurrentDirectory();
			if (File.Exists(binaryPath)) {
				binaryPath = Path.GetDirectoryName(binaryPath);
			}
			GlobalFFOptions.Current.BinaryFolder = binaryPath;

			try {
				if (checkForExe("ffmpeg.exe") && checkForExe("ffprobe.exe")) return;

				MessageBox.Show("FFMpeg binaries have to be installed to encode the clips.\nThey will automatically be installed in the same directory as the exe. Continue?",
					"Lightclip", MessageBoxButton.OK, MessageBoxImage.Information);

				FFMpegBinaries requiredBinaries;
				if (!checkForExe("ffmpeg.exe") && !checkForExe("ffprobe.exe")) {
					requiredBinaries = FFMpegBinaries.FFMpeg | FFMpegBinaries.FFProbe;
				} else if (!checkForExe("ffprobe.exe")) {
					requiredBinaries = FFMpegBinaries.FFProbe;
				} else {
					requiredBinaries = FFMpegBinaries.FFMpeg;
				}
				Debug.WriteLine(requiredBinaries);

				List<string> files = await FFMpegDownloader.DownloadBinaries(binaries: requiredBinaries);
				if (files.Count != 0) {
					MessageBox.Show("FFMpeg has been installed.",
						"Lightclip", MessageBoxButton.OK, MessageBoxImage.Information);
				} else {
					MessageBox.Show("An error has occured while installing FFMpeg.", "Lightclip", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			} catch (Exception exception) {
				MessageBox.Show("Error while downloading FFMpeg\n" + exception.ToString(), "Lightclip");
			}
		}

		private void addAppToStartup(bool add) {
			RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
			if (key == null) return;
			
			if (add) {
				key.SetValue("Lightclip", Environment.ProcessPath);
			} else {
				key.DeleteValue("Lightclip", false);
			}
		}

		protected override void OnExit(ExitEventArgs e) {
			TrayIcon.Icon.Dispose();
			base.OnExit(e);
		}
	}
}
