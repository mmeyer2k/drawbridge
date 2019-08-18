using System;

namespace drawbridge
{
    public class Registry
    {
        public static void Set(string key, string value)
        {
            if (Environment.UserInteractive)
            {
                Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Drawbridge").SetValue(key, value);
            }
            else
            {
                Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Drawbridge").SetValue(key, value);
            }
        }

        public static string Get(string key)
        {
            if (Has(key))
            {
                if (Environment.UserInteractive)
                {
                    return Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Drawbridge", false).GetValue(key).ToString();
                }
                else
                {
                    return Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Drawbridge", false).GetValue(key).ToString();
                }
            }
            else
            {
                return "";
            }
        }

        public static bool Has(string key)
        {
            if (Environment.UserInteractive)
            {
                return Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Drawbridge", false).GetValue(key) != null;
            }
            else
            {
                return Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Drawbridge", false).GetValue(key) != null;
            }
        }

        public static void CopyUserToHKLM()
        {
            string[] keys = {
                "Key",
                "ApiKey",
                "Interval",
                "Port",
                "PortLifetime",
            };

            foreach (string key in keys)
            {
                string value = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Drawbridge", false).GetValue(key).ToString();
                Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Drawbridge").SetValue(key, value);
            }
        }
    }
}
