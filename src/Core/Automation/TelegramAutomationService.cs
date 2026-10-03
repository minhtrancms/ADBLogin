using System;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;

namespace ADBLogin.Core.Automation
{
    /// <summary>
    /// Service tự động hóa các thao tác trên Telegram Web:
    /// - Mở Telegram Web (bản K/A)
    /// - Tham gia Channel / Group theo link
    /// - Thả Reaction tương tác tin nhắn mới nhất
    /// - Kiểm tra trạng thái đăng nhập
    /// </summary>
    public class TelegramAutomationService
    {
        private static readonly Random _rnd = new Random();

        public static void Sleep(int minMs, int maxMs = -1)
        {
            int delay = maxMs > minMs ? _rnd.Next(minMs, maxMs) : minMs;
            Thread.Sleep(delay);
        }

        private static void DoLog(Action<string> log, string msg)
        {
            if (log != null) log(msg);
        }

        public bool OpenTelegramWeb(IWebDriver driver, Action<string> log = null)
        {
            try
            {
                DoLog(log, "[*] Đang điều hướng đến Telegram Web...");
                driver.Navigate().GoToUrl("https://web.telegram.org/k/");
                Sleep(3500, 5000);
                return CheckIsLoggedIn(driver);
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi mở Telegram Web: {0}", ex.Message));
                return false;
            }
        }

        public bool CheckIsLoggedIn(IWebDriver driver)
        {
            try
            {
                string page = driver.PageSource;
                if (page.Contains("chatlist-parts") || page.Contains("chats-container") || page.Contains("input-search"))
                {
                    return true;
                }
                var search = driver.FindElements(By.CssSelector(".input-search, [placeholder*='Search']"));
                return search.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public bool JoinChannel(IWebDriver driver, string channelUrlOrUsername, Action<string> log = null)
        {
            try
            {
                string link = channelUrlOrUsername.Trim();
                if (!link.StartsWith("http"))
                {
                    link = "https://t.me/" + link.TrimStart('@');
                }

                DoLog(log, string.Format("[*] Điều hướng tham gia nhóm/kênh: {0}", link));
                driver.Navigate().GoToUrl(link);
                Sleep(3500, 5000);

                // Bấm nút "Open in Web" hoặc "Join Channel"
                try
                {
                    var webBtn = driver.FindElement(By.CssSelector(".tgme_action_web_button, a[href*='web.telegram.org']"));
                    if (webBtn != null && webBtn.Displayed)
                    {
                        webBtn.Click();
                        Sleep(3000, 4500);
                    }
                }
                catch { }

                // Bấm nút Tham Gia (Join Channel / Join Group)
                IWebElement joinBtn = null;
                string[] selectors = new string[]
                {
                    "button.chat-join",
                    "button[class*='join']",
                    "div.chat-join",
                    "//button[contains(., 'JOIN') or contains(., 'Join') or contains(., 'Tham gia')]"
                };

                foreach (var sel in selectors)
                {
                    try
                    {
                        var el = sel.StartsWith("//") ? driver.FindElement(By.XPath(sel)) : driver.FindElement(By.CssSelector(sel));
                        if (el != null && el.Displayed)
                        {
                            joinBtn = el;
                            break;
                        }
                    }
                    catch { }
                }

                if (joinBtn != null)
                {
                    joinBtn.Click();
                    Sleep(2000, 3000);
                    DoLog(log, string.Format("[✓] ĐÃ BẤM THAM GIA KÊNH TELEGRAM: {0}", link));
                    return true;
                }
                else
                {
                    DoLog(log, "[+] Đã mở kênh (Có thể đã tham gia từ trước hoặc đang ở trong nhóm).");
                    return true;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi tham gia kênh Telegram: {0}", ex.Message));
                return false;
            }
        }

        public bool ReactLatestMessage(IWebDriver driver, Action<string> log = null)
        {
            try
            {
                DoLog(log, "[*] Đang tìm tin nhắn mới nhất để thả Reaction...");
                var messages = driver.FindElements(By.CssSelector(".bubble, .message-bubble, div[data-message-id]"));
                if (messages.Count > 0)
                {
                    var lastMsg = messages[messages.Count - 1];
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({behavior: 'smooth', block: 'center'});", lastMsg);
                    Sleep(1500, 2000);

                    // Thả reaction bằng hover hoặc double click
                    try
                    {
                        new OpenQA.Selenium.Interactions.Actions(driver).DoubleClick(lastMsg).Perform();
                        DoLog(log, "[✓] Đã thả Reaction vào tin nhắn mới nhất!");
                        return true;
                    }
                    catch { }
                }
                DoLog(log, "[!] Chưa thả được reaction.");
                return false;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi thả reaction Telegram: {0}", ex.Message));
                return false;
            }
        }
    }
}
