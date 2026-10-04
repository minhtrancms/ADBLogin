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
        /// Khởi động 1 cổng WARP Proxy cụ thể với endpoint tùy chọn
        /// </summary>
        public async Task<bool> StartPortAsync(int port, int endpointOffset = 0)
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

            try
            {
                // 1. Sinh hoặc lấy tài khoản WARP
                string profilePath = await EnsureWarpProfileAsync(port);
                if (string.IsNullOrEmpty(profilePath) || !File.Exists(profilePath))
                {
                    throw new Exception("Không thể tạo cấu hình Cloudflare WARP!");
                }

                // 2. Tạo file cấu hình wireproxy với Endpoint xoay vòng
                string wireproxyConf = GenerateWireproxyConfig(port, profilePath, endpointOffset);

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

        /// <summary>
        /// Khởi động 1 cổng và tự động kiểm tra, xoay Endpoint liên tục nếu bị trùng IP với các cổng trước
        /// </summary>
        public async Task<bool> StartPortWithUniqueIpAsync(int port, HashSet<string> existingIps, int maxAttempts = 10)
        {
            HmaProxyPortItem item;
            lock (_lock)
            {
                item = PortItems.FirstOrDefault(p => p.Port == port);
            }

            if (item == null) return false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Log(string.Format("Cổng {0}: Đang kết nối (Thử lần {1}, Endpoint #{2})...", port, attempt + 1, attempt));

                // Nếu thử từ lần thứ 2 trở lên mà vẫn trùng, xóa profile cũ để wgcf đăng ký tài khoản WARP mới
                if (attempt >= 2)
                {
                    string accDir = Path.Combine(AccountsDir, string.Format("acc_{0}", port));
                    if (Directory.Exists(accDir))
                    {
                        try { Directory.Delete(accDir, true); } catch { }
                    }
                }

                bool started = await StartPortAsync(port, attempt);
                if (!started) continue;

                // Chờ kiểm tra IP Public thực tế
                await CheckPortPublicIpAsync(item);

                if (!string.IsNullOrEmpty(item.PublicIp) && item.PublicIp != "---")
                {
                    if (existingIps == null || !existingIps.Contains(item.PublicIp))
                    {
                        // Thành công: IP hoàn toàn độc nhất!
                        if (existingIps != null) existingIps.Add(item.PublicIp);
                        item.StatusText = "🟢 LIVE (Độc nhất)";
                        NotifyStatusChanged(item);
                        Log(string.Format("🎉 Cổng {0} đã nhận IP ĐỘC NHẤT: {1}", port, item.PublicIp));
                        return true;
                    }
                    else
                    {
                        // Bị trùng IP với một cổng khác đã mở!
                        Log(string.Format("⚠️ Cổng {0} nhận IP {1} (Bị trùng với cổng khác). Đang tự động đổi Endpoint...", port, item.PublicIp));
                        item.StatusText = string.Format("🔄 Trùng ({0}) -> Đang đổi...", item.PublicIp);
                        NotifyStatusChanged(item);
                        StopPort(port);
                        await Task.Delay(1000);
                    }
                }
            }

            // Nếu sau maxAttempts vẫn không tìm được IP khác, giữ kết nối cuối cùng
            await StartPortAsync(port, 0);
            await CheckPortPublicIpAsync(item);
            return true;
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

        private static readonly string[] CloudflareEndpoints = new string[]
        {
            "162.159.192.1:2408",
            "162.159.192.3:2408",
            "188.114.96.1:2408",
            "188.114.96.3:1701",
            "188.114.97.2:500",
            "162.159.192.5:854",
            "188.114.98.3:4500",
            "162.159.195.2:2408",
            "188.114.99.3:2408",
            "162.159.192.7:2408",
            "162.159.192.9:500",
            "188.114.96.5:1701",
            "188.114.97.5:2408",
            "162.159.195.4:854",
            "188.114.96.7:2408",
            "188.114.97.7:500",
            "162.159.192.11:1701",
            "188.114.98.5:2408",
            "188.114.99.5:4500",
            "162.159.195.5:2408"
        };

        private string GenerateWireproxyConfig(int port, string wgcfProfilePath, int endpointOffset = 0)
        {
            string content = File.ReadAllText(wgcfProfilePath);
            string privateKey = "";
            string address = "172.16.0.2/32";
            string publicKey = "bmXOC+F1FxEMF9dyiK2H5/1SUtzH0JuVo51h2wPfgyo=";
            
            // Xoay vong endpoint theo port va attempt offset
            int epIdx = Math.Abs((port + endpointOffset).GetHashCode()) % CloudflareEndpoints.Length;
            string endpoint = CloudflareEndpoints[epIdx];

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

        public async Task StartAllAsync(bool enforceUniqueIps = true)
        {
            List<int> ports;
            lock (_lock)
            {
                ports = PortItems.Select(p => p.Port).ToList();
            }

            var existingIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var port in ports)
            {
                if (enforceUniqueIps)
                {
                    await StartPortWithUniqueIpAsync(port, existingIps);
                }
                else
                {
                    await StartPortAsync(port);
                }
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
        /// Quét tất cả các cổng đang chạy, phát hiện các cổng bị trùng IP và tự động đổi sang IP khác
        /// </summary>
        public async Task<int> DeduplicateAllPortsAsync()
        {
            Log("🔍 Bắt đầu kiểm tra và đổi các cổng bị trùng IP...");
            List<HmaProxyPortItem> runningItems;
            lock (_lock)
            {
                runningItems = PortItems.Where(p => p.Status == HmaTunnelStatus.Connected).ToList();
            }

            if (runningItems.Count == 0)
            {
                Log("Không có cổng nào đang chạy.");
                return 0;
            }

            var uniqueIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicatePorts = new List<int>();

            foreach (var item in runningItems)
            {
                if (!string.IsNullOrEmpty(item.PublicIp) && item.PublicIp != "---")
                {
                    if (!uniqueIps.Contains(item.PublicIp))
                    {
                        uniqueIps.Add(item.PublicIp);
                    }
                    else
                    {
                        duplicatePorts.Add(item.Port);
                    }
                }
                else
                {
                    duplicatePorts.Add(item.Port);
                }
            }

            if (duplicatePorts.Count == 0)
            {
                Log("✅ Tuyệt vời! Tất cả các cổng đang chạy đều có IP ĐỘC NHẤT, không bị trùng.");
                return 0;
            }

            Log(string.Format("Phát hiện {0} cổng bị trùng IP. Đang tự động đổi IP cho từng cổng...", duplicatePorts.Count));
            int fixedCount = 0;

            foreach (var port in duplicatePorts)
            {
                bool success = await StartPortWithUniqueIpAsync(port, uniqueIps, 10);
                if (success) fixedCount++;
            }

            Log(string.Format("🎉 Hoàn tất! Đã đổi thành công {0}/{1} cổng trùng IP.", fixedCount, duplicatePorts.Count));
            return fixedCount;
        }

        /// <summary>
        /// Tự động đổi IP cho cổng: Xóa cấu hình cũ và đăng ký tài khoản WARP mới, đồng thời tránh trùng IP với các cổng đang chạy khác
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

            Log(string.Format("Đã làm mới danh tính cổng {0}. Đang kết nối và dò tìm IP độc nhất...", port));

            HashSet<string> existingIps;
            lock (_lock)
            {
                existingIps = new HashSet<string>(
                    PortItems.Where(p => p.Port != port && !string.IsNullOrEmpty(p.PublicIp) && p.PublicIp != "---")
                             .Select(p => p.PublicIp),
                    StringComparer.OrdinalIgnoreCase);
            }

            await StartPortWithUniqueIpAsync(port, existingIps, 10);
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

                    string ip = await Task.Run(() =>
                    {
                        try
                        {
                            var resp = req.Get("http://api.ipify.org");
                            return resp.ToString().Trim();
                        }
                        catch
                        {
                            try
                            {
                                var resp2 = req.Get("http://icanhazip.com");
                                return resp2.ToString().Trim();
                            }
                            catch
                            {
                                try
                                {
                                    var resp3 = req.Get("https://api.myip.com");
                                    var m = Regex.Match(resp3.ToString(), @"""ip""\s*:\s*""([^""]+)""");
                                    if (m.Success) return m.Groups[1].Value.Trim();
                                }
                                catch { }
                                return string.Empty;
                            }
                        }
                    });
                    sw.Stop();

                    if (!string.IsNullOrEmpty(ip) && ip.Length < 45)
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
