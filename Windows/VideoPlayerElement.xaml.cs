using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace lightclip.Windows {
	/// <summary>
	/// Interaction logic for VideoPlayerElement.xaml
	/// </summary>
	public partial class VideoPlayerElement : UserControl {
		public event EventHandler VideoLoaded;
		public event EventHandler VideoEnded;

		public VideoPlayerElement() {
			InitializeComponent();

			// shortest c# class name
			WebView.WebMessageReceived += (object _, CoreWebView2WebMessageReceivedEventArgs e) => {
				string message = e.TryGetWebMessageAsString();
				if (message == "VideoLoaded") {
					VideoLoaded?.Invoke(this, null);
				} else if (message == "VideoEnded") {
					VideoEnded?.Invoke(this, null);
				} else {
					// ontimeupdate
					position = TimeSpan.FromSeconds(Convert.ToDouble(message, CultureInfo.InvariantCulture));
				}
			};
		}

		public async void Open(Uri source) {
			WebView.Source = source;

			await WebView.EnsureCoreWebView2Async();
			await WebView.ExecuteScriptAsync(@"
			let video = document.querySelector('video');
			video.controls = false;
			video.autoplay = false;
			video.oncanplaythrough = () => {
				chrome.webview.postMessage('VideoLoaded');
			};
			setInterval(() => {
				if (video.paused || video.ended) return;
				chrome.webview.postMessage(video.currentTime.toString());
			}, 16);
			video.onended = () => {
				chrome.webview.postMessage(video.currentTime.toString());
				chrome.webview.postMessage('VideoEnded');
			};
			");
		}

		public void Play() {
			WebView.ExecuteScriptAsync("document.querySelector('video').play();");
		}

		public void Pause() {
			WebView.ExecuteScriptAsync("document.querySelector('video').pause();");
		}

		public void Dispose() {
			WebView.Dispose();
		}

		private bool isMuted = false;
		public bool IsMuted {
			get => isMuted;
			set {
				isMuted = value;
				WebView.ExecuteScriptAsync($"document.querySelector('video').muted = {(value ? "true" : "false")};");
			}
		}

		private TimeSpan position = TimeSpan.Zero;
		public TimeSpan Position {
			get => position;
			set {
				position = value;
				WebView.ExecuteScriptAsync(FormattableString.Invariant($"document.querySelector('video').currentTime = {position.TotalSeconds}"));
			}
		}

		private double volume = 1;
		public double Volume {
			get => volume;
			set {
				volume = value;
				WebView.ExecuteScriptAsync(FormattableString.Invariant($"document.querySelector('video').volume = {volume};"));
			}
		}

		public async Task<TimeSpan> GetDuration() {
			string result = await WebView.ExecuteScriptAsync("document.querySelector('video').duration;");
			return TimeSpan.FromSeconds(Convert.ToDouble(result, CultureInfo.InvariantCulture));
		}

		public async Task<Size> GetResolution() {
			string width = await WebView.ExecuteScriptAsync("document.querySelector('video').videoWidth;");
			string height = await WebView.ExecuteScriptAsync("document.querySelector('video').videoHeight;");
			return new Size(Convert.ToDouble(width, CultureInfo.InvariantCulture), Convert.ToDouble(height, CultureInfo.InvariantCulture));
		}
	}
}
