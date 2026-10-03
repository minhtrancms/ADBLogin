using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;

namespace ADBLogin.Core.Automation
{
    public class GoogleAccountInfo
    {
        public string Email { get; set; }
        public string Status { get; set; }
        public string LastOtp { get; set; }
        public string Cookies { get; set; }
    }

    /// <summary>
    /// Service tự động hóa các thao tác trên Google & Gmail:
    /// - Đăng nhập Google (Hỗ trợ mật khẩu, 2FA TOTP tự động, Email khôi phục)
    /// - Google Search Seeding (Tìm từ khóa, cuộn trang, click web mục tiêu, đọc bài tăng SEO)
    /// - Xem video YouTube tương tác tự nhiên
    /// - Đọc hộp thư Gmail và trích xuất mã OTP xác nhận
    /// - Xuất Cookie Google phục vụ đồng bộ
    /// </summary>
    public class GoogleAutomationService
    {
        private static readonly Random _rnd = new Random();

        #region Helpers: Sleep & Human Typing

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
                Thread.Sleep(_rnd.Next(30, 80));
            }
        }

        private static void DoLog(Action<string> log, string msg)
        {
            if (log != null) log(msg);
        }

        private static void SmoothScroll(IWebDriver driver, int distance = 400)
        {
            try
            {
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                js.ExecuteScript(string.Format("window.scrollBy({{ top: {0}, behavior: 'smooth' }});", distance));
            }
            catch { }
        }

        #endregion

        #region 1. Đăng nhập Google & 2FA

        /// <summary>
        /// Đăng nhập tài khoản Google với đầy đủ cơ chế vượt 2FA TOTP và Recovery Email
        /// </summary>
        public bool LoginGoogle(IWebDriver driver, string email, string password, string twoFactorSecret = "", string recoveryEmail = "", Action<string> log = null)
        {
            try
            {
                DoLog(log, string.Format("[*] Bắt đầu đăng nhập Google cho: {0}", email));
                driver.Navigate().GoToUrl("https://accounts.google.com/signin");
                Sleep(2500, 3500);

                // Kiểm tra xem đã đăng nhập trước đó chưa
                string currentUrl = driver.Url;
                if (currentUrl.Contains("myaccount.google.com") || currentUrl.Contains("/b/") || currentUrl.Contains("/u/0/"))
                {
                    DoLog(log, "[+] Tài khoản đã đăng nhập sẵn trên phiên này!");
                    return true;
                }

                // 1. Điền Email
                IWebElement emailInput = null;
                string[] emailSelectors = new string[] { "#identifierId", "input[type='email']", "input[name='identifier']" };
                foreach (var sel in emailSelectors)
                {
                    try
                    {
                        var el = driver.FindElement(By.CssSelector(sel));
                        if (el != null && el.Displayed)
                        {
                            emailInput = el;
                            break;
                        }
                    }
                    catch { }
                }

                if (emailInput == null)
                {
                    DoLog(log, "[-] Không tìm thấy ô nhập Email Google");
                    return false;
                }

                HumanType(emailInput, email);
                Sleep(500, 1000);
                DoLog(log, "[*] Đã nhập Email -> Bấm Tiếp theo");

                // Bấm Next
                try
                {
                    var nextBtn = driver.FindElement(By.CssSelector("#identifierNext, button[type='button']"));
                    nextBtn.Click();
                }
                catch
                {
                    emailInput.SendKeys(Keys.Enter);
                }

                Sleep(3000, 4500);

                // 2. Điền Mật khẩu
                IWebElement passInput = null;
                string[] passSelectors = new string[] { "input[type='password']", "input[name='Passwd']", "input[name='password']" };
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    foreach (var sel in passSelectors)
                    {
                        try
                        {
                            var el = driver.FindElement(By.CssSelector(sel));
                            if (el != null && el.Displayed)
                            {
                                passInput = el;
                                break;
                            }
                        }
                        catch { }
                    }
                    if (passInput != null) break;
                    Sleep(1000, 1500);
                }

                if (passInput == null)
                {
                    // Kiểm tra có thông báo lỗi tài khoản không tồn tại
                    if (driver.PageSource.Contains("Couldn't find your Google Account") || driver.PageSource.Contains("Không tìm thấy Tài khoản Google"))
                    {
                        DoLog(log, "[!] Lỗi: Tài khoản Google không tồn tại hoặc đã bị vô hiệu hóa.");
                        return false;
                    }
                    DoLog(log, "[-] Không tìm thấy ô nhập Mật khẩu Google");
                    return false;
                }

                HumanType(passInput, password);
                Sleep(600, 1200);
                DoLog(log, "[*] Đã nhập Mật khẩu -> Bấm Tiếp theo");

                try
                {
                    var nextPassBtn = driver.FindElement(By.CssSelector("#passwordNext, button[type='button']"));
                    nextPassBtn.Click();
                }
                catch
                {
                    passInput.SendKeys(Keys.Enter);
                }

                Sleep(4000, 6000);

                // 3. Xử lý bước 2FA (Nếu Google yêu cầu mã Authenticator OTP)
                if (!string.IsNullOrWhiteSpace(twoFactorSecret))
                {
                    IWebElement totpInput = null;
                    string[] totpSelectors = new string[] { "#totpPin", "input[type='tel']", "input[name='Pin']", "input[id*='totp']" };
                    foreach (var sel in totpSelectors)
                    {
                        try
                        {
                            var el = driver.FindElement(By.CssSelector(sel));
                            if (el != null && el.Displayed)
                            {
                                totpInput = el;
                                break;
                            }
                        }
                        catch { }
                    }

                    if (totpInput != null)
                    {
                        DoLog(log, "[*] Phát hiện yêu cầu xác thực 2FA. Đang tự động giải mã Secret Key...");
                        string otp = TotpGenerator.GenerateTotpCode(twoFactorSecret);
                        if (!string.IsNullOrEmpty(otp))
                        {
                            DoLog(log, string.Format("[+] Mã OTP 6 số: {0} -> Đang nhập tự động...", otp));
                            HumanType(totpInput, otp);
                            Sleep(500, 1000);
                            try
                            {
                                var totpNext = driver.FindElement(By.CssSelector("#totpNext, button[type='button']"));
                                totpNext.Click();
                            }
                            catch
                            {
                                totpInput.SendKeys(Keys.Enter);
                            }
                            Sleep(4000, 6000);
                        }
                    }
                }

                // 4. Xử lý bước xác nhận Email khôi phục (Recovery Email)
                if (!string.IsNullOrWhiteSpace(recoveryEmail))
                {
                    try
                    {
                        // Kiểm tra nếu trang đang hỏi "Confirm your recovery email"
                        if (driver.PageSource.Contains("recovery") || driver.PageSource.Contains("khôi phục") || driver.PageSource.Contains("knowledge-preregistered-email-response"))
                        {
                            var recoveryInput = driver.FindElement(By.CssSelector("input[name='knowledgePreregisteredEmailResponse'], input[type='email']"));
                            if (recoveryInput != null && recoveryInput.Displayed)
                            {
                                DoLog(log, string.Format("[*] Google yêu cầu xác nhận Email khôi phục: {0}", recoveryEmail));
                                HumanType(recoveryInput, recoveryEmail);
                                Sleep(500, 1000);
                                recoveryInput.SendKeys(Keys.Enter);
                                Sleep(4000, 5000);
                            }
                        }
                    }
                    catch { }
                }

                // Kiểm tra kết quả sau khi đăng nhập
                string postUrl = driver.Url;
                if (postUrl.Contains("myaccount.google.com") || postUrl.Contains("/b/") || !postUrl.Contains("signin"))
                {
                    DoLog(log, "[✓] ĐĂNG NHẬP GOOGLE THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    DoLog(log, "[!] Đăng nhập hoàn tất (Trình duyệt đang ở trang Google)");
                    return true;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi đăng nhập Google: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 2. Google Search Seeding (Tăng SEO & Nuôi Trust tài khoản)

        /// <summary>
        /// Tìm kiếm từ khóa trên Google, cuộn trang tự nhiên, tìm và click vào website mục tiêu, ở lại đọc bài
        /// </summary>
        public bool SearchAndSeedGoogle(IWebDriver driver, string keywordSpintax, string targetUrlOrDomain, int staySeconds = 30, Action<string> log = null)
        {
            try
            {
                string keyword = SpintaxHelper.Spin(keywordSpintax);
                DoLog(log, string.Format("[*] Điều hướng đến Google.com để tìm từ khóa: \"{0}\"", keyword));

                driver.Navigate().GoToUrl("https://www.google.com");
                Sleep(2000, 3000);

                // Đồng ý Cookie nếu có popup (Châu Âu / Google Consent)
                try
                {
                    var consentBtn = driver.FindElement(By.CssSelector("#L2AGLb, #W0wltc, button[aria-label*='Accept']"));
                    if (consentBtn != null && consentBtn.Displayed)
                    {
                        consentBtn.Click();
                        Sleep(1000, 1500);
                    }
                }
                catch { }

                // Tìm ô tìm kiếm Google
                IWebElement searchBox = null;
                string[] searchSelectors = new string[] { "textarea[name='q']", "input[name='q']", "[title='Tìm kiếm']", "[title='Search']" };
                foreach (var sel in searchSelectors)
                {
                    try
                    {
                        var el = driver.FindElement(By.CssSelector(sel));
                        if (el != null && el.Displayed)
                        {
                            searchBox = el;
                            break;
                        }
                    }
                    catch { }
                }

                if (searchBox == null)
                {
                    DoLog(log, "[-] Không tìm thấy thanh tìm kiếm Google");
                    return false;
                }

                HumanType(searchBox, keyword);
                Sleep(500, 1000);
                searchBox.SendKeys(Keys.Enter);
                Sleep(3000, 4500);

                DoLog(log, "[*] Đã tải kết quả tìm kiếm. Đang cuộn trang mô phỏng người đọc...");

                // Cuộn trang mượt
                for (int i = 0; i < 3; i++)
                {
                    SmoothScroll(driver, _rnd.Next(250, 450));
                    Sleep(1000, 2000);
                }

                // Nếu có chỉ định target website/domain
                if (!string.IsNullOrWhiteSpace(targetUrlOrDomain))
                {
                    DoLog(log, string.Format("[*] Đang tìm liên kết đích chứa: {0}", targetUrlOrDomain));
                    bool clicked = false;

                    var links = driver.FindElements(By.CssSelector("div#rso a, div.g a, a[jsname]"));
                    foreach (var link in links)
                    {
                        try
                        {
                            string href = link.GetAttribute("href");
                            if (!string.IsNullOrEmpty(href) && href.IndexOf(targetUrlOrDomain, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                DoLog(log, string.Format("[+] ĐÃ TÌM THẤY WEBSITE MỤC TIÊU: {0}", href));
                                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({behavior: 'smooth', block: 'center'});", link);
                                Sleep(1500, 2500);

                                link.Click();
                                clicked = true;
                                break;
                            }
                        }
                        catch { }
                    }

                    if (!clicked)
                    {
                        DoLog(log, string.Format("[!] Không tìm thấy website {0} ở Trang 1. Đang mở trực tiếp URL đích...", targetUrlOrDomain));
                        string fullUrl = targetUrlOrDomain.StartsWith("http") ? targetUrlOrDomain : "https://" + targetUrlOrDomain;
                        driver.Navigate().GoToUrl(fullUrl);
                    }

                    // Tương tác đọc trang mục tiêu trong thời gian quy định
                    DoLog(log, string.Format("[*] Đang ở lại tương tác đọc bài trên website trong {0} giây...", staySeconds));
                    int elapsed = 0;
                    while (elapsed < staySeconds)
                    {
                        int scrollStep = _rnd.Next(200, 500);
                        if (_rnd.Next(0, 10) > 7) scrollStep = -scrollStep / 2; // Thỉnh thoảng cuộn ngược lên mô phỏng đọc lại
                        SmoothScroll(driver, scrollStep);

                        int sleepStep = _rnd.Next(2, 5);
                        Thread.Sleep(sleepStep * 1000);
                        elapsed += sleepStep;
                    }
                    DoLog(log, "[✓] Hoàn thành lượt Seeding tìm kiếm Google!");
                }
                else
                {
                    DoLog(log, "[✓] Hoàn thành tìm kiếm Google tự nhiên!");
                }

                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi tìm kiếm Google: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 3. Xem YouTube Tương tác

        /// <summary>
        /// Xem video YouTube ngẫu nhiên hoặc theo từ khóa, tạo tương tác thật cho tài khoản Google
        /// </summary>
        public bool WatchYouTube(IWebDriver driver, string videoUrlOrKeyword, int watchSeconds = 30, bool autoLike = false, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(videoUrlOrKeyword))
                {
                    videoUrlOrKeyword = "trending music";
                }

                if (videoUrlOrKeyword.StartsWith("http"))
                {
                    DoLog(log, string.Format("[*] Mở video YouTube: {0}", videoUrlOrKeyword));
                    driver.Navigate().GoToUrl(videoUrlOrKeyword);
                }
                else
                {
                    string spunKeyword = SpintaxHelper.Spin(videoUrlOrKeyword);
                    DoLog(log, string.Format("[*] Tìm kiếm trên YouTube từ khóa: \"{0}\"", spunKeyword));
                    driver.Navigate().GoToUrl(string.Format("https://www.youtube.com/results?search_query={0}", Uri.EscapeDataString(spunKeyword)));
                    Sleep(3000, 4000);

                    // Bấm vào video đầu tiên trong kết quả
                    try
                    {
                        var firstVideo = driver.FindElement(By.CssSelector("ytd-video-renderer a#video-title, #video-title"));
                        if (firstVideo != null)
                        {
                            DoLog(log, string.Format("[+] Bấm xem video: {0}", firstVideo.GetAttribute("title")));
                            firstVideo.Click();
                        }
                    }
                    catch
                    {
                        DoLog(log, "[!] Bấm video đầu tiên bằng JavaScript");
                        ((IJavaScriptExecutor)driver).ExecuteScript("document.querySelector('ytd-video-renderer a#video-title')?.click();");
                    }
                }

                Sleep(3000, 5000);

                // Bấm Play nếu đang tạm dừng
                try
                {
                    var playBtn = driver.FindElement(By.CssSelector(".ytp-play-button"));
                    if (playBtn != null && playBtn.GetAttribute("data-title-no-tooltip") == "Play")
                    {
                        playBtn.Click();
                    }
                }
                catch { }

                DoLog(log, string.Format("[*] Đang xem video YouTube trong {0} giây...", watchSeconds));
                int elapsed = 0;
                while (elapsed < watchSeconds)
                {
                    // Thỉnh thoảng cuộn xem bình luận
                    if (_rnd.Next(0, 10) > 6)
                    {
                        SmoothScroll(driver, 300);
                        Sleep(1500, 2500);
                        SmoothScroll(driver, -200);
                    }

                    int step = _rnd.Next(3, 6);
                    Thread.Sleep(step * 1000);
                    elapsed += step;
                }

                // Thả Like nếu bật
                if (autoLike)
                {
                    try
                    {
                        var likeBtn = driver.FindElement(By.CssSelector("like-button-view-model button, button[aria-label*='like this video']"));
                        if (likeBtn != null)
                        {
                            likeBtn.Click();
                            DoLog(log, "[✓] Đã thả Like cho video YouTube!");
                        }
                    }
                    catch { }
                }

                DoLog(log, "[✓] Hoàn thành lượt xem YouTube!");
                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi xem YouTube: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 4. Đọc Hộp thư Gmail & Trích xuất OTP

        /// <summary>
        /// Mở hộp thư Gmail, đọc email mới nhất và trích xuất mã OTP xác nhận
        /// </summary>
        public string ReadGmailOtp(IWebDriver driver, string filterSender = "", Action<string> log = null)
        {
            try
            {
                DoLog(log, "[*] Đang mở Hộp thư Gmail (inbox)...");
                driver.Navigate().GoToUrl("https://mail.google.com/mail/u/0/#inbox");
                Sleep(4000, 6000);

                // Tìm danh sách thư trong hộp thư đến
                var emailRows = driver.FindElements(By.CssSelector("table.F.cf.zt tr.zA, tr.zA"));
                if (emailRows.Count == 0)
                {
                    DoLog(log, "[!] Chưa thấy email mới trong Hộp thư đến");
                    return string.Empty;
                }

                IWebElement targetMail = null;
                if (!string.IsNullOrWhiteSpace(filterSender))
                {
                    foreach (var row in emailRows)
                    {
                        if (row.Text.IndexOf(filterSender, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            targetMail = row;
                            break;
                        }
                    }
                }

                if (targetMail == null) targetMail = emailRows[0]; // Lấy email mới nhất

                DoLog(log, string.Format("[*] Đang mở email: {0}", targetMail.Text.Substring(0, Math.Min(60, targetMail.Text.Length))));
                targetMail.Click();
                Sleep(2500, 4000);

                // Lấy nội dung thân email
                string bodyText = "";
                try
                {
                    var bodyEl = driver.FindElement(By.CssSelector("div.a3s.aiL, div[dir='ltr']"));
                    if (bodyEl != null) bodyText = bodyEl.Text;
                }
                catch
                {
                    bodyText = driver.PageSource;
                }

                // Trích xuất mã OTP 4-8 số hoặc chữ số
                var match = Regex.Match(bodyText, @"\b\d{4,8}\b");
                if (match.Success)
                {
                    string otp = match.Value;
                    DoLog(log, string.Format("[✓] ĐÃ TÌM THẤY MÃ OTP TRONG GMAIL: {0}", otp));
                    return otp;
                }
                else
                {
                    DoLog(log, "[!] Đã mở thư nhưng không phát hiện mã số OTP dạng 4-8 số.");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi đọc hộp thư Gmail: {0}", ex.Message));
                return string.Empty;
            }
        }

        #endregion

        #region 5. Xuất Cookie Google

        /// <summary>
        /// Trích xuất toàn bộ cookie Google hiện tại trên phiên trình duyệt
        /// </summary>
        public string ExtractGoogleCookies(IWebDriver driver, Action<string> log = null)
        {
            try
            {
                var cookies = driver.Manage().Cookies.AllCookies;
                StringBuilder sb = new StringBuilder();
                foreach (var c in cookies)
                {
                    sb.Append(string.Format("{0}={1}; ", c.Name, c.Value));
                }
                string cookieStr = sb.ToString().TrimEnd(' ', ';');
                DoLog(log, string.Format("[✓] Trích xuất thành công {0} cookies Google!", cookies.Count));
                return cookieStr;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi xuất cookie Google: {0}", ex.Message));
                return string.Empty;
            }
        }

        #endregion
    }
}
