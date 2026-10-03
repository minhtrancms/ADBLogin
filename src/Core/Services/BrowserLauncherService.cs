using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using ADBLogin.Core.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace ADBLogin.Core.Services
{
    public class BrowserLauncherService
    {
        private readonly ChromiumPreferenceService _prefService = new ChromiumPreferenceService();
        private readonly WindowManagerService _windowManager = new WindowManagerService();

        // Windows API dung de ep kich thuoc cua so nho hon muc toi thieu cua Chrome
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public IWebDriver LaunchBrowser(UserProfile profile, string browserBinaryPath = null, int windowIndex = -1, int rows = 2, int columns = 4, bool isMobileMode = false, int debuggingPort = 0)
        {
            if (profile == null) throw new ArgumentNullException("profile");

            // 1. Xac dinh thu muc profile (Uu tien thu muc goc neu co)
            string profileDir = profile.BrowserPath;
            if (string.IsNullOrEmpty(profileDir) || !Directory.Exists(profileDir))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                profileDir = Path.Combine(baseDir, "Files", "Profiles", profile.ProfileId);
                if (!Directory.Exists(profileDir))
                {
                    Directory.CreateDirectory(profileDir);
                }
            }

            // 2. Tự động chuẩn hóa User-Agent tương thích phiên bản Orbita (Tránh lỗi Chrome cũ trên Google/Gmail)
            string globalSetting = LocalConfigManager.Instance.CurrentConfig != null ? LocalConfigManager.Instance.CurrentConfig.SelectedBrowserVersion : "144";
            string activeVer = ExtractNumericVersion(!string.IsNullOrEmpty(profile.BrowserVersion) ? profile.BrowserVersion : globalSetting);

            string effectiveUA;
            if (isMobileMode)
            {
                effectiveUA = !string.IsNullOrEmpty(profile.UserAgent) && profile.UserAgent.Contains("Mobile") && !IsObsoleteUA(profile.UserAgent)
                    ? profile.UserAgent
                    : string.Format("Mozilla/5.0 (Linux; Android 14; SM-S928B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{0}.0.0.0 Mobile Safari/537.36", activeVer);
            }
            else
            {
                effectiveUA = !string.IsNullOrEmpty(profile.UserAgent) && !IsObsoleteUA(profile.UserAgent)
                    ? profile.UserAgent
                    : string.Format("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{0}.0.0.0 Safari/537.36", activeVer);
            }

            ProxySettings proxy = !string.IsNullOrEmpty(profile.Proxy) ? ProxySettings.Parse(profile.Proxy) : null;
            _prefService.UpdatePreferences(profileDir, proxy, effectiveUA);

            // 3. Cau hinh ChromeOptions
            var options = new ChromeOptions();

            string resolvedBrowser = BrowserVersionService.ResolveBrowserBinary(browserBinaryPath, profile.BrowserVersion, globalSetting);
            if (!string.IsNullOrEmpty(resolvedBrowser) && File.Exists(resolvedBrowser))
            {
                options.BinaryLocation = resolvedBrowser;
            }

            // Gan thu muc Profile
            options.AddArgument(string.Format("--user-data-dir={0}", profileDir));

            // Mo Remote Debugging Port (CDP) cho phep automation ben ngoai (Python, Puppeteer, Playwright)
            if (debuggingPort <= 0)
            {
                debuggingPort = FindFreeTcpPort();
            }
            options.AddArgument(string.Format("--remote-debugging-port={0}", debuggingPort));

            // Cau hinh Proxy qua CLI neu co
            if (proxy != null && proxy.IsEnabled)
            {
                options.AddArgument(string.Format("--proxy-server={0}://{1}:{2}", proxy.Protocol.ToString().ToLower(), proxy.Host, proxy.Port));
            }

            // ================= KỸ THUẬT SYSTEM SCALE FACTOR (CHUẨN PHONE FARM DÀI) =================
            int mobileWidth = 300;
            int mobileHeight = 720;
            int mobilePosX = 0;
            int mobilePosY = 0;

            if (isMobileMode)
            {
                // 1. Giảm Scale toàn bộ giao diện Chromium xuống 70%
                options.AddArgument("--force-device-scale-factor=0.7");
                options.AddArgument("--enable-features=OverlayScrollbar");

                // Lấy thông số số cột và số hàng do người dùng đặt trên giao diện (ví dụ 6 cột, 1 hàng)
                int activeCols = columns > 0 ? columns : 6;
                int activeRows = rows > 0 ? rows : 1;

                var screenArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;

                // Tự động tính toán chiều rộng của mỗi máy để vừa đủ activeCols máy trên màn hình
                int autoWidth = (screenArea.Width / activeCols) - 4;
                mobileWidth = Math.Max(220, Math.Min(350, autoWidth));
                mobileHeight = Math.Min(720, screenArea.Height - 40);

                options.AddArgument(string.Format("--window-size={0},{1}", mobileWidth, mobileHeight));
                options.AddArgument(string.Format("--user-agent={0}", effectiveUA));

                // Định vị cửa sổ theo đúng cột và hàng
                if (windowIndex >= 0)
                {
                    int colIndex = windowIndex % activeCols;
                    int rowIndex = windowIndex / activeCols;

                    int colSlotWidth = screenArea.Width / activeCols;
                    mobilePosX = screenArea.Left + (colIndex * colSlotWidth);
                    mobilePosY = screenArea.Top + (rowIndex * (mobileHeight + 10));

                    options.AddArgument(string.Format("--window-position={0},{1}", mobilePosX, mobilePosY));
                }
            }
            else
            {
                // Che do Desktop tieu chuan
                options.AddArgument(string.Format("--user-agent={0}", effectiveUA));

                if (windowIndex >= 0)
                {
                    var bounds = _windowManager.CalculateWindowBounds(windowIndex, rows, columns);
                    options.AddArgument(string.Format("--window-position={0},{1}", bounds.X, bounds.Y));
                    options.AddArgument(string.Format("--window-size={0},{1}", bounds.Width, bounds.Height));
                }
            }

            options.AddArgument("--no-first-run");
            options.AddArgument("--no-default-browser-check");
            options.AddArgument("--disable-popup-blocking");
            options.AddArgument("--disable-background-networking");
            options.AddArgument("--disable-default-apps");

            // Nap extension neu co
            string extDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chrome-extensions");
            if (Directory.Exists(extDir))
            {
                var crxFiles = Directory.GetFiles(extDir, "*.crx");
                foreach (var crx in crxFiles)
                {
                    options.AddExtension(crx);
                }
            }

            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;

            var driver = new ChromeDriver(service, options);

            // Dam bao kich thuoc va vi tri cua so duoc ep chuan xac
            if (isMobileMode)
            {
                try
                {
                    driver.Manage().Window.Size = new Size(mobileWidth, mobileHeight);
                    if (windowIndex >= 0)
                    {
                        driver.Manage().Window.Position = new Point(mobilePosX, mobilePosY);
                    }
                }
                catch { }
            }
            else if (windowIndex >= 0)
            {
                _windowManager.ApplyWindowBounds(driver, windowIndex, rows, columns);
            }

            // Dang ky phien trinh duyet vao SessionManager de theo doi va dong khi can (kem Debugging Port)
            BrowserSessionManager.Instance.RegisterSession(profile.ProfileId, driver, debuggingPort);

            return driver;
        }

        private static bool IsObsoleteUA(string ua)
        {
            if (string.IsNullOrWhiteSpace(ua)) return true;
            var match = System.Text.RegularExpressions.Regex.Match(ua, @"Chrome/(\d+)");
            if (match.Success)
            {
                int ver;
                if (int.TryParse(match.Groups[1].Value, out ver))
                {
                    return ver < 130;
                }
            }
            return false;
        }

        private static string ExtractNumericVersion(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "144";
            var match = System.Text.RegularExpressions.Regex.Match(input, @"\d+");
            return match.Success ? match.Value : "144";
        }

        public static int FindFreeTcpPort()
        {
            try
            {
                var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                l.Start();
                int port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
            catch
            {
                return new Random().Next(15000, 30000);
            }
        }
    }
}
