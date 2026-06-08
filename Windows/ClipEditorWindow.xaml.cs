using FFMpegCore;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
		private static Properties.Settings settings = Properties.Settings.Default;
		private static ClipEditorWindow instance;
		CropPanel cropPanel;
		SettingsWindow settingsWindow = null;

		public ClipEditorWindow(string path) {
			if (instance != null) {
				instance.Close();
			}

			InitializeComponent();
			VideoPath = path;
			instance = this;

			VideoElement.Open(new Uri(path, UriKind.Absolute));

			Update();
			EventHandler handler = null;
			handler = async (_, _) => {
				_ = Task.Run(getVideoDuration);
				SetPlaying(false);
				updateVolume();
				VideoElement.VideoLoaded -= handler;

				cropPanel = new CropPanel(await VideoElement.GetResolution());
				((Grid)VideoElement.Parent).Children.Add(cropPanel);
			};
			VideoElement.VideoLoaded += handler;

			PlayButton.Click += (_, _) => {
				if (!VideoPlaying && VideoElement.Position.TotalSeconds + 0.01 >= TrimEnd) {
					VideoElement.Position = TimeSpan.Zero;
				}
				SetPlaying(!VideoPlaying);
			};

			VideoElement.VideoEnded += (_, _) => {
				SetPlaying(false);
				Update();
			};			

			CompositionTarget.Rendering += onFrameRendered;

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

			SettingsButton.Click += (_, _) => {
				if (settingsWindow != null) {
					settingsWindow.Close();
				}
				settingsWindow = new SettingsWindow("Clip editor");
				settingsWindow.Owner = this;
				settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
				settingsWindow.ShowDialog();
			};

			bool holdingCtrl = false;
			PreviewKeyDown += (object _, KeyEventArgs e) => {
				if (e.Key == Key.Space) {
					FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), this);
					PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
			if (saving || cropPanel == null) return;
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
			Rect crop = cropPanel.GetResolutionCrop();

			await FFMpegArguments
				.FromFileInput(VideoPath)
				.OutputToFile(tempFile, true, (FFMpegArgumentOptions options) => {
					options.Seek(TimeSpan.FromSeconds(TrimStart))
						.EndSeek(TimeSpan.FromSeconds(TrimEnd));

					bool applyCrop = crop.Size != cropPanel.Resolution;
					bool applyResolution = settings.ClipEditorResolution != "Source" && ClipSettings.ShouldRescale(cropPanel.Resolution, settings.ClipEditorResolution);
					if (applyCrop) {
						options.Crop((int)crop.Width, (int)crop.Height, (int)crop.Left, (int)crop.Top);
					}
					if (applyResolution) {
						Size size = ClipSettings.GetResolution(cropPanel.Resolution, settings.ClipEditorResolution);
						options.Resize((int)size.Width, (int)size.Height);
					}
					if (!applyCrop && !applyResolution) {
						options.WithCustomArgument("-c:v copy");
					}

					if (settings.ClipEditorVideoVolume != 100) {
						options.WithCustomArgument(FormattableString.Invariant($"-af \"volume={settings.ClipEditorVideoVolume / 100d}\""));
					} else {
						options.WithCustomArgument("-c:a copy");
					}
				})
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
			bool skimming = false;
			bool wasPlaying = false;
			Action<MouseEventArgs> onClick = (MouseEventArgs e) => {
				if (VideoPlaying) {
					SetPlaying(false);
				}

				Point pos = e.GetPosition(TrimBarBg);
				double width = TrimBarBg.ActualWidth;
				double progress = Math.Clamp(pos.X / width, 0, 1);
				double curTime = progress * videoDuration.TotalSeconds;
				update(curTime);

				VideoElement.Position = TimeSpan.FromSeconds(Math.Clamp(curTime, TrimStart, TrimEnd));
				Update();
			};
			Action onRelease = () => {
				skimming = false;
				if (wasPlaying) {
					SetPlaying(true);
				}
			};

			element.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) => {
				skimming = true;
				e.Handled = true;
				wasPlaying = VideoPlaying;
				onClick(e);
			};
			element.MouseLeftButtonUp += (object _, MouseButtonEventArgs e) => {
				onRelease();
				e.Handled = true;
			};

			MouseMove += (object _, MouseEventArgs e) => {
				if (!skimming) return;
				if (e.LeftButton != MouseButtonState.Pressed) {
					onRelease();
					return;
				}
				onClick(e);
			};
			MouseLeftButtonUp += (_, _) => {
				if (!skimming) return;
				onRelease();
			};
		}

		private void onFrameRendered(object _, EventArgs e) {
			if (!VideoPlaying) return;

			if (VideoElement.Position.TotalSeconds >= TrimEnd) {
				SetPlaying(false);
				VideoElement.Position = TimeSpan.FromSeconds(TrimEnd);
			}
			Update();
		}

		protected override void OnClosed(EventArgs e) {
			CompositionTarget.Rendering -= onFrameRendered;
			VideoElement.Dispose();
			settingsWindow = null;

			base.OnClosed(e);
		}

		public void UpdateBottomBar() {
			string icon = (VideoPlaying) ? "/Properties/PauseButton.png" : "/Properties/PlayButton.png";
			PlayButtonImage.Source = new BitmapImage(new Uri("pack://application:,,," + icon, UriKind.Absolute));

			PlaybackTimer.Text = $"{VideoElement.Position.ToString(@"m\:ss\.ff")} / {videoDuration.ToString(@"m\:ss\.ff")}";
			TrimLength.Text = TimeSpan.FromSeconds(GetClipLength()).ToString(@"m\:ss\.ff");
		}

		public void UpdatePlaybackBar() {
			double duration = videoDuration.TotalSeconds;
			double width = TrimBarBg.ActualWidth;
			TrimBar.Width = GetClipLength() / duration * width;
			TrimBar.Margin = new Thickness(TrimStart / duration * width, 0, 0, 0);
			TrimBarCircle.Margin = new Thickness(VideoElement.Position.TotalSeconds / duration * width - TrimBarCircle.Width / 2, 0, -6, 0);
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
				VideoElement.Position = TimeSpan.FromSeconds(Math.Clamp(VideoElement.Position.TotalSeconds, TrimStart, TrimEnd));
				VideoElement.Play();
			} else {
				VideoElement.Pause();
			}
			UpdateBottomBar();
		}

		public double GetClipLength() => (TrimEnd - TrimStart);

		async void getVideoDuration() {
			await VideoElement.Dispatcher.Invoke(async () => {
				videoDuration = await VideoElement.GetDuration();
			});
			TrimEnd = videoDuration.TotalSeconds;
			Dispatcher.Invoke(Update);
		}
	}
}
