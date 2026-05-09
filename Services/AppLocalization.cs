using System;
using System.Collections.Generic;
using System.Globalization;
using MonitorSwap.Models;

namespace MonitorSwap.Services
{
    internal enum TextKey
    {
        AppName,
        ErrorTitle,
        SettingsTitle,
        LanguageLabel,
        GroupHotkeys,
        GroupOptions,
        GroupIncludedMonitors,
        RotateLeft,
        RotateRight,
        Change,
        IncludeMinimizedWindows,
        StartWithWindowsInBackground,
        PreserveWindowOrder,
        EnableFastMode,
        BrowserCompatibilityMode,
        SkipBrowserFullscreenWindows,
        EnableRotationDiagnostics,
        MonitorHelp,
        Save,
        Cancel,
        CaptureDefault,
        CapturePrompt,
        CaptureCancelled,
        CaptureSuccess,
        MenuRotateLeft,
        MenuRotateRight,
        MenuSettings,
        MenuStartWithWindows,
        MenuExit,
        SelectAtLeastOneMonitor,
        HotkeysMustBeDifferent,
        PrimaryMonitor,
        SecondaryMonitor,
        UnknownDisplay,
        MonitorDetailsFormat
    }

    internal static class AppLocalization
    {
        private static readonly Dictionary<TextKey, string> EnglishTexts = new Dictionary<TextKey, string>
        {
            { TextKey.AppName, "Monitor Swap" },
            { TextKey.ErrorTitle, "Monitor Swap Error" },
            { TextKey.SettingsTitle, "Monitor Swap Settings" },
            { TextKey.LanguageLabel, "Language" },
            { TextKey.GroupHotkeys, "Hotkeys" },
            { TextKey.GroupOptions, "Options" },
            { TextKey.GroupIncludedMonitors, "Included Monitors" },
            { TextKey.RotateLeft, "Rotate Left" },
            { TextKey.RotateRight, "Rotate Right" },
            { TextKey.Change, "Change" },
            { TextKey.IncludeMinimizedWindows, "Include minimized windows" },
            { TextKey.StartWithWindowsInBackground, "Start in background with Windows" },
            { TextKey.PreserveWindowOrder, "Preserve window order after moving" },
            { TextKey.EnableFastMode, "Fast mode (prioritize speed)" },
            { TextKey.BrowserCompatibilityMode, "Use compatibility mode for Chromium-based browsers" },
            { TextKey.SkipBrowserFullscreenWindows, "Skip Chromium fullscreen windows when moving may be unsafe" },
            { TextKey.EnableRotationDiagnostics, "Enable diagnostic rotation logging (cleared on reboot)" },
            { TextKey.MonitorHelp, "Only checked monitors participate in rotation. Two or more are recommended." },
            { TextKey.Save, "Save" },
            { TextKey.Cancel, "Cancel" },
            { TextKey.CaptureDefault, "Press a new hotkey combination. Press Esc to cancel." },
            { TextKey.CapturePrompt, "Press the new combination now. Press Esc to cancel." },
            { TextKey.CaptureCancelled, "Hotkey capture was cancelled." },
            { TextKey.CaptureSuccess, "New hotkey captured." },
            { TextKey.MenuRotateLeft, "Rotate Left" },
            { TextKey.MenuRotateRight, "Rotate Right" },
            { TextKey.MenuSettings, "Settings" },
            { TextKey.MenuStartWithWindows, "Start with Windows" },
            { TextKey.MenuExit, "Exit" },
            { TextKey.SelectAtLeastOneMonitor, "Select at least one monitor." },
            { TextKey.HotkeysMustBeDifferent, "Left and right hotkeys must be different." },
            { TextKey.PrimaryMonitor, "[Primary]" },
            { TextKey.SecondaryMonitor, "[Secondary]" },
            { TextKey.UnknownDisplay, "Unknown Display" },
            { TextKey.MonitorDetailsFormat, "{0}x{1}, pos {2},{3}" }
        };

        private static readonly Dictionary<TextKey, string> KoreanTexts = new Dictionary<TextKey, string>
        {
            { TextKey.AppName, "Monitor Swap" },
            { TextKey.ErrorTitle, "Monitor Swap \uC624\uB958" },
            { TextKey.SettingsTitle, "Monitor Swap \uC124\uC815" },
            { TextKey.LanguageLabel, "\uC5B8\uC5B4" },
            { TextKey.GroupHotkeys, "\uB2E8\uCD95\uD0A4" },
            { TextKey.GroupOptions, "\uC635\uC158" },
            { TextKey.GroupIncludedMonitors, "\uD3EC\uD568\uD560 \uBAA8\uB2C8\uD130" },
            { TextKey.RotateLeft, "\uC67C\uCABD\uC73C\uB85C \uC21C\uD658" },
            { TextKey.RotateRight, "\uC624\uB978\uCABD\uC73C\uB85C \uC21C\uD658" },
            { TextKey.Change, "\uBCC0\uACBD" },
            { TextKey.IncludeMinimizedWindows, "\uCD5C\uC18C\uD654\uB41C \uCC3D \uD3EC\uD568" },
            { TextKey.StartWithWindowsInBackground, "Windows \uC2DC\uC791 \uC2DC \uBC31\uADF8\uB77C\uC6B4\uB4DC\uC5D0\uC11C \uC2E4\uD589" },
            { TextKey.PreserveWindowOrder, "\uC774\uB3D9 \uD6C4 \uCC3D \uC21C\uC11C \uC720\uC9C0" },
            { TextKey.EnableFastMode, "\uACE0\uC18D \uBAA8\uB4DC (\uC18D\uB3C4 \uC6B0\uC120)" },
            { TextKey.BrowserCompatibilityMode, "Chromium \uAE30\uBC18 \uBE0C\uB77C\uC6B0\uC800\uC5D0 \uD638\uD658 \uBAA8\uB4DC \uC0AC\uC6A9" },
            { TextKey.SkipBrowserFullscreenWindows, "\uC774\uB3D9\uC774 \uC704\uD5D8\uD560 \uC218 \uC788\uC744 \uB54C Chromium \uC804\uCCB4\uD654\uBA74 \uCC3D \uAC74\uB108\uB6F0\uAE30" },
            { TextKey.EnableRotationDiagnostics, "\uD68C\uC804 \uC9C4\uB2E8 \uB85C\uADF8 \uAE30\uB85D \uD65C\uC131\uD654 (\uC7AC\uBD80\uD305 \uC2DC \uCD08\uAE30\uD654)" },
            { TextKey.MonitorHelp, "\uCCB4\uD06C\uB41C \uBAA8\uB2C8\uD130\uB9CC \uC21C\uD658 \uB300\uC0C1\uC5D0 \uD3EC\uD568\uB429\uB2C8\uB2E4. \uB450 \uB300 \uC774\uC0C1\uC744 \uAD8C\uC7A5\uD569\uB2C8\uB2E4." },
            { TextKey.Save, "\uC800\uC7A5" },
            { TextKey.Cancel, "\uCDE8\uC18C" },
            { TextKey.CaptureDefault, "\uC0C8 \uB2E8\uCD95\uD0A4 \uC870\uD569\uC744 \uB204\uB974\uC138\uC694. Esc\uB97C \uB204\uB974\uBA74 \uCDE8\uC18C\uB429\uB2C8\uB2E4." },
            { TextKey.CapturePrompt, "\uC9C0\uAE08 \uC0C8 \uC870\uD569\uC744 \uB204\uB974\uC138\uC694. Esc\uB97C \uB204\uB974\uBA74 \uCDE8\uC18C\uB429\uB2C8\uB2E4." },
            { TextKey.CaptureCancelled, "\uB2E8\uCD95\uD0A4 \uC785\uB825\uC774 \uCDE8\uC18C\uB418\uC5C8\uC2B5\uB2C8\uB2E4." },
            { TextKey.CaptureSuccess, "\uC0C8 \uB2E8\uCD95\uD0A4\uAC00 \uC9C0\uC815\uB418\uC5C8\uC2B5\uB2C8\uB2E4." },
            { TextKey.MenuRotateLeft, "\uC67C\uCABD\uC73C\uB85C \uC21C\uD658" },
            { TextKey.MenuRotateRight, "\uC624\uB978\uCABD\uC73C\uB85C \uC21C\uD658" },
            { TextKey.MenuSettings, "\uC124\uC815" },
            { TextKey.MenuStartWithWindows, "Windows\uC640 \uD568\uAED8 \uC2DC\uC791" },
            { TextKey.MenuExit, "\uC885\uB8CC" },
            { TextKey.SelectAtLeastOneMonitor, "\uBAA8\uB2C8\uD130\uB97C \uD558\uB098 \uC774\uC0C1 \uC120\uD0DD\uD558\uC138\uC694." },
            { TextKey.HotkeysMustBeDifferent, "\uC67C\uCABD\uACFC \uC624\uB978\uCABD \uB2E8\uCD95\uD0A4\uB294 \uC11C\uB85C \uB2EC\uB77C\uC57C \uD569\uB2C8\uB2E4." },
            { TextKey.PrimaryMonitor, "[\uC8FC \uBAA8\uB2C8\uD130]" },
            { TextKey.SecondaryMonitor, "[\uBCF4\uC870 \uBAA8\uB2C8\uD130]" },
            { TextKey.UnknownDisplay, "\uC54C \uC218 \uC5C6\uB294 \uB514\uC2A4\uD50C\uB808\uC774" },
            { TextKey.MonitorDetailsFormat, "{0}x{1}, \uC704\uCE58 {2},{3}" }
        };

        private static AppLanguage _currentLanguage = AppLanguage.English;

        public static event EventHandler LanguageChanged;

        public static AppLanguage CurrentLanguage
        {
            get { return _currentLanguage; }
        }

        public static void SetLanguage(AppLanguage language)
        {
            if (_currentLanguage == language)
            {
                return;
            }

            _currentLanguage = language;
            var handler = LanguageChanged;
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        public static string Get(TextKey key)
        {
            var resources = _currentLanguage == AppLanguage.Korean ? KoreanTexts : EnglishTexts;
            string value;
            if (resources.TryGetValue(key, out value))
            {
                return value;
            }

            return EnglishTexts[key];
        }

        public static string Format(TextKey key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), args);
        }
    }
}
