using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorSwap.Services
{
    internal static class AppIconProvider
    {
        public static Icon CreateAppIcon()
        {
            try
            {
                using (var extractedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                {
                    if (extractedIcon != null)
                    {
                        return (Icon)extractedIcon.Clone();
                    }
                }
            }
            catch
            {
                // Fall back to the default Windows application icon when extraction fails.
            }

            return (Icon)SystemIcons.Application.Clone();
        }
    }
}
