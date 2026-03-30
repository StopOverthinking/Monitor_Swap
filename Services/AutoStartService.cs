using System.Windows.Forms;
using Microsoft.Win32;

namespace MonitorSwap.Services
{
    internal sealed class AutoStartService
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MonitorSwap";

        public void Apply(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
            {
                if (key == null)
                {
                    return;
                }

                if (enabled)
                {
                    key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                }
                else if (key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }

        public bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                return key != null && key.GetValue(ValueName) != null;
            }
        }
    }
}
