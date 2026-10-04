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
    public class ShopeeAutomationForm : Form
    {
        private CheckedListBox _chkListProfiles;
        private TextBox _txtSearch;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRunning;
        private Label _lblProfileCount;

        // Tab 1: Coins & Video
        private CheckBox _chkCheckinCoins;
        private CheckBox _chkSurfVideo;
        private NumericUpDown _numVideoCount;
        private NumericUpDown _numVideoMin;
        private NumericUpDown _numVideoMax;

        // Tab 2: Vouchers
        private Button _btnCollectVouchersNow;

        // Tab 3: Seeding
        private TextBox _txtSearchKeywords;
        private TextBox _txtTargetProductOrShop;
        private NumericUpDown _numStaySeconds;
        private CheckBox _chkAddToCart;

        // Tab 4: Cookie
        private TextBox _txtCookieInput;
        private Button _btnLoginCookie;
        private Button _btnExtractCookie;

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
        private readonly ShopeeAutomationService _shopeeService = new ShopeeAutomationService();
        private readonly BrowserLauncherService _launcherService = new BrowserLauncherService();
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        public ShopeeAutomationForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            LoadProfileList();
        }

        private void InitializeComponent()
        {
            this.Text = "🛒 BỘ CÔNG CỤ TỰ ĐỘNG HÓA SHOPEE (SHOPEE AUTOMATION & SEEDING)";
            this.Size = new Size(1180, 780);
            this.MinimumSize = new Size(1000, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            // ================= HEADER =================
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(238, 77, 45), // Shopee Orange
                Padding = new Padding(16, 8, 16, 8)
            };

            Label lblTitle = new Label
            {
                Text = "🛒 SHOPEE AUTOMATION & SEEDING STUDIO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Nuôi nick cày Xu hàng ngày, Lướt Shopee Video, Lưu toàn bộ Voucher, Seeding kéo traffic & SEO gian hàng",
                ForeColor = Color.FromArgb(254, 226, 226),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(15, 30)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // ================= MAIN SPLIT =================
            _splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.FromArgb(226, 232, 240)
            };
            this.Controls.Add(_splitMain);
            pnlHeader.SendToBack();

            // LEFT PANEL
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
            Label lblProfilesTitle = new Label { Text = "DANH SÁCH PROFILES", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Dock = DockStyle.Top, Height = 24 };

            _txtSearch = new TextBox { Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(100, 116, 139), Text = "🔍 Tìm kiếm profile..." };
            _txtSearch.GotFocus += (s, e) => { if (_txtSearch.Text == "🔍 Tìm kiếm profile...") { _txtSearch.Text = ""; _txtSearch.ForeColor = Color.Black; } };
            _txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_txtSearch.Text)) { _txtSearch.Text = "🔍 Tìm kiếm profile..."; _txtSearch.ForeColor = Color.FromArgb(100, 116, 139); } };
            _txtSearch.TextChanged += (s, e) => FilterProfiles();

            Panel pnlSelectButtons = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 4, 0, 4) };
            _btnSelectAll = new Button { Text = "Tất cả", Width = 60, Dock = DockStyle.Left, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnSelectAll.Click += (s, e) => SetCheckAll(true);

            _btnDeselectAll = new Button { Text = "Bỏ chọn", Width = 65, Dock = DockStyle.Left, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnDeselectAll.Click += (s, e) => SetCheckAll(false);

            _btnSelectRunning = new Button { Text = "Đang chạy", Width = 80, Dock = DockStyle.Left, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnSelectRunning.Click += (s, e) => SelectRunningProfiles();

            pnlSelectButtons.Controls.Add(_btnSelectRunning);
            pnlSelectButtons.Controls.Add(_btnDeselectAll);
            pnlSelectButtons.Controls.Add(_btnSelectAll);

            _chkListProfiles = new CheckedListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, CheckOnClick = true, Font = new Font("Segoe UI", 9F) };
            _lblProfileCount = new Label { Dock = DockStyle.Bottom, Height = 22, Text = "Đã chọn: 0 / 0 profiles", ForeColor = Color.FromArgb(100, 116, 139), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5F) };
            _chkListProfiles.ItemCheck += (s, e) => { this.BeginInvoke(new Action(UpdateProfileCountLabel)); };

            pnlLeft.Controls.Add(_chkListProfiles);
            pnlLeft.Controls.Add(pnlSelectButtons);
            pnlLeft.Controls.Add(_txtSearch);
            pnlLeft.Controls.Add(lblProfilesTitle);
            pnlLeft.Controls.Add(_lblProfileCount);
            _splitMain.Panel1.Controls.Add(pnlLeft);

            // RIGHT PANEL
            _splitRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Color.FromArgb(226, 232, 240) };
            _splitMain.Panel2.Controls.Add(_splitRight);

            // TABS
            _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _tabs.TabPages.Add(CreateCoinsTab());
            _tabs.TabPages.Add(CreateVouchersTab());
            _tabs.TabPages.Add(CreateSeedingTab());
            _tabs.TabPages.Add(CreateCookieTab());
            _splitRight.Panel1.Controls.Add(_tabs);

            // BOTTOM CONTROLS & LOG
            Panel pnlLogContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
            Panel pnlControls = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(0, 0, 0, 8) };

            Label lblThreads = new Label { Text = "Số luồng song song:", AutoSize = true, Location = new Point(4, 10), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numThreads = new NumericUpDown { Location = new Point(140, 8), Width = 55, Minimum = 1, Maximum = 30, Value = 2 };

            _btnStart = new Button { Text = "🚀 BẮT ĐẦU CHẠY", Location = new Point(206, 4), Width = 150, Height = 32, BackColor = Color.FromArgb(238, 77, 45), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            _btnStart.FlatAppearance.BorderSize = 0;
            _btnStart.Click += BtnStart_Click;

            _btnStop = new Button { Text = "⏹ DỪNG LẠI", Location = new Point(364, 4), Width = 110, Height = 32, BackColor = Color.FromArgb(239, 68, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Enabled = false, Cursor = Cursors.Hand };
            _btnStop.FlatAppearance.BorderSize = 0;
            _btnStop.Click += BtnStop_Click;

            _lblRunningStatus = new Label { Text = "Sẵn sàng", Location = new Point(485, 10), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 8.5F, FontStyle.Italic) };
            _btnClearLog = new Button { Text = "Xóa Log", Dock = DockStyle.Right, Width = 70, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnClearLog.Click += (s, e) => _rtbLog.Clear();

            _btnCopyLog = new Button { Text = "Copy Log", Dock = DockStyle.Right, Width = 75, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnCopyLog.Click += (s, e) => { if (!string.IsNullOrEmpty(_rtbLog.Text)) Clipboard.SetText(_rtbLog.Text); };

            pnlControls.Controls.Add(_btnCopyLog);
            pnlControls.Controls.Add(_btnClearLog);
            pnlControls.Controls.Add(_lblRunningStatus);
            pnlControls.Controls.Add(_btnStop);
            pnlControls.Controls.Add(_btnStart);
            pnlControls.Controls.Add(_numThreads);
            pnlControls.Controls.Add(lblThreads);

            _rtbLog = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.FromArgb(226, 232, 240), Font = new Font("Consolas", 9F), ReadOnly = true, BorderStyle = BorderStyle.None };
            pnlLogContainer.Controls.Add(_rtbLog);
            pnlLogContainer.Controls.Add(pnlControls);
            _splitRight.Panel2.Controls.Add(pnlLogContainer);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                if (_splitMain != null && _splitMain.Width > 400) _splitMain.SplitterDistance = Math.Min(320, Math.Max(240, _splitMain.Width / 4));
                if (_splitRight != null && _splitRight.Height > 400) _splitRight.SplitterDistance = Math.Min(420, Math.Max(280, _splitRight.Height - 240));
            }
            catch { }
        }

        #region Create Tabs

        private TabPage CreateCoinsTab()
        {
            TabPage tab = new TabPage("🪙 Cày Shopee Xu & Video") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            _chkCheckinCoins = new CheckBox { Text = "Tự động Điểm danh nhận Shopee Xu hàng ngày", Location = new Point(14, 16), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _chkSurfVideo = new CheckBox { Text = "Tự động lướt Shopee Video nhận xu & thưởng", Location = new Point(14, 52), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

            Label lblCount = new Label { Text = "Số lượng video cần lướt:", Location = new Point(36, 88), AutoSize = true };
            _numVideoCount = new NumericUpDown { Location = new Point(184, 86), Width = 55, Minimum = 1, Maximum = 50, Value = 8 };

            Label lblTime = new Label { Text = "Thời gian xem mỗi video:", Location = new Point(36, 122), AutoSize = true };
            _numVideoMin = new NumericUpDown { Location = new Point(184, 120), Width = 50, Minimum = 3, Maximum = 60, Value = 6 };
            Label lblTo = new Label { Text = "đến", Location = new Point(238, 122), AutoSize = true };
            _numVideoMax = new NumericUpDown { Location = new Point(266, 120), Width = 50, Minimum = 3, Maximum = 120, Value = 14 };
            Label lblUnit = new Label { Text = "giây (ngẫu nhiên)", Location = new Point(320, 122), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };

            Label lblGuide = new Label
            {
                Text = "💡 Cơ chế: Tự động truy cập trang Shopee Xu để bấm nhận xu điểm danh, sau đó chuyển sang Shopee Video\nđể xem video và cuộn tự nhiên, giúp tài khoản tích lũy hàng ngàn xu để mua sắm.",
                Location = new Point(14, 170),
                AutoSize = true,
                ForeColor = Color.FromArgb(3, 105, 161),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            pnl.Controls.Add(lblGuide);
            pnl.Controls.Add(lblUnit);
            pnl.Controls.Add(_numVideoMax);
            pnl.Controls.Add(lblTo);
            pnl.Controls.Add(_numVideoMin);
            pnl.Controls.Add(lblTime);
            pnl.Controls.Add(_numVideoCount);
            pnl.Controls.Add(lblCount);
            pnl.Controls.Add(_chkSurfVideo);
            pnl.Controls.Add(_chkCheckinCoins);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateVouchersTab()
        {
            TabPage tab = new TabPage("🎁 Săn Voucher Giảm Giá") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblVoucher = new Label { Text = "Tự động quét và Lưu toàn bộ Mã Giảm Giá & Freeship:", Location = new Point(14, 16), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

            _btnCollectVouchersNow = new Button
            {
                Text = "📥 Thu Thập Toàn Bộ Voucher Trên Shopee",
                Location = new Point(18, 48),
                Width = 320,
                Height = 38,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnCollectVouchersNow.Click += (s, e) =>
            {
                _tabs.SelectedIndex = 1;
                BtnStart_Click(s, e);
            };

            Label lblVGuide = new Label
            {
                Text = "💡 Tool sẽ tự động mở trang Mã Giảm Giá Shopee, cuộn trang liên tục để nạp các mã mới\nvà tự động bấm nút [Lưu] trên toàn bộ các mã freeship, giảm giá sàn và voucher ngành hàng.",
                Location = new Point(18, 110),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F)
            };

            pnl.Controls.Add(lblVGuide);
            pnl.Controls.Add(_btnCollectVouchersNow);
            pnl.Controls.Add(lblVoucher);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateSeedingTab()
        {
            TabPage tab = new TabPage("🔍 Seeding Kéo Traffic Shop") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblKwd = new Label { Text = "Từ khóa tìm kiếm (hỗ trợ Spintax {nồi chiên không dầu|nồi chiên đa năng}):", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtSearchKeywords = new TextBox { Location = new Point(14, 34), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "{áo thun nam form rộng|áo thun cotton cao cấp}" };

            Label lblTarget = new Label { Text = "Tên Shop hoặc Tên Sản phẩm mục tiêu cần click (đích đến):", Location = new Point(10, 72), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtTargetProductOrShop = new TextBox { Location = new Point(14, 94), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "Tên Shop Của Bạn" };

            Label lblStay = new Label { Text = "Thời gian ở lại xem sản phẩm & đánh giá:", Location = new Point(10, 132), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numStaySeconds = new NumericUpDown { Location = new Point(270, 130), Width = 60, Minimum = 10, Maximum = 600, Value = 30 };
            Label lblStayUnit = new Label { Text = "giây (cuộn mượt mô phỏng người mua thật)", Location = new Point(336, 132), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };

            _chkAddToCart = new CheckBox { Text = "Tự động bấm [Thêm Vào Giỏ Hàng] (tăng tỷ lệ chuyển đổi cho Shop)", Location = new Point(14, 170), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            pnl.Controls.Add(_chkAddToCart);
            pnl.Controls.Add(lblStayUnit);
            pnl.Controls.Add(_numStaySeconds);
            pnl.Controls.Add(lblStay);
            pnl.Controls.Add(_txtTargetProductOrShop);
            pnl.Controls.Add(lblTarget);
            pnl.Controls.Add(_txtSearchKeywords);
            pnl.Controls.Add(lblKwd);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateCookieTab()
        {
            TabPage tab = new TabPage("🔑 Quản Lý Cookie") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblCookie = new Label { Text = "Chuỗi Cookie Shopee (chứa SPC_EC=...; shopee_token=...):", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtCookieInput = new TextBox { Location = new Point(14, 34), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F) };

            _btnLoginCookie = new Button { Text = "🔐 Đăng nhập bằng Cookie", Location = new Point(14, 76), Width = 200, Height = 34, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnLoginCookie.Click += BtnLoginCookie_Click;

            _btnExtractCookie = new Button { Text = "📥 Xuất Cookie vào Clipboard", Location = new Point(224, 76), Width = 210, Height = 34, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnExtractCookie.Click += BtnExtractCookie_Click;

            pnl.Controls.Add(_btnExtractCookie);
            pnl.Controls.Add(_btnLoginCookie);
            pnl.Controls.Add(_txtCookieInput);
            pnl.Controls.Add(lblCookie);

            tab.Controls.Add(pnl);
            return tab;
        }

        #endregion

        #region Profile List & Filtering

        private void LoadProfileList()
        {
            _chkListProfiles.Items.Clear();
            var sessions = BrowserSessionManager.Instance.GetActiveSessionIds();

            foreach (var p in _allProfiles)
            {
                bool isRunning = sessions.Contains(p.ProfileId);
                string text = string.Format("{0} - {1}{2}", p.ProfileName, !string.IsNullOrEmpty(p.Username) ? p.Username : "No User", isRunning ? " [▶ Đang chạy]" : "");
                _chkListProfiles.Items.Add(new ProfileItemWrapper { Profile = p, DisplayText = text });
            }
            UpdateProfileCountLabel();
        }

        private void FilterProfiles()
        {
            string query = _txtSearch.Text.Trim();
            if (query == "🔍 Tìm kiếm profile...") query = "";

            _chkListProfiles.Items.Clear();
            var sessions = BrowserSessionManager.Instance.GetActiveSessionIds();

            var filtered = string.IsNullOrEmpty(query)
                ? _allProfiles
                : _allProfiles.Where(p => (p.ProfileName != null && p.ProfileName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                          (p.Username != null && p.Username.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

            foreach (var p in filtered)
            {
                bool isRunning = sessions.Contains(p.ProfileId);
                string text = string.Format("{0} - {1}{2}", p.ProfileName, !string.IsNullOrEmpty(p.Username) ? p.Username : "No User", isRunning ? " [▶ Đang chạy]" : "");
                _chkListProfiles.Items.Add(new ProfileItemWrapper { Profile = p, DisplayText = text });
            }
            UpdateProfileCountLabel();
        }

        private void SetCheckAll(bool check)
        {
            for (int i = 0; i < _chkListProfiles.Items.Count; i++) _chkListProfiles.SetItemChecked(i, check);
            UpdateProfileCountLabel();
        }

        private void SelectRunningProfiles()
        {
            var sessions = BrowserSessionManager.Instance.GetActiveSessionIds();
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                var item = _chkListProfiles.Items[i] as ProfileItemWrapper;
                _chkListProfiles.SetItemChecked(i, item != null && sessions.Contains(item.Profile.ProfileId));
            }
            UpdateProfileCountLabel();
        }

        private void UpdateProfileCountLabel()
        {
            int checkedCount = _chkListProfiles.CheckedItems.Count;
            int total = _chkListProfiles.Items.Count;
            _lblProfileCount.Text = string.Format("Đã chọn: {0} / {1} profiles", checkedCount, total);
        }

        private List<UserProfile> GetSelectedProfiles()
        {
            var list = new List<UserProfile>();
            foreach (var item in _chkListProfiles.CheckedItems)
            {
                var wrapper = item as ProfileItemWrapper;
                if (wrapper != null) list.Add(wrapper.Profile);
            }
            return list;
        }

        #endregion

        #region Execution

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_isRunning) return;
            var selectedProfiles = GetSelectedProfiles();
            if (selectedProfiles.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để thực hiện tác vụ Shopee!", "Chưa chọn profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selectedTab = _tabs.SelectedIndex;
            int maxThreads = (int)_numThreads.Value;

            _isRunning = true;
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _lblRunningStatus.Text = string.Format("Đang chạy {0} profile...", selectedProfiles.Count);
            _lblRunningStatus.ForeColor = Color.FromArgb(238, 77, 45);

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Log(string.Format("=== [BẮT ĐẦU TÁC VỤ SHOPEE - {0} PROFILE - {1} LUỒNG] ===", selectedProfiles.Count, maxThreads), Color.Cyan);

            Task.Run(() =>
            {
                try
                {
                    Parallel.ForEach(selectedProfiles, new ParallelOptions { MaxDegreeOfParallelism = maxThreads, CancellationToken = token }, (profile) =>
                    {
                        if (token.IsCancellationRequested) return;
                        ExecuteTaskForProfile(profile, selectedTab, token);
                    });
                }
                catch (OperationCanceledException)
                {
                    Log("[-] Tác vụ đã được dừng bởi người dùng.", Color.Orange);
                }
                catch (Exception ex)
                {
                    Log(string.Format("[-] Lỗi tiến trình: {0}", ex.Message), Color.Red);
                }
                finally
                {
                    this.Invoke(new Action(() =>
                    {
                        _isRunning = false;
                        _btnStart.Enabled = true;
                        _btnStop.Enabled = false;
                        _lblRunningStatus.Text = "Đã hoàn thành";
                        _lblRunningStatus.ForeColor = Color.FromArgb(100, 116, 139);
                        Log("=== [HOÀN TẤT TẤT CẢ TÁC VỤ SHOPEE] ===", Color.Green);
                    }));
                }
            });
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                _lblRunningStatus.Text = "Đang dừng lại...";
                _lblRunningStatus.ForeColor = Color.FromArgb(239, 68, 68);
            }
        }

        private void ExecuteTaskForProfile(UserProfile profile, int tabIndex, CancellationToken token)
        {
            string pName = profile.ProfileName;
            IWebDriver driver = null;

            try
            {
                driver = BrowserSessionManager.Instance.GetSession(profile.ProfileId);
                if (driver == null)
                {
                    Log(string.Format("[{0}] Đang khởi động trình duyệt Orbita...", pName), Color.White);
                    driver = _launcherService.LaunchBrowser(profile);
                }

                if (driver == null)
                {
                    Log(string.Format("[{0}] Không thể khởi động trình duyệt!", pName), Color.Red);
                    return;
                }

                Action<string> logger = (msg) => Log(string.Format("[{0}] {1}", pName, msg), Color.LightGray);

                switch (tabIndex)
                {
                    case 0: // Coins & Video
                        bool doCheckin = true;
                        bool doVideo = true;
                        int vCount = 8;
                        int minW = 6;
                        int maxW = 14;
                        this.Invoke(new Action(() =>
                        {
                            doCheckin = _chkCheckinCoins.Checked;
                            doVideo = _chkSurfVideo.Checked;
                            vCount = (int)_numVideoCount.Value;
                            minW = (int)_numVideoMin.Value;
                            maxW = (int)_numVideoMax.Value;
                        }));

                        BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Shopee: Cày Xu & Video");
                        if (doCheckin) _shopeeService.CheckInCoins(driver, logger);
                        if (doVideo) _shopeeService.SurfShopeeVideo(driver, vCount, minW, maxW, logger);
                        break;

                    case 1: // Vouchers
                        BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Shopee: Săn Voucher");
                        _shopeeService.CollectVouchers(driver, logger);
                        break;

                    case 2: // Seeding
                        string kwd = "";
                        string target = "";
                        int stay = 30;
                        bool addCart = true;
                        this.Invoke(new Action(() =>
                        {
                            kwd = _txtSearchKeywords.Text;
                            target = _txtTargetProductOrShop.Text;
                            stay = (int)_numStaySeconds.Value;
                            addCart = _chkAddToCart.Checked;
                        }));
                        BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Shopee: Seeding Sản Phẩm");
                        _shopeeService.SearchAndSeedProduct(driver, kwd, target, stay, addCart, logger);
                        break;

                    case 3: // Cookie
                        string cStr = "";
                        this.Invoke(new Action(() => { cStr = _txtCookieInput.Text; }));
                        if (!string.IsNullOrEmpty(cStr))
                        {
                            BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Shopee: Đăng nhập Cookie");
                            _shopeeService.LoginWithCookie(driver, cStr, logger);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[{0}] Lỗi: {1}", pName, ex.Message), Color.Red);
            }
            finally
            {
                BrowserSessionManager.Instance.ClearRunningTask(profile.ProfileId);
            }
        }

        private void BtnLoginCookie_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0) return;
            string cookie = _txtCookieInput.Text.Trim();
            if (string.IsNullOrEmpty(cookie)) return;

            var profile = selected[0];
            var driver = BrowserSessionManager.Instance.GetSession(profile.ProfileId) ?? _launcherService.LaunchBrowser(profile);
            if (driver != null) _shopeeService.LoginWithCookie(driver, cookie, (m) => Log(string.Format("[{0}] {1}", profile.ProfileName, m), Color.LightGray));
        }

        private void BtnExtractCookie_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0) return;
            var profile = selected[0];
            var driver = BrowserSessionManager.Instance.GetSession(profile.ProfileId) ?? _launcherService.LaunchBrowser(profile);
            if (driver != null)
            {
                string cookies = _shopeeService.ExtractShopeeCookies(driver, (m) => Log(m, Color.LightGray));
                if (!string.IsNullOrEmpty(cookies))
                {
                    Clipboard.SetText(cookies);
                    MessageBox.Show("Đã trích xuất Cookie Shopee thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void Log(string message, Color color)
        {
            if (_rtbLog.IsDisposed) return;
            if (_rtbLog.InvokeRequired)
            {
                _rtbLog.BeginInvoke(new Action(() => Log(message, color)));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _rtbLog.SelectionStart = _rtbLog.TextLength;
            _rtbLog.SelectionLength = 0;
            _rtbLog.SelectionColor = Color.FromArgb(148, 163, 184);
            _rtbLog.AppendText(string.Format("[{0}] ", timestamp));
            _rtbLog.SelectionColor = color;
            _rtbLog.AppendText(message + Environment.NewLine);
            _rtbLog.ScrollToCaret();
        }

        #endregion

        private class ProfileItemWrapper
        {
            public UserProfile Profile { get; set; }
            public string DisplayText { get; set; }
            public override string ToString() { return DisplayText; }
        }
    }
}
