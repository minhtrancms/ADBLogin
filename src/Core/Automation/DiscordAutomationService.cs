using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;

namespace ADBLogin.Core.Automation
{
    /// <summary>
    /// Service tự động hóa Discord Web:
    /// - Đăng nhập bằng Discord Token (không cần mật khẩu)
    /// - Tự động tham gia server (Join Guild qua invite link)
    /// - Tự động click Verify / Đồng ý nội quy
    /// - Tự động chat seeding / cày cấp (XP leveling) hỗ trợ Spintax
    /// </summary>
    public class DiscordAutomationService
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
                Thread.Sleep(_rnd.Next(30, 80));
            }
        }

        private static void DoLog(Action<string> log, string msg)
        {
            if (log != null) log(msg);
        }

        /// <summary>
        /// Đăng nhập Discord Web trực tiếp bằng User Token thông qua localStorage
        /// </summary>
        public bool LoginWithToken(IWebDriver driver, string token, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    DoLog(log, "[-] Discord Token rỗng!");
                    return false;
                }

                DoLog(log, "[*] Đang điều hướng đến Discord Web...");
                driver.Navigate().GoToUrl("https://discord.com/login");
                Sleep(3000, 4500);

                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                DoLog(log, "[*] Đang nạp Discord Token vào phiên duyệt...");

                string cleanToken = token.Trim().Trim('"', '\'');
                string script = @"
                    try {
                        var t = arguments[0];
                        function login(token) {
                            setInterval(() => {
                                try {
                                    document.body.appendChild(document.createElement('iframe')).contentWindow.localStorage.token = '""' + token + '""';
                                } catch(e) {}
                            }, 50);
                            setTimeout(() => { location.href = 'https://discord.com/channels/@me'; }, 1000);
                        }
                        login(t);
                        return true;
                    } catch(e) {
                        return false;
                    }
                ";

                js.ExecuteScript(script, cleanToken);
                Sleep(4000, 6000);

                // Kiểm tra xem đã đăng nhập thành công chưa
                if (driver.Url.Contains("/channels/") || driver.PageSource.Contains("guildsnav") || driver.PageSource.Contains("userSettingsAccount"))
                {
                    DoLog(log, "[+] ĐĂNG NHẬP DISCORD BẰNG TOKEN THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    DoLog(log, "[!] Đã nạp token, đang đợi Discord đồng bộ dữ liệu phiên...");
                    Sleep(3000, 5000);
                    return true;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi đăng nhập Discord: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Tham gia Server Discord qua link invite (discord.gg/... hoặc discord.com/invite/...)
        /// </summary>
        public bool JoinServer(IWebDriver driver, string inviteUrl, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(inviteUrl)) return false;

                string link = inviteUrl.Trim();
                if (!link.StartsWith("http"))
                {
                    link = "https://discord.gg/" + link;
                }

                DoLog(log, string.Format("[*] Điều hướng tới lời mời: {0}", link));
                driver.Navigate().GoToUrl(link);
                Sleep(3500, 5000);

                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                // Tìm nút "Chấp nhận lời mời" / "Accept Invite"
                var btnSelectors = new string[]
                {
                    "button[type='button']",
                    "div[role='button']",
                    "a[role='button']"
                };

                IWebElement acceptBtn = null;
                var buttons = driver.FindElements(By.CssSelector("button, div[role='button']"));
                foreach (var b in buttons)
                {
                    try
                    {
                        if (!b.Displayed) continue;
                        string txt = b.Text.Trim();
                        if (txt.IndexOf("Chấp nhận lời mời", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            txt.IndexOf("Accept Invite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            txt.IndexOf("Join", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            acceptBtn = b;
                            break;
                        }
                    }
                    catch { }
                }

                if (acceptBtn != null)
                {
                    DoLog(log, string.Format("[*] Đã tìm thấy nút: \"{0}\" -> Đang bấm tham gia...", acceptBtn.Text));
                    try { acceptBtn.Click(); }
                    catch { js.ExecuteScript("arguments[0].click();", acceptBtn); }

                    Sleep(4000, 6000);
                    DoLog(log, "[+] ĐÃ THAM GIA SERVER DISCORD THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    DoLog(log, "[i] Không tìm thấy nút Accept Invite (có thể tài khoản đã ở trong server)");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi tham gia server Discord: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Tự động click Xác minh (Verify) hoặc Đồng ý nội quy server
        /// </summary>
        public bool AutoVerify(IWebDriver driver, Action<string> log = null)
        {
            try
            {
                DoLog(log, "[*] Đang tìm kiếm nút Verify / Hoàn thành nội quy...");
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                // 1. Kiểm tra thanh "Bạn phải hoàn thành một vài bước nữa trước khi có thể nói chuyện"
                var rulesNotice = driver.FindElements(By.CssSelector("button, div[role='button']"));
                foreach (var b in rulesNotice)
                {
                    try
                    {
                        if (!b.Displayed) continue;
                        string txt = b.Text;
                        if (txt.IndexOf("Hoàn thành", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            txt.IndexOf("Complete", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            DoLog(log, "[*] Đang bấm nút [Hoàn thành] nội quy...");
                            try { b.Click(); } catch { js.ExecuteScript("arguments[0].click();", b); }
                            Sleep(2000, 3000);

                            // Tick chọn checkbox "Tôi đã đọc và đồng ý với nội quy"
                            var chkRules = driver.FindElements(By.CssSelector("input[type='checkbox'], div[role='checkbox']"));
                            if (chkRules.Count > 0 && chkRules[0].Displayed)
                            {
                                try { chkRules[0].Click(); } catch { js.ExecuteScript("arguments[0].click();", chkRules[0]); }
                                Sleep(1000, 1500);
                            }

                            // Bấm nút Gửi / Submit
                            var submitBtns = driver.FindElements(By.CssSelector("button[type='submit'], div[role='dialog'] button"));
                            foreach (var sb in submitBtns)
                            {
                                if (sb.Displayed && (sb.Text.Contains("Gửi") || sb.Text.Contains("Submit") || sb.Text.Contains("Xong")))
                                {
                                    try { sb.Click(); } catch { js.ExecuteScript("arguments[0].click();", sb); }
                                    Sleep(2000, 3000);
                                    DoLog(log, "[+] Đã xác nhận nội quy server thành công!");
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    catch { }
                }

                // 2. Tìm các nút Verify hoặc reaction emoji trong kênh #verify
                var verifyBtns = driver.FindElements(By.CssSelector("button, div[role='button']"));
                foreach (var vb in verifyBtns)
                {
                    try
                    {
                        if (!vb.Displayed) continue;
                        string txt = vb.Text;
                        if (txt.IndexOf("Verify", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            txt.IndexOf("Xác minh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            txt.IndexOf("Click here", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            DoLog(log, string.Format("[*] Đang bấm nút xác thực: \"{0}\"...", txt));
                            try { vb.Click(); } catch { js.ExecuteScript("arguments[0].click();", vb); }
                            Sleep(2000, 3500);
                            DoLog(log, "[+] Đã bấm nút Verify!");
                            return true;
                        }
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi thực hiện Verify: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Gửi tin nhắn Spintax vào kênh chat hiện tại (cày cấp XP, seeding)
        /// </summary>
        public bool SendChatMessage(IWebDriver driver, string messageSpintax, Action<string> log = null)
        {
            try
            {
                string textToSend = SpintaxHelper.Process(messageSpintax);
                DoLog(log, string.Format("[*] Chuẩn bị gửi tin nhắn: \"{0}\"", textToSend));

                var chatBoxes = driver.FindElements(By.CssSelector("div[role='textbox'], div[data-slate-editor='true'], div[aria-label*='tin nhắn'], div[aria-label*='Message']"));
                IWebElement targetBox = null;
                foreach (var box in chatBoxes)
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
                    HumanType(targetBox, textToSend);
                    Sleep(800, 1500);
                    targetBox.SendKeys(Keys.Enter);
                    Sleep(2000, 3000);
                    DoLog(log, "[+] ĐÃ GỬI TIN NHẮN DISCORD THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    DoLog(log, "[-] Không tìm thấy ô nhập tin nhắn trong kênh hiện tại");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi khi gửi tin nhắn Discord: {0}", ex.Message));
                return false;
            }
        }
    }
}
