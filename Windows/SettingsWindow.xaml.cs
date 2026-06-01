using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using lightclip.Settings;
using Microsoft.Win32;

namespace lightclip {
	/// <summary>
	/// Interaction logic for SettingsWindow.xaml
	/// </summary>
	public partial class SettingsWindow : Window {
		public SettingsWindow() {
			InitializeComponent();

			foreach (SettingsCategory category in SettingsData.Data) {
				Button button = CreateCategoryButton(category.Name);
				button.Click += (_, _) => {
					UpdateList(category);
				};
			}
			((Button)SettingCategories.Items[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		}

		public Button CreateCategoryButton(string name) {
			Button button = new Button() {
				Width = 136,
				Margin = new Thickness(-20, 0, 0, 0),
				Padding = new Thickness(20, 1, 0, 1),
				Content = name
			};
			SettingCategories.Items.Add(button);

			return button;
		}

		public void UpdateList(SettingsCategory category) {
			foreach (Button button in SettingCategories.Items) {
				button.Style = (Style)FindResource(((string)button.Content == category.Name) ? "SelectedButton" : "UnselectedButton");
			}

			SettingsList.Children.Clear();
			foreach (SettingDefinition setting in category.List) {
				CreateSetting(setting);
			}
		}

		public Grid CreateSetting(SettingDefinition setting) {
			Grid grid = new Grid();
			grid.Children.Add(new TextBlock() { Text = setting.DisplayName });

			object curVal = Properties.Settings.Default[setting.Name];
			Action<object> setSetting = (object val) => {
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
				TextBox box = new TextBox();
				box.Text = ((int)curVal).ToString();

				box.LostFocus += (_, _) => {
					int val;
					if (int.TryParse(box.Text, out val)) {
						setSetting(Math.Clamp(val, number.Minimum, number.Maximum));
					}
					box.Text = Properties.Settings.Default[setting.Name].ToString();
				};

				grid.Children.Add(box);
			} else if (setting.Type is FilePathSettingType) {
				TextBox box = new TextBox();
				box.Text = (string)curVal;

				box.LostFocus += (_, _) => {
					setSetting(box.Text);
				};
				box.GotFocus += (_, _) => {
					OpenFolderDialog dialog = new OpenFolderDialog();
					dialog.Title = "Select directory";
					dialog.Multiselect = false;
					dialog.DefaultDirectory = (string)Properties.Settings.Default[setting.Name];
					
					if (dialog.ShowDialog(this) == true) {
						setSetting(dialog.FolderName);
						box.Text = dialog.FolderName;
					}
				};

				grid.Children.Add(box);
			} else if (setting.Type is KeybindSettingType) {
				TextBox box = new TextBox();
				box.Text = (string)curVal;

				box.LostFocus += (_, _) => {
					if (box.Text == "...") {
						box.Text = (string)Properties.Settings.Default[setting.Name];
						return;
					}
					setSetting(box.Text);
				};
				box.GotFocus += (_, _) => {
					if (box.Text == "...") return;
					box.Text = "...";
					GlobalKeyboardHook hook = new GlobalKeyboardHook();
					hook.KeyboardPressed += (object _, GlobalKeyboardHookEventArgs e) => {
						if (e.KeyboardState != GlobalKeyboardHook.KeyboardState.KeyUp) return;
						if (hook.IsModifierKey(e.KeyboardData)) return;

						string key = hook.GetFormattedKeyCode(e.KeyboardData);
						hook.Dispose();

						if (box.Text != "...") return;
						box.Text = key;
						setSetting(key);
					}; ;
				};

				grid.Children.Add(box);
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
			}

			SettingsList.Children.Add(grid);
			return grid;
		}
	}
}
