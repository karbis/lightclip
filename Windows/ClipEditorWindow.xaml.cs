using FFMpegCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace lightclip.Windows {
	/// <summary>
	/// Interaction logic for ClipEditorWindow.xaml
	/// </summary>
	public partial class ClipEditorWindow : Window {
		public double TrimStart = 0;
		public double TrimEnd = 0;
		public bool VideoPlaying = false;
		public string VideoPath;
		TimeSpan videoDuration = TimeSpan.FromMilliseconds(0.1);
		TimeSpan videoPosition = TimeSpan.Zero;
		private static Properties.Settings settings = Properties.Settings.Default;

		public ClipEditorWindow(string path) {
			InitializeComponent();
			VideoPath = path;

			VideoElement.Source = new Uri(path, UriKind.Absolute);
			VideoElement.Play();
			VideoElement.IsMuted = true;
			VideoElement.ScrubbingEnabled = true;

			Update();
			VideoElement.MediaOpened += (_, _) => {
				VideoElement.Pause();
				VideoElement.Position = TimeSpan.Zero;
				VideoElement.IsMuted = false;
				SetPlaying(false);
			};

			PlayButton.Click += (_, _) => {
				if (!VideoPlaying && videoPosition.TotalSeconds + 0.01 >= TrimEnd) {
					VideoElement.Position = TimeSpan.Zero;
					videoPosition = TimeSpan.Zero;
				}
				SetPlaying(!VideoPlaying);
			};

			VideoElement.MediaEnded += (_, _) => {
				SetPlaying(false);
				videoPosition = videoDuration;
				UpdateBottomBar();
			};

			CompositionTarget.Rendering += (_, _) => {
				if (!VideoPlaying) return;
				if (VideoElement.Position.TotalSeconds == 0) return;
				videoPosition = VideoElement.Position;

				if (videoPosition.TotalSeconds >= TrimEnd) {
					SetPlaying(false);
					videoPosition = TimeSpan.FromSeconds(TrimEnd);
				}
				Update();
			};

			Task.Run(getVideoDuration);

			setUpDragEvent(SkimBar, (double progress) => {});

			setUpDragEvent(TrimMinBar, (double progress) => {
				TrimStart = Math.Min(progress, TrimEnd - 0.01);
			});
			setUpHoverEvents(TrimMinBar);

			setUpDragEvent(TrimMaxBar, (double progress) => {
				TrimEnd = Math.Max(progress, TrimStart + 0.01);
			});
			setUpHoverEvents(TrimMaxBar);

			SizeChanged += (_, _) => {
				Update();
			};

			Deactivated += (_, _) => {
				VolumePopup.IsOpen = false;
			};

			VolumeButton.Click += (_, _) => {
				VolumePopup.IsOpen = !VolumePopup.IsOpen;
			};

			VolumeSlider.Value = settings.ClipEditorVolume;
			updateVolume();
			VolumeSlider.ValueChanged += (_, _) => {
				settings.ClipEditorVolume = Convert.ToInt32(VolumeSlider.Value);
				updateVolume();
			};

			VolumePopup.Closed += (_, _) => {
				settings.Save();
			};

			SaveButton.Click += (_, _) => {
				Save();
			};

			bool holdingCtrl = false;
			PreviewKeyDown += (object _, KeyEventArgs e) => {
				if (e.Key == Key.Space && !PlayButton.IsKeyboardFocused) {
					FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), this);
					SetPlaying(!VideoPlaying);
				} else if (e.Key == Key.S && holdingCtrl) {
					SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				} else if (e.Key == Key.LeftCtrl) {
					holdingCtrl = true;
				};
			};
			PreviewKeyUp += (object _, KeyEventArgs e) => {
				if (e.Key == Key.LeftCtrl) {
					holdingCtrl = false;
				}
			};
		}

		bool saving = false;
		public async void Save() {
			if (saving) return;
			saving = true;

			ProcessingOverlay.Visibility = Visibility.Visible;

			void updateProgressBar(double progress) {
				ProcessingBar.Width = progress * 500;
				ProcessingText.Text = $"{Math.Floor(progress * 100)}%";
			}
			updateProgressBar(0);

			string fileName = Path.Combine(Path.GetDirectoryName(VideoPath), Path.GetFileNameWithoutExtension(VideoPath));
			string fileExtension = Path.GetExtension(VideoPath);
			string saveMode = settings.ClipEditorSaveMode;
			string tempFile = (saveMode == "Save new") ? $"{fileName}-Edit{fileExtension}" : $"{fileName}.temp{fileExtension}";
			string finalFile = (saveMode == "Save new") ? tempFile : VideoPath;

			await FFMpegArguments
				.FromFileInput(VideoPath)
				.OutputToFile(tempFile, true, (FFMpegArgumentOptions options) => options
					.Seek(TimeSpan.FromSeconds(TrimStart))
					.EndSeek(TimeSpan.FromSeconds(TrimEnd))
					.WithCustomArgument("-c copy")
				)
				.NotifyOnProgress((double percent) => Dispatcher.Invoke(() => updateProgressBar(percent / 100d)), TimeSpan.FromSeconds(GetClipLength()))
				.ProcessAsynchronously();

			try {
				if (saveMode == "Override") {
					File.Delete(VideoPath);
					File.Move(tempFile, VideoPath);
				}
				if (settings.CopyToClipboard) {
					Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection() { finalFile });
				}
			} catch (Exception e) {
				MessageBox.Show("Error while saving video\n" + e.ToString(), "Lightclip error");
			}

			if (settings.ShowNotification) {
				ClipNotification.CreateNotification("Clip edited.").StartFadeout();
			}
			Close();
		}

		private void setUpHoverEvents(Border border) {
			SolidColorBrush defaultColor = (SolidColorBrush)border.Background;
			SolidColorBrush hoverColor = new SolidColorBrush(Color.FromRgb(53, 104, 140));

			border.MouseEnter += (_, _) => {
				border.Background = hoverColor;
			};
			border.MouseLeave += (object _, MouseEventArgs e) => {
				if (e.LeftButton == MouseButtonState.Pressed) return;
				border.Background = defaultColor;
			};
			MouseLeftButtonUp += (_, _) => {
				if (border.IsMouseOver) return;
				border.Background = defaultColor;
			};
		}

		private void updateVolume() {
			VideoElement.Volume = settings.ClipEditorVolume / 100d;
		}

		private void setUpDragEvent(UIElement element, Action<double> update) {
			Action<MouseEventArgs> onClick = (MouseEventArgs e) => {
				if (VideoPlaying) {
					SetPlaying(false);
				}
				VideoElement.IsMuted = true;

				Point pos = e.GetPosition(TrimBarBg);
				double width = TrimBarBg.ActualWidth;
				double progress = Math.Clamp(pos.X / width, 0, 1);
				double curTime = progress * videoDuration.TotalSeconds;
				update(curTime);

				videoPosition = TimeSpan.FromSeconds(Math.Clamp(curTime, TrimStart, TrimEnd));
				VideoElement.Position = videoPosition;
				Update();
			};

			bool skimming = false;
			element.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) => {
				skimming = true;
				e.Handled = true;
				onClick(e);
			};
			element.MouseLeftButtonUp += (object _, MouseButtonEventArgs e) => {
				skimming = false;
				e.Handled = true;
			};

			MouseMove += (object _, MouseEventArgs e) => {
				if (!skimming) return;
				if (e.LeftButton != MouseButtonState.Pressed) {
					skimming = false;
					return;
				}
				onClick(e);
			};
		}

		public void UpdateBottomBar() {
			string icon = (VideoPlaying) ? "/Properties/PauseButton.png" : "/Properties/PlayButton.png";
			PlayButtonImage.Source = new BitmapImage(new Uri("pack://application:,,," + icon, UriKind.Absolute));

			PlaybackTimer.Text = $"{videoPosition.ToString(@"m\:ss\.ff")} / {videoDuration.ToString(@"m\:ss\.ff")}";
			TrimLength.Text = TimeSpan.FromSeconds(GetClipLength()).ToString(@"m\:ss\.ff");
		}

		public void UpdatePlaybackBar() {
			double duration = videoDuration.TotalSeconds;
			double width = TrimBarBg.ActualWidth;
			TrimBar.Width = GetClipLength() / duration * width;
			TrimBar.Margin = new Thickness(TrimStart / duration * width, 0, 0, 0);
			TrimBarCircle.Margin = new Thickness(videoPosition.TotalSeconds / duration * width - TrimBarCircle.Width / 2, 0, -6, 0);
			TrimMinBar.Margin = new Thickness(TrimStart / duration * width - TrimMinBar.Width, 0, 0, 0);
			TrimMaxBar.Margin = new Thickness(TrimEnd / duration * width, 0, -8, 0);

			Visibility visibilty = (duration < 0.01) ? Visibility.Hidden : Visibility.Visible;
			TrimMinBar.Visibility = visibilty;
			TrimMaxBar.Visibility = visibilty;
		}

		public void Update() {
			UpdateBottomBar();
			UpdatePlaybackBar();
		}

		public void SetPlaying(bool playing) {
			VideoPlaying = playing;
			if (playing) {
				VideoElement.Play();
				VideoElement.Position = TimeSpan.FromSeconds(Math.Clamp(videoPosition.TotalSeconds, TrimStart, TrimEnd));
				VideoElement.IsMuted = false;
			} else {
				VideoElement.Pause();
			}
			UpdateBottomBar();
		}

		public double GetClipLength() => (TrimEnd - TrimStart);

		void getVideoDuration() {
			IMediaAnalysis analysis = FFProbe.Analyse(VideoPath);
			videoDuration = analysis.Duration;
			TrimEnd = videoDuration.TotalSeconds;
			Dispatcher.Invoke(Update);
		}
	}
}
