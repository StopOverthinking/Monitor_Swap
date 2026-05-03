using System;
using System.Drawing;
using System.Windows.Forms;
using MonitorSwap.Forms;
using MonitorSwap.Models;
using MonitorSwap.Services;

namespace MonitorSwap
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly AutoStartService _autoStartService;
        private readonly Icon _appIcon;
        private readonly HotkeyManager _hotkeyManager;
        private readonly RotationTraceService _rotationTraceService;
        private readonly SettingsService _settingsService;
        private readonly WindowRotationService _windowRotationService;
        private readonly ContextMenuStrip _contextMenu;
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _rotateLeftMenuItem;
        private readonly ToolStripMenuItem _rotateRightMenuItem;
        private readonly ToolStripMenuItem _settingsMenuItem;
        private readonly ToolStripMenuItem _startupMenuItem;
        private readonly ToolStripMenuItem _exitMenuItem;
        private AppSettings _settings;

        public TrayApplicationContext()
        {
            _settingsService = new SettingsService();
            _autoStartService = new AutoStartService();
            _rotationTraceService = new RotationTraceService();
            _windowRotationService = new WindowRotationService();
            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

            _settings = _settingsService.Load();
            _rotationTraceService.ResetLogsForNewBootIfNeeded();
            _settings.StartWithWindows = _autoStartService.IsEnabled();
            _appIcon = AppIconProvider.CreateAppIcon();

            AppLocalization.SetLanguage(_settings.GetUiLanguage());
            AppLocalization.LanguageChanged += OnLanguageChanged;

            _contextMenu = new ContextMenuStrip();
            _rotateLeftMenuItem = new ToolStripMenuItem();
            _rotateLeftMenuItem.Click += delegate { Rotate(RotationDirection.Left); };
            _contextMenu.Items.Add(_rotateLeftMenuItem);

            _rotateRightMenuItem = new ToolStripMenuItem();
            _rotateRightMenuItem.Click += delegate { Rotate(RotationDirection.Right); };
            _contextMenu.Items.Add(_rotateRightMenuItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            _settingsMenuItem = new ToolStripMenuItem();
            _settingsMenuItem.Click += delegate { OpenSettings(); };
            _contextMenu.Items.Add(_settingsMenuItem);

            _startupMenuItem = new ToolStripMenuItem();
            _startupMenuItem.Click += delegate { ToggleAutoStart(); };
            _startupMenuItem.Checked = _settings.StartWithWindows;
            _startupMenuItem.CheckOnClick = false;
            _contextMenu.Items.Add(_startupMenuItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            _exitMenuItem = new ToolStripMenuItem();
            _exitMenuItem.Click += delegate { ExitThread(); };
            _contextMenu.Items.Add(_exitMenuItem);

            _notifyIcon = new NotifyIcon
            {
                Text = AppLocalization.Get(TextKey.AppName),
                Icon = _appIcon,
                ContextMenuStrip = _contextMenu,
                Visible = true
            };
            _notifyIcon.DoubleClick += delegate { OpenSettings(); };
            UpdateLocalizedText();

            try
            {
                ApplySettings(_settings);
            }
            catch (Exception ex)
            {
                ShowBalloonTip(AppLocalization.Get(TextKey.ErrorTitle), ex.Message, ToolTipIcon.Error);
            }
        }

        private void OnHotkeyPressed(object sender, RotationDirection direction)
        {
            Rotate(direction);
        }

        private void Rotate(RotationDirection direction)
        {
            try
            {
                _windowRotationService.Rotate(direction, _settings);
            }
            catch
            {
                // Rotation should stay silent.
            }
        }

        private void OpenSettings()
        {
            try
            {
                using (var form = new SettingsForm(_settings))
                {
                    if (form.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    var updatedSettings = form.UpdatedSettings.Clone();
                    ApplySettings(updatedSettings);
                    _settingsService.Save(_settings);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppLocalization.Get(TextKey.AppName), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ToggleAutoStart()
        {
            var updatedSettings = _settings.Clone();
            updatedSettings.StartWithWindows = !updatedSettings.StartWithWindows;

            try
            {
                ApplySettings(updatedSettings);
                _settingsService.Save(_settings);
            }
            catch (Exception ex)
            {
                ShowBalloonTip(AppLocalization.Get(TextKey.ErrorTitle), ex.Message, ToolTipIcon.Warning);
            }
        }

        private void ApplySettings(AppSettings settingsToApply)
        {
            settingsToApply.EnsureDefaults();
            AppLocalization.SetLanguage(settingsToApply.GetUiLanguage());
            _hotkeyManager.RegisterHotkeys(settingsToApply);
            _autoStartService.Apply(settingsToApply.StartWithWindows);
            _settings = settingsToApply;
            _startupMenuItem.Checked = _settings.StartWithWindows;
            UpdateLocalizedText();
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            UpdateLocalizedText();
        }

        private void UpdateLocalizedText()
        {
            _rotateLeftMenuItem.Text = AppLocalization.Get(TextKey.MenuRotateLeft);
            _rotateRightMenuItem.Text = AppLocalization.Get(TextKey.MenuRotateRight);
            _settingsMenuItem.Text = AppLocalization.Get(TextKey.MenuSettings);
            _startupMenuItem.Text = AppLocalization.Get(TextKey.MenuStartWithWindows);
            _exitMenuItem.Text = AppLocalization.Get(TextKey.MenuExit);
            _notifyIcon.Text = AppLocalization.Get(TextKey.AppName);
        }

        private void ShowBalloonTip(string title, string message, ToolTipIcon icon)
        {
            // User feedback requested that tray balloon notifications stay disabled.
        }

        protected override void ExitThreadCore()
        {
            AppLocalization.LanguageChanged -= OnLanguageChanged;
            _hotkeyManager.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
            _appIcon.Dispose();
            base.ExitThreadCore();
        }
    }
}
