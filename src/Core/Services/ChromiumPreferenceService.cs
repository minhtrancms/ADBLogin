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

                // 2.1 Tự động vô hiệu hóa các Extension Proxy / VPN xung đột (Proxy Helper, SwitchyOmega, 1click VPN, v.v.)
                // để Chromium tuân thủ 100% proxy được gán từ ADBLogin mà không bị tiện ích chặn/làm mất mạng/lộ IP
                DisableConflictingProxyExtensions(prefJson, proxy);

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
                if (proxy.Protocol == ProxyProtocol.Http || proxy.Protocol == ProxyProtocol.Https)
                {
                    // Chuẩn fixed_servers của Chromium: không tiền tố để áp dụng cho cả HTTP và HTTPS
                    proxyToken["server"] = string.Format("{0}:{1}", proxy.Host, proxy.Port);
                }
                else
                {
                    proxyToken["server"] = string.Format("{0}://{1}:{2}", protocolPrefix, proxy.Host, proxy.Port);
                }
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
                string proto = proxy.Protocol.ToString().ToLower();
                glProxy["mode"] = proto;
                glProxy["type"] = proto;
                glProxy["host"] = proxy.Host;
                glProxy["port"] = proxy.Port;
                glProxy["username"] = proxy.Username ?? "";
                glProxy["password"] = proxy.Password ?? "";
            }
        }

        private void DisableConflictingProxyExtensions(JObject root, ProxySettings proxy)
        {
            if (proxy == null || !proxy.IsEnabled) return;
            if (root["extensions"] == null || root["extensions"]["settings"] == null) return;

            var settings = root["extensions"]["settings"] as JObject;
            if (settings == null) return;

            foreach (var prop in settings.Properties())
            {
                var extObj = prop.Value as JObject;
                if (extObj == null) continue;

                bool isProxyExt = false;
                string id = prop.Name;
                if (id == "mnloefcpaepkpmhaoipjkpikbnkmbnic" || // Proxy Helper
                    id == "fcfhplploccackoneaefokcmbjfbkenj" || // Free VPN for Chrome - 1click VPN
                    id == "padekgcemlokbadohgkifijomclgjgif" || // Proxy SwitchyOmega
                    id == "gcknhkkoolaabfmlnjonogaaifnjlfnp" || // FoxyProxy
                    id == "cahedbgfiagepiohgodabbiplkocdpac")   // SmartProxy
                {
                    isProxyExt = true;
                }

                if (!isProxyExt)
                {
                    var actPerms = extObj["active_permissions"] as JObject;
                    if (actPerms != null && actPerms["api"] != null)
                    {
                        string apiStr = actPerms["api"].ToString();
                        if (apiStr.Contains("proxy")) isProxyExt = true;
                    }
                }

                if (!isProxyExt)
                {
                    var man = extObj["manifest"] as JObject;
                    if (man != null && man["permissions"] != null)
                    {
                        string permStr = man["permissions"].ToString();
                        if (permStr.Contains("proxy")) isProxyExt = true;
                    }
                }

                if (isProxyExt)
                {
                    extObj["state"] = 0; // Disable extension
                    extObj["disable_reasons"] = new JArray(1); // 1 = DISABLE_USER_ACTION
                    if (extObj["preferences"] != null)
                    {
                        extObj.Remove("preferences");
                    }
                }
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
