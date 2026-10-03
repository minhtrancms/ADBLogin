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
    public class TikTokAutomationForm : Form
    {
        private CheckedListBox _chkListProfiles;
        private TextBox _txtSearch;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRunning;
        private Label _lblProfileCount;

        // Tab 1: Nuoi nick FYP
        private NumericUpDown _numVideoCount;
        private NumericUpDown _numMinWatch;
        private NumericUpDown _numMaxWatch;
        private NumericUpDown _numLikeRate;
        private CheckBox _chkViewComments;

        // Tab 2: Auto Follow
        private TextBox _txtFollowTarget;
        private NumericUpDown _numFollowDelay;

        // Tab 3: Auto Comment
        private TextBox _txtCommentVideoUrl;
        private TextBox _txtCommentSpintax;

        // Tab 4: Login & Cookies
        private TextBox _txtCookieInput;
        private Button _btnLoginCookie;
        private Button _btnExtractCookie;
        private Button _btnCheckStats;

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
        private readonly TikTokAutomationService _tikTokService = new TikTokAutomationService();
        private readonly BrowserLauncherService _launcherService = new BrowserLauncherService();
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        public TikTokAutomationForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            LoadProfileList();
        }

        private void InitializeComponent()
        {
            this.Text = "🎵 BỘ CÔNG CỤ TỰ ĐỘNG HÓA TIKTOK (TIKTOK AUTOMATION STUDIO)";
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
                Text = "🎵 TIKTOK AUTOMATION & FYP STUDIO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Nuôi nick lướt FYP tự nhiên, Auto Like ngẫu nhiên, Tự động Follow kênh, Bình luận Spintax, Quản lý Cookie SessionID",
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

            _tabs.TabPages.Add(CreateFypTab());
            _tabs.TabPages.Add(CreateFollowTab());
            _tabs.TabPages.Add(CreateCommentTab());
            _tabs.TabPages.Add(CreateLoginCookieTab());
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

        private TabPage CreateFypTab()
        {
            TabPage tab = new TabPage("📱 Nuôi Nick Lướt FYP") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblCount = new Label { Text = "Số lượng video lướt mỗi profile:", Location = new Point(10, 14), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numVideoCount = new NumericUpDown { Location = new Point(220, 12), Width = 60, Minimum = 1, Maximum = 100, Value = 10 };
            Label lblCountUnit = new Label { Text = "video", Location = new Point(286, 14), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };

            Label lblWatch = new Label { Text = "Thời gian xem mỗi video:", Location = new Point(10, 56), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numMinWatch = new NumericUpDown { Location = new Point(170, 54), Width = 55, Minimum = 3, Maximum = 120, Value = 6 };
            Label lblTo = new Label { Text = "đến", Location = new Point(230, 56), AutoSize = true };
            _numMaxWatch = new NumericUpDown { Location = new Point(260, 54), Width = 55, Minimum = 3, Maximum = 300, Value = 16 };
            Label lblWatchUnit = new Label { Text = "giây (ngẫu nhiên)", Location = new Point(322, 56), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };

            Label lblLike = new Label { Text = "Tỷ lệ ngẫu nhiên Thả Tim (Like):", Location = new Point(10, 98), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numLikeRate = new NumericUpDown { Location = new Point(220, 96), Width = 55, Minimum = 0, Maximum = 100, Value = 30 };
            Label lblLikeUnit = new Label { Text = "% (ví dụ 30% nghĩa là xem 10 video sẽ tim ~3 video)", Location = new Point(282, 98), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };

            _chkViewComments = new CheckBox { Text = "Thỉnh thoảng mở xem phần bình luận (tạo hành vi người thật tự nhiên)", Location = new Point(14, 140), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            Label lblNote = new Label
            {
                Text = "💡 Cơ chế: Tự động điều hướng https://www.tiktok.com/foryou, gửi phím Mũi tên xuống để cuộn video,\nngẫu nhiên dừng xem video theo khoảng thời gian thực tế, ngẫu nhiên bấm tim và xem comment.",
                Location = new Point(14, 184),
                AutoSize = true,
                ForeColor = Color.FromArgb(3, 105, 161),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            pnl.Controls.Add(lblNote);
            pnl.Controls.Add(_chkViewComments);
            pnl.Controls.Add(lblLikeUnit);
            pnl.Controls.Add(_numLikeRate);
            pnl.Controls.Add(lblLike);
            pnl.Controls.Add(lblWatchUnit);
            pnl.Controls.Add(_numMaxWatch);
            pnl.Controls.Add(lblTo);
            pnl.Controls.Add(_numMinWatch);
            pnl.Controls.Add(lblWatch);
            pnl.Controls.Add(lblCountUnit);
            pnl.Controls.Add(_numVideoCount);
            pnl.Controls.Add(lblCount);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateFollowTab()
        {
            TabPage tab = new TabPage("➕ Auto Follow Kênh") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblTarget = new Label { Text = "Kênh cần theo dõi (Link profile hoặc @username, ví dụ: @tiktok hoặc https://www.tiktok.com/@creator):", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtFollowTarget = new TextBox { Location = new Point(14, 34), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "@tiktok" };

            Label lblDelay = new Label { Text = "Thời gian nghỉ sau khi follow:", Location = new Point(10, 76), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numFollowDelay = new NumericUpDown { Location = new Point(190, 74), Width = 60, Minimum = 2, Maximum = 60, Value = 5 };
            Label lblDelayUnit = new Label { Text = "giây", Location = new Point(256, 76), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };

            Label lblFollowGuide = new Label
            {
                Text = "💡 Tính năng này giúp kéo Follower cho kênh đích từ dàn tài khoản TikTok trong tool.\nTool tự động kiểm tra nếu đã follow từ trước sẽ tự động bỏ qua để tránh bấm unfollow.",
                Location = new Point(14, 130),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F)
            };

            pnl.Controls.Add(lblFollowGuide);
            pnl.Controls.Add(lblDelayUnit);
            pnl.Controls.Add(_numFollowDelay);
            pnl.Controls.Add(lblDelay);
            pnl.Controls.Add(_txtFollowTarget);
            pnl.Controls.Add(lblTarget);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateCommentTab()
        {
            TabPage tab = new TabPage("💬 Bình luận Spintax") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblVideo = new Label { Text = "Link Video TikTok cần bình luận (URL):", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtCommentVideoUrl = new TextBox { Location = new Point(14, 34), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "https://www.tiktok.com/@tiktok/video/7300000000000000000" };

            Label lblComment = new Label { Text = "Nội dung bình luận (hỗ trợ Spintax đa dạng nội dung tránh trùng lặp):", Location = new Point(10, 76), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtCommentSpintax = new TextBox { Location = new Point(14, 98), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "{Video hay quá bạn ơi|Nội dung tuyệt vời lắm|Quá đỉnh luôn ạ|Cảm ơn bạn đã chia sẻ ♥}" };

            Label lblCommentGuide = new Label
            {
                Text = "💡 Hỗ trợ cú pháp Spintax {A|B|C}: Mỗi profile khi chạy sẽ ngẫu nhiên chọn một vế nội dung\nđể comment, giúp dàn nick comment tự nhiên như người thật.",
                Location = new Point(14, 150),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F)
            };

            pnl.Controls.Add(lblCommentGuide);
            pnl.Controls.Add(_txtCommentSpintax);
            pnl.Controls.Add(lblComment);
            pnl.Controls.Add(_txtCommentVideoUrl);
            pnl.Controls.Add(lblVideo);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateLoginCookieTab()
        {
            TabPage tab = new TabPage("🔑 Đăng nhập & Cookie") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblCookie = new Label { Text = "Chuỗi Cookie TikTok (chứa sessionid=...; ttwid=...;):", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtCookieInput = new TextBox { Location = new Point(14, 34), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F) };

            _btnLoginCookie = new Button
            {
                Text = "🔐 Đăng nhập bằng Cookie",
                Location = new Point(14, 76),
                Width = 190,
                Height = 34,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnLoginCookie.Click += BtnLoginCookie_Click;

            _btnExtractCookie = new Button
            {
                Text = "📥 Xuất Cookie vào Clipboard",
                Location = new Point(212, 76),
                Width = 200,
                Height = 34,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnExtractCookie.Click += BtnExtractCookie_Click;

            _btnCheckStats = new Button
            {
                Text = "🔍 Xem Thống kê Nick",
                Location = new Point(420, 76),
                Width = 160,
                Height = 34,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnCheckStats.Click += BtnCheckStats_Click;

            Label lblCookieGuide = new Label
            {
                Text = "💡 Quản lý phiên TikTok: Nhập chuỗi cookie hoặc xuất cookie hiện tại để đồng bộ sang phần mềm khác.\nNút [Xem Thống kê Nick] sẽ tự động đọc số Follower, Following, Likes của tài khoản.",
                Location = new Point(14, 140),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F)
            };

            pnl.Controls.Add(lblCookieGuide);
            pnl.Controls.Add(_btnCheckStats);
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
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để thực hiện tác vụ TikTok!", "Chưa chọn profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            Log(string.Format("=== [BẮT ĐẦU TÁC VỤ TIKTOK - {0} PROFILE - {1} LUỒNG] ===", selectedProfiles.Count, maxThreads), Color.Cyan);

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
                        Log("=== [HOÀN TẤT TẤT CẢ TÁC VỤ TIKTOK] ===", Color.Green);
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
                    case 0: // FYP Surfing
                        int vCount = 10;
                        int minW = 6;
                        int maxW = 16;
                        int likeRate = 30;
                        bool comments = true;
                        this.Invoke(new Action(() =>
                        {
                            vCount = (int)_numVideoCount.Value;
                            minW = (int)_numMinWatch.Value;
                            maxW = (int)_numMaxWatch.Value;
                            likeRate = (int)_numLikeRate.Value;
                            comments = _chkViewComments.Checked;
                        }));
                        _tikTokService.SurfFyp(driver, vCount, minW, maxW, likeRate, comments, logger);
                        break;

                    case 1: // Follow Creator
                        string followTarget = "";
                        int followDelay = 5;
                        this.Invoke(new Action(() =>
                        {
                            followTarget = _txtFollowTarget.Text;
                            followDelay = (int)_numFollowDelay.Value;
                        }));
                        _tikTokService.FollowUser(driver, followTarget, logger);
                        Thread.Sleep(followDelay * 1000);
                        break;

                    case 2: // Comment Video
                        string vidUrl = "";
                        string commentSpintax = "";
                        this.Invoke(new Action(() =>
                        {
                            vidUrl = _txtCommentVideoUrl.Text;
                            commentSpintax = _txtCommentSpintax.Text;
                        }));
                        _tikTokService.CommentVideo(driver, vidUrl, commentSpintax, logger);
                        break;

                    case 3: // Login via Cookie
                        string cookieStr = "";
                        this.Invoke(new Action(() => { cookieStr = _txtCookieInput.Text; }));
                        if (!string.IsNullOrWhiteSpace(cookieStr))
                        {
                            _tikTokService.LoginWithCookie(driver, cookieStr, logger);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[{0}] Lỗi: {1}", pName, ex.Message), Color.Red);
            }
        }

        private void BtnLoginCookie_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn profile để đăng nhập TikTok bằng Cookie!", "Chưa chọn profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string cookie = _txtCookieInput.Text.Trim();
            if (string.IsNullOrEmpty(cookie))
            {
                MessageBox.Show("Vui lòng nhập chuỗi Cookie TikTok trước!", "Thiếu Cookie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var profile = selected[0];
            IWebDriver driver = BrowserSessionManager.Instance.GetSession(profile.ProfileId);
            if (driver == null)
            {
                Log(string.Format("[{0}] Đang mở trình duyệt...", profile.ProfileName), Color.Yellow);
                driver = _launcherService.LaunchBrowser(profile);
            }

            if (driver != null)
            {
                _tikTokService.LoginWithCookie(driver, cookie, (m) => Log(string.Format("[{0}] {1}", profile.ProfileName, m), Color.LightGray));
            }
        }

        private void BtnExtractCookie_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn profile để xuất Cookie TikTok!", "Chưa chọn profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                string cookies = _tikTokService.ExtractTikTokCookies(driver, (m) => Log(m, Color.LightGray));
                if (!string.IsNullOrEmpty(cookies))
                {
                    Clipboard.SetText(cookies);
                    MessageBox.Show(string.Format("Đã trích xuất Cookie TikTok thành công vào Clipboard!\n\nSố ký tự: {0}", cookies.Length), "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnCheckStats_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn profile để kiểm tra thống kê TikTok!", "Chưa chọn profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var profile = selected[0];
            IWebDriver driver = BrowserSessionManager.Instance.GetSession(profile.ProfileId);
            if (driver == null)
            {
                Log(string.Format("[{0}] Đang mở trình duyệt để kiểm tra...", profile.ProfileName), Color.Yellow);
                driver = _launcherService.LaunchBrowser(profile);
            }

            if (driver != null)
            {
                var stats = _tikTokService.CheckProfileStats(driver, (m) => Log(string.Format("[{0}] {1}", profile.ProfileName, m), Color.LightGray));
                MessageBox.Show(string.Format("Tài khoản: {0}\nFollowers: {1}\nFollowing: {2}\nLikes: {3}\nTrạng thái: {4}",
                    stats.Username ?? "N/A", stats.Followers ?? "N/A", stats.Following ?? "N/A", stats.Likes ?? "N/A", stats.Status), "Thống kê TikTok", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
