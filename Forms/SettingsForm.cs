using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
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
        private readonly TableLayoutPanel _rootLayout;
        private readonly TableLayoutPanel _languagePanel;
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
        private readonly CheckBox _fastModeCheckBox;
        private readonly CheckBox _browserCompatibilityModeCheckBox;
        private readonly CheckBox _skipBrowserFullscreenWindowsCheckBox;
        private readonly CheckBox _enableRotationDiagnosticsCheckBox;
        private readonly GroupBox _exclusionGroup;
        private readonly CheckedListBox _exclusionRuleList;
        private readonly Label _exclusionHelpLabel;
        private readonly FlowLayoutPanel _exclusionButtonPanel;
        private readonly Button _addExclusionButton;
        private readonly Button _removeExclusionButton;
        private readonly GroupBox _monitorGroup;
        private readonly CheckedListBox _monitorList;
        private readonly Label _monitorHelpLabel;
        private readonly FlowLayoutPanel _buttonPanel;
        private readonly Button _saveButton;
        private readonly Button _cancelButton;
        private CaptureTarget _captureTarget;
        private CaptureStatusState _captureStatusState;
        private bool _suppressLanguageSelectionChanged;
        private bool _suppressExclusionRuleChecks;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings.Clone();
            _settings.EnsureDefaults();
            _originalLanguage = _settings.GetUiLanguage();
            _monitorDisplayService = new MonitorDisplayService();

            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(580, 760);
            Size = new Size(680, 820);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            KeyPreview = true;
            _formIcon = AppIconProvider.CreateAppIcon();
            Icon = _formIcon;

            _rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(14)
            };
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(_rootLayout);

            _languagePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 8)
            };
            _languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
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
            _languagePanel.Controls.Add(_languageLabel, 0, 0);
            _languagePanel.Controls.Add(_languageComboBox, 1, 0);
            _rootLayout.Controls.Add(_languagePanel, 0, 0);

            _hotkeyGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                AutoSize = true
            };
            _rootLayout.Controls.Add(_hotkeyGroup, 0, 1);

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
            _rootLayout.Controls.Add(_optionGroup, 0, 2);

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

            _fastModeCheckBox = new CheckBox
            {
                AutoSize = true,
                Checked = _settings.EnableFastMode
            };
            optionLayout.Controls.Add(_fastModeCheckBox);

            _browserCompatibilityModeCheckBox = new CheckBox
            {
                AutoSize = true,
                Checked = _settings.EnableBrowserCompatibilityMode
            };
            optionLayout.Controls.Add(_browserCompatibilityModeCheckBox);

            _skipBrowserFullscreenWindowsCheckBox = new CheckBox
            {
                AutoSize = true,
                Checked = _settings.SkipBrowserFullscreenWindows
            };
            optionLayout.Controls.Add(_skipBrowserFullscreenWindowsCheckBox);

            _enableRotationDiagnosticsCheckBox = new CheckBox
            {
                AutoSize = true,
                Checked = _settings.EnableRotationDiagnostics
            };
            optionLayout.Controls.Add(_enableRotationDiagnosticsCheckBox);

            _exclusionGroup = new GroupBox
            {
                Dock = DockStyle.Fill
            };
            _rootLayout.Controls.Add(_exclusionGroup, 0, 3);

            var exclusionLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(10)
            };
            exclusionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            exclusionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            exclusionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            exclusionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _exclusionGroup.Controls.Add(exclusionLayout);

            _exclusionRuleList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false
            };
            _exclusionRuleList.ItemCheck += OnExclusionRuleItemCheck;
            exclusionLayout.Controls.Add(_exclusionRuleList, 0, 0);

            _exclusionButtonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                WrapContents = false
            };
            _addExclusionButton = new Button { AutoSize = true };
            _addExclusionButton.Click += OnAddExclusionClicked;
            _removeExclusionButton = new Button { AutoSize = true };
            _removeExclusionButton.Click += OnRemoveExclusionClicked;
            _exclusionButtonPanel.Controls.Add(_addExclusionButton);
            _exclusionButtonPanel.Controls.Add(_removeExclusionButton);
            exclusionLayout.Controls.Add(_exclusionButtonPanel, 1, 0);

            _exclusionHelpLabel = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(3, 6, 3, 0)
            };
            exclusionLayout.Controls.Add(_exclusionHelpLabel, 0, 1);
            exclusionLayout.SetColumnSpan(_exclusionHelpLabel, 2);

            _monitorGroup = new GroupBox
            {
                Dock = DockStyle.Fill
            };
            _rootLayout.Controls.Add(_monitorGroup, 0, 4);

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
            _rootLayout.Controls.Add(_monitorHelpLabel, 0, 5);

            _buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };
            _saveButton = new Button { AutoSize = true };
            _saveButton.Click += OnSaveClicked;
            _cancelButton = new Button { AutoSize = true };
            _cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; };
            _buttonPanel.Controls.Add(_saveButton);
            _buttonPanel.Controls.Add(_cancelButton);
            _rootLayout.Controls.Add(_buttonPanel, 0, 6);

            _captureStatusState = CaptureStatusState.Default;
            PopulateMonitorList(GetStoredMonitorSelections());
            RefreshExclusionRulesList();
            RefreshHotkeyDisplay();
            AppLocalization.LanguageChanged += OnApplicationLanguageChanged;
            SelectLanguage(_settings.GetUiLanguage());
            UpdateLocalizedText();
            AdjustMinimumFormSize();
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
            _fastModeCheckBox.Text = AppLocalization.Get(TextKey.EnableFastMode);
            _browserCompatibilityModeCheckBox.Text = AppLocalization.Get(TextKey.BrowserCompatibilityMode);
            _skipBrowserFullscreenWindowsCheckBox.Text = AppLocalization.Get(TextKey.SkipBrowserFullscreenWindows);
            _enableRotationDiagnosticsCheckBox.Text = AppLocalization.Get(TextKey.EnableRotationDiagnostics);
            _exclusionGroup.Text = AppLocalization.Get(TextKey.GroupExcludedWindows);
            _addExclusionButton.Text = AppLocalization.Get(TextKey.AddExcludedWindow);
            _removeExclusionButton.Text = AppLocalization.Get(TextKey.RemoveExcludedWindow);
            _exclusionHelpLabel.Text = AppLocalization.Get(TextKey.ExcludedWindowHelp);
            _monitorGroup.Text = AppLocalization.Get(TextKey.GroupIncludedMonitors);
            _monitorHelpLabel.Text = AppLocalization.Get(TextKey.MonitorHelp);
            _saveButton.Text = AppLocalization.Get(TextKey.Save);
            _cancelButton.Text = AppLocalization.Get(TextKey.Cancel);
            UpdateCaptureStatusText();
            RefreshExclusionRulesList();
            RefreshMonitorLabels();
            AdjustMinimumFormSize();
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
            var checkedMonitorIds = _monitorList.CheckedItems.Cast<MonitorItem>().Select(item => item.Id).ToList();
            PopulateMonitorList(checkedMonitorIds.Count > 0 ? checkedMonitorIds : GetStoredMonitorSelections());
        }

        private IEnumerable<string> GetStoredMonitorSelections()
        {
            return (_settings.IncludedMonitorIds ?? Enumerable.Empty<string>())
                .Concat(_settings.IncludedMonitorDeviceNames ?? Enumerable.Empty<string>())
                .Where(selection => !string.IsNullOrWhiteSpace(selection))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private void PopulateMonitorList(IEnumerable<string> checkedMonitorIds)
        {
            var checkedSet = new HashSet<string>(
                checkedMonitorIds ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            _monitorList.BeginUpdate();
            try
            {
                _monitorList.Items.Clear();
                var monitors = _monitorDisplayService.GetMonitorInfos();
                foreach (var monitor in monitors)
                {
                    var label = BuildMonitorLabel(monitor.Screen, monitor.FriendlyName);
                    var isChecked = checkedSet.Contains(monitor.Id) || checkedSet.Contains(monitor.DeviceName);
                    _monitorList.Items.Add(new MonitorItem(monitor.Id, monitor.DeviceName, label), isChecked);
                }
            }
            finally
            {
                _monitorList.EndUpdate();
            }
        }

        private void AdjustMinimumFormSize()
        {
            var minimumVisibleMonitorItemHeight = Math.Max(28, _monitorList.ItemHeight + 10);
            var minimumMonitorGroupHeight = minimumVisibleMonitorItemHeight + 48;
            _monitorGroup.MinimumSize = new Size(0, minimumMonitorGroupHeight);

            var currentClientWidth = Math.Max(580, ClientSize.Width);
            var requiredClientHeight =
                _rootLayout.Padding.Vertical +
                _languagePanel.GetPreferredSize(new Size(currentClientWidth, 0)).Height +
                _hotkeyGroup.GetPreferredSize(new Size(currentClientWidth, 0)).Height +
                _optionGroup.GetPreferredSize(new Size(currentClientWidth, 0)).Height +
                Math.Max(150, _exclusionGroup.GetPreferredSize(new Size(currentClientWidth, 0)).Height) +
                _monitorGroup.MinimumSize.Height +
                _monitorHelpLabel.GetPreferredSize(new Size(currentClientWidth, 0)).Height +
                _buttonPanel.GetPreferredSize(new Size(currentClientWidth, 0)).Height +
                40;

            var nonClientHeight = Height - ClientSize.Height;
            var minimumHeight = requiredClientHeight + nonClientHeight;
            MinimumSize = new Size(580, Math.Max(760, minimumHeight));
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

        private void RefreshExclusionRulesList()
        {
            _suppressExclusionRuleChecks = true;
            try
            {
                _exclusionRuleList.BeginUpdate();
                _exclusionRuleList.Items.Clear();

                foreach (var rule in _settings.WindowExclusionRules ?? new List<WindowExclusionRule>())
                {
                    if (rule == null)
                    {
                        continue;
                    }

                    _exclusionRuleList.Items.Add(
                        new ExclusionRuleItem(rule, BuildExclusionRuleLabel(rule)),
                        !rule.Disabled);
                }
            }
            finally
            {
                _exclusionRuleList.EndUpdate();
                _suppressExclusionRuleChecks = false;
            }
        }

        private static string BuildExclusionRuleLabel(WindowExclusionRule rule)
        {
            var name = string.IsNullOrWhiteSpace(rule.Name) ? "<unnamed>" : rule.Name.Trim();
            var conditions = new List<string>();

            AddCondition(conditions, "process", rule.ProcessName);
            AddCondition(conditions, "class", rule.ClassName);
            AddCondition(conditions, "title", rule.WindowTitle);

            if (rule.RequireTopMost)
            {
                conditions.Add("topmost");
            }

            if (rule.RequireNoActivate)
            {
                conditions.Add("noactivate");
            }

            if (rule.MaxWidth > 0 || rule.MaxHeight > 0)
            {
                conditions.Add(string.Format("max={0}x{1}", rule.MaxWidth > 0 ? rule.MaxWidth.ToString() : "*", rule.MaxHeight > 0 ? rule.MaxHeight.ToString() : "*"));
            }

            return string.Format("{0} - {1}", name, conditions.Count > 0 ? string.Join(", ", conditions.ToArray()) : "<empty>");
        }

        private static void AddCondition(ICollection<string> conditions, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                conditions.Add(string.Format("{0}={1}", label, value.Trim()));
            }
        }

        private void OnExclusionRuleItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_suppressExclusionRuleChecks)
            {
                return;
            }

            var item = _exclusionRuleList.Items[e.Index] as ExclusionRuleItem;
            if (item != null)
            {
                item.Rule.Disabled = e.NewValue != CheckState.Checked;
            }
        }

        private void OnAddExclusionClicked(object sender, EventArgs e)
        {
            var candidate = SelectWindowExclusionCandidate();
            if (candidate == null)
            {
                return;
            }

            _settings.WindowExclusionRules = _settings.WindowExclusionRules ?? new List<WindowExclusionRule>();
            _settings.WindowExclusionRules.Add(CreateExclusionRule(candidate));
            RefreshExclusionRulesList();
            _exclusionRuleList.SelectedIndex = _exclusionRuleList.Items.Count - 1;
        }

        private void OnRemoveExclusionClicked(object sender, EventArgs e)
        {
            var item = _exclusionRuleList.SelectedItem as ExclusionRuleItem;
            if (item == null)
            {
                return;
            }

            _settings.WindowExclusionRules.Remove(item.Rule);
            RefreshExclusionRulesList();
        }

        private WindowCandidate SelectWindowExclusionCandidate()
        {
            var candidates = GetWindowExclusionCandidates();
            if (candidates.Count == 0)
            {
                MessageBox.Show(
                    this,
                    AppLocalization.Get(TextKey.NoExcludedWindowCandidates),
                    AppLocalization.Get(TextKey.AppName),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return null;
            }

            using (var dialog = new Form())
            {
                dialog.Text = AppLocalization.Get(TextKey.AddExcludedWindowTitle);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(680, 390);

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    Padding = new Padding(10)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                dialog.Controls.Add(layout);

                var promptLabel = new Label
                {
                    AutoSize = true,
                    Dock = DockStyle.Top,
                    Padding = new Padding(0, 0, 0, 8),
                    Text = AppLocalization.Get(TextKey.SelectWindowToExclude)
                };
                layout.Controls.Add(promptLabel, 0, 0);

                var listBox = new ListBox
                {
                    Dock = DockStyle.Fill,
                    IntegralHeight = false
                };
                foreach (var candidate in candidates)
                {
                    listBox.Items.Add(candidate);
                }
                layout.Controls.Add(listBox, 0, 1);

                var buttonPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    AutoSize = true
                };
                var addButton = new Button
                {
                    AutoSize = true,
                    Text = AppLocalization.Get(TextKey.Add),
                    DialogResult = DialogResult.OK,
                    Enabled = false
                };
                var cancelButton = new Button
                {
                    AutoSize = true,
                    Text = AppLocalization.Get(TextKey.Cancel),
                    DialogResult = DialogResult.Cancel
                };
                listBox.SelectedIndexChanged += delegate { addButton.Enabled = listBox.SelectedItem != null; };
                listBox.DoubleClick += delegate
                {
                    if (listBox.SelectedItem != null)
                    {
                        dialog.DialogResult = DialogResult.OK;
                    }
                };
                buttonPanel.Controls.Add(addButton);
                buttonPanel.Controls.Add(cancelButton);
                layout.Controls.Add(buttonPanel, 0, 2);

                dialog.AcceptButton = addButton;
                dialog.CancelButton = cancelButton;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var selectedCandidate = listBox.SelectedItem as WindowCandidate;
                    if (selectedCandidate != null)
                    {
                        return selectedCandidate;
                    }

                    MessageBox.Show(
                        this,
                        AppLocalization.Get(TextKey.NoWindowSelected),
                        AppLocalization.Get(TextKey.AppName),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            return null;
        }

        private static WindowExclusionRule CreateExclusionRule(WindowCandidate candidate)
        {
            return new WindowExclusionRule
            {
                Name = BuildCandidateRuleName(candidate),
                ProcessName = candidate.ProcessName,
                ClassName = candidate.ClassName,
                WindowTitle = candidate.WindowTitle,
                RequireTopMost = candidate.IsTopMost,
                RequireNoActivate = candidate.IsNoActivate
            };
        }

        private static string BuildCandidateRuleName(WindowCandidate candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate.WindowTitle))
            {
                return Truncate(candidate.WindowTitle.Trim(), 80);
            }

            if (!string.IsNullOrWhiteSpace(candidate.ProcessName))
            {
                return candidate.ProcessName.Trim();
            }

            return "Window";
        }

        private static List<WindowCandidate> GetWindowExclusionCandidates()
        {
            var candidates = new List<WindowCandidate>();
            var shellWindow = NativeMethods.GetShellWindow();

            NativeMethods.EnumWindows(
                delegate(IntPtr hWnd, IntPtr _)
                {
                    WindowCandidate candidate;
                    if (TryCreateWindowCandidate(hWnd, shellWindow, out candidate))
                    {
                        candidates.Add(candidate);
                    }

                    return true;
                },
                IntPtr.Zero);

            return candidates
                .OrderBy(candidate => candidate.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.WindowTitle, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool TryCreateWindowCandidate(IntPtr hWnd, IntPtr shellWindow, out WindowCandidate candidate)
        {
            candidate = null;
            if (hWnd == IntPtr.Zero || hWnd == shellWindow || !NativeMethods.IsWindowVisible(hWnd))
            {
                return false;
            }

            uint processId;
            NativeMethods.GetWindowThreadProcessId(hWnd, out processId);
            if (processId == (uint)Process.GetCurrentProcess().Id)
            {
                return false;
            }

            var style = (uint)NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GwlStyle).ToInt64();
            if ((style & NativeMethods.WsChild) != 0)
            {
                return false;
            }

            var exStyle = (uint)NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GwlExStyle).ToInt64();
            if ((exStyle & NativeMethods.WsExToolWindow) != 0 || IsCloaked(hWnd))
            {
                return false;
            }

            RECT rect;
            if (!NativeMethods.GetWindowRect(hWnd, out rect))
            {
                return false;
            }

            var rectangle = NativeMethods.ToRectangle(rect);
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return false;
            }

            var className = ReadClassName(hWnd);
            if (IsShellWindowClass(className))
            {
                return false;
            }

            var processName = TryReadProcessName(processId);
            var title = ReadWindowTitle(hWnd);
            if (string.IsNullOrWhiteSpace(processName) &&
                string.IsNullOrWhiteSpace(className) &&
                string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            candidate = new WindowCandidate(
                processName,
                className,
                title,
                rectangle,
                (exStyle & NativeMethods.WsExTopMost) != 0,
                (exStyle & NativeMethods.WsExNoActivate) != 0);
            return true;
        }

        private static bool IsShellWindowClass(string className)
        {
            return string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(className, "NotifyIconOverflowWindow", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCloaked(IntPtr hWnd)
        {
            int cloaked;
            return NativeMethods.DwmGetWindowAttribute(
                       hWnd,
                       NativeMethods.DwmaCloaked,
                       out cloaked,
                       sizeof(int)) == 0 &&
                   cloaked != 0;
        }

        private static string ReadClassName(IntPtr hWnd)
        {
            var buffer = new StringBuilder(256);
            NativeMethods.GetClassName(hWnd, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        private static string ReadWindowTitle(IntPtr hWnd)
        {
            var length = NativeMethods.GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return string.Empty;
            }

            var buffer = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        private static string TryReadProcessName(uint processId)
        {
            if (processId == 0)
            {
                return string.Empty;
            }

            try
            {
                using (var process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            var selectedMonitors = _monitorList.CheckedItems.Cast<MonitorItem>().Select(item => item.Id).ToList();
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
            _settings.EnableFastMode = _fastModeCheckBox.Checked;
            _settings.EnableBrowserCompatibilityMode = _browserCompatibilityModeCheckBox.Checked;
            _settings.SkipBrowserFullscreenWindows = _skipBrowserFullscreenWindowsCheckBox.Checked;
            _settings.EnableRotationDiagnostics = _enableRotationDiagnosticsCheckBox.Checked;
            _settings.IncludedMonitorIds = selectedMonitors;
            _settings.IncludedMonitorDeviceNames = new List<string>();
            _settings.SetUiLanguage(GetSelectedLanguage());
            DialogResult = DialogResult.OK;
        }

        private sealed class ExclusionRuleItem
        {
            public ExclusionRuleItem(WindowExclusionRule rule, string description)
            {
                Rule = rule;
                Description = description;
            }

            public WindowExclusionRule Rule { get; private set; }

            public string Description { get; private set; }

            public override string ToString()
            {
                return Description;
            }
        }

        private sealed class WindowCandidate
        {
            public WindowCandidate(
                string processName,
                string className,
                string windowTitle,
                Rectangle rectangle,
                bool isTopMost,
                bool isNoActivate)
            {
                ProcessName = processName ?? string.Empty;
                ClassName = className ?? string.Empty;
                WindowTitle = windowTitle ?? string.Empty;
                Rectangle = rectangle;
                IsTopMost = isTopMost;
                IsNoActivate = isNoActivate;
            }

            public string ProcessName { get; private set; }

            public string ClassName { get; private set; }

            public string WindowTitle { get; private set; }

            public Rectangle Rectangle { get; private set; }

            public bool IsTopMost { get; private set; }

            public bool IsNoActivate { get; private set; }

            public override string ToString()
            {
                var title = string.IsNullOrWhiteSpace(WindowTitle) ? "<untitled>" : WindowTitle;
                var process = string.IsNullOrWhiteSpace(ProcessName) ? "<unknown>" : ProcessName;
                var traits = new List<string>();
                if (IsTopMost)
                {
                    traits.Add("topmost");
                }

                if (IsNoActivate)
                {
                    traits.Add("noactivate");
                }

                var suffix = traits.Count > 0 ? " [" + string.Join(", ", traits.ToArray()) + "]" : string.Empty;
                return string.Format(
                    "{0} - {1} ({2}, {3}x{4}){5}",
                    process,
                    title,
                    string.IsNullOrWhiteSpace(ClassName) ? "<no class>" : ClassName,
                    Rectangle.Width,
                    Rectangle.Height,
                    suffix);
            }
        }

        private sealed class MonitorItem
        {
            public MonitorItem(string id, string deviceName, string description)
            {
                Id = id;
                DeviceName = deviceName;
                Description = description;
            }

            public string Id { get; private set; }

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
