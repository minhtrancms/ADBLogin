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
        /// Cập nhật cấu hình Proxy và Vân tay trình duyệt vào tệp Preferences của Profile
        /// </summary>
        /// <param name="profileDirectory">Thư mục User Data của profile</param>
        /// <param name="proxy">Thông tin Proxy cần áp dụng</param>
        /// <param name="userAgent">Chuỗi User-Agent tùy biến (nếu có)</param>
        public bool UpdatePreferences(string profileDirectory, ProxySettings proxy, string userAgent = null)
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

                // Lưu lại tệp Preferences
                File.WriteAllText(prefFilePath, prefJson.ToString(Newtonsoft.Json.Formatting.Indented));
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
    }
}
