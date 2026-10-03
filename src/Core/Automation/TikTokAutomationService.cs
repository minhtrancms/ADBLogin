using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;

namespace ADBLogin.Core.Automation
{
    public class TikTokAccountStats
    {
        public string Username { get; set; }
        public string Following { get; set; }
        public string Followers { get; set; }
        public string Likes { get; set; }
        public string Status { get; set; }
    }

    /// <summary>
    /// Service tự động hóa các thao tác trên TikTok:
    /// - Đăng nhập bằng Cookie (sessionid) hoặc Tài khoản mật khẩu
    /// - Nuôi nick lướt FYP (For You Page): xem video tự nhiên, lướt phím mũi tên, thả tim ngẫu nhiên, xem bình luận
    /// - Tự động Follow kênh theo danh sách chỉ định
    /// - Bình luận video theo nội dung Spintax
    /// - Kiểm tra trạng thái tài khoản & số lượng Follower/Like
    /// - Xuất Cookie TikTok (sessionid, ttwid...)
    /// </summary>
    public class TikTokAutomationService
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

        #endregion

        #region 1. Đăng nhập TikTok (Cookie & User/Pass)

        /// <summary>
        /// Đăng nhập TikTok bằng chuỗi Cookie (chứa sessionid, ttwid...)
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

                DoLog(log, "[*] Điều hướng đến TikTok để nạp Cookie...");
                driver.Navigate().GoToUrl("https://www.tiktok.com/");
                Sleep(2000, 3000);

                string[] parts = cookieString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int count = 0;

                foreach (var part in parts)
                {
                    int eqIndex = part.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        string name = part.Substring(0, eqIndex).Trim();
                        string value = part.Substring(eqIndex + 1).Trim();

                        if (!string.IsNullOrEmpty(name))
                        {
                            try
                            {
                                driver.Manage().Cookies.DeleteCookieNamed(name);
                                driver.Manage().Cookies.AddCookie(new Cookie(name, value, ".tiktok.com", "/", DateTime.Now.AddYears(1)));
                                count++;
                            }
                            catch { }
                        }
                    }
                }

                DoLog(log, string.Format("[*] Đã nạp {0} cookie. Đang làm mới trang TikTok...", count));
                driver.Navigate().Refresh();
                Sleep(3500, 5000);

                // Kiểm tra đăng nhập thành công
                bool isLoggedIn = CheckIsLoggedIn(driver);
                if (isLoggedIn)
                {
                    DoLog(log, "[✓] ĐĂNG NHẬP TIKTOK QUA COOKIE THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    DoLog(log, "[!] Đã nạp Cookie nhưng chưa nhận diện avatar (Có thể cần thêm sessionid hợp lệ).");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi đăng nhập TikTok bằng Cookie: {0}", ex.Message));
                return false;
            }
        }

        public bool CheckIsLoggedIn(IWebDriver driver)
        {
            try
            {
                string page = driver.PageSource;
                if (page.Contains("header-more-menu") || page.Contains("avatar-anchor") || page.Contains("tiktok-avatar") || page.Contains("profile-button"))
                {
                    return true;
                }
                var avatars = driver.FindElements(By.CssSelector("[data-e2e='profile-icon'], img[class*='avatar'], a[href*='/@']"));
                return avatars.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 2. Nuôi nick lướt FYP (For You Page)

        /// <summary>
        /// Tự động lướt trang For You Page (FYP), xem video tự nhiên theo thời lượng ngẫu nhiên, ngẫu nhiên thả tim và xem comment
        /// </summary>
        public bool SurfFyp(IWebDriver driver, int videoCount = 10, int minWatchSec = 6, int maxWatchSec = 16, int likeRatePercent = 30, bool viewComments = true, Action<string> log = null)
        {
            try
            {
                DoLog(log, string.Format("[*] Bắt đầu lướt FYP TikTok: {0} video (Thời gian xem: {1}-{2}s, Tỷ lệ tim: {3}%)...", videoCount, minWatchSec, maxWatchSec, likeRatePercent));

                if (!driver.Url.Contains("tiktok.com/foryou"))
                {
                    driver.Navigate().GoToUrl("https://www.tiktok.com/foryou");
                    Sleep(3000, 4500);
                }

                // Tắt các popup gợi ý ứng dụng hoặc đăng nhập nếu có
                DismissPopups(driver);

                for (int i = 1; i <= videoCount; i++)
                {
                    int watchSec = _rnd.Next(minWatchSec, maxWatchSec + 1);
                    DoLog(log, string.Format("[*] Video #{0}/{1}: Đang xem tự nhiên trong {2} giây...", i, videoCount, watchSec));

                    int elapsed = 0;
                    while (elapsed < watchSec)
                    {
                        int step = _rnd.Next(2, 4);
                        Thread.Sleep(step * 1000);
                        elapsed += step;
                    }

                    // Tỷ lệ ngẫu nhiên bấm Thả Tim (Like)
                    if (_rnd.Next(1, 101) <= likeRatePercent)
                    {
                        try
                        {
                            // Thử bấm nút Like bằng phím nóng "L" của TikTok hoặc click selector
                            var likeBtn = driver.FindElement(By.CssSelector("[data-e2e='like-icon'], [data-e2e='feed-like-icon'], span[data-e2e='like-icon']"));
                            if (likeBtn != null)
                            {
                                likeBtn.Click();
                                DoLog(log, string.Format("  -> [♥] Đã thả tim Video #{0}", i));
                            }
                        }
                        catch
                        {
                            // Fallback gửi phím "l"
                            try
                            {
                                driver.FindElement(By.TagName("body")).SendKeys("l");
                                DoLog(log, string.Format("  -> [♥] Đã gửi phím Like Video #{0}", i));
                            }
                            catch { }
                        }
                        Sleep(1000, 1800);
                    }

                    // Thỉnh thoảng mở xem phần bình luận để tăng trust
                    if (viewComments && _rnd.Next(1, 101) <= 25)
                    {
                        try
                        {
                            var commentBtn = driver.FindElement(By.CssSelector("[data-e2e='comment-icon'], [data-e2e='feed-comment-icon']"));
                            if (commentBtn != null)
                            {
                                commentBtn.Click();
                                DoLog(log, string.Format("  -> [💬] Mở xem bình luận Video #{0}...", i));
                                Sleep(2500, 4000);

                                // Bấm nút đóng comment hoặc click lại
                                var closeComment = driver.FindElement(By.CssSelector("[data-e2e='comment-close-icon'], button[class*='close']"));
                                if (closeComment != null) closeComment.Click();
                            }
                        }
                        catch { }
                    }

                    // Chuyển sang video tiếp theo: TikTok hỗ trợ bấm Phím Mũi Tên Xuống (Arrow Down)
                    DoLog(log, "  -> Chuyển sang video tiếp theo...");
                    try
                    {
                        driver.FindElement(By.TagName("body")).SendKeys(Keys.ArrowDown);
                    }
                    catch
                    {
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                        js.ExecuteScript("window.scrollBy({ top: window.innerHeight, behavior: 'smooth' });");
                    }

                    Sleep(1500, 2500);
                }

                DoLog(log, string.Format("[✓] HOÀN TẤT LƯỢT NUÔI NICK TIKTOK ({0} video)!", videoCount));
                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi lướt FYP TikTok: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 3. Follow Kênh Chỉ Định

        /// <summary>
        /// Theo dõi kênh TikTok theo đường dẫn trang cá nhân hoặc @username
        /// </summary>
        public bool FollowUser(IWebDriver driver, string profileUrlOrUsername, Action<string> log = null)
        {
            try
            {
                string targetUrl = profileUrlOrUsername.StartsWith("http")
                    ? profileUrlOrUsername
                    : "https://www.tiktok.com/@" + profileUrlOrUsername.TrimStart('@');

                DoLog(log, string.Format("[*] Điều hướng đến kênh: {0}", targetUrl));
                driver.Navigate().GoToUrl(targetUrl);
                Sleep(3000, 4500);

                DismissPopups(driver);

                // Tìm nút Follow
                IWebElement followBtn = null;
                string[] selectors = new string[]
                {
                    "[data-e2e='follow-button']",
                    "button[data-e2e='follow-button']",
                    "button:has-text('Follow')",
                    "button:has-text('Theo dõi')"
                };

                foreach (var sel in selectors)
                {
                    try
                    {
                        var el = driver.FindElement(By.CssSelector(sel));
                        if (el != null && el.Displayed)
                        {
                            followBtn = el;
                            break;
                        }
                    }
                    catch { }
                }

                if (followBtn == null)
                {
                    // Kiểm tra xem đã theo dõi từ trước chưa
                    if (driver.PageSource.Contains("Following") || driver.PageSource.Contains("Đang theo dõi"))
                    {
                        DoLog(log, "[+] Kênh này đã theo dõi từ trước!");
                        return true;
                    }
                    DoLog(log, "[-] Không tìm thấy nút Follow trên trang");
                    return false;
                }

                string btnText = followBtn.Text;
                if (btnText.Contains("Following") || btnText.Contains("Đang theo dõi"))
                {
                    DoLog(log, "[+] Kênh này đã theo dõi từ trước!");
                    return true;
                }

                followBtn.Click();
                Sleep(1500, 2500);
                DoLog(log, string.Format("[✓] ĐÃ FOLLOW THÀNH CÔNG KÊNH: {0}", targetUrl));
                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi follow kênh TikTok: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 4. Bình luận Video bằng Spintax

        /// <summary>
        /// Bình luận video TikTok bằng nội dung Spintax
        /// </summary>
        public bool CommentVideo(IWebDriver driver, string videoUrl, string spintaxComment, Action<string> log = null)
        {
            try
            {
                string comment = SpintaxHelper.Spin(spintaxComment);
                DoLog(log, string.Format("[*] Mở video để bình luận: {0}", videoUrl));
                driver.Navigate().GoToUrl(videoUrl);
                Sleep(3500, 5000);

                DismissPopups(driver);

                // Tìm ô nhập comment
                IWebElement inputEl = null;
                string[] inputSelectors = new string[]
                {
                    "[data-e2e='comment-input'] [contenteditable='true']",
                    "div[contenteditable='true']",
                    "[data-e2e='comment-input']",
                    "textarea[placeholder*='comment']",
                    "textarea[placeholder*='bình luận']"
                };

                foreach (var sel in inputSelectors)
                {
                    try
                    {
                        var el = driver.FindElement(By.CssSelector(sel));
                        if (el != null && el.Displayed)
                        {
                            inputEl = el;
                            break;
                        }
                    }
                    catch { }
                }

                if (inputEl == null)
                {
                    DoLog(log, "[-] Không tìm thấy ô nhập bình luận (Có thể video khóa bình luận hoặc chưa đăng nhập)");
                    return false;
                }

                inputEl.Click();
                Sleep(500, 1000);
                HumanType(inputEl, comment);
                Sleep(800, 1500);

                // Bấm nút Post
                try
                {
                    var postBtn = driver.FindElement(By.CssSelector("[data-e2e='comment-post'], div[class*='PostButton']"));
                    if (postBtn != null)
                    {
                        postBtn.Click();
                    }
                    else
                    {
                        inputEl.SendKeys(Keys.Enter);
                    }
                }
                catch
                {
                    inputEl.SendKeys(Keys.Enter);
                }

                Sleep(2000, 3000);
                DoLog(log, string.Format("[✓] ĐÃ GỬI BÌNH LUẬN: \"{0}\"", comment));
                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi bình luận TikTok: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 5. Kiểm tra Thống kê & Trạng thái Nick

        /// <summary>
        /// Đọc thông tin Followers, Following, Likes của tài khoản TikTok
        /// </summary>
        public TikTokAccountStats CheckProfileStats(IWebDriver driver, Action<string> log = null)
        {
            var stats = new TikTokAccountStats { Status = "Unknown" };
            try
            {
                DoLog(log, "[*] Đang điều hướng đến trang hồ sơ cá nhân...");
                driver.Navigate().GoToUrl("https://www.tiktok.com/profile");
                Sleep(3500, 5000);

                try
                {
                    var followingEl = driver.FindElement(By.CssSelector("[data-e2e='following-count']"));
                    if (followingEl != null) stats.Following = followingEl.Text;

                    var followersEl = driver.FindElement(By.CssSelector("[data-e2e='followers-count']"));
                    if (followersEl != null) stats.Followers = followersEl.Text;

                    var likesEl = driver.FindElement(By.CssSelector("[data-e2e='likes-count']"));
                    if (likesEl != null) stats.Likes = likesEl.Text;

                    var userEl = driver.FindElement(By.CssSelector("[data-e2e='user-title'], h1[data-e2e='user-title']"));
                    if (userEl != null) stats.Username = userEl.Text;

                    stats.Status = "Live (Đang hoạt động)";
                    DoLog(log, string.Format("[✓] TÀI KHOẢN TIKTOK: {0} | Follower: {1} | Following: {2} | Likes: {3}", stats.Username, stats.Followers, stats.Following, stats.Likes));
                }
                catch
                {
                    stats.Status = "Chưa nhận diện được hồ sơ (Có thể chưa login)";
                    DoLog(log, "[!] Chưa đọc được thông số trang cá nhân TikTok.");
                }

                return stats;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi kiểm tra stats TikTok: {0}", ex.Message));
                stats.Status = "Error: " + ex.Message;
                return stats;
            }
        }

        #endregion

        #region 6. Xuất Cookie TikTok

        /// <summary>
        /// Trích xuất toàn bộ cookie TikTok (bao gồm sessionid, ttwid...)
        /// </summary>
        public string ExtractTikTokCookies(IWebDriver driver, Action<string> log = null)
        {
            try
            {
                var cookies = driver.Manage().Cookies.AllCookies;
                StringBuilder sb = new StringBuilder();
                string sessionId = "";

                foreach (var c in cookies)
                {
                    sb.Append(string.Format("{0}={1}; ", c.Name, c.Value));
                    if (c.Name.Equals("sessionid", StringComparison.OrdinalIgnoreCase))
                    {
                        sessionId = c.Value;
                    }
                }

                string result = sb.ToString().TrimEnd(' ', ';');
                if (!string.IsNullOrEmpty(sessionId))
                {
                    DoLog(log, string.Format("[✓] Tìm thấy SessionID: {0}...", sessionId.Substring(0, Math.Min(10, sessionId.Length))));
                }
                DoLog(log, string.Format("[✓] Trích xuất thành công {0} cookies TikTok!", cookies.Count));
                return result;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi xuất cookie TikTok: {0}", ex.Message));
                return string.Empty;
            }
        }

        #endregion

        private void DismissPopups(IWebDriver driver)
        {
            try
            {
                // Bấm nút đóng modal popup tải app / cookie / login
                var closeButtons = driver.FindElements(By.CssSelector("[data-e2e='modal-close-icon'], button[aria-label='Close'], div[class*='DivCloseWrapper']"));
                foreach (var btn in closeButtons)
                {
                    if (btn.Displayed)
                    {
                        btn.Click();
                        Sleep(500, 1000);
                        break;
                    }
                }
            }
            catch { }
        }
    }
}
