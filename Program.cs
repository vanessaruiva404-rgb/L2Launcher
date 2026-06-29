using System;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LineageII
{
    internal static class Program
    {
        private static Mutex _mutex;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [STAThread]
        private static void Main()
        {

            string mutexName = "L2JDEV_UPDATE2_SINGLE_INSTANCE";
            _mutex = new Mutex(true, mutexName, out bool created);

            if (!created)
            {
                FocusExistingInstance();
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new update());
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        private static void FocusExistingInstance()
        {
            var current = Process.GetCurrentProcess();

            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id != current.Id)
                {
                    IntPtr handle = process.MainWindowHandle;

                    if (handle != IntPtr.Zero)
                    {
                        SetForegroundWindow(handle);
                    }

                    break;
                }
            }
        }
    }
}