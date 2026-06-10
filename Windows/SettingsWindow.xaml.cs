using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using lightclip.Settings;
using Microsoft.Win32;

namespace lightclip {
	/// <summary>
	/// Interaction logic for SettingsWindow.xaml
	/// </summary>
	public partial class SettingsWindow : Window {
		GlobalKeyboardHook hook = null;
		Dictionary<SettingDefinition, UIElement> settingUiMap = new();
		string defaultCategory = null;

		public SettingsWindow(string openedCategory = "General") {
			InitializeComponent();
			defaultCategory = openedCategory;

			foreach (SettingsCategory category in SettingsData.Data) {
				Button button = CreateCategoryButton(category);
			}
			CreateCategoryButton(new SettingsCategory() { Name = "Info" });

			int openedIndex = 0;
			foreach (SettingsCategory category in SettingsData.Data) {
				if (category.Name == openedCategory) break;
				openedIndex++;
			}
			((Button)SettingCategories.Items[openedIndex]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

			Properties.Settings.Default.PropertyChanged += propertyChanged;
		}

		private void propertyChanged(object _, PropertyChangedEventArgs e) {
			foreach (KeyValuePair<SettingDefinition, UIElement> pair in settingUiMap) {
				UpdateSettingVisibility(pair.Key);
			}
		}

		protected override void OnClosed(EventArgs e) {
			Properties.Settings.Default.PropertyChanged -= propertyChanged;
			if (hook != null) {
				hook.Dispose();
			}

			base.OnClosed(e);
		}

		public Button CreateCategoryButton(SettingsCategory category) {
			Button button = new Button() {
				Width = 136,
				Margin = new Thickness(-20, 0, 0, 0),
				Padding = new Thickness(20, 1, 0, 1),
				Content = category.Name
			};

			button.Click += (_, _) => {
				UpdateList(category);
			};

			SettingCategories.Items.Add(button);
			return button;
		}

		public void UpdateList(SettingsCategory category) {
			foreach (Button button in SettingCategories.Items) {
				button.Style = (Style)FindResource(((string)button.Content == category.Name) ? "SelectedButton" : "UnselectedButton");
			}

			if (category.Name == "Info") {
				SettingsList.Visibility = Visibility.Collapsed;
				InfoPanel.Visibility = Visibility.Visible;
				return;
			} else {
				SettingsList.Visibility = Visibility.Visible;
				InfoPanel.Visibility = Visibility.Collapsed;
			}

			SettingsList.Children.Clear();
			settingUiMap.Clear();
			CreateSetting(new SettingDefinition() { DisplayName = category.Name, Name = "Bitrate", Type = new SeperatorSettingType() });
			foreach (SettingDefinition setting in category.List) {
				CreateSetting(setting);
			}

			if (category.Name == "Clip editor" && defaultCategory != "Clip editor") {
				CreateSetting(new SettingDefinition() { 
					DisplayName = "Open video in clip editor",
					Description = "Open a video in the clip editor.",
					Name = "Bitrate",
					Type = new OpenClipEditorSettingType()
				});
			}
		}

		public Grid CreateSetting(SettingDefinition setting) {
			Grid grid = new Grid();
			TextBlock settingName = new TextBlock() { Text = setting.DisplayName };
			grid.Children.Add(settingName);
			if (setting.Description != null) {
				settingName.ToolTip = setting.Description;
			}

			object curVal = Properties.Settings.Default[setting.Name];
			Action<object> setSetting = (object val) => {
				if (Properties.Settings.Default[setting.Name].Equals(val)) return;
				Properties.Settings.Default[setting.Name] = val;
				Properties.Settings.Default.Save();
			};
			if (setting.Type is DropdownSettingType dropdown) {
				ComboBox box = new ComboBox();
				foreach (string value in dropdown.Values) {
					box.Items.Add(value);
				}

				box.SelectedItem = (string)curVal;
				box.SelectionChanged += (_, _) => {
					setSetting((string)box.SelectedItem);
				};

				grid.Children.Add(box);
			} else if (setting.Type is NumberSettingType number) {
				DockPanel panel = new DockPanel();
				TextBlock text = new TextBlock() { Text = number.Unit, Margin = new Thickness(4, 4, 0, 0), Width = 25 };
				TextBox box = new TextBox() { Width = 125 - 29 };
				box.Text = ((int)curVal).ToString();

				box.LostFocus += (_, _) => {
					int val;
					if (int.TryParse(box.Text, out val)) {
						setSetting(Math.Clamp(val, number.Minimum, number.Maximum));
					}
					box.Text = Properties.Settings.Default[setting.Name].ToString();
				};

				panel.Children.Add(box);
				panel.Children.Add(text);
				grid.Children.Add(panel);
			} else if (setting.Type is FilePathSettingType) {
				DockPanel panel = new DockPanel();
				Button button = new Button() { Content = "Select" };
				TextBox box = new TextBox();
				box.Text = (string)curVal;

				box.LostFocus += (_, _) => {
					setSetting(box.Text);
				};
				button.Click += (_, _) => {
					OpenFolderDialog dialog = new OpenFolderDialog();
					dialog.Title = "Select directory";
					dialog.Multiselect = false;
					dialog.DefaultDirectory = (string)Properties.Settings.Default[setting.Name];
					
					if (dialog.ShowDialog(this) == true) {
						setSetting(dialog.FolderName);
						box.Text = dialog.FolderName;
					}
				};

				panel.Children.Add(button);
				panel.Children.Add(box);
				grid.Children.Add(panel);
			} else if (setting.Type is KeybindSettingType) {
				DockPanel panel = new DockPanel();
				Button button = new Button() { Content = "Select" };
				TextBox box = new TextBox();
				box.Text = (string)curVal;

				box.LostFocus += (_, _) => {
					if (box.Text == "...") {
						box.Text = (string)Properties.Settings.Default[setting.Name];
						return;
					}
					setSetting(box.Text);
				};
				button.Click += (_, _) => {
					if (box.Text == "...") return;
					box.Text = "...";
					hook = new GlobalKeyboardHook();
					hook.KeyboardPressed += (object _, GlobalKeyboardHookEventArgs e) => {
						if (e.KeyboardState != GlobalKeyboardHook.KeyboardState.KeyUp) return;
						if (hook.IsModifierKey(e.KeyboardData)) return;

						string key = hook.GetFormattedKeyCode(e.KeyboardData);
						hook.Dispose();

						if (box.Text != "...") return;
						box.Text = key;
						setSetting(key);
					};
				};

				panel.Children.Add(button);
				panel.Children.Add(box);
				grid.Children.Add(panel);
			} else if (setting.Type is BoolSettingType) {
				CheckBox box = new CheckBox();
				box.IsChecked = (bool)curVal;

				box.Checked += (_, _) => {
					setSetting(box.IsChecked);
				};
				box.Unchecked += (_, _) => {
					setSetting(box.IsChecked);
				};

				grid.Children.Add(box);
			} else if (setting.Type is SeperatorSettingType) {
				settingName.FontWeight = FontWeights.SemiBold;
				grid.Margin = new Thickness(0, 0, 0, 1);
			} else if (setting.Type is SliderSettingType sliderSetting) {
				DockPanel panel = new DockPanel();
				TextBox box = new TextBox() { Width = 50 };
				Slider slider = new Slider() { Minimum = sliderSetting.SliderMinimum, Maximum = sliderSetting.SliderMaximum };
				Action updateText = () => {
					box.Text = (int)Properties.Settings.Default[setting.Name] + sliderSetting.Unit;
				};
				updateText();
				slider.Value = Convert.ToDouble(curVal);

				box.GotFocus += (_, _) => {
					box.Text = Properties.Settings.Default[setting.Name].ToString();
				};
				box.LostFocus += (_, _) => {
					int val;
					if (int.TryParse(box.Text, out val)) {
						setSetting(Math.Clamp(val, sliderSetting.Minimum, sliderSetting.Maximum));
					}
					updateText();
					slider.Value = (int)Properties.Settings.Default[setting.Name];
				};

				slider.ValueChanged += (_, _) => {
					int curVal = (int)Properties.Settings.Default[setting.Name];
					if (curVal > sliderSetting.SliderMaximum || curVal < sliderSetting.SliderMinimum) return;

					box.Text = (int)slider.Value + sliderSetting.Unit;
				};
				slider.PreviewMouseUp += (_, _) => {
					setSetting((int)slider.Value);
					updateText();
				};

				panel.Children.Add(box);
				panel.Children.Add(slider);
				grid.Children.Add(panel);
			} else if (setting.Type is OpenClipEditorSettingType) {
				Button button = new Button() { Content = "Open", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness() };
				button.Click += (_, _) => {
					OpenFileDialog dialog = new OpenFileDialog();
					dialog.Title = "Select video";
					dialog.Multiselect = false;
					dialog.CheckFileExists = true;
					dialog.CheckPathExists = true;
					dialog.Filter = "Video files (*.mp4, *.webm)|*.mp4;*.webm";
					dialog.DefaultDirectory = Properties.Settings.Default.OutputDirectory;

					if (dialog.ShowDialog() == true) {
						Close();
						new Windows.ClipEditorWindow(dialog.FileName).Show();
					}
				};
				grid.Children.Add(button);
			}

			SettingsList.Children.Add(grid);
			settingUiMap[setting] = grid;
			UpdateSettingVisibility(setting);

			return grid;
		}

		public void UpdateSettingVisibility(SettingDefinition setting) {
			if (setting.VisibleCheck == null) return;
			settingUiMap[setting].Visibility = (setting.VisibleCheck()) ? Visibility.Visible : Visibility.Collapsed;
		}
	}
}
