using System;
using System.IO;
using ADBLogin.Core.Models;
using Newtonsoft.Json.Linq;

namespace ADBLogin.Core.Services
{
    /// <summary>
    /// Quản lý việc đọc, ghi và thiết lập tệp Preferences của Chromium/Orbita Profile
    /// </summary>
    public class ChromiumPreferenceService
    {
        /// <summary>
        /// Cập nhật cấu hình Proxy, Vân tay trình duyệt và Tên Profile vào tệp Preferences và Local State của Profile
        /// </summary>
        /// <param name="profileDirectory">Thư mục User Data của profile</param>
        /// <param name="proxy">Thông tin Proxy cần áp dụng</param>
        /// <param name="userAgent">Chuỗi User-Agent tùy biến (nếu có)</param>
        /// <param name="profileName">Tên hiển thị của Profile (đồng bộ lên thanh tiêu đề/Orbita)</param>
        public bool UpdatePreferences(string profileDirectory, ProxySettings proxy, string userAgent = null, string profileName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(profileDirectory) || !Directory.Exists(profileDirectory))
                {
                    Directory.CreateDirectory(profileDirectory);
                }

                string defaultDir = Path.Combine(profileDirectory, "Default");
                if (!Directory.Exists(defaultDir))
                {
                    Directory.CreateDirectory(defaultDir);
                }

                string prefFilePath = Path.Combine(defaultDir, "Preferences");
                JObject prefJson = new JObject();

                if (File.Exists(prefFilePath))
                {
                    string existingContent = File.ReadAllText(prefFilePath);
                    if (!string.IsNullOrWhiteSpace(existingContent))
                    {
                        prefJson = JObject.Parse(existingContent);
                    }
                }

                // 1. Cấu hình Proxy chuẩn Chromium
                ApplyChromiumProxy(prefJson, proxy);

                // 2. Cấu hình Proxy & Metadata cho nhân trình duyệt Antidetect (nếu có trường gologin)
                ApplyAntidetectProxy(prefJson, proxy);

                // 3. Cấu hình User-Agent nếu được truyền vào
                if (!string.IsNullOrEmpty(userAgent))
                {
                    ApplyUserAgent(prefJson, userAgent);
                }

                // 4. Đồng bộ tên Profile vào gologin và profile để hiển thị chuẩn trên thanh công cụ / Orbita
                if (!string.IsNullOrEmpty(profileName))
                {
                    ApplyProfileName(prefJson, profileName);
                }

                // Lưu lại tệp Preferences
                File.WriteAllText(prefFilePath, prefJson.ToString(Newtonsoft.Json.Formatting.Indented));

                // 5. Đồng bộ tên Profile vào Local State và ProfileInfo.txt
                if (!string.IsNullOrEmpty(profileName))
                {
                    UpdateLocalState(profileDirectory, profileName);
                    UpdateProfileInfoTxt(profileDirectory, profileName);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ApplyChromiumProxy(JObject root, ProxySettings proxy)
        {
            if (root["proxy"] == null)
            {
                root["proxy"] = new JObject();
            }

            var proxyToken = (JObject)root["proxy"];

            if (proxy == null || !proxy.IsEnabled)
            {
                proxyToken["mode"] = "direct";
                proxyToken.Remove("server");
            }
            else
            {
                proxyToken["mode"] = "fixed_servers";
                string protocolPrefix = proxy.Protocol.ToString().ToLower();
                proxyToken["server"] = string.Format("{0}://{1}:{2}", protocolPrefix, proxy.Host, proxy.Port);
            }
        }

        private void ApplyAntidetectProxy(JObject root, ProxySettings proxy)
        {
            if (root["gologin"] == null)
            {
                root["gologin"] = new JObject();
            }

            var glToken = (JObject)root["gologin"];
            if (glToken["proxy"] == null)
            {
                glToken["proxy"] = new JObject();
            }

            var glProxy = (JObject)glToken["proxy"];

            if (proxy == null || !proxy.IsEnabled)
            {
                glProxy["mode"] = "none";
                glProxy["host"] = "";
                glProxy["port"] = 0;
                glProxy["username"] = "";
                glProxy["password"] = "";
            }
            else
            {
                glProxy["mode"] = "fixed_servers";
                glProxy["type"] = proxy.Protocol.ToString().ToLower();
                glProxy["host"] = proxy.Host;
                glProxy["port"] = proxy.Port;
                glProxy["username"] = proxy.Username ?? "";
                glProxy["password"] = proxy.Password ?? "";
            }
        }

        private void ApplyUserAgent(JObject root, string userAgent)
        {
            if (root["gologin"] == null)
            {
                root["gologin"] = new JObject();
            }

            root["gologin"]["userAgent"] = userAgent;
        }

        private void ApplyProfileName(JObject root, string profileName)
        {
            if (string.IsNullOrEmpty(profileName)) return;

            // 1. Chuẩn Chromium Profile
            if (root["profile"] == null)
            {
                root["profile"] = new JObject();
            }
            var profToken = (JObject)root["profile"];
            profToken["name"] = profileName;

            // 2. Gologin / Orbita Metadata
            if (root["gologin"] == null)
            {
                root["gologin"] = new JObject();
            }
            var glToken = (JObject)root["gologin"];
            glToken["name"] = profileName;
            glToken["profileName"] = profileName;
        }

        private void UpdateLocalState(string profileDirectory, string profileName)
        {
            if (string.IsNullOrEmpty(profileDirectory) || string.IsNullOrEmpty(profileName)) return;

            string localStatePath = Path.Combine(profileDirectory, "Local State");
            if (!File.Exists(localStatePath)) return;

            try
            {
                string content = File.ReadAllText(localStatePath);
                if (string.IsNullOrWhiteSpace(content)) return;

                var localState = JObject.Parse(content);
                var prof = localState["profile"] as JObject;
                if (prof != null)
                {
                    var cache = prof["info_cache"] as JObject;
                    if (cache != null)
                    {
                        foreach (var prop in cache.Properties())
                        {
                            var d = prop.Value as JObject;
                            if (d != null)
                            {
                                d["name"] = profileName;
                                d["user_name"] = profileName;
                            }
                        }
                        File.WriteAllText(localStatePath, localState.ToString(Newtonsoft.Json.Formatting.Indented));
                    }
                }
            }
            catch { }
        }

        private void UpdateProfileInfoTxt(string profileDirectory, string profileName)
        {
            if (string.IsNullOrEmpty(profileDirectory) || string.IsNullOrEmpty(profileName)) return;

            string infoPath = Path.Combine(profileDirectory, "ProfileInfo.txt");
            if (!File.Exists(infoPath)) return;

            try
            {
                string content = File.ReadAllText(infoPath);
                string[] parts = content.Split('|');
                if (parts.Length >= 2)
                {
                    parts[0] = profileName;
                    File.WriteAllText(infoPath, string.Join("|", parts));
                }
            }
            catch { }
        }
    }
}
