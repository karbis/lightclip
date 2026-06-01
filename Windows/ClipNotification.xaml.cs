using ScreenRecorderLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace lightclip {
	/// <summary>
	/// Interaction logic for ClipNotification.xaml
	/// </summary>
	public partial class ClipNotification : Window {
		public ClipNotification() {
			InitializeComponent();
		}

		public static ClipNotification PrevNotif = null;
		public static ClipNotification CreateNotification(string text) {
			if (PrevNotif != null) {
				PrevNotif.Close();
			}

			ClipNotification notif = new ClipNotification();
			notif.Label.Text = text;
			notif.Opacity = 0;

			notif.TweenOpacity(1, () => {});

			Rect size = ExternMonitor.GetMonitorSize(ExternMonitor.GetMonitorFromWindow(notif));
			notif.Left = size.Width - notif.Width - 10;
			notif.Top = size.Height - notif.Height - 10;

			notif.CloseButton.Click += (_, _) => {
				notif.Close();
			};

			notif.Loaded += (_, _) => {
				IntPtr hwnd = new WindowInteropHelper(notif).Handle;
				SetWindowLong(hwnd, GWL_EX_STYLE, (GetWindowLong(hwnd, GWL_EX_STYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW); // hide from alt tab
			};
			notif.Show();
			PrevNotif = notif;
			return notif;
		}

		public void StartFadeout() {
			delay(4.5, () => {
				TweenOpacity(0, () => {
					Close();
				});
			});
		}

		public DoubleAnimation TweenOpacity(double to, Action callback) {
			DoubleAnimation tween = new DoubleAnimation();
			tween.From = Opacity;
			tween.To = to;
			tween.Duration = new Duration(TimeSpan.FromSeconds(0.5));
			tween.EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut };
			tween.Completed += (_, _) => {
				callback();
			};
			BeginAnimation(Window.OpacityProperty, tween);
			return tween;
		}

		public static async void delay(double delay, Action func) {
			await Task.Delay((int)(delay * 1000));
			func();
		}

		[DllImport("user32.dll", SetLastError = true)]
		static extern int GetWindowLong(IntPtr hWnd, int nIndex);
		[DllImport("user32.dll")]
		static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
		private const int GWL_EX_STYLE = -20;
		private const int WS_EX_APPWINDOW = 0x00040000, WS_EX_TOOLWINDOW = 0x00000080;
	}
}
