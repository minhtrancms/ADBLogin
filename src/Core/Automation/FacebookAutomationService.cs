using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Leaf.xNet;
using OpenQA.Selenium;

namespace ADBLogin.Core.Automation
{
    public class FacebookAccountInfo
    {
        public string Uid { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string Cookie { get; set; }
        public string Token { get; set; }
    }

    /// <summary>
    /// Service tự động hóa các thao tác trên Facebook:
    /// - Đăng nhập Cookie / User-Pass-2FA
    /// - Nuôi nick tương tác Newfeed, Reels, Like, Thông báo
    /// - Kiểm tra trạng thái Live/Die/Checkpoint
    /// - Seeding bài viết, tham gia nhóm
    /// </summary>
    public class FacebookAutomationService
    {
        private static readonly Random _rnd = new Random();

        #region Helper: Sleep & Human Typing

        public static void Sleep(int minMs, int maxMs = -1)
        {
            int delay = maxMs > minMs ? _rnd.Next(minMs, maxMs) : minMs;
            Thread.Sleep(delay);
        }

        public static void HumanType(IWebElement element, string text)
        {
            if (element == null || string.IsNullOrEmpty(text)) return;
            element.Clear();
            foreach (char c in text)
            {
                element.SendKeys(c.ToString());
                Thread.Sleep(_rnd.Next(25, 75));
            }
        }

        private static void DoLog(Action<string> log, string msg)
        {
            if (log != null)
            {
                log(msg);
            }
        }

        #endregion

        #region Đăng nhập Facebook

        /// <summary>
        /// Đăng nhập Facebook thông qua chuỗi Cookie (c_user=...; xs=...)
        /// </summary>
        public bool LoginWithCookie(IWebDriver driver, string cookieString, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cookieString))
                {
                    DoLog(log, "[-] Chuỗi Cookie rỗng");
                    return false;
                }

                DoLog(log, "[*] Đang điều hướng đến Facebook...");
                driver.Navigate().GoToUrl("https://www.facebook.com/");
                Sleep(2000, 3000);

                // Phân tích và nạp từng cookie vào trình duyệt
                DoLog(log, "[*] Đang nạp Cookie vào phiên duyệt...");
                string[] parts = cookieString.Split(';');
                int injected = 0;

                foreach (var part in parts)
                {
                    string trimmed = part.Trim();
                    int equalPos = trimmed.IndexOf('=');
                    if (equalPos > 0)
                    {
                        string name = trimmed.Substring(0, equalPos).Trim();
                        string val = trimmed.Substring(equalPos + 1).Trim();

                        try
                        {
                            driver.Manage().Cookies.AddCookie(new Cookie(name, val, ".facebook.com", "/", DateTime.Now.AddDays(180)));
                            injected++;
                        }
                        catch
                        {
                            // Dự phòng domain không có dấu chấm
                            try
                            {
                                driver.Manage().Cookies.AddCookie(new Cookie(name, val, "facebook.com", "/", DateTime.Now.AddDays(180)));
                                injected++;
                            }
                            catch { }
                        }
                    }
                }

                DoLog(log, string.Format("[*] Đã nạp {0} cookie. Đang tải lại trang...", injected));
                driver.Navigate().GoToUrl("https://www.facebook.com/");
                Sleep(3000, 5000);

                // Kiểm tra trạng thái đăng nhập
                string uid;
                string status;
                CheckAccountStatus(driver, out uid, out status);

                if (status == "LIVE")
                {
                    DoLog(log, string.Format("[+] Đăng nhập Cookie THÀNH CÔNG! UID: {0}", uid));
                    return true;
                }
                else
                {
                    DoLog(log, string.Format("[-] Đăng nhập thất bại: {0}", status));
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi nạp Cookie: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Đăng nhập bằng Tài khoản | Mật khẩu | Mã bí mật 2FA
        /// Tự động giải mã 2FA TOTP điền vào form xác nhận
        /// </summary>
        public bool LoginWithCredentials(IWebDriver driver, string username, string password, string twoFactorSecret = "", Action<string> log = null)
        {
            try
            {
                DoLog(log, "[*] Đang mở trang đăng nhập Facebook...");
                driver.Navigate().GoToUrl("https://www.facebook.com/login");
                Sleep(2000, 3000);

                // Tìm ô Email / Username
                IWebElement emailInput = null;
                try { emailInput = driver.FindElement(By.Id("email")); } catch { }
                if (emailInput == null)
                {
                    try { emailInput = driver.FindElement(By.Name("email")); } catch { }
                }

                if (emailInput == null)
                {
                    DoLog(log, "[-] Không tìm thấy ô nhập tài khoản!");
                    return false;
                }

                DoLog(log, string.Format("[*] Điền tài khoản: {0}", username));
                HumanType(emailInput, username);
                Sleep(500, 1000);

                // Tìm ô Mật khẩu
                IWebElement passInput = null;
                try { passInput = driver.FindElement(By.Id("pass")); } catch { }
                if (passInput == null)
                {
                    try { passInput = driver.FindElement(By.Name("pass")); } catch { }
                }

                if (passInput == null)
                {
                    DoLog(log, "[-] Không tìm thấy ô nhập mật khẩu!");
                    return false;
                }

                DoLog(log, "[*] Điền mật khẩu...");
                HumanType(passInput, password);
                Sleep(800, 1500);

                // Bấm Đăng nhập
                DoLog(log, "[*] Bấm nút Đăng nhập...");
                IWebElement loginBtn = null;
                try { loginBtn = driver.FindElement(By.Name("login")); } catch { }
                if (loginBtn == null)
                {
                    try { loginBtn = driver.FindElement(By.Id("loginbutton")); } catch { }
                }

                if (loginBtn != null)
                {
                    loginBtn.Click();
                }
                else
                {
                    passInput.SendKeys(Keys.Enter);
                }

                Sleep(4000, 6000);

                // Kiểm tra xem có yêu cầu mã 2FA hay không
                string currentUrl = driver.Url.ToLower();
                if (currentUrl.Contains("checkpoint") || currentUrl.Contains("two_step_verification") || driver.PageSource.Contains("approvals_code"))
                {
                    DoLog(log, "[*] Phát hiện trang yêu cầu mã xác thực 2FA...");
                    if (string.IsNullOrWhiteSpace(twoFactorSecret))
                    {
                        DoLog(log, "[-] Cần mã 2FA nhưng không có Secret Key!");
                        return false;
                    }

                    string otp = TotpGenerator.GenerateCode(twoFactorSecret);
                    DoLog(log, string.Format("[+] Đã sinh mã OTP 2FA: {0}", otp));

                    IWebElement codeInput = null;
                    try { codeInput = driver.FindElement(By.Id("approvals_code")); } catch { }
                    if (codeInput == null)
                    {
                        try { codeInput = driver.FindElement(By.Name("approvals_code")); } catch { }
                    }
                    if (codeInput == null)
                    {
                        try { codeInput = driver.FindElement(By.CssSelector("input[type='text'], input[type='number']")); } catch { }
                    }

                    if (codeInput != null)
                    {
                        HumanType(codeInput, otp);
                        Sleep(500, 1000);

                        IWebElement submitBtn = null;
                        try { submitBtn = driver.FindElement(By.Id("checkpointSubmitButton")); } catch { }
                        if (submitBtn == null)
                        {
                            try { submitBtn = driver.FindElement(By.CssSelector("button[type='submit']")); } catch { }
                        }

                        if (submitBtn != null)
                        {
                            submitBtn.Click();
                        }
                        else
                        {
                            codeInput.SendKeys(Keys.Enter);
                        }

                        Sleep(5000, 8000);

                        // Bấm "Tiếp tục" hoặc "Lưu trình duyệt" nếu có các màn hình tiếp theo
                        for (int step = 0; step < 3; step++)
                        {
                            try
                            {
                                var nextBtn = driver.FindElement(By.Id("checkpointSubmitButton"));
                                if (nextBtn != null && nextBtn.Displayed)
                                {
                                    nextBtn.Click();
                                    Sleep(3000, 4000);
                                }
                            }
                            catch { break; }
                        }
                    }
                }

                string uid;
                string status;
                CheckAccountStatus(driver, out uid, out status);

                if (status == "LIVE")
                {
                    DoLog(log, string.Format("[+] Đăng nhập User/Pass/2FA THÀNH CÔNG! UID: {0}", uid));
                    return true;
                }
                else
                {
                    DoLog(log, string.Format("[-] Đăng nhập không thành công: {0}", status));
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi đăng nhập: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region Kiểm tra trạng thái tài khoản & Trích xuất dữ liệu

        /// <summary>
        /// Kiểm tra trạng thái tài khoản trên trình duyệt đang mở: LIVE, CHECKPOINT, VÔ HIỆU HÓA, CHƯA ĐĂNG NHẬP
        /// </summary>
        public void CheckAccountStatus(IWebDriver driver, out string uid, out string statusMessage)
        {
            uid = string.Empty;
            statusMessage = "UNKNOWN";

            try
            {
                var cookies = driver.Manage().Cookies.AllCookies;
                foreach (var c in cookies)
                {
                    if (c.Name == "c_user")
                    {
                        uid = c.Value;
                        break;
                    }
                }

                string url = driver.Url.ToLower();

                if (url.Contains("/checkpoint/"))
                {
                    statusMessage = "CHECKPOINT (Xác minh danh tính)";
                    return;
                }

                if (url.Contains("disabled") || driver.PageSource.Contains("Tài khoản của bạn đã bị vô hiệu hóa") || driver.PageSource.Contains("Your Account Has Been Disabled"))
                {
                    statusMessage = "VÔ HIỆU HÓA (DIE)";
                    return;
                }

                if (!string.IsNullOrEmpty(uid))
                {
                    statusMessage = "LIVE";
                    return;
                }

                if (url.Contains("login") || driver.PageSource.Contains("login_form") || driver.PageSource.Contains("id=\"email\""))
                {
                    statusMessage = "CHƯA ĐĂNG NHẬP";
                    return;
                }

                statusMessage = "CHƯA XÁC ĐỊNH";
            }
            catch (Exception ex)
            {
                statusMessage = "Lỗi: " + ex.Message;
            }
        }

        /// <summary>
        /// Kiểm tra nhanh UID Facebook còn sống hay chết (Live / Die) qua API ảnh đại diện (Không cần bật trình duyệt)
        /// </summary>
        public static bool FastCheckUidLive(string uid, out string name, out string error)
        {
            name = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(uid))
            {
                error = "UID trống";
                return false;
            }

            try
            {
                using (var req = new Leaf.xNet.HttpRequest())
                {
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";
                    req.AllowAutoRedirect = false;
                    req.ConnectTimeout = 8000;

                    // Graph API avatar redirect: nếu live sẽ trả về 302 Found trỏ tới CDN ảnh
                    var res = req.Get(string.Format("https://graph.facebook.com/{0}/picture?type=normal", uid.Trim()));
                    if (res.StatusCode == Leaf.xNet.HttpStatusCode.Found || res.StatusCode == Leaf.xNet.HttpStatusCode.OK)
                    {
                        return true;
                    }
                    else
                    {
                        error = "UID không tồn tại hoặc đã bị khóa";
                        return false;
                    }
                }
            }
            catch (Leaf.xNet.HttpException)
            {
                error = "Không kết nối được hoặc UID Die";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Trích xuất toàn bộ chuỗi Cookie hiện tại của phiên duyệt
        /// </summary>
        public string ExtractCookies(IWebDriver driver)
        {
            try
            {
                var sb = new StringBuilder();
                var cookies = driver.Manage().Cookies.AllCookies;
                foreach (var c in cookies)
                {
                    sb.Append(string.Format("{0}={1}; ", c.Name, c.Value));
                }
                return sb.ToString().TrimEnd(' ', ';');
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Trích xuất Access Token (EAAB...) nếu có trên phiên duyệt
        /// </summary>
        public string ExtractToken(IWebDriver driver)
        {
            try
            {
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                // Thử tìm token trong các biến toàn cục hoặc script tag
                object res = js.ExecuteScript(@"
                    try {
                        if (window.__accessToken) return window.__accessToken;
                        var scripts = document.getElementsByTagName('script');
                        for (var i = 0; i < scripts.length; i++) {
                            var text = scripts[i].innerText;
                            var m = text.match(/(EAAB\w+)/);
                            if (m) return m[1];
                        }
                    } catch(e) {}
                    return '';
                ");

                return res != null ? res.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion

        #region Nuôi nick & Tương tác tự nhiên (Human-like Farming)

        /// <summary>
        /// Lướt bảng tin (Newsfeed) mô phỏng hành vi người thật: cuộn mượt, dừng đọc ngẫu nhiên, thả like tự nhiên
        /// </summary>
        public void SurfNewsfeed(IWebDriver driver, int durationSeconds, bool autoLike, int maxLikes, Action<string> log, CancellationToken ct)
        {
            try
            {
                DoLog(log, string.Format("[*] Bắt đầu lướt Newfeed trong {0}s (Auto Like: {1})...", durationSeconds, autoLike));
                if (!driver.Url.Contains("facebook.com"))
                {
                    driver.Navigate().GoToUrl("https://www.facebook.com/");
                    Sleep(3000, 4000);
                }

                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                DateTime startTime = DateTime.Now;
                int currentLikes = 0;

                while ((DateTime.Now - startTime).TotalSeconds < durationSeconds)
                {
                    if (ct.IsCancellationRequested) break;

                    // Cuộn trang mượt xuống
                    int scrollAmount = _rnd.Next(350, 750);
                    js.ExecuteScript(string.Format("window.scrollBy({{ top: {0}, behavior: 'smooth' }});", scrollAmount));

                    // Dừng lại đọc bài ngẫu nhiên 3 - 6 giây
                    int readTime = _rnd.Next(3000, 6500);
                    DoLog(log, string.Format("[*] Cuộn xuống +{0}px, dừng đọc {1:0.0}s...", scrollAmount, readTime / 1000.0));
                    Sleep(readTime);

                    // Thả Like ngẫu nhiên (tỷ lệ 30% khi cuộn)
                    if (autoLike && currentLikes < maxLikes && _rnd.Next(100) < 30)
                    {
                        try
                        {
                            // Tìm các nút Thích / Like chưa bấm
                            var likeButtons = driver.FindElements(By.CssSelector("div[aria-label='Thích'], div[aria-label='Like'], div[role='button']"));
                            foreach (var btn in likeButtons)
                            {
                                if (btn.Displayed && (btn.GetAttribute("aria-label") == "Thích" || btn.GetAttribute("aria-label") == "Like"))
                                {
                                    btn.Click();
                                    currentLikes++;
                                    DoLog(log, string.Format("[♥] Đã thả cảm xúc ({0}/{1})", currentLikes, maxLikes));
                                    Sleep(1500, 3000);
                                    break;
                                }
                            }
                        }
                        catch { }
                    }

                    // Thỉnh thoảng cuộn nhẹ lên 1 chút mô phỏng người thật đọc lại bài (tỷ lệ 15%)
                    if (_rnd.Next(100) < 15)
                    {
                        int scrollUp = _rnd.Next(100, 300);
                        js.ExecuteScript(string.Format("window.scrollBy({{ top: -{0}, behavior: 'smooth' }});", scrollUp));
                        Sleep(1500, 2500);
                    }
                }

                DoLog(log, string.Format("[+] Hoàn thành lướt Newfeed! Tổng số like: {0}", currentLikes));
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi lướt Newfeed: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Xem video Reels / Watch: dừng xem 10 - 25 giây rồi chuyển sang video tiếp theo
        /// </summary>
        public void WatchReels(IWebDriver driver, int reelCount, Action<string> log, CancellationToken ct)
        {
            try
            {
                DoLog(log, string.Format("[*] Bắt đầu xem {0} video Reels...", reelCount));
                driver.Navigate().GoToUrl("https://www.facebook.com/reel/");
                Sleep(3000, 5000);

                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                for (int i = 1; i <= reelCount; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    int watchSeconds = _rnd.Next(10, 25);
                    DoLog(log, string.Format("[▶] Đang xem Reel #{0}/{1} trong {2}s...", i, reelCount, watchSeconds));
                    Sleep(watchSeconds * 1000);

                    // Nhấn phím Mũi tên Xuống hoặc cuộn xuống để chuyển Reel
                    try
                    {
                        var body = driver.FindElement(By.TagName("body"));
                        body.SendKeys(Keys.ArrowDown);
                    }
                    catch
                    {
                        js.ExecuteScript("window.scrollBy({ top: window.innerHeight, behavior: 'smooth' });");
                    }

                    Sleep(1500, 2500);
                }

                DoLog(log, "[+] Hoàn tất xem Reels!");
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi xem Reels: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Mở tab thông báo để tăng trust tương tác cho tài khoản
        /// </summary>
        public void ViewNotifications(IWebDriver driver, Action<string> log)
        {
            try
            {
                DoLog(log, "[*] Mở xem danh sách Thông báo...");
                var notifButtons = driver.FindElements(By.CssSelector("div[aria-label='Thông báo'], div[aria-label='Notifications']"));
                if (notifButtons.Count > 0 && notifButtons[0].Displayed)
                {
                    notifButtons[0].Click();
                    Sleep(3000, 5000);
                    DoLog(log, "[+] Đã mở bảng thông báo");
                }
                else
                {
                    driver.Navigate().GoToUrl("https://www.facebook.com/notifications");
                    Sleep(3000, 4000);
                    DoLog(log, "[+] Đã điều hướng đến trang Thông báo");
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi xem thông báo: {0}", ex.Message));
            }
        }

        #endregion

        #region Tương tác Nhóm & Seeding Bình luận

        /// <summary>
        /// Tham gia nhóm Facebook theo đường dẫn URL
        /// </summary>
        public bool JoinGroup(IWebDriver driver, string groupUrl, Action<string> log)
        {
            try
            {
                DoLog(log, string.Format("[*] Điều hướng tới nhóm: {0}", groupUrl));
                driver.Navigate().GoToUrl(groupUrl);
                Sleep(3000, 5000);

                var buttons = driver.FindElements(By.CssSelector("div[aria-label='Tham gia nhóm'], div[aria-label='Join group'], div[role='button']"));
                foreach (var b in buttons)
                {
                    string label = b.GetAttribute("aria-label");
                    string txt = b.Text;
                    if (label == "Tham gia nhóm" || label == "Join group" || txt.Contains("Tham gia nhóm") || txt.Contains("Join group"))
                    {
                        b.Click();
                        DoLog(log, "[+] Đã bấm nút Tham gia nhóm thành công!");
                        Sleep(2000, 3000);
                        return true;
                    }
                }

                DoLog(log, "[i] Không thấy nút Tham gia (có thể đã là thành viên)");
                return false;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi tham gia nhóm: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Bình luận seeding vào bài viết theo link chỉ định, hỗ trợ Spintax
        /// </summary>
        public bool CommentPost(IWebDriver driver, string postUrl, string commentSpintax, Action<string> log)
        {
            try
            {
                string resolvedComment = SpintaxHelper.Process(commentSpintax);
                DoLog(log, string.Format("[*] Mở bài viết: {0}", postUrl));
                driver.Navigate().GoToUrl(postUrl);
                Sleep(4000, 6000);

                // Cuộn xuống để load khung bình luận
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("window.scrollBy({ top: 400, behavior: 'smooth' });");
                Sleep(1500, 2500);

                // Tìm khung nhập bình luận
                var commentBoxes = driver.FindElements(By.CssSelector("div[aria-label='Viết bình luận'], div[aria-label='Write a comment'], div[role='textbox'], div[aria-label*='bình luận']"));
                IWebElement targetBox = null;
                foreach (var box in commentBoxes)
                {
                    if (box.Displayed)
                    {
                        targetBox = box;
                        break;
                    }
                }

                if (targetBox != null)
                {
                    targetBox.Click();
                    Sleep(500, 1000);
                    DoLog(log, string.Format("[*] Nhập bình luận: \"{0}\"...", resolvedComment));
                    HumanType(targetBox, resolvedComment);
                    Sleep(1000, 1500);
                    targetBox.SendKeys(Keys.Enter);
                    Sleep(3000, 4000);
                    DoLog(log, "[+] Đã gửi bình luận seeding thành công!");
                    return true;
                }
                else
                {
                    DoLog(log, "[-] Không tìm thấy ô nhập bình luận trên bài viết này");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi bình luận bài viết: {0}", ex.Message));
                return false;
            }
        }

        #endregion
    }
}
