using Mono.Nat;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.ServiceProcess;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace drawbridge
{
    public class StaticHelpers
    {
        public static string GetHeader(dynamic response, string name)
        {
            IEnumerable<string> headers;
            if (response.Headers.TryGetValues("header", out headers))
            {
                foreach (string header in headers)
                {
                    return header;
                }
            }

            return null;
        }

        public static string GetVersion()
        {
            return (Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Drawbridge", false).GetValue("Version") ?? "666.555.444").ToString();
        }

        public static bool IsServiceInstalled()
        {
            bool ServiceOk = false;

            ServiceController[] services = ServiceController.GetServices();

            foreach (ServiceController service in services)
            {
                if (service.DisplayName == "Drawbridge")
                {
                    ServiceOk = true;
                }
            }

            return ServiceOk;
        }

        public static bool isPortMappedOnRouter(INatDevice Router)
        {
            if (Registry.Has("Port") == false)
            {
                return false;
            }

            int Port = Int32.Parse(Registry.Get("Port"));

            // Check with router to see if port is open
            // This allows the system to detect if port is still forwarded from a previous
            if (Router != null)
            {
                Mapping RoutedPort = Router.GetSpecificMapping(Protocol.Tcp, Port);

                return RoutedPort.PrivatePort == 3389;
            }

            return false;
        }

        public static bool IsServiceRunning()
        {
            bool ServiceOk = false;

            ServiceController[] services = ServiceController.GetServices();

            foreach (ServiceController service in services)
            {
                if (service.DisplayName == "Drawbridge" && service.Status.ToString() == "Running")
                {
                    ServiceOk = true;
                }
            }

            return ServiceOk;
        }

        public static string GetInternalIP()
        {
            foreach (var i in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (i.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (i.Speed < 100000)
                {
                    continue;
                }

                // discard virtual cards (virtual box, virtual pc, etc.)
                if ((i.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (i.Description.IndexOf("loopback", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (i.Description.IndexOf("npcap", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    continue;
                }

                if (i.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                if (i.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                foreach (var ua in i.GetIPProperties().UnicastAddresses)
                {
                    if ((ua.Address.ToString().IndexOf(".") >= 0))
                    {
                        return ua.Address.ToString();
                    }
                }
            }

            return "";
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random random = new Random();
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);

                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static bool isRDPAvailable()
        {
            using (TcpClient tcpClient = new TcpClient())
            {
                try
                {
                    tcpClient.Connect("127.0.0.1", 3389);

                    return true;
                }
                catch
                {

                }

                return false;
            }
        }

        public static string GetHostHash(string host)
        {
            string Key = Registry.Get("Key");

            var shaAlgorithm = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(Key));

            byte[] hostBytes = System.Text.Encoding.UTF8.GetBytes(host);

            byte[] signatureHashBytes = shaAlgorithm.ComputeHash(hostBytes).Take(8).ToArray();

            return Convert.ToBase64String(signatureHashBytes);
        }

        public static int RandomizePort()
        {
            Random rnd = new Random();

            int next = rnd.Next(1024, 65535);

            Registry.Set("Port", next.ToString());

            return Int32.Parse(next.ToString());
        }
    }
}
