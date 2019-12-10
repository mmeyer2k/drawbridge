using Mono.Nat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace drawbridge
{
    public class PingRequest
    {
        public string ExternalIP;
        public bool IsPortMapped;
        public const string Endpoint = "https://drawbridge.xyz/api/ping.php";
        public int LifeTime;
        public string Command;
        public string Parameter;
        public string Version;
        public Dictionary<string, RemoteMachine> RemoteMachines = new Dictionary<string, RemoteMachine>();
        public bool CommandWasProcessed;

        public PingRequest()
        {

        }

        public async System.Threading.Tasks.Task<dynamic> SendAsync(INatDevice Router, string ApiKey, string Key)
        {
            try
            {
                this.ExternalIP = Router.GetExternalIP().ToString();
            }
            catch (Exception exc)
            {
                Debug.WriteLine(exc.Message);
                this.ExternalIP = "";
            }

            string Hostname = Dns.GetHostName();

            this.IsPortMapped = StaticHelpers.isPortMappedOnRouter(Router);

            ManagementObject os = new ManagementObject("Win32_OperatingSystem=@");
            string serial = (string)os["SerialNumber"];

            var values = new Dictionary<string, string>
            {
                { "status", IsPortMapped ? "open" : "closed" },
                { "rdpopen", StaticHelpers.isRDPAvailable() ? "1" : "0" },
                { "wanip", this.ExternalIP != null ? this.ExternalIP : "" },
                { "lanip",  StaticHelpers.GetInternalIP() },
                { "port", Registry.Get("Port") },
                { "host", Hostname },
                { "interval", Registry.Get("Interval")  },
                { "lifetime", Registry.Get("PortLifetime") },
                { "version", StaticHelpers.GetVersion() },
                { "guid", serial },
                { "serviceinstalled", StaticHelpers.IsServiceInstalled().ToString() },
                { "servicerunning", StaticHelpers.IsServiceRunning().ToString() }
            };

            var serializer = new JavaScriptSerializer();

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "hostid", StaticHelpers.GetHostHash(Hostname) },
                { "apikey", ApiKey },
                { "payload",  Harpocrates.Engine.Encrypt(serializer.Serialize(values), Key) }
            });

            // HttpWebRequest request = WebRequest.Create(Endpoint) as HttpWebRequest;
            // request.Proxy = new WebProxy(MyProxyHostString, MyProxyPort);

            // Send the API call
            HttpClient client = new HttpClient();
            dynamic response = await client.PostAsync(Endpoint, content);

            // Parse out the account TTL header
            IEnumerable<string> ttls;
            if (response.Headers.TryGetValues("ttl", out ttls))
            {
                foreach (string ttl in ttls)
                {
                    this.LifeTime = Int32.Parse(ttl);
                }
            }

            // Parse out version header
            IEnumerable<string> versions;
            if (response.Headers.TryGetValues("version", out versions))
            {
                foreach (string version in versions)
                {
                    this.Version = version;
                }
            }

            this.CommandWasProcessed = ProcessCommandHeader(Router, response, Key);

            // Create the machines List
            string responseString = await response.Content.ReadAsStringAsync();
            List<Dictionary<string, string>> RemoteMachinesRaw = new JavaScriptSerializer().Deserialize<List<Dictionary<string, string>>>(responseString);

            // If there was some error parsing the json, just quit here...
            if (RemoteMachinesRaw == null)
            {
                return response;
            }

            foreach (var a in RemoteMachinesRaw)
            {
                RemoteMachineImportJson parsed;

                // First try to decrypt one return parameter for this machine
                // If decryption fails then other machine probably has different key
                try
                {
                    string payloadDecrypted = Harpocrates.Engine.Decrypt(a["payload"], Key);
                    parsed = serializer.Deserialize<RemoteMachineImportJson>(payloadDecrypted);
                }
                catch
                {
                    continue;
                }

                RemoteMachine x = new RemoteMachine();

                x.wanip = parsed.wanip;
                x.lanip = parsed.lanip;
                x.host = parsed.host;
                x.port = Int32.Parse(parsed.port);
                x.pending = Convert.ToBoolean(Convert.ToInt32(a["pending"]));
                x.rdpopen = Convert.ToBoolean(Convert.ToInt32(parsed.rdpopen));
                x.status = parsed.status;
                x.version = parsed.version;
                x.interval = Int32.Parse(parsed.interval);
                x.lifetime = Int32.Parse(parsed.lifetime);
                x.guid = parsed.guid;
                x.servicerunning = Boolean.Parse(parsed.servicerunning);
                x.serviceinstalled = Boolean.Parse(parsed.serviceinstalled);

                RemoteMachines.Add(x.host, x);
            }
            
            return response;
        }

        public static async System.Threading.Tasks.Task SendCommandToTargetAsync(string target, string command, string parameter = "")
        {
            string Key = Registry.Get("Key");

            var content = new Dictionary<string, string>
            {
                { "hostid", StaticHelpers.GetHostHash(Dns.GetHostName()) },
                { "apikey", Registry.Get("ApiKey") },
                { "target", StaticHelpers.GetHostHash(target) },
                { "command", Harpocrates.Engine.Encrypt(command, Key) },
                { "parameter", Harpocrates.Engine.Encrypt(parameter, Key) },
            };

            HttpClient client = new HttpClient();

            await client.PostAsync(PingRequest.Endpoint, new FormUrlEncodedContent(content));
        }

        public bool ProcessCommandHeader(INatDevice Router, dynamic Response, string Key)
        {
            IEnumerable<string> Commands;
            IEnumerable<string> Parameters;

            if (Response.Headers.TryGetValues("Command", out Commands))
            {
                foreach (string c in Commands)
                {
                    Command = c;
                }
            }

            if (Response.Headers.TryGetValues("Parameter", out Parameters))
            {
                foreach (string p in Parameters)
                {
                    Parameter = p;
                }
            }

            // If no command headers were found in the response, return false
            if (Command == null || Parameter == null)
            {
                return false;
            }

            int rdpPortExternal = Int32.Parse(Registry.Get("Port"));

            // Decrypt the headers passed back by server
            string commandDecrypted = Harpocrates.Engine.Decrypt(Command, Key);
            string parameterDecrypted = Harpocrates.Engine.Decrypt(Parameter, Key);

            if (commandDecrypted == "open")
            {
                int lifetime = Convert.ToInt32(Registry.Get("PortLifetime"));
                string desc = "Drawbridge [" + Dns.GetHostName() + "]";
                Mapping mapping = new Mapping(Protocol.Tcp, 3389, rdpPortExternal, lifetime * 60);
                Router.CreatePortMap(mapping);
            }
            else if (commandDecrypted == "close")
            {
                try
                {
                    Router.DeletePortMap(new Mapping(Protocol.Tcp, 3389, rdpPortExternal));
                }
                catch
                {

                }
            }
            else if (commandDecrypted == "lifetime")
            {
                Registry.Set("PortLifetime", parameterDecrypted);
            }
            else if (commandDecrypted == "interval")
            {
                Registry.Set("Interval", parameterDecrypted);
            }
            else if (commandDecrypted == "test")
            {
                PingRequest.SendCommandToTargetAsync(parameterDecrypted, "test-reply", Dns.GetHostName());
            }
            else if (commandDecrypted == "test-reply")
            {
                MessageBox.Show(String.Format("Test command reply processed from: {0}", parameterDecrypted));
            }
            else if (commandDecrypted == "port")
            {
                // Delete old port mapping on router (if any exists)
                try
                {
                    Router.DeletePortMap(new Mapping(Protocol.Tcp, 3389, rdpPortExternal));
                }
                catch
                {

                }

                Registry.Set("Port", parameterDecrypted);
            }
            else if (commandDecrypted == "randomize")
            {
                // Delete old port mapping on router (if any exists)
                try
                {
                    Router.DeletePortMap(new Mapping(Protocol.Tcp, 3389, rdpPortExternal));
                }
                catch
                {

                }

                StaticHelpers.RandomizePort();
            }

            return true;
        }
    }
}
