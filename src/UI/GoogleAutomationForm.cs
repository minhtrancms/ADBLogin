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
    public class GoogleAutomationForm : Form
    {
        private CheckedListBox _chkListProfiles;
        private TextBox _txtSearch;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRunning;
        private Label _lblProfileCount;

        // Tab 1: Seeding
        private TextBox _txtSearchKeywords;
        private TextBox _txtTargetDomain;
        private NumericUpDown _numStaySeconds;

        // Tab 2: Login
        private TextBox _txtEmail;
        private TextBox _txtPassword;
        private TextBox _txtTwoFactorSecret;
        private TextBox _txtRecoveryEmail;
        private Button _btnExtractCookie;

        // Tab 3: Gmail OTP
        private TextBox _txtSenderFilter;
        private TextBox _txtResultOtp;
        private Button _btnCopyOtp;

        // Tab 4: YouTube
        private TextBox _txtYoutubeTarget;
        private NumericUpDown _numWatchSeconds;
        private CheckBox _chkAutoLikeYoutube;

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
        private readonly GoogleAutomationService _googleService = new GoogleAutomationService();
        private readonly BrowserLauncherService _launcherService = new BrowserLauncherService();
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        public GoogleAutomationForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            LoadProfileList();
        }

        private void InitializeComponent()
        {
            this.Text = "🌐 BỘ CÔNG CỤ TỰ ĐỘNG HÓA GOOGLE & GMAIL (GOOGLE AUTOMATION STUDIO)";
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
                Text = "🌐 GOOGLE & GMAIL AUTOMATION STUDIO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Đăng nhập Google 2FA TOTP, Seeding tìm kiếm SEO, Xem YouTube tương tác, Đọc mã OTP Gmail tự động",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(15, 30)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // ================= 2. MAIN SPLIT CONTAINER =================
            _splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.FromArgb(226, 232, 240)
            };
            this.Controls.Add(_splitMain);
            pnlHeader.SendToBack();

            // ================= 3. LEFT PANEL: DANH SÁCH PROFILES =================
            Panel pnlLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            Label lblProfilesTitle = new Label
            {
                Text = "DANH SÁCH PROFILES",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Dock = DockStyle.Top,
                Height = 24
            };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            _txtSearch.Text = "🔍 Tìm kiếm profile...";
            _txtSearch.GotFocus += (s, e) => { if (_txtSearch.Text == "🔍 Tìm kiếm profile...") { _txtSearch.Text = ""; _txtSearch.ForeColor = Color.Black; } };
            _txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_txtSearch.Text)) { _txtSearch.Text = "🔍 Tìm kiếm profile..."; _txtSearch.ForeColor = Color.FromArgb(100, 116, 139); } };
            _txtSearch.TextChanged += (s, e) => FilterProfiles();

            Panel pnlSelectButtons = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 4, 0, 4) };
            _btnSelectAll = new Button { Text = "Tất cả", Width = 60, Dock = DockStyle.Left, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnSelectAll.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnSelectAll.Click += (s, e) => SetCheckAll(true);

            _btnDeselectAll = new Button { Text = "Bỏ chọn", Width = 65, Dock = DockStyle.Left, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnDeselectAll.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnDeselectAll.Click += (s, e) => SetCheckAll(false);

            _btnSelectRunning = new Button { Text = "Đang chạy", Width = 80, Dock = DockStyle.Left, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnSelectRunning.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnSelectRunning.Click += (s, e) => SelectRunningProfiles();

            pnlSelectButtons.Controls.Add(_btnSelectRunning);
            pnlSelectButtons.Controls.Add(_btnDeselectAll);
            pnlSelectButtons.Controls.Add(_btnSelectAll);

            _chkListProfiles = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9F)
            };

            _lblProfileCount = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Text = "Đã chọn: 0 / 0 profiles",
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5F)
            };
            _chkListProfiles.ItemCheck += (s, e) => { this.BeginInvoke(new Action(UpdateProfileCountLabel)); };

            pnlLeft.Controls.Add(_chkListProfiles);
            pnlLeft.Controls.Add(pnlSelectButtons);
            pnlLeft.Controls.Add(_txtSearch);
            pnlLeft.Controls.Add(lblProfilesTitle);
            pnlLeft.Controls.Add(_lblProfileCount);
            _splitMain.Panel1.Controls.Add(pnlLeft);

            // ================= 4. RIGHT PANEL: TABS & REAL-TIME LOG =================
            _splitRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.FromArgb(226, 232, 240)
            };
            _splitMain.Panel2.Controls.Add(_splitRight);

            // TABS CẤU HÌNH TÁC VỤ
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            _tabs.TabPages.Add(CreateSearchSeedingTab());
            _tabs.TabPages.Add(CreateLoginTab());
            _tabs.TabPages.Add(CreateGmailOtpTab());
            _tabs.TabPages.Add(CreateYouTubeTab());
            _splitRight.Panel1.Controls.Add(_tabs);

            // LOG WINDOW & CHÂN TRANG ĐIỀU KHIỂN
            Panel pnlLogContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };

            Panel pnlControls = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(0, 0, 0, 8) };

            Label lblThreads = new Label { Text = "Số luồng chạy song song:", AutoSize = true, Location = new Point(4, 10), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numThreads = new NumericUpDown { Location = new Point(168, 8), Width = 55, Minimum = 1, Maximum = 30, Value = 2 };

            _btnStart = new Button
            {
                Text = "🚀 BẮT ĐẦU CHẠY",
                Location = new Point(236, 4),
                Width = 150,
                Height = 32,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnStart.FlatAppearance.BorderSize = 0;
            _btnStart.Click += BtnStart_Click;

            _btnStop = new Button
            {
                Text = "⏹ DỪNG LẠI",
                Location = new Point(394, 4),
                Width = 110,
                Height = 32,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _btnStop.FlatAppearance.BorderSize = 0;
            _btnStop.Click += BtnStop_Click;

            _lblRunningStatus = new Label
            {
                Text = "Sẵn sàng",
                Location = new Point(515, 10),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

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

            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            pnlLogContainer.Controls.Add(_rtbLog);
            pnlLogContainer.Controls.Add(pnlControls);
            _splitRight.Panel2.Controls.Add(pnlLogContainer);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                if (_splitMain != null && _splitMain.Width > 400)
                {
                    _splitMain.SplitterDistance = Math.Min(320, Math.Max(240, _splitMain.Width / 4));
                }
                if (_splitRight != null && _splitRight.Height > 400)
                {
                    _splitRight.SplitterDistance = Math.Min(420, Math.Max(280, _splitRight.Height - 240));
                }
            }
            catch { }
        }

        #region Create Tabs

        private TabPage CreateSearchSeedingTab()
        {
            TabPage tab = new TabPage("🔍 Google Search Seeding") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblKwd = new Label { Text = "Từ khóa tìm kiếm trên Google (hỗ trợ Spintax {từ khóa 1|từ khóa 2}):", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtSearchKeywords = new TextBox { Location = new Point(14, 34), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "{dịch vụ marketing uy tín|cách tăng view tiktok|mua đồ gia dụng thông minh}" };

            Label lblDomain = new Label { Text = "Website mục tiêu (Domain hoặc URL cần click, ví dụ: mywebsite.com hoặc shopee.vn):", Location = new Point(10, 72), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtTargetDomain = new TextBox { Location = new Point(14, 94), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "shopee.vn" };

            Label lblStay = new Label { Text = "Thời gian ở lại tương tác đọc bài trên website:", Location = new Point(10, 134), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numStaySeconds = new NumericUpDown { Location = new Point(290, 132), Width = 60, Minimum = 5, Maximum = 600, Value = 30 };
            Label lblUnit = new Label { Text = "giây (tự động cuộn mượt mô phỏng người thật)", Location = new Point(356, 134), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };

            Label lblNote = new Label
            {
                Text = "💡 Kịch bản: Mở Google -> Tìm kiếm từ khóa -> Cuộn kết quả -> Bấm vào website mục tiêu -> Cuộn đọc trang -> Tăng điểm SEO & trust profile.",
                Location = new Point(14, 180),
                AutoSize = true,
                ForeColor = Color.FromArgb(3, 105, 161),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            pnl.Controls.Add(lblNote);
            pnl.Controls.Add(lblUnit);
            pnl.Controls.Add(_numStaySeconds);
            pnl.Controls.Add(lblStay);
            pnl.Controls.Add(_txtTargetDomain);
            pnl.Controls.Add(lblDomain);
            pnl.Controls.Add(_txtSearchKeywords);
            pnl.Controls.Add(lblKwd);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateLoginTab()
        {
            TabPage tab = new TabPage("🔑 Đăng nhập & 2FA") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblEmail = new Label { Text = "Tài khoản Gmail / Email:", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtEmail = new TextBox { Location = new Point(14, 34), Width = 280, Height = 26, Font = new Font("Segoe UI", 9.5F) };

            Label lblPass = new Label { Text = "Mật khẩu (Password):", Location = new Point(310, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtPassword = new TextBox { Location = new Point(314, 34), Width = 260, Height = 26, PasswordChar = '•', Font = new Font("Segoe UI", 9.5F) };

            Label lbl2FA = new Label { Text = "2FA Secret Key (Tự động giải mã OTP 6 số điền vào Google):", Location = new Point(10, 72), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtTwoFactorSecret = new TextBox { Location = new Point(14, 94), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F) };

            Label lblRecovery = new Label { Text = "Email khôi phục (Recovery Email - nếu Google hỏi):", Location = new Point(10, 132), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtRecoveryEmail = new TextBox { Location = new Point(14, 154), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F) };

            _btnExtractCookie = new Button
            {
                Text = "📥 Xuất Cookie Google vào Clipboard",
                Location = new Point(14, 196),
                Width = 260,
                Height = 34,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnExtractCookie.Click += BtnExtractCookie_Click;

            pnl.Controls.Add(_btnExtractCookie);
            pnl.Controls.Add(_txtRecoveryEmail);
            pnl.Controls.Add(lblRecovery);
            pnl.Controls.Add(_txtTwoFactorSecret);
            pnl.Controls.Add(lbl2FA);
            pnl.Controls.Add(_txtPassword);
            pnl.Controls.Add(lblPass);
            pnl.Controls.Add(_txtEmail);
            pnl.Controls.Add(lblEmail);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateGmailOtpTab()
        {
            TabPage tab = new TabPage("📧 Đọc OTP Gmail") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblFilter = new Label { Text = "Lọc email theo tên dịch vụ hoặc người gửi (ví dụ: Facebook, TikTok, Shopee, Telegram...):", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtSenderFilter = new TextBox { Location = new Point(14, 34), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "Facebook" };

            Label lblOtpResult = new Label { Text = "Mã OTP trích xuất mới nhất:", Location = new Point(10, 80), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtResultOtp = new TextBox { Location = new Point(14, 104), Width = 280, Height = 36, Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = Color.FromArgb(16, 185, 129), ReadOnly = true, BackColor = Color.FromArgb(248, 250, 252) };

            _btnCopyOtp = new Button
            {
                Text = "📋 Copy Mã OTP",
                Location = new Point(304, 104),
                Width = 120,
                Height = 36,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnCopyOtp.Click += (s, e) => { if (!string.IsNullOrEmpty(_txtResultOtp.Text)) Clipboard.SetText(_txtResultOtp.Text); };

            Label lblOtpGuide = new Label
            {
                Text = "💡 Tính năng này sẽ điều hướng trực tiếp vào Hộp thư đến (Inbox) của Gmail trên profile được chọn,\ntự động mở thư mới nhất và phân tích chuỗi số OTP (4 - 8 chữ số) để điền vào form đăng ký của bạn.",
                Location = new Point(14, 160),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F)
            };

            pnl.Controls.Add(lblOtpGuide);
            pnl.Controls.Add(_btnCopyOtp);
            pnl.Controls.Add(_txtResultOtp);
            pnl.Controls.Add(lblOtpResult);
            pnl.Controls.Add(_txtSenderFilter);
            pnl.Controls.Add(lblFilter);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateYouTubeTab()
        {
            TabPage tab = new TabPage("🎬 YouTube Watch") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblYt = new Label { Text = "URL Video hoặc Từ khóa tìm kiếm trên YouTube (hỗ trợ Spintax):", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtYoutubeTarget = new TextBox { Location = new Point(14, 34), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "{nhạc lofi thư giãn|tin tức công nghệ mới|review điện thoại}" };

            Label lblDuration = new Label { Text = "Thời gian xem video:", Location = new Point(10, 76), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numWatchSeconds = new NumericUpDown { Location = new Point(150, 74), Width = 65, Minimum = 10, Maximum = 3600, Value = 45 };
            Label lblDurationUnit = new Label { Text = "giây", Location = new Point(220, 76), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };

            _chkAutoLikeYoutube = new CheckBox { Text = "Tự động bấm Thả Like cho video", Location = new Point(14, 116), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            Label lblYtGuide = new Label
            {
                Text = "💡 Tương tác xem YouTube giúp tạo lịch sử xem phong phú cho tài khoản Google, tăng uy tín tránh bị Google quét bot.",
                Location = new Point(14, 160),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F)
            };

            pnl.Controls.Add(lblYtGuide);
            pnl.Controls.Add(_chkAutoLikeYoutube);
            pnl.Controls.Add(lblDurationUnit);
            pnl.Controls.Add(_numWatchSeconds);
            pnl.Controls.Add(lblDuration);
            pnl.Controls.Add(_txtYoutubeTarget);
            pnl.Controls.Add(lblYt);

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
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                _chkListProfiles.SetItemChecked(i, check);
            }
            UpdateProfileCountLabel();
        }

        private void SelectRunningProfiles()
        {
            var sessions = BrowserSessionManager.Instance.GetActiveSessionIds();
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                var item = _chkListProfiles.Items[i] as ProfileItemWrapper;
                if (item != null && sessions.Contains(item.Profile.ProfileId))
                {
                    _chkListProfiles.SetItemChecked(i, true);
                }
                else
                {
                    _chkListProfiles.SetItemChecked(i, false);
                }
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

        #region Execution Engine

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_isRunning) return;
            var selectedProfiles = GetSelectedProfiles();
            if (selectedProfiles.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để thực hiện tác vụ Google/Gmail!", "Chưa chọn profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selectedTab = _tabs.SelectedIndex;
            int maxThreads = (int)_numThreads.Value;

            _isRunning = true;
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _lblRunningStatus.Text = string.Format("Đang chạy {0} profile...", selectedProfiles.Count);
            _lblRunningStatus.ForeColor = Color.FromArgb(16, 185, 129);

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Log(string.Format("=== [BẮT ĐẦU TÁC VỤ GOOGLE - {0} PROFILE - {1} LUỒNG] ===", selectedProfiles.Count, maxThreads), Color.Cyan);

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
                        Log("=== [HOÀN TẤT TẤT CẢ TÁC VỤ GOOGLE] ===", Color.Green);
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
                    case 0: // Search Seeding
                        string kwd = "";
                        string domain = "";
                        int stay = 30;
                        this.Invoke(new Action(() =>
                        {
                            kwd = _txtSearchKeywords.Text;
                            domain = _txtTargetDomain.Text;
                            stay = (int)_numStaySeconds.Value;
                        }));
                        BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Google: Seeding & Search");
                        _googleService.SearchAndSeedGoogle(driver, kwd, domain, stay, logger);
                        break;

                    case 1: // Login & 2FA
                        string email = "";
                        string pass = "";
                        string secret2fa = "";
                        string recovery = "";
                        this.Invoke(new Action(() =>
                        {
                            email = !string.IsNullOrEmpty(_txtEmail.Text) ? _txtEmail.Text : profile.Username;
                            pass = _txtPassword.Text;
                            secret2fa = _txtTwoFactorSecret.Text;
                            recovery = _txtRecoveryEmail.Text;
                        }));
                        BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Google: Đăng nhập Gmail");
                        _googleService.LoginGoogle(driver, email, pass, secret2fa, recovery, logger);
                        break;

                    case 2: // Read Gmail OTP
                        string senderFilter = "";
                        this.Invoke(new Action(() => { senderFilter = _txtSenderFilter.Text; }));
                        BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Google: Lấy OTP Gmail");
                        string otp = _googleService.ReadGmailOtp(driver, senderFilter, logger);
                        if (!string.IsNullOrEmpty(otp))
                        {
                            this.Invoke(new Action(() => { _txtResultOtp.Text = otp; }));
                        }
                        break;

                    case 3: // YouTube Watch
                        string ytTarget = "";
                        int watchSec = 45;
                        bool autoLike = true;
                        this.Invoke(new Action(() =>
                        {
                            ytTarget = _txtYoutubeTarget.Text;
                            watchSec = (int)_numWatchSeconds.Value;
                            autoLike = _chkAutoLikeYoutube.Checked;
                        }));
                        BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Google: Xem YouTube");
                        _googleService.WatchYouTube(driver, ytTarget, watchSec, autoLike, logger);
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

        private void BtnExtractCookie_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn profile để xuất Cookie Google!", "Chưa chọn profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var profile = selected[0];
            IWebDriver driver = BrowserSessionManager.Instance.GetSession(profile.ProfileId);
            if (driver == null)
            {
                Log(string.Format("[{0}] Đang mở trình duyệt để đọc Cookie...", profile.ProfileName), Color.Yellow);
                driver = _launcherService.LaunchBrowser(profile);
            }

            if (driver != null)
            {
                string cookies = _googleService.ExtractGoogleCookies(driver, (m) => Log(m, Color.LightGray));
                if (!string.IsNullOrEmpty(cookies))
                {
                    Clipboard.SetText(cookies);
                    MessageBox.Show(string.Format("Đã trích xuất Cookie Google thành công vào Clipboard!\n\nSố ký tự: {0}", cookies.Length), "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
