using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace MonitorSwap.Models
{
    [DataContract]
    internal sealed class AppSettings
    {
        public AppSettings()
        {
            IncludedMonitorIds = new List<string>();
            IncludedMonitorDeviceNames = new List<string>();
            WindowExclusionRules = new List<WindowExclusionRule>();
            IncludeMinimizedWindows = true;
            RotateLeftHotkey = HotkeyBinding.CreateDefault(Keys.Left);
            RotateRightHotkey = HotkeyBinding.CreateDefault(Keys.Right);
            PreserveWindowOrder = true;
            EnableFastMode = false;
            EnableBrowserCompatibilityMode = true;
            EnableRotationDiagnostics = false;
        }

        [DataMember]
        public bool IncludeMinimizedWindows { get; set; }

        [DataMember]
        public bool StartWithWindows { get; set; }

        [DataMember]
        public bool PreserveWindowOrder { get; set; }

        [DataMember]
        public bool EnableFastMode { get; set; }

        [DataMember]
        public bool EnableBrowserCompatibilityMode { get; set; }

        [DataMember]
        public bool SkipBrowserFullscreenWindows { get; set; }

        [DataMember]
        public bool EnableRotationDiagnostics { get; set; }

        [DataMember]
        public bool CompatibilityDefaultsInitialized { get; set; }

        [DataMember]
        public bool ExclusionDefaultsInitialized { get; set; }

        [DataMember]
        public string UiLanguageCode { get; set; }

        [DataMember]
        public List<string> IncludedMonitorIds { get; set; }

        [DataMember]
        public List<string> IncludedMonitorDeviceNames { get; set; }

        [DataMember]
        public List<WindowExclusionRule> WindowExclusionRules { get; set; }

        [DataMember]
        public HotkeyBinding RotateLeftHotkey { get; set; }

        [DataMember]
        public HotkeyBinding RotateRightHotkey { get; set; }

        public void EnsureDefaults()
        {
            IncludedMonitorIds = IncludedMonitorIds ?? new List<string>();
            IncludedMonitorDeviceNames = IncludedMonitorDeviceNames ?? new List<string>();
            WindowExclusionRules = WindowExclusionRules ?? new List<WindowExclusionRule>();
            WindowExclusionRules.RemoveAll(rule => rule == null);
            RotateLeftHotkey = RotateLeftHotkey ?? HotkeyBinding.CreateDefault(Keys.Left);
            RotateRightHotkey = RotateRightHotkey ?? HotkeyBinding.CreateDefault(Keys.Right);
            if (!CompatibilityDefaultsInitialized)
            {
                EnableBrowserCompatibilityMode = true;
                EnableRotationDiagnostics = false;
                CompatibilityDefaultsInitialized = true;
            }

            if (!ExclusionDefaultsInitialized)
            {
                WindowExclusionRules.Add(WindowExclusionRule.CreateDefaultCodexPetRule());
                ExclusionDefaultsInitialized = true;
            }
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                IncludeMinimizedWindows = IncludeMinimizedWindows,
                StartWithWindows = StartWithWindows,
                PreserveWindowOrder = PreserveWindowOrder,
                EnableFastMode = EnableFastMode,
                EnableBrowserCompatibilityMode = EnableBrowserCompatibilityMode,
                SkipBrowserFullscreenWindows = SkipBrowserFullscreenWindows,
                EnableRotationDiagnostics = EnableRotationDiagnostics,
                CompatibilityDefaultsInitialized = CompatibilityDefaultsInitialized,
                ExclusionDefaultsInitialized = ExclusionDefaultsInitialized,
                UiLanguageCode = UiLanguageCode,
                IncludedMonitorIds = new List<string>(IncludedMonitorIds ?? new List<string>()),
                IncludedMonitorDeviceNames = new List<string>(IncludedMonitorDeviceNames ?? new List<string>()),
                WindowExclusionRules = CloneExclusionRules(WindowExclusionRules),
                RotateLeftHotkey = RotateLeftHotkey != null ? RotateLeftHotkey.Clone() : null,
                RotateRightHotkey = RotateRightHotkey != null ? RotateRightHotkey.Clone() : null
            };
        }

        public AppLanguage GetUiLanguage()
        {
            return AppLanguageExtensions.FromCode(UiLanguageCode);
        }

        public void SetUiLanguage(AppLanguage language)
        {
            UiLanguageCode = language.ToCode();
        }

        private static List<WindowExclusionRule> CloneExclusionRules(IEnumerable<WindowExclusionRule> rules)
        {
            var clonedRules = new List<WindowExclusionRule>();
            if (rules == null)
            {
                return clonedRules;
            }

            foreach (var rule in rules)
            {
                if (rule != null)
                {
                    clonedRules.Add(rule.Clone());
                }
            }

            return clonedRules;
        }
    }

    [DataContract]
    internal sealed class WindowExclusionRule
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public bool Disabled { get; set; }

        [DataMember]
        public string ProcessName { get; set; }

        [DataMember]
        public string ClassName { get; set; }

        [DataMember]
        public string WindowTitle { get; set; }

        [DataMember]
        public bool RequireTopMost { get; set; }

        [DataMember]
        public bool RequireNoActivate { get; set; }

        [DataMember]
        public int MaxWidth { get; set; }

        [DataMember]
        public int MaxHeight { get; set; }

        public WindowExclusionRule Clone()
        {
            return new WindowExclusionRule
            {
                Name = Name,
                Disabled = Disabled,
                ProcessName = ProcessName,
                ClassName = ClassName,
                WindowTitle = WindowTitle,
                RequireTopMost = RequireTopMost,
                RequireNoActivate = RequireNoActivate,
                MaxWidth = MaxWidth,
                MaxHeight = MaxHeight
            };
        }

        public bool HasAnyMatchCondition()
        {
            return !string.IsNullOrWhiteSpace(ProcessName) ||
                   !string.IsNullOrWhiteSpace(ClassName) ||
                   !string.IsNullOrWhiteSpace(WindowTitle) ||
                   RequireTopMost ||
                   RequireNoActivate ||
                   MaxWidth > 0 ||
                   MaxHeight > 0;
        }

        public static WindowExclusionRule CreateDefaultCodexPetRule()
        {
            return new WindowExclusionRule
            {
                Name = "Codex pet",
                ProcessName = "Codex",
                ClassName = "Chrome_WidgetWin_1",
                WindowTitle = "Codex",
                RequireTopMost = true,
                RequireNoActivate = true
            };
        }
    }

    [DataContract]
    internal sealed class HotkeyBinding
    {
        [DataMember]
        public bool Ctrl { get; set; }

        [DataMember]
        public bool Alt { get; set; }

        [DataMember]
        public bool Shift { get; set; }

        [DataMember]
        public bool Win { get; set; }

        [DataMember]
        public int KeyCodeValue { get; set; }

        public Keys KeyCode
        {
            get { return (Keys)KeyCodeValue; }
            set { KeyCodeValue = (int)value; }
        }

        public bool IsValid
        {
            get
            {
                var keyCode = KeyCode & Keys.KeyCode;
                return keyCode != Keys.None &&
                       keyCode != Keys.ControlKey &&
                       keyCode != Keys.Menu &&
                       keyCode != Keys.ShiftKey &&
                       keyCode != Keys.LWin &&
                       keyCode != Keys.RWin;
            }
        }

        public uint ToNativeModifiers()
        {
            uint modifiers = 0;
            if (Alt)
            {
                modifiers |= 0x0001;
            }

            if (Ctrl)
            {
                modifiers |= 0x0002;
            }

            if (Shift)
            {
                modifiers |= 0x0004;
            }

            if (Win)
            {
                modifiers |= 0x0008;
            }

            return modifiers;
        }

        public string ToDisplayString()
        {
            var parts = new List<string>();
            if (Ctrl)
            {
                parts.Add("Ctrl");
            }

            if (Win)
            {
                parts.Add("Win");
            }

            if (Shift)
            {
                parts.Add("Shift");
            }

            if (Alt)
            {
                parts.Add("Alt");
            }

            parts.Add((KeyCode & Keys.KeyCode).ToString());
            return string.Join(" + ", parts);
        }

        public HotkeyBinding Clone()
        {
            return new HotkeyBinding
            {
                Ctrl = Ctrl,
                Alt = Alt,
                Shift = Shift,
                Win = Win,
                KeyCodeValue = KeyCodeValue
            };
        }

        public override bool Equals(object obj)
        {
            var other = obj as HotkeyBinding;
            return other != null &&
                   Ctrl == other.Ctrl &&
                   Alt == other.Alt &&
                   Shift == other.Shift &&
                   Win == other.Win &&
                   KeyCodeValue == other.KeyCodeValue;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Ctrl.GetHashCode();
                hashCode = (hashCode * 397) ^ Alt.GetHashCode();
                hashCode = (hashCode * 397) ^ Shift.GetHashCode();
                hashCode = (hashCode * 397) ^ Win.GetHashCode();
                hashCode = (hashCode * 397) ^ KeyCodeValue;
                return hashCode;
            }
        }

        public static HotkeyBinding CreateDefault(Keys key)
        {
            return new HotkeyBinding
            {
                Ctrl = true,
                Shift = true,
                Win = true,
                KeyCode = key
            };
        }

        public static HotkeyBinding FromCapturedInput(Keys keyCode, bool ctrl, bool alt, bool shift, bool win)
        {
            return new HotkeyBinding
            {
                Ctrl = ctrl,
                Alt = alt,
                Shift = shift,
                Win = win,
                KeyCode = keyCode & Keys.KeyCode
            };
        }
    }
}
