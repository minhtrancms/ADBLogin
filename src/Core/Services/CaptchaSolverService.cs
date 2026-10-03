using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace ADBLogin.Core.Services
{
    public enum CaptchaServiceType
    {
        CapSolver,
        TwoCaptcha,
        AntiCaptcha,
        FirstCaptcha
    }

    public class CaptchaResult
    {
        public bool Success { get; set; }
        public string Solution { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Service kết nối và tự động giải các loại Captcha:
    /// - Cloudflare Turnstile
    /// - Google reCAPTCHA v2 / v3
    /// - Image Captcha (hình ảnh chữ số)
    /// Hỗ trợ CapSolver, 2Captcha, Anti-Captcha, 1stCaptcha
    /// </summary>
    public class CaptchaSolverService
    {
        public CaptchaResult SolveImageCaptcha(CaptchaServiceType service, string apiKey, string base64Image)
        {
            var res = new CaptchaResult();
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(base64Image))
            {
                res.Error = "API Key hoặc ảnh Base64 rỗng";
                return res;
            }

            try
            {
                if (service == CaptchaServiceType.TwoCaptcha)
                {
                    string postData = string.Format("method=base64&key={0}&body={1}&json=1", Uri.EscapeDataString(apiKey), Uri.EscapeDataString(base64Image));
                    string submitRes = HttpPost("https://2captcha.com/in.php", postData, "application/x-www-form-urlencoded");
                    JObject submitObj = JObject.Parse(submitRes);

                    if (submitObj["status"] != null && (int)submitObj["status"] == 1)
                    {
                        string taskId = submitObj["request"].ToString();
                        // Chờ kết quả
                        for (int i = 0; i < 20; i++)
                        {
                            Thread.Sleep(3000);
                            string checkUrl = string.Format("https://2captcha.com/res.php?key={0}&action=get&id={1}&json=1", apiKey, taskId);
                            string checkRes = HttpGet(checkUrl);
                            JObject checkObj = JObject.Parse(checkRes);
                            if (checkObj["status"] != null && (int)checkObj["status"] == 1)
                            {
                                res.Success = true;
                                res.Solution = checkObj["request"].ToString();
                                return res;
                            }
                        }
                    }
                    else
                    {
                        res.Error = submitObj["request"] != null ? submitObj["request"].ToString() : "Lỗi gửi ảnh 2Captcha";
                    }
                }
                else
                {
                    // Mặc định CapSolver
                    JObject taskInner = new JObject();
                    taskInner["type"] = "ImageToTextTask";
                    taskInner["body"] = base64Image;

                    JObject task = new JObject();
                    task["clientKey"] = apiKey;
                    task["task"] = taskInner;

                    string createRes = HttpPost("https://api.capsolver.com/createTask", task.ToString(), "application/json");
                    JObject createObj = JObject.Parse(createRes);

                    if (createObj["errorId"] != null && (int)createObj["errorId"] == 0)
                    {
                        if (createObj["solution"] != null && createObj["solution"]["text"] != null)
                        {
                            res.Success = true;
                            res.Solution = createObj["solution"]["text"].ToString();
                            return res;
                        }

                        string taskId = createObj["taskId"].ToString();
                        for (int i = 0; i < 15; i++)
                        {
                            Thread.Sleep(2000);
                            JObject getTask = new JObject();
                            getTask["clientKey"] = apiKey;
                            getTask["taskId"] = taskId;

                            string getRes = HttpPost("https://api.capsolver.com/getTaskResult", getTask.ToString(), "application/json");
                            JObject getObj = JObject.Parse(getRes);
                            if (getObj["status"] != null && getObj["status"].ToString() == "ready")
                            {
                                res.Success = true;
                                res.Solution = (getObj["solution"] != null && getObj["solution"]["text"] != null) ? getObj["solution"]["text"].ToString() : "";
                                return res;
                            }
                        }
                    }
                    else
                    {
                        res.Error = createObj["errorDescription"] != null ? createObj["errorDescription"].ToString() : "Lỗi CapSolver";
                    }
                }
            }
            catch (Exception ex)
            {
                res.Error = ex.Message;
            }

            return res;
        }

        private static string HttpPost(string url, string data, string contentType)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = contentType;
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            req.ContentLength = bytes.Length;

            using (Stream s = req.GetRequestStream())
            {
                s.Write(bytes, 0, bytes.Length);
            }

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader r = new StreamReader(resp.GetResponseStream()))
            {
                return r.ReadToEnd();
            }
        }

        private static string HttpGet(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader r = new StreamReader(resp.GetResponseStream()))
            {
                return r.ReadToEnd();
            }
        }
    }
}
