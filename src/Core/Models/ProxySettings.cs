using System;

namespace ADBLogin.Core.Models
{
    public enum ProxyProtocol
    {
        Direct,
        Http,
        Https,
        Socks4,
        Socks5
    }

    public class ProxySettings
    {
        public ProxyProtocol Protocol { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public bool IsEnabled
        {
            get
            {
                return Protocol != ProxyProtocol.Direct && !string.IsNullOrEmpty(Host) && Port > 0;
            }
        }

        public bool HasCredentials
        {
            get
            {
                return !string.IsNullOrEmpty(Username);
            }
        }

        public ProxySettings()
        {
            Protocol = ProxyProtocol.Direct;
            Host = string.Empty;
            Port = 0;
            Username = string.Empty;
            Password = string.Empty;
        }

        public static ProxySettings Parse(string rawProxy, ProxyProtocol defaultProtocol = ProxyProtocol.Http)
        {
            var settings = new ProxySettings();
            settings.Protocol = defaultProtocol;

            if (string.IsNullOrEmpty(rawProxy) || rawProxy.Trim().Length == 0)
            {
                settings.Protocol = ProxyProtocol.Direct;
                return settings;
            }

            string clean = rawProxy.Trim();

            if (clean.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase))
            {
                settings.Protocol = ProxyProtocol.Socks5;
                clean = clean.Substring(9);
            }
            else if (clean.StartsWith("socks4://", StringComparison.OrdinalIgnoreCase))
            {
                settings.Protocol = ProxyProtocol.Socks4;
                clean = clean.Substring(9);
            }
            else if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                settings.Protocol = ProxyProtocol.Http;
                clean = clean.Substring(7);
            }
            else if (clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                settings.Protocol = ProxyProtocol.Https;
                clean = clean.Substring(8);
            }

            clean = clean.Replace('|', ':');

            if (clean.Contains("@"))
            {
                var parts = clean.Split('@');
                var authParts = parts[0].Split(':');
                var serverParts = parts[1].Split(':');

                if (authParts.Length > 0) settings.Username = authParts[0];
                if (authParts.Length > 1) settings.Password = authParts[1];

                if (serverParts.Length > 0) settings.Host = serverParts[0];
                if (serverParts.Length > 1)
                {
                    int p;
                    if (int.TryParse(serverParts[1], out p)) settings.Port = p;
                }

                return settings;
            }

            var segments = clean.Split(':');
            if (segments.Length >= 2)
            {
                settings.Host = segments[0];
                int p;
                if (int.TryParse(segments[1], out p)) settings.Port = p;

                if (segments.Length >= 4)
                {
                    settings.Username = segments[2];
                    settings.Password = segments[3];
                }
            }

            return settings;
        }

        public override string ToString()
        {
            if (!IsEnabled) return "Direct";
            string protocolStr = Protocol.ToString().ToLower();
            string server = string.Format("{0}:{1}", Host, Port);
            return HasCredentials
                ? string.Format("{0}://{1}:{2}@{3}", protocolStr, Username, Password, server)
                : string.Format("{0}://{1}", protocolStr, server);
        }
    }
}
