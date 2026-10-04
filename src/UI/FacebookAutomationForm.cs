using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
    public class FacebookAutomationForm : Form
    {
        private CheckedListBox _chkListProfiles;
        private TextBox _txtSearch;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRunning;
        private readonly HashSet<string> _selectedProfileIds = new HashSet<string>();

        // Tab 1: Nuoi nick
        private CheckBox _chkSurfFeed;
        private NumericUpDown _numFeedDuration;
        private CheckBox _chkAutoLike;
        private NumericUpDown _numMaxLikes;
        private CheckBox _chkWatchReels;
        private NumericUpDown _numReelCount;
        private CheckBox _chkCheckNotif;

        // Tab 2: Dang nhap & Quan ly acc
        private RadioButton _radLoginCookie;
        private RadioButton _radLoginCreds;
        private TextBox _txtCookieInput;
        private TextBox _txtUsername;
        private TextBox _txtPassword;
        private TextBox _txtTwoFactor;
        private Button _btnCheckLive;
        private Button _btnExtractCookie;
        private Button _btnExtractToken;

        // Tab 3: Seeding
        private TextBox _txtPostUrl;
        private TextBox _txtCommentSpintax;
        private CheckBox _chkJoinGroup;
        private TextBox _txtGroupUrl;

        // Tab 4: Dang bai viet (Auto Post)
        private CheckBox _chkEnablePost;
        private RadioButton _radPostWall;
        private RadioButton _radPostGroup;
        private TextBox _txtPostGroupTarget;
        private RadioButton _radContentManual;
        private RadioButton _radContentApi;
        private Panel _pnlApiSettings;
        private TextBox _txtPostApiUrl;
        private Button _btnTestFetchApi;
        private CheckBox _chkApiFallbackToManual;
        private TextBox _txtAutoPostContent;
        private CheckBox _chkPostAttachImageFromApi;
        private RadioButton _radAllImages;
        private RadioButton _radThumbOnly;
        private Button _btnTestPostSpintax;
        private Button _btnPostNow;

        // Bottom Controls
        private NumericUpDown _numThreads;
        private Button _btnStart;
        private Button _btnStop;
        private RichTextBox _rtbLog;
        private Label _lblProfileCount;
        private Label _lblRunningStatus;
        private Button _btnClearLog;
        private Button _btnCopyLog;
        private SplitContainer _splitMain;
        private SplitContainer _splitRight;

        private readonly List<UserProfile> _allProfiles;
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        public FacebookAutomationForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            LoadProfileList();
        }

        private void InitializeComponent()
        {
            this.Text = "🤖 BỘ CÔNG CỤ TỰ ĐỘNG HÓA FACEBOOK (AUTOMATION SUITE)";
            this.Size = new Size(1220, 800);
            this.MinimumSize = new Size(1020, 650);
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
                Text = "⚡ FACEBOOK AUTOMATION & CDP STUDIO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Nuôi nick tương tác, Đăng nhập 2FA, Check Live UID, Seeding bài viết & Cổng điều khiển Remote DevTools (CDP)",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(15, 30)
            };

            int apiPort = LocalApiService.Instance.Port;
            Panel pnlApiBadge = new Panel
            {
                Dock = DockStyle.Right,
                Width = 270,
                Height = 38,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(8, 4, 8, 4)
            };

            Label lblApiStatus = new Label
            {
                Text = string.Format("🌐 API: 127.0.0.1:{0} (Online)", apiPort),
                ForeColor = Color.FromArgb(52, 211, 153),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(8, 10)
            };

            Button btnCopyApiHeader = new Button
            {
                Text = "📋 Copy",
                Width = 60,
                Height = 24,
                Location = new Point(200, 6),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7.5F)
            };
            btnCopyApiHeader.FlatAppearance.BorderSize = 0;
            btnCopyApiHeader.Click += (s, e) =>
            {
                Clipboard.SetText(string.Format("http://127.0.0.1:{0}", apiPort));
                MessageBox.Show(string.Format("Đã sao chép: http://127.0.0.1:{0}", apiPort), "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            pnlApiBadge.Controls.Add(lblApiStatus);
            pnlApiBadge.Controls.Add(btnCopyApiHeader);

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(pnlApiBadge);

            // ================= 2. MAIN VERTICAL SPLIT (LEFT = PROFILES, RIGHT = WORKSPACE) =================
            _splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6
            };

            // ---------- LEFT PANEL: Profile Selection ----------
            Panel pnlLeft = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(248, 250, 252)
            };

            Panel pnlProfileTitle = new Panel { Dock = DockStyle.Top, Height = 28 };
            Label lblProfiles = new Label
            {
                Text = "📋 PROFILES ÁP DỤNG",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Dock = DockStyle.Left,
                AutoSize = true
            };
            _lblProfileCount = new Label
            {
                Text = "0/0 profiles",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Dock = DockStyle.Right,
                AutoSize = true
            };
            pnlProfileTitle.Controls.Add(lblProfiles);
            pnlProfileTitle.Controls.Add(_lblProfileCount);

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 4, 0, 4),
                Text = "🔍 Tìm kiếm profile...",
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            _txtSearch.GotFocus += (s, e) =>
            {
                if (_txtSearch.Text == "🔍 Tìm kiếm profile...")
                {
                    _txtSearch.Text = "";
                    _txtSearch.ForeColor = Color.FromArgb(30, 41, 59);
                }
            };
            _txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "🔍 Tìm kiếm profile...";
                    _txtSearch.ForeColor = Color.FromArgb(100, 116, 139);
                }
            };
            _txtSearch.TextChanged += (s, e) => FilterProfileList();

            Panel pnlSelectButtons = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 4, 0, 4) };
            _btnSelectAll = new Button { Text = "Chọn tất", Width = 72, Height = 26, Location = new Point(0, 4), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F), BackColor = Color.FromArgb(241, 245, 249) };
            _btnSelectAll.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnSelectAll.Click += (s, e) => SetAllChecked(true);

            _btnDeselectAll = new Button { Text = "Bỏ chọn", Width = 72, Height = 26, Location = new Point(78, 4), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F), BackColor = Color.FromArgb(241, 245, 249) };
            _btnDeselectAll.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnDeselectAll.Click += (s, e) => SetAllChecked(false);

            _btnSelectRunning = new Button { Text = "Chỉ đang mở", Width = 95, Height = 26, Location = new Point(156, 4), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F), BackColor = Color.FromArgb(224, 242, 254), ForeColor = Color.FromArgb(2, 132, 199) };
            _btnSelectRunning.FlatAppearance.BorderColor = Color.FromArgb(186, 230, 253);
            _btnSelectRunning.Click += (s, e) => SelectRunningProfiles();

            pnlSelectButtons.Controls.Add(_btnSelectAll);
            pnlSelectButtons.Controls.Add(_btnDeselectAll);
            pnlSelectButtons.Controls.Add(_btnSelectRunning);

            _chkListProfiles = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            _chkListProfiles.ItemCheck += (s, e) =>
            {
                var item = _chkListProfiles.Items[e.Index] as ProfileItem;
                if (item != null)
                {
                    if (e.NewValue == CheckState.Checked)
                        _selectedProfileIds.Add(item.Profile.ProfileId);
                    else
                        _selectedProfileIds.Remove(item.Profile.ProfileId);
                }
                this.BeginInvoke(new Action(UpdateProfileCountLabel));
            };

            pnlLeft.Controls.Add(_chkListProfiles);
            pnlLeft.Controls.Add(pnlSelectButtons);
            pnlLeft.Controls.Add(_txtSearch);
            pnlLeft.Controls.Add(pnlProfileTitle);
            _splitMain.Panel1.Controls.Add(pnlLeft);

            // ================= 3. RIGHT PANEL: HORIZONTAL SPLIT (TOP = TABS, BOTTOM = LOG & ACTIONS) =================
            _splitRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6
            };

            // ---------- UPPER PANEL: Configuration Tabs ----------
            TabControl tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Padding = new Point(14, 6)
            };

            // ===== TAB 1: Nuôi nick & Tương tác =====
            TabPage tabFarming = new TabPage("🌟 Nuôi Nick & Tương Tác");
            tabFarming.BackColor = Color.White;
            tabFarming.AutoScroll = true;
            tabFarming.Padding = new Padding(12);

            TableLayoutPanel tlpFarming = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(4)
            };
            tlpFarming.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpFarming.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // Card 1: Bảng tin
            GroupBox grpFeed = new GroupBox
            {
                Text = "📰 Tương Tác Bảng Tin (Newsfeed)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(14)
            };

            _chkSurfFeed = new CheckBox { Text = "Lướt Newsfeed tự nhiên (cuộn mượt, dừng đọc ngẫu nhiên)", Location = new Point(14, 28), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            
            Panel pnlFeedSec = new Panel { Location = new Point(36, 58), Size = new Size(340, 28) };
            Label lblFeedSec = new Label { Text = "Thời gian lướt:", Location = new Point(0, 4), AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _numFeedDuration = new NumericUpDown { Location = new Point(100, 2), Width = 65, Minimum = 10, Maximum = 600, Value = 60, Font = new Font("Segoe UI", 9F) };
            Label lblSecUnit = new Label { Text = "giây / profile (Gợi ý: 60s)", Location = new Point(172, 4), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5F) };
            pnlFeedSec.Controls.Add(lblFeedSec);
            pnlFeedSec.Controls.Add(_numFeedDuration);
            pnlFeedSec.Controls.Add(lblSecUnit);

            _chkAutoLike = new CheckBox { Text = "Thả cảm xúc ngẫu nhiên khi lướt bài (Like/Love/Care)", Location = new Point(14, 98), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            
            Panel pnlLikeCount = new Panel { Location = new Point(36, 128), Size = new Size(340, 28) };
            Label lblLikeCount = new Label { Text = "Tối đa like:", Location = new Point(0, 4), AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _numMaxLikes = new NumericUpDown { Location = new Point(100, 2), Width = 65, Minimum = 1, Maximum = 50, Value = 3, Font = new Font("Segoe UI", 9F) };
            Label lblLikeUnit = new Label { Text = "bài viết / phiên", Location = new Point(172, 4), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5F) };
            pnlLikeCount.Controls.Add(lblLikeCount);
            pnlLikeCount.Controls.Add(_numMaxLikes);
            pnlLikeCount.Controls.Add(lblLikeUnit);

            Label lblFeedHint = new Label
            {
                Text = "💡 Hành vi cuộn trang mượt, dừng ngẫu nhiên 3-6s và thỉnh thoảng cuộn ngược lại giúp vượt qua bộ lọc quét của Meta.",
                Location = new Point(14, 170),
                Size = new Size(360, 45),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8F)
            };

            grpFeed.Controls.Add(_chkSurfFeed);
            grpFeed.Controls.Add(pnlFeedSec);
            grpFeed.Controls.Add(_chkAutoLike);
            grpFeed.Controls.Add(pnlLikeCount);
            grpFeed.Controls.Add(lblFeedHint);

            // Card 2: Reels & Trust
            GroupBox grpReels = new GroupBox
            {
                Text = "🎬 Video, Reels & Tăng Trust",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(14)
            };

            _chkWatchReels = new CheckBox { Text = "Xem video ngắn Reels / Watch", Location = new Point(14, 28), AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            
            Panel pnlReelCount = new Panel { Location = new Point(36, 58), Size = new Size(340, 28) };
            Label lblReel = new Label { Text = "Số lượng Reels:", Location = new Point(0, 4), AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _numReelCount = new NumericUpDown { Location = new Point(110, 2), Width = 65, Minimum = 1, Maximum = 20, Value = 3, Font = new Font("Segoe UI", 9F) };
            Label lblReelUnit = new Label { Text = "video (Dừng 10-25s)", Location = new Point(182, 4), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5F) };
            pnlReelCount.Controls.Add(lblReel);
            pnlReelCount.Controls.Add(_numReelCount);
            pnlReelCount.Controls.Add(lblReelUnit);

            _chkCheckNotif = new CheckBox { Text = "Đọc và mở tab Thông báo (Tăng trust tài khoản)", Location = new Point(14, 98), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };

            Label lblReelHint = new Label
            {
                Text = "💡 Việc phân bổ xem Reels và đọc Thông báo tạo luồng hoạt động tự nhiên, tránh bị nghi ngờ là bot tự động.",
                Location = new Point(14, 170),
                Size = new Size(360, 45),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8F)
            };

            grpReels.Controls.Add(_chkWatchReels);
            grpReels.Controls.Add(pnlReelCount);
            grpReels.Controls.Add(_chkCheckNotif);
            grpReels.Controls.Add(lblReelHint);

            tlpFarming.Controls.Add(grpFeed, 0, 0);
            tlpFarming.Controls.Add(grpReels, 1, 0);
            tabFarming.Controls.Add(tlpFarming);
            tabs.TabPages.Add(tabFarming);

            // ===== TAB 2: Đăng nhập & Quản lý Acc =====
            TabPage tabLogin = new TabPage("🔑 Đăng Nhập & Quản Lý Acc");
            tabLogin.BackColor = Color.White;
            tabLogin.AutoScroll = true;
            tabLogin.Padding = new Padding(12);

            GroupBox grpLogin = new GroupBox
            {
                Text = "Phương Thức Đăng Nhập Tự Động",
                Dock = DockStyle.Top,
                Height = 195,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(14)
            };

            _radLoginCookie = new RadioButton { Text = "Đăng nhập bằng Cookie (c_user=...; xs=...)", Location = new Point(14, 24), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtCookieInput = new TextBox
            {
                Location = new Point(14, 48),
                Height = 26,
                Width = 720,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F)
            };

            _radLoginCreds = new RadioButton { Text = "Đăng nhập bằng Tài khoản | Mật khẩu | 2FA Secret Key (Tự giải mã OTP):", Location = new Point(14, 88), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            TableLayoutPanel tlpCreds = new TableLayoutPanel
            {
                Location = new Point(14, 114),
                Height = 60,
                Width = 720,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 3,
                RowCount = 1
            };
            tlpCreds.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            tlpCreds.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            tlpCreds.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));

            Panel pnlU = new Panel { Dock = DockStyle.Fill };
            Label lblU = new Label { Text = "Tài khoản / UID:", Location = new Point(0, 0), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _txtUsername = new TextBox { Location = new Point(0, 20), Dock = DockStyle.Bottom, Height = 25, Font = new Font("Segoe UI", 9F) };
            pnlU.Controls.Add(lblU);
            pnlU.Controls.Add(_txtUsername);

            Panel pnlP = new Panel { Dock = DockStyle.Fill };
            Label lblP = new Label { Text = "Mật khẩu:", Location = new Point(4, 0), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _txtPassword = new TextBox { Location = new Point(4, 20), Dock = DockStyle.Bottom, Height = 25, PasswordChar = '•', Font = new Font("Segoe UI", 9F) };
            pnlP.Controls.Add(lblP);
            pnlP.Controls.Add(_txtPassword);

            Panel pnl2FA = new Panel { Dock = DockStyle.Fill };
            Label lbl2FA = new Label { Text = "2FA Secret Key (Tự sinh OTP):", Location = new Point(4, 0), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _txtTwoFactor = new TextBox { Location = new Point(4, 20), Dock = DockStyle.Bottom, Height = 25, Font = new Font("Segoe UI", 9F) };
            pnl2FA.Controls.Add(lbl2FA);
            pnl2FA.Controls.Add(_txtTwoFactor);

            tlpCreds.Controls.Add(pnlU, 0, 0);
            tlpCreds.Controls.Add(pnlP, 1, 0);
            tlpCreds.Controls.Add(pnl2FA, 2, 0);

            grpLogin.Controls.Add(_radLoginCookie);
            grpLogin.Controls.Add(_txtCookieInput);
            grpLogin.Controls.Add(_radLoginCreds);
            grpLogin.Controls.Add(tlpCreds);

            GroupBox grpQuickActions = new GroupBox
            {
                Text = "Thao Tác Nhanh Trên Profile Đang Chọn",
                Dock = DockStyle.Top,
                Height = 84,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(14)
            };

            FlowLayoutPanel flpQuickActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            _btnCheckLive = new Button { Text = "🔍 Check Live / Die / Checkpoint", Width = 210, Height = 34, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnCheckLive.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnCheckLive.Click += BtnCheckLive_Click;

            _btnExtractCookie = new Button { Text = "📥 Xuất Cookie vào Clipboard", Width = 190, Height = 34, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnExtractCookie.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnExtractCookie.Click += BtnExtractCookie_Click;

            _btnExtractToken = new Button { Text = "🔑 Xuất Token EAAB", Width = 160, Height = 34, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnExtractToken.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnExtractToken.Click += BtnExtractToken_Click;

            flpQuickActions.Controls.Add(_btnCheckLive);
            flpQuickActions.Controls.Add(_btnExtractCookie);
            flpQuickActions.Controls.Add(_btnExtractToken);
            grpQuickActions.Controls.Add(flpQuickActions);

            tabLogin.Controls.Add(grpQuickActions);
            tabLogin.Controls.Add(grpLogin);
            tabs.TabPages.Add(tabLogin);

            // ===== TAB 3: Seeding & Nhóm =====
            TabPage tabSeeding = new TabPage("💬 Seeding & Nhóm");
            tabSeeding.BackColor = Color.White;
            tabSeeding.AutoScroll = true;
            tabSeeding.Padding = new Padding(12);

            GroupBox grpPost = new GroupBox
            {
                Text = "Seeding Bình Luận Bài Viết",
                Dock = DockStyle.Top,
                Height = 185,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(14)
            };

            Label lblPostUrl = new Label { Text = "Link bài viết Facebook cần bình luận / seeding:", Location = new Point(14, 24), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _txtPostUrl = new TextBox { Location = new Point(14, 46), Height = 26, Width = 720, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 9F) };

            Label lblComment = new Label { Text = "Nội dung bình luận (Hỗ trợ Spintax dạng {Hay quá|Tuyệt vời|Đẹp thế shop}):", Location = new Point(14, 80), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _txtCommentSpintax = new TextBox
            {
                Location = new Point(14, 102),
                Height = 65,
                Width = 720,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F),
                Text = "{Sản phẩm tuyệt vời|Quá đẹp luôn ạ|Shop tư vấn nhiệt tình nha|10 điểm cho chất lượng}"
            };

            grpPost.Controls.Add(lblPostUrl);
            grpPost.Controls.Add(_txtPostUrl);
            grpPost.Controls.Add(lblComment);
            grpPost.Controls.Add(_txtCommentSpintax);

            GroupBox grpGroup = new GroupBox
            {
                Text = "Tương Tác Nhóm (Facebook Group)",
                Dock = DockStyle.Top,
                Height = 85,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(14)
            };

            _chkJoinGroup = new CheckBox { Text = "Tham gia nhóm theo link:", Location = new Point(14, 30), AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtGroupUrl = new TextBox { Location = new Point(195, 28), Height = 26, Width = 535, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 9F) };

            grpGroup.Controls.Add(_chkJoinGroup);
            grpGroup.Controls.Add(_txtGroupUrl);

            tabSeeding.Controls.Add(grpGroup);
            tabSeeding.Controls.Add(grpPost);
            tabs.TabPages.Add(tabSeeding);
            tabs.TabPages.Add(CreatePostTab());

            // ===== TAB 5: Cổng CDP & Local API Studio =====
            TabPage tabCdp = new TabPage("🌐 Cổng CDP & Local API");
            tabCdp.BackColor = Color.White;
            tabCdp.AutoScroll = true;
            tabCdp.Padding = new Padding(14);

            GroupBox grpCdp = new GroupBox
            {
                Text = "Local REST API & Remote Chrome DevTools Protocol (CDP)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(14)
            };

            Label lblApiBanner = new Label
            {
                Text = string.Format("⚡ Local API Server đang hoạt động tại: http://127.0.0.1:{0}", apiPort),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(14, 28),
                AutoSize = true
            };

            Label lblApiInfo = new Label
            {
                Text = "Bạn có thể điều khiển trực tiếp ADBLogin bằng Python / Node.js / Playwright / Selenium qua các endpoint:\n\n" +
                       string.Format("  • GET  http://127.0.0.1:{0}/api/profiles         -> Lấy danh sách profiles kèm trạng thái & CDP port\n", apiPort) +
                       string.Format("  • GET  http://127.0.0.1:{0}/api/profile/start?id=ID  -> Khởi chạy profile và nhận cdp_port\n", apiPort) +
                       string.Format("  • GET  http://127.0.0.1:{0}/api/profile/stop?id=ID   -> Đóng profile đang mở\n", apiPort) +
                       string.Format("  • GET  http://127.0.0.1:{0}/api/fb/check_uid?uid=ID  -> Check nhanh UID Live/Die không cần mở browser", apiPort),
                Location = new Point(14, 58),
                Size = new Size(720, 115),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 9F),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            FlowLayoutPanel flpCdpButtons = new FlowLayoutPanel
            {
                Location = new Point(14, 185),
                Size = new Size(720, 45),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                FlowDirection = FlowDirection.LeftToRight
            };

            Button btnOpenPythonFolder = new Button
            {
                Text = "📁 Thư mục code mẫu Python (Playwright / Selenium)",
                Width = 320,
                Height = 34,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnOpenPythonFolder.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnOpenPythonFolder.Click += (s, e) =>
            {
                string pyDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "automation", "python");
                if (Directory.Exists(pyDir)) Process.Start("explorer.exe", pyDir);
            };

            Button btnTestApi = new Button
            {
                Text = "🔗 Mở /api/profiles trên trình duyệt",
                Width = 220,
                Height = 34,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnTestApi.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnTestApi.Click += (s, e) => Process.Start(string.Format("http://127.0.0.1:{0}/api/profiles", apiPort));

            Button btnCopyApi = new Button
            {
                Text = "📋 Copy API Base URL",
                Width = 150,
                Height = 34,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnCopyApi.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnCopyApi.Click += (s, e) =>
            {
                Clipboard.SetText(string.Format("http://127.0.0.1:{0}", apiPort));
                MessageBox.Show(string.Format("Đã sao chép: http://127.0.0.1:{0}", apiPort), "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            flpCdpButtons.Controls.Add(btnOpenPythonFolder);
            flpCdpButtons.Controls.Add(btnTestApi);
            flpCdpButtons.Controls.Add(btnCopyApi);

            grpCdp.Controls.Add(lblApiBanner);
            grpCdp.Controls.Add(lblApiInfo);
            grpCdp.Controls.Add(flpCdpButtons);
            tabCdp.Controls.Add(grpCdp);
            tabs.TabPages.Add(tabCdp);

            _splitRight.Panel1.Controls.Add(tabs);

            // ---------- LOWER PANEL: Log Console & Action Bar ----------
            Panel pnlLogContainer = new Panel { Dock = DockStyle.Fill };

            Panel pnlLogToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.FromArgb(241, 245, 249),
                Padding = new Padding(8, 4, 8, 4)
            };

            Label lblLogTitle = new Label
            {
                Text = "⚡ NHẬT KÝ THỰC THI (EXECUTION LOG)",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                Dock = DockStyle.Left,
                AutoSize = true
            };

            _btnClearLog = new Button
            {
                Text = "🧹 Xóa log",
                Width = 75,
                Height = 22,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F),
                BackColor = Color.White
            };
            _btnClearLog.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnClearLog.Click += (s, e) => _rtbLog.Clear();

            _btnCopyLog = new Button
            {
                Text = "📋 Copy log",
                Width = 80,
                Height = 22,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F),
                BackColor = Color.White
            };
            _btnCopyLog.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnCopyLog.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_rtbLog.Text))
                {
                    Clipboard.SetText(_rtbLog.Text);
                    MessageBox.Show("Đã sao chép toàn bộ log!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            pnlLogToolbar.Controls.Add(lblLogTitle);
            pnlLogToolbar.Controls.Add(_btnCopyLog);
            pnlLogToolbar.Controls.Add(_btnClearLog);

            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.None,
                ReadOnly = true
            };

            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };

            Label lblThread = new Label
            {
                Text = "Số luồng:",
                Location = new Point(12, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };
            _numThreads = new NumericUpDown
            {
                Location = new Point(80, 14),
                Width = 50,
                Minimum = 1,
                Maximum = 10,
                Value = 2,
                Font = new Font("Segoe UI", 9F)
            };

            _btnStart = new Button
            {
                Text = "▶ BẮT ĐẦU CHẠY",
                Location = new Point(145, 9),
                Width = 155,
                Height = 35,
                BackColor = Color.FromArgb(24, 119, 242),
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
                Location = new Point(310, 9),
                Width = 115,
                Height = 35,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnStop.FlatAppearance.BorderSize = 0;
            _btnStop.Click += BtnStop_Click;

            _lblRunningStatus = new Label
            {
                Text = "● Sẵn sàng",
                Location = new Point(440, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(5, 150, 105)
            };

            pnlBottom.Controls.Add(lblThread);
            pnlBottom.Controls.Add(_numThreads);
            pnlBottom.Controls.Add(_btnStart);
            pnlBottom.Controls.Add(_btnStop);
            pnlBottom.Controls.Add(_lblRunningStatus);

            pnlLogContainer.Controls.Add(_rtbLog);
            pnlLogContainer.Controls.Add(pnlLogToolbar);
            pnlLogContainer.Controls.Add(pnlBottom);
            _splitRight.Panel2.Controls.Add(pnlLogContainer);

            _splitMain.Panel2.Controls.Add(_splitRight);

            this.Controls.Add(_splitMain);
            this.Controls.Add(pnlHeader);

            Log("Hệ thống Automation Facebook sẵn sàng. Vui lòng chọn profile và cấu hình kịch bản.");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                if (_splitMain != null && _splitMain.Width > 500)
                {
                    int target = Math.Min(310, _splitMain.Width - 250);
                    if (target > 50) _splitMain.SplitterDistance = target;
                }
                if (_splitRight != null && _splitRight.Height > 400)
                {
                    int target = Math.Min(360, _splitRight.Height - 150);
                    if (target > 50) _splitRight.SplitterDistance = target;
                }
            }
            catch { }
        }

        #region Create Tabs

        private TabPage CreatePostTab()
        {
            TabPage tab = new TabPage("📝 Đăng Bài Viết") { BackColor = Color.White, AutoScroll = true };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            // Group 1: Vị trí đăng bài
            GroupBox grpTarget = new GroupBox
            {
                Text = "1. Vị Trí Đăng Bài Viết",
                Dock = DockStyle.Top,
                Height = 85,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(12)
            };

            _radPostWall = new RadioButton { Text = "Tường cá nhân (Newsfeed / Timeline)", Location = new Point(14, 24), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            _radPostGroup = new RadioButton { Text = "Nhóm Facebook (Group)", Location = new Point(270, 24), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            
            Label lblGroupTarget = new Label { Text = "Link hoặc ID nhóm:", Location = new Point(14, 52), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular), ForeColor = Color.Gray };
            _txtPostGroupTarget = new TextBox { Location = new Point(135, 49), Width = 560, Height = 24, Font = new Font("Segoe UI", 9F), Enabled = false };
            
            _radPostGroup.CheckedChanged += (s, e) => { _txtPostGroupTarget.Enabled = _radPostGroup.Checked; };

            grpTarget.Controls.Add(lblGroupTarget);
            grpTarget.Controls.Add(_txtPostGroupTarget);
            grpTarget.Controls.Add(_radPostGroup);
            grpTarget.Controls.Add(_radPostWall);

            // Group 2: Nội dung bài viết
            GroupBox grpContent = new GroupBox
            {
                Text = "2. Nội Dung Bài Viết (Hỗ trợ Spintax {A|B|C} hoặc Lấy từ API tự động)",
                Dock = DockStyle.Top,
                Height = 270,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(12)
            };

            _radContentManual = new RadioButton { Text = "✍️ Soạn nội dung thủ công / Spintax", Location = new Point(14, 22), AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            _radContentApi = new RadioButton { Text = "🌐 Lấy nội dung tự động từ API Endpoint", Location = new Point(270, 22), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(24, 119, 242) };

            _pnlApiSettings = new Panel
            {
                Location = new Point(14, 48),
                Width = 710,
                Height = 58,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Enabled = true
            };

            Label lblApiUrl = new Label { Text = "URL API Endpoint (GET):", Location = new Point(0, 5), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105) };
            _txtPostApiUrl = new TextBox { Location = new Point(155, 2), Width = 385, Height = 24, Font = new Font("Segoe UI", 9F), Text = "https://blog.shin520.org/api/v1/feed/facebook" };
            _btnTestFetchApi = new Button
            {
                Text = "⚡ Lấy thử từ API",
                Location = new Point(548, 1),
                Width = 150,
                Height = 26,
                BackColor = Color.FromArgb(238, 242, 255),
                ForeColor = Color.FromArgb(79, 70, 229),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnTestFetchApi.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 254);

            _chkApiFallbackToManual = new CheckBox
            {
                Text = "Dùng nội dung thủ công bên dưới nếu API bị lỗi / mất mạng",
                Location = new Point(0, 32),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            Label lblApiTip = new Label
            {
                Text = "💡 Tự động đọc Plain Text hoặc JSON (content, text, post, quote, caption, image...)",
                Location = new Point(345, 33),
                AutoSize = true,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(148, 163, 184)
            };

            _pnlApiSettings.Controls.Add(lblApiTip);
            _pnlApiSettings.Controls.Add(_chkApiFallbackToManual);
            _pnlApiSettings.Controls.Add(_btnTestFetchApi);
            _pnlApiSettings.Controls.Add(_txtPostApiUrl);
            _pnlApiSettings.Controls.Add(lblApiUrl);

            _radContentApi.CheckedChanged += (s, e) =>
            {
                _pnlApiSettings.Enabled = _radContentApi.Checked;
                if (_radContentApi.Checked) _txtPostApiUrl.Focus();
            };

            Label lblManualContent = new Label { Text = "Nội dung bài viết (hoặc dự phòng khi dùng API):", Location = new Point(14, 110), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular), ForeColor = Color.FromArgb(71, 85, 105) };

            _txtAutoPostContent = new TextBox
            {
                Location = new Point(14, 130),
                Width = 710,
                Height = 85,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F),
                Text = "{Chào mọi người|Hello cả nhà|Chúc anh em ngày mới tốt lành}! {Hôm nay mình chia sẻ thông tin hữu ích này|Mọi người cùng xem qua nhé|Cập nhật tin tức mới nhất}. Chúc mọi người luôn thành công! #facebook #adblogin"
            };

            _btnTestPostSpintax = new Button
            {
                Text = "🎲 Thử Spintax ngẫu nhiên",
                Location = new Point(14, 224),
                Width = 180,
                Height = 28,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnTestPostSpintax.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnTestPostSpintax.Click += (s, e) =>
            {
                string sample = SpintaxHelper.Process(_txtAutoPostContent.Text);
                MessageBox.Show(sample, "Xem trước kết quả Spintax", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnClearPostContent = new Button
            {
                Text = "🧹 Xóa trắng",
                Location = new Point(202, 224),
                Width = 100,
                Height = 28,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F)
            };
            btnClearPostContent.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnClearPostContent.Click += (s, e) => { _txtAutoPostContent.Clear(); };

            _btnTestFetchApi.Click += async (s, e) =>
            {
                string url = _txtPostApiUrl.Text.Trim();
                if (string.IsNullOrEmpty(url))
                {
                    MessageBox.Show("Vui lòng nhập URL Endpoint API cần test!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _btnTestFetchApi.Enabled = false;
                _btnTestFetchApi.Text = "⏳ Đang gọi API...";

                try
                {
                    FacebookAutomationService.ApiPostResult apiResult = null;
                    await Task.Run(() =>
                    {
                        var fb = new FacebookAutomationService();
                        apiResult = fb.FetchPostFromApi(url, m => Log(m));
                    });

                    if (apiResult != null && apiResult.Success)
                    {
                        string preview = string.Format("✅ LẤY BÀI VIẾT TỪ API THÀNH CÔNG!\n\nNội dung ({0} ký tự):\n\"{1}\"",
                            apiResult.Content.Length,
                            apiResult.Content.Length > 200 ? apiResult.Content.Substring(0, 200) + "..." : apiResult.Content);

                        if (apiResult.ImageUrls.Count > 0)
                        {
                            preview += string.Format("\n\n🖼️ Hình ảnh đính kèm từ API ({0} ảnh):\n- {1}",
                                apiResult.ImageUrls.Count, string.Join("\n- ", apiResult.ImageUrls.ToArray()));
                        }

                        var dr = MessageBox.Show(preview + "\n\nBạn có muốn nạp nội dung này vào ô soạn thảo bên dưới để xem đầy đủ không?",
                            "Kết quả test API", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                        if (dr == DialogResult.Yes)
                        {
                            _txtAutoPostContent.Text = apiResult.Content;
                        }
                    }
                    else
                    {
                        MessageBox.Show("❌ Không thể lấy bài viết từ API!\n\nChi tiết lỗi: " + (apiResult != null ? apiResult.ErrorMessage : "Không rõ lỗi"),
                            "Lỗi API", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi gọi API: " + ex.Message, "Lỗi API", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _btnTestFetchApi.Enabled = true;
                    _btnTestFetchApi.Text = "⚡ Lấy thử từ API";
                }
            };

            grpContent.Controls.Add(btnClearPostContent);
            grpContent.Controls.Add(_btnTestPostSpintax);
            grpContent.Controls.Add(_txtAutoPostContent);
            grpContent.Controls.Add(lblManualContent);
            grpContent.Controls.Add(_pnlApiSettings);
            grpContent.Controls.Add(_radContentApi);
            grpContent.Controls.Add(_radContentManual);

            // Group 3: Hình ảnh đính kèm từ API (không dùng ảnh từ máy tính)
            GroupBox grpImage = new GroupBox
            {
                Text = "3. Hình Ảnh Đăng Kèm (Tự động lấy link ảnh từ API)",
                Dock = DockStyle.Top,
                Height = 85,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(12)
            };

            _chkPostAttachImageFromApi = new CheckBox
            {
                Text = "🖼️ Tự động đính kèm hình ảnh từ API vào bài viết Facebook",
                Location = new Point(14, 24),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129)
            };

            _radAllImages = new RadioButton { Text = "Tất cả ảnh từ bài viết (Album ảnh)", Location = new Point(14, 52), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };
            _radThumbOnly = new RadioButton { Text = "Chỉ ảnh đại diện (Thumbnail)", Location = new Point(260, 52), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };

            Label lblImageApiNote = new Label
            {
                Text = "💡 Hệ thống tự động đọc link ảnh từ API (mảng images hoặc thumbnail), tải về máy tạm và nạp trực tiếp lên Facebook.",
                Location = new Point(450, 54),
                AutoSize = true,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            _chkPostAttachImageFromApi.CheckedChanged += (s, e) =>
            {
                _radAllImages.Enabled = _chkPostAttachImageFromApi.Checked;
                _radThumbOnly.Enabled = _chkPostAttachImageFromApi.Checked;
            };

            grpImage.Controls.Add(lblImageApiNote);
            grpImage.Controls.Add(_radThumbOnly);
            grpImage.Controls.Add(_radAllImages);
            grpImage.Controls.Add(_chkPostAttachImageFromApi);

            // Group 4: Thực thi
            GroupBox grpAction = new GroupBox
            {
                Text = "4. Cấu Hình Thực Thi",
                Dock = DockStyle.Top,
                Height = 85,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(12)
            };

            _chkEnablePost = new CheckBox { Text = "Tự động đăng bài khi bấm nút [BẮT ĐẦU CHẠY]", Location = new Point(14, 25), AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(24, 119, 242) };
            
            _btnPostNow = new Button
            {
                Text = "🚀 ĐĂNG BÀI NGAY CHO CÁC PROFILE ĐÃ CHỌN",
                Location = new Point(14, 48),
                Width = 400,
                Height = 30,
                BackColor = Color.FromArgb(24, 119, 242),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnPostNow.FlatAppearance.BorderSize = 0;
            _btnPostNow.Click += (s, e) =>
            {
                _chkEnablePost.Checked = true;
                BtnStart_Click(s, e);
            };

            grpAction.Controls.Add(_btnPostNow);
            grpAction.Controls.Add(_chkEnablePost);

            pnl.Controls.Add(grpAction);
            pnl.Controls.Add(grpImage);
            pnl.Controls.Add(grpContent);
            pnl.Controls.Add(grpTarget);

            tab.Controls.Add(pnl);
            return tab;
        }

        #endregion

        private void UpdateProfileCountLabel()
        {
            if (_lblProfileCount != null)
            {
                _lblProfileCount.Text = string.Format("Đã chọn: {0}/{1}", _selectedProfileIds.Count, _allProfiles.Count);
            }
        }

        private void LoadProfileList()
        {
            _chkListProfiles.Items.Clear();
            foreach (var p in _allProfiles)
            {
                bool running = BrowserSessionManager.Instance.IsRunning(p.ProfileId);
                string display = string.Format("{0} [{1}]", p.ProfileName, running ? "RUNNING" : "STOP");
                int idx = _chkListProfiles.Items.Add(new ProfileItem { Profile = p, Display = display });
                if (_selectedProfileIds.Contains(p.ProfileId))
                {
                    _chkListProfiles.SetItemChecked(idx, true);
                }
            }
            UpdateProfileCountLabel();
        }

        private void FilterProfileList()
        {
            string q = _txtSearch.Text.Trim();
            if (q.StartsWith("🔍")) q = "";
            q = q.ToLowerInvariant();

            _chkListProfiles.BeginUpdate();
            _chkListProfiles.Items.Clear();

            foreach (var p in _allProfiles)
            {
                bool match = string.IsNullOrEmpty(q) ||
                    (!string.IsNullOrEmpty(p.ProfileName) && p.ProfileName.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(p.Username) && p.Username.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(p.ProfileId) && p.ProfileId.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(p.Proxy) && p.Proxy.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(p.Notes) && p.Notes.ToLowerInvariant().Contains(q));

                if (match)
                {
                    bool running = BrowserSessionManager.Instance.IsRunning(p.ProfileId);
                    string display = string.Format("{0} [{1}]", p.ProfileName, running ? "RUNNING" : "STOP");
                    int idx = _chkListProfiles.Items.Add(new ProfileItem { Profile = p, Display = display });
                    if (_selectedProfileIds.Contains(p.ProfileId))
                    {
                        _chkListProfiles.SetItemChecked(idx, true);
                    }
                }
            }
            _chkListProfiles.EndUpdate();
            UpdateProfileCountLabel();
        }

        private void SetAllChecked(bool isChecked)
        {
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                var item = _chkListProfiles.Items[i] as ProfileItem;
                if (item != null)
                {
                    if (isChecked) _selectedProfileIds.Add(item.Profile.ProfileId);
                    else _selectedProfileIds.Remove(item.Profile.ProfileId);
                }
                _chkListProfiles.SetItemChecked(i, isChecked);
            }
            UpdateProfileCountLabel();
        }

        private void SelectRunningProfiles()
        {
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                var item = _chkListProfiles.Items[i] as ProfileItem;
                if (item != null)
                {
                    bool running = BrowserSessionManager.Instance.IsRunning(item.Profile.ProfileId);
                    if (running) _selectedProfileIds.Add(item.Profile.ProfileId);
                    else _selectedProfileIds.Remove(item.Profile.ProfileId);
                    _chkListProfiles.SetItemChecked(i, running);
                }
            }
            UpdateProfileCountLabel();
        }

        private List<UserProfile> GetSelectedProfiles()
        {
            var list = new List<UserProfile>();
            foreach (var p in _allProfiles)
            {
                if (_selectedProfileIds.Contains(p.ProfileId))
                {
                    list.Add(p);
                }
            }
            return list;
        }

        private void Log(string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => Log(msg)));
                return;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            _rtbLog.AppendText(string.Format("[{0}] {1}\n", time, msg));
            _rtbLog.SelectionStart = _rtbLog.Text.Length;
            _rtbLog.ScrollToCaret();
        }

        #region Thao tác Nút Bấm

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            var selectedProfiles = GetSelectedProfiles();
            if (selectedProfiles.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để thực hiện kịch bản!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _isRunning = true;
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _cts = new CancellationTokenSource();

            if (_lblRunningStatus != null)
            {
                _lblRunningStatus.Text = string.Format("● Đang chạy ({0} profiles)...", selectedProfiles.Count);
                _lblRunningStatus.ForeColor = Color.FromArgb(24, 119, 242);
            }

            int threadCount = (int)_numThreads.Value;
            Log(string.Format("=== BẮT ĐẦU CHẠY KỊCH BẢN ({0} Profiles, {1} Luồng) ===", selectedProfiles.Count, threadCount));

            // Chụp cấu hình trước khi chạy async
            bool doSurf = _chkSurfFeed.Checked;
            int feedSec = (int)_numFeedDuration.Value;
            bool doLike = _chkAutoLike.Checked;
            int maxLike = (int)_numMaxLikes.Value;
            bool doReels = _chkWatchReels.Checked;
            int reelCount = (int)_numReelCount.Value;
            bool doNotif = _chkCheckNotif.Checked;

            bool doLoginCookie = _radLoginCookie.Checked && !string.IsNullOrWhiteSpace(_txtCookieInput.Text);
            string cookieStr = _txtCookieInput.Text.Trim();

            bool doLoginCreds = _radLoginCreds.Checked && !string.IsNullOrWhiteSpace(_txtUsername.Text);
            string userStr = _txtUsername.Text.Trim();
            string passStr = _txtPassword.Text;
            string secretStr = _txtTwoFactor.Text.Trim();

            string postUrl = _txtPostUrl.Text.Trim();
            string commentSpintax = _txtCommentSpintax.Text.Trim();
            bool doJoin = _chkJoinGroup.Checked && !string.IsNullOrWhiteSpace(_txtGroupUrl.Text);
            string groupUrl = _txtGroupUrl.Text.Trim();

            // Cấu hình Đăng bài viết
            bool doPost = _chkEnablePost != null && _chkEnablePost.Checked;
            bool useApi = _radContentApi != null && _radContentApi.Checked;
            string apiUrl = _txtPostApiUrl != null ? _txtPostApiUrl.Text.Trim() : "https://blog.shin520.org/api/v1/feed/facebook";
            bool apiFallback = _chkApiFallbackToManual != null && _chkApiFallbackToManual.Checked;
            bool attachApiImages = _chkPostAttachImageFromApi != null && _chkPostAttachImageFromApi.Checked;
            bool allImages = _radAllImages != null && _radAllImages.Checked;
            string postContent = _txtAutoPostContent != null ? _txtAutoPostContent.Text : "";
            string postGroupUrl = (_radPostGroup != null && _radPostGroup.Checked && _txtPostGroupTarget != null) ? _txtPostGroupTarget.Text.Trim() : null;

            await Task.Run(() =>
            {
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = threadCount,
                    CancellationToken = _cts.Token
                };

                try
                {
                    Parallel.ForEach(selectedProfiles, options, (profile) =>
                    {
                        if (_cts.IsCancellationRequested) return;

                        RunProfileTask(profile, doSurf, feedSec, doLike, maxLike, doReels, reelCount, doNotif,
                            doLoginCookie, cookieStr, doLoginCreds, userStr, passStr, secretStr,
                            postUrl, commentSpintax, doJoin, groupUrl, doPost, useApi, apiUrl, apiFallback, attachApiImages, allImages, postContent, postGroupUrl, _cts.Token);
                    });
                }
                catch (OperationCanceledException)
                {
                    Log("[!] Người dùng đã bấm dừng tác vụ.");
                }
                catch (Exception ex)
                {
                    Log("[-] Lỗi chạy luồng: " + ex.Message);
                }
            });

            _isRunning = false;
            _btnStart.Enabled = true;
            _btnStop.Enabled = false;
            if (_lblRunningStatus != null)
            {
                _lblRunningStatus.Text = "● Đã hoàn tất";
                _lblRunningStatus.ForeColor = Color.FromArgb(5, 150, 105);
            }
            Log("=== HOÀN TẤT TẤT CẢ KỊCH BẢN ===");
        }

        private void RunProfileTask(UserProfile profile, bool doSurf, int feedSec, bool doLike, int maxLike, bool doReels, int reelCount, bool doNotif,
            bool doLoginCookie, string cookieStr, bool doLoginCreds, string userStr, string passStr, string secretStr,
            string postUrl, string commentSpintax, bool doJoin, string groupUrl,
            bool doPost, bool useApi, string apiUrl, bool apiFallback, bool attachApiImages, bool allImages, string postContent, string postGroupUrl, CancellationToken ct)
        {
            string tag = string.Format("[{0}]", profile.ProfileName);
            Log(string.Format("{0} Đang chuẩn bị trình duyệt...", tag));

            IWebDriver driver = BrowserSessionManager.Instance.GetDriver(profile.ProfileId);

            if (driver == null)
            {
                try
                {
                    var launcher = new BrowserLauncherService();
                    driver = launcher.LaunchBrowser(profile);
                }
                catch (Exception ex)
                {
                    Log(string.Format("{0} Không thể mở trình duyệt: {1}", tag, ex.Message));
                    return;
                }
            }

            var fb = new FacebookAutomationService();

            try
            {
                BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Auto FB: Khởi tạo");

                // 1. Dang nhap Cookie neu co
                if (doLoginCookie)
                {
                    BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Auto FB: Đăng nhập Cookie");
                    Log(string.Format("{0} Đang đăng nhập bằng Cookie...", tag));
                    fb.LoginWithCookie(driver, cookieStr, m => Log(tag + " " + m));
                }
                // Hoac dang nhap User/Pass/2FA
                else if (doLoginCreds)
                {
                    BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Auto FB: Đăng nhập User/Pass");
                    Log(string.Format("{0} Đang đăng nhập bằng User/Pass/2FA...", tag));
                    fb.LoginWithCredentials(driver, userStr, passStr, secretStr, m => Log(tag + " " + m));
                }

                // 2. Kiem tra trang thai
                string uid, status;
                fb.CheckAccountStatus(driver, out uid, out status);
                Log(string.Format("{0} Trạng thái hiện tại: {1} (UID: {2})", tag, status, uid));

                // 3. Nuoi nick Newfeed
                if (doSurf && !ct.IsCancellationRequested)
                {
                    BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Auto FB: Lướt Newsfeed");
                    fb.SurfNewsfeed(driver, feedSec, doLike, maxLike, m => Log(tag + " " + m), ct);
                }

                // 4. Xem Reels
                if (doReels && !ct.IsCancellationRequested)
                {
                    BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Auto FB: Xem Reels");
                    fb.WatchReels(driver, reelCount, m => Log(tag + " " + m), ct);
                }

                // 5. Xem Thong bao
                if (doNotif && !ct.IsCancellationRequested)
                {
                    BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Auto FB: Đọc thông báo");
                    fb.ViewNotifications(driver, m => Log(tag + " " + m));
                }

                // 6. Tham gia nhom neu co
                if (doJoin && !ct.IsCancellationRequested)
                {
                    BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Auto FB: Vào Group");
                    fb.JoinGroup(driver, groupUrl, m => Log(tag + " " + m));
                }

                // 7. Seeding binh luan neu co
                if (!string.IsNullOrEmpty(postUrl) && !string.IsNullOrEmpty(commentSpintax) && !ct.IsCancellationRequested)
                {
                    BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Auto FB: Bình luận");
                    fb.CommentPost(driver, postUrl, commentSpintax, m => Log(tag + " " + m));
                }

                // 8. Dang bai viet len tuong hoac vao nhom
                if (doPost && !ct.IsCancellationRequested)
                {
                    BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Auto FB: Đăng bài viết");
                    string actualContent = postContent;
                    List<string> actualImages = new List<string>();

                    if (useApi && !string.IsNullOrWhiteSpace(apiUrl))
                    {
                        Log(string.Format("{0} Đang gọi API lấy nội dung & hình ảnh bài viết: {1}", tag, apiUrl));
                        var apiRes = fb.FetchPostFromApi(apiUrl, m => Log(tag + " " + m));
                        if (apiRes != null && apiRes.Success && !string.IsNullOrWhiteSpace(apiRes.Content))
                        {
                            actualContent = apiRes.Content;
                            if (attachApiImages && apiRes.DownloadedImagePaths != null && apiRes.DownloadedImagePaths.Count > 0)
                            {
                                if (allImages)
                                {
                                    actualImages.AddRange(apiRes.DownloadedImagePaths);
                                }
                                else
                                {
                                    actualImages.Add(apiRes.DownloadedImagePaths[0]);
                                }
                            }
                        }
                        else if (apiFallback && !string.IsNullOrWhiteSpace(postContent))
                        {
                            Log(string.Format("{0} [!] Lấy bài từ API thất bại ({1}). Sử dụng nội dung thủ công dự phòng...", tag, apiRes != null ? apiRes.ErrorMessage : "Lỗi"));
                            actualContent = postContent;
                        }
                        else
                        {
                            Log(string.Format("{0} [-] Bỏ qua đăng bài do không lấy được nội dung từ API!", tag));
                            actualContent = null;
                        }
                    }

                    if (!string.IsNullOrEmpty(actualContent))
                    {
                        Log(string.Format("{0} Đang tiến hành đăng bài viết kèm {1} hình ảnh từ API...", tag, actualImages.Count));
                        fb.CreatePost(driver, actualContent, actualImages, postGroupUrl, m => Log(tag + " " + m));
                    }
                }

                Log(string.Format("{0} Đã hoàn thành các tác vụ trên profile này!", tag));
            }
            finally
            {
                BrowserSessionManager.Instance.ClearRunningTask(profile.ProfileId);
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (_lblRunningStatus != null)
                {
                    _lblRunningStatus.Text = "● Đang dừng lại...";
                    _lblRunningStatus.ForeColor = Color.FromArgb(239, 68, 68);
                }
                Log("[*] Đang gửi tín hiệu dừng tới các luồng...");
            }
        }

        private async void BtnCheckLive_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 profile để kiểm tra!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Log(string.Format("[*] Đang kiểm tra Live/Die cho {0} profile...", selected.Count));

            await Task.Run(() =>
            {
                foreach (var p in selected)
                {
                    // Trích xuất UID nếu có từ tên profile hoặc note
                    string uid = "";
                    var match = System.Text.RegularExpressions.Regex.Match(p.ProfileName, @"\d{10,20}");
                    if (match.Success) uid = match.Value;

                    if (!string.IsNullOrEmpty(uid))
                    {
                        string name, err;
                        bool live = FacebookAutomationService.FastCheckUidLive(uid, out name, out err);
                        Log(string.Format("[{0}] UID {1}: {2} {3}", p.ProfileName, uid, live ? "LIVE (Hoạt động)" : "DIE (Khóa/Chết)", err));
                    }
                    else
                    {
                        // Kiểm tra qua trình duyệt nếu đang mở
                        var driver = BrowserSessionManager.Instance.GetDriver(p.ProfileId);
                        if (driver != null)
                        {
                            string curUid, status;
                            new FacebookAutomationService().CheckAccountStatus(driver, out curUid, out status);
                            Log(string.Format("[{0}] Trạng thái qua browser: {1} (UID: {2})", p.ProfileName, status, curUid));
                        }
                        else
                        {
                            Log(string.Format("[{0}] Không có UID trong tên và profile chưa mở để quét cookie.", p.ProfileName));
                        }
                    }
                }
            });
        }

        private void BtnExtractCookie_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn profile đang mở để trích xuất cookie!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (var p in selected)
            {
                var driver = BrowserSessionManager.Instance.GetDriver(p.ProfileId);
                if (driver != null)
                {
                    string cookies = new FacebookAutomationService().ExtractCookies(driver);
                    Log(string.Format("[{0}] COOKIE: {1}", p.ProfileName, cookies));
                    if (!string.IsNullOrEmpty(cookies))
                    {
                        Clipboard.SetText(cookies);
                        Log(string.Format("[+] Đã copy Cookie của {0} vào Clipboard!", p.ProfileName));
                    }
                }
                else
                {
                    Log(string.Format("[-] Profile {0} chưa mở trình duyệt!", p.ProfileName));
                }
            }
        }

        private void BtnExtractToken_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn profile đang mở để trích xuất Token!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (var p in selected)
            {
                var driver = BrowserSessionManager.Instance.GetDriver(p.ProfileId);
                if (driver != null)
                {
                    string token = new FacebookAutomationService().ExtractToken(driver);
                    if (!string.IsNullOrEmpty(token))
                    {
                        Log(string.Format("[+] [{0}] TOKEN: {1}", p.ProfileName, token));
                        Clipboard.SetText(token);
                        Log(string.Format("[+] Đã copy Token của {0} vào Clipboard!", p.ProfileName));
                    }
                    else
                    {
                        Log(string.Format("[-] [{0}] Không tìm thấy EAAB Token trên phiên duyệt hiện tại.", p.ProfileName));
                    }
                }
                else
                {
                    Log(string.Format("[-] Profile {0} chưa mở trình duyệt!", p.ProfileName));
                }
            }
        }

        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isRunning)
            {
                if (MessageBox.Show("Các kịch bản Facebook đang chạy trong nền. Bạn có chắc muốn dừng và đóng cửa sổ này không?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                if (_cts != null) _cts.Cancel();
            }
            base.OnFormClosing(e);
        }

        private class ProfileItem
        {
            public UserProfile Profile { get; set; }
            public string Display { get; set; }
            public override string ToString()
            {
                return Display;
            }
        }
    }
}
