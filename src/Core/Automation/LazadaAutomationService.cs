using System;
using System.Collections.Generic;
using System.Threading;
using OpenQA.Selenium;

namespace ADBLogin.Core.Automation
{
    /// <summary>
    /// Service tự động hóa các thao tác trên sàn Thương mại điện tử Lazada:
    /// - Điểm danh nhận LazCoins hàng ngày
    /// - Tự động thu thập / Lưu toàn bộ mã giảm giá Voucher Lazada
    /// - Seeding tìm kiếm từ khóa sản phẩm & Thêm vào giỏ hàng
    /// - Đăng nhập bằng Cookie & Xuất Cookie Lazada
    /// </summary>
    public class LazadaAutomationService
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

        #region 1. Đăng nhập & Quản lý Cookie Lazada

        public bool LoginWithCookie(IWebDriver driver, string cookieString, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cookieString))
                {
                    DoLog(log, "[-] Chuỗi Cookie Lazada rỗng");
                    return false;
                }

                DoLog(log, "[*] Điều hướng tới Lazada để nạp Cookie...");
                driver.Navigate().GoToUrl("https://www.lazada.vn/");
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
                                driver.Manage().Cookies.AddCookie(new Cookie(name, value, ".lazada.vn", "/", DateTime.Now.AddYears(1)));
                                count++;
                            }
                            catch { }
                        }
                    }
                }

                DoLog(log, string.Format("[+] Đã nạp {0} cookie vào Lazada. Đang làm mới trang...", count));
                driver.Navigate().Refresh();
                Sleep(3000, 5000);

                if (IsLoggedIn(driver))
                {
                    DoLog(log, "[✓] ĐĂNG NHẬP LAZADA QUA COOKIE THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    DoLog(log, "[!] Cookie Lazada có thể đã hết hạn.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi nạp Cookie Lazada: {0}", ex.Message));
                return false;
            }
        }

        public string ExportCookies(IWebDriver driver)
        {
            try
            {
                var cookies = driver.Manage().Cookies.AllCookies;
                var list = new List<string>();
                foreach (var c in cookies)
                {
                    list.Add(string.Format("{0}={1}", c.Name, c.Value));
                }
                return string.Join("; ", list.ToArray());
            }
            catch
            {
                return "";
            }
        }

        public bool IsLoggedIn(IWebDriver driver)
        {
            try
            {
                string src = driver.PageSource;
                if (src.Contains("Đăng nhập") && src.Contains("Đăng ký") && !src.Contains("Tài khoản của tôi"))
                {
                    return false;
                }
                return src.Contains("my-account") || src.Contains("Tài khoản") || src.Contains("lzd-act-user");
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 2. Điểm danh nhận LazCoins hàng ngày

        public bool CheckinLazCoins(IWebDriver driver, Action<string> log = null)
        {
            try
            {
                DoLog(log, "[*] Đang mở trang LazCoins (Xu thưởng Lazada)...");
                driver.Navigate().GoToUrl("https://pages.lazada.vn/wow/gcp/lazada/channel/vn/lazcoins/coins-center");
                Sleep(3500, 5000);

                IWebElement checkinBtn = null;
                string[] btnSelectors = new string[]
                {
                    "//button[contains(., 'Điểm danh')]",
                    "//div[contains(., 'Điểm danh') and @role='button']",
                    "//button[contains(., 'Nhận ngay')]",
                    "//button[contains(., 'Check in')]",
                    "//div[contains(@class, 'checkin') or contains(@class, 'check-in')]",
                    "//span[contains(., 'Điểm danh ngay')]"
                };

                foreach (var sel in btnSelectors)
                {
                    try
                    {
                        var els = driver.FindElements(By.XPath(sel));
                        foreach (var el in els)
                        {
                            if (el.Displayed)
                            {
                                checkinBtn = el;
                                break;
                            }
                        }
                        if (checkinBtn != null) break;
                    }
                    catch { }
                }

                if (checkinBtn != null)
                {
                    try { checkinBtn.Click(); }
                    catch
                    {
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", checkinBtn);
                    }
                    Sleep(2000, 3000);
                    DoLog(log, "[✓] ĐÃ BẤM ĐIỂM DANH NHẬN LAZCOINS THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    if (driver.PageSource.Contains("Đã điểm danh") || driver.PageSource.Contains("Đã nhận"))
                    {
                        DoLog(log, "[+] Hôm nay tài khoản đã điểm danh LazCoins từ trước!");
                        return true;
                    }
                    DoLog(log, "[!] Chưa bấm được nút điểm danh (Có thể chưa đăng nhập hoặc đã nhận).");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi điểm danh LazCoins: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 3. Tự động thu thập / Lưu toàn bộ Voucher Lazada

        public int CollectVouchers(IWebDriver driver, Action<string> log = null)
        {
            try
            {
                DoLog(log, "[*] Đang mở trang Mã Giảm Giá (Lazada Voucher Center)...");
                driver.Navigate().GoToUrl("https://www.lazada.vn/ma-giam-gia/");
                Sleep(3500, 5000);

                int collectedCount = 0;
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                for (int round = 1; round <= 4; round++)
                {
                    var buttons = driver.FindElements(By.XPath("//button[contains(., 'Thu thập') or contains(., 'Lưu mã') or contains(., 'Collect')] | //div[@role='button' and contains(., 'Thu thập')]"));
                    DoLog(log, string.Format("[*] Vòng {0}: Tìm thấy {1} nút thu thập voucher...", round, buttons.Count));

                    foreach (var btn in buttons)
                    {
                        try
                        {
                            if (!btn.Displayed) continue;
                            string txt = btn.Text.Trim();
                            if (txt.Contains("Thu thập") || txt.Contains("Collect") || txt.Contains("Lưu"))
                            {
                                try { btn.Click(); }
                                catch { js.ExecuteScript("arguments[0].click();", btn); }

                                collectedCount++;
                                DoLog(log, string.Format("[+] Đã lưu mã giảm giá #{0}", collectedCount));
                                Sleep(700, 1400);
                            }
                        }
                        catch { }
                    }

                    // Cuộn trang để tải thêm voucher bên dưới
                    SmoothScroll(driver, 800);
                    Sleep(2000, 3000);
                }

                DoLog(log, string.Format("[✓] HOÀN TẤT THU THẬP VOUCHER! Tổng số mã đã bấm lưu: {0}", collectedCount));
                return collectedCount;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi thu thập Voucher: {0}", ex.Message));
                return 0;
            }
        }

        #endregion

        #region 4. Seeding Tìm kiếm & Thêm vào giỏ hàng

        public bool SearchAndAddToCart(IWebDriver driver, string keyword, int scrollSec, bool addToCart, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    DoLog(log, "[-] Từ khóa tìm kiếm Lazada không được rỗng!");
                    return false;
                }

                DoLog(log, string.Format("[*] Đang tìm kiếm sản phẩm: \"{0}\" trên Lazada...", keyword));
                driver.Navigate().GoToUrl("https://www.lazada.vn/");
                Sleep(2500, 4000);

                IWebElement searchBox = null;
                try
                {
                    searchBox = driver.FindElement(By.CssSelector("input#q, input[type='search'], input[class*='search']"));
                }
                catch { }

                if (searchBox != null)
                {
                    HumanType(searchBox, keyword);
                    Sleep(500, 1000);
                    searchBox.SendKeys(Keys.Enter);
                }
                else
                {
                    string searchUrl = "https://www.lazada.vn/catalog/?q=" + Uri.EscapeDataString(keyword);
                    driver.Navigate().GoToUrl(searchUrl);
                }
                Sleep(3500, 5000);

                DoLog(log, string.Format("[*] Đang lướt xem kết quả tìm kiếm trong {0} giây...", scrollSec));
                DateTime startTime = DateTime.Now;
                while ((DateTime.Now - startTime).TotalSeconds < scrollSec)
                {
                    SmoothScroll(driver, _rnd.Next(300, 600));
                    Sleep(2000, 3500);
                }

                // Click vào 1 sản phẩm đầu trang
                var productLinks = driver.FindElements(By.CssSelector("div[data-qa-locator='product-item'] a, div[class*='productCard'] a, div[class*='item-card'] a"));
                if (productLinks.Count > 0)
                {
                    IWebElement targetProd = productLinks[_rnd.Next(Math.Min(productLinks.Count, 3))];
                    DoLog(log, "[*] Đang bấm xem chi tiết sản phẩm...");
                    try { targetProd.Click(); }
                    catch { ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", targetProd); }
                    Sleep(4000, 6000);

                    // Lướt xem chi tiết, đánh giá
                    SmoothScroll(driver, 500);
                    Sleep(2500, 4000);

                    if (addToCart)
                    {
                        DoLog(log, "[*] Đang bấm nút 'Thêm vào giỏ hàng'...");
                        try
                        {
                            var addBtns = driver.FindElements(By.XPath("//button[contains(., 'Thêm vào giỏ hàng') or contains(., 'Add to Cart')]"));
                            if (addBtns.Count > 0)
                            {
                                try { addBtns[0].Click(); }
                                catch { ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", addBtns[0]); }
                                Sleep(2000, 3000);
                                DoLog(log, "[✓] ĐÃ THÊM SẢN PHẨM VÀO GIỎ HÀNG THÀNH CÔNG!");
                            }
                        }
                        catch (Exception ex)
                        {
                            DoLog(log, string.Format("[-] Không thể thêm giỏ hàng: {0}", ex.Message));
                        }
                    }

                    return true;
                }
                else
                {
                    DoLog(log, "[!] Không tìm thấy thẻ sản phẩm nào trong kết quả tìm kiếm.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi seeding Lazada: {0}", ex.Message));
                return false;
            }
        }

        #endregion
    }
}
