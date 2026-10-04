using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
        public async Task<int> DownloadNordVpnConfigsAsync(string targetDir, int limit = 20)
        {
            if (string.IsNullOrEmpty(targetDir)) return 0;
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            Log("🌐 Đang truy vấn danh sách máy chủ tối ưu từ NordVPN API...");
            int downloaded = 0;
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
                    string apiUrl = string.Format("https://api.nordvpn.com/v1/servers/recommendations?limit={0}", limit);
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

            string detectedTunnelIp = null;

            if (hasOpenVpn && !string.IsNullOrEmpty(item.OvpnPath) && File.Exists(item.OvpnPath))
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
                // Chế độ mô phỏng / Direct Proxy Bridge (Giúp test mượt mà ngay cả khi chưa nạp OpenVPN)
                Log(string.Format("Cổng {0}: Chạy chế độ Local HTTP Proxy Bridge độc lập...", port));
                item.StatusText = "Bridge HTTP Sẵn Sàng";
            }

            // Khởi động Local HTTP Proxy Server trên 127.0.0.1:port
            try
            {
                StartLocalProxyListener(item.Port, detectedTunnelIp);
                item.LocalTunnelIp = detectedTunnelIp;
                item.Status = HmaTunnelStatus.Connected;
                item.StatusText = !string.IsNullOrEmpty(detectedTunnelIp) ? "Đang chạy (VPN)" : "Đang chạy (Local)";
                NotifyStatusChanged(item);

                Log(string.Format("✅ Cổng {0} đã mở thành công! (127.0.0.1:{0})", port));

                // Bắt đầu kiểm tra IP Public thực tế
                var taskCheck = Task.Run(() => CheckPortPublicIpAsync(item));
                return true;
            }
            catch (Exception ex)
            {
                item.Status = HmaTunnelStatus.Error;
                item.StatusText = "Lỗi cổng " + port;
                item.LastError = ex.Message;
                NotifyStatusChanged(item);
                Log(string.Format("❌ Lỗi mở cổng {0}: {1}", port, ex.Message));
                return false;
            }
        }

        private async Task<string> LaunchOpenVpnTunnelAsync(HmaProxyPortItem item, string openVpnExe)
        {
            EnsureDirectory();
            string authFile = Path.Combine(TempDir, string.Format("auth_{0}.txt", item.Port));
            File.WriteAllText(authFile, string.Format("{0}\r\n{1}\r\n", Config.Username, Config.Password));

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
            newLines.Add("windows-driver wintun");
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

        private void StartLocalProxyListener(int port, string outboundBindingIp)
        {
            lock (_lock)
            {
                if (_runningProxies.ContainsKey(port))
                {
                    _runningProxies[port].Stop();
                    _runningProxies.Remove(port);
                }

                var server = new LocalHttpProxyServer(port, outboundBindingIp);
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
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("http://ip-api.com/json");
                request.Proxy = new WebProxy("127.0.0.1", item.Port);
                request.Timeout = 6000;
                request.ReadWriteTimeout = 6000;

                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string json = await reader.ReadToEndAsync();
                    sw.Stop();
                    var obj = JObject.Parse(json);

                    item.PublicIp = (string)obj["query"] ?? "---";
                    item.Country = (string)obj["country"] ?? "---";
                    item.City = (string)obj["city"] ?? "---";
                    item.Isp = (string)obj["isp"] ?? "---";
                    item.PingMs = sw.ElapsedMilliseconds;
                    item.StatusText = "🟢 LIVE";

                    NotifyStatusChanged(item);
                    Log(string.Format("Cổng {0}: Public IP: {1} ({2}, {3}) - {4}ms", item.Port, item.PublicIp, item.Country, item.City, item.PingMs));
                    ipFound = true;
                }
            }
            catch (Exception ex)
            {
                item.LastError = ex.Message;
            }

            if (!ipFound)
            {
                // Fallback ipify
                try
                {
                    var req2 = (HttpWebRequest)WebRequest.Create("http://api.ipify.org");
                    req2.Proxy = new WebProxy("127.0.0.1", item.Port);
                    req2.Timeout = 5000;
                    using (var resp2 = (HttpWebResponse)await req2.GetResponseAsync())
                    using (var s2 = resp2.GetResponseStream())
                    using (var r2 = new StreamReader(s2))
                    {
                        item.PublicIp = (await r2.ReadToEndAsync()).Trim();
                        item.PingMs = sw.ElapsedMilliseconds;
                        item.Country = "Quốc tế";
                        item.StatusText = "🟢 LIVE";
                        NotifyStatusChanged(item);
                    }
                }
                catch (Exception ex2)
                {
                    item.StatusText = "Chưa nhận IP (" + ex2.Message + ")";
                    NotifyStatusChanged(item);
                }
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
                    var ms = new MemoryStream();
                    byte[] tempBuf = new byte[4096];
                    string headerStr = string.Empty;
                    int endHeaderIdx = -1;

                    // Đọc đầy đủ header HTTP (kết thúc bằng \r\n\r\n)
                    while (true)
                    {
                        int read = await clientStream.ReadAsync(tempBuf, 0, tempBuf.Length, token);
                        if (read <= 0) break;
                        ms.Write(tempBuf, 0, read);
                        headerStr = Encoding.ASCII.GetString(ms.ToArray());
                        endHeaderIdx = headerStr.IndexOf("\r\n\r\n");
                        if (endHeaderIdx >= 0) break;
                        if (ms.Length > 65536) break;
                    }

                    if (string.IsNullOrEmpty(headerStr)) return;

                    string[] lines = headerStr.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;

                    string requestLine = lines[0];
                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    string method = parts[0].ToUpper();
                    string target = parts[1];

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
