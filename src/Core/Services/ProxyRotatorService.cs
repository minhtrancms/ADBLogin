using System;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;

namespace ADBLogin.Core.Services
{
    public enum ProxyRotatorProvider
    {
        TMProxy,
        Tinsoft,
        ShopLike,
        ProxyFB,
        CustomUrl
    }

    public class RotateProxyResult
    {
        public bool Success { get; set; }
        public string Proxy { get; set; }
        public string Message { get; set; }
        public int NextChangeSeconds { get; set; }
    }

    /// <summary>
    /// Service quản lý và tự động xoay IP Proxy qua API (TMProxy, Tinsoft, ShopLike, ProxyFB...)
    /// Hỗ trợ kiểm tra thời gian chờ (cooldown) và tự động cấp IP mới cho từng Profile
    /// </summary>
    public class ProxyRotatorService
    {
        public RotateProxyResult RequestNewProxy(ProxyRotatorProvider provider, string apiKeyOrUrl)
        {
            var result = new RotateProxyResult { Success = false };

            if (string.IsNullOrWhiteSpace(apiKeyOrUrl))
            {
                result.Message = "API Key hoặc URL rỗng";
                return result;
            }

            try
            {
                string requestUrl = "";
                switch (provider)
                {
                    case ProxyRotatorProvider.TMProxy:
                        requestUrl = string.Format("https://tmproxy.com/api/proxy/get-new-proxy?api_key={0}", apiKeyOrUrl.Trim());
                        break;
                    case ProxyRotatorProvider.Tinsoft:
                        requestUrl = string.Format("http://proxy.tinsoftsv.com/api/changeProxy.php?key={0}", apiKeyOrUrl.Trim());
                        break;
                    case ProxyRotatorProvider.ShopLike:
                        requestUrl = string.Format("http://proxy.shoplike.vn/v2/proxy/get-new-proxy?api_key={0}", apiKeyOrUrl.Trim());
                        break;
                    case ProxyRotatorProvider.ProxyFB:
                        requestUrl = string.Format("https://api.proxyfb.com/api/changeProxy.php?key={0}", apiKeyOrUrl.Trim());
                        break;
                    case ProxyRotatorProvider.CustomUrl:
                        requestUrl = apiKeyOrUrl.Trim();
                        break;
                }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(requestUrl);
                req.Timeout = 10000;
                req.UserAgent = "ADBLogin/2.0";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    ParseApiResponse(provider, json, result);
                }
            }
            catch (Exception ex)
            {
                result.Message = "Lỗi kết nối API: " + ex.Message;
            }

            return result;
        }

        private void ParseApiResponse(ProxyRotatorProvider provider, string responseText, RotateProxyResult result)
        {
            try
            {
                JObject obj = JObject.Parse(responseText);

                switch (provider)
                {
                    case ProxyRotatorProvider.TMProxy:
                        int code = obj["code"] != null ? (int)obj["code"] : -1;
                        if (code == 0)
                        {
                            result.Success = true;
                            string proxyIp = null;
                            if (obj["data"] != null)
                            {
                                if (obj["data"]["https"] != null) proxyIp = obj["data"]["https"].ToString();
                                else if (obj["data"]["socks5"] != null) proxyIp = obj["data"]["socks5"].ToString();
                            }
                            result.Proxy = proxyIp;
                            result.NextChangeSeconds = (obj["data"] != null && obj["data"]["next_request"] != null) ? (int)obj["data"]["next_request"] : 0;
                            result.Message = "Đổi IP TMProxy thành công!";
                        }
                        else
                        {
                            result.Message = obj["message"] != null ? obj["message"].ToString() : "Không thể đổi IP TMProxy";
                        }
                        break;

                    case ProxyRotatorProvider.Tinsoft:
                        bool success = obj["success"] != null && (bool)obj["success"];
                        if (success)
                        {
                            result.Success = true;
                            result.Proxy = obj["proxy"] != null ? obj["proxy"].ToString() : "";
                            result.NextChangeSeconds = obj["next_change"] != null ? (int)obj["next_change"] : 0;
                            result.Message = "Đổi IP Tinsoft thành công!";
                        }
                        else
                        {
                            result.Message = obj["description"] != null ? obj["description"].ToString() : "Tinsoft từ chối đổi IP";
                        }
                        break;

                    case ProxyRotatorProvider.ShopLike:
                        string status = obj["status"] != null ? obj["status"].ToString() : "";
                        if (status == "success")
                        {
                            result.Success = true;
                            result.Proxy = (obj["data"] != null && obj["data"]["proxy"] != null) ? obj["data"]["proxy"].ToString() : "";
                            result.Message = "Đổi IP ShopLike thành công!";
                        }
                        else
                        {
                            result.Message = obj["mess"] != null ? obj["mess"].ToString() : "Không thể đổi IP ShopLike";
                        }
                        break;

                    default:
                        // Phân tích định dạng chung
                        string proxy = "";
                        if (obj["proxy"] != null) proxy = obj["proxy"].ToString();
                        else if (obj["data"] != null && obj["data"]["proxy"] != null) proxy = obj["data"]["proxy"].ToString();
                        else proxy = responseText.Trim();

                        result.Success = !string.IsNullOrEmpty(proxy);
                        result.Proxy = proxy;
                        result.Message = result.Success ? "Đổi IP thành công!" : responseText;
                        break;
                }
            }
            catch
            {
                // Nếu phản hồi là plain text dạng ip:port
                if (responseText.Contains(":") && !responseText.Contains("{"))
                {
                    result.Success = true;
                    result.Proxy = responseText.Trim();
                    result.Message = "Đổi IP thành công!";
                }
                else
                {
                    result.Message = "Phản hồi không hợp lệ: " + responseText;
                }
            }
        }
    }
}
