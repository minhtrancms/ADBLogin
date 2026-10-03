using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using ADBLogin.Core.Automation;
using ADBLogin.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ADBLogin.Core.Services
{
    /// <summary>
    /// Máy chủ REST API nội bộ (Local HTTP Server) cho phép các công cụ tự động hóa bên ngoài
    /// (Python, Node.js, Puppeteer, Playwright, Selenium, Go) điều khiển mở/tắt profile và kết nối cổng CDP.
    /// </summary>
    public class LocalApiService
    {
        private static LocalApiService _instance;
        private static readonly object _lock = new object();

        private HttpListener _listener;
        private Thread _serverThread;
        private bool _isRunning;
        private int _port = 5858;

        public static LocalApiService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LocalApiService();
                        }
                    }
                }
                return _instance;
            }
        }

        public int Port { get { return _port; } }
        public bool IsRunning { get { return _isRunning; } }

        private LocalApiService() { }

        public bool Start(int preferredPort = 5858)
        {
            if (_isRunning) return true;

            for (int p = preferredPort; p < preferredPort + 10; p++)
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", p));
                    _listener.Start();
                    _port = p;
                    _isRunning = true;
                    break;
                }
                catch
                {
                    _listener = null;
                }
            }

            if (!_isRunning || _listener == null) return false;

            _serverThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "ADBLogin_LocalApiServer"
            };
            _serverThread.Start();

            return true;
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                    _listener.Close();
                }
            }
            catch { }
        }

        private void ListenLoop()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(state => ProcessRequest((HttpListenerContext)state), context);
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            // Thiết lập CORS cho phép gọi từ web/dashboard
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "Content-Type");
            res.ContentType = "application/json; charset=utf-8";

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            try
            {
                string rawUrl = req.RawUrl ?? "/";
                string path = req.Url.AbsolutePath.ToLowerInvariant();

                // 1. Root / Status
                if (path == "/" || path == "/api/status")
                {
                    SendJson(res, new
                    {
                        code = 0,
                        status = "online",
                        service = "ADBLogin Automation API",
                        port = _port,
                        active_browsers = BrowserSessionManager.Instance.GetRunningProfileIds().Count
                    });
                    return;
                }

                // 2. Danh sách Profiles
                if (path == "/api/profiles")
                {
                    var profiles = AccountManager.Instance.GetAllProfiles();
                    var list = new List<object>();

                    foreach (var p in profiles)
                    {
                        bool running = BrowserSessionManager.Instance.IsRunning(p.ProfileId);
                        int cdpPort = running ? BrowserSessionManager.Instance.GetDebuggingPort(p.ProfileId) : 0;

                        list.Add(new
                        {
                            profile_id = p.ProfileId,
                            name = p.ProfileName,
                            proxy = p.Proxy,
                            browser_version = p.BrowserVersion,
                            is_running = running,
                            cdp_port = cdpPort,
                            cdp_url = cdpPort > 0 ? string.Format("http://127.0.0.1:{0}", cdpPort) : ""
                        });
                    }

                    SendJson(res, new { code = 0, total = list.Count, data = list });
                    return;
                }

                // 3. Khởi chạy Profile: /api/profile/start?id=xyz
                if (path == "/api/profile/start")
                {
                    string id = req.QueryString["id"] ?? req.QueryString["profile_id"] ?? "";
                    if (string.IsNullOrEmpty(id))
                    {
                        SendJson(res, new { code = 400, message = "Thiếu tham số 'id' hoặc 'profile_id'" }, 400);
                        return;
                    }

                    var profile = FindProfile(id);
                    if (profile == null)
                    {
                        SendJson(res, new { code = 404, message = "Không tìm thấy profile: " + id }, 404);
                        return;
                    }

                    // Nếu đã đang chạy, trả về luôn port
                    if (BrowserSessionManager.Instance.IsRunning(profile.ProfileId))
                    {
                        int existingPort = BrowserSessionManager.Instance.GetDebuggingPort(profile.ProfileId);
                        SendJson(res, new
                        {
                            code = 0,
                            message = "Profile đang hoạt động",
                            profile_id = profile.ProfileId,
                            cdp_port = existingPort,
                            cdp_url = string.Format("http://127.0.0.1:{0}", existingPort)
                        });
                        return;
                    }

                    // Khởi chạy trình duyệt
                    var launcher = new BrowserLauncherService();
                    var driver = launcher.LaunchBrowser(profile);
                    int cdp = BrowserSessionManager.Instance.GetDebuggingPort(profile.ProfileId);

                    SendJson(res, new
                    {
                        code = 0,
                        message = "Khởi chạy thành công",
                        profile_id = profile.ProfileId,
                        cdp_port = cdp,
                        cdp_url = string.Format("http://127.0.0.1:{0}", cdp)
                    });
                    return;
                }

                // 4. Dừng Profile: /api/profile/stop?id=xyz
                if (path == "/api/profile/stop")
                {
                    string id = req.QueryString["id"] ?? req.QueryString["profile_id"] ?? "";
                    if (string.IsNullOrEmpty(id))
                    {
                        SendJson(res, new { code = 400, message = "Thiếu tham số 'id'" }, 400);
                        return;
                    }

                    var profile = FindProfile(id);
                    string targetId = profile != null ? profile.ProfileId : id;

                    bool closed = BrowserSessionManager.Instance.CloseSession(targetId);
                    SendJson(res, new { code = 0, success = closed, message = closed ? "Đã đóng profile" : "Profile không chạy hoặc đã đóng trước đó" });
                    return;
                }

                // 5. Kiểm tra nhanh UID Live: /api/fb/check_uid?uid=xyz
                if (path == "/api/fb/check_uid")
                {
                    string uid = req.QueryString["uid"] ?? "";
                    string name;
                    string err;
                    bool isLive = FacebookAutomationService.FastCheckUidLive(uid, out name, out err);

                    SendJson(res, new
                    {
                        code = 0,
                        uid = uid,
                        is_live = isLive,
                        status = isLive ? "LIVE" : "DIE",
                        error = err
                    });
                    return;
                }

                // 6. Tự động đăng nhập Cookie qua API: POST /api/fb/login_cookie
                if (path == "/api/fb/login_cookie" && req.HttpMethod == "POST")
                {
                    string body = ReadBody(req);
                    var json = JObject.Parse(body);
                    string id = (string)json["profile_id"] ?? (string)json["id"];
                    string cookie = (string)json["cookie"];

                    var profile = FindProfile(id);
                    if (profile == null)
                    {
                        SendJson(res, new { code = 404, message = "Không tìm thấy profile" }, 404);
                        return;
                    }

                    var driver = BrowserSessionManager.Instance.GetDriver(profile.ProfileId);
                    if (driver == null)
                    {
                        var launcher = new BrowserLauncherService();
                        driver = launcher.LaunchBrowser(profile);
                    }

                    var fb = new FacebookAutomationService();
                    bool ok = fb.LoginWithCookie(driver, cookie);

                    SendJson(res, new { code = ok ? 0 : 500, success = ok, message = ok ? "Đăng nhập thành công" : "Đăng nhập thất bại" });
                    return;
                }

                SendJson(res, new { code = 404, message = "Không tìm thấy endpoint API: " + path }, 404);
            }
            catch (Exception ex)
            {
                try
                {
                    SendJson(res, new { code = 500, error = ex.Message }, 500);
                }
                catch { }
            }
        }

        private UserProfile FindProfile(string idOrName)
        {
            var profiles = AccountManager.Instance.GetAllProfiles();
            foreach (var p in profiles)
            {
                if (p.ProfileId.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ||
                    p.ProfileName.Equals(idOrName, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            return null;
        }

        private string ReadBody(HttpListenerRequest req)
        {
            using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
            {
                return reader.ReadToEnd();
            }
        }

        private void SendJson(HttpListenerResponse res, object data, int statusCode = 200)
        {
            res.StatusCode = statusCode;
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.OutputStream.Flush();
            res.Close();
        }
    }
}
