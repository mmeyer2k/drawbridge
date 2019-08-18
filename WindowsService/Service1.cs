using System;
using System.ComponentModel;
using System.ServiceProcess;
using Mono.Nat;
using System.Diagnostics;
using drawbridge;
using System.IO;
using System.Threading;

namespace WindowsService
{
    public partial class Service1 : ServiceBase
    {
        private INatDevice Router;
        private bool RouterFound = false;

        public Service1()
        {
            // Setup Service
            this.ServiceName = "Drawbridge";
            this.CanStop = true;
            this.CanPauseAndContinue = true;

            // Setup logging
            this.AutoLog = true;

            ((ISupportInitialize)this.EventLog).BeginInit();

            if (!EventLog.SourceExists(this.ServiceName))
            {
                EventLog.CreateEventSource(this.ServiceName, "Application");
            }

            ((ISupportInitialize)this.EventLog).EndInit();

            this.EventLog.Source = this.ServiceName;
            this.EventLog.Log = "Application";

            try
            {
                InitializeComponent();
            }
            catch (Exception exc)
            {
                this.EventLog.WriteEntry(exc.ToString(), EventLogEntryType.Warning);
            }
        }

        protected override void OnStart(string[] args)
        {
            string Interval = Registry.Get("Interval");

            // If interval can not be found for some reason, set to 60s
            if (Interval == "")
            {
                Interval = "60";
            }

            try
            {
                // Set up a timer that triggers every minute.
                System.Timers.Timer timer = new System.Timers.Timer();
                timer.Interval = Int32.Parse(Interval) * 1000;
                timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnTimer);
                timer.Start();

                NatUtility.DeviceFound += DeviceFound;
                NatUtility.DeviceLost += DeviceLost;
                NatUtility.StartDiscovery();

                // Send first tick at start up
                this.OnTimer(null, null);
            }
            catch (Exception exc)
            {
                this.EventLog.WriteEntry(exc.ToString(), EventLogEntryType.Warning);
            }
        }

        protected override void OnStop()
        {

        }

        protected async void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            // If UI is running then we can skip sending pings from the service
            // This is done by reading the timestamp placed in the temp file and 
            // seeing if it exceeds 1 minute
            try
            {
                string tmp = Path.Combine(Path.GetTempPath(), "DrawbridgeTimestamp.txt");
                long stamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                if (stamp - Int32.Parse(File.ReadAllText(tmp)) < 60)
                {
                    this.EventLog.WriteEntry("UI is running... skipping ping.", EventLogEntryType.Information);

                    return;
                }
            }
            catch (Exception exc)
            {
                this.EventLog.WriteEntry(exc.ToString(), EventLogEntryType.Warning);
            }

            // Get the API key and protocol key from registry
            string Key = Registry.Get("Key");
            string ApiKey = Registry.Get("ApiKey");

            // If either key is not found or if router is not responding
            // write a log entry and abory until next loop
            if (this.RouterFound == false || ApiKey == "" || Key == "")
            {
                this.EventLog.WriteEntry("Skipping ping.", EventLogEntryType.Warning);

                return;
            }

            PingRequest ping = new PingRequest();

            try
            {
                await ping.SendAsync(Router, ApiKey, Key);
            }
            catch (Exception exc)
            {
                this.EventLog.WriteEntry(exc.ToString(), EventLogEntryType.Warning);
            }

            // If this ping request resulted in commands which were processed, then a 
            // new thread should run another ping request to send any updated status
            // back to the central server
            if (ping.Command != null)
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    this.OnTimer(null, null);
                }).Start();
            }
        }

        private void DeviceFound(object sender, DeviceEventArgs args)
        {
            this.Router = args.Device;
            this.RouterFound = true;

            // Send a ping as soon as router is found
            this.OnTimer(null, null);
        }

        private void DeviceLost(object sender, DeviceEventArgs args)
        {
            this.RouterFound = false;
        }
    }
}
