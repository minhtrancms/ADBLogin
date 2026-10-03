using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace ADBLogin.Core.Services
{
    public class BrowserVersionInfo
    {
        public string DisplayName { get; set; }
        public string VersionKey { get; set; }
        public string ExecutablePath { get; set; }
        public string ZipPath { get; set; }
        public bool IsInstalled { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public static class BrowserVersionService
    {
        private static readonly string UserProfileDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static readonly string GoLoginBrowserDir = Path.Combine(UserProfileDir, ".gologin", "browser");
        private static readonly string AppBaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string AllBrowsersZipDir = Path.Combine(AppBaseDir, "Gologin", "All-Browsers");

        /// <summary>
        /// Quét toàn bộ các phiên bản Orbita và Chrome có sẵn trên hệ thống và trong kho lưu trữ
        /// </summary>
        public static List<BrowserVersionInfo> GetAvailableBrowsers()
        {
            var list = new List<BrowserVersionInfo>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Quét các phiên bản Orbita đã được cài đặt trong ~/.gologin/browser/
            if (Directory.Exists(GoLoginBrowserDir))
            {
                var dirs = Directory.GetDirectories(GoLoginBrowserDir, "orbita-browser-*");
                // Sắp xếp giảm dần theo phiên bản (144, 143, 142...)
                Array.Sort(dirs);
                Array.Reverse(dirs);

                foreach (var dir in dirs)
                {
                    string dirName = Path.GetFileName(dir); // ví dụ: orbita-browser-144
                    string ver = dirName.Replace("orbita-browser-", "").Trim();
                    string exe = Path.Combine(dir, "chrome.exe");

                    if (File.Exists(exe))
                    {
                        seenKeys.Add(ver);
                        list.Add(new BrowserVersionInfo
                        {
                            DisplayName = string.Format("Orbita {0} (Đã cài đặt)", ver),
                            VersionKey = ver,
                            ExecutablePath = exe,
                            IsInstalled = true
                        });
                    }
                }
            }

            // 2. Quét các gói zip có sẵn trong Gologin/All-Browsers (chưa giải nén)
            if (Directory.Exists(AllBrowsersZipDir))
            {
                var zips = Directory.GetFiles(AllBrowsersZipDir, "orbita-browser-*.zip");
                Array.Sort(zips);
                Array.Reverse(zips);

                foreach (var zip in zips)
                {
                    string fileName = Path.GetFileNameWithoutExtension(zip); // orbita-browser-143
                    string ver = fileName.Replace("orbita-browser-", "").Trim();

                    if (!seenKeys.Contains(ver))
                    {
                        seenKeys.Add(ver);
                        list.Add(new BrowserVersionInfo
                        {
                            DisplayName = string.Format("Orbita {0} (Kho lưu trữ - Tự giải nén)", ver),
                            VersionKey = ver,
                            ZipPath = zip,
                            IsInstalled = false
                        });
                    }
                }
            }

            // Đảm bảo luôn có ít nhất Orbita 144 và 143 trong danh sách
            if (!seenKeys.Contains("144"))
            {
                list.Insert(0, new BrowserVersionInfo
                {
                    DisplayName = "Orbita 144 (Mặc định)",
                    VersionKey = "144",
                    IsInstalled = false
                });
            }

            // 3. Quét Google Chrome chính thức trên máy tính
            string chromePath = GetSystemChromePath();
            if (!string.IsNullOrEmpty(chromePath))
            {
                list.Add(new BrowserVersionInfo
                {
                    DisplayName = "Google Chrome (Hệ thống)",
                    VersionKey = "chrome",
                    ExecutablePath = chromePath,
                    IsInstalled = true
                });
            }

            // 4. Tùy chọn đường dẫn riêng
            list.Add(new BrowserVersionInfo
            {
                DisplayName = "📁 Tùy chọn đường dẫn riêng...",
                VersionKey = "custom",
                IsInstalled = false
            });

            return list;
        }

        /// <summary>
        /// Tìm đường dẫn thực thi của Google Chrome trên Windows
        /// </summary>
        public static string GetSystemChromePath()
        {
            string[] possiblePaths = new string[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path)) return path;
            }

            return null;
        }

        /// <summary>
        /// Phân giải chính xác file chrome.exe theo phiên bản được yêu cầu, tự động giải nén nếu cần
        /// </summary>
        public static string ResolveBrowserBinary(string explicitPath, string profileVersion, string globalSetting)
        {
            // 1. Đường dẫn trực tiếp được cấu hình trong profile
            if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
            {
                return explicitPath;
            }

            // 2. Xác định Version Key mong muốn
            string target = !string.IsNullOrEmpty(profileVersion) ? profileVersion : globalSetting;
            if (string.IsNullOrWhiteSpace(target)) target = "144";

            // Nếu người dùng chọn file thực thi trực tiếp
            if (File.Exists(target))
            {
                return target;
            }

            // Trích xuất số phiên bản (ví dụ "Orbita 143 (Kho...)" -> "143", "chrome" -> "chrome")
            string verKey = ExtractVersionKey(target);

            // Xử lý Google Chrome hệ thống
            if (verKey.Equals("chrome", StringComparison.OrdinalIgnoreCase))
            {
                string chrome = GetSystemChromePath();
                if (!string.IsNullOrEmpty(chrome)) return chrome;
            }

            // Xử lý Orbita Browser
            string targetFolder = Path.Combine(GoLoginBrowserDir, string.Format("orbita-browser-{0}", verKey));
            string targetExe = Path.Combine(targetFolder, "chrome.exe");

            // Nếu đã tồn tại file chrome.exe của phiên bản đó
            if (File.Exists(targetExe))
            {
                return targetExe;
            }

            // Nếu chưa có, kiểm tra xem có file zip trong Gologin/All-Browsers không
            string zipFile = Path.Combine(AllBrowsersZipDir, string.Format("orbita-browser-{0}.zip", verKey));
            if (File.Exists(zipFile))
            {
                try
                {
                    if (!Directory.Exists(GoLoginBrowserDir))
                    {
                        Directory.CreateDirectory(GoLoginBrowserDir);
                    }

                    // Tự động giải nén gói trình duyệt vào thư mục .gologin/browser/
                    ZipFile.ExtractToDirectory(zipFile, GoLoginBrowserDir);

                    if (File.Exists(targetExe))
                    {
                        return targetExe;
                    }
                }
                catch { }
            }

            // 3. Fallback: Nếu phiên bản chỉ định không có, tìm phiên bản Orbita bất kỳ có sẵn
            string fallback144 = Path.Combine(GoLoginBrowserDir, "orbita-browser-144", "chrome.exe");
            if (File.Exists(fallback144)) return fallback144;

            string fallback142 = Path.Combine(GoLoginBrowserDir, "orbita-browser-142", "chrome.exe");
            if (File.Exists(fallback142)) return fallback142;

            if (Directory.Exists(GoLoginBrowserDir))
            {
                var anyExes = Directory.GetFiles(GoLoginBrowserDir, "chrome.exe", SearchOption.AllDirectories);
                if (anyExes.Length > 0) return anyExes[0];
            }

            // Cuối cùng thử Google Chrome hệ thống
            string finalChrome = GetSystemChromePath();
            if (!string.IsNullOrEmpty(finalChrome)) return finalChrome;

            return null;
        }

        private static string ExtractVersionKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "144";
            if (input.IndexOf("chrome", StringComparison.OrdinalIgnoreCase) >= 0) return "chrome";

            // Tìm chuỗi số (ví dụ 144, 143, 142)
            string numbers = "";
            foreach (char c in input)
            {
                if (char.IsDigit(c)) numbers += c;
                else if (numbers.Length >= 2) break;
            }

            return numbers.Length >= 2 ? numbers : "144";
        }
    }
}
