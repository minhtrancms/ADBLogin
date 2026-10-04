using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADBLogin.Core.Automation;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;
using OpenQA.Selenium;

namespace ADBLogin.UI
{
    public class ZaloAutomationForm : Form
    {
        private CheckedListBox _chkListProfiles;
        private TextBox _txtSearch;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRunning;
        private Label _lblProfileCount;

        // Tab 1: Nhắn tin SĐT
        private TextBox _txtPhoneList;
        private Label _lblPhoneCount;
        private Button _btnImportPhones;
        private Button _btnClearPhones;
        private TextBox _txtMessageTemplate;
        private Button _btnPreviewSpintax;
        private CheckBox _chkAutoFriend;
        private TextBox _txtFriendGreeting;
        private NumericUpDown _numDelaySeconds;
        private CheckBox _chkDistributePhones;

        // Tab 2: Chỉ kết bạn
        private TextBox _txtFriendOnlyPhones;
        private TextBox _txtFriendOnlyGreeting;
        private NumericUpDown _numFriendDelaySeconds;

        // Tab 3: Mở Zalo Web Quét QR
        private Button _btnOpenZaloWeb;

        // Bottom Controls
        private NumericUpDown _numThreads;
        private Button _btnStart;
        private Button _btnStop;
        private RichTextBox _rtbLog;
        private Label _lblRunningStatus;
        private Button _btnClearLog;
        private Button _btnExportReport;

        private SplitContainer _splitMain;
        private TabControl _tabs;

        private readonly List<UserProfile> _allProfiles;
        private readonly ZaloAutomationService _zaloService = new ZaloAutomationService();
        private readonly BrowserLauncherService _launcherService = new BrowserLauncherService();
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        private readonly List<ZaloSendResult> _sessionResults = new List<ZaloSendResult>();
        private readonly object _lockObj = new object();

        public ZaloAutomationForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            LoadProfileList();
        }

        private void InitializeComponent()
        {
            this.Text = "💬 BỘ CÔNG CỤ TỰ ĐỘNG HÓA ZALO (ZALO WEB AUTOMATION STUDIO)";
            this.Size = new Size(1220, 800);
            this.MinimumSize = new Size(1050, 640);
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
                Height = 60,
                BackColor = Color.FromArgb(0, 104, 255), // Zalo Blue
                Padding = new Padding(16, 8, 16, 8)
            };

            Label lblTitle = new Label
            {
                Text = "💬 ZALO WEB AUTOMATION STUDIO - TÌM SĐT & GỬI TIN NHẮN",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Tự động tìm kiếm khách hàng theo Số Điện Thoại (SĐT), Gửi tin nhắn Spintax chăm sóc / Telesale, Tự động gửi kết bạn Zalo",
                ForeColor = Color.FromArgb(224, 242, 254),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(15, 33)
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

            // ---------------- PANEL TRÁI: DANH SÁCH PROFILES ----------------
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };

            Label lblProfHeader = new Label
            {
                Text = "CHỌN TÀI KHOẢN ZALO (PROFILES):",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Dock = DockStyle.Top,
                Height = 22
            };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Text = "🔍 Tìm kiếm profile..."
            };
            _txtSearch.GotFocus += (s, e) => { if (_txtSearch.Text == "🔍 Tìm kiếm profile...") _txtSearch.Text = ""; _txtSearch.ForeColor = Color.Black; };
            _txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_txtSearch.Text)) { _txtSearch.Text = "🔍 Tìm kiếm profile..."; _txtSearch.ForeColor = Color.FromArgb(100, 116, 139); } };
            _txtSearch.TextChanged += (s, e) => FilterProfiles();

            FlowLayoutPanel pnlQuickBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(0, 4, 0, 4)
            };

            _btnSelectAll = CreateSmallButton("Tất cả", 58);
            _btnSelectAll.Click += (s, e) => SetAllProfileChecked(true);

            _btnDeselectAll = CreateSmallButton("Bỏ chọn", 64);
            _btnDeselectAll.Click += (s, e) => SetAllProfileChecked(false);

            _btnSelectRunning = CreateSmallButton("Đang chạy", 78);
            _btnSelectRunning.Click += (s, e) => SelectRunningProfiles();

            pnlQuickBtns.Controls.Add(_btnSelectAll);
            pnlQuickBtns.Controls.Add(_btnDeselectAll);
            pnlQuickBtns.Controls.Add(_btnSelectRunning);

            _lblProfileCount = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Text = "Đã chọn: 0 / 0",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _chkListProfiles = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 8.5F)
            };
            _chkListProfiles.ItemCheck += (s, e) =>
            {
                this.BeginInvoke(new Action(UpdateProfileCountLabel));
            };

            pnlLeft.Controls.Add(_chkListProfiles);
            pnlLeft.Controls.Add(_lblProfileCount);
            pnlLeft.Controls.Add(pnlQuickBtns);
            pnlLeft.Controls.Add(_txtSearch);
            pnlLeft.Controls.Add(lblProfHeader);
            _splitMain.Panel1.Controls.Add(pnlLeft);

            // ---------------- PANEL PHẢI: TABS VÀ LOG ----------------
            SplitContainer splitRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.FromArgb(226, 232, 240)
            };

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Point(14, 6)
            };

            _tabs.TabPages.Add(CreateMessageTab());
            _tabs.TabPages.Add(CreateFriendOnlyTab());
            _tabs.TabPages.Add(CreateFastLaunchTab());

            // Bottom Log Panel
            Panel pnlLogContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42), // Slate 900
                Padding = new Padding(10)
            };

            Panel pnlLogBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.Transparent
            };

            Label lblLogTitle = new Label
            {
                Text = "📋 NHẬT KÝ HOẠT ĐỘNG (REALTIME LOG):",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Dock = DockStyle.Left,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };

            FlowLayoutPanel pnlLogActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft
            };

            _btnExportReport = new Button
            {
                Text = "📥 Xuất Báo Cáo",
                Size = new Size(110, 24),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnExportReport.FlatAppearance.BorderSize = 0;
            _btnExportReport.Click += BtnExportReport_Click;

            _btnClearLog = new Button
            {
                Text = "Xóa Log",
                Size = new Size(70, 24),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(203, 213, 225),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F),
                Cursor = Cursors.Hand
            };
            _btnClearLog.FlatAppearance.BorderSize = 0;
            _btnClearLog.Click += (s, e) => _rtbLog.Clear();

            pnlLogActions.Controls.Add(_btnExportReport);
            pnlLogActions.Controls.Add(_btnClearLog);

            pnlLogBar.Controls.Add(lblLogTitle);
            pnlLogBar.Controls.Add(pnlLogActions);

            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 8.5F),
                ReadOnly = true
            };

            pnlLogContainer.Controls.Add(_rtbLog);
            pnlLogContainer.Controls.Add(pnlLogBar);

            splitRight.Panel1.Controls.Add(_tabs);
            splitRight.Panel2.Controls.Add(pnlLogContainer);
            _splitMain.Panel2.Controls.Add(splitRight);

            // ================= BOTTOM EXECUTION BAR =================
            Panel pnlBottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.White,
                Padding = new Padding(16, 8, 16, 8)
            };
            pnlBottomBar.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Color.FromArgb(226, 232, 240)), 0, 0, pnlBottomBar.Width, 0);
            };

            Label lblThreads = new Label
            {
                Text = "Số luồng chạy (Threads):",
                Location = new Point(14, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _numThreads = new NumericUpDown
            {
                Location = new Point(165, 13),
                Width = 50,
                Minimum = 1,
                Maximum = 15,
                Value = 2,
                Font = new Font("Segoe UI", 9F)
            };

            _btnStart = new Button
            {
                Text = "▶ BẮT ĐẦU CHẠY",
                Location = new Point(230, 9),
                Size = new Size(160, 34),
                BackColor = Color.FromArgb(16, 185, 129), // Emerald
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnStart.FlatAppearance.BorderSize = 0;
            _btnStart.Click += BtnStart_Click;

            _btnStop = new Button
            {
                Text = "⏹ DỪNG LẠI",
                Location = new Point(400, 9),
                Size = new Size(110, 34),
                BackColor = Color.FromArgb(239, 68, 68), // Red
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnStop.FlatAppearance.BorderSize = 0;
            _btnStop.Click += BtnStop_Click;

            _lblRunningStatus = new Label
            {
                Text = "Sẵn sàng",
                Location = new Point(525, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            pnlBottomBar.Controls.Add(lblThreads);
            pnlBottomBar.Controls.Add(_numThreads);
            pnlBottomBar.Controls.Add(_btnStart);
            pnlBottomBar.Controls.Add(_btnStop);
            pnlBottomBar.Controls.Add(_lblRunningStatus);

            this.Controls.Add(_splitMain);
            this.Controls.Add(pnlBottomBar);

            this.Load += (s, e) =>
            {
                try
                {
                    if (_splitMain.Width > 500) _splitMain.SplitterDistance = 330;
                    if (splitRight.Height > 300) splitRight.SplitterDistance = Math.Max(200, splitRight.Height - 220);
                }
                catch { }
            };
        }

        #region Create Tabs

        private TabPage CreateMessageTab()
        {
            TabPage tab = new TabPage("💬 Nhắn Tin Theo Danh Sách SĐT") { BackColor = Color.White };
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.FromArgb(241, 245, 249)
            };

            tab.HandleCreated += (s, e) =>
            {
                try { if (split.Width > 500) split.SplitterDistance = 380; } catch { }
            };

            // ---- Left: Danh sách SĐT ----
            Panel pnlPhones = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };

            Label lblPhoneTitle = new Label
            {
                Text = "DANH SÁCH SỐ ĐIỆN THOẠI (1 SỐ / DÒNG):",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Dock = DockStyle.Top,
                Height = 22
            };

            Panel pnlPhoneToolbar = new Panel { Dock = DockStyle.Top, Height = 32 };
            _btnImportPhones = CreateSmallButton("📁 Nhập file .TXT", 100);
            _btnImportPhones.Click += BtnImportPhones_Click;

            _btnClearPhones = CreateSmallButton("🗑️ Xóa danh sách", 105);
            _btnClearPhones.Click += (s, e) => { _txtPhoneList.Clear(); UpdatePhoneCount(); };

            pnlPhoneToolbar.Controls.Add(_btnImportPhones);
            _btnImportPhones.Location = new Point(0, 3);
            pnlPhoneToolbar.Controls.Add(_btnClearPhones);
            _btnClearPhones.Location = new Point(106, 3);

            _lblPhoneCount = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Text = "📱 Tổng số: 0 SĐT",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtPhoneList = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                Text = "0987654321\r\n0912345678"
            };
            _txtPhoneList.TextChanged += (s, e) => UpdatePhoneCount();

            pnlPhones.Controls.Add(_txtPhoneList);
            pnlPhones.Controls.Add(_lblPhoneCount);
            pnlPhones.Controls.Add(pnlPhoneToolbar);
            pnlPhones.Controls.Add(lblPhoneTitle);
            split.Panel1.Controls.Add(pnlPhones);

            // ---- Right: Nội dung tin nhắn & Tùy chọn ----
            Panel pnlMsg = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14), AutoScroll = true };

            Label lblMsgTitle = new Label
            {
                Text = "NỘI DUNG TIN NHẮN (HỖ TRỢ SPINTAX & {phone}):",
                Location = new Point(12, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            _btnPreviewSpintax = new Button
            {
                Text = "🎲 Xem thử Spintax",
                Location = new Point(340, 7),
                Size = new Size(120, 24),
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnPreviewSpintax.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnPreviewSpintax.Click += (s, e) =>
            {
                string spun = SpintaxHelper.Spin(_txtMessageTemplate.Text);
                spun = spun.Replace("{phone}", "0987654321");
                MessageBox.Show(spun, "Xem thử tin nhắn ngẫu nhiên (Spintax Preview)", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            _txtMessageTemplate = new TextBox
            {
                Location = new Point(14, 36),
                Width = 450,
                Height = 110,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9F),
                Text = "{Chào bạn|Xin chào|Chào anh/chị}! Em thấy số {phone} của mình nên nhắn tin trao đổi công việc ạ. {Rất vui được kết nối|Chúc anh/chị ngày mới an lành}!"
            };

            _chkAutoFriend = new CheckBox
            {
                Text = "🤝 Tự động gửi lời mời kết bạn nếu chưa là bạn bè",
                Location = new Point(14, 155),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(14, 116, 144),
                Checked = true
            };

            Label lblFriendGreeting = new Label
            {
                Text = "Lời chào khi gửi kết bạn:",
                Location = new Point(14, 185),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F)
            };

            _txtFriendGreeting = new TextBox
            {
                Location = new Point(14, 205),
                Width = 450,
                Height = 24,
                Font = new Font("Segoe UI", 9F),
                Text = "{Xin chào|Chào bạn}, mình kết bạn trao đổi công việc nhé!"
            };

            Label lblDelay = new Label
            {
                Text = "⏱️ Giãn cách giữa 2 lần gửi tin (giây):",
                Location = new Point(14, 240),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _numDelaySeconds = new NumericUpDown
            {
                Location = new Point(245, 238),
                Width = 60,
                Minimum = 3,
                Maximum = 120,
                Value = 8,
                Font = new Font("Segoe UI", 9F)
            };

            _chkDistributePhones = new CheckBox
            {
                Text = "🔀 Chia đều danh sách SĐT cho các profile đã chọn (Tránh trùng lặp)",
                Location = new Point(14, 275),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(99, 102, 241),
                Checked = true
            };

            Label lblTip = new Label
            {
                Text = "💡 Mẹo an toàn Zalo:\n" +
                       "• Hãy dùng Spintax {A|B|C} để mỗi tin nhắn gửi đi có nội dung khác nhau, tránh bị Zalo chặn spam.\n" +
                       "• Nên đặt giãn cách từ 8 - 15 giây giữa các lượt gửi.\n" +
                       "• Chế độ 'Chia đều' giúp tận dụng dàn profile để gửi hàng nghìn SĐT nhanh chóng.",
                Location = new Point(14, 305),
                Size = new Size(450, 90),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8F)
            };

            pnlMsg.Controls.Add(lblMsgTitle);
            pnlMsg.Controls.Add(_btnPreviewSpintax);
            pnlMsg.Controls.Add(_txtMessageTemplate);
            pnlMsg.Controls.Add(_chkAutoFriend);
            pnlMsg.Controls.Add(lblFriendGreeting);
            pnlMsg.Controls.Add(_txtFriendGreeting);
            pnlMsg.Controls.Add(lblDelay);
            pnlMsg.Controls.Add(_numDelaySeconds);
            pnlMsg.Controls.Add(_chkDistributePhones);
            pnlMsg.Controls.Add(lblTip);

            split.Panel2.Controls.Add(pnlMsg);
            tab.Controls.Add(split);

            UpdatePhoneCount();
            return tab;
        }

        private TabPage CreateFriendOnlyTab()
        {
            TabPage tab = new TabPage("🤝 Chỉ Kết Bạn Theo SĐT") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), AutoScroll = true };

            Label lblDesc = new Label
            {
                Text = "Chế độ này chỉ tìm kiếm SĐT và gửi lời mời kết bạn (Không gửi tin nhắn trò chuyện):",
                Location = new Point(14, 14),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            Label lblPhones = new Label { Text = "Danh sách SĐT cần kết bạn:", Location = new Point(14, 44), AutoSize = true };
            _txtFriendOnlyPhones = new TextBox
            {
                Location = new Point(14, 64),
                Width = 320,
                Height = 220,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                Text = "0987654321\r\n0912345678"
            };

            Label lblGreeting = new Label { Text = "Lời chào kết bạn (Spintax):", Location = new Point(360, 44), AutoSize = true };
            _txtFriendOnlyGreeting = new TextBox
            {
                Location = new Point(360, 64),
                Width = 380,
                Height = 70,
                Multiline = true,
                Font = new Font("Segoe UI", 9F),
                Text = "{Chào bạn|Xin chào}, mình thấy thông tin của bạn nên gửi kết bạn giao lưu nhé!"
            };

            Label lblDelay = new Label { Text = "Giãn cách (giây):", Location = new Point(360, 150), AutoSize = true };
            _numFriendDelaySeconds = new NumericUpDown { Location = new Point(470, 148), Width = 60, Minimum = 3, Maximum = 60, Value = 6 };

            pnl.Controls.Add(lblDesc);
            pnl.Controls.Add(lblPhones);
            pnl.Controls.Add(_txtFriendOnlyPhones);
            pnl.Controls.Add(lblGreeting);
            pnl.Controls.Add(_txtFriendOnlyGreeting);
            pnl.Controls.Add(lblDelay);
            pnl.Controls.Add(_numFriendDelaySeconds);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateFastLaunchTab()
        {
            TabPage tab = new TabPage("⚡ Mở Zalo Web (Quét QR)") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

            Label lblInfo = new Label
            {
                Text = "Khởi chạy Zalo Web nhanh trên các profile được chọn để quét mã QR hoặc đăng nhập:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };

            _btnOpenZaloWeb = new Button
            {
                Text = "🚀 Mở Zalo Web Cho Các Profile Đã Chọn",
                Location = new Point(20, 55),
                Size = new Size(300, 42),
                BackColor = Color.FromArgb(0, 104, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnOpenZaloWeb.FlatAppearance.BorderSize = 0;
            _btnOpenZaloWeb.Click += BtnOpenZaloWeb_Click;

            Label lblNote = new Label
            {
                Text = "📌 Lưu ý:\n" +
                       "• Mỗi profile ADBLogin lưu trữ toàn bộ Cookie, LocalStorage và IndexedDB riêng biệt.\n" +
                       "• Khi bạn đăng nhập Zalo một lần bằng QR hoặc SMS, phiên đăng nhập sẽ được lưu vĩnh viễn trên profile đó.\n" +
                       "• Bạn có thể chạy song song hàng chục tài khoản Zalo cùng lúc mà không sợ xung đột!",
                Location = new Point(20, 120),
                Size = new Size(600, 100),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F)
            };

            pnl.Controls.Add(lblInfo);
            pnl.Controls.Add(_btnOpenZaloWeb);
            pnl.Controls.Add(lblNote);

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

        private void SetAllProfileChecked(bool isChecked)
        {
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                _chkListProfiles.SetItemChecked(i, isChecked);
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
            _lblProfileCount.Text = string.Format("Đã chọn: {0} / {1}", _chkListProfiles.CheckedItems.Count, _chkListProfiles.Items.Count);
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

        private void UpdatePhoneCount()
        {
            var phones = GetPhonesFromText(_txtPhoneList.Text);
            _lblPhoneCount.Text = string.Format("📱 Tổng số: {0} SĐT", phones.Count);
        }

        private List<string> GetPhonesFromText(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            var lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var l in lines)
            {
                string p = ZaloAutomationService.NormalizePhone(l);
                if (!string.IsNullOrEmpty(p) && p.Length >= 8)
                {
                    list.Add(p);
                }
            }
            return list;
        }

        private void BtnImportPhones_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog { Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*", Title = "Chọn file danh sách số điện thoại" })
            {
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string content = File.ReadAllText(ofd.FileName);
                        if (!string.IsNullOrWhiteSpace(_txtPhoneList.Text))
                        {
                            _txtPhoneList.AppendText("\r\n" + content);
                        }
                        else
                        {
                            _txtPhoneList.Text = content;
                        }
                        UpdatePhoneCount();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi đọc file: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        #endregion

        #region Execution

        private void BtnOpenZaloWeb_Click(object sender, EventArgs e)
        {
            var profiles = GetSelectedProfiles();
            if (profiles.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để mở Zalo Web!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Task.Run(() =>
            {
                foreach (var p in profiles)
                {
                    try
                    {
                        Log(string.Format("[{0}] Đang mở Zalo Web...", p.ProfileName), Color.Cyan);
                        var driver = BrowserSessionManager.Instance.GetSession(p.ProfileId) ?? _launcherService.LaunchBrowser(p);
                        if (driver != null)
                        {
                            driver.Navigate().GoToUrl("https://chat.zalo.me/");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format("[{0}] Lỗi mở Zalo Web: {1}", p.ProfileName, ex.Message), Color.Red);
                    }
                }
            });
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_isRunning) return;
            var selectedProfiles = GetSelectedProfiles();
            if (selectedProfiles.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile Zalo!", "Chưa chọn profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int tabIdx = _tabs.SelectedIndex;
            List<string> phones;
            string msgTemplate = "";
            bool autoFriend = false;
            string friendGreeting = "";
            int delaySec = 8;
            bool distribute = true;

            if (tabIdx == 0) // Nhắn tin SĐT
            {
                phones = GetPhonesFromText(_txtPhoneList.Text);
                msgTemplate = _txtMessageTemplate.Text;
                autoFriend = _chkAutoFriend.Checked;
                friendGreeting = _txtFriendGreeting.Text;
                delaySec = (int)_numDelaySeconds.Value;
                distribute = _chkDistributePhones.Checked;
            }
            else if (tabIdx == 1) // Chỉ kết bạn
            {
                phones = GetPhonesFromText(_txtFriendOnlyPhones.Text);
                msgTemplate = "";
                autoFriend = true;
                friendGreeting = _txtFriendOnlyGreeting.Text;
                delaySec = (int)_numFriendDelaySeconds.Value;
                distribute = true;
            }
            else
            {
                BtnOpenZaloWeb_Click(sender, e);
                return;
            }

            if (phones.Count == 0)
            {
                MessageBox.Show("Danh sách số điện thoại đang trống! Vui lòng nhập SĐT.", "Chưa nhập SĐT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int maxThreads = (int)_numThreads.Value;
            _isRunning = true;
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _lblRunningStatus.Text = string.Format("Đang chạy: 0 / {0} SĐT...", phones.Count);
            _lblRunningStatus.ForeColor = Color.FromArgb(0, 104, 255);

            _sessionResults.Clear();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Log(string.Format("=== [BẮT ĐẦU AUTO ZALO - {0} PROFILES - {1} SĐT - {2} LUỒNG] ===", selectedProfiles.Count, phones.Count, maxThreads), Color.Cyan);

            Task.Run(() =>
            {
                try
                {
                    // Phân bổ danh sách SĐT cho từng profile
                    var profilePhoneMap = new Dictionary<UserProfile, List<string>>();
                    if (distribute)
                    {
                        for (int i = 0; i < selectedProfiles.Count; i++)
                        {
                            profilePhoneMap[selectedProfiles[i]] = new List<string>();
                        }
                        for (int i = 0; i < phones.Count; i++)
                        {
                            var targetProfile = selectedProfiles[i % selectedProfiles.Count];
                            profilePhoneMap[targetProfile].Add(phones[i]);
                        }
                    }
                    else
                    {
                        foreach (var p in selectedProfiles)
                        {
                            profilePhoneMap[p] = new List<string>(phones);
                        }
                    }

                    int totalProcessed = 0;
                    int successCount = 0;

                    Parallel.ForEach(profilePhoneMap, new ParallelOptions { MaxDegreeOfParallelism = maxThreads, CancellationToken = token }, (pair) =>
                    {
                        var profile = pair.Key;
                        var profilePhones = pair.Value;
                        if (token.IsCancellationRequested || profilePhones.Count == 0) return;

                        RunZaloJobForProfile(profile, profilePhones, msgTemplate, autoFriend, friendGreeting, delaySec, token, () =>
                        {
                            Interlocked.Increment(ref totalProcessed);
                            this.BeginInvoke(new Action(() =>
                            {
                                _lblRunningStatus.Text = string.Format("Tiến độ: {0}/{1} (Thành công: {2})", totalProcessed, phones.Count, successCount);
                            }));
                        }, (success) =>
                        {
                            if (success) Interlocked.Increment(ref successCount);
                        });
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
                        _lblRunningStatus.Text = "Đã hoàn thành!";
                        _lblRunningStatus.ForeColor = Color.FromArgb(16, 185, 129);
                        Log(string.Format("=== [HOÀN TẤT TÁC VỤ ZALO! ĐÃ XỬ LÝ {0} KẾT QUẢ] ===", _sessionResults.Count), Color.Green);
                    }));
                }
            });
        }

        private void RunZaloJobForProfile(UserProfile profile, List<string> phones, string msgTemplate, bool autoFriend, string friendGreeting, int delaySec, CancellationToken token, Action onSingleDone, Action<bool> onSuccessDone)
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

                // Mở Zalo Web
                BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, "Zalo: Kiểm tra đăng nhập");
                bool isLogged = _zaloService.CheckIsLoggedIn(driver);
                if (!isLogged)
                {
                    isLogged = _zaloService.OpenZaloWeb(driver, logger);
                }

                if (!isLogged)
                {
                    Log(string.Format("[{0}] CẢNH BÁO: Zalo Web chưa đăng nhập. Vui lòng quét mã QR trước!", pName), Color.Yellow);
                    return;
                }

                // Chạy từng số điện thoại
                for (int i = 0; i < phones.Count; i++)
                {
                    if (token.IsCancellationRequested) break;
                    string phone = phones[i];

                    BrowserSessionManager.Instance.SetRunningTask(profile.ProfileId, string.Format("Zalo: Gửi SĐT {0} ({1}/{2})", phone, i + 1, phones.Count));

                    var result = _zaloService.ExecuteSendToPhone(driver, phone, msgTemplate, autoFriend, friendGreeting, logger);
                    lock (_lockObj)
                    {
                        _sessionResults.Add(result);
                    }

                    if (result.Success)
                    {
                        Log(string.Format("[{0}] [✓ THÀNH CÔNG] Đã gửi tới SĐT: {1}", pName, phone), Color.LimeGreen);
                        if (onSuccessDone != null) onSuccessDone(true);
                    }
                    else
                    {
                        Log(string.Format("[{0}] [✗ THẤT BẠI] SĐT {1}: {2}", pName, phone, result.ErrorNote), Color.FromArgb(248, 113, 113));
                        if (onSuccessDone != null) onSuccessDone(false);
                    }

                    if (onSingleDone != null) onSingleDone();

                    if (i < phones.Count - 1 && !token.IsCancellationRequested)
                    {
                        int actualDelay = (delaySec * 1000) + new Random().Next(-1000, 1500);
                        if (actualDelay < 2000) actualDelay = 2000;
                        Thread.Sleep(actualDelay);
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[{0}] Lỗi luồng: {1}", pName, ex.Message), Color.Red);
            }
            finally
            {
                BrowserSessionManager.Instance.ClearRunningTask(profile.ProfileId);
            }
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

        private void BtnExportReport_Click(object sender, EventArgs e)
        {
            if (_sessionResults.Count == 0)
            {
                MessageBox.Show("Chưa có kết quả gửi Zalo nào trong phiên chạy hiện tại!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var sfd = new SaveFileDialog { Filter = "CSV UTF-8 (*.csv)|*.csv|Text File (*.txt)|*.txt", FileName = string.Format("Zalo_Report_{0}.csv", DateTime.Now.ToString("yyyyMMdd_HHmmss")) })
            {
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Số Điện Thoại,Trạng Thái,Gửi Kết Bạn,Nội Dung Tin Nhắn,Ghi Chú Lỗi");
                        lock (_lockObj)
                        {
                            foreach (var r in _sessionResults)
                            {
                                sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\"",
                                    r.Phone,
                                    r.Success ? "Thành Công" : "Thất Bại",
                                    r.FriendRequestSent ? "Đã Gửi Lời Mời" : "Không",
                                    (r.Message ?? "").Replace("\"", "\"\""),
                                    (r.ErrorNote ?? "").Replace("\"", "\"\"")));
                            }
                        }
                        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        MessageBox.Show("Xuất báo cáo kết quả thành công:\n" + sfd.FileName, "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi lưu file: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        #endregion

        #region Helpers

        private Button CreateSmallButton(string text, int width)
        {
            Button btn = new Button
            {
                Text = text,
                Size = new Size(width, 24),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
            return btn;
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
