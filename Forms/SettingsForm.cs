using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MonitorSwap.Models;
using MonitorSwap.Native;
using MonitorSwap.Services;

namespace MonitorSwap.Forms
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private readonly AppLanguage _originalLanguage;
        private readonly MonitorDisplayService _monitorDisplayService;
        private readonly Icon _formIcon;
        private readonly ComboBox _languageComboBox;
        private readonly Label _languageLabel;
        private readonly GroupBox _hotkeyGroup;
        private readonly Label _rotateLeftLabel;
        private readonly Label _rotateRightLabel;
        private readonly TextBox _leftHotkeyTextBox;
        private readonly TextBox _rightHotkeyTextBox;
        private readonly Button _leftCaptureButton;
        private readonly Button _rightCaptureButton;
        private readonly Label _captureStatusLabel;
        private readonly GroupBox _optionGroup;
        private readonly CheckBox _includeMinimizedCheckBox;
        private readonly CheckBox _startWithWindowsCheckBox;
        private readonly CheckBox _preserveOrderCheckBox;
        private readonly GroupBox _monitorGroup;
        private readonly CheckedListBox _monitorList;
        private readonly Label _monitorHelpLabel;
        private readonly Button _saveButton;
        private readonly Button _cancelButton;
        private CaptureTarget _captureTarget;
        private CaptureStatusState _captureStatusState;
        private bool _suppressLanguageSelectionChanged;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings.Clone();
            _settings.EnsureDefaults();
            _originalLanguage = _settings.GetUiLanguage();
            _monitorDisplayService = new MonitorDisplayService();

            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(580, 520);
            Size = new Size(640, 560);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            KeyPreview = true;
            _formIcon = AppIconProvider.CreateAppIcon();
            Icon = _formIcon;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var languagePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 8)
            };
            languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            _languageLabel = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 12, 0)
            };
            _languageComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Left,
                Width = 160
            };
            _languageComboBox.Items.Add(new LanguageItem(AppLanguage.English, "English"));
            _languageComboBox.Items.Add(new LanguageItem(AppLanguage.Korean, "\uD55C\uAD6D\uC5B4"));
            _languageComboBox.SelectedIndexChanged += OnLanguageSelectionChanged;
            languagePanel.Controls.Add(_languageLabel, 0, 0);
            languagePanel.Controls.Add(_languageComboBox, 1, 0);
            root.Controls.Add(languagePanel, 0, 0);

            _hotkeyGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                AutoSize = true
            };
            root.Controls.Add(_hotkeyGroup, 0, 1);

            var hotkeyLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                AutoSize = true,
                Padding = new Padding(10)
            };
            hotkeyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            hotkeyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            hotkeyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            _hotkeyGroup.Controls.Add(hotkeyLayout);

            _rotateLeftLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
            hotkeyLayout.Controls.Add(_rotateLeftLabel, 0, 0);
            _leftHotkeyTextBox = CreateReadOnlyTextBox();
            hotkeyLayout.Controls.Add(_leftHotkeyTextBox, 1, 0);
            _leftCaptureButton = CreateCaptureButton(CaptureTarget.Left);
            hotkeyLayout.Controls.Add(_leftCaptureButton, 2, 0);

            _rotateRightLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
            hotkeyLayout.Controls.Add(_rotateRightLabel, 0, 1);
            _rightHotkeyTextBox = CreateReadOnlyTextBox();
            hotkeyLayout.Controls.Add(_rightHotkeyTextBox, 1, 1);
            _rightCaptureButton = CreateCaptureButton(CaptureTarget.Right);
            hotkeyLayout.Controls.Add(_rightCaptureButton, 2, 1);

            _captureStatusLabel = new Label
            {
                Dock = DockStyle.Top,
                Padding = new Padding(3, 8, 3, 0),
                AutoSize = true
            };
            hotkeyLayout.Controls.Add(_captureStatusLabel, 0, 2);
            hotkeyLayout.SetColumnSpan(_captureStatusLabel, 3);

            _optionGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                AutoSize = true
            };
            root.Controls.Add(_optionGroup, 0, 2);

            var optionLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                Padding = new Padding(10),
                WrapContents = false
            };
            _optionGroup.Controls.Add(optionLayout);

            _includeMinimizedCheckBox = new CheckBox
            {
                AutoSize = true,
                Checked = _settings.IncludeMinimizedWindows
            };
            optionLayout.Controls.Add(_includeMinimizedCheckBox);

            _startWithWindowsCheckBox = new CheckBox
            {
                AutoSize = true,
                Checked = _settings.StartWithWindows
            };
            optionLayout.Controls.Add(_startWithWindowsCheckBox);

            _preserveOrderCheckBox = new CheckBox
            {
                AutoSize = true,
                Checked = _settings.PreserveWindowOrder
            };
            optionLayout.Controls.Add(_preserveOrderCheckBox);

            _monitorGroup = new GroupBox
            {
                Dock = DockStyle.Fill
            };
            root.Controls.Add(_monitorGroup, 0, 3);

            _monitorList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false
            };
            _monitorGroup.Controls.Add(_monitorList);

            _monitorHelpLabel = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(3, 8, 3, 0)
            };
            root.Controls.Add(_monitorHelpLabel, 0, 4);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };
            _saveButton = new Button { AutoSize = true };
            _saveButton.Click += OnSaveClicked;
            _cancelButton = new Button { AutoSize = true };
            _cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; };
            buttonPanel.Controls.Add(_saveButton);
            buttonPanel.Controls.Add(_cancelButton);
            root.Controls.Add(buttonPanel, 0, 5);

            _captureStatusState = CaptureStatusState.Default;
            PopulateMonitorList(_settings.IncludedMonitorDeviceNames);
            RefreshHotkeyDisplay();
            AppLocalization.LanguageChanged += OnApplicationLanguageChanged;
            SelectLanguage(_settings.GetUiLanguage());
            UpdateLocalizedText();
        }

        public AppSettings UpdatedSettings
        {
            get { return _settings; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
            {
                AppLocalization.SetLanguage(_originalLanguage);
            }

            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                AppLocalization.LanguageChanged -= OnApplicationLanguageChanged;
                if (_formIcon != null)
                {
                    _formIcon.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_captureTarget == CaptureTarget.None)
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }

            if (keyData == Keys.Escape)
            {
                EndCapture(CaptureStatusState.Cancelled);
                return true;
            }

            var binding = HotkeyBinding.FromCapturedInput(
                keyData & Keys.KeyCode,
                (ModifierKeys & Keys.Control) == Keys.Control,
                (ModifierKeys & Keys.Alt) == Keys.Alt,
                (ModifierKeys & Keys.Shift) == Keys.Shift,
                IsWindowsKeyPressed());

            if (!binding.IsValid)
            {
                System.Media.SystemSounds.Beep.Play();
                return true;
            }

            if (_captureTarget == CaptureTarget.Left)
            {
                _settings.RotateLeftHotkey = binding;
            }
            else
            {
                _settings.RotateRightHotkey = binding;
            }

            RefreshHotkeyDisplay();
            EndCapture(CaptureStatusState.Success);
            return true;
        }

        private static TextBox CreateReadOnlyTextBox()
        {
            return new TextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill
            };
        }

        private Button CreateCaptureButton(CaptureTarget target)
        {
            var button = new Button { AutoSize = true };
            button.Click += delegate { BeginCapture(target); };
            return button;
        }

        private void OnLanguageSelectionChanged(object sender, EventArgs e)
        {
            if (_suppressLanguageSelectionChanged)
            {
                return;
            }

            var selectedLanguage = GetSelectedLanguage();
            _settings.SetUiLanguage(selectedLanguage);
            AppLocalization.SetLanguage(selectedLanguage);
        }

        private void OnApplicationLanguageChanged(object sender, EventArgs e)
        {
            SelectLanguage(AppLocalization.CurrentLanguage);
            UpdateLocalizedText();
        }

        private void SelectLanguage(AppLanguage language)
        {
            _suppressLanguageSelectionChanged = true;
            try
            {
                for (var i = 0; i < _languageComboBox.Items.Count; i++)
                {
                    var item = _languageComboBox.Items[i] as LanguageItem;
                    if (item != null && item.Language == language)
                    {
                        _languageComboBox.SelectedIndex = i;
                        return;
                    }
                }
            }
            finally
            {
                _suppressLanguageSelectionChanged = false;
            }
        }

        private AppLanguage GetSelectedLanguage()
        {
            var item = _languageComboBox.SelectedItem as LanguageItem;
            return item != null ? item.Language : _settings.GetUiLanguage();
        }

        private void UpdateLocalizedText()
        {
            Text = AppLocalization.Get(TextKey.SettingsTitle);
            _languageLabel.Text = AppLocalization.Get(TextKey.LanguageLabel);
            _hotkeyGroup.Text = AppLocalization.Get(TextKey.GroupHotkeys);
            _rotateLeftLabel.Text = AppLocalization.Get(TextKey.RotateLeft);
            _rotateRightLabel.Text = AppLocalization.Get(TextKey.RotateRight);
            _leftCaptureButton.Text = AppLocalization.Get(TextKey.Change);
            _rightCaptureButton.Text = AppLocalization.Get(TextKey.Change);
            _optionGroup.Text = AppLocalization.Get(TextKey.GroupOptions);
            _includeMinimizedCheckBox.Text = AppLocalization.Get(TextKey.IncludeMinimizedWindows);
            _startWithWindowsCheckBox.Text = AppLocalization.Get(TextKey.StartWithWindowsInBackground);
            _preserveOrderCheckBox.Text = AppLocalization.Get(TextKey.PreserveWindowOrder);
            _monitorGroup.Text = AppLocalization.Get(TextKey.GroupIncludedMonitors);
            _monitorHelpLabel.Text = AppLocalization.Get(TextKey.MonitorHelp);
            _saveButton.Text = AppLocalization.Get(TextKey.Save);
            _cancelButton.Text = AppLocalization.Get(TextKey.Cancel);
            UpdateCaptureStatusText();
            RefreshMonitorLabels();
        }

        private void BeginCapture(CaptureTarget target)
        {
            _captureTarget = target;
            _captureStatusState = CaptureStatusState.Active;
            UpdateCaptureStatusText();
            ActiveControl = null;
        }

        private void EndCapture(CaptureStatusState statusState)
        {
            _captureTarget = CaptureTarget.None;
            _captureStatusState = statusState;
            UpdateCaptureStatusText();
        }

        private void UpdateCaptureStatusText()
        {
            switch (_captureStatusState)
            {
                case CaptureStatusState.Active:
                    _captureStatusLabel.Text = AppLocalization.Get(TextKey.CapturePrompt);
                    break;
                case CaptureStatusState.Cancelled:
                    _captureStatusLabel.Text = AppLocalization.Get(TextKey.CaptureCancelled);
                    break;
                case CaptureStatusState.Success:
                    _captureStatusLabel.Text = AppLocalization.Get(TextKey.CaptureSuccess);
                    break;
                default:
                    _captureStatusLabel.Text = AppLocalization.Get(TextKey.CaptureDefault);
                    break;
            }
        }

        private static bool IsWindowsKeyPressed()
        {
            return (NativeMethods.GetKeyState(NativeMethods.VkLWin) & 0x8000) != 0 ||
                   (NativeMethods.GetKeyState(NativeMethods.VkRWin) & 0x8000) != 0;
        }

        private void RefreshHotkeyDisplay()
        {
            _leftHotkeyTextBox.Text = _settings.RotateLeftHotkey.ToDisplayString();
            _rightHotkeyTextBox.Text = _settings.RotateRightHotkey.ToDisplayString();
        }

        private void RefreshMonitorLabels()
        {
            var checkedDevices = _monitorList.CheckedItems.Cast<MonitorItem>().Select(item => item.DeviceName).ToList();
            PopulateMonitorList(checkedDevices.Count > 0 ? checkedDevices : _settings.IncludedMonitorDeviceNames);
        }

        private void PopulateMonitorList(IEnumerable<string> checkedDeviceNames)
        {
            var checkedSet = new HashSet<string>(checkedDeviceNames ?? Enumerable.Empty<string>());
            _monitorList.BeginUpdate();
            try
            {
                _monitorList.Items.Clear();
                var friendlyNames = _monitorDisplayService.GetFriendlyNames();
                var screens = Screen.AllScreens.OrderBy(screen => screen.Bounds.Left).ThenBy(screen => screen.Bounds.Top);
                foreach (var screen in screens)
                {
                    string friendlyName;
                    friendlyNames.TryGetValue(screen.DeviceName, out friendlyName);
                    var label = BuildMonitorLabel(screen, friendlyName);
                    var isChecked = checkedSet.Contains(screen.DeviceName);
                    _monitorList.Items.Add(new MonitorItem(screen.DeviceName, label), isChecked);
                }
            }
            finally
            {
                _monitorList.EndUpdate();
            }
        }

        private static string BuildMonitorLabel(Screen screen, string friendlyName)
        {
            var deviceAlias = NormalizeDeviceAlias(screen.DeviceName);
            var nameToShow = string.IsNullOrWhiteSpace(friendlyName) ? deviceAlias : friendlyName;
            var details = AppLocalization.Format(
                TextKey.MonitorDetailsFormat,
                screen.Bounds.Width,
                screen.Bounds.Height,
                screen.Bounds.Left,
                screen.Bounds.Top);
            var monitorRole = screen.Primary
                ? AppLocalization.Get(TextKey.PrimaryMonitor)
                : AppLocalization.Get(TextKey.SecondaryMonitor);

            if (string.Equals(nameToShow, deviceAlias, StringComparison.OrdinalIgnoreCase))
            {
                return string.Format("{0} {1} ({2})", monitorRole, nameToShow, details);
            }

            return string.Format("{0} {1} ({2}, {3})", monitorRole, nameToShow, deviceAlias, details);
        }

        private static string NormalizeDeviceAlias(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return AppLocalization.Get(TextKey.UnknownDisplay);
            }

            const string prefix = @"\\.\";
            return deviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? deviceName.Substring(prefix.Length)
                : deviceName;
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            var selectedMonitors = _monitorList.CheckedItems.Cast<MonitorItem>().Select(item => item.DeviceName).ToList();
            if (selectedMonitors.Count == 0)
            {
                MessageBox.Show(
                    this,
                    AppLocalization.Get(TextKey.SelectAtLeastOneMonitor),
                    AppLocalization.Get(TextKey.AppName),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (_settings.RotateLeftHotkey.Equals(_settings.RotateRightHotkey))
            {
                MessageBox.Show(
                    this,
                    AppLocalization.Get(TextKey.HotkeysMustBeDifferent),
                    AppLocalization.Get(TextKey.AppName),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _settings.IncludeMinimizedWindows = _includeMinimizedCheckBox.Checked;
            _settings.StartWithWindows = _startWithWindowsCheckBox.Checked;
            _settings.PreserveWindowOrder = _preserveOrderCheckBox.Checked;
            _settings.IncludedMonitorDeviceNames = selectedMonitors;
            _settings.SetUiLanguage(GetSelectedLanguage());
            DialogResult = DialogResult.OK;
        }

        private sealed class MonitorItem
        {
            public MonitorItem(string deviceName, string description)
            {
                DeviceName = deviceName;
                Description = description;
            }

            public string DeviceName { get; private set; }

            public string Description { get; private set; }

            public override string ToString()
            {
                return Description;
            }
        }

        private sealed class LanguageItem
        {
            public LanguageItem(AppLanguage language, string label)
            {
                Language = language;
                Label = label;
            }

            public AppLanguage Language { get; private set; }

            public string Label { get; private set; }

            public override string ToString()
            {
                return Label;
            }
        }

        private enum CaptureTarget
        {
            None,
            Left,
            Right
        }

        private enum CaptureStatusState
        {
            Default,
            Active,
            Cancelled,
            Success
        }
    }
}
