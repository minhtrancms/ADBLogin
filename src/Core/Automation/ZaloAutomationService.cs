using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;

namespace ADBLogin.Core.Automation
{
    public class ZaloSendResult
    {
        public string Phone { get; set; }
        public bool Success { get; set; }
        public string DisplayName { get; set; }
        public string Message { get; set; }
        public bool FriendRequestSent { get; set; }
        public string ErrorNote { get; set; }
    }

    /// <summary>
    /// Service tự động hóa tương tác Zalo Web (https://chat.zalo.me):
    /// - Mở và kiểm tra trạng thái đăng nhập Zalo Web
    /// - Tìm kiếm người dùng theo Số Điện Thoại (SĐT)
    /// - Soạn thảo và gửi tin nhắn (Hỗ trợ Spintax nội dung phong phú)
    /// - Tự động gửi lời mời kết bạn kèm lời chào
    /// - Điều hướng và mở khung trò chuyện mượt mà
    /// </summary>
    public class ZaloAutomationService
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

        public bool OpenZaloWeb(IWebDriver driver, Action<string> log = null)
        {
            try
            {
                DoLog(log, "[*] Đang điều hướng đến Zalo Web (https://chat.zalo.me)...");
                driver.Navigate().GoToUrl("https://chat.zalo.me/");
                Sleep(3500, 5000);
                return CheckIsLoggedIn(driver);
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi mở Zalo Web: {0}", ex.Message));
                return false;
            }
        }

        public bool CheckIsLoggedIn(IWebDriver driver)
        {
            try
            {
                // Khi đã đăng nhập, Zalo Web sẽ có ô tìm kiếm #contact-search-input hoặc thanh menu tabs
                var search = driver.FindElements(By.CssSelector("#contact-search-input, input[placeholder*='Tìm kiếm'], [data-id='contact-search-input'], .nav__tabs, .main-tab__list, .conv-item"));
                if (search.Count > 0) return true;

                string page = driver.PageSource;
                if (page.Contains("contact-search-input") || page.Contains("nav__tabs") || page.Contains("chat-box"))
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool SearchAndOpenContact(IWebDriver driver, string phoneNumber, Action<string> log = null)
        {
            try
            {
                string phone = NormalizePhone(phoneNumber);
                DoLog(log, string.Format("[*] Đang tìm kiếm SĐT: {0}...", phone));

                // 1. Tìm ô tìm kiếm
                IWebElement searchInput = null;
                string[] searchSelectors = new string[]
                {
                    "#contact-search-input",
                    "[data-id='contact-search-input']",
                    "input[placeholder*='Tìm kiếm']",
                    "input.input-search",
                    "//input[contains(@placeholder, 'Tìm kiếm')]"
                };

                foreach (var sel in searchSelectors)
                {
                    try
                    {
                        var elements = sel.StartsWith("//") ? driver.FindElements(By.XPath(sel)) : driver.FindElements(By.CssSelector(sel));
                        if (elements.Count > 0 && elements[0].Displayed)
                        {
                            searchInput = elements[0];
                            break;
                        }
                    }
                    catch { }
                }

                if (searchInput == null)
                {
                    DoLog(log, "[-] Không tìm thấy ô tìm kiếm Zalo. Có thể phiên Zalo Web chưa đăng nhập!");
                    return false;
                }

                // Focus và xóa nội dung cũ
                searchInput.Click();
                Sleep(300, 500);

                try
                {
                    searchInput.SendKeys(Keys.Control + "a");
                    searchInput.SendKeys(Keys.Backspace);
                }
                catch
                {
                    IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                    js.ExecuteScript("arguments[0].value = '';", searchInput);
                }
                Sleep(300, 500);

                // Nhập số điện thoại
                searchInput.SendKeys(phone);
                Sleep(1800, 2600);

                // Nhấn Enter để Zalo kích hoạt tìm kiếm và mở trực tiếp cuộc hội thoại
                searchInput.SendKeys(Keys.Enter);
                Sleep(2000, 3000);

                // Kiểm tra xem Zalo có báo không tìm thấy kết quả hoặc số chưa đăng ký không
                string page = driver.PageSource;
                if (page.Contains("Không tìm thấy kết quả") ||
                    page.Contains("Số điện thoại chưa đăng ký") ||
                    page.Contains("không cho phép tìm kiếm qua Zalo") ||
                    page.Contains("Không tìm thấy người dùng này"))
                {
                    DoLog(log, string.Format("[-] SĐT {0} không tồn tại hoặc chưa đăng ký / chặn tìm kiếm Zalo.", phone));
                    return false;
                }

                // Nếu nhấn Enter chưa mở, thử click vào item kết quả tìm kiếm đầu tiên
                try
                {
                    string[] itemSelectors = new string[]
                    {
                        "[data-id='div_SearchResultItem']",
                        ".search-list-item",
                        ".conv-item",
                        "div[data-id*='chat-item']",
                        "div[class*='search-item']",
                        "div[class*='conv-item']"
                    };

                    foreach (var isel in itemSelectors)
                    {
                        var list = driver.FindElements(By.CssSelector(isel));
                        if (list.Count > 0 && list[0].Displayed)
                        {
                            list[0].Click();
                            Sleep(1500, 2500);
                            break;
                        }
                    }
                }
                catch { }

                // Kiểm tra xem khung chat đã mở ra chưa
                bool chatOpened = CheckIsChatEditorReady(driver);
                if (chatOpened)
                {
                    DoLog(log, string.Format("[+] Đã mở thành công khung chat với SĐT: {0}", phone));
                    return true;
                }

                // Thử thêm 1 lần bấm Enter nữa
                try
                {
                    searchInput.SendKeys(Keys.Enter);
                    Sleep(2000, 2500);
                    chatOpened = CheckIsChatEditorReady(driver);
                }
                catch { }

                if (chatOpened)
                {
                    DoLog(log, string.Format("[+] Đã mở khung chat với SĐT: {0}", phone));
                    return true;
                }

                DoLog(log, string.Format("[-] Không thể mở khung chat với SĐT {0}.", phone));
                return false;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi tìm kiếm SĐT: {0}", ex.Message));
                return false;
            }
        }

        public bool CheckIsChatEditorReady(IWebDriver driver)
        {
            try
            {
                var editors = driver.FindElements(By.CssSelector("#chat-input-editor, [data-id='chat-input-editor'], div[contenteditable='true'], .rich-input__text"));
                foreach (var ed in editors)
                {
                    if (ed.Displayed) return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool SendChatMessage(IWebDriver driver, string message, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message)) return true;

                DoLog(log, "[*] Đang soạn thảo tin nhắn vào khung chat Zalo...");

                IWebElement editor = null;
                string[] editorSelectors = new string[]
                {
                    "#chat-input-editor",
                    "[data-id='chat-input-editor']",
                    "div[contenteditable='true']",
                    "div.rich-input__text"
                };

                foreach (var sel in editorSelectors)
                {
                    try
                    {
                        var list = driver.FindElements(By.CssSelector(sel));
                        foreach (var el in list)
                        {
                            if (el.Displayed)
                            {
                                editor = el;
                                break;
                            }
                        }
                        if (editor != null) break;
                    }
                    catch { }
                }

                if (editor == null)
                {
                    DoLog(log, "[-] Không tìm thấy ô soạn thảo tin nhắn Zalo!");
                    return false;
                }

                editor.Click();
                Sleep(400, 600);

                // Xử lý gửi tin qua JavaScript execCommand để chuẩn xác 100% với rich-text editor của Zalo
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                try
                {
                    js.ExecuteScript(
                        "arguments[0].focus(); " +
                        "document.execCommand('selectAll', false, null); " +
                        "document.execCommand('insertText', false, arguments[1]);",
                        editor, message);
                }
                catch
                {
                    editor.SendKeys(message);
                }

                Sleep(800, 1200);

                // Gửi tin nhắn bằng phím Enter
                editor.SendKeys(Keys.Enter);
                Sleep(1200, 1800);

                // Thử bấm thêm nút Gửi nếu Enter chưa kích hoạt
                try
                {
                    var sendBtns = driver.FindElements(By.CssSelector("[data-id='btn-send'], .btn-send, div[title*='Gửi (Enter)'], div[title*='Gửi']"));
                    foreach (var sBtn in sendBtns)
                    {
                        if (sBtn.Displayed)
                        {
                            sBtn.Click();
                            Sleep(1000, 1500);
                            break;
                        }
                    }
                }
                catch { }

                DoLog(log, "[✓] ĐÃ GỬI TIN NHẮN ZALO THÀNH CÔNG!");
                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi gửi tin nhắn: {0}", ex.Message));
                return false;
            }
        }

        public bool SendFriendRequest(IWebDriver driver, string greetingMessage = "", Action<string> log = null)
        {
            try
            {
                DoLog(log, "[*] Đang kiểm tra nút Kết bạn...");

                IWebElement addFriendBtn = null;
                string[] btnSelectors = new string[]
                {
                    "[data-id='btn-add-friend']",
                    "div[data-id*='btn-add-friend']",
                    "[data-translate-inner='STR_ADD_FRIEND']",
                    "//div[contains(text(), 'Kết bạn') and not(contains(@class, 'disabled'))]",
                    "//button[contains(., 'Kết bạn')]"
                };

                foreach (var sel in btnSelectors)
                {
                    try
                    {
                        var list = sel.StartsWith("//") ? driver.FindElements(By.XPath(sel)) : driver.FindElements(By.CssSelector(sel));
                        foreach (var el in list)
                        {
                            if (el.Displayed)
                            {
                                addFriendBtn = el;
                                break;
                            }
                        }
                        if (addFriendBtn != null) break;
                    }
                    catch { }
                }

                if (addFriendBtn == null)
                {
                    DoLog(log, "[i] Không tìm thấy nút Kết bạn (Có thể đã là bạn bè hoặc người này không nhận kết bạn).");
                    return false;
                }

                addFriendBtn.Click();
                Sleep(1500, 2500);

                // Kiểm tra popup nhập lời chào kết bạn
                if (!string.IsNullOrEmpty(greetingMessage))
                {
                    try
                    {
                        var textAreas = driver.FindElements(By.CssSelector("textarea, input[data-id*='greeting'], input[placeholder*='Xin chào']"));
                        if (textAreas.Count > 0 && textAreas[0].Displayed)
                        {
                            textAreas[0].Click();
                            textAreas[0].SendKeys(Keys.Control + "a");
                            textAreas[0].SendKeys(greetingMessage);
                            Sleep(500, 800);
                        }
                    }
                    catch { }
                }

                // Bấm nút xác nhận "Gửi yêu cầu" / "Kết bạn" trong modal popup
                string[] confirmSelectors = new string[]
                {
                    "//button[contains(., 'Gửi yêu cầu')]",
                    "//button[contains(., 'Kết bạn')]",
                    "//div[contains(., 'Gửi yêu cầu') and contains(@class, 'btn')]",
                    ".btn-primary"
                };

                foreach (var cSel in confirmSelectors)
                {
                    try
                    {
                        var cList = cSel.StartsWith("//") ? driver.FindElements(By.XPath(cSel)) : driver.FindElements(By.CssSelector(cSel));
                        foreach (var btn in cList)
                        {
                            if (btn.Displayed)
                            {
                                btn.Click();
                                Sleep(1200, 1800);
                                DoLog(log, "[✓] ĐÃ GỬI LỜI MỜI KẾT BẠN ZALO!");
                                return true;
                            }
                        }
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi gửi kết bạn: {0}", ex.Message));
                return false;
            }
        }

        public ZaloSendResult ExecuteSendToPhone(IWebDriver driver, string phone, string messageTemplate, bool sendFriendReq, string friendGreeting, Action<string> log = null)
        {
            var res = new ZaloSendResult { Phone = phone };

            try
            {
                // Kiểm tra đăng nhập
                if (!CheckIsLoggedIn(driver))
                {
                    bool opened = OpenZaloWeb(driver, log);
                    if (!opened)
                    {
                        res.Success = false;
                        res.ErrorNote = "Zalo Web chưa đăng nhập (Cần quét mã QR)";
                        return res;
                    }
                }

                // Tìm và mở chat
                bool found = SearchAndOpenContact(driver, phone, log);
                if (!found)
                {
                    res.Success = false;
                    res.ErrorNote = "Không tìm thấy SĐT hoặc chưa đăng ký Zalo";
                    return res;
                }

                // Gửi tin nhắn Spintax
                if (!string.IsNullOrWhiteSpace(messageTemplate))
                {
                    string withPhone = (messageTemplate ?? "").Replace("{phone}", phone);
                    string spunMessage = SpintaxHelper.Spin(withPhone);
                    res.Message = spunMessage;

                    bool sent = SendChatMessage(driver, spunMessage, log);
                    res.Success = sent;
                    if (!sent)
                    {
                        res.ErrorNote = "Lỗi khi nhập hoặc gửi tin nhắn vào khung chat";
                    }
                }
                else
                {
                    res.Success = true;
                }

                // Gửi lời mời kết bạn nếu được cấu hình
                if (sendFriendReq)
                {
                    string spunGreeting = SpintaxHelper.Spin(friendGreeting ?? "");
                    bool frSent = SendFriendRequest(driver, spunGreeting, log);
                    res.FriendRequestSent = frSent;
                }

                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.ErrorNote = ex.Message;
                return res;
            }
        }

        public static string NormalizePhone(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string clean = Regex.Replace(raw, @"[^\d\+]", "");
            if (clean.StartsWith("+84"))
            {
                clean = "0" + clean.Substring(3);
            }
            else if (clean.StartsWith("84") && clean.Length > 9)
            {
                clean = "0" + clean.Substring(2);
            }
            return clean.Trim();
        }
    }
}
