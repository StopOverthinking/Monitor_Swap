using System;
using System.Threading;
using System.Windows.Forms;

namespace MonitorSwap
{
    internal static class Program
    {
        private const string MutexName = "MonitorSwap.Singleton";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApplicationContext());
                GC.KeepAlive(mutex);
            }
        }
    }
}
