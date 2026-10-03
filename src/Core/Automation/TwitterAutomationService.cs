using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;

namespace ADBLogin.Core.Automation
{
    /// <summary>
    /// Service tự động hóa các thao tác trên X (Twitter):
    /// - Nuôi nick lướt Home Timeline tự nhiên (Cuộn mượt, ngẫu nhiên Thả tim & Retweet)
    /// - Auto Follow tài khoản mục tiêu theo danh sách
    /// - Tự động đăng Tweet / Reply bằng nội dung Spintax
    /// - Đăng nhập bằng Cookie (auth_token, ct0) & Xuất Cookie
    /// </summary>
    public class TwitterAutomationService
    {
        private static readonly Random _rnd = new Random();

        #region Helpers

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

        #region 1. Đăng nhập & Xuất Cookie

        public bool LoginWithCookie(IWebDriver driver, string cookieString, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cookieString))
                {
                    DoLog(log, "[-] Chuỗi Cookie X/Twitter rỗng");
                    return false;
                }

                DoLog(log, "[*] Điều hướng đến X (Twitter)...");
                driver.Navigate().GoToUrl("https://x.com/");
                Sleep(2500, 3500);

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
                                driver.Manage().Cookies.AddCookie(new Cookie(name, value, ".x.com", "/", DateTime.Now.AddYears(1)));
                                count++;
                            }
                            catch { }
                        }
                    }
                }

                DoLog(log, string.Format("[*] Đã nạp {0} cookie. Đang làm mới trang X...", count));
                driver.Navigate().Refresh();
                Sleep(4000, 5500);

                string page = driver.PageSource;
                if (page.Contains("data-testid=\"AppTabBar_Profile_Link\"") || page.Contains("data-testid=\"SideNav_NewTweet_Button\"") || page.Contains("/home"))
                {
                    DoLog(log, "[✓] ĐĂNG NHẬP X (TWITTER) THÀNH CÔNG QUA COOKIE!");
                    return true;
                }
                else
                {
                    DoLog(log, "[!] Đã nạp Cookie X (auth_token) và làm mới trang.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi đăng nhập X/Twitter: {0}", ex.Message));
                return false;
            }
        }

        public string ExtractTwitterCookies(IWebDriver driver, Action<string> log = null)
        {
            try
            {
                var cookies = driver.Manage().Cookies.AllCookies;
                StringBuilder sb = new StringBuilder();
                foreach (var c in cookies)
                {
                    sb.Append(string.Format("{0}={1}; ", c.Name, c.Value));
                }
                string result = sb.ToString().TrimEnd(' ', ';');
                DoLog(log, string.Format("[✓] Trích xuất thành công {0} cookies X (Twitter)!", cookies.Count));
                return result;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi xuất cookie X: {0}", ex.Message));
                return string.Empty;
            }
        }

        #endregion

        #region 2. Nuôi Nick Lướt Home Timeline

        public bool SurfTimeline(IWebDriver driver, int tweetCount = 10, int minReadSec = 4, int maxReadSec = 10, int likeRatePercent = 25, int retweetRatePercent = 10, Action<string> log = null)
        {
            try
            {
                DoLog(log, string.Format("[*] Bắt đầu lướt Home Timeline X: {0} bài (Tỷ lệ Tim: {1}%, RT: {2}%)...", tweetCount, likeRatePercent, retweetRatePercent));
                driver.Navigate().GoToUrl("https://x.com/home");
                Sleep(3500, 5000);

                for (int i = 1; i <= tweetCount; i++)
                {
                    int readSec = _rnd.Next(minReadSec, maxReadSec + 1);
                    DoLog(log, string.Format("[*] Bài viết #{0}/{1}: Dừng đọc trong {2} giây...", i, tweetCount, readSec));
                    Thread.Sleep(readSec * 1000);

                    // Tỷ lệ bấm Like
                    if (_rnd.Next(1, 101) <= likeRatePercent)
                    {
                        try
                        {
                            var likeBtns = driver.FindElements(By.CssSelector("[data-testid='like']"));
                            if (likeBtns.Count > 0)
                            {
                                int idx = Math.Min(likeBtns.Count - 1, _rnd.Next(0, 2));
                                if (likeBtns[idx].Displayed)
                                {
                                    likeBtns[idx].Click();
                                    DoLog(log, string.Format("  -> [♥] Đã thả Like bài viết #{0}", i));
                                    Sleep(1000, 2000);
                                }
                            }
                        }
                        catch { }
                    }

                    // Tỷ lệ bấm Retweet
                    if (_rnd.Next(1, 101) <= retweetRatePercent)
                    {
                        try
                        {
                            var rtBtns = driver.FindElements(By.CssSelector("[data-testid='retweet']"));
                            if (rtBtns.Count > 0)
                            {
                                int idx = Math.Min(rtBtns.Count - 1, _rnd.Next(0, 2));
                                if (rtBtns[idx].Displayed)
                                {
                                    rtBtns[idx].Click();
                                    Sleep(1000, 1500);
                                    // Xác nhận Repost
                                    var confirmRt = driver.FindElement(By.CssSelector("[data-testid='retweetConfirm']"));
                                    if (confirmRt != null && confirmRt.Displayed) confirmRt.Click();
                                    DoLog(log, string.Format("  -> [🔁] Đã Retweet bài viết #{0}", i));
                                    Sleep(1000, 2000);
                                }
                            }
                        }
                        catch { }
                    }

                    // Cuộn xuống bài tiếp theo
                    SmoothScroll(driver, _rnd.Next(350, 650));
                    Sleep(1500, 2500);
                }

                DoLog(log, string.Format("[✓] HOÀN TẤT LƯỢT NUÔI NICK X/TWITTER ({0} bài)!", tweetCount));
                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi lướt Timeline X: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 3. Auto Follow Tài Khoản

        public bool FollowUser(IWebDriver driver, string userHandleOrUrl, Action<string> log = null)
        {
            try
            {
                string targetUrl = userHandleOrUrl.StartsWith("http")
                    ? userHandleOrUrl
                    : "https://x.com/" + userHandleOrUrl.TrimStart('@');

                DoLog(log, string.Format("[*] Điều hướng đến trang cá nhân X: {0}", targetUrl));
                driver.Navigate().GoToUrl(targetUrl);
                Sleep(3500, 5000);

                // Tìm nút Follow
                IWebElement followBtn = null;
                string[] selectors = new string[]
                {
                    "[data-testid$='-follow']",
                    "button[aria-label*='Follow']",
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
                    if (driver.PageSource.Contains("data-testid=\"placementTracking\"") || driver.PageSource.Contains("Following") || driver.PageSource.Contains("Đang theo dõi"))
                    {
                        DoLog(log, "[+] Tài khoản này đã theo dõi từ trước!");
                        return true;
                    }
                    DoLog(log, "[-] Không tìm thấy nút Follow trên trang");
                    return false;
                }

                string btnText = followBtn.Text;
                if (btnText.Contains("Following") || btnText.Contains("Đang theo dõi"))
                {
                    DoLog(log, "[+] Tài khoản này đã theo dõi từ trước!");
                    return true;
                }

                followBtn.Click();
                Sleep(1500, 2500);
                DoLog(log, string.Format("[✓] ĐÃ FOLLOW THÀNH CÔNG TÀI KHOẢN X: {0}", targetUrl));
                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi follow tài khoản X: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 4. Đăng Bài Tweet bằng Spintax

        public bool PostTweet(IWebDriver driver, string spintaxContent, Action<string> log = null)
        {
            try
            {
                string content = SpintaxHelper.Spin(spintaxContent);
                DoLog(log, string.Format("[*] Chuẩn bị đăng Tweet: \"{0}\"", content));

                if (!driver.Url.Contains("x.com/home"))
                {
                    driver.Navigate().GoToUrl("https://x.com/home");
                    Sleep(3500, 5000);
                }

                // Tìm ô nhập nội dung Tweet
                IWebElement tweetInput = null;
                string[] inputSelectors = new string[]
                {
                    "[data-testid='tweetTextarea_0']",
                    "div[class*='public-DraftEditor-content']",
                    "div[role='textbox']"
                };

                foreach (var sel in inputSelectors)
                {
                    try
                    {
                        var el = driver.FindElement(By.CssSelector(sel));
                        if (el != null && el.Displayed)
                        {
                            tweetInput = el;
                            break;
                        }
                    }
                    catch { }
                }

                if (tweetInput == null)
                {
                    DoLog(log, "[-] Không tìm thấy ô soạn thảo Tweet");
                    return false;
                }

                tweetInput.Click();
                Sleep(500, 1000);
                HumanType(tweetInput, content);
                Sleep(1000, 2000);

                // Bấm nút Đăng (Post / Tweet)
                var postBtn = driver.FindElement(By.CssSelector("[data-testid='tweetButtonInline'], [data-testid='tweetButton']"));
                if (postBtn != null && postBtn.Displayed && postBtn.Enabled)
                {
                    postBtn.Click();
                    Sleep(2500, 4000);
                    DoLog(log, "[✓] ĐÃ ĐĂNG BÀI VIẾT LÊN X (TWITTER) THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    DoLog(log, "[-] Không thể bấm nút Đăng Tweet.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi đăng Tweet: {0}", ex.Message));
                return false;
            }
        }

        #endregion
    }
}
