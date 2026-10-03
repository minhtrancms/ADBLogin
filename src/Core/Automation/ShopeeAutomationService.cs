using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;

namespace ADBLogin.Core.Automation
{
    /// <summary>
    /// Service tự động hóa các thao tác trên sàn Thương mại điện tử Shopee:
    /// - Điểm danh nhận Shopee Xu hàng ngày
    /// - Lướt Shopee Video nhận xu & quà
    /// - Tự động thu thập / lưu toàn bộ Voucher giảm giá vào ví
    /// - Seeding tìm kiếm sản phẩm, kéo traffic và thứ hạng SEO cho gian hàng
    /// - Đăng nhập Cookie (SPC_EC) & Xuất Cookie
    /// </summary>
    public class ShopeeAutomationService
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

        #region 1. Đăng nhập Cookie & Xuất Cookie

        public bool LoginWithCookie(IWebDriver driver, string cookieString, Action<string> log = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cookieString))
                {
                    DoLog(log, "[-] Chuỗi Cookie Shopee rỗng");
                    return false;
                }

                DoLog(log, "[*] Điều hướng đến Shopee để nạp Cookie...");
                driver.Navigate().GoToUrl("https://shopee.vn/");
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
                                driver.Manage().Cookies.AddCookie(new Cookie(name, value, ".shopee.vn", "/", DateTime.Now.AddYears(1)));
                                count++;
                            }
                            catch { }
                        }
                    }
                }

                DoLog(log, string.Format("[*] Đã nạp {0} cookie. Đang làm mới Shopee...", count));
                driver.Navigate().Refresh();
                Sleep(3500, 5000);

                string page = driver.PageSource;
                if (page.Contains("navbar__username") || page.Contains("shopee-avatar") || page.Contains("user-profile"))
                {
                    DoLog(log, "[✓] ĐĂNG NHẬP SHOPEE THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    DoLog(log, "[!] Đã nạp Cookie Shopee (Trang đã làm mới).");
                    return true;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi đăng nhập Shopee qua Cookie: {0}", ex.Message));
                return false;
            }
        }

        public string ExtractShopeeCookies(IWebDriver driver, Action<string> log = null)
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
                DoLog(log, string.Format("[✓] Trích xuất thành công {0} cookies Shopee!", cookies.Count));
                return result;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi xuất cookie Shopee: {0}", ex.Message));
                return string.Empty;
            }
        }

        #endregion

        #region 2. Điểm danh nhận Shopee Xu

        public bool CheckInCoins(IWebDriver driver, Action<string> log = null)
        {
            try
            {
                DoLog(log, "[*] Đang mở trang Shopee Xu (Shopee Coins)...");
                driver.Navigate().GoToUrl("https://shopee.vn/shopee-coins");
                Sleep(3500, 5000);

                // Tìm nút "Nhận xu ngay" hoặc "Điểm danh"
                IWebElement checkinBtn = null;
                string[] btnSelectors = new string[]
                {
                    "button[class*='check-in']",
                    "button[class*='checkin']",
                    "div[class*='check-in-btn']",
                    "button:has-text('Nhận xu')",
                    "button:has-text('Điểm danh')",
                    "//button[contains(., 'Nhận xu')]",
                    "//div[contains(., 'Nhận ngay')]"
                };

                foreach (var sel in btnSelectors)
                {
                    try
                    {
                        var el = sel.StartsWith("//") ? driver.FindElement(By.XPath(sel)) : driver.FindElement(By.CssSelector(sel));
                        if (el != null && el.Displayed)
                        {
                            checkinBtn = el;
                            break;
                        }
                    }
                    catch { }
                }

                if (checkinBtn != null)
                {
                    checkinBtn.Click();
                    Sleep(2000, 3000);
                    DoLog(log, "[✓] ĐÃ BẤM ĐIỂM DANH NHẬN SHOPEE XU THÀNH CÔNG!");
                    return true;
                }
                else
                {
                    if (driver.PageSource.Contains("Đã nhận") || driver.PageSource.Contains("đã điểm danh"))
                    {
                        DoLog(log, "[+] Hôm nay tài khoản đã điểm danh nhận xu từ trước!");
                        return true;
                    }
                    DoLog(log, "[!] Chưa bấm được nút điểm danh (Có thể chưa đăng nhập hoặc hôm nay đã nhận).");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi điểm danh Shopee: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 3. Lướt Shopee Video nhận thưởng

        public bool SurfShopeeVideo(IWebDriver driver, int videoCount = 8, int minWatchSec = 6, int maxWatchSec = 14, Action<string> log = null)
        {
            try
            {
                DoLog(log, string.Format("[*] Bắt đầu lướt Shopee Video ({0} video)...", videoCount));
                driver.Navigate().GoToUrl("https://shopee.vn/universal-link/video");
                Sleep(3500, 5000);

                for (int i = 1; i <= videoCount; i++)
                {
                    int watchSec = _rnd.Next(minWatchSec, maxWatchSec + 1);
                    DoLog(log, string.Format("[*] Video #{0}/{1}: Đang xem trong {2} giây...", i, videoCount, watchSec));
                    Thread.Sleep(watchSec * 1000);

                    // Gửi phím mũi tên xuống hoặc cuộn chuột sang video tiếp theo
                    try
                    {
                        driver.FindElement(By.TagName("body")).SendKeys(Keys.ArrowDown);
                    }
                    catch
                    {
                        SmoothScroll(driver, 600);
                    }
                    Sleep(1500, 2500);
                }

                DoLog(log, string.Format("[✓] HOÀN TẤT LƯỢT XEM SHOPEE VIDEO ({0} video)!", videoCount));
                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi lướt Shopee Video: {0}", ex.Message));
                return false;
            }
        }

        #endregion

        #region 4. Tự động Lưu Toàn Bộ Voucher

        public int CollectVouchers(IWebDriver driver, Action<string> log = null)
        {
            int collected = 0;
            try
            {
                DoLog(log, "[*] Mở trang Mã Giảm Giá Shopee (Voucher Wallet)...");
                driver.Navigate().GoToUrl("https://shopee.vn/m/ma-giam-gia");
                Sleep(4000, 6000);

                // Cuộn trang để tải thêm voucher
                for (int i = 0; i < 4; i++)
                {
                    SmoothScroll(driver, 500);
                    Sleep(1000, 1500);
                }

                // Tìm toàn bộ các nút "Lưu" hoặc "Lưu mã"
                var saveButtons = driver.FindElements(By.CssSelector("button[class*='voucher'], button[aria-label*='Lưu'], button:has-text('Lưu')"));
                if (saveButtons.Count == 0)
                {
                    saveButtons = driver.FindElements(By.XPath("//button[text()='Lưu' or contains(text(), 'Lưu mã')]"));
                }

                DoLog(log, string.Format("[*] Tìm thấy {0} nút lưu Voucher trên trang. Bắt đầu thu thập...", saveButtons.Count));

                foreach (var btn in saveButtons)
                {
                    try
                    {
                        if (btn.Displayed && btn.Enabled)
                        {
                            btn.Click();
                            collected++;
                            Sleep(400, 900);
                        }
                    }
                    catch { }
                }

                DoLog(log, string.Format("[✓] ĐÃ LƯU THÀNH CÔNG {0} VOUCHER VÀO VÍ SHOPEE!", collected));
                return collected;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi thu thập voucher Shopee: {0}", ex.Message));
                return collected;
            }
        }

        #endregion

        #region 5. Seeding Tìm kiếm Sản phẩm & Shop

        public bool SearchAndSeedProduct(IWebDriver driver, string keywordSpintax, string shopOrProductKeyword, int staySeconds = 30, bool addToCart = false, Action<string> log = null)
        {
            try
            {
                string keyword = SpintaxHelper.Spin(keywordSpintax);
                DoLog(log, string.Format("[*] Điều hướng Shopee tìm từ khóa: \"{0}\"", keyword));
                driver.Navigate().GoToUrl("https://shopee.vn/");
                Sleep(2500, 3500);

                // Đóng popup quảng cáo Shopee nếu có
                try
                {
                    var closePopup = driver.FindElement(By.CssSelector("div[class*='shopee-popup__close-btn'], div[class*='close-btn']"));
                    if (closePopup != null && closePopup.Displayed) closePopup.Click();
                }
                catch { }

                // Tìm ô tìm kiếm
                IWebElement searchBox = null;
                string[] searchSelectors = new string[] { "input.shopee-searchbar-input__input", "input[type='search']", "input[placeholder*='Shopee']" };
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
                    DoLog(log, "[-] Không tìm thấy thanh tìm kiếm Shopee");
                    return false;
                }

                HumanType(searchBox, keyword);
                Sleep(500, 1000);
                searchBox.SendKeys(Keys.Enter);
                Sleep(3500, 5000);

                // Cuộn trang kết quả tìm kiếm
                DoLog(log, "[*] Đã tải kết quả tìm kiếm. Đang cuộn trang xem sản phẩm...");
                for (int i = 0; i < 3; i++)
                {
                    SmoothScroll(driver, _rnd.Next(300, 600));
                    Sleep(1200, 2200);
                }

                // Nếu có từ khóa sản phẩm hoặc shop mục tiêu
                if (!string.IsNullOrWhiteSpace(shopOrProductKeyword))
                {
                    DoLog(log, string.Format("[*] Đang tìm kiếm sản phẩm đích chứa: \"{0}\"", shopOrProductKeyword));
                    var items = driver.FindElements(By.CssSelector("div[data-sqe='item'] a, a[href*='-i.']"));
                    bool clicked = false;

                    foreach (var item in items)
                    {
                        try
                        {
                            string text = item.Text;
                            string href = item.GetAttribute("href");
                            if ((!string.IsNullOrEmpty(text) && text.IndexOf(shopOrProductKeyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (!string.IsNullOrEmpty(href) && href.IndexOf(shopOrProductKeyword, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                DoLog(log, string.Format("[+] TÌM THẤY SẢN PHẨM MỤC TIÊU: {0} -> Đang bấm xem...", text.Substring(0, Math.Min(40, text.Length))));
                                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({behavior: 'smooth', block: 'center'});", item);
                                Sleep(1500, 2500);
                                item.Click();
                                clicked = true;
                                break;
                            }
                        }
                        catch { }
                    }

                    if (!clicked && items.Count > 0)
                    {
                        DoLog(log, "[!] Chưa thấy sản phẩm chính xác ở trang 1. Bấm sản phẩm đầu tiên để tạo tương tác...");
                        items[0].Click();
                    }

                    // Tương tác đọc trang sản phẩm (cuộn xem hình ảnh, xem đánh giá)
                    DoLog(log, string.Format("[*] Đang ở lại xem sản phẩm & đánh giá trong {0} giây...", staySeconds));
                    int elapsed = 0;
                    while (elapsed < staySeconds)
                    {
                        SmoothScroll(driver, _rnd.Next(250, 550));
                        int step = _rnd.Next(2, 5);
                        Thread.Sleep(step * 1000);
                        elapsed += step;
                    }

                    // Bấm Thêm Vào Giỏ Hàng nếu bật
                    if (addToCart)
                    {
                        try
                        {
                            var addCartBtn = driver.FindElement(By.CssSelector("button:has-text('Thêm Vào Giỏ Hàng'), button[class*='btn-tinted'], button[aria-label*='giỏ hàng']"));
                            if (addCartBtn != null && addCartBtn.Displayed)
                            {
                                addCartBtn.Click();
                                DoLog(log, "[✓] ĐÃ BẤM THÊM VÀO GIỎ HÀNG THÀNH CÔNG (Tăng chuyển đổi shop)!");
                                Sleep(1500, 2500);
                            }
                        }
                        catch { }
                    }

                    DoLog(log, "[✓] Hoàn thành lượt Seeding Shopee!");
                }
                else
                {
                    DoLog(log, "[✓] Hoàn thành tìm kiếm dạo Shopee tự nhiên!");
                }

                return true;
            }
            catch (Exception ex)
            {
                DoLog(log, string.Format("[-] Lỗi Seeding Shopee: {0}", ex.Message));
                return false;
            }
        }

        #endregion
    }
}
