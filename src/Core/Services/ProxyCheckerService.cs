using System;
using System.Diagnostics;
using ADBLogin.Core.Models;
using Leaf.xNet;

namespace ADBLogin.Core.Services
{
    public class ProxyCheckResult
    {
        public bool IsLive { get; set; }
        public string ExternalIp { get; set; }
        public long PingMs { get; set; }
        public string Message { get; set; }

        public ProxyCheckResult()
        {
            IsLive = false;
            ExternalIp = string.Empty;
            PingMs = 0;
            Message = string.Empty;
        }

        public override string ToString()
        {
            if (IsLive)
            {
                return string.Format("🟢 LIVE ({0}ms | IP: {1})", PingMs, ExternalIp);
            }
            return string.Format("🔴 DIE ({0})", string.IsNullOrEmpty(Message) ? "Khong the ket noi" : Message);
        }
    }

    /// <summary>
    /// Service kiem tra trang thai Proxy toc do cao su dung thu vien Leaf.xNet
    /// </summary>
    public class ProxyCheckerService
    {
        private const string TestEndpoint = "http://api.ipify.org";
        private const int DefaultTimeoutMs = 6000;

        /// <summary>
        /// Kiem tra 1 proxy bat ky
        /// </summary>
        public ProxyCheckResult CheckProxy(ProxySettings proxy, int timeoutMs = DefaultTimeoutMs)
        {
            var result = new ProxyCheckResult();

            if (proxy == null || !proxy.IsEnabled)
            {
                result.IsLive = true;
                result.ExternalIp = "Direct IP";
                result.Message = "Mang truc tiep";
                return result;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                using (var request = new HttpRequest())
                {
                    request.ConnectTimeout = timeoutMs;
                    request.ReadWriteTimeout = timeoutMs;
                    request.KeepAlive = false;
                    request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

                    // Gan cau hinh Proxy vao Leaf.xNet
                    ProxyClient proxyClient = null;

                    if (proxy.Protocol == ProxyProtocol.Socks5)
                    {
                        proxyClient = proxy.HasCredentials
                            ? new Socks5ProxyClient(proxy.Host, proxy.Port, proxy.Username, proxy.Password)
                            : new Socks5ProxyClient(proxy.Host, proxy.Port);
                    }
                    else if (proxy.Protocol == ProxyProtocol.Socks4)
                    {
                        proxyClient = proxy.HasCredentials
                            ? new Socks4ProxyClient(proxy.Host, proxy.Port, proxy.Username)
                            : new Socks4ProxyClient(proxy.Host, proxy.Port);
                    }
                    else
                    {
                        // HTTP / HTTPS
                        proxyClient = proxy.HasCredentials
                            ? new HttpProxyClient(proxy.Host, proxy.Port, proxy.Username, proxy.Password)
                            : new HttpProxyClient(proxy.Host, proxy.Port);
                    }

                    request.Proxy = proxyClient;

                    // Gui yeu cau kiem tra
                    var response = request.Get(TestEndpoint);
                    stopwatch.Stop();

                    string ip = response.ToString().Trim();
                    if (!string.IsNullOrEmpty(ip))
                    {
                        result.IsLive = true;
                        result.ExternalIp = ip;
                        result.PingMs = stopwatch.ElapsedMilliseconds;
                        result.Message = "Ket noi on dinh";
                    }
                    else
                    {
                        result.IsLive = false;
                        result.Message = "Khong phan hoi IP";
                    }
                }
            }
            catch (HttpException hex)
            {
                stopwatch.Stop();
                result.IsLive = false;
                result.PingMs = stopwatch.ElapsedMilliseconds;
                result.Message = hex.Message;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.IsLive = false;
                result.PingMs = stopwatch.ElapsedMilliseconds;
                result.Message = ex.Message;
            }

            return result;
        }

        public ProxyCheckResult CheckProxyString(string rawProxy, int timeoutMs = DefaultTimeoutMs)
        {
            var proxy = ProxySettings.Parse(rawProxy);
            return CheckProxy(proxy, timeoutMs);
        }
    }
}
