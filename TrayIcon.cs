using Hardcodet.Wpf.TaskbarNotification;
using lightclip.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace lightclip {
	internal class TrayIcon {
		public static TaskbarIcon Icon;
		static SettingsWindow settingsWindow = null;
		
		public static void Create(Application app) {
			Icon = new TaskbarIcon();
			Icon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Properties/icon.ico", UriKind.Absolute));
			Icon.ToolTipText = "Lightclip";

			ContextMenu menu = new ContextMenu();

			MenuItem titleItem = new MenuItem() { Header = "Lightclip" };
			titleItem.Click += (_, _) => {
				openSettings(true);
			};
			menu.Items.Add(titleItem);
			menu.Items.Add(new Separator());

			MenuItem clipItem = new MenuItem() { Header = "Clip" };
			clipItem.Click += (_, _) => {
				ClipRecorder.Clip();
			};
			menu.Items.Add(clipItem);

			MenuItem editItem = new MenuItem() { Header = "Edit last clip" };
			editItem.Click += (_, _) => {
				if (!editItem.IsEnabled) return;
				new ClipEditorWindow(ClipRecorder.LastClipPath).Show();
			};
			menu.Items.Add(editItem);
			menu.Items.Add(new Separator());

			MenuItem settingsItem = new MenuItem() { Header = "Settings" };
			settingsItem.Click += (_, _) => {
				openSettings(false);
			};
			menu.Items.Add(settingsItem);

			MenuItem exitItem = new MenuItem() { Header = "Exit" };
			exitItem.Click += (_, _) => {
				app.Shutdown();
			};
			menu.Items.Add(exitItem);

			Icon.ContextMenu = menu;
			Icon.TrayLeftMouseUp += (_, _) => {
				ClipRecorder.Clip();
			};

			menu.Opened += (_, _) => {
				editItem.IsEnabled = ClipRecorder.LastClipPath != null;
			};
		}

		private static void openSettings(bool openInfo) {
			if (settingsWindow != null) {
				settingsWindow.Focus();
				return;
			}
			settingsWindow = new SettingsWindow((openInfo) ? "Info" : "General");
			settingsWindow.Show();

			settingsWindow.Closed += (_, _) => {
				settingsWindow = null;
			};
		}
	}
}
