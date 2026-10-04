using System;
using System.Collections.Generic;

namespace ADBLogin.Core.Models
{
    public enum HmaTunnelStatus
    {
        Stopped,
        Starting,
        Connected,
        Error
    }

    public class HmaConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string OvpnDirectory { get; set; }
        public int StartPort { get; set; }
        public int PortCount { get; set; }
        public string CustomOpenVpnPath { get; set; }
        public List<string> SelectedOvpnFiles { get; set; }

        public HmaConfig()
        {
            Username = string.Empty;
            Password = string.Empty;
            OvpnDirectory = string.Empty;
            StartPort = 10001;
            PortCount = 5;
            CustomOpenVpnPath = string.Empty;
            SelectedOvpnFiles = new List<string>();
        }
    }

    public class HmaProxyPortItem
    {
        public int Port { get; set; }
        public string OvpnPath { get; set; }
        public string ServerName { get; set; }
        public HmaTunnelStatus Status { get; set; }
        public string StatusText { get; set; }
        public string PublicIp { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Isp { get; set; }
        public long PingMs { get; set; }
        public string LocalTunnelIp { get; set; }
        public int ProcessId { get; set; }
        public string LastError { get; set; }
        public List<string> AssignedProfileNames { get; set; }

        public string ProxyAddress
        {
            get { return string.Format("127.0.0.1:{0}", Port); }
        }

        public HmaProxyPortItem()
        {
            Port = 10001;
            OvpnPath = string.Empty;
            ServerName = "Tự động";
            Status = HmaTunnelStatus.Stopped;
            StatusText = "Đã dừng";
            PublicIp = "---";
            Country = "---";
            City = "---";
            Isp = "---";
            PingMs = 0;
            LocalTunnelIp = string.Empty;
            ProcessId = 0;
            LastError = string.Empty;
            AssignedProfileNames = new List<string>();
        }
    }
}
