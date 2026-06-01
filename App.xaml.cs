using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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
			ClipRecorder.Start();

			keyboardHook = new GlobalKeyboardHook();
			keyboardHook.KeyboardPressed += (object _, GlobalKeyboardHookEventArgs e) => {
				if (e.KeyboardState != GlobalKeyboardHook.KeyboardState.KeyDown) return;
				if (keyboardHook.GetFormattedKeyCode(e.KeyboardData) == lightclip.Properties.Settings.Default.Keybind) {
					ClipRecorder.Clip();
				}
			};

			icon = new TaskbarIcon();
			icon.IconSource = new BitmapImage(new Uri("Properties/icon.ico", UriKind.Relative));
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

		protected override void OnExit(ExitEventArgs e) {
			icon.Dispose();
			base.OnExit(e);
		}
	}
}
