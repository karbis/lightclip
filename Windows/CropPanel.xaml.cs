using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using Windows.ApplicationModel.VoiceCommands;

namespace lightclip.Windows
{
	/// <summary>
	/// Interaction logic for CropPanel.xaml
	/// </summary>
	public partial class CropPanel : UserControl {
		public Size Resolution;
		Rect cropRegion = new Rect(0, 0, 1, 1);
		static Properties.Settings settings = Properties.Settings.Default;
		const int MAX_SIZE = 50;

		public CropPanel(Size res) {
			InitializeComponent();
			Resolution = res;

			Update();
			SizeChanged += (_, _) => {
				Update();
			};

			PropertyChangedEventHandler onSettingChanged = (object _, PropertyChangedEventArgs e) => {
				if (e.PropertyName == "ClipEditorCropEditor") {
					Update();
				}
			};
			settings.PropertyChanged += onSettingChanged;
			Unloaded += (_, _) => {
				settings.PropertyChanged -= onSettingChanged;
			};

			setUpCropButton(TopLeftButton, "Left", "Top");
			setUpCropButton(TopRightButton, "Right", "Top");
			setUpCropButton(BottomLeftButton, "Left", "Bottom");
			setUpCropButton(BottomRightButton, "Right", "Bottom");

			setUpCropMoving();
		}

		public void Update() {
			double sizeMult = Math.Min(ActualWidth / Resolution.Width, ActualHeight / Resolution.Height);
			Canvas.Width = Resolution.Width * sizeMult;
			Canvas.Height = Resolution.Height * sizeMult;

			Rect cropRect = getCropRect();
			ScreenRectangle.Rect = new Rect(0, 0, Canvas.Width, Canvas.Height);
			((RectangleGeometry)Canvas.Resources["CropRegion"]).Rect = cropRect;

			double centerOffset = TopLeftButton.Width / 2;
			Canvas.SetLeft(TopLeftButton, cropRect.Left - centerOffset);
			Canvas.SetTop(TopLeftButton, cropRect.Top - centerOffset);
			Canvas.SetLeft(TopRightButton, cropRect.Right - centerOffset);
			Canvas.SetTop(TopRightButton, cropRect.Top - centerOffset);
			Canvas.SetLeft(BottomLeftButton, cropRect.Left - centerOffset);
			Canvas.SetTop(BottomLeftButton, cropRect.Bottom - centerOffset);
			Canvas.SetLeft(BottomRightButton, cropRect.Right - centerOffset);
			Canvas.SetTop(BottomRightButton, cropRect.Bottom - centerOffset);

			Visibility = (settings.ClipEditorCropEditor) ? Visibility.Visible : Visibility.Hidden;
			CropRegionPath.Cursor = (cropRegion.Size == new Size(1, 1)) ? null : Cursors.SizeAll;
		}

		void setUpCropMoving() {
			bool dragging = false;
			Rect curRect;
			Point curMousePos;
			CropRegionPath.PreviewMouseLeftButtonDown += (object _, MouseButtonEventArgs e) => {
				dragging = true;
				curRect = cropRegion;
				curMousePos = e.GetPosition(Canvas);
			};
			CropRegionPath.PreviewMouseLeftButtonUp += (_, _) => {
				dragging = false;
			};
			MouseLeftButtonUp += (_, _) => {
				dragging = false;
			};

			PreviewMouseMove += (object _, MouseEventArgs e) => {
				if (!dragging) return;
				if (e.LeftButton != MouseButtonState.Pressed) {
					dragging = false;
					return;
				}

				Point pos = e.GetPosition(Canvas);
				Point offset = (Point)(pos - curMousePos);
				cropRegion.X = curRect.X + offset.X / Canvas.ActualWidth;
				cropRegion.Y = curRect.Y + offset.Y / Canvas.ActualHeight;
				cropRegion.Width = curRect.Width;
				cropRegion.Height = curRect.Height;

				cropRegion = setMinSize(correctRect(cropRegion));
				Update();
			};
		}

		Rect correctRect(Rect rect) {
			double xOffset = Math.Max(0, rect.X) - rect.X;
			double yOffset = Math.Max(0, rect.Y) - rect.Y;
			double widthOffset = rect.Right - Math.Min(1, rect.Right);
			double heightOffset = rect.Bottom - Math.Min(1, rect.Bottom);
			return new Rect(Math.Max(0, rect.X), Math.Max(0, rect.Y), rect.Width - xOffset - widthOffset, rect.Height - yOffset - heightOffset);
		}

		Rect setMinSize(Rect rect) {
			rect.Width = Math.Max(MAX_SIZE / Resolution.Width, rect.Width);
			rect.Height = Math.Max(MAX_SIZE / Resolution.Height, rect.Height);
			rect.X = Math.Min(rect.X, 1 - rect.Width);
			rect.Y = Math.Min(rect.Y, 1 - rect.Height);
			return rect;
		}

		void setUpCropButton(Button button, string x, string y) {
			bool dragging = false;
			Rect curRect;
			button.PreviewMouseLeftButtonDown += (_, _) => {
				dragging = true;
				curRect = cropRegion;
			};
			button.PreviewMouseLeftButtonUp += (_, _) => {
				dragging = false;
			};
			MouseLeftButtonUp += (_, _) => {
				dragging = false;
			};

			if ((x == "Left" && y == "Top") || (x == "Right" && y == "Bottom")) {
				button.Cursor = Cursors.SizeNWSE;
			} else {
				button.Cursor = Cursors.SizeNESW;
			}

			PreviewMouseMove += (object _, MouseEventArgs e) => {
				if (!dragging) return;
				if (e.LeftButton != MouseButtonState.Pressed) {
					dragging = false;
					return;
				}

				Point pos = e.GetPosition(Canvas);
				double curX = Math.Clamp(pos.X / Canvas.ActualWidth, 0, 1);
				double curY = Math.Clamp(pos.Y / Canvas.ActualHeight, 0, 1);

				if (x == "Left") {
					cropRegion.Width = Math.Max(0, curRect.Width - (curX - curRect.X));
					cropRegion.X = Math.Min(curX, curRect.Right - MAX_SIZE / Resolution.Width);
				} else if (x == "Right") {
					cropRegion.Width = Math.Max(0, curX - cropRegion.X);
				}
				;

				if (y == "Top") {
					cropRegion.Height = Math.Max(0, curRect.Height - (curY - curRect.Y));
					cropRegion.Y = Math.Min(curY, curRect.Bottom - MAX_SIZE / Resolution.Height);
				} else if (y == "Bottom") {
					cropRegion.Height = Math.Max(0, curY - cropRegion.Y);
				}

				cropRegion = setMinSize(cropRegion);
				Update();
			};
		}

		Rect getCropRect() {
			return new Rect(cropRegion.X * Canvas.Width, cropRegion.Y * Canvas.Height, Canvas.Width * cropRegion.Width, Canvas.Height * cropRegion.Height);
		}

		public Rect GetResolutionCrop() {
			if (!settings.ClipEditorCropEditor) return new Rect(0, 0, Resolution.Width, Resolution.Height);
			return new Rect(cropRegion.X * Resolution.Width, cropRegion.Y * Resolution.Height, cropRegion.Width * Resolution.Width, cropRegion.Height * Resolution.Height);
		}
	}
}
