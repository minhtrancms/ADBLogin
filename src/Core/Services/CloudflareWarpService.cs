using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ADBLogin.Core.Models;
using Leaf.xNet;

namespace ADBLogin.Core.Services
{
    /// <summary>
    /// Service quản lý tạo và chạy hàng loạt Proxy từ Cloudflare WARP (1.1.1.1)
    /// Sử dụng WireGuard & Wireproxy độc lập trong Userspace, không cần tài khoản, hoàn toàn miễn phí
    /// </summary>
    public class CloudflareWarpService
    {
        private static CloudflareWarpService _instance;
        private static readonly object _instanceLock = new object();

        public static CloudflareWarpService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new CloudflareWarpService();
                        }
                    }
                }
                return _instance;
            }
        }

        private static readonly string ToolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "wireproxy");
        private static readonly string WgcfExe = Path.Combine(ToolsDir, "wgcf.exe");
        private static readonly string WireproxyExe = Path.Combine(ToolsDir, "wireproxy.exe");
        private static readonly string AccountsDir = Path.Combine(ToolsDir, "warp_accounts");
        private static readonly string ConfigsDir = Path.Combine(ToolsDir, "warp_configs");

        public List<HmaProxyPortItem> PortItems { get; private set; }
        private readonly Dictionary<int, Process> _runningProcesses = new Dictionary<int, Process>();
        private readonly object _lock = new object();

        public event Action<HmaProxyPortItem> PortStatusChanged;
        public event Action<string> LogReceived;

        public CloudflareWarpService()
        {
            PortItems = new List<HmaProxyPortItem>();
            EnsureDirectories();
        }

        private void EnsureDirectories()
        {
            try
            {
                if (!Directory.Exists(ToolsDir)) Directory.CreateDirectory(ToolsDir);
                if (!Directory.Exists(AccountsDir)) Directory.CreateDirectory(AccountsDir);
                if (!Directory.Exists(ConfigsDir)) Directory.CreateDirectory(ConfigsDir);
            }
            catch { }
        }

        public void Log(string message)
        {
            string formatted = string.Format("[{0:HH:mm:ss}] [WARP] {1}", DateTime.Now, message);
            try
            {
                if (LogReceived != null)
                {
                    LogReceived(formatted);
                }
            }
            catch { }
        }

        public bool AreToolsAvailable()
        {
            return File.Exists(WgcfExe) && File.Exists(WireproxyExe);
        }

        /// <summary>
        /// Khởi tạo danh sách các cổng WARP Proxy
        /// </summary>
        public void InitializePorts(int startPort, int count)
        {
            lock (_lock)
            {
                PortItems.Clear();
                for (int i = 0; i < count; i++)
                {
                    int port = startPort + i;
                    var item = new HmaProxyPortItem
                    {
                        Port = port,
                        Protocol = "socks5",
                        ServerName = string.Format("Cloudflare WARP #{0}", i + 1),
                        Status = HmaTunnelStatus.Stopped,
                        StatusText = "Chờ khởi động"
                    };
                    PortItems.Add(item);
                }

                UpdateAssignedProfiles();
            }
        }

        public void UpdateAssignedProfiles()
        {
            lock (_lock)
            {
                var profiles = AccountManager.Instance.GetAllProfiles();
                foreach (var item in PortItems)
                {
                    item.AssignedProfileNames.Clear();
                    string target = string.Format("127.0.0.1:{0}", item.Port);

                    foreach (var p in profiles)
                    {
                        if (!string.IsNullOrEmpty(p.Proxy) && p.Proxy.Contains(target))
                        {
                            item.AssignedProfileNames.Add(p.ProfileName);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Khởi động 1 cổng WARP Proxy cụ thể
        /// </summary>
        public async Task<bool> StartPortAsync(int port)
        {
            HmaProxyPortItem item;
            lock (_lock)
            {
                item = PortItems.FirstOrDefault(p => p.Port == port);
            }

            if (item == null) return false;

            if (!AreToolsAvailable())
            {
                item.Status = HmaTunnelStatus.Error;
                item.StatusText = "Thiếu wireproxy/wgcf";
                NotifyStatusChanged(item);
                Log("❌ Lỗi: Không tìm thấy tools\\wireproxy\\wireproxy.exe hoặc wgcf.exe");
                return false;
            }

            item.Status = HmaTunnelStatus.Starting;
            item.StatusText = "Đang khởi tạo...";
            item.LastError = string.Empty;
            NotifyStatusChanged(item);

            Log(string.Format("Đang chuẩn bị tài khoản WARP cho cổng {0}...", port));

            try
            {
                // 1. Sinh hoặc lấy tài khoản WARP
                string profilePath = await EnsureWarpProfileAsync(port);
                if (string.IsNullOrEmpty(profilePath) || !File.Exists(profilePath))
                {
                    throw new Exception("Không thể tạo cấu hình Cloudflare WARP!");
                }

                // 2. Tạo file cấu hình wireproxy
                string wireproxyConf = GenerateWireproxyConfig(port, profilePath);

                // 3. Khởi động tiến trình wireproxy
                var psi = new ProcessStartInfo
                {
                    FileName = WireproxyExe,
                    Arguments = string.Format("-c \"{0}\"", wireproxyConf),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var proc = new Process { StartInfo = psi };
                proc.OutputDataReceived += (s, e) => { };
                proc.ErrorDataReceived += (s, e) => { };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                lock (_lock)
                {
                    if (_runningProcesses.ContainsKey(port))
                    {
                        try { _runningProcesses[port].Kill(); } catch { }
                    }
                    _runningProcesses[port] = proc;
                    item.ProcessId = proc.Id;
                }

                await Task.Delay(3500);

                item.Status = HmaTunnelStatus.Connected;
                item.StatusText = "🟢 LIVE (SOCKS5)";
                NotifyStatusChanged(item);
                Log(string.Format("✅ Cổng WARP {0} đã mở thành công! (socks5://127.0.0.1:{0})", port));

                // Kiểm tra IP Public
                var checkTask = Task.Run(() => CheckPortPublicIpAsync(item));
                return true;
            }
            catch (Exception ex)
            {
                item.Status = HmaTunnelStatus.Error;
                item.StatusText = "Lỗi khởi động";
                item.LastError = ex.Message;
                NotifyStatusChanged(item);
                Log(string.Format("❌ Lỗi mở cổng {0}: {1}", port, ex.Message));
                return false;
            }
        }

        private async Task<string> EnsureWarpProfileAsync(int port)
        {
            string accDir = Path.Combine(AccountsDir, string.Format("acc_{0}", port));
            if (!Directory.Exists(accDir)) Directory.CreateDirectory(accDir);

            string profileFile = Path.Combine(accDir, "wgcf-profile.conf");
            if (File.Exists(profileFile))
            {
                return profileFile;
            }

            Log(string.Format("Đang đăng ký danh tính WARP mới trên Cloudflare (Cổng {0})...", port));

            // Chạy wgcf register
            await RunProcessAsync(WgcfExe, "register --accept-tos", accDir);
            // Chạy wgcf generate
            await RunProcessAsync(WgcfExe, "generate", accDir);

            if (File.Exists(profileFile))
            {
                return profileFile;
            }

            return null;
        }

        private string GenerateWireproxyConfig(int port, string wgcfProfilePath)
        {
            string content = File.ReadAllText(wgcfProfilePath);
            string privateKey = "";
            string address = "172.16.0.2/32";
            string publicKey = "bmXOC+F1FxEMF9dyiK2H5/1SUtzH0JuVo51h2wPfgyo=";
            string endpoint = "162.159.192.1:2408";

            var mKey = Regex.Match(content, @"PrivateKey\s*=\s*(.+)");
            if (mKey.Success) privateKey = mKey.Groups[1].Value.Trim();

            var mAddr = Regex.Match(content, @"Address\s*=\s*([0-9\.\/]+)");
            if (mAddr.Success) address = mAddr.Groups[1].Value.Trim();

            var mPub = Regex.Match(content, @"PublicKey\s*=\s*(.+)");
            if (mPub.Success) publicKey = mPub.Groups[1].Value.Trim();

            string confPath = Path.Combine(ConfigsDir, string.Format("wireproxy_{0}.conf", port));
            string confContent = string.Format(
@"[Interface]
PrivateKey = {0}
Address = {1}
DNS = 1.1.1.1

[Peer]
PublicKey = {2}
Endpoint = {3}
AllowedIPs = 0.0.0.0/0

[Socks5]
BindAddress = 127.0.0.1:{4}
", privateKey, address, publicKey, endpoint, port);

            File.WriteAllText(confPath, confContent);
            return confPath;
        }

        private async Task RunProcessAsync(string exe, string args, string workingDir)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            {
                await Task.Run(() => proc.WaitForExit(15000));
            }
        }

        public void StopPort(int port)
        {
            lock (_lock)
            {
                if (_runningProcesses.ContainsKey(port))
                {
                    try
                    {
                        var proc = _runningProcesses[port];
                        if (!proc.HasExited) proc.Kill();
                        proc.Dispose();
                    }
                    catch { }
                    _runningProcesses.Remove(port);
                }

                var item = PortItems.FirstOrDefault(p => p.Port == port);
                if (item != null)
                {
                    item.Status = HmaTunnelStatus.Stopped;
                    item.StatusText = "Đã dừng";
                    item.PublicIp = "---";
                    item.Country = "---";
                    item.City = "---";
                    item.Isp = "---";
                    item.PingMs = 0;
                    item.ProcessId = 0;
                    NotifyStatusChanged(item);
                }
            }

            Log(string.Format("Đã dừng cổng WARP {0}", port));
        }

        public async Task StartAllAsync()
        {
            List<int> ports;
            lock (_lock)
            {
                ports = PortItems.Select(p => p.Port).ToList();
            }

            foreach (var port in ports)
            {
                await StartPortAsync(port);
            }
        }

        public void StopAll()
        {
            List<int> ports;
            lock (_lock)
            {
                ports = PortItems.Select(p => p.Port).ToList();
            }

            foreach (var port in ports)
            {
                StopPort(port);
            }
        }

        /// <summary>
        /// Tự động đổi IP cho cổng: Xóa cấu hình cũ và đăng ký tài khoản WARP mới
        /// </summary>
        public async Task ResetPortIpAsync(int port)
        {
            StopPort(port);
            string accDir = Path.Combine(AccountsDir, string.Format("acc_{0}", port));
            try
            {
                if (Directory.Exists(accDir)) Directory.Delete(accDir, true);
            }
            catch { }

            Log(string.Format("Đã làm mới danh tính cổng {0}. Đang kết nối lại...", port));
            await StartPortAsync(port);
        }

        public async Task CheckPortPublicIpAsync(HmaProxyPortItem item)
        {
            var sw = Stopwatch.StartNew();
            bool checkSuccess = false;

            try
            {
                using (var req = new HttpRequest())
                {
                    req.Proxy = new Socks5ProxyClient("127.0.0.1", item.Port);
                    req.ConnectTimeout = 6000;
                    req.ReadWriteTimeout = 6000;
                    req.UserAgent = "Mozilla/5.0";

                    var resp = await Task.Run(() => req.Get("http://api.ipify.org"));
                    sw.Stop();

                    string ip = resp.ToString().Trim();
                    if (!string.IsNullOrEmpty(ip))
                    {
                        item.PublicIp = ip;
                        item.Country = "Cloudflare";
                        item.Isp = "Cloudflare WARP";
                        item.PingMs = sw.ElapsedMilliseconds;
                        item.StatusText = "🟢 LIVE (WARP)";
                        checkSuccess = true;
                        NotifyStatusChanged(item);
                        Log(string.Format("Cổng {0}: Public IP: {1} (Cloudflare WARP) - {2}ms", item.Port, item.PublicIp, item.PingMs));
                    }
                }
            }
            catch (Exception ex)
            {
                item.LastError = ex.Message;
            }

            if (!checkSuccess)
            {
                item.StatusText = "Không phản hồi";
                NotifyStatusChanged(item);
            }
        }

        public int AssignProxiesToProfiles(List<UserProfile> profiles, List<int> selectedPorts = null)
        {
            if (profiles == null || profiles.Count == 0) return 0;

            List<int> availablePorts;
            lock (_lock)
            {
                if (selectedPorts != null && selectedPorts.Count > 0)
                {
                    availablePorts = selectedPorts;
                }
                else
                {
                    availablePorts = PortItems.Select(p => p.Port).ToList();
                }
            }

            if (availablePorts.Count == 0) return 0;

            int updated = 0;
            for (int i = 0; i < profiles.Count; i++)
            {
                int assignedPort = availablePorts[i % availablePorts.Count];
                profiles[i].Proxy = string.Format("socks5://127.0.0.1:{0}", assignedPort);
                AccountManager.Instance.AddOrUpdateProfile(profiles[i]);
                updated++;
            }

            AccountManager.Instance.SaveProfiles();
            UpdateAssignedProfiles();

            lock (_lock)
            {
                foreach (var item in PortItems)
                {
                    NotifyStatusChanged(item);
                }
            }

            Log(string.Format("Đã gán thành công {0} profile vào {1} cổng Cloudflare WARP Proxy!", updated, availablePorts.Count));
            return updated;
        }

        private void NotifyStatusChanged(HmaProxyPortItem item)
        {
            try
            {
                if (PortStatusChanged != null)
                {
                    PortStatusChanged(item);
                }
            }
            catch { }
        }
    }
}
