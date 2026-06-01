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

namespace lightclip {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		GlobalKeyboardHook keyboardHook;
		TaskbarIcon icon;
		SettingsWindow settingsWindow = null;

		[STAThread]
		private void Start(object _, StartupEventArgs e) {
			downloadFfmpeg();
			ClipRecorder.Start();

			keyboardHook = new GlobalKeyboardHook();
			keyboardHook.KeyboardPressed += (object _, GlobalKeyboardHookEventArgs e) => {
				if (e.KeyboardState != GlobalKeyboardHook.KeyboardState.KeyDown) return;
				if (keyboardHook.GetFormattedKeyCode(e.KeyboardData) == lightclip.Properties.Settings.Default.Keybind) {
					ClipRecorder.Clip();
				}
			};

			icon = new TaskbarIcon();
			icon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Properties/icon.ico", UriKind.Absolute));
			icon.ToolTipText = "Lightclip";

			ContextMenu menu = new ContextMenu();
			menu.Items.Add(new MenuItem() { Header = "Lightclip" });
			menu.Items.Add(new Separator());

			MenuItem clipItem = new MenuItem() { Header = "Clip" };
			clipItem.Click += (_, _) => {
				ClipRecorder.Clip();
			};
			menu.Items.Add(clipItem);

			MenuItem settingsItem = new MenuItem() { Header = "Settings" };
			settingsItem.Click += (_, _) => {
				if (settingsWindow != null) {
					settingsWindow.Focus();
					return;
				}
				settingsWindow = new SettingsWindow();
				settingsWindow.Show();

				settingsWindow.Closed += (_, _) => {
					settingsWindow = null;
				};
			};
			menu.Items.Add(settingsItem);

			MenuItem exitItem = new MenuItem() { Header = "Exit" };
			exitItem.Click += (_, _) => {
				Shutdown();
			};
			menu.Items.Add(exitItem);

			icon.ContextMenu = menu;
			icon.TrayLeftMouseUp += (_, _) => {
				ClipRecorder.Clip();
			};

			if (lightclip.Properties.Settings.Default.OutputDirectory == "") {
				lightclip.Properties.Settings.Default.OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
				lightclip.Properties.Settings.Default.Save();
			}
		}

		private async void downloadFfmpeg() {
			try {
				if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg.exe"))) return;
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

				FFOptions options = new FFOptions();
				options.BinaryFolder = Directory.GetCurrentDirectory();

				List<string> files = await FFMpegDownloader.DownloadBinaries(binaries: FFMpegCore.Extensions.Downloader.Enums.FFMpegBinaries.FFMpeg, options: options);
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

		protected override void OnExit(ExitEventArgs e) {
			icon.Dispose();
			base.OnExit(e);
		}
	}
}
