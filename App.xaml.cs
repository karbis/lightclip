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
using Microsoft.Win32;

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

		private async void downloadFfmpeg() {
			string binaryPath = Environment.ProcessPath ?? Directory.GetCurrentDirectory();
			if (File.Exists(binaryPath)) {
				binaryPath = Path.GetDirectoryName(binaryPath);
			}
			GlobalFFOptions.Current.BinaryFolder = binaryPath;

			try {
				if (File.Exists(Path.Combine(binaryPath, "ffmpeg.exe"))) return;
				string pathEnv = Environment.GetEnvironmentVariable("Path");
				if (pathEnv != null) {
					foreach (string path in pathEnv.Split(Path.PathSeparator)) {
						try {
							if (File.Exists(Path.Combine(path, "ffmpeg.exe"))) return;
						} catch {
							continue;
						}
					}
				}

				MessageBox.Show("FFMpeg has to be installed to encode the clips.\nIt will automatically be installed in the same directory as the exe. Continue?",
					"Lightclip", MessageBoxButton.OK, MessageBoxImage.Information);

				List<string> files = await FFMpegDownloader.DownloadBinaries(binaries: FFMpegCore.Extensions.Downloader.Enums.FFMpegBinaries.FFMpeg);
				if (files.Count != 0) {
					MessageBox.Show("FFMpeg has been installed.",
						"Lightclip", MessageBoxButton.OK, MessageBoxImage.Information);
				} else {
					MessageBox.Show("An error has occured while installing FFMpeg.", "Lightclip", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			} catch (Exception exception) {
				MessageBox.Show("Error while downloading ffmpeg\n" + exception.ToString(), "Lightclip");
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
