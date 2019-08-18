using System;
using System.Threading;
using System.Windows.Forms;

namespace drawbridge
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (Mutex mutex = new Mutex(false, "Global\\DrawbridgeUI"))
            {
                if (!mutex.WaitOne(0, false))
                {
                    MessageBox.Show("Drawbridge is already running");
                    return;
                }

                Application.Run(new TrayApplicationContext());
            }
        }
    }
}
