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

        public IWebDriver LaunchBrowser(UserProfile profile, string browserBinaryPath = null, int windowIndex = -1, int rows = 2, int columns = 4, bool isMobileMode = false)
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

            // 2. Cap nhat Proxy neu duoc thiet lap
            if (!string.IsNullOrEmpty(profile.Proxy))
            {
                ProxySettings proxy = ProxySettings.Parse(profile.Proxy);
                _prefService.UpdatePreferences(profileDir, proxy, profile.UserAgent);
            }

            // 3. Cau hinh ChromeOptions
            var options = new ChromeOptions();

            string globalSetting = LocalConfigManager.Instance.CurrentConfig != null ? LocalConfigManager.Instance.CurrentConfig.SelectedBrowserVersion : "144";
            string resolvedBrowser = BrowserVersionService.ResolveBrowserBinary(browserBinaryPath, profile.BrowserVersion, globalSetting);
            if (!string.IsNullOrEmpty(resolvedBrowser) && File.Exists(resolvedBrowser))
            {
                options.BinaryLocation = resolvedBrowser;
            }

            // Gan thu muc Profile
            options.AddArgument(string.Format("--user-data-dir={0}", profileDir));

            // Cau hinh Proxy qua CLI neu co
            if (!string.IsNullOrEmpty(profile.Proxy))
            {
                ProxySettings proxy = ProxySettings.Parse(profile.Proxy);
                if (proxy.IsEnabled)
                {
                    options.AddArgument(string.Format("--proxy-server={0}://{1}:{2}", proxy.Protocol.ToString().ToLower(), proxy.Host, proxy.Port));
                }
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

                // Giả lập User-Agent Smartphone hiện đại
                string mobileUA = !string.IsNullOrEmpty(profile.UserAgent) && profile.UserAgent.Contains("Mobile")
                    ? profile.UserAgent
                    : "Mozilla/5.0 (Linux; Android 13; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";
                options.AddArgument(string.Format("--user-agent={0}", mobileUA));

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
                if (!string.IsNullOrEmpty(profile.UserAgent))
                {
                    options.AddArgument(string.Format("--user-agent={0}", profile.UserAgent));
                }

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

            // Dang ky phien trinh duyet vao SessionManager de theo doi va dong khi can
            BrowserSessionManager.Instance.RegisterSession(profile.ProfileId, driver);

            return driver;
        }
    }
}
