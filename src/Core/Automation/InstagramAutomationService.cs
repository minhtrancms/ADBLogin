using System;
using System.IO;
using System.Threading;
using OpenQA.Selenium;

namespace ADBLogin.Core.Automation
{
    /// <summary>
    /// Service tự động hóa Instagram Web:
    /// - Nuôi nick tương tác Feed & Reels
    /// - Tự động Follow theo danh sách username
    /// - Tự động đăng bài viết (kèm ảnh & caption Spintax)
    /// </summary>
    public class InstagramAutomationService
    {
        private static readonly Random _rnd = new Random();

        public static void Sleep(int minMs, int maxMs = -1)
        {
            int delay = maxMs > minMs ? _rnd.Next(minMs, maxMs) : minMs;
            Thread.Sleep(delay);
        }

        public static void HumanType(IWebElement element, string text)
        {
            if (element == null || string.IsNullOrEmpty(text)) return;
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

        /// <summary>
        /// Lướt bảng tin Instagram (Feed): cuộn mượt và thả tim ngẫu nhiên
        /// </summary>
        public void SurfFeed(IWebDriver driver, int durationSeconds, bool autoLike, int maxLikes, Action<string> log, CancellationToken ct)
        {
            try
            {
                DoLog(log, string.Format("[*] Bắt đầu lướt Feed Instagram trong {0}s...", durationSeconds));
                if (!driver.Url.Contains("instagram.com"))
                {
                    driver.Navigate().GoToUrl("https://www.instagram.com/");
                    Sleep(3000, 5000);
                }

                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                DateTime startTime = DateTime.Now;
                int currentLikes = 0;

                while ((DateTime.Now - startTime).TotalSeconds < durationSeconds)
                {
                    if (ct.IsCancellationRequested) break;

                    int scroll = _rnd.Next(400, 800);
                    js.ExecuteScript(string.Format("window.scrollBy({{ top: {0}, behavior: 'smooth' }});", scroll));

                    int readTime = _rnd.Next(3000, 6000);
                    DoLog(log, string.Format("[*] Cuộn xuống +{0}px, dừng xem {1:0.0}s...", scroll, readTime / 1000.0));
                    Sleep(readTime);

                    // Thả tim ngẫu nhiên
                    if (autoLike && currentLikes < maxLikes && _rnd.Next(100) < 35)
                    {
                        try
                        {
                            var likeSvgs = driver.FindElements(By.CssSelector("svg[aria-label='Thích'], svg[aria-label='Like']"));
                            foreach (var svg in likeSvgs)
                            {
                                if (svg.Displayed)
                                {
                                    IWebElement btn = svg;
                                    try { btn = svg.FindElement(By.XPath("./ancestor::button")); } catch { }
                                    btn.Click();
                                    currentLikes++;
                                    DoLog(log, string.Format("[♥] Đã thả tim bài viết ({0}/{1})", currentLikes, maxLikes));
                                    Sleep(1500, 3000);
                                    break;
                                }
                            }
                        }
                        catch { }
                    }

                    if (_rnd.Next(100) < 15)
                    {
                        int scrollUp = _rnd.Next(100, 250);
                        js.ExecuteScript(string.Format("window.scrollBy({{ top: -{0}, behavior: 'smooth' }});", scrollUp));
                        Sleep(1200, 2000);
                    }
                }

                DoLog(log, string.Format("[+] Hoàn thành lướt Feed! Tổng lượt thích: {0}", currentLikes));
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi lướt Feed Instagram: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Lướt xem video Instagram Reels
        /// </summary>
        public void WatchReels(IWebDriver driver, int reelCount, Action<string> log, CancellationToken ct)
        {
            try
            {
                DoLog(log, string.Format("[*] Bắt đầu xem {0} video Instagram Reels...", reelCount));
                driver.Navigate().GoToUrl("https://www.instagram.com/reels/");
                Sleep(3500, 5500);

                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                for (int i = 1; i <= reelCount; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    int watchSec = _rnd.Next(10, 25);
                    DoLog(log, string.Format("[▶] Đang xem Reel #{0}/{1} trong {2}s...", i, reelCount, watchSec));
                    Sleep(watchSec * 1000);

                    // Thả tim ngẫu nhiên tỷ lệ 25%
                    if (_rnd.Next(100) < 25)
                    {
                        try
                        {
                            var likeBtn = driver.FindElements(By.CssSelector("svg[aria-label='Thích'], svg[aria-label='Like']"));
                            if (likeBtn.Count > 0 && likeBtn[0].Displayed)
                            {
                                try { likeBtn[0].Click(); } catch { }
                                DoLog(log, "[♥] Đã thả tim Reel!");
                                Sleep(1000, 2000);
                            }
                        }
                        catch { }
                    }

                    // Chuyển sang Reel tiếp theo bằng ArrowDown hoặc scroll
                    try
                    {
                        driver.FindElement(By.TagName("body")).SendKeys(Keys.ArrowDown);
                    }
                    catch
                    {
                        js.ExecuteScript("window.scrollBy({ top: window.innerHeight, behavior: 'smooth' });");
                    }
                    Sleep(1500, 2500);
                }

                DoLog(log, "[+] Hoàn tất xem Instagram Reels!");
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi xem Reels: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Theo dõi (Follow) tài khoản Instagram mục tiêu
        /// </summary>
        public bool FollowUser(IWebDriver driver, string usernameOrUrl, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usernameOrUrl)) return false;

                string target = usernameOrUrl.Trim();
                if (!target.StartsWith("http"))
                {
                    target = "https://www.instagram.com/" + target.TrimStart('@') + "/";
                }

                DoLog(log, string.Format("[*] Điều hướng tới trang cá nhân: {0}", target));
                driver.Navigate().GoToUrl(target);
                Sleep(3500, 5000);

                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                // Tìm nút Follow / Theo dõi
                var buttons = driver.FindElements(By.CssSelector("button[type='button'], div[role='button']"));
                foreach (var b in buttons)
                {
                    try
                    {
                        if (!b.Displayed) continue;
                        string txt = b.Text.Trim();
                        if (txt.Equals("Theo dõi", StringComparison.OrdinalIgnoreCase) ||
                            txt.Equals("Follow", StringComparison.OrdinalIgnoreCase))
                        {
                            DoLog(log, string.Format("[*] Đang bấm nút: \"{0}\"...", txt));
                            try { b.Click(); } catch { js.ExecuteScript("arguments[0].click();", b); }
                            Sleep(2000, 3500);
                            DoLog(log, "[+] ĐÃ THEO DÕI TÀI KHOẢN THÀNH CÔNG!");
                            return true;
                        }
                        else if (txt.Equals("Đang theo dõi", StringComparison.OrdinalIgnoreCase) ||
                                 txt.Equals("Following", StringComparison.OrdinalIgnoreCase))
                        {
                            DoLog(log, "[i] Tài khoản này ĐÃ ĐƯỢC THEO DÕI từ trước.");
                            return true;
                        }
                    }
                    catch { }
                }

                DoLog(log, "[-] Không tìm thấy nút Theo dõi trên trang này");
                return false;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi follow tài khoản: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Đăng bài viết mới lên Instagram (yêu cầu file ảnh và caption Spintax)
        /// </summary>
        public bool CreatePost(IWebDriver driver, string imagePath, string captionSpintax, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    DoLog(log, "[-] File ảnh không tồn tại để đăng lên Instagram!");
                    return false;
                }

                string resolvedCaption = SpintaxHelper.Process(captionSpintax);
                DoLog(log, "[*] Điều hướng tới Instagram...");
                driver.Navigate().GoToUrl("https://www.instagram.com/");
                Sleep(3500, 5000);

                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                // 1. Tìm nút Tạo bài viết (Create / New post)
                DoLog(log, "[*] Đang tìm nút Tạo bài viết mới...");
                var createSvgs = driver.FindElements(By.CssSelector("svg[aria-label='Bài viết mới'], svg[aria-label='New post'], svg[aria-label='Tạo']"));
                IWebElement createBtn = null;
                foreach (var s in createSvgs)
                {
                    if (s.Displayed)
                    {
                        try { createBtn = s.FindElement(By.XPath("./ancestor::a | ./ancestor::div[@role='button']")); }
                        catch { createBtn = s; }
                        break;
                    }
                }

                if (createBtn != null)
                {
                    try { createBtn.Click(); } catch { js.ExecuteScript("arguments[0].click();", createBtn); }
                    Sleep(2000, 3500);
                }

                // 2. Nạp file ảnh vào input[type=file]
                DoLog(log, string.Format("[*] Đang tải ảnh lên: {0}", Path.GetFileName(imagePath)));
                var fileInputs = driver.FindElements(By.CssSelector("input[type='file']"));
                if (fileInputs.Count > 0)
                {
                    fileInputs[0].SendKeys(Path.GetFullPath(imagePath));
                    Sleep(3500, 5000);
                }
                else
                {
                    DoLog(log, "[-] Không tìm thấy ô tải tệp ảnh trên Instagram");
                    return false;
                }

                // 3. Bấm Tiếp (Next) qua các bước chỉnh sửa
                for (int step = 0; step < 2; step++)
                {
                    var nextBtns = driver.FindElements(By.XPath("//div[@role='button' and (text()='Tiếp' or text()='Next')]"));
                    if (nextBtns.Count > 0 && nextBtns[0].Displayed)
                    {
                        try { nextBtns[0].Click(); } catch { js.ExecuteScript("arguments[0].click();", nextBtns[0]); }
                        Sleep(2000, 3000);
                    }
                }

                // 4. Nhập caption
                DoLog(log, "[*] Đang nhập caption bài viết...");
                var textareas = driver.FindElements(By.CssSelector("div[role='textbox'][aria-label*='chú thích'], div[role='textbox'][aria-label*='caption'], div[contenteditable='true']"));
                if (textareas.Count > 0 && textareas[0].Displayed)
                {
                    textareas[0].Click();
                    Sleep(500, 1000);
                    HumanType(textareas[0], resolvedCaption);
                    Sleep(1500, 2500);
                }

                // 5. Bấm nút Chia sẻ (Share)
                DoLog(log, "[*] Đang bấm nút [Chia sẻ]...");
                var shareBtns = driver.FindElements(By.XPath("//div[@role='button' and (text()='Chia sẻ' or text()='Share')]"));
                if (shareBtns.Count > 0 && shareBtns[0].Displayed)
                {
                    try { shareBtns[0].Click(); } catch { js.ExecuteScript("arguments[0].click();", shareBtns[0]); }
                    DoLog(log, "[*] Đang đợi Instagram xử lý đăng bài...");
                    Sleep(6000, 10000);
                    DoLog(log, "[+] ĐÃ ĐĂNG BÀI VIẾT INSTAGRAM THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    DoLog(log, "[-] Không tìm thấy nút Chia sẻ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi đăng bài Instagram: {0}", ex.Message));
                return false;
            }
        }
    }
}
