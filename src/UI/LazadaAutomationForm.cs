using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADBLogin.Core.Automation;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;
using OpenQA.Selenium;

namespace ADBLogin.UI
{
    public class LazadaAutomationForm : Form
    {
        private CheckedListBox _chkListProfiles;
        private TextBox _txtSearch;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRunning;
        private Label _lblProfileCount;

        // Tab 1: LazCoins & Voucher
        private CheckBox _chkLazCoins;
        private CheckBox _chkCollectVouchers;

        // Tab 2: Seeding
        private TextBox _txtKeyword;
        private NumericUpDown _numScrollSec;
        private CheckBox _chkAddToCart;

        // Tab 3: Cookie
        private TextBox _txtCookieInput;
        private Button _btnLoginCookie;
        private Button _btnExportCookie;

        // Bottom Controls
        private NumericUpDown _numThreads;
        private Button _btnStart;
        private Button _btnStop;
        private RichTextBox _rtbLog;
        private Label _lblRunningStatus;
        private Button _btnClearLog;
        private Button _btnCopyLog;

        private SplitContainer _splitMain;
        private SplitContainer _splitRight;
        private TabControl _tabs;

        private readonly List<UserProfile> _allProfiles;
        private readonly HashSet<string> _selectedProfileIds = new HashSet<string>();
        private readonly LazadaAutomationService _lazadaService = new LazadaAutomationService();
        private readonly BrowserLauncherService _launcherService = new BrowserLauncherService();
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        public LazadaAutomationForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            LoadProfileList();
        }

        private void InitializeComponent()
        {
            this.Text = "🛍️ BỘ CÔNG CỤ TỰ ĐỘNG HÓA LAZADA (LAZADA AUTOMATION STUDIO)";
            this.Size = new Size(1180, 780);
            this.MinimumSize = new Size(1000, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            // ================= 1. HEADER BANNER =================
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(16, 8, 16, 8)
            };

            Label lblTitle = new Label
            {
                Text = "🛍️ LAZADA AUTOMATION & AFFILIATE GROWTH STUDIO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Tự động điểm danh LazCoins hàng ngày, săn toàn bộ Voucher giảm giá, seeding tìm kiếm & thêm giỏ hàng đa luồng",
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(16, 32)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // ================= 2. MAIN SPLIT CONTAINER =================
            _splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 330,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(229, 231, 235)
            };
            this.Controls.Add(_splitMain);
            _splitMain.BringToFront();

            // ================= LEFT: PROFILE SELECTOR =================
            Panel pnlLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            Label lblLeftHeader = new Label
            {
                Text = "📋 CHỌN PROFILE LAZADA",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Dock = DockStyle.Top,
                Height = 26
            };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.Gray,
                Text = "🔍 Tìm kiếm profile...",
                Height = 26
            };
            _txtSearch.GotFocus += delegate
            {
                if (_txtSearch.Text == "🔍 Tìm kiếm profile...")
                {
                    _txtSearch.Text = "";
                    _txtSearch.ForeColor = Color.Black;
                }
            };
            _txtSearch.LostFocus += delegate
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "🔍 Tìm kiếm profile...";
                    _txtSearch.ForeColor = Color.Gray;
                }
            };
            _txtSearch.TextChanged += delegate { LoadProfileList(); };

            FlowLayoutPanel pnlQuickSelect = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(0, 4, 0, 4),
                WrapContents = false
            };

            _btnSelectAll = new Button { Text = "Tất cả", Width = 65, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnDeselectAll = new Button { Text = "Bỏ chọn", Width = 65, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnSelectRunning = new Button { Text = "Đang chạy", Width = 80, Height = 25, FlatStyle = FlatStyle.Flat };

            _btnSelectAll.Click += delegate
            {
                for (int i = 0; i < _chkListProfiles.Items.Count; i++)
                {
                    _chkListProfiles.SetItemChecked(i, true);
                    var p = _chkListProfiles.Items[i] as ProfileDisplayItem;
                    if (p != null) _selectedProfileIds.Add(p.Profile.ProfileId);
                }
                UpdateProfileCount();
            };

            _btnDeselectAll.Click += delegate
            {
                for (int i = 0; i < _chkListProfiles.Items.Count; i++)
                {
                    _chkListProfiles.SetItemChecked(i, false);
                    var p = _chkListProfiles.Items[i] as ProfileDisplayItem;
                    if (p != null) _selectedProfileIds.Remove(p.Profile.ProfileId);
                }
                UpdateProfileCount();
            };

            _btnSelectRunning.Click += delegate
            {
                for (int i = 0; i < _chkListProfiles.Items.Count; i++)
                {
                    var item = _chkListProfiles.Items[i] as ProfileDisplayItem;
                    bool running = item != null && BrowserSessionManager.Instance.IsRunning(item.Profile.ProfileId);
                    _chkListProfiles.SetItemChecked(i, running);
                    if (item != null)
                    {
                        if (running) _selectedProfileIds.Add(item.Profile.ProfileId);
                        else _selectedProfileIds.Remove(item.Profile.ProfileId);
                    }
                }
                UpdateProfileCount();
            };

            pnlQuickSelect.Controls.Add(_btnSelectAll);
            pnlQuickSelect.Controls.Add(_btnDeselectAll);
            pnlQuickSelect.Controls.Add(_btnSelectRunning);

            _chkListProfiles = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F)
            };
            _chkListProfiles.ItemCheck += delegate(object s, ItemCheckEventArgs e)
            {
                this.BeginInvoke(new Action(delegate
                {
                    var p = _chkListProfiles.Items[e.Index] as ProfileDisplayItem;
                    if (p != null)
                    {
                        if (e.NewValue == CheckState.Checked) _selectedProfileIds.Add(p.Profile.ProfileId);
                        else _selectedProfileIds.Remove(p.Profile.ProfileId);
                    }
                    UpdateProfileCount();
                }));
            };

            _lblProfileCount = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 25,
                Text = "Đã chọn: 0 / 0 profiles",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(75, 85, 99)
            };

            pnlLeft.Controls.Add(_chkListProfiles);
            pnlLeft.Controls.Add(pnlQuickSelect);
            pnlLeft.Controls.Add(_txtSearch);
            pnlLeft.Controls.Add(lblLeftHeader);
            pnlLeft.Controls.Add(_lblProfileCount);
            _splitMain.Panel1.Controls.Add(pnlLeft);

            // ================= RIGHT: TABS & LOG SPLITTER =================
            _splitRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 370,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(229, 231, 235)
            };
            _splitMain.Panel2.Controls.Add(_splitRight);

            // TAB CONTROL
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };
            _splitRight.Panel1.Controls.Add(_tabs);

            CreateLazCoinsTab();
            CreateSeedingTab();
            CreateCookieTab();

            // LOG & ACTION PANEL (BOTTOM)
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            Panel pnlAction = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = Color.FromArgb(249, 250, 251),
                Padding = new Padding(8, 6, 8, 6)
            };

            Label lblThread = new Label { Text = "Số luồng:", AutoSize = true, Location = new Point(10, 12), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numThreads = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 2, Location = new Point(78, 10), Width = 55 };

            _btnStart = new Button
            {
                Text = "▶ BẮT ĐẦU CHẠY",
                Location = new Point(148, 6),
                Size = new Size(150, 32),
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnStart.FlatAppearance.BorderSize = 0;
            _btnStart.Click += delegate { StartAutomation(); };

            _btnStop = new Button
            {
                Text = "⏹ DỪNG",
                Location = new Point(306, 6),
                Size = new Size(95, 32),
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _btnStop.FlatAppearance.BorderSize = 0;
            _btnStop.Click += delegate { StopAutomation(); };

            _lblRunningStatus = new Label
            {
                Text = "Sẵn sàng",
                Location = new Point(415, 12),
                AutoSize = true,
                ForeColor = Color.FromArgb(107, 114, 128),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };

            _btnClearLog = new Button { Text = "Xóa log", Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(710, 8), Size = new Size(70, 26), FlatStyle = FlatStyle.Flat };
            _btnClearLog.Click += delegate { _rtbLog.Clear(); };

            _btnCopyLog = new Button { Text = "Copy log", Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(786, 8), Size = new Size(75, 26), FlatStyle = FlatStyle.Flat };
            _btnCopyLog.Click += delegate
            {
                if (!string.IsNullOrEmpty(_rtbLog.Text))
                {
                    Clipboard.SetText(_rtbLog.Text);
                    MessageBox.Show("Đã copy toàn bộ nhật ký vào clipboard!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            pnlAction.Controls.Add(lblThread);
            pnlAction.Controls.Add(_numThreads);
            pnlAction.Controls.Add(_btnStart);
            pnlAction.Controls.Add(_btnStop);
            pnlAction.Controls.Add(_lblRunningStatus);
            pnlAction.Controls.Add(_btnClearLog);
            pnlAction.Controls.Add(_btnCopyLog);

            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            pnlBottom.Controls.Add(_rtbLog);
            pnlBottom.Controls.Add(pnlAction);
            _splitRight.Panel2.Controls.Add(pnlBottom);
        }

        private void CreateLazCoinsTab()
        {
            TabPage tab = new TabPage("💰 LazCoins & Vouchers");
            tab.BackColor = Color.White;
            tab.Padding = new Padding(16);

            _chkLazCoins = new CheckBox
            {
                Text = "1. Điểm danh nhận LazCoins (Xu thưởng Lazada) hàng ngày",
                Dock = DockStyle.Top,
                Height = 35,
                Checked = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            _chkCollectVouchers = new CheckBox
            {
                Text = "2. Săn & Thu thập toàn bộ Mã Giảm Giá (Lazada Voucher Center)",
                Dock = DockStyle.Top,
                Height = 35,
                Checked = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            Label lblInfo = new Label
            {
                Text = "💡 Hướng dẫn:\n- Hệ thống sẽ mở trang LazCoins và tự động bấm nút nhận xu hàng ngày.\n- Sau đó chuyển sang trang Voucher Center và tự động quét 4 lượt cuộn trang, bấm toàn bộ các nút 'Thu thập' mã giảm giá còn hiệu lực.",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(55, 65, 81),
                Padding = new Padding(0, 15, 0, 0)
            };

            tab.Controls.Add(lblInfo);
            tab.Controls.Add(_chkCollectVouchers);
            tab.Controls.Add(_chkLazCoins);
            _tabs.TabPages.Add(tab);
        }

        private void CreateSeedingTab()
        {
            TabPage tab = new TabPage("🛒 Seeding & Giỏ Hàng");
            tab.BackColor = Color.White;
            tab.Padding = new Padding(16);

            Label lblKey = new Label { Text = "Từ khóa sản phẩm cần tìm kiếm (Keyword):", Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtKeyword = new TextBox { Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 9.5F), Text = "tai nghe bluetooth" };

            Panel pnlOptions = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(0, 10, 0, 0) };

            Label lblScroll = new Label { Text = "Thời gian lướt xem kết quả (giây):", Location = new Point(0, 15), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numScrollSec = new NumericUpDown { Minimum = 5, Maximum = 300, Value = 25, Location = new Point(220, 13), Width = 80 };

            _chkAddToCart = new CheckBox { Text = "Tự động bấm 'Thêm vào giỏ hàng' để tạo tương tác mua sắm", Checked = true, Location = new Point(0, 48), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            pnlOptions.Controls.Add(lblScroll);
            pnlOptions.Controls.Add(_numScrollSec);
            pnlOptions.Controls.Add(_chkAddToCart);

            Label lblNote = new Label
            {
                Text = "💡 Tính năng: Giúp tăng điểm hiển thị SEO cho gian hàng/sản phẩm Lazada, tạo lịch sử hành vi mua sắm tự nhiên cho dàn nick phụ / nick nuôi.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(107, 114, 128),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                Padding = new Padding(0, 10, 0, 0)
            };

            tab.Controls.Add(lblNote);
            tab.Controls.Add(pnlOptions);
            tab.Controls.Add(_txtKeyword);
            tab.Controls.Add(lblKey);
            _tabs.TabPages.Add(tab);
        }

        private void CreateCookieTab()
        {
            TabPage tab = new TabPage("🍪 Quản Lý Cookie");
            tab.BackColor = Color.White;
            tab.Padding = new Padding(16);

            Label lblCookie = new Label { Text = "Dán chuỗi Cookie Lazada vào đây (định dạng name=value; name2=value2):", Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtCookieInput = new TextBox { Dock = DockStyle.Top, Height = 100, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5F) };

            Panel pnlBtns = new Panel { Dock = DockStyle.Top, Height = 45, Padding = new Padding(0, 8, 0, 0) };
            _btnLoginCookie = new Button { Text = "🔑 Đăng nhập Cookie vào Profile đã chọn", Width = 260, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _btnLoginCookie.Click += delegate { LoginSelectedProfilesCookie(); };

            _btnExportCookie = new Button { Text = "📤 Xuất Cookie từ Profile", Location = new Point(270, 8), Width = 180, Height = 30, FlatStyle = FlatStyle.Flat };
            _btnExportCookie.Click += delegate { ExportSelectedProfileCookie(); };

            pnlBtns.Controls.Add(_btnLoginCookie);
            pnlBtns.Controls.Add(_btnExportCookie);

            tab.Controls.Add(pnlBtns);
            tab.Controls.Add(_txtCookieInput);
            tab.Controls.Add(lblCookie);
            _tabs.TabPages.Add(tab);
        }

        private void LoadProfileList()
        {
            string keyword = _txtSearch.Text.Trim();
            if (keyword == "🔍 Tìm kiếm profile...") keyword = "";

            _chkListProfiles.BeginUpdate();
            _chkListProfiles.Items.Clear();

            var filtered = _allProfiles.Where(p =>
                string.IsNullOrEmpty(keyword) ||
                (p.ProfileName != null && p.ProfileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (p.Username != null && p.Username.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (p.ProfileId != null && p.ProfileId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (p.Notes != null && p.Notes.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            ).ToList();

            for (int i = 0; i < filtered.Count; i++)
            {
                var profile = filtered[i];
                var item = new ProfileDisplayItem(profile);
                bool isChecked = _selectedProfileIds.Contains(profile.ProfileId);
                _chkListProfiles.Items.Add(item, isChecked);
            }

            _chkListProfiles.EndUpdate();
            UpdateProfileCount();
        }

        private void UpdateProfileCount()
        {
            _lblProfileCount.Text = string.Format("Đã chọn: {0} / {1} profiles", _selectedProfileIds.Count, _allProfiles.Count);
        }

        private List<UserProfile> GetSelectedProfiles()
        {
            return _allProfiles.Where(p => _selectedProfileIds.Contains(p.ProfileId)).ToList();
        }

        private void Log(string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(Log), msg);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _rtbLog.AppendText(string.Format("[{0}] {1}\r\n", timestamp, msg));
            _rtbLog.ScrollToCaret();
        }

        private void StartAutomation()
        {
            if (_isRunning) return;
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để thực thi!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selectedTab = _tabs.SelectedIndex;
            if (selectedTab == 1 && string.IsNullOrWhiteSpace(_txtKeyword.Text))
            {
                MessageBox.Show("Vui lòng nhập từ khóa tìm kiếm sản phẩm Lazada!", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _lblRunningStatus.Text = "Đang chạy automation...";
            _lblRunningStatus.ForeColor = Color.FromArgb(15, 23, 42);

            int maxThreads = (int)_numThreads.Value;
            var token = _cts.Token;

            bool doCoins = _chkLazCoins.Checked;
            bool doVouchers = _chkCollectVouchers.Checked;
            string keyword = _txtKeyword.Text.Trim();
            int scrollSec = (int)_numScrollSec.Value;
            bool addToCart = _chkAddToCart.Checked;

            Log(string.Format("=== BẮT ĐẦU CHIẾN DỊCH LAZADA ({0} PROFILES, {1} LUỒNG) ===", selected.Count, maxThreads));

            Task.Factory.StartNew(delegate
            {
                using (var semaphore = new SemaphoreSlim(maxThreads))
                {
                    var tasks = new List<Task>();

                    foreach (var prof in selected)
                    {
                        if (token.IsCancellationRequested) break;

                        var currentProf = prof;
                        semaphore.Wait(token);

                        var t = Task.Factory.StartNew(delegate
                        {
                            try
                            {
                                RunSingleProfile(currentProf, selectedTab, doCoins, doVouchers, keyword, scrollSec, addToCart, token);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }, token);

                        tasks.Add(t);
                    }

                    Task.WaitAll(tasks.ToArray());
                }

                this.BeginInvoke(new Action(delegate
                {
                    _isRunning = false;
                    _btnStart.Enabled = true;
                    _btnStop.Enabled = false;
                    _lblRunningStatus.Text = token.IsCancellationRequested ? "Đã dừng bởi người dùng" : "Hoàn tất chiến dịch!";
                    _lblRunningStatus.ForeColor = Color.FromArgb(16, 185, 129);
                    Log("=== TẤT CẢ TIẾN TRÌNH LAZADA ĐÃ KẾT THÚC ===");
                }));
            }, token);
        }

        private void RunSingleProfile(
            UserProfile profile,
            int tabIndex,
            bool doCoins,
            bool doVouchers,
            string keyword,
            int scrollSec,
            bool addToCart,
            CancellationToken token)
        {
            string profTag = string.Format("[{0}]", profile.ProfileName);
            Log(string.Format("{0} Đang khởi động trình duyệt...", profTag));

            IWebDriver driver = null;
            try
            {
                driver = BrowserSessionManager.Instance.GetDriver(profile.ProfileId);
                if (driver == null)
                {
                    driver = _launcherService.LaunchBrowser(profile);
                }

                if (driver == null)
                {
                    Log(string.Format("{0} Không thể mở trình duyệt profile!", profTag));
                    return;
                }

                Action<string> logger = delegate(string msg)
                {
                    Log(string.Format("{0} {1}", profTag, msg));
                };

                if (tabIndex == 0) // Coins & Voucher
                {
                    if (doCoins && !token.IsCancellationRequested)
                    {
                        _lazadaService.CheckinLazCoins(driver, logger);
                    }
                    if (doVouchers && !token.IsCancellationRequested)
                    {
                        _lazadaService.CollectVouchers(driver, logger);
                    }
                }
                else if (tabIndex == 1) // Seeding
                {
                    _lazadaService.SearchAndAddToCart(driver, keyword, scrollSec, addToCart, logger);
                }

                Log(string.Format("{0} Đã hoàn thành kịch bản Lazada thành công.", profTag));
            }
            catch (Exception ex)
            {
                Log(string.Format("{0} Lỗi ngoại lệ: {1}", profTag, ex.Message));
            }
        }

        private void LoginSelectedProfilesCookie()
        {
            string cookieStr = _txtCookieInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(cookieStr))
            {
                MessageBox.Show("Vui lòng dán chuỗi cookie Lazada vào ô văn bản!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để đăng nhập cookie!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Task.Factory.StartNew(delegate
            {
                foreach (var prof in selected)
                {
                    Log(string.Format("[{0}] Đang nạp cookie Lazada...", prof.ProfileName));
                    var driver = BrowserSessionManager.Instance.GetDriver(prof.ProfileId) ?? _launcherService.LaunchBrowser(prof);
                    if (driver != null)
                    {
                        _lazadaService.LoginWithCookie(driver, cookieStr, delegate(string msg)
                        {
                            Log(string.Format("[{0}] {1}", prof.ProfileName, msg));
                        });
                    }
                }
            });
        }

        private void ExportSelectedProfileCookie()
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn 1 profile để xuất cookie!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var prof = selected[0];
            var driver = BrowserSessionManager.Instance.GetDriver(prof.ProfileId) ?? _launcherService.LaunchBrowser(prof);
            if (driver != null)
            {
                string cookie = _lazadaService.ExportCookies(driver);
                if (!string.IsNullOrEmpty(cookie))
                {
                    _txtCookieInput.Text = cookie;
                    MessageBox.Show("Đã xuất Cookie thành công vào ô văn bản và sẵn sàng copy!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Không lấy được Cookie từ profile này.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void StopAutomation()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                Log("[*] Đang gửi tín hiệu dừng tới tất cả các luồng...");
            }
        }

        private class ProfileDisplayItem
        {
            public UserProfile Profile { get; private set; }

            public ProfileDisplayItem(UserProfile profile)
            {
                Profile = profile;
            }

            public override string ToString()
            {
                string proxyStr = string.IsNullOrEmpty(Profile.Proxy) ? "Direct" : Profile.Proxy;
                return string.Format("{0} ({1})", Profile.ProfileName, proxyStr);
            }
        }
    }
}
