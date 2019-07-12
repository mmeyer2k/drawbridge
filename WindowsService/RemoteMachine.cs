namespace drawbridge
{
    public class RemoteMachine
    {
        public string guid;
        public string wanip;
        public string lanip;
        public int port;
        public string host;
        public string status;
        public bool pending;
        public bool rdpopen;
        public int interval;
        public int lifetime;
        public string version;
        public bool servicerunning;
        public bool serviceinstalled;
    }

    class RemoteMachineImportJson
    {
        public string guid;
        public string wanip;
        public string lanip;
        public string port;
        public string host;
        public string status;
        //public string pending;
        public string rdpopen;
        public string interval;
        public string lifetime;
        public string version;
        public string servicerunning;
        public string serviceinstalled;
    }
}
