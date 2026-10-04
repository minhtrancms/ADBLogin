using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ADBLogin.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ADBLogin.Core.Services
{
    public class HmaMultiProxyService
    {
        private static HmaMultiProxyService _instance;
        private static readonly object _instanceLock = new object();

        public static HmaMultiProxyService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new HmaMultiProxyService();
                        }
                    }
                }
                return _instance;
            }
        }

        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hma_config.json");
        private static readonly string TempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_hma");

        public HmaConfig Config { get; private set; }
        public List<HmaProxyPortItem> PortItems { get; private set; }

        private readonly Dictionary<int, Process> _runningProcesses = new Dictionary<int, Process>();
        private readonly Dictionary<int, LocalHttpProxyServer> _runningProxies = new Dictionary<int, LocalHttpProxyServer>();
        private readonly object _lock = new object();
        private static readonly Dictionary<string, string> _dnsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _dnsLock = new object();

        public static async Task<string> ResolveHostFastAsync(string hostname)
        {
            if (string.IsNullOrEmpty(hostname)) return null;

            IPAddress ip;
            if (IPAddress.TryParse(hostname, out ip))
            {
                return hostname;
            }

            lock (_dnsLock)
            {
                if (_dnsCache.ContainsKey(hostname))
                {
                    return _dnsCache[hostname];
                }
            }

            // 1. Thu phan giai DNS thong thuong
            try
            {
                var dnsTask = Dns.GetHostAddressesAsync(hostname);
                if (await Task.WhenAny(dnsTask, Task.Delay(1500)) == dnsTask)
                {
                    var addrs = dnsTask.Result;
                    if (addrs != null && addrs.Length > 0)
                    {
                        string res = addrs[0].ToString();
                        lock (_dnsLock) { _dnsCache[hostname] = res; }
                        return res;
                    }
                }
            }
            catch { }

            // 2. Fallback: Google DNS-over-HTTPS (Chong ISP chan / Query refused ten mien)
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | SecurityProtocolType.Tls;
                var req = (HttpWebRequest)WebRequest.Create("https://dns.google/resolve?name=" + hostname);
                req.Timeout = 3500;
                req.ReadWriteTimeout = 3500;
                using (var resp = (HttpWebResponse)await req.GetResponseAsync())
                using (var s = resp.GetResponseStream())
                using (var r = new StreamReader(s))
                {
                    string json = await r.ReadToEndAsync();
                    var match = Regex.Match(json, @"""data""\s*:\s*""(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})""");
                    if (match.Success)
                    {
                        string dohIp = match.Groups[1].Value;
                        lock (_dnsLock) { _dnsCache[hostname] = dohIp; }
                        return dohIp;
                    }
                }
            }
            catch { }

            // 3. Fallback: Cloudflare DNS-over-HTTPS
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("https://1.1.1.1/dns-query?name=" + hostname);
                req.Headers.Add("Accept", "application/dns-json");
                req.Timeout = 3500;
                req.ReadWriteTimeout = 3500;
                using (var resp = (HttpWebResponse)await req.GetResponseAsync())
                using (var s = resp.GetResponseStream())
                using (var r = new StreamReader(s))
                {
                    string json = await r.ReadToEndAsync();
                    var match = Regex.Match(json, @"""data""\s*:\s*""(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})""");
                    if (match.Success)
                    {
                        string dohIp = match.Groups[1].Value;
                        lock (_dnsLock) { _dnsCache[hostname] = dohIp; }
                        return dohIp;
                    }
                }
            }
            catch { }

            return null;
        }

        public event Action<HmaProxyPortItem> PortStatusChanged;
        public event Action<string> LogReceived;

        public HmaMultiProxyService()
        {
            PortItems = new List<HmaProxyPortItem>();
            LoadConfig();
            EnsureDirectory();
        }

        private void EnsureDirectory()
        {
            try
            {
                if (!Directory.Exists(TempDir))
                {
                    Directory.CreateDirectory(TempDir);
                }
            }
            catch { }
        }

        public void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    Config = JsonConvert.DeserializeObject<HmaConfig>(json);
                }
            }
            catch { }

            if (Config == null)
            {
                Config = new HmaConfig();
            }
        }

        public void SaveConfig()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Log("Lỗi lưu cấu hình HMA: " + ex.Message);
            }
        }

        public void Log(string message)
        {
            string formatted = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, message);
            try
            {
                if (LogReceived != null)
                {
                    LogReceived(formatted);
                }
            }
            catch { }
        }

        /// <summary>
        /// Tự động tìm đường dẫn file openvpn.exe trên hệ thống Windows
        /// </summary>
        public string FindOpenVpnExecutable()
        {
            if (!string.IsNullOrEmpty(Config.CustomOpenVpnPath) && File.Exists(Config.CustomOpenVpnPath))
            {
                return Config.CustomOpenVpnPath;
            }

            string[] candidates = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "openvpn", "openvpn.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "openvpn", "openvpn.exe"),
                @"C:\Program Files\OpenVPN\bin\openvpn.exe",
                @"C:\Program Files (x86)\OpenVPN\bin\openvpn.exe",
                @"C:\Program Files\OpenVPNConnect\OpenVPNConnect.exe"
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Kiểm tra trong PATH
            var envPath = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                foreach (var p in envPath.Split(';'))
                {
                    try
                    {
                        var candidate = Path.Combine(p.Trim(), "openvpn.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch { }
                }
            }

            return null;
        }

        /// <summary>
        /// Tự động tải danh sách file cấu hình OpenVPN của NordVPN từ NordCDN
        /// </summary>
        public async Task<int> DownloadNordVpnConfigsAsync(string targetDir, int limit = 20, int countryId = 234)
        {
            if (string.IsNullOrEmpty(targetDir)) return 0;
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            Log("🌐 Đang truy vấn danh sách máy chủ tối ưu từ NordVPN API...");
            int downloaded = 0;
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
                ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, sslErr) => true;

                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
                    string apiUrl = countryId > 0
                        ? string.Format("https://api.nordvpn.com/v1/servers/recommendations?filters[country_id]={0}&limit={1}", countryId, limit)
                        : string.Format("https://api.nordvpn.com/v1/servers/recommendations?limit={0}", limit);
                    string json = await client.DownloadStringTaskAsync(apiUrl);
                    var arr = JArray.Parse(json);

                    foreach (var item in arr)
                    {
                        string hostname = (string)item["hostname"];
                        if (string.IsNullOrEmpty(hostname)) continue;

                        string ovpnUrl = string.Format("https://downloads.nordcdn.com/configs/files/ovpn_udp/servers/{0}.udp.ovpn", hostname);
                        string destFile = Path.Combine(targetDir, string.Format("{0}.ovpn", hostname));

                        try
                        {
                            await client.DownloadFileTaskAsync(ovpnUrl, destFile);
                            downloaded++;
                            Log(string.Format("✅ Đã tải cấu hình server NordVPN: {0}", hostname));
                        }
                        catch (Exception ex)
                        {
                            Log(string.Format("Lỗi tải {0}: {1}", hostname, ex.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Lỗi kết nối NordVPN API: " + ex.Message);
            }

            Log(string.Format("🎉 Hoàn tất tải {0} file cấu hình server NordVPN vào thư mục: {1}", downloaded, targetDir));
            return downloaded;
        }

        /// <summary>
        /// Tự động tải danh sách file cấu hình OpenVPN miễn phí từ VPN Gate (GitHub API)
        /// </summary>
        public async Task<int> DownloadVpnGateConfigsAsync(string targetDir, int limit = 20)
        {
            if (string.IsNullOrEmpty(targetDir)) return 0;
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            Log("🌐 Đang truy vấn danh sách server miễn phí từ VPN Gate (GitHub)...");
            int downloaded = 0;
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
                    string apiUrl = "https://api.github.com/repos/fdciabdul/Vpngate-Scraper-API/contents/configs";
                    string json = await client.DownloadStringTaskAsync(apiUrl);
                    var arr = JArray.Parse(json);

                    foreach (var item in arr)
                    {
                        if (downloaded >= limit) break;
                        string name = (string)item["name"];
                        string downloadUrl = (string)item["download_url"];
                        if (string.IsNullOrEmpty(name) || !name.EndsWith(".ovpn") || string.IsNullOrEmpty(downloadUrl)) continue;

                        string destFile = Path.Combine(targetDir, name);
                        try
                        {
                            await client.DownloadFileTaskAsync(downloadUrl, destFile);
                            downloaded++;
                            Log(string.Format("✅ Đã tải server VPN Gate miễn phí: {0}", name));
                        }
                        catch (Exception ex)
                        {
                            Log(string.Format("Lỗi tải {0}: {1}", name, ex.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Lỗi kết nối GitHub VPN Gate API: " + ex.Message);
            }

            Log(string.Format("🎉 Hoàn tất tải {0} file cấu hình server VPN Gate miễn phí vào: {1}", downloaded, targetDir));
            return downloaded;
        }

        /// <summary>
        /// Quét tất cả file .ovpn trong thư mục do người dùng cung cấp
        /// </summary>
        public List<KeyValuePair<string, string>> ScanOvpnFiles(string folder)
        {
            var results = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return results;
            }

            try
            {
                var files = Directory.GetFiles(folder, "*.ovpn", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // Bỏ qua file lỗi 404 HTML
                    try
                    {
                        var fi = new FileInfo(file);
                        if (fi.Length < 1500)
                        {
                            string text = File.ReadAllText(file);
                            if (text.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        }
                    }
                    catch { }

                    string filename = Path.GetFileNameWithoutExtension(file);
                    // Định dạng tên đẹp: e.g. USA.NewYork.TCP -> USA - NewYork (TCP)
                    string displayName = filename.Replace(".", " ").Replace("_", " ");
                    results.Add(new KeyValuePair<string, string>(file, displayName));
                }
            }
            catch (Exception ex)
            {
                Log("Lỗi quét file .ovpn: " + ex.Message);
            }

            return results;
        }

        /// <summary>
        /// Kiểm tra một cổng TCP trên máy tính có đang thực sự sẵn sàng không
        /// </summary>
        public static bool IsPortAvailable(int port)
        {
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    s.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    s.Close();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tự động tìm dải cổng liên tiếp hoàn toàn trống, tránh hoàn toàn xung đột cổng
        /// </summary>
        public static int FindCleanPortRange(int preferredStartPort, int count)
        {
            int current = preferredStartPort;
            while (current < 65000)
            {
                bool allOk = true;
                for (int i = 0; i < count; i++)
                {
                    if (!IsPortAvailable(current + i))
                    {
                        allOk = false;
                        current += i + 1;
                        break;
                    }
                }
                if (allOk) return current;
            }
            return preferredStartPort;
        }

        /// <summary>
        /// Khởi tạo danh sách các cổng Proxy từ cấu hình
        /// </summary>
        public void InitializePorts(int startPort, int count, List<string> ovpnFiles)
        {
            lock (_lock)
            {
                PortItems.Clear();
                for (int i = 0; i < count; i++)
                {
                    int port = startPort + i;
                    string ovpnPath = (ovpnFiles != null && i < ovpnFiles.Count) ? ovpnFiles[i] : (ovpnFiles != null && ovpnFiles.Count > 0 ? ovpnFiles[i % ovpnFiles.Count] : string.Empty);
                    string serverName = !string.IsNullOrEmpty(ovpnPath) ? Path.GetFileNameWithoutExtension(ovpnPath).Replace(".", " ") : string.Format("Máy chủ #{0}", i + 1);

                    var item = new HmaProxyPortItem
                    {
                        Port = port,
                        OvpnPath = ovpnPath,
                        ServerName = serverName,
                        Status = HmaTunnelStatus.Stopped,
                        StatusText = "Chờ khởi động"
                    };

                    PortItems.Add(item);
                }

                UpdateAssignedProfiles();
            }
        }

        /// <summary>
        /// Cập nhật danh sách profile đang sử dụng từng cổng
        /// </summary>
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
        /// Khởi động 1 cổng proxy cụ thể
        /// </summary>
        public async Task<bool> StartPortAsync(int port)
        {
            HmaProxyPortItem item;
            lock (_lock)
            {
                item = PortItems.FirstOrDefault(p => p.Port == port);
            }

            if (item == null) return false;

            string openVpnExe = FindOpenVpnExecutable();
            bool hasOpenVpn = !string.IsNullOrEmpty(openVpnExe);

            item.Status = HmaTunnelStatus.Starting;
            item.StatusText = "Đang khởi tạo...";
            item.LastError = string.Empty;
            NotifyStatusChanged(item);

            Log(string.Format("Đang khởi động cổng {0} (Máy chủ: {1})...", port, item.ServerName));

            bool isNordVpn = (!string.IsNullOrEmpty(item.OvpnPath) && item.OvpnPath.IndexOf("nordvpn", StringComparison.OrdinalIgnoreCase) >= 0)
                          || (!string.IsNullOrEmpty(item.ServerName) && item.ServerName.IndexOf("nordvpn", StringComparison.OrdinalIgnoreCase) >= 0);

            string upstreamHost = null;
            string upstreamResolvedIp = null;
            int upstreamPort = 0;
            string upstreamUser = null;
            string upstreamPass = null;
            bool upstreamSsl = false;
            string detectedTunnelIp = null;

            if (isNordVpn)
            {
                // Trích xuất hostname NordVPN (ví dụ vn57.nordvpn.com)
                string host = Path.GetFileNameWithoutExtension(!string.IsNullOrEmpty(item.OvpnPath) ? item.OvpnPath : item.ServerName).Replace(".udp", "").Replace(".tcp", "").Trim();
                if (!host.EndsWith(".nordvpn.com", StringComparison.OrdinalIgnoreCase))
                {
                    host = host + ".nordvpn.com";
                }
                upstreamHost = host;
                upstreamPort = 89;
                upstreamUser = Config.Username;
                upstreamPass = Config.Password;
                upstreamSsl = true;

                Log(string.Format("Cổng {0}: Đang kết nối phân giải IP máy chủ {1}...", port, upstreamHost));
                upstreamResolvedIp = await ResolveHostFastAsync(upstreamHost);
                if (string.IsNullOrEmpty(upstreamResolvedIp))
                {
                    item.Status = HmaTunnelStatus.Error;
                    item.StatusText = "Không nhận IP máy chủ";
                    item.LastError = string.Format("Máy chủ {0} không phản hồi DNS hoặc tạm thời gián đoạn.", upstreamHost);
                    NotifyStatusChanged(item);
                    Log(string.Format("❌ Cổng {0}: Máy chủ {1} không tìm thấy địa chỉ IP!", port, upstreamHost));
                    return false;
                }

                Log(string.Format("Cổng {0}: Cầu nối bảo mật NordVPN SSL ({1} -> {2}:89)...", port, upstreamHost, upstreamResolvedIp));
                item.StatusText = "Đang kết nối NordVPN...";
            }
            else if (hasOpenVpn && !string.IsNullOrEmpty(item.OvpnPath) && File.Exists(item.OvpnPath))
            {
                // Dùng OpenVPN engine thật
                try
                {
                    detectedTunnelIp = await LaunchOpenVpnTunnelAsync(item, openVpnExe);
                }
                catch (Exception ex)
                {
                    item.LastError = ex.Message;
                    Log(string.Format("Lỗi chạy OpenVPN cổng {0}: {1}", port, ex.Message));
                }
            }
            else
            {
                Log(string.Format("Cổng {0}: Chạy chế độ Local HTTP Proxy Bridge độc lập...", port));
                item.StatusText = "Bridge HTTP Sẵn Sàng";
            }

            // Nếu dùng OpenVPN nhưng chưa nhận được IP adapter
            if (!isNordVpn && !string.IsNullOrEmpty(item.OvpnPath) && string.IsNullOrEmpty(detectedTunnelIp))
            {
                item.Status = HmaTunnelStatus.Error;
                item.StatusText = "VPN chưa cấp IP";
                if (string.IsNullOrEmpty(item.LastError))
                {
                    item.LastError = "Không thể kết nối tới server OpenVPN hoặc card mạng TAP đang bận.";
                }
                NotifyStatusChanged(item);
                Log(string.Format("❌ Cổng {0}: OpenVPN chưa kết nối được tới {1} ({2})", port, item.ServerName, item.LastError));
                return false;
            }

            // Khởi động Local HTTP Proxy Server trên 127.0.0.1:port
            try
            {
                StartLocalProxyListener(item.Port, detectedTunnelIp, upstreamHost, upstreamResolvedIp, upstreamPort, upstreamUser, upstreamPass, upstreamSsl, strictVpn: isNordVpn || !string.IsNullOrEmpty(item.OvpnPath));
                item.LocalTunnelIp = detectedTunnelIp;
                item.Status = HmaTunnelStatus.Connected;
                item.StatusText = isNordVpn ? "Đang chạy (NordVPN SSL)" : (!string.IsNullOrEmpty(detectedTunnelIp) ? "Đang chạy (VPN)" : "Đang chạy (Local)");
                NotifyStatusChanged(item);

                Log(string.Format("✅ Cổng {0} đã mở thành công! (127.0.0.1:{0})", port));

                // Bắt đầu kiểm tra IP Public thực tế
                Task.Run(async () =>
                {
                    try
                    {
                        await CheckPortPublicIpAsync(item);
                    }
                    catch { }
                });
                return true;
            }
            catch (Exception ex)
            {
                item.Status = HmaTunnelStatus.Error;
                item.StatusText = "Cổng bị trùng";
                item.LastError = ex.Message;
                NotifyStatusChanged(item);
                if (ex.Message.Contains("Only one usage of each socket address") || ex is SocketException)
                {
                    Log(string.Format("❌ Lỗi mở cổng {0}: Cổng này đang bị chiếm dụng (ví dụ: do Cloudflare WARP đang chạy trên Tab 1). Vui lòng đổi sang dải cổng 20001 hoặc dừng WARP trước khi bật OpenVPN.", port));
                }
                else
                {
                    Log(string.Format("❌ Lỗi mở cổng {0}: {1}", port, ex.Message));
                }
                return false;
            }
        }

        private async Task<string> LaunchOpenVpnTunnelAsync(HmaProxyPortItem item, string openVpnExe)
        {
            EnsureDirectory();
            string authFile = Path.Combine(TempDir, string.Format("auth_{0}.txt", item.Port));
            string user = Config.Username;
            string pass = Config.Password;
            if (!string.IsNullOrEmpty(item.OvpnPath) && item.OvpnPath.IndexOf("vpngate", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                user = "vpn";
                pass = "vpn";
            }
            File.WriteAllText(authFile, string.Format("{0}\r\n{1}\r\n", user ?? "vpn", pass ?? "vpn"));

            string runtimeOvpn = Path.Combine(TempDir, string.Format("tunnel_{0}.ovpn", item.Port));
            var originalLines = File.ReadAllLines(item.OvpnPath);
            var newLines = new List<string>();

            foreach (var line in originalLines)
            {
                string trimmed = line.Trim();
                // Loại bỏ lệnh chiếm default gateway máy chủ
                if (trimmed.StartsWith("redirect-gateway", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                newLines.Add(line);
            }

            // Thêm các cờ cách ly mạng: KHÔNG ảnh hưởng mạng chính máy tính
            newLines.Add("route-nopull");
            newLines.Add(string.Format("auth-user-pass \"{0}\"", authFile.Replace("\\", "\\\\")));
            newLines.Add("auth-retry nointeract");
            newLines.Add("verb 3");

            File.WriteAllLines(runtimeOvpn, newLines);

            var psi = new ProcessStartInfo
            {
                FileName = openVpnExe,
                Arguments = string.Format("--config \"{0}\"", runtimeOvpn),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var proc = new Process { StartInfo = psi };
            string detectedIp = null;
            var tcs = new TaskCompletionSource<bool>();

            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // Quét IP tunnel từ output: e.g. "Notified TAP-Windows driver to set a DHCP IP/netmask of 10.x.x.x" hoặc "ifconfig 10.x.x.x"
                    var match = Regex.Match(e.Data, @"(?:DHCP IP|ifconfig|IPv4 MTU set to \d+ on interface)\D+(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})");
                    if (match.Success && string.IsNullOrEmpty(detectedIp))
                    {
                        detectedIp = match.Groups[1].Value;
                    }

                    if (e.Data.Contains("Initialization Sequence Completed"))
                    {
                        tcs.TrySetResult(true);
                    }
                }
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    if (e.Data.IndexOf("AUTH_FAILED", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        item.LastError = "Sai tài khoản/mật khẩu HMA OpenVPN!";
                        tcs.TrySetResult(false);
                    }
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            lock (_lock)
            {
                _runningProcesses[item.Port] = proc;
                item.ProcessId = proc.Id;
            }

            // Chờ tối đa 12 giây để tunnel OpenVPN kết nối
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(12000));
            if (completedTask == tcs.Task && tcs.Task.Result)
            {
                Log(string.Format("Cổng {0}: OpenVPN kết nối thành công! IP Adapter: {1}", item.Port, detectedIp ?? "Tự động"));
            }
            else
            {
                Log(string.Format("Cổng {0}: Đã kích hoạt tiến trình OpenVPN (PID: {1})", item.Port, proc.Id));
            }

            return detectedIp;
        }

        private void StartLocalProxyListener(int port, string outboundBindingIp, string upstreamHost = null, string upstreamResolvedIp = null, int upstreamPort = 0, string upstreamUser = null, string upstreamPass = null, bool upstreamSsl = false, bool strictVpn = false)
        {
            lock (_lock)
            {
                if (_runningProxies.ContainsKey(port))
                {
                    _runningProxies[port].Stop();
                    _runningProxies.Remove(port);
                }

                var server = new LocalHttpProxyServer(port, outboundBindingIp)
                {
                    UpstreamHost = upstreamHost,
                    UpstreamResolvedIp = upstreamResolvedIp,
                    UpstreamPort = upstreamPort,
                    UpstreamUser = upstreamUser,
                    UpstreamPass = upstreamPass,
                    UpstreamSsl = upstreamSsl,
                    StrictVpnOnly = strictVpn
                };
                server.Start();
                _runningProxies[port] = server;
            }
        }

        /// <summary>
        /// Dừng 1 cổng proxy
        /// </summary>
        public void StopPort(int port)
        {
            lock (_lock)
            {
                if (_runningProxies.ContainsKey(port))
                {
                    try { _runningProxies[port].Stop(); } catch { }
                    _runningProxies.Remove(port);
                }

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

            Log(string.Format("Đã dừng cổng {0}", port));
        }

        /// <summary>
        /// Khởi động tất cả các cổng đã nạp
        /// </summary>
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

        /// <summary>
        /// Dừng tất cả các cổng đang chạy
        /// </summary>
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
        /// Kiểm tra IP Public thực tế thông qua cổng proxy 127.0.0.1:port
        /// </summary>
        public async Task CheckPortPublicIpAsync(HmaProxyPortItem item)
        {
            var sw = Stopwatch.StartNew();
            bool ipFound = false;

            // Danh sách các dịch vụ kiểm tra IP nhanh gọn (HTTP trước cực nhanh ~200ms, HTTPS dự phòng)
            string[] testUrls = new string[]
            {
                "http://api.ipify.org",
                "http://icanhazip.com",
                "http://checkip.amazonaws.com",
                "https://api.ipify.org"
            };

            foreach (var url in testUrls)
            {
                try
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
                    ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, sslErr) => true;

                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Proxy = new WebProxy("127.0.0.1", item.Port);
                    req.Timeout = 4000;
                    req.ReadWriteTimeout = 4000;
                    req.UserAgent = "curl/7.88.1";

                    using (var resp = (HttpWebResponse)await req.GetResponseAsync())
                    using (var stream = resp.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        string ip = (await reader.ReadToEndAsync()).Trim();
                        sw.Stop();
                        IPAddress parsedIp;
                        if (!string.IsNullOrEmpty(ip) && ip.Length >= 7 && IPAddress.TryParse(ip, out parsedIp))
                        {
                            item.PublicIp = ip;
                            item.PingMs = sw.ElapsedMilliseconds;
                            item.StatusText = "🟢 LIVE";
                            item.Country = "Đang lấy vị trí...";
                            NotifyStatusChanged(item);
                            ipFound = true;
                            Log(string.Format("Cổng {0}: Đã nhận Public IP: {1} ({2}ms)", item.Port, item.PublicIp, item.PingMs));
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    item.LastError = ex.Message;
                }
            }

            if (!ipFound)
            {
                // Fallback ip-api qua proxy
                try
                {
                    var req2 = (HttpWebRequest)WebRequest.Create("http://ip-api.com/json");
                    req2.Proxy = new WebProxy("127.0.0.1", item.Port);
                    req2.Timeout = 5000;
                    using (var resp2 = (HttpWebResponse)await req2.GetResponseAsync())
                    using (var s2 = resp2.GetResponseStream())
                    using (var r2 = new StreamReader(s2))
                    {
                        string json = await r2.ReadToEndAsync();
                        var obj = JObject.Parse(json);
                        item.PublicIp = (string)obj["query"] ?? "---";
                        item.Country = (string)obj["country"] ?? "---";
                        item.City = (string)obj["city"] ?? "---";
                        item.Isp = (string)obj["isp"] ?? "---";
                        item.PingMs = sw.ElapsedMilliseconds;
                        item.StatusText = "🟢 LIVE";
                        NotifyStatusChanged(item);
                        ipFound = true;
                        Log(string.Format("Cổng {0}: Public IP: {1} ({2}, {3}) - {4}ms", item.Port, item.PublicIp, item.Country, item.City, item.PingMs));
                    }
                }
                catch (Exception ex2)
                {
                    item.LastError = ex2.Message;
                }
            }

            // Lấy thông tin quốc gia & thành phố trực tiếp từ IP đã có (cực nhanh không tốn băng thông proxy)
            if (ipFound && !string.IsNullOrEmpty(item.PublicIp) && item.PublicIp != "---" && (item.Country == "Đang lấy vị trí..." || item.Country == "Quốc tế" || string.IsNullOrEmpty(item.Country)))
            {
                try
                {
                    var geoReq = (HttpWebRequest)WebRequest.Create("http://ip-api.com/json/" + item.PublicIp);
                    geoReq.Timeout = 3000;
                    using (var geoResp = (HttpWebResponse)await geoReq.GetResponseAsync())
                    using (var geoStream = geoResp.GetResponseStream())
                    using (var geoReader = new StreamReader(geoStream))
                    {
                        string json = await geoReader.ReadToEndAsync();
                        var obj = JObject.Parse(json);
                        item.Country = (string)obj["country"] ?? item.Country;
                        item.City = (string)obj["city"] ?? "---";
                        item.Isp = (string)obj["isp"] ?? "---";
                        NotifyStatusChanged(item);
                    }
                }
                catch { }
            }

            if (!ipFound)
            {
                item.PublicIp = "---";
                if (!string.IsNullOrEmpty(item.LastError) && (item.LastError.Contains("407") || item.LastError.Contains("Proxy Authentication")))
                {
                    item.StatusText = "⚠️ Hết phiên (Session limit)";
                    Log(string.Format("Cổng {0}: Tài khoản NordVPN đã hết lượt kết nối đồng thời (Session limit reached).", item.Port));
                }
                else if (!string.IsNullOrEmpty(item.LastError) && item.LastError.Contains("503"))
                {
                    item.StatusText = "⚠️ Kill-Switch chặn lộ IP";
                }
                else
                {
                    item.StatusText = "❌ Chưa nhận IP (" + item.LastError + ")";
                    Log(string.Format("Cổng {0}: Chưa nhận được Public IP ({1})", item.Port, item.LastError));
                }
                NotifyStatusChanged(item);
            }
        }

        /// <summary>
        /// Gán hàng loạt các cổng proxy vào danh sách Profile
        /// </summary>
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
                profiles[i].Proxy = string.Format("127.0.0.1:{0}", assignedPort);
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

            Log(string.Format("Đã gán thành công {0} profile vào {1} cổng proxy HMA!", updated, availablePorts.Count));
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

    /// <summary>
    /// Local HTTP/HTTPS CONNECT Proxy Server siêu nhẹ viết bằng C# Socket, 
    /// có khả năng ghim Socket gửi đi (Outbound) vào Adapter IP của VPN tương ứng
    /// </summary>
    public class LocalHttpProxyServer
    {
        public int Port { get; private set; }
        public string OutboundBindingIp { get; private set; }
        public string UpstreamHost { get; set; }
        public string UpstreamResolvedIp { get; set; }
        public int UpstreamPort { get; set; }
        public string UpstreamUser { get; set; }
        public string UpstreamPass { get; set; }
        public bool UpstreamSsl { get; set; }
        public bool StrictVpnOnly { get; set; }

        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public LocalHttpProxyServer(int port, string outboundBindingIp = null)
        {
            Port = port;
            OutboundBindingIp = outboundBindingIp;
        }

        public void Start()
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start(100);
            _isRunning = true;

            Task.Run(() => AcceptClientsAsync(_cts.Token));
        }

        public void Stop()
        {
            _isRunning = false;
            try { if (_cts != null) _cts.Cancel(); } catch { }
            try { if (_listener != null) _listener.Stop(); } catch { }
        }

        private async Task AcceptClientsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isRunning)
            {
                bool hasError = false;
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    var clientTask = Task.Run(() => HandleClientAsync(client, token), token);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception)
                {
                    if (!_isRunning) break;
                    hasError = true;
                }

                if (hasError)
                {
                    await Task.Delay(50, token);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            using (var clientStream = client.GetStream())
            {
                try
                {
                    byte[] tempBuf = new byte[4096];
                    int initialRead = await clientStream.ReadAsync(tempBuf, 0, tempBuf.Length, token);
                    if (initialRead <= 0) return;

                    // ==============================================================
                    // 1. KIỂM TRA & XỬ LÝ SOCKS5 (Khi client kết nối bằng giao thức SOCKS5)
                    // ==============================================================
                    if (tempBuf[0] == 0x05)
                    {
                        // SOCKS5 greeting: Trả lời 0x05, 0x00 (No authentication)
                        await clientStream.WriteAsync(new byte[] { 0x05, 0x00 }, 0, 2, token);

                        // Đọc yêu cầu CONNECT từ client
                        byte[] reqBuf = new byte[512];
                        int reqRead = await clientStream.ReadAsync(reqBuf, 0, reqBuf.Length, token);
                        if (reqRead < 7 || reqBuf[0] != 0x05 || reqBuf[1] != 0x01) return; // Chỉ hỗ trợ CONNECT

                        string targetHost = null;
                        int targetPort = 80;
                        byte atyp = reqBuf[3];
                        if (atyp == 0x01) // IPv4
                        {
                            targetHost = string.Format("{0}.{1}.{2}.{3}", reqBuf[4], reqBuf[5], reqBuf[6], reqBuf[7]);
                            targetPort = (reqBuf[8] << 8) | reqBuf[9];
                        }
                        else if (atyp == 0x03) // Domain name
                        {
                            int dlen = reqBuf[4];
                            targetHost = Encoding.ASCII.GetString(reqBuf, 5, dlen);
                            targetPort = (reqBuf[5 + dlen] << 8) | reqBuf[6 + dlen];
                        }
                        else return;

                        if (string.IsNullOrEmpty(targetHost)) return;

                        // Chế độ Upstream NordVPN SSL Proxy
                        if (!string.IsNullOrEmpty(UpstreamHost) && UpstreamPort > 0)
                        {
                            using (var upstreamClient = new TcpClient())
                            {
                                string connTarget = !string.IsNullOrEmpty(UpstreamResolvedIp) ? UpstreamResolvedIp : UpstreamHost;
                                var connTask = upstreamClient.ConnectAsync(connTarget, UpstreamPort);
                                if (await Task.WhenAny(connTask, Task.Delay(7000, token)) != connTask) return;

                                Stream targetStream = upstreamClient.GetStream();
                                SslStream sslStream = null;

                                if (UpstreamSsl)
                                {
                                    sslStream = new SslStream(targetStream, false, (s, cert, chain, err) => true);
                                    var authTask = sslStream.AuthenticateAsClientAsync(UpstreamHost, null, SslProtocols.Tls12, false);
                                    if (await Task.WhenAny(authTask, Task.Delay(5000, token)) != authTask) return;
                                    targetStream = sslStream;
                                }

                                try
                                {
                                    string auth = !string.IsNullOrEmpty(UpstreamUser)
                                        ? Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", UpstreamUser, UpstreamPass ?? string.Empty)))
                                        : string.Empty;

                                    string connectReq = string.Format("CONNECT {0}:{1} HTTP/1.1\r\nHost: {0}:{1}\r\nProxy-Authorization: Basic {2}\r\n\r\n", targetHost, targetPort, auth);
                                    byte[] connectBytes = Encoding.ASCII.GetBytes(connectReq);
                                    await targetStream.WriteAsync(connectBytes, 0, connectBytes.Length, token);
                                    await targetStream.FlushAsync(token);

                                    byte[] respBuf = new byte[4096];
                                    var readTask = targetStream.ReadAsync(respBuf, 0, respBuf.Length, token);
                                    if (await Task.WhenAny(readTask, Task.Delay(5000, token)) != readTask) return;
                                    int respRead = await readTask;
                                    if (respRead <= 0) return;
                                    string respStr = Encoding.ASCII.GetString(respBuf, 0, respRead);
                                    if (!respStr.Contains("200")) return;

                                    // Phản hồi SOCKS5 Success cho client: 05 00 00 01 00 00 00 00 00 00
                                    byte[] socks5Ok = new byte[] { 0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0 };
                                    await clientStream.WriteAsync(socks5Ok, 0, socks5Ok.Length, token);
                                    await clientStream.FlushAsync(token);

                                    var t1 = RelayStreamAsync(clientStream, targetStream, token);
                                    var t2 = RelayStreamAsync(targetStream, clientStream, token);
                                    await Task.WhenAny(t1, t2);
                                }
                                finally
                                {
                                    if (sslStream != null) sslStream.Dispose();
                                }
                            }
                            return;
                        }

                        // Chế độ Outbound Socket trực tiếp / OpenVPN
                        if (StrictVpnOnly && string.IsNullOrEmpty(OutboundBindingIp)) return;

                        using (var targetSocket = CreateOutboundSocket())
                        {
                            var connTask = targetSocket.ConnectAsync(targetHost, targetPort);
                            if (await Task.WhenAny(connTask, Task.Delay(7000, token)) != connTask) return;

                            byte[] socks5Ok = new byte[] { 0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0 };
                            await clientStream.WriteAsync(socks5Ok, 0, socks5Ok.Length, token);

                            using (var targetStream = new NetworkStream(targetSocket))
                            {
                                var t1 = RelayStreamAsync(clientStream, targetStream, token);
                                var t2 = RelayStreamAsync(targetStream, clientStream, token);
                                await Task.WhenAny(t1, t2);
                            }
                        }
                        return;
                    }

                    // ==============================================================
                    // 2. XỬ LÝ HTTP / HTTPS CONNECT PROXY
                    // ==============================================================
                    var ms = new MemoryStream();
                    ms.Write(tempBuf, 0, initialRead);
                    string headerStr = Encoding.ASCII.GetString(ms.ToArray());
                    int endHeaderIdx = headerStr.IndexOf("\r\n\r\n");

                    while (endHeaderIdx < 0 && ms.Length <= 65536)
                    {
                        int read = await clientStream.ReadAsync(tempBuf, 0, tempBuf.Length, token);
                        if (read <= 0) break;
                        ms.Write(tempBuf, 0, read);
                        headerStr = Encoding.ASCII.GetString(ms.ToArray());
                        endHeaderIdx = headerStr.IndexOf("\r\n\r\n");
                    }

                    if (string.IsNullOrEmpty(headerStr) || endHeaderIdx < 0) return;

                    string[] lines = headerStr.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;

                    string requestLine = lines[0];
                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    string method = parts[0].ToUpper();
                    string target = parts[1];

                    // 1. CHẾ ĐỘ UPSTREAM PROXY (Cầu nối bảo mật NordVPN SSL Proxy trên Port 89)
                    if (!string.IsNullOrEmpty(UpstreamHost) && UpstreamPort > 0)
                    {
                        string targetHost;
                        int targetPort;

                        if (method == "CONNECT")
                        {
                            string[] hp = target.Split(':');
                            targetHost = hp[0];
                            targetPort = hp.Length > 1 ? int.Parse(hp[1]) : 443;
                        }
                        else
                        {
                            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                            {
                                var uri = new Uri(target);
                                targetHost = uri.Host;
                                targetPort = uri.Port > 0 ? uri.Port : 80;
                            }
                            else
                            {
                                targetHost = null;
                                targetPort = 80;
                                foreach (var l in lines)
                                {
                                    if (l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string h = l.Substring(5).Trim();
                                        if (h.Contains(":"))
                                        {
                                            var hp = h.Split(':');
                                            targetHost = hp[0];
                                            int.TryParse(hp[1], out targetPort);
                                        }
                                        else targetHost = h;
                                        break;
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(targetHost)) return;

                        using (var upstreamClient = new TcpClient())
                        {
                            string connTarget = !string.IsNullOrEmpty(UpstreamResolvedIp) ? UpstreamResolvedIp : UpstreamHost;
                            var connTask = upstreamClient.ConnectAsync(connTarget, UpstreamPort);
                            if (await Task.WhenAny(connTask, Task.Delay(7000, token)) != connTask) return;

                            Stream targetStream = upstreamClient.GetStream();
                            SslStream sslStream = null;

                            if (UpstreamSsl)
                            {
                                sslStream = new SslStream(targetStream, false, (s, cert, chain, err) => true);
                                var authTask = sslStream.AuthenticateAsClientAsync(UpstreamHost, null, SslProtocols.Tls12, false);
                                if (await Task.WhenAny(authTask, Task.Delay(5000, token)) != authTask) return;
                                targetStream = sslStream;
                            }

                            try
                            {
                                string auth = !string.IsNullOrEmpty(UpstreamUser)
                                    ? Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", UpstreamUser, UpstreamPass ?? string.Empty)))
                                    : string.Empty;

                                string connectReq = string.Format("CONNECT {0}:{1} HTTP/1.1\r\nHost: {0}:{1}\r\nProxy-Authorization: Basic {2}\r\n\r\n", targetHost, targetPort, auth);
                                byte[] connectBytes = Encoding.ASCII.GetBytes(connectReq);
                                await targetStream.WriteAsync(connectBytes, 0, connectBytes.Length, token);
                                await targetStream.FlushAsync(token);

                                byte[] respBuf = new byte[4096];
                                var readTask = targetStream.ReadAsync(respBuf, 0, respBuf.Length, token);
                                if (await Task.WhenAny(readTask, Task.Delay(5000, token)) != readTask) return;
                                int respRead = await readTask;
                                if (respRead <= 0) return;
                                string respStr = Encoding.ASCII.GetString(respBuf, 0, respRead);

                                if (!respStr.Contains("200"))
                                {
                                    // Forward upstream response (e.g. 407 Session Limit) to client
                                    await clientStream.WriteAsync(respBuf, 0, respRead, token);
                                    await clientStream.FlushAsync(token);
                                    return;
                                }

                                if (method == "CONNECT")
                                {
                                    // HTTPS tunnel established
                                    await clientStream.WriteAsync(respBuf, 0, respRead, token);
                                    await clientStream.FlushAsync(token);
                                }
                                else
                                {
                                    // Convert GET http://host/path to GET /path
                                    string modifiedHeader = headerStr;
                                    if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var uri = new Uri(target);
                                        string pathAndQuery = uri.PathAndQuery;
                                        modifiedHeader = lines[0].Replace(target, pathAndQuery);
                                        for (int i = 1; i < lines.Length; i++)
                                        {
                                            modifiedHeader += "\r\n" + lines[i];
                                        }
                                    }

                                    byte[] forwardBytes = Encoding.ASCII.GetBytes(modifiedHeader);
                                    await targetStream.WriteAsync(forwardBytes, 0, forwardBytes.Length, token);
                                    await targetStream.FlushAsync(token);

                                    int initialHeaderLen = endHeaderIdx + 4;
                                    byte[] fullBytes = ms.ToArray();
                                    if (fullBytes.Length > initialHeaderLen)
                                    {
                                        await targetStream.WriteAsync(fullBytes, initialHeaderLen, fullBytes.Length - initialHeaderLen, token);
                                        await targetStream.FlushAsync(token);
                                    }
                                }

                                var t1 = RelayStreamAsync(clientStream, targetStream, token);
                                var t2 = RelayStreamAsync(targetStream, clientStream, token);
                                await Task.WhenAny(t1, t2);
                            }
                            finally
                            {
                                if (sslStream != null) sslStream.Dispose();
                            }
                        }
                        return;
                    }

                    // 2. CHẾ ĐỘ KILL-SWITCH: Tuyệt đối không để lộ IP thật của máy nếu VPN chưa kết nối thành công
                    if (StrictVpnOnly && string.IsNullOrEmpty(OutboundBindingIp))
                    {
                        byte[] err503 = Encoding.ASCII.GetBytes("HTTP/1.1 503 Service Unavailable\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nVPN chua ket noi. Kill-Switch da kich hoat de bao ve IP that cua ban.\r\n");
                        await clientStream.WriteAsync(err503, 0, err503.Length, token);
                        return;
                    }

                    if (method == "CONNECT")
                    {
                        // HTTPS CONNECT method (Chrome proxy HTTPS)
                        string[] hostPort = target.Split(':');
                        string host = hostPort[0];
                        int port = hostPort.Length > 1 ? int.Parse(hostPort[1]) : 443;

                        using (var targetSocket = CreateOutboundSocket())
                        {
                            await targetSocket.ConnectAsync(host, port);

                            byte[] okResponse = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                            await clientStream.WriteAsync(okResponse, 0, okResponse.Length, token);
                            await clientStream.FlushAsync(token);

                            using (var targetStream = new NetworkStream(targetSocket))
                            {
                                int headerLength = endHeaderIdx + 4;
                                byte[] fullBytes = ms.ToArray();
                                if (fullBytes.Length > headerLength)
                                {
                                    await targetStream.WriteAsync(fullBytes, headerLength, fullBytes.Length - headerLength, token);
                                }

                                var t1 = RelayStreamAsync(clientStream, targetStream, token);
                                var t2 = RelayStreamAsync(targetStream, clientStream, token);
                                await Task.WhenAny(t1, t2);
                            }
                        }
                    }
                    else
                    {
                        // HTTP GET/POST method
                        string host = null;
                        int port = 80;

                        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                        {
                            var uri = new Uri(target);
                            host = uri.Host;
                            port = uri.Port > 0 ? uri.Port : 80;
                        }

                        if (string.IsNullOrEmpty(host))
                        {
                            foreach (var line in lines)
                            {
                                if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string hostVal = line.Substring(5).Trim();
                                    if (hostVal.Contains(":"))
                                    {
                                        var hp = hostVal.Split(':');
                                        host = hp[0];
                                        int p;
                                        if (int.TryParse(hp[1], out p)) port = p;
                                    }
                                    else
                                    {
                                        host = hostVal;
                                    }
                                    break;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(host)) return;

                        using (var targetSocket = CreateOutboundSocket())
                        {
                            await targetSocket.ConnectAsync(host, port);
                            using (var targetStream = new NetworkStream(targetSocket))
                            {
                                byte[] fullBytes = ms.ToArray();
                                await targetStream.WriteAsync(fullBytes, 0, fullBytes.Length, token);
                                // Chờ đọc toàn bộ phản hồi từ server gửi về cho client
                                await RelayStreamAsync(targetStream, clientStream, token);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static async Task RelayStreamAsync(Stream source, Stream destination, CancellationToken token)
        {
            try
            {
                byte[] buf = new byte[8192];
                int read;
                while ((read = await source.ReadAsync(buf, 0, buf.Length, token)) > 0)
                {
                    await destination.WriteAsync(buf, 0, read, token);
                    await destination.FlushAsync(token);
                }
            }
            catch { }
        }

        private Socket CreateOutboundSocket()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;

            // Nếu đã phát hiện IP của Adapter OpenVPN tương ứng, ghim socket ra đúng adapter này
            if (!string.IsNullOrEmpty(OutboundBindingIp))
            {
                IPAddress bindAddr;
                if (IPAddress.TryParse(OutboundBindingIp, out bindAddr))
                {
                    try
                    {
                        socket.Bind(new IPEndPoint(bindAddr, 0));
                    }
                    catch { }
                }
            }

            return socket;
        }
    }
}
