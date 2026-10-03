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

        // Bottom Controls
        private NumericUpDown _numThreads;
        private Button _btnStart;
        private Button _btnStop;
        private RichTextBox _rtbLog;
        private Label _lblProfileCount;
        private Label _lblRunningStatus;
        private Button _btnClearLog;
        private Button _btnCopyLog;

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
            SplitContainer splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 310,
                SplitterWidth = 6,
                Panel1MinSize = 240,
                Panel2MinSize = 680
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
                Margin = new Padding(0, 4, 0, 4)
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
            _chkListProfiles.ItemCheck += (s, e) => this.BeginInvoke(new Action(UpdateProfileCountLabel));

            pnlLeft.Controls.Add(_chkListProfiles);
            pnlLeft.Controls.Add(pnlSelectButtons);
            pnlLeft.Controls.Add(_txtSearch);
            pnlLeft.Controls.Add(pnlProfileTitle);
            splitMain.Panel1.Controls.Add(pnlLeft);

            // ================= 3. RIGHT PANEL: HORIZONTAL SPLIT (TOP = TABS, BOTTOM = LOG & ACTIONS) =================
            SplitContainer splitRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 370,
                SplitterWidth = 6,
                Panel1MinSize = 260,
                Panel2MinSize = 220
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

            // ===== TAB 4: Cổng CDP & Local API Studio =====
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

            splitRight.Panel1.Controls.Add(tabs);

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
            splitRight.Panel2.Controls.Add(pnlLogContainer);

            splitMain.Panel2.Controls.Add(splitRight);

            this.Controls.Add(splitMain);
            this.Controls.Add(pnlHeader);

            Log("Hệ thống Automation Facebook sẵn sàng. Vui lòng chọn profile và cấu hình kịch bản.");
        }

        private void UpdateProfileCountLabel()
        {
            if (_lblProfileCount != null)
            {
                int total = _chkListProfiles.Items.Count;
                int checkedCount = _chkListProfiles.CheckedItems.Count;
                _lblProfileCount.Text = string.Format("Đã chọn: {0}/{1}", checkedCount, total);
            }
        }

        private void LoadProfileList()
        {
            _chkListProfiles.Items.Clear();
            foreach (var p in _allProfiles)
            {
                bool running = BrowserSessionManager.Instance.IsRunning(p.ProfileId);
                string display = string.Format("{0} [{1}]", p.ProfileName, running ? "RUNNING" : "STOP");
                _chkListProfiles.Items.Add(new ProfileItem { Profile = p, Display = display });
            }
            UpdateProfileCountLabel();
        }

        private void FilterProfileList()
        {
            string q = _txtSearch.Text.Trim().ToLowerInvariant();
            _chkListProfiles.Items.Clear();
            foreach (var p in _allProfiles)
            {
                if (string.IsNullOrEmpty(q) || p.ProfileName.ToLowerInvariant().Contains(q))
                {
                    bool running = BrowserSessionManager.Instance.IsRunning(p.ProfileId);
                    string display = string.Format("{0} [{1}]", p.ProfileName, running ? "RUNNING" : "STOP");
                    _chkListProfiles.Items.Add(new ProfileItem { Profile = p, Display = display });
                }
            }
            UpdateProfileCountLabel();
        }

        private void SetAllChecked(bool isChecked)
        {
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                _chkListProfiles.SetItemChecked(i, isChecked);
            }
            UpdateProfileCountLabel();
        }

        private void SelectRunningProfiles()
        {
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                var item = _chkListProfiles.Items[i] as ProfileItem;
                if (item != null && BrowserSessionManager.Instance.IsRunning(item.Profile.ProfileId))
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

        private List<UserProfile> GetSelectedProfiles()
        {
            var list = new List<UserProfile>();
            foreach (var item in _chkListProfiles.CheckedItems)
            {
                var pItem = item as ProfileItem;
                if (pItem != null) list.Add(pItem.Profile);
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
                            postUrl, commentSpintax, doJoin, groupUrl, _cts.Token);
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
            string postUrl, string commentSpintax, bool doJoin, string groupUrl, CancellationToken ct)
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

            // 1. Dang nhap Cookie neu co
            if (doLoginCookie)
            {
                Log(string.Format("{0} Đang đăng nhập bằng Cookie...", tag));
                fb.LoginWithCookie(driver, cookieStr, m => Log(tag + " " + m));
            }
            // Hoac dang nhap User/Pass/2FA
            else if (doLoginCreds)
            {
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
                fb.SurfNewsfeed(driver, feedSec, doLike, maxLike, m => Log(tag + " " + m), ct);
            }

            // 4. Xem Reels
            if (doReels && !ct.IsCancellationRequested)
            {
                fb.WatchReels(driver, reelCount, m => Log(tag + " " + m), ct);
            }

            // 5. Xem Thong bao
            if (doNotif && !ct.IsCancellationRequested)
            {
                fb.ViewNotifications(driver, m => Log(tag + " " + m));
            }

            // 6. Tham gia nhom neu co
            if (doJoin && !ct.IsCancellationRequested)
            {
                fb.JoinGroup(driver, groupUrl, m => Log(tag + " " + m));
            }

            // 7. Seeding binh luan neu co
            if (!string.IsNullOrEmpty(postUrl) && !string.IsNullOrEmpty(commentSpintax) && !ct.IsCancellationRequested)
            {
                fb.CommentPost(driver, postUrl, commentSpintax, m => Log(tag + " " + m));
            }

            Log(string.Format("{0} Đã hoàn thành các tác vụ trên profile này!", tag));
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
