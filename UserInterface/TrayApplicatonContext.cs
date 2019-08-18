using Mono.Nat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Windows.Forms;
using System.Drawing;
using System.ServiceProcess;
using System.Threading;
using System.IO;
using System.Net.NetworkInformation;

namespace drawbridge
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon TrayIcon;
        private ContextMenuStrip TrayIconContextMenu;
        public System.Windows.Forms.Timer loopTimer = new System.Windows.Forms.Timer();
        public string ExternalIp;
        public bool isPortOpen = false;
        public PingRequest Ping;
        public INatDevice Router;
        public int FailedPings = 0;
        ToolStripMenuItem TitleMenuItem = new ToolStripMenuItem();
        ToolStripMenuItem LifetimePickerMenuItem = new ToolStripMenuItem();
        ToolStripMenuItem IntervalPicker = new ToolStripMenuItem();
        ToolStripLabel PortDisplayLabelItem = new ToolStripLabel();
        ToolStripMenuItem WindowsServiceMenuItem = new ToolStripMenuItem();
        ToolStripMenuItem UpdateMenuItem = new ToolStripMenuItem();

        public Dictionary<int, string> PingsIntervals = new Dictionary<int, string> {
                { 10, "10 Seconds" },
                { 20, "20 Seconds" },
                { 30, "30 Seconds" },
                { 40, "40 Seconds" },
                { 50, "50 Seconds" },
                { 60, "60 Seconds" },
            };

        public Dictionary<int, string> Lifetimes = new Dictionary<int, string> {
                { 15, "15 Minutes"},
                { 30, "30 Minutes"},
                { 60, "1 Hour"},
                { 240, "4 Hours"},
                { 480, "8 Hours"},
                { 1440, "24 Hours"},
                { 2880, "48 Hours"},
                { 4320, "72 Hours"},
            };

        public TrayApplicationContext()
        {
            bool createdNew = true;
            using (Mutex mutex = new Mutex(true, "Drawbridge", out createdNew))
            {
                if (createdNew == false)
                {
                    Environment.Exit(0);
                }

                NatUtility.DeviceFound += DeviceFound;
                NatUtility.StartDiscovery();

                Application.ApplicationExit += new EventHandler(this.OnApplicationExit);

                try
                {
                    InitializeComponent();
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.ToString());
                }

                TrayIcon.Visible = true;
            }
        }

        private void InitializeComponent()
        {
            TrayIcon = new NotifyIcon();

            // If this is the first run, generate a random port
            if (Registry.Has("Port") == false)
            {
                StaticHelpers.RandomizePort();
            }

            // If this is the first run, make sure key field is available
            if (Registry.Has("PortLifetime") == false)
            {
                Registry.Set("PortLifetime", 30.ToString());
            }

            // If this is the first run, set default connection interval
            if (Registry.Has("Interval") == false)
            {
                Registry.Set("Interval", 20.ToString());
            }

            // If this is the first run, prompt user with authentication settings page
            if (Registry.Has("ApiKey") == false || Registry.Has("Key") == false)
            {
                SettingsMenuItem_Click(null, null);
            }

            // If user aborted before entering either key, we need to exit here.
            if (Registry.Has("ApiKey") == false || Registry.Has("Key") == false)
            {
                MessageBox.Show("Drawbridge will now exit.");

                Environment.Exit(1);
            }

            // Start the main timer loop
            this.loopTimer.Tick += new EventHandler(timerTickAsync);
            this.loopTimer.Interval = Int32.Parse(Registry.Get("Interval")) * 1000;
            this.loopTimer.Start();
            timerTickAsync(null, null);

            // Check if this is the first run by seeing if there is a saved API and Protocol key
            // If keys are not present, then prompt user to enter them

            //The icon is added to the project resources.
            //Here I assume that the name of the file is 'TrayIcon.ico'
            TrayIcon.Icon = Properties.Resources.BlackRook;

            //Optional - Add a context menu to the TrayIcon:
            TrayIconContextMenu = new ContextMenuStrip();

            TrayIconContextMenu.SuspendLayout();

            this.TitleMenuItem.Text = String.Format("Drawbridge [{0}]", StaticHelpers.GetVersion());
            this.TrayIconContextMenu.Items.Add(this.TitleMenuItem);
            this.TitleMenuItem.Image = Properties.Resources.bullet_grey;

            // Add some HRs
            this.TrayIconContextMenu.Items.Add("-");
            this.TrayIconContextMenu.Items.Add("-");

            ToolStripMenuItem SettingsMenuItem = new ToolStripMenuItem();
            SettingsMenuItem.Text = "Settings";
            this.TrayIconContextMenu.Items.Add(SettingsMenuItem);

            ToolStripMenuItem Authentication = new ToolStripMenuItem();
            Authentication.Text = "Authentication";
            Authentication.Click += new EventHandler(this.SettingsMenuItem_Click);
            SettingsMenuItem.DropDownItems.Add(Authentication);

            this.IntervalPicker.Text = "Ping Interval";
            SettingsMenuItem.DropDownItems.Add(this.IntervalPicker);

            foreach (KeyValuePair<int, string> Ping in this.PingsIntervals)
            {
                ToolStripMenuItem x = new ToolStripMenuItem();
                x.Text = Ping.Value;
                x.Tag = Ping.Key;
                if (Ping.Key == Int32.Parse(Registry.Get("Interval")))
                {
                    x.Checked = true;
                }
                x.Click += delegate (Object sender, EventArgs e)
                {
                    ToolStripMenuItem s = sender as ToolStripMenuItem;
                    this.SelectIntervalSetting(Int32.Parse(s.Tag.ToString()));
                    Registry.Set("Interval", Ping.Key.ToString());
                };
                this.IntervalPicker.DropDownItems.Add(x);
            }

            foreach (KeyValuePair<int, string> Lifetime in this.Lifetimes)
            {
                ToolStripMenuItem x = new ToolStripMenuItem();
                x.Text = Lifetime.Value;
                x.Tag = Lifetime.Key;
                if (Lifetime.Key == Int32.Parse(Registry.Get("PortLifetime")))
                {
                    x.Checked = true;
                }
                x.Click += delegate (Object sender, EventArgs e)
                {
                    // Uncheck existing option...
                    foreach (ToolStripMenuItem a in this.LifetimePickerMenuItem.DropDownItems)
                    {
                        a.Checked = false;
                    }
                    ToolStripMenuItem s = sender as ToolStripMenuItem;
                    Registry.Set("PortLifetime", Lifetime.Key.ToString());
                    s.Checked = true;
                };
                this.LifetimePickerMenuItem.DropDownItems.Add(x);
            }

            ToolStripMenuItem PortSettingsMenuItem = new ToolStripMenuItem();
            PortSettingsMenuItem.Text = "Mapped Port";
            SettingsMenuItem.DropDownItems.Add(PortSettingsMenuItem);

            this.PortDisplayLabelItem.Text = "Port: " + Registry.Get("Port");
            PortSettingsMenuItem.DropDownItems.Add(this.PortDisplayLabelItem);

            PortSettingsMenuItem.DropDownItems.Add("-");

            ToolStripMenuItem PortRandomizeMenuItem = new ToolStripMenuItem();
            PortRandomizeMenuItem.Text = "Randomize";
            PortRandomizeMenuItem.Click += delegate (Object sender, EventArgs e)
            {
                int port = StaticHelpers.RandomizePort();
                this.PortDisplayLabelItem.Text = "Port: " + port;
                timerTickAsync(null, null);
            };
            PortSettingsMenuItem.DropDownItems.Add(PortRandomizeMenuItem);

            ToolStripMenuItem PortSetManualMenuItem = new ToolStripMenuItem();
            PortSetManualMenuItem.Text = "Specify";
            PortSetManualMenuItem.Click += delegate (Object sender, EventArgs e)
            {
                int port = PortNumberInput(Int32.Parse(Registry.Get("Port")));
                Registry.Set("Port", port.ToString());
                this.PortDisplayLabelItem.Text = "Port: " + port;
                timerTickAsync(null, null);
            };
            PortSettingsMenuItem.DropDownItems.Add(PortSetManualMenuItem);

            this.LifetimePickerMenuItem.Text = "Time To Live";
            PortSettingsMenuItem.DropDownItems.Add(this.LifetimePickerMenuItem);

            WindowsServiceMenuItem.Text = "Windows Service";
            SettingsMenuItem.DropDownItems.Add(WindowsServiceMenuItem);

            this.RefreshWindowsServiceMenu();

            ToolStripMenuItem RefreshMenuItem = new ToolStripMenuItem();
            RefreshMenuItem.Text = "Refresh";
            RefreshMenuItem.Click += new EventHandler(RefreshMenuItem_Click);
            this.TrayIconContextMenu.Items.Add(RefreshMenuItem);

            this.UpdateMenuItem.Text = "Update Available!";
            this.UpdateMenuItem.Visible = false;
            this.TrayIconContextMenu.Items.Add(this.UpdateMenuItem);

            ToolStripMenuItem CloseMenuItem = new ToolStripMenuItem();
            CloseMenuItem.Text = "Close";
            CloseMenuItem.Click += new EventHandler(CloseMenuItem_Click);
            this.TrayIconContextMenu.Items.Add(CloseMenuItem);

            TrayIconContextMenu.ResumeLayout(false);
            TrayIcon.ContextMenuStrip = TrayIconContextMenu;
        }

        public void RefreshWindowsServiceMenu()
        {
            // Delete all of the existing items
            while (this.WindowsServiceMenuItem.DropDownItems.Count > 0)
            {
                this.WindowsServiceMenuItem.DropDownItems.RemoveAt(0);
            }

            Font f = new Font("Consolas", 9);

            ToolStripLabel WindowsServiceInstalledLabelItem = new ToolStripLabel();
            WindowsServiceInstalledLabelItem.Text = "Installation Status: " + (StaticHelpers.IsServiceInstalled() ? "Installed" : "Not Installed!");
            WindowsServiceInstalledLabelItem.Font = f;
            if (StaticHelpers.IsServiceInstalled() == true)
            {
                WindowsServiceInstalledLabelItem.Image = Properties.Resources.bullet_green;
            }
            else
            {
                WindowsServiceInstalledLabelItem.Image = Properties.Resources.bullet_yellow;
            }
            WindowsServiceMenuItem.DropDownItems.Add(WindowsServiceInstalledLabelItem);

            ToolStripLabel WindowsServiceStatusLabelItem = new ToolStripLabel();
            WindowsServiceStatusLabelItem.Text = "Operational Status : " + (StaticHelpers.IsServiceRunning() ? "Running" : "Not Running!");
            WindowsServiceStatusLabelItem.Font = f;
            if (StaticHelpers.IsServiceRunning() == true)
            {
                WindowsServiceStatusLabelItem.Image = Properties.Resources.bullet_green;
            }
            else
            {
                WindowsServiceStatusLabelItem.Image = Properties.Resources.bullet_yellow;
            }
            WindowsServiceMenuItem.DropDownItems.Add(WindowsServiceStatusLabelItem);

            WindowsServiceMenuItem.DropDownItems.Add("-");

            WindowsServiceMenuItem.Image = Properties.Resources.bullet_yellow;

            ToolStripMenuItem RefreshServiceSettingsMenuItem = new ToolStripMenuItem();
            RefreshServiceSettingsMenuItem.Text = "Refresh Service Settings";
            RefreshServiceSettingsMenuItem.ToolTipText = "The Drawbridge service must keep a separate copy of the Drawbridge settings. This button syncs the settings to the service process.";
            RefreshServiceSettingsMenuItem.Click += delegate (Object sender, EventArgs e)
            {
                Registry.CopyUserToHKLM();
                MessageBox.Show("Done! Service is now updated.");
            };
            if (StaticHelpers.IsAdministrator() == false)
            {
                RefreshServiceSettingsMenuItem.Enabled = false;
                RefreshServiceSettingsMenuItem.ToolTipText = "You must re-launch Drawbridge as an administrator";
            }
            WindowsServiceMenuItem.DropDownItems.Add(RefreshServiceSettingsMenuItem);

            if (StaticHelpers.IsServiceInstalled() == true)
            {
                if (StaticHelpers.IsServiceRunning() == false)
                {
                    ToolStripMenuItem StartServiceMenuItem = new ToolStripMenuItem();
                    StartServiceMenuItem.Text = "Start Service";
                    StartServiceMenuItem.Click += delegate (Object sender, EventArgs e)
                    {
                        // Write out the settings path folder so that service knows where to look
                        Registry.CopyUserToHKLM();

                        TimeSpan timeout = TimeSpan.FromMilliseconds(10000);
                        ServiceController service = new ServiceController("Drawbridge");
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        MessageBox.Show("Drawbridge service has been started.");
                        RefreshMenuItem_Click(null, null);
                    };
                    if (StaticHelpers.IsAdministrator() == false)
                    {
                        StartServiceMenuItem.Enabled = false;
                        StartServiceMenuItem.ToolTipText = "You must re-launch Drawbridge as an administrator";
                    }
                    WindowsServiceMenuItem.DropDownItems.Add(StartServiceMenuItem);
                }
                else
                {
                    WindowsServiceMenuItem.Image = Properties.Resources.bullet_green;
                    ToolStripMenuItem StartServiceMenuItem = new ToolStripMenuItem();
                    StartServiceMenuItem.Text = "Stop Service";
                    StartServiceMenuItem.Click += delegate (Object sender, EventArgs e)
                    {
                        TimeSpan timeout = TimeSpan.FromMilliseconds(10000);
                        ServiceController service = new ServiceController("Drawbridge");
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                        MessageBox.Show("Drawbridge service has been stopped.");
                        RefreshMenuItem_Click(null, null);
                    };
                    if (StaticHelpers.IsAdministrator() == false)
                    {
                        StartServiceMenuItem.Enabled = false;
                        StartServiceMenuItem.ToolTipText = "You must re-launch Drawbridge as an administrator";
                    }
                    WindowsServiceMenuItem.DropDownItems.Add(StartServiceMenuItem);
                }

                ToolStripMenuItem UninstallServiceMenuItem = new ToolStripMenuItem();
                UninstallServiceMenuItem.Text = "Uninstall Service";
                UninstallServiceMenuItem.Enabled = StaticHelpers.IsAdministrator();
                if (StaticHelpers.IsAdministrator() == false)
                {
                    UninstallServiceMenuItem.Enabled = false;
                    UninstallServiceMenuItem.ToolTipText = "You must re-launch Drawbridge as an administrator";
                }
                UninstallServiceMenuItem.Click += delegate (Object sender, EventArgs e)
                {
                    string path = System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "\\WindowsService.exe";
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.Arguments = "/K sc.exe delete Drawbridge";
                    psi.FileName = "CMD.EXE";
                    psi.Verb = "runas";
                    Process p = Process.Start(psi);
                    MessageBox.Show("Drawbridge service has been deleted.");
                    RefreshMenuItem_Click(null, null);
                };
                WindowsServiceMenuItem.DropDownItems.Add(UninstallServiceMenuItem);
            }
            else
            {
                ToolStripMenuItem InstallServiceMenuItem = new ToolStripMenuItem();
                InstallServiceMenuItem.Text = "Install Service";
                if (StaticHelpers.IsAdministrator() == false)
                {
                    InstallServiceMenuItem.Enabled = false;
                    InstallServiceMenuItem.ToolTipText = "You must re-launch Drawbridge as an administrator";
                }
                InstallServiceMenuItem.Click += delegate (Object sender, EventArgs e)
                {
                    // Write out the settings path folder so that service knows where to look
                    Registry.CopyUserToHKLM();

                    string path = System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "\\WindowsService.exe";
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.Arguments = String.Format("/K sc.exe create \"Drawbridge\" binPath= \"{0}\" start= \"auto\"", path);
                    psi.FileName = "CMD.EXE";
                    psi.Verb = "runas";
                    Process p = Process.Start(psi);
                    MessageBox.Show("Drawbridge service has been created.");
                    RefreshMenuItem_Click(null, null);
                };
                WindowsServiceMenuItem.DropDownItems.Add(InstallServiceMenuItem);
            }
        }

        private void RefreshMenuItem_Click(object sender, EventArgs e)
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                timerTickAsync(null, null);
            }).Start();

            this.RefreshWindowsServiceMenu();
        }

        public void SelectIntervalSetting(int Seconds)
        {
            foreach (ToolStripMenuItem a in this.IntervalPicker.DropDownItems)
            {
                a.Checked = a.Tag.ToString() == Seconds.ToString();
            }
        }

        private void SendTestCommand(object sender, EventArgs e)
        {
            ToolStripMenuItem s = sender as ToolStripMenuItem;

            PingRequest.SendCommandToTargetAsync(s.Tag.ToString(), "test", Dns.GetHostName());

            timerTickAsync(null, null);
        }

        private void SettingsMenuItem_Click(object sender, EventArgs e)
        {
            // Show protocol key modal
            Form2 frm2 = new Form2();
            frm2.StartPosition = FormStartPosition.CenterScreen;
            frm2.ShowDialog();

            // Show API key modal
            Form1 frm1 = new Form1();
            frm1.Router = this.Router;
            frm1.StartPosition = FormStartPosition.CenterScreen;
            frm1.ShowDialog();

            timerTickAsync(null, null);
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            // Cleanup so that the icon will be removed when the application is closed
            this.TrayIcon.Visible = false;
        }

        private void CloseMenuItem_Click(object sender, EventArgs e)
        {
            if (StaticHelpers.IsServiceRunning())
            {
                Application.Exit();
            }

            if (MessageBox.Show(
                    "Do you really want to close me?",
                    "Are you sure?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button2
                ) == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        private async void ClosePortMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem s = sender as ToolStripItem;

            RemoteMachine node = Ping.RemoteMachines[s.Tag.ToString()];

            await PingRequest.SendCommandToTargetAsync(node.host, "close");

            timerTickAsync(null, null);
        }

        private async void OpenPortMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem s = sender as ToolStripItem;

            s.Image = Properties.Resources.bullet_blue;

            RemoteMachine node = Ping.RemoteMachines[s.Tag.ToString()];

            await PingRequest.SendCommandToTargetAsync(node.host, "open");

            timerTickAsync(null, null);
        }

        private async void RandomizePortMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem s = sender as ToolStripItem;

            s.Image = Properties.Resources.bullet_blue;

            RemoteMachine node = Ping.RemoteMachines[s.Tag.ToString()];

            await PingRequest.SendCommandToTargetAsync(node.host, "randomize");

            timerTickAsync(null, null);
        }

        private void ConnectClientMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem s = sender as ToolStripItem;

            s.Image = Properties.Resources.bullet_blue;

            RemoteMachine node = Ping.RemoteMachines[s.Tag.ToString()];

            Process.Start("mstsc", "/v:" + node.wanip + ":" + node.port);
        }

        private void ConnectClientInternalMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem s = sender as ToolStripItem;

            RemoteMachine node = Ping.RemoteMachines[s.Tag.ToString()];

            Process.Start("mstsc", "/v:" + node.lanip);
        }

        public static int PortNumberInput(int PreSelected = 1024)
        {
            PortSelect PortSelect = new PortSelect();

            PortSelect.numPort.Value = PreSelected;

            PortSelect.ShowDialog();

            return PortSelect.PortNum;
        }

        public async void timerTickAsync(object sender, EventArgs e)
        {
            // Save a timestamp file on every ping which is how
            // the service knows when the UI is running
            string tmp = Path.Combine(Path.GetTempPath(), "DrawbridgeTimestamp.txt");
            long stamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            File.WriteAllText(tmp, stamp.ToString());

            // Get some basic settings items out of the registry
            // which are required to make an API ping call
            string Key = Registry.Get("Key");
            string ApiKey = Registry.Get("ApiKey");
            string Interval = Registry.Get("Interval");

            // Protect application from crashing when important
            // settings values can not be found.
            if (ApiKey == "" || Interval == "" || Key == "")
            {
                return;
            }

            string host = Dns.GetHostName();

            // Reset timer interval incase it was changed in the settings
            this.loopTimer.Interval = Int32.Parse(Interval) * 10000;

            // If the router object is still being found, skip this ping
            if (Router == null)
            {
                this.TitleMenuItem.Image = Properties.Resources.bullet_yellow;
                this.TitleMenuItem.ToolTipText = "Your router does not seem to support all required features.";
                return;
            }
            else
            {
                this.TitleMenuItem.ToolTipText = "";
            }

            // Do something if the failed number of ping attempts is to high
            if (this.FailedPings > 3)
            {
                this.TitleMenuItem.Image = Properties.Resources.bullet_red;
            }
            else
            {
                this.TitleMenuItem.Image = Properties.Resources.bullet_green;
            }

            dynamic response;

            Ping = new PingRequest();
            try
            {
                response = await Ping.SendAsync(Router, ApiKey, Key);
            }
            catch (Exception exc)
            {
                //MessageBox.Show(exc.InnerException.Message);

                this.FailedPings++;

                return;
            }

            // Check with router to see if port is open
            // This allows the system to detect if port is still forwarded from a previous
            Mapping RoutedPort = this.Router.GetSpecificMapping(Protocol.Tcp, Int32.Parse(Registry.Get("Port")));
            this.isPortOpen = Ping.IsPortMapped;

            // If account is good, make menu header icon green
            if (Ping.LifeTime > 0)
            {
                this.TitleMenuItem.Image = Properties.Resources.bullet_green;
                this.TitleMenuItem.ToolTipText = String.Format("Your API key is valid for {0} more days", Ping.LifeTime / (60 * 60 * 24));
            }

            // Keep running list of discovered machines, so that expired ones can be removed after
            List<string> machineList = new List<string>();

            if (Ping.RemoteMachines != null)
            {
                foreach (KeyValuePair<string, RemoteMachine> b in Ping.RemoteMachines)
                {
                    RemoteMachine x = b.Value;

                    string menukey = "menu___" + x.host;

                    machineList.Add(menukey);

                    // Remove any existing machine from the tray icon in a thread-safe way
                    if (TrayIconContextMenu.InvokeRequired)
                    {
                        TrayIconContextMenu.Invoke(new MethodInvoker(delegate
                        {
                            TrayIconContextMenu.Items.RemoveByKey(menukey);
                        }));
                    }
                    else
                    {
                        TrayIconContextMenu.Items.RemoveByKey(menukey);
                    }

                    ToolStripMenuItem HostMenuItem = this.CreateHostMenuItem(menukey, x);

                    // Add the menu item thread-safely
                    if (TrayIconContextMenu.InvokeRequired)
                    {
                        TrayIconContextMenu.Invoke(new MethodInvoker(delegate
                        {
                            this.TrayIconContextMenu.Items.Insert(2, HostMenuItem);
                        }));
                    }
                    else
                    {
                        this.TrayIconContextMenu.Items.Insert(2, HostMenuItem);
                    }
                }
            }

            List<string> removedMenuNames = new List<string>();

            // Remove expired entries
            foreach (var b in this.TrayIconContextMenu.Items)
            {
                ToolStripMenuItem test = b as ToolStripMenuItem;
                if (test != null && test.Name.Contains("menu___") && !machineList.Contains(test.Name))
                {
                    removedMenuNames.Add(test.Name);
                }
            }

            foreach (string removedMenuName in removedMenuNames)
            {
                this.TrayIconContextMenu.Items.RemoveByKey(removedMenuName);
            }

            // If this ping request resulted in commands which were processed, then a 
            // new thread should run another ping request to send any updated status
            // back to the central server
            if (Ping.CommandWasProcessed == true)
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    timerTickAsync(null, null);
                }).Start();
            }

            // Refresh the interval value just incase it was changed
            this.SelectIntervalSetting(Int32.Parse(Registry.Get("Interval")));

            // Once a ping sequence has been fully completed, reset the fails counter
            this.FailedPings = 0;
        }

        private void DeviceFound(object sender, DeviceEventArgs args)
        {
            this.Router = args.Device;
            this.ExternalIp = this.Router.GetExternalIP().ToString();
            timerTickAsync(null, null);
        }

        private ToolStripMenuItem CreateHostMenuItem(string Name, RemoteMachine x)
        {
            ToolStripMenuItem HostMenuItem = new ToolStripMenuItem();

            HostMenuItem.Text = x.host;

            HostMenuItem.Name = Name;

            if (x.pending)
            {
                HostMenuItem.Image = Properties.Resources.bullet_blue;
            }
            else if (x.rdpopen == false)
            {
                HostMenuItem.Image = Properties.Resources.bullet_red;
                HostMenuItem.ToolTipText = "RDP not listening on this host.";
            }
            else if (x.status == "open")
            {
                HostMenuItem.Image = Properties.Resources.bullet_green;
            }
            else
            {
                HostMenuItem.Image = Properties.Resources.bullet_grey;
            }

            HostMenuItem.Tag = x.host;

            if (x.status == "closed")
            {
                ToolStripMenuItem OpenPortMenuItem = new ToolStripMenuItem();
                OpenPortMenuItem.Text = "Open Port";
                OpenPortMenuItem.Tag = x.host;
                OpenPortMenuItem.Click += new EventHandler(OpenPortMenuItem_Click);
                HostMenuItem.DropDownItems.Add(OpenPortMenuItem);
            }

            if (x.status == "open" && x.rdpopen == true)
            {
                ToolStripMenuItem ConnectClientMenuItem = new ToolStripMenuItem();
                ConnectClientMenuItem.Text = "Launch RDP";
                ConnectClientMenuItem.Tag = x.host;
                ConnectClientMenuItem.Click += new EventHandler(ConnectClientMenuItem_Click);
                if (x.host == Dns.GetHostName() || x.wanip == this.ExternalIp || x.rdpopen == false)
                {
                    ConnectClientMenuItem.Enabled = false;
                }
                HostMenuItem.DropDownItems.Add(ConnectClientMenuItem);
            }

            if (x.rdpopen == true)
            {
                ToolStripMenuItem LaunchRDPInternalMenuItem = new ToolStripMenuItem();
                LaunchRDPInternalMenuItem.Text = "Launch RDP [internal]";
                // If this machine is the one the user is on disable it...
                if (x.host == Dns.GetHostName() || x.wanip != this.ExternalIp)
                {
                    LaunchRDPInternalMenuItem.ToolTipText = "Computer can not connect to itself.";
                    LaunchRDPInternalMenuItem.Enabled = false;
                }
                LaunchRDPInternalMenuItem.Tag = x.host;
                LaunchRDPInternalMenuItem.Click += new EventHandler(ConnectClientInternalMenuItem_Click);
                HostMenuItem.DropDownItems.Add(LaunchRDPInternalMenuItem);
            }

            if (x.status == "open")
            {
                ToolStripMenuItem ClosePortMenuItem = new ToolStripMenuItem();
                ClosePortMenuItem.Text = "Close Port";
                ClosePortMenuItem.Tag = x.host;
                ClosePortMenuItem.Click += new EventHandler(ClosePortMenuItem_Click);
                HostMenuItem.DropDownItems.Add(ClosePortMenuItem);
            }

            ToolStripMenuItem HostPortSettingsMenuItem = new ToolStripMenuItem();
            HostPortSettingsMenuItem.Text = "Port Settings";
            HostMenuItem.DropDownItems.Add(HostPortSettingsMenuItem);

            ToolStripMenuItem RandomizePortMenuItem = new ToolStripMenuItem();
            RandomizePortMenuItem.Text = "Randomize Port";
            RandomizePortMenuItem.Tag = x.host;
            RandomizePortMenuItem.Click += new EventHandler(RandomizePortMenuItem_Click);
            HostPortSettingsMenuItem.DropDownItems.Add(RandomizePortMenuItem);

            ToolStripMenuItem PortSetManualMenuItem = new ToolStripMenuItem();
            PortSetManualMenuItem.Text = "Choose Port";
            PortSetManualMenuItem.Tag = x.host;
            PortSetManualMenuItem.Click += delegate (Object sender1, EventArgs ee)
            {
                PortSelect ps = new PortSelect();

                ps.numPort.Value = x.port;

                ps.ShowDialog();

                PingRequest.SendCommandToTargetAsync(x.host, "port", ps.numPort.Value.ToString());

                timerTickAsync(null, null);
            };
            HostPortSettingsMenuItem.DropDownItems.Add(PortSetManualMenuItem);

            ToolStripMenuItem HostPortLifetimeSelectMenuItem = new ToolStripMenuItem();
            HostPortLifetimeSelectMenuItem.Text = "Lifetime";
            HostPortLifetimeSelectMenuItem.Tag = x.host;
            HostPortSettingsMenuItem.DropDownItems.Add(HostPortLifetimeSelectMenuItem);

            foreach (KeyValuePair<int, string> Life in this.Lifetimes)
            {
                ToolStripMenuItem i = new ToolStripMenuItem();
                i.Text = Life.Value;
                if (Life.Key == x.lifetime)
                {
                    i.Checked = true;
                }
                i.Click += delegate (Object sender1, EventArgs ee)
                {
                    ToolStripMenuItem s = sender1 as ToolStripMenuItem;

                    PingRequest.SendCommandToTargetAsync(x.host, "lifetime", Life.Key.ToString());

                    timerTickAsync(null, null);
                };
                HostPortLifetimeSelectMenuItem.DropDownItems.Add(i);
            }

            ToolStripMenuItem MachineIntervalMenuItem = new ToolStripMenuItem();
            MachineIntervalMenuItem.Text = "Ping Interval";
            HostMenuItem.DropDownItems.Add(MachineIntervalMenuItem);

            foreach (KeyValuePair<int, string> Ping in this.PingsIntervals)
            {
                ToolStripMenuItem i = new ToolStripMenuItem();
                i.Text = Ping.Value;
                i.Tag = Ping.Key;
                if (Ping.Key == x.interval)
                {
                    i.Checked = true;
                }
                i.Click += delegate (Object sender1, EventArgs ee)
                {
                    ToolStripMenuItem s = sender1 as ToolStripMenuItem;

                    PingRequest.SendCommandToTargetAsync(x.host, "interval", Ping.Key.ToString());

                    timerTickAsync(null, null);
                };
                MachineIntervalMenuItem.DropDownItems.Add(i);
            }

            ToolStripMenuItem TestCommandMenuItem = new ToolStripMenuItem();
            TestCommandMenuItem.Text = "Test Command Bus";
            TestCommandMenuItem.Tag = x.host;
            TestCommandMenuItem.Click += new EventHandler(SendTestCommand);
            HostMenuItem.DropDownItems.Add(TestCommandMenuItem);

            HostMenuItem.DropDownItems.Add("-");

            ToolStripMenuItem MachineStatusMenuItem = new ToolStripMenuItem();
            MachineStatusMenuItem.Text = "Machine Information";
            MachineStatusMenuItem.Tag = x.host;
            MachineStatusMenuItem.Width = 400;
            HostMenuItem.DropDownItems.Add(MachineStatusMenuItem);

            int width = 300;
            Font f = new Font("Consolas", 9);

            MachineStatusMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                        new ToolStripLabel
                        {
                            Text = "Host Name         :  " + x.host,
                            Width = width,
                            Font = f
                        },
                        new ToolStripLabel
                        {
                            Text = "Machine GUID      :  " + x.guid,
                            Width = width,
                            Font = f
                        },
                        new ToolStripLabel
                        {
                            Text = "External IP       :  " + x.wanip,
                            Width = width,
                            Font = f
                        },
                        new ToolStripLabel
                        {
                            Text = "Internal IP       :  " + x.lanip,
                            Width = width,
                            Font = f
                        },
                        new ToolStripLabel
                        {
                            Text = "Mapped Port       :  " + x.port,
                            Width = width,
                            Font = f
                        },
                        new ToolStripLabel
                        {
                            Text = "Interval          :  " + x.interval + " Seconds",
                            Width = width,
                            Font = f
                        },
                        new ToolStripLabel
                        {
                            Text = "Version           :  " + x.version,
                            Width = width,
                            Font = f
                        },
                        new ToolStripLabel
                        {
                            Text = "Port Lifetime     :  " + x.lifetime + " Minutes",
                            Width = width,
                            Font = f
                        },
                        new ToolStripLabel
                        {
                            Text = "Port Status       :  " + x.status,
                            Width = width,
                            Font = f
                        },
                        new ToolStripLabel
                        {
                            Text = "Service Installed :  " + x.serviceinstalled,
                            Width = width,
                            Font = f
                        },
                        new ToolStripLabel
                        {
                            Text = "Service Running   :  " + x.servicerunning,
                            Width = width,
                            Font = f
                        }
                    });

            return HostMenuItem;
        }
    }
}
