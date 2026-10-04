using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;

namespace ADBLogin.UI
{
    public class MainForm : Form
    {
        private DataGridView _grid;
        private TextBox _txtSearch;
        private Label _lblStatsTotal;
        private Label _lblStatsRunning;
        private Label _lblStatsProxy;
        private Label _lblStatsSelected;
        private Label _lblStatus;

        private ComboBox _cboFilterStatus;
        private ComboBox _cboFilterTag;
        private bool _allChecked = false;

        private Button _btnProfileMenu;
        private Button _btnLaunch;
        private Button _btnStop;
        private Button _btnRunOptions;
        private Button _btnProxyMenu;
        private Button _btnMmoMenu;
        private Button _btnToolsMenu;
        private Button _btnRefresh;
        private ToolStripMenuItem _itemCheckAllProxy;

        private NumericUpDown _numRows;
        private NumericUpDown _numCols;
        private CheckBox _chkMobileMode;
        private ComboBox _cboBrowserVersion;

        private System.Windows.Forms.Timer _statusTimer;
        private ContextMenuStrip _contextMenu;

        private readonly AccountManager _accountManager;
        private readonly BrowserLauncherService _launcherService;
        private readonly ProxyCheckerService _proxyChecker;
        private readonly BrowserSessionManager _sessionManager;

        public MainForm()
        {
            _accountManager = AccountManager.Instance;
            _accountManager.ProfilesChanged += (s, e) =>
            {
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(new Action(LoadData));
                }
            };
            _launcherService = new BrowserLauncherService();
            _proxyChecker = new ProxyCheckerService();
            _sessionManager = BrowserSessionManager.Instance;

            InitializeForm();
            InitializeContextMenu();
            LoadData();
            InitializeStatusTimer();
            LocalApiService.Instance.Start(5858);
        }

        private void InitializeForm()
        {
            this.Text = "ADBLogin v2.0 - Profile Manager [Enterprise Unlimited]";
            this.Size = new Size(1300, 700);
            this.MinimumSize = new Size(980, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.DoubleBuffered = true;

            // ================= 1. HEADER PANEL (GỌN GÀNG, CHUẨN WINDOWS 11) =================
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(12, 8, 12, 8)
            };

            Label lblLogo = new Label
            {
                Text = "⚡ ADBLogin",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                Location = new Point(12, 12),
                AutoSize = true
            };

            Label lblBadge = new Label
            {
                Text = "OFFLINE v2.0",
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                Location = new Point(128, 15),
                Padding = new Padding(4, 1, 4, 1),
                AutoSize = true
            };

            // Stat Chips nhỏ gọn, sắc nét
            _lblStatsTotal = CreateStatChip("Tổng: 0", Color.FromArgb(30, 41, 59), 230);
            _lblStatsRunning = CreateStatChip("Đang mở: 0", Color.FromArgb(6, 78, 59), 320);
            _lblStatsProxy = CreateStatChip("Proxy: 0", Color.FromArgb(51, 65, 85), 425);
            _lblStatsSelected = CreateStatChip("Đã chọn: 0", Color.FromArgb(79, 70, 229), 525);

            headerPanel.Controls.Add(lblLogo);
            headerPanel.Controls.Add(lblBadge);
            headerPanel.Controls.Add(_lblStatsTotal);
            headerPanel.Controls.Add(_lblStatsRunning);
            headerPanel.Controls.Add(_lblStatsProxy);
            headerPanel.Controls.Add(_lblStatsSelected);

            // ================= 2. SMART COMMAND BAR (DÒNG 1: CÁC NÚT HÀNH ĐỘNG & MENU THÔNG MINH) =================
            Panel pnlProfileBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.White,
                Padding = new Padding(8, 4, 8, 4)
            };
            pnlProfileBar.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Color.FromArgb(226, 232, 240)), 0, pnlProfileBar.Height - 1, pnlProfileBar.Width, pnlProfileBar.Height - 1);
            };

            ToolTip toolTip = new ToolTip();

            // 1. SMART BUTTON: QUẢN LÝ PROFILE
            _btnProfileMenu = CreateCompactButton("➕ Profile ▾", Color.FromArgb(37, 99, 235), Color.White, 106, true, "Quản lý Profile: Thêm mới, tạo hàng loạt, nhập nick", Color.FromArgb(29, 78, 216));
            var mnuProfile = new ContextMenuStrip();
            AddMenuItem(mnuProfile, "➕  Thêm Profile Thủ Công (Ctrl+N)", BtnAdd_Click, Color.FromArgb(37, 99, 235), true);
            AddMenuItem(mnuProfile, "🚀  Tạo Nhanh Hàng Loạt (Auto-Generate)...", BtnAutoCreate_Click);
            AddMenuItem(mnuProfile, "📥  Nhập Danh Sách Nick (UID|Pass|2FA|Proxy)...", (s, e) =>
            {
                using (var impForm = new BatchAccountImporterForm())
                {
                    if (impForm.ShowDialog(this) == DialogResult.OK)
                    {
                        LoadData();
                    }
                }
            });
            AddMenuSeparator(mnuProfile);
            AddMenuItem(mnuProfile, "📁  Mở Thư Mục Profile Dữ Liệu", BtnOpenFolder_Click);
            AttachSmartDropdown(_btnProfileMenu, mnuProfile);

            // 2 & 3. FAST ACTIONS: CHẠY & DỪNG
            _btnLaunch = CreateCompactButton("▶ Chạy", Color.FromArgb(16, 185, 129), Color.White, 76, true, "Khởi chạy các profile đã chọn", Color.FromArgb(5, 150, 105));
            _btnLaunch.Click += BtnLaunch_Click;

            _btnStop = CreateCompactButton("⏹ Dừng", Color.FromArgb(239, 68, 68), Color.White, 74, true, "Đóng trình duyệt các profile đã chọn", Color.FromArgb(220, 38, 38));
            _btnStop.Click += BtnStop_Click;

            // 4. SMART BUTTON: ĐIỀU KHIỂN & VẬN HÀNH
            _btnRunOptions = CreateCompactButton("⚙️ Điều Khiển ▾", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 114, false, "Tắt hết trình duyệt đang mở", Color.FromArgb(226, 232, 240));
            var mnuRunOptions = new ContextMenuStrip();
            AddMenuItem(mnuRunOptions, "⏹  Tắt Tất Cả Trình Duyệt Đang Chạy", BtnStopAll_Click, Color.FromArgb(220, 38, 38), true);
            AttachSmartDropdown(_btnRunOptions, mnuRunOptions);

            // 5. SMART BUTTON: PROXY STUDIO
            _btnProxyMenu = CreateCompactButton("🌐 Proxy Studio ▾", Color.FromArgb(14, 165, 233), Color.White, 126, true, "Quản lý Proxy: Multi-Proxy Studio, kiểm tra, sao chép, xoay proxy", Color.FromArgb(2, 132, 199));
            var mnuProxy = new ContextMenuStrip();
            AddMenuItem(mnuProxy, "🌐  Trạm Multi-Proxy Studio (WARP, HMA, NordVPN...)", (s, e) =>
            {
                try
                {
                    var hmaForm = new HmaMultiProxyForm(_accountManager.GetAllProfiles());
                    hmaForm.FormClosed += (fs, fe) => LoadData();
                    hmaForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở HMA Multi-Proxy:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }, Color.FromArgb(2, 132, 199), true);
            AddMenuSeparator(mnuProxy);
            AddMenuItem(mnuProxy, "⚡  Kiểm Tra Proxy Các Profile Được Chọn", BtnCheckProxy_Click);
            _itemCheckAllProxy = AddMenuItem(mnuProxy, "🔍  Kiểm Tra Toàn Bộ Proxy (Tất Cả Profile)", BtnCheckAllProxy_Click);
            AddMenuItem(mnuProxy, "📋  Sao Chép Chuỗi Proxy Vào Clipboard", BtnCopyProxy_Click);
            AddMenuSeparator(mnuProxy);
            AddMenuItem(mnuProxy, "⚙️  Cấu Hình Xoay Proxy (TMProxy, Tinsoft...) & Captcha", (s, e) =>
            {
                try
                {
                    var setForm = new AdvancedSettingsForm();
                    setForm.ShowDialog(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở cấu hình:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
            AttachSmartDropdown(_btnProxyMenu, mnuProxy);

            // 6. SMART BUTTON: KỊCH BẢN MMO STUDIO
            _btnMmoMenu = CreateCompactButton("🤖 Kịch Bản MMO ▾", Color.FromArgb(99, 102, 241), Color.White, 134, true, "Kịch bản Automation MMO: Facebook, Google, TikTok, Shopee, X, Telegram...", Color.FromArgb(79, 70, 229));
            var mnuMmo = new ContextMenuStrip();
            AddMenuItem(mnuMmo, "📘  Auto Facebook Studio (CDP Automation)", (s, e) =>
            {
                try { new FacebookAutomationForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(string.Format("Lỗi Auto FB: {0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(24, 119, 242), true);
            AddMenuItem(mnuMmo, "🌐  Auto Google & Gmail Studio", (s, e) =>
            {
                try { new GoogleAutomationForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(string.Format("Lỗi Auto Google: {0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(234, 67, 53), true);
            AddMenuItem(mnuMmo, "🎵  Auto TikTok & FYP Studio", (s, e) =>
            {
                try { new TikTokAutomationForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(string.Format("Lỗi Auto TikTok: {0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(15, 23, 42), true);
            AddMenuItem(mnuMmo, "🛒  Auto Shopee (Săn Xu, Voucher, Nuôi Nick)", (s, e) =>
            {
                try { new ShopeeAutomationForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(string.Format("Lỗi Auto Shopee: {0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(238, 77, 45), true);
            AddMenuItem(mnuMmo, "🐦  Auto X / Twitter (Airdrop, Follow, Retweet)", (s, e) =>
            {
                try { new TwitterAutomationForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(string.Format("Lỗi Auto Twitter: {0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(29, 155, 240), true);
            AddMenuItem(mnuMmo, "✈️  Auto Telegram Web Studio", (s, e) =>
            {
                try { new TelegramAutomationForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(string.Format("Lỗi Auto Telegram: {0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(34, 158, 217), true);
            AddMenuItem(mnuMmo, "💬  Auto Zalo Studio (Tìm SĐT & Nhắn Tin)", (s, e) =>
            {
                try { new ZaloAutomationForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(string.Format("Lỗi Auto Zalo: {0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(0, 104, 255), true);
            AddMenuSeparator(mnuMmo);
            AddMenuItem(mnuMmo, "👾  Auto Discord Web (Join Server & Leveling)", (s, e) =>
            {
                try { new DiscordAutomationForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(88, 101, 242));
            AddMenuItem(mnuMmo, "📸  Auto Instagram (Nuôi Feed & Reels)", (s, e) =>
            {
                try { new InstagramAutomationForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(193, 53, 132));
            AddMenuItem(mnuMmo, "🛍️  Auto Lazada (LazCoins & Voucher)", (s, e) =>
            {
                try { new LazadaAutomationForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(15, 23, 42));
            AttachSmartDropdown(_btnMmoMenu, mnuMmo);

            // 7. SMART BUTTON: TIỆN ÍCH & CÔNG CỤ
            _btnToolsMenu = CreateCompactButton("🛠️ Tiện Ích ▾", Color.FromArgb(248, 250, 252), Color.FromArgb(51, 65, 85), 102, false, "Công cụ: Đồng bộ, lập lịch, gắn nhãn, xóa profile", Color.FromArgb(226, 232, 240));
            var mnuTools = new ContextMenuStrip();
            AddMenuItem(mnuTools, "⚡  Đồng Bộ Thao Tác Chuột & Phím (Synchronizer)", (s, e) =>
            {
                try { new SynchronizerForm(_accountManager.GetAllProfiles()).Show(this); }
                catch (Exception ex) { MessageBox.Show(string.Format("Lỗi mở Synchronizer:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(99, 102, 241), true);
            AddMenuItem(mnuTools, "⏰  Bộ Quản Lý Lập Lịch Tự Động (Scheduler)", (s, e) =>
            {
                try { new SchedulerManagerForm().Show(this); }
                catch (Exception ex) { MessageBox.Show(string.Format("Lỗi mở Lập Lịch:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }, Color.FromArgb(16, 185, 129), true);
            AddMenuSeparator(mnuTools);
            AddMenuItem(mnuTools, "🏷️  Gắn Nhãn / Tag Nhanh Profile...", (s, e) => ShowQuickTagDialog());
            AddMenuItem(mnuTools, "🗑️  Xóa Vĩnh Viễn Profile Đang Chọn (Delete)", BtnDelete_Click, Color.FromArgb(225, 29, 72), true);
            AttachSmartDropdown(_btnToolsMenu, mnuTools);

            pnlProfileBar.Controls.Add(_btnProfileMenu);
            pnlProfileBar.Controls.Add(CreateDivider());
            pnlProfileBar.Controls.Add(_btnLaunch);
            pnlProfileBar.Controls.Add(_btnStop);
            pnlProfileBar.Controls.Add(CreateDivider());
            pnlProfileBar.Controls.Add(_btnRunOptions);
            pnlProfileBar.Controls.Add(_btnProxyMenu);
            pnlProfileBar.Controls.Add(_btnMmoMenu);
            pnlProfileBar.Controls.Add(_btnToolsMenu);

            LayoutCompactToolbar(pnlProfileBar);
            pnlProfileBar.Resize += (s, e) => LayoutCompactToolbar(pnlProfileBar);

            // ================= 3. SUB TOOLBAR (DÒNG 2: BỘ LỌC, ORBITA, MOBILE, LƯỚI, RELOAD) =================
            Panel pnlFilterBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(8, 3, 8, 3)
            };
            pnlFilterBar.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Color.FromArgb(226, 232, 240)), 0, pnlFilterBar.Height - 1, pnlFilterBar.Width, pnlFilterBar.Height - 1);
            };

            // 0. Ô tìm kiếm Profile Name / Email / Proxy / Tag trực tiếp trên Toolbar Dòng 2
            _txtSearch = new TextBox
            {
                Width = 210,
                Height = 24,
                Font = new Font("Segoe UI", 8.5F),
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = Color.FromArgb(100, 116, 139),
                Text = "🔍 Tìm profile, proxy, tag..."
            };
            _txtSearch.GotFocus += (s, e) =>
            {
                if (_txtSearch.Text == "🔍 Tìm profile, proxy, tag...")
                {
                    _txtSearch.Text = "";
                    _txtSearch.ForeColor = Color.FromArgb(15, 23, 42);
                }
            };
            _txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "🔍 Tìm profile, proxy, tag...";
                    _txtSearch.ForeColor = Color.FromArgb(100, 116, 139);
                }
            };
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    _txtSearch.Text = "";
                    if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                    {
                        _txtSearch.Text = "🔍 Tìm profile, proxy, tag...";
                        _txtSearch.ForeColor = Color.FromArgb(100, 116, 139);
                    }
                    this.ActiveControl = _grid;
                    e.Handled = true;
                }
            };
            _txtSearch.TextChanged += (s, e) => FilterData();
            toolTip.SetToolTip(_txtSearch, "Tìm nhanh tên profile, email, proxy, id, ghi chú hoặc tag (Nhấn ESC để hủy tìm)");

            // 1. Bộ lọc Status & Tag
            Label lblFilter = new Label { Text = "Lọc:", AutoSize = true, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(100, 116, 139) };
            _cboFilterStatus = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 115,
                Height = 24,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            _cboFilterStatus.Items.Add("Tất cả trạng thái");
            _cboFilterStatus.Items.Add("🟢 Đang mở");
            _cboFilterStatus.Items.Add("⚪ Đã tắt");
            _cboFilterStatus.SelectedIndex = 0;
            _cboFilterStatus.SelectedIndexChanged += (s, e) => FilterData();

            _cboFilterTag = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Height = 24,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            _cboFilterTag.Items.Add("Tất cả nhãn");
            _cboFilterTag.SelectedIndex = 0;
            _cboFilterTag.SelectedIndexChanged += (s, e) => FilterData();

            // 2. Lõi Orbita / Chrome
            Label lblBrowser = new Label { Text = "🌐 Orbita:", AutoSize = true, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(100, 116, 139) };
            _cboBrowserVersion = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 135,
                Height = 24,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            toolTip.SetToolTip(_cboBrowserVersion, "Chọn phiên bản lõi Chrome / Orbita sử dụng để mở profile");
            PopulateBrowserVersions();
            _cboBrowserVersion.SelectedIndexChanged += CboBrowserVersion_SelectedIndexChanged;

            // 3. Mobile Mode & Lưới Phone Farm
            _chkMobileMode = new CheckBox
            {
                Text = "📱 Mobile",
                AutoSize = true,
                Checked = false,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199),
                Cursor = Cursors.Hand
            };

            Label lblGridConfig = new Label { Text = "Lưới:", AutoSize = true, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(100, 116, 139) };
            _numRows = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 2, Width = 34, Height = 22, Font = new Font("Segoe UI", 8.5F) };
            Label lblRowUnit = new Label { Text = "x", AutoSize = true, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(100, 116, 139) };
            _numCols = new NumericUpDown { Minimum = 1, Maximum = 12, Value = 4, Width = 34, Height = 22, Font = new Font("Segoe UI", 8.5F) };

            toolTip.SetToolTip(_chkMobileMode, "Chế độ giao diện điện thoại (Scale 70%, tự động xếp 1 hàng 6 máy)");
            toolTip.SetToolTip(_numRows, "Số hàng (Rows)");
            toolTip.SetToolTip(_numCols, "Số cột / Số máy trên 1 hàng (Columns)");

            _chkMobileMode.CheckedChanged += (s, e) =>
            {
                if (_chkMobileMode.Checked)
                {
                    _numRows.Value = 1;
                    _numCols.Value = 6;
                }
                else
                {
                    _numRows.Value = 2;
                    _numCols.Value = 4;
                }
            };

            // 4. Reload / Refresh
            _btnRefresh = CreateCompactButton("🔄 Làm Mới", Color.FromArgb(255, 255, 255), Color.FromArgb(51, 65, 85), 78, false, "Tải lại danh sách profile (F5)", Color.FromArgb(241, 245, 249));
            _btnRefresh.Click += (s, e) => LoadData();

            pnlFilterBar.Controls.Add(_txtSearch);
            pnlFilterBar.Controls.Add(CreateDivider());
            pnlFilterBar.Controls.Add(lblFilter);
            pnlFilterBar.Controls.Add(_cboFilterStatus);
            pnlFilterBar.Controls.Add(_cboFilterTag);
            pnlFilterBar.Controls.Add(CreateDivider());
            pnlFilterBar.Controls.Add(lblBrowser);
            pnlFilterBar.Controls.Add(_cboBrowserVersion);
            pnlFilterBar.Controls.Add(CreateDivider());
            pnlFilterBar.Controls.Add(_chkMobileMode);
            pnlFilterBar.Controls.Add(lblGridConfig);
            pnlFilterBar.Controls.Add(_numRows);
            pnlFilterBar.Controls.Add(lblRowUnit);
            pnlFilterBar.Controls.Add(_numCols);
            pnlFilterBar.Controls.Add(CreateDivider());
            pnlFilterBar.Controls.Add(_btnRefresh);

            LayoutCompactToolbar(pnlFilterBar);
            pnlFilterBar.Resize += (s, e) => LayoutCompactToolbar(pnlFilterBar);

            // ================= 4. DATAGRIDVIEW (TEXT NHỎ GỌN, DỄ NHÌN) =================
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(243, 244, 246),
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = true,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars = ScrollBars.Both,
                RowTemplate = { Height = 34 }
            };

            // Kích hoạt DoubleBuffered qua Reflection để loại bỏ 100% hiện tượng xé hình / lỗi vẽ khi phóng to thu nhỏ
            try
            {
                typeof(DataGridView).InvokeMember("DoubleBuffered",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                    null, _grid, new object[] { true });
            }
            catch { }

            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(71, 85, 105);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
            _grid.ColumnHeadersHeight = 34;

            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 231, 255);
            _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(17, 24, 39);
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F); // Text nhỏ gọn
            _grid.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);

            _grid.RowTemplate.Height = 36;

            // 1. Cột Checkbox chọn nhiều
            var chkCol = new DataGridViewCheckBoxColumn
            {
                Name = "clCheck",
                HeaderText = "",
                Width = 36,
                Resizable = DataGridViewTriState.False
            };
            _grid.Columns.Add(chkCol);

            // 2. Cột Nút Hành Động (Run / Stop GoLogin style)
            var actCol = new DataGridViewButtonColumn
            {
                Name = "clAction",
                HeaderText = "Hành Động",
                Width = 92,
                UseColumnTextForButtonValue = false,
                Resizable = DataGridViewTriState.False
            };
            _grid.Columns.Add(actCol);

            // 3. Tên Profile (Đã giảm kích thước theo yêu cầu)
            _grid.Columns.Add("clName", "Tên Profile / Email");
            _grid.Columns["clName"].Width = 165;
            _grid.Columns["clName"].MinimumWidth = 130;

            // 4. Trạng Thái
            _grid.Columns.Add("clRunningStatus", "Trạng Thái");
            _grid.Columns["clRunningStatus"].Width = 120;
            _grid.Columns["clRunningStatus"].MinimumWidth = 95;

            // 5. Proxy
            _grid.Columns.Add("clProxy", "Proxy");
            _grid.Columns["clProxy"].Width = 145;
            _grid.Columns["clProxy"].MinimumWidth = 110;

            // 6. Sức Khỏe Proxy
            _grid.Columns.Add("clStatusProxy", "Sức Khỏe Proxy");
            _grid.Columns["clStatusProxy"].Width = 150;
            _grid.Columns["clStatusProxy"].MinimumWidth = 120;

            // 7. Nhãn (Tags) - Đã tăng kích thước
            _grid.Columns.Add("clTags", "🏷️ Nhãn (Tags)");
            _grid.Columns["clTags"].Width = 200;
            _grid.Columns["clTags"].MinimumWidth = 140;

            // 8. Ghi chú - Đã tăng kích thước & tự động mở rộng Fill
            _grid.Columns.Add("clNote", "Ghi Chú");
            _grid.Columns["clNote"].MinimumWidth = 180;
            _grid.Columns["clNote"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // 9. Nút Sửa Nhanh
            var editCol = new DataGridViewButtonColumn
            {
                Name = "clQuickEdit",
                HeaderText = "Sửa",
                Text = "✏️",
                UseColumnTextForButtonValue = true,
                Width = 46,
                Resizable = DataGridViewTriState.False
            };
            _grid.Columns.Add(editCol);

            // Cột ẩn
            _grid.Columns.Add("clId", "ID");
            _grid.Columns["clId"].Visible = false;

            _grid.Columns.Add("clPath", "Thư Mục Dữ Liệu");
            _grid.Columns["clPath"].Visible = false;

            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnLaunch_Click(null, null); };
            _grid.CellContentClick += Grid_CellContentClick;
            _grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
            _grid.CellValueChanged += Grid_CellValueChanged;
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell != null && _grid.CurrentCell.ColumnIndex == _grid.Columns["clCheck"].Index)
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _grid.SelectionChanged += (s, e) => UpdateSelectionUi();
            _grid.MouseDown += Grid_MouseDown;
            _grid.KeyDown += Grid_KeyDown;
            _grid.CellPainting += Grid_CellPainting;

            // ================= 5. STATUS BAR =================
            _lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8F),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Text = "⚡ ADBLogin v2.0 Enterprise sẵn sàng (Offline-first)"
            };

            this.Controls.Add(_grid);
            this.Controls.Add(pnlFilterBar);
            this.Controls.Add(pnlProfileBar);
            this.Controls.Add(headerPanel);
            this.Controls.Add(_lblStatus);
        }

        private Label CreateStatChip(string text, Color backColor, int x)
        {
            return new Label
            {
                Text = text,
                BackColor = backColor,
                ForeColor = Color.FromArgb(241, 245, 249),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Location = new Point(x, 14),
                Padding = new Padding(6, 2, 6, 2),
                AutoSize = true
            };
        }

        private Button CreateCompactButton(string text, Color backColor, Color foreColor, int width, bool isPrimary, string tooltip = "", Color? hoverColor = null)
        {
            Button btn = new Button
            {
                Text = text,
                Size = new Size(width, 28),
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, isPrimary ? FontStyle.Bold : FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = isPrimary ? 0 : 1;
            if (!isPrimary)
            {
                btn.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
            }

            if (hoverColor.HasValue)
            {
                btn.FlatAppearance.MouseOverBackColor = hoverColor.Value;
            }
            else if (isPrimary)
            {
                btn.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(backColor, 0.08f);
            }
            else
            {
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
            }
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.18f);

            if (!string.IsNullOrEmpty(tooltip))
            {
                ToolTip tt = new ToolTip();
                tt.SetToolTip(btn, tooltip);
            }

            return btn;
        }

        private Panel CreateDivider()
        {
            return new Panel
            {
                Size = new Size(1, 20),
                BackColor = Color.FromArgb(229, 231, 235),
                Margin = new Padding(3, 4, 3, 4)
            };
        }

        private void LayoutCompactToolbar(Panel panel)
        {
            int currentX = 8;
            int gap = 3;
            foreach (Control c in panel.Controls)
            {
                if (c is Button)
                {
                    c.Location = new Point(currentX, 5);
                    currentX += c.Width + gap;
                }
                else if (c is Panel && c.Width == 1) // Divider
                {
                    c.Location = new Point(currentX + 2, 9);
                    currentX += 7;
                }
                else if (c is CheckBox)
                {
                    c.Location = new Point(currentX + 2, 9);
                    currentX += c.Width + gap + 3;
                }
                else if (c is NumericUpDown)
                {
                    c.Location = new Point(currentX, 8);
                    currentX += c.Width + gap;
                }
                else if (c is Label)
                {
                    c.Location = new Point(currentX, 11);
                    currentX += c.Width + gap;
                }
                else if (c is ComboBox)
                {
                    c.Location = new Point(currentX, 7);
                    currentX += c.Width + gap;
                }
                else if (c is TextBox)
                {
                    c.Location = new Point(currentX, 7);
                    currentX += c.Width + gap;
                }
            }
        }

        private static ContextMenuStrip _activeSmartMenu = null;

        private void AttachSmartDropdown(Button btn, ContextMenuStrip menu)
        {
            menu.Renderer = new ModernMenuRenderer();
            menu.Font = new Font("Segoe UI", 9F);
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Padding = new Padding(4);
            menu.DropShadowEnabled = true;

            menu.Closed += (s, e) =>
            {
                if (_activeSmartMenu == menu) _activeSmartMenu = null;
            };

            btn.Click += (s, e) =>
            {
                if (menu.Visible)
                {
                    menu.Close();
                    _activeSmartMenu = null;
                }
                else
                {
                    if (_activeSmartMenu != null && _activeSmartMenu != menu && _activeSmartMenu.Visible)
                    {
                        _activeSmartMenu.Close();
                    }
                    _activeSmartMenu = menu;
                    menu.Show(btn, new Point(0, btn.Height + 1));
                }
            };
        }

        private ToolStripMenuItem AddMenuItem(ContextMenuStrip menu, string text, EventHandler onClick, Color? foreColor = null, bool isBold = false)
        {
            var item = new ToolStripMenuItem(text);
            item.Padding = new Padding(8, 5, 8, 5);
            item.Font = new Font("Segoe UI", 9F, isBold ? FontStyle.Bold : FontStyle.Regular);
            if (foreColor.HasValue)
            {
                item.ForeColor = foreColor.Value;
            }
            else
            {
                item.ForeColor = Color.FromArgb(30, 41, 59);
            }

            if (onClick != null)
            {
                item.Click += onClick;
            }

            menu.Items.Add(item);
            return item;
        }

        private void AddMenuSeparator(ContextMenuStrip menu)
        {
            ToolStripSeparator sep = new ToolStripSeparator();
            sep.Margin = new Padding(4, 2, 4, 2);
            menu.Items.Add(sep);
        }

        private class ModernMenuRenderer : ToolStripProfessionalRenderer
        {
            public ModernMenuRenderer() : base(new ModernMenuColorTable()) { }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Selected)
                {
                    Rectangle rc = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                    using (var brush = new SolidBrush(Color.FromArgb(238, 242, 255)))
                    using (var pen = new Pen(Color.FromArgb(199, 210, 254)))
                    {
                        e.Graphics.FillRectangle(brush, rc);
                        e.Graphics.DrawRectangle(pen, rc);
                    }
                }
                else
                {
                    base.OnRenderMenuItemBackground(e);
                }
            }
        }

        private class ModernMenuColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Color.White; } }
            public override Color ImageMarginGradientBegin { get { return Color.White; } }
            public override Color ImageMarginGradientMiddle { get { return Color.White; } }
            public override Color ImageMarginGradientEnd { get { return Color.White; } }
            public override Color MenuBorder { get { return Color.FromArgb(226, 232, 240); } }
            public override Color MenuItemBorder { get { return Color.Transparent; } }
            public override Color MenuItemSelected { get { return Color.FromArgb(238, 242, 255); } }
            public override Color MenuStripGradientBegin { get { return Color.White; } }
            public override Color MenuStripGradientEnd { get { return Color.White; } }
            public override Color SeparatorDark { get { return Color.FromArgb(241, 245, 249); } }
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                if (e.RowIndex == -1 && e.ColumnIndex == _grid.Columns["clCheck"].Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    Rectangle chkRect = new Rectangle(e.CellBounds.X + 11, e.CellBounds.Y + 9, 14, 14);
                    ControlPaint.DrawCheckBox(e.Graphics, chkRect, _allChecked ? ButtonState.Checked : ButtonState.Normal);
                    e.Handled = true;
                }
                return;
            }

            try
            {
                string colName = _grid.Columns[e.ColumnIndex].Name;

                if (colName == "clAction")
                {
                    e.PaintBackground(e.CellBounds, true);
                    string id = _grid.Rows[e.RowIndex].Cells["clId"].Value != null ? _grid.Rows[e.RowIndex].Cells["clId"].Value.ToString() : "";
                    bool isRunning = _sessionManager.IsRunning(id);

                    int btnWidth = 76;
                    int btnHeight = 24;
                    int btnX = e.CellBounds.X + (e.CellBounds.Width - btnWidth) / 2;
                    int btnY = e.CellBounds.Y + (e.CellBounds.Height - btnHeight) / 2;
                    Rectangle btnRect = new Rectangle(btnX, btnY, btnWidth, btnHeight);

                    Color bg = isRunning ? Color.FromArgb(239, 68, 68) : Color.FromArgb(16, 185, 129);
                    Color border = isRunning ? Color.FromArgb(220, 38, 38) : Color.FromArgb(5, 150, 105);
                    string btnText = isRunning ? "⏹ Dừng" : "▶ Chạy";

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (GraphicsPath path = GetRoundedRectangle(btnRect, 5))
                    {
                        using (SolidBrush brush = new SolidBrush(bg))
                        {
                            e.Graphics.FillPath(brush, path);
                        }
                        using (Pen pen = new Pen(border, 1f))
                        {
                            e.Graphics.DrawPath(pen, path);
                        }
                    }

                    using (Font font = new Font("Segoe UI", 8.5F, FontStyle.Bold))
                    {
                        TextRenderer.DrawText(
                            e.Graphics,
                            btnText,
                            font,
                            btnRect,
                            Color.White,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                        );
                    }
                    e.Handled = true;
                }
                else if (colName == "clQuickEdit")
                {
                    e.PaintBackground(e.CellBounds, true);
                    int btnSize = 24;
                    int btnX = e.CellBounds.X + (e.CellBounds.Width - btnSize) / 2;
                    int btnY = e.CellBounds.Y + (e.CellBounds.Height - btnSize) / 2;
                    Rectangle btnRect = new Rectangle(btnX, btnY, btnSize, btnSize);

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (GraphicsPath path = GetRoundedRectangle(btnRect, 4))
                    {
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(241, 245, 249)))
                        {
                            e.Graphics.FillPath(brush, path);
                        }
                        using (Pen pen = new Pen(Color.FromArgb(203, 213, 225), 1f))
                        {
                            e.Graphics.DrawPath(pen, path);
                        }
                    }
                    using (Font font = new Font("Segoe UI", 9F))
                    {
                        TextRenderer.DrawText(
                            e.Graphics,
                            "✏️",
                            font,
                            btnRect,
                            Color.FromArgb(51, 65, 85),
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                        );
                    }
                    e.Handled = true;
                }
                else if (colName == "clTags")
                {
                    e.PaintBackground(e.CellBounds, true);
                    string val = e.Value != null ? e.Value.ToString() : "";
                    DrawTagBadges(e.Graphics, e.CellBounds, val);
                    e.Handled = true;
                }
                else if (colName == "clRunningStatus" && e.Value != null)
                {
                    e.PaintBackground(e.CellBounds, true);

                    string val = e.Value.ToString();
                    bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                    bool isRunning = val.Contains("ĐANG MỞ") || val.StartsWith("▶") || val.Contains("Auto") || val.Contains("Đồng Bộ");

                    Color pillBg = Color.FromArgb(243, 244, 246);
                    Color pillBorder = Color.FromArgb(209, 213, 219);
                    Color textColor = Color.FromArgb(100, 116, 139);

                    if (val.IndexOf("FB", StringComparison.OrdinalIgnoreCase) >= 0 || val.IndexOf("Facebook", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pillBg = Color.FromArgb(239, 246, 255);
                        pillBorder = Color.FromArgb(191, 219, 254);
                        textColor = Color.FromArgb(29, 78, 216);
                    }
                    else if (val.IndexOf("Shopee", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pillBg = Color.FromArgb(255, 247, 237);
                        pillBorder = Color.FromArgb(254, 215, 170);
                        textColor = Color.FromArgb(194, 65, 12);
                    }
                    else if (val.IndexOf("TikTok", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pillBg = Color.FromArgb(241, 245, 249);
                        pillBorder = Color.FromArgb(203, 213, 225);
                        textColor = Color.FromArgb(15, 23, 42);
                    }
                    else if (val.IndexOf("Google", StringComparison.OrdinalIgnoreCase) >= 0 || val.IndexOf("Gmail", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pillBg = Color.FromArgb(254, 242, 242);
                        pillBorder = Color.FromArgb(254, 202, 202);
                        textColor = Color.FromArgb(185, 28, 28);
                    }
                    else if (val.IndexOf("Đồng Bộ", StringComparison.OrdinalIgnoreCase) >= 0 || val.IndexOf("Sync", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pillBg = Color.FromArgb(238, 242, 255);
                        pillBorder = Color.FromArgb(199, 210, 254);
                        textColor = Color.FromArgb(67, 56, 202);
                    }
                    else if (isRunning)
                    {
                        pillBg = Color.FromArgb(220, 252, 231);
                        pillBorder = Color.FromArgb(134, 239, 172);
                        textColor = Color.FromArgb(21, 128, 61);
                    }

                    DrawPillBadge(e.Graphics, e.CellBounds, val, pillBg, pillBorder, textColor, isSelected);
                    e.Handled = true;
                }
                else if (colName == "clStatusProxy" && e.Value != null)
                {
                    e.PaintBackground(e.CellBounds, true);

                    string val = e.Value.ToString();
                    bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                    Color pillBg = Color.FromArgb(243, 244, 246);
                    Color pillBorder = Color.FromArgb(229, 231, 235);
                    Color textColor = Color.FromArgb(100, 116, 139);

                    if (val.Contains("LIVE"))
                    {
                        pillBg = Color.FromArgb(236, 253, 245);
                        pillBorder = Color.FromArgb(167, 243, 208);
                        textColor = Color.FromArgb(4, 120, 87);
                    }
                    else if (val.Contains("DIE"))
                    {
                        pillBg = Color.FromArgb(254, 242, 242);
                        pillBorder = Color.FromArgb(254, 202, 202);
                        textColor = Color.FromArgb(185, 28, 28);
                    }
                    else if (val.Contains("Đang kiểm tra"))
                    {
                        pillBg = Color.FromArgb(254, 243, 199);
                        pillBorder = Color.FromArgb(253, 230, 138);
                        textColor = Color.FromArgb(180, 83, 9);
                    }
                    else if (val.Contains("Direct"))
                    {
                        pillBg = Color.FromArgb(240, 249, 255);
                        pillBorder = Color.FromArgb(186, 230, 253);
                        textColor = Color.FromArgb(3, 105, 161);
                    }

                    DrawPillBadge(e.Graphics, e.CellBounds, val, pillBg, pillBorder, textColor, isSelected);
                    e.Handled = true;
                }
            }
            catch
            {
                e.Handled = false;
            }
        }

        private void GetTagColors(string tag, out Color bg, out Color border, out Color text)
        {
            string t = (tag ?? "").ToLowerInvariant();
            if (t.Contains("fb") || t.Contains("facebook"))
            {
                bg = Color.FromArgb(239, 246, 255);
                border = Color.FromArgb(191, 219, 254);
                text = Color.FromArgb(29, 78, 216);
            }
            else if (t.Contains("shopee"))
            {
                bg = Color.FromArgb(255, 247, 237);
                border = Color.FromArgb(254, 215, 170);
                text = Color.FromArgb(194, 65, 12);
            }
            else if (t.Contains("tiktok"))
            {
                bg = Color.FromArgb(241, 245, 249);
                border = Color.FromArgb(203, 213, 225);
                text = Color.FromArgb(15, 23, 42);
            }
            else if (t.Contains("google") || t.Contains("gmail"))
            {
                bg = Color.FromArgb(254, 242, 242);
                border = Color.FromArgb(254, 202, 202);
                text = Color.FromArgb(185, 28, 28);
            }
            else if (t.Contains("xu") || t.Contains("cày"))
            {
                bg = Color.FromArgb(254, 243, 199);
                border = Color.FromArgb(253, 230, 138);
                text = Color.FromArgb(180, 83, 9);
            }
            else if (t.Contains("chính") || t.Contains("main"))
            {
                bg = Color.FromArgb(236, 253, 245);
                border = Color.FromArgb(167, 243, 208);
                text = Color.FromArgb(4, 120, 87);
            }
            else if (t.Contains("airdrop") || t.Contains("twitter") || t.Contains("x"))
            {
                bg = Color.FromArgb(240, 249, 255);
                border = Color.FromArgb(186, 230, 253);
                text = Color.FromArgb(3, 105, 161);
            }
            else
            {
                bg = Color.FromArgb(243, 244, 246);
                border = Color.FromArgb(229, 231, 235);
                text = Color.FromArgb(75, 85, 99);
            }
        }

        private void DrawTagBadges(Graphics g, Rectangle bounds, string tagsString)
        {
            if (string.IsNullOrWhiteSpace(tagsString) || bounds.Width < 15 || bounds.Height < 10) return;

            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                string[] tags = tagsString.Split(new char[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (tags.Length == 0) return;

                int curX = bounds.X + 6;
                int curY = bounds.Y + 6;
                int height = bounds.Height - 12;
                if (height < 6) return;
                int maxRight = bounds.Right - 4;

                using (Font font = new Font("Segoe UI", 7.5F, FontStyle.Bold))
                {
                    for (int i = 0; i < tags.Length; i++)
                    {
                        string tag = tags[i].Trim();
                        if (string.IsNullOrEmpty(tag)) continue;

                        Color bg, border, text;
                        GetTagColors(tag, out bg, out border, out text);

                        Size textSize = TextRenderer.MeasureText(tag, font);
                        int badgeWidth = textSize.Width + 10;

                        if (curX + badgeWidth > maxRight)
                        {
                            int avail = maxRight - curX;
                            if (avail >= 18)
                            {
                                int remaining = tags.Length - i;
                                string moreText = "+" + remaining;
                                Rectangle moreRect = new Rectangle(curX, curY, avail, height);
                                using (GraphicsPath p = GetRoundedRectangle(moreRect, 4))
                                {
                                    using (SolidBrush b = new SolidBrush(Color.FromArgb(241, 245, 249))) g.FillPath(b, p);
                                    using (Pen pen = new Pen(Color.FromArgb(203, 213, 225), 1f)) g.DrawPath(pen, p);
                                }
                                TextRenderer.DrawText(g, moreText, font, moreRect, Color.FromArgb(71, 85, 105),
                                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                            }
                            break;
                        }

                        Rectangle badgeRect = new Rectangle(curX, curY, badgeWidth, height);
                        using (GraphicsPath path = GetRoundedRectangle(badgeRect, 4))
                        {
                            using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, path);
                            using (Pen pen = new Pen(border, 1f)) g.DrawPath(pen, path);
                        }
                        TextRenderer.DrawText(g, tag, font, badgeRect, text,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                        curX += badgeWidth + 4;
                    }
                }
            }
            catch { }
        }

        private void DrawPillBadge(Graphics g, Rectangle bounds, string text, Color bg, Color border, Color textColor, bool isSelected = false)
        {
            if (bounds.Width < 15 || bounds.Height < 10) return;

            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int padX = 4;
                int padY = 4;
                int w = bounds.Width - (padX * 2);
                int h = bounds.Height - (padY * 2);
                if (w < 8 || h < 8) return;

                Rectangle pillRect = new Rectangle(bounds.X + padX, bounds.Y + padY, w, h);

                if (!isSelected)
                {
                    using (GraphicsPath path = GetRoundedRectangle(pillRect, 4))
                    {
                        using (SolidBrush brush = new SolidBrush(bg))
                        {
                            g.FillPath(brush, path);
                        }
                        using (Pen pen = new Pen(border, 1f))
                        {
                            g.DrawPath(pen, path);
                        }
                    }
                }

                Rectangle textRect = new Rectangle(pillRect.X + 6, pillRect.Y, pillRect.Width - 10, pillRect.Height);
                using (Font font = new Font("Segoe UI", 8F, FontStyle.Bold))
                {
                    Color renderColor = isSelected ? Color.FromArgb(17, 24, 39) : textColor;
                    TextRenderer.DrawText(
                        g,
                        text ?? "",
                        font,
                        textRect,
                        renderColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                    );
                }
            }
            catch { }
        }

        private GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0) return path;

            int maxR = Math.Min(rect.Width / 2, rect.Height / 2);
            if (radius > maxR) radius = maxR;
            if (radius < 1)
            {
                path.AddRectangle(rect);
                return path;
            }

            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void InitializeStatusTimer()
        {
            _statusTimer = new System.Windows.Forms.Timer();
            _statusTimer.Interval = 1500;
            _statusTimer.Tick += (s, e) => UpdateRunningStatus();
            _statusTimer.Start();
        }

        private void UpdateRunningStatus()
        {
            int runningCount = 0;
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (_grid.Rows[i].Cells["clId"].Value == null) continue;
                string id = _grid.Rows[i].Cells["clId"].Value.ToString();
                bool isRunning = _sessionManager.IsRunning(id);

                if (isRunning)
                {
                    runningCount++;
                    string activeTask = _sessionManager.GetRunningTask(id);
                    if (!string.IsNullOrEmpty(activeTask))
                    {
                        _grid.Rows[i].Cells["clRunningStatus"].Value = "▶ " + activeTask;
                    }
                    else
                    {
                        _grid.Rows[i].Cells["clRunningStatus"].Value = "🟢 ĐANG MỞ";
                    }
                }
                else
                {
                    _grid.Rows[i].Cells["clRunningStatus"].Value = "⚪ ĐÃ TẮT";
                }
            }

            _lblStatsRunning.Text = string.Format("Đang mở: {0}", runningCount);
            if (_grid.Columns.Contains("clAction"))
            {
                _grid.InvalidateColumn(_grid.Columns["clAction"].Index);
            }
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            string colName = _grid.Columns[e.ColumnIndex].Name;
            if (_grid.Rows[e.RowIndex].Cells["clId"].Value == null) return;
            string id = _grid.Rows[e.RowIndex].Cells["clId"].Value.ToString();
            var profile = _accountManager.GetProfile(id);
            if (profile == null) return;

            if (colName == "clAction")
            {
                bool isRunning = _sessionManager.IsRunning(id);
                if (isRunning)
                {
                    StopSingleProfile(id);
                }
                else
                {
                    LaunchSingleProfile(profile);
                }
            }
            else if (colName == "clQuickEdit")
            {
                OpenEditProfileDialog(profile);
            }
            else if (colName == "clStatusProxy" || colName == "clProxy")
            {
                CheckSingleProxy(e.RowIndex, profile);
            }
            else if (colName == "clCheck")
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                UpdateSelectionUi();
            }
        }

        private void Grid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (_grid.Columns.Contains("clCheck") && e.ColumnIndex == _grid.Columns["clCheck"].Index)
            {
                _allChecked = !_allChecked;
                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    _grid.Rows[i].Cells["clCheck"].Value = _allChecked;
                }
                _grid.InvalidateColumn(_grid.Columns["clCheck"].Index);
                UpdateSelectionUi();
            }
        }

        private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && _grid.Columns.Contains("clCheck") && e.ColumnIndex == _grid.Columns["clCheck"].Index)
            {
                UpdateSelectionUi();
            }
        }

        private void UpdateSelectionUi()
        {
            int checkedCount = 0;
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var val = _grid.Rows[i].Cells["clCheck"].Value;
                if (val != null && (bool)val) checkedCount++;
            }

            if (checkedCount == 0)
            {
                checkedCount = _grid.SelectedRows.Count;
            }

            if (_lblStatsSelected != null)
            {
                _lblStatsSelected.Text = string.Format("Đã chọn: {0}", checkedCount);
            }

            if (_btnLaunch != null)
            {
                _btnLaunch.Text = checkedCount > 1 ? string.Format("▶ Chạy ({0})", checkedCount) : "▶ Chạy";
            }
            if (_btnStop != null)
            {
                _btnStop.Text = checkedCount > 1 ? string.Format("⏹ Dừng ({0})", checkedCount) : "⏹ Dừng";
            }
            if (_btnToolsMenu != null)
            {
                _btnToolsMenu.Text = checkedCount > 1 ? string.Format("🛠️ Tiện Ích ({0}) ▾", checkedCount) : "🛠️ Tiện Ích ▾";
            }
            if (_btnProxyMenu != null)
            {
                _btnProxyMenu.Text = checkedCount > 1 ? string.Format("🌐 Proxy ({0}) ▾", checkedCount) : "🌐 Proxy Studio ▾";
            }
        }

        private void LaunchSingleProfile(UserProfile profile)
        {
            if (profile == null) return;

            ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    this.Invoke((MethodInvoker)(() =>
                    {
                        _lblStatus.Text = string.Format("Đang mở trình duyệt [{0}]...", profile.ProfileName);
                    }));

                    _launcherService.LaunchBrowser(profile, null, 0, 1, 1, _chkMobileMode != null && _chkMobileMode.Checked);

                    this.Invoke((MethodInvoker)(() =>
                    {
                        _lblStatus.Text = string.Format("Đã mở trình duyệt [{0}] thành công!", profile.ProfileName);
                        UpdateRunningStatus();
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke((MethodInvoker)(() =>
                    {
                        _lblStatus.Text = string.Format("Lỗi mở [{0}]: {1}", profile.ProfileName, ex.Message);
                    }));
                }
            });
        }

        private void StopSingleProfile(string profileId)
        {
            if (string.IsNullOrEmpty(profileId)) return;
            if (_sessionManager.CloseSession(profileId))
            {
                UpdateRunningStatus();
                _lblStatus.Text = "Đã tắt trình duyệt!";
            }
        }

        private void CheckSingleProxy(int rowIndex, UserProfile profile)
        {
            if (profile == null) return;
            if (string.IsNullOrEmpty(profile.Proxy))
            {
                _grid.Rows[rowIndex].Cells["clStatusProxy"].Value = "🟢 Direct";
                _grid.InvalidateCell(_grid.Rows[rowIndex].Cells["clStatusProxy"]);
                return;
            }

            _grid.Rows[rowIndex].Cells["clStatusProxy"].Value = "⏳ Đang test...";
            _grid.InvalidateCell(_grid.Rows[rowIndex].Cells["clStatusProxy"]);

            ThreadPool.QueueUserWorkItem((state) =>
            {
                var result = _proxyChecker.CheckProxyString(profile.Proxy);
                this.Invoke((MethodInvoker)(() =>
                {
                    if (rowIndex < _grid.Rows.Count)
                    {
                        _grid.Rows[rowIndex].Cells["clStatusProxy"].Value = result.ToString();
                        _grid.InvalidateCell(_grid.Rows[rowIndex].Cells["clStatusProxy"]);
                    }
                }));
            });
        }

        private void OpenEditProfileDialog(UserProfile profile)
        {
            if (profile == null) return;
            using (var form = new ProfileEditForm(profile))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _accountManager.AddOrUpdateProfile(form.Profile);

                    // Đồng bộ ngay lập tức tên và cấu hình mới vào tệp Preferences & Local State của Profile trên ổ đĩa
                    try
                    {
                        string profileDir = form.Profile.BrowserPath;
                        if (string.IsNullOrEmpty(profileDir) || !Directory.Exists(profileDir))
                        {
                            profileDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files", "Profiles", form.Profile.ProfileId);
                        }
                        if (Directory.Exists(profileDir))
                        {
                            var proxy = !string.IsNullOrEmpty(form.Profile.Proxy) ? ProxySettings.Parse(form.Profile.Proxy) : null;
                            new ChromiumPreferenceService().UpdatePreferences(profileDir, proxy, form.Profile.UserAgent, form.Profile.ProfileName);
                        }
                    }
                    catch { }

                    LoadData();
                    _lblStatus.Text = string.Format("Đã cập nhật profile [{0}]!", profile.ProfileName);
                }
            }
        }

        private void PopulateFilterTags()
        {
            if (_cboFilterTag == null) return;
            string current = _cboFilterTag.SelectedItem != null ? _cboFilterTag.SelectedItem.ToString() : "Tất cả nhãn";
            _cboFilterTag.Items.Clear();
            _cboFilterTag.Items.Add("Tất cả nhãn");

            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in _accountManager.Profiles)
            {
                if (!string.IsNullOrEmpty(p.Tags))
                {
                    var parts = p.Tags.Split(new char[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        string t = part.Trim();
                        if (!string.IsNullOrEmpty(t)) set.Add(t);
                    }
                }
            }

            foreach (var tag in set)
            {
                _cboFilterTag.Items.Add("#" + tag);
            }

            int idx = _cboFilterTag.Items.IndexOf(current);
            _cboFilterTag.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void PopulateBrowserVersions()
        {
            _cboBrowserVersion.Items.Clear();
            var browsers = BrowserVersionService.GetAvailableBrowsers();
            string savedSetting = LocalConfigManager.Instance.CurrentConfig != null ? LocalConfigManager.Instance.CurrentConfig.SelectedBrowserVersion : "144";

            int selectedIndex = 0;
            for (int i = 0; i < browsers.Count; i++)
            {
                var b = browsers[i];
                _cboBrowserVersion.Items.Add(b);

                if (!string.IsNullOrEmpty(savedSetting) &&
                    (b.DisplayName.IndexOf(savedSetting, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     b.VersionKey.Equals(savedSetting, StringComparison.OrdinalIgnoreCase)))
                {
                    selectedIndex = i;
                }
            }

            if (_cboBrowserVersion.Items.Count > 0)
            {
                _cboBrowserVersion.SelectedIndex = selectedIndex;
            }
        }

        private void CboBrowserVersion_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selected = _cboBrowserVersion.SelectedItem as BrowserVersionInfo;
            if (selected == null) return;

            if (selected.VersionKey == "custom")
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = "Chọn file thực thi Chrome / Orbita (chrome.exe)";
                    ofd.Filter = "Trình duyệt (*.exe)|*.exe|Tất cả tệp (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        LocalConfigManager.Instance.CurrentConfig.SelectedBrowserVersion = ofd.FileName;
                        LocalConfigManager.Instance.SaveConfig();
                        if (_lblStatus != null)
                        {
                            _lblStatus.Text = string.Format("Đã chọn trình duyệt tùy chỉnh: {0}", Path.GetFileName(ofd.FileName));
                        }
                    }
                    else
                    {
                        PopulateBrowserVersions();
                    }
                }
            }
            else
            {
                LocalConfigManager.Instance.CurrentConfig.SelectedBrowserVersion = selected.VersionKey;
                LocalConfigManager.Instance.SaveConfig();
                if (_lblStatus != null)
                {
                    _lblStatus.Text = string.Format("Đã chuyển phiên bản trình duyệt mặc định: {0}", selected.DisplayName);
                }
            }
        }

        private void InitializeContextMenu()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Font = new Font("Segoe UI", 9F);

            var itemLaunch = _contextMenu.Items.Add("▶ Khởi chạy trình duyệt");
            itemLaunch.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemLaunch.Click += (s, e) => LaunchProfiles(false);

            var itemLaunchMobile = _contextMenu.Items.Add("📱 Khởi chạy Mobile (Gọn nhẹ)");
            itemLaunchMobile.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemLaunchMobile.ForeColor = Color.FromArgb(2, 132, 199);
            itemLaunchMobile.Click += (s, e) => LaunchProfiles(true);

            var itemStop = _contextMenu.Items.Add("⏹ Tắt trình duyệt này");
            itemStop.ForeColor = Color.FromArgb(220, 38, 38);
            itemStop.Click += BtnStop_Click;

            var itemAutoFb = _contextMenu.Items.Add("🤖 Kịch bản Facebook (Auto FB)");
            itemAutoFb.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemAutoFb.ForeColor = Color.FromArgb(24, 119, 242);
            itemAutoFb.Click += (s, e) =>
            {
                try
                {
                    var fbForm = new FacebookAutomationForm(_accountManager.GetAllProfiles());
                    fbForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Auto FB:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemAutoGoogle = _contextMenu.Items.Add("🌐 Kịch bản Google & Gmail");
            itemAutoGoogle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemAutoGoogle.ForeColor = Color.FromArgb(219, 68, 55);
            itemAutoGoogle.Click += (s, e) =>
            {
                try
                {
                    var gForm = new GoogleAutomationForm(_accountManager.GetAllProfiles());
                    gForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Auto Google:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemAutoTikTok = _contextMenu.Items.Add("🎵 Kịch bản TikTok (FYP)");
            itemAutoTikTok.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemAutoTikTok.ForeColor = Color.FromArgb(15, 23, 42);
            itemAutoTikTok.Click += (s, e) =>
            {
                try
                {
                    var ttForm = new TikTokAutomationForm(_accountManager.GetAllProfiles());
                    ttForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Auto TikTok:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemAutoShopee = _contextMenu.Items.Add("🛒 Kịch bản Shopee (Cày Xu & Seeding)");
            itemAutoShopee.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemAutoShopee.ForeColor = Color.FromArgb(238, 77, 45);
            itemAutoShopee.Click += (s, e) =>
            {
                try
                {
                    var shopeeForm = new ShopeeAutomationForm(_accountManager.GetAllProfiles());
                    shopeeForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Auto Shopee:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemAutoTwitter = _contextMenu.Items.Add("🐦 Kịch bản X / Twitter (Airdrop)");
            itemAutoTwitter.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemAutoTwitter.ForeColor = Color.FromArgb(29, 155, 240);
            itemAutoTwitter.Click += (s, e) =>
            {
                try
                {
                    var twForm = new TwitterAutomationForm(_accountManager.GetAllProfiles());
                    twForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Auto Twitter:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemAutoTelegram = _contextMenu.Items.Add("✈️ Kịch bản Telegram Web");
            itemAutoTelegram.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemAutoTelegram.ForeColor = Color.FromArgb(34, 158, 217);
            itemAutoTelegram.Click += (s, e) =>
            {
                try
                {
                    var tgForm = new TelegramAutomationForm(_accountManager.GetAllProfiles());
                    tgForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Auto Telegram:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemAutoDiscord = _contextMenu.Items.Add("👾 Kịch bản Discord (Auto Join & Cày Cấp)");
            itemAutoDiscord.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemAutoDiscord.ForeColor = Color.FromArgb(88, 101, 242);
            itemAutoDiscord.Click += (s, e) =>
            {
                try
                {
                    var dcForm = new DiscordAutomationForm(_accountManager.GetAllProfiles());
                    dcForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Auto Discord:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemAutoInstagram = _contextMenu.Items.Add("📸 Kịch bản Instagram (Nuôi Feed & Reels)");
            itemAutoInstagram.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemAutoInstagram.ForeColor = Color.FromArgb(193, 53, 132);
            itemAutoInstagram.Click += (s, e) =>
            {
                try
                {
                    var instaForm = new InstagramAutomationForm(_accountManager.GetAllProfiles());
                    instaForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Auto Instagram:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemAutoLazada = _contextMenu.Items.Add("🛍️ Kịch bản Lazada (LazCoins & Voucher)");
            itemAutoLazada.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemAutoLazada.ForeColor = Color.FromArgb(15, 23, 42);
            itemAutoLazada.Click += (s, e) =>
            {
                try
                {
                    var lzForm = new LazadaAutomationForm(_accountManager.GetAllProfiles());
                    lzForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Auto Lazada:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemScheduler = _contextMenu.Items.Add("⏰ Quản Lý Lập Lịch (Scheduler)");
            itemScheduler.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemScheduler.ForeColor = Color.FromArgb(16, 185, 129);
            itemScheduler.Click += (s, e) =>
            {
                try
                {
                    var scForm = new SchedulerManagerForm();
                    scForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Lập Lịch:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemBatch = _contextMenu.Items.Add("📥 Nhập Nick Hàng Loạt (Batch Importer Pro)");
            itemBatch.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemBatch.ForeColor = Color.FromArgb(16, 185, 129);
            itemBatch.Click += (s, e) =>
            {
                using (var imp = new BatchAccountImporterForm())
                {
                    if (imp.ShowDialog(this) == DialogResult.OK)
                    {
                        LoadData();
                    }
                }
            };

            _contextMenu.Items.Add(new ToolStripSeparator());

            var itemSync = _contextMenu.Items.Add("⚡ Đồng Bộ Chuột & Phím (Synchronizer)");
            itemSync.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemSync.ForeColor = Color.FromArgb(99, 102, 241);
            itemSync.Click += (s, e) =>
            {
                try
                {
                    var syncForm = new SynchronizerForm(_accountManager.GetAllProfiles());
                    syncForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở giao diện Synchronizer:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemHmaProxy = _contextMenu.Items.Add("🌐 Quản Lý Multi-Proxy Studio (WARP & VPN)");
            itemHmaProxy.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemHmaProxy.ForeColor = Color.FromArgb(14, 165, 233);
            itemHmaProxy.Click += (s, e) =>
            {
                try
                {
                    var hmaForm = new HmaMultiProxyForm(_accountManager.GetAllProfiles());
                    hmaForm.FormClosed += (fs, fe) => LoadData();
                    hmaForm.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở HMA Multi-Proxy:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemAdvSettings = _contextMenu.Items.Add("⚙️ Cấu Hình Proxy Xoay & Captcha");
            itemAdvSettings.Click += (s, e) =>
            {
                try
                {
                    var setForm = new AdvancedSettingsForm();
                    setForm.ShowDialog(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Lỗi mở cấu hình:\n{0}", ex.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var itemCopyCdp = _contextMenu.Items.Add("🌐 Sao chép Debugging Port (CDP)");
            itemCopyCdp.Click += (s, e) =>
            {
                var selected = GetSelectedProfiles();
                if (selected.Count > 0)
                {
                    int port = _sessionManager.GetDebuggingPort(selected[0].ProfileId);
                    if (port > 0)
                    {
                        Clipboard.SetText(string.Format("http://127.0.0.1:{0}", port));
                        MessageBox.Show(string.Format("Đã sao chép địa chỉ CDP: http://127.0.0.1:{0}", port), "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Profile này chưa mở hoặc chưa có cổng CDP.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            };

            _contextMenu.Items.Add(new ToolStripSeparator());

            var itemCheck = _contextMenu.Items.Add("⚡ Kiểm tra Proxy");
            itemCheck.Click += BtnCheckProxy_Click;

            _contextMenu.Items.Add(new ToolStripSeparator());

            var itemEdit = _contextMenu.Items.Add("✏️ Sửa cấu hình");
            itemEdit.Click += BtnEdit_Click;

            var itemQuickTag = _contextMenu.Items.Add("🏷️ Gắn Nhãn (Tags) nhanh...");
            itemQuickTag.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemQuickTag.ForeColor = Color.FromArgb(139, 92, 246);
            itemQuickTag.Click += (s, e) => ShowQuickTagDialog();

            var itemFolder = _contextMenu.Items.Add("📁 Mở thư mục ổ đĩa");
            itemFolder.Click += BtnOpenFolder_Click;

            var itemCopy = _contextMenu.Items.Add("📋 Sao chép Proxy");
            itemCopy.Click += BtnCopyProxy_Click;

            _contextMenu.Items.Add(new ToolStripSeparator());

            var itemDelete = _contextMenu.Items.Add("🗑️ Xóa Profile đã chọn");
            itemDelete.ForeColor = Color.Red;
            itemDelete.Click += BtnDelete_Click;

            _grid.ContextMenuStrip = _contextMenu;
        }

        private void ShowQuickTagDialog()
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 profile để gắn nhãn!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (Form dlg = new Form())
            {
                dlg.Text = string.Format("Gắn Nhãn Cho {0} Profile Đã Chọn", selected.Count);
                dlg.Size = new Size(500, 260);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Color.FromArgb(248, 250, 252);
                dlg.Font = new Font("Segoe UI", 9.5F);

                Label lblPrompt = new Label
                {
                    Text = string.Format("Nhập nhãn muốn gán cho {0} profile đang chọn (phân cách bằng dấu phẩy):", selected.Count),
                    Location = new Point(20, 15),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 41, 59)
                };

                TextBox txtInput = new TextBox
                {
                    Location = new Point(20, 42),
                    Width = 445,
                    Font = new Font("Segoe UI", 10F)
                };
                if (selected.Count == 1) txtInput.Text = selected[0].Tags ?? "";

                FlowLayoutPanel pnlQuick = new FlowLayoutPanel
                {
                    Location = new Point(20, 75),
                    Width = 445,
                    Height = 30,
                    WrapContents = false
                };

                string[] commonTags = new string[] { "Facebook", "Shopee", "TikTok", "Google", "Nuôi nick", "Cày Xu", "Airdrop", "Acc Chính" };
                foreach (var qTag in commonTags)
                {
                    string t = qTag;
                    Button btn = new Button
                    {
                        Text = "+" + t,
                        AutoSize = true,
                        Height = 24,
                        BackColor = Color.FromArgb(241, 245, 249),
                        ForeColor = Color.FromArgb(51, 65, 85),
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 8F),
                        Cursor = Cursors.Hand,
                        Margin = new Padding(0, 0, 4, 0)
                    };
                    btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
                    btn.Click += (s2, e2) =>
                    {
                        string cur = txtInput.Text.Trim();
                        if (string.IsNullOrEmpty(cur)) txtInput.Text = t;
                        else if (!cur.Contains(t)) txtInput.Text = cur + ", " + t;
                        txtInput.SelectionStart = txtInput.Text.Length;
                    };
                    pnlQuick.Controls.Add(btn);
                }

                CheckBox chkAppend = new CheckBox
                {
                    Text = "Thêm vào nhãn hiện có (thay vì ghi đè hoàn toàn)",
                    Location = new Point(20, 115),
                    AutoSize = true,
                    Checked = (selected.Count > 1),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.FromArgb(71, 85, 105)
                };

                Button btnOk = new Button
                {
                    Text = "✔ Áp Dụng",
                    DialogResult = DialogResult.OK,
                    Size = new Size(115, 34),
                    Location = new Point(235, 160),
                    BackColor = Color.FromArgb(16, 185, 129),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnOk.FlatAppearance.BorderSize = 0;

                Button btnCancel = new Button
                {
                    Text = "Hủy",
                    DialogResult = DialogResult.Cancel,
                    Size = new Size(95, 34),
                    Location = new Point(365, 160),
                    BackColor = Color.FromArgb(241, 245, 249),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9F),
                    Cursor = Cursors.Hand
                };
                btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

                dlg.Controls.Add(lblPrompt);
                dlg.Controls.Add(txtInput);
                dlg.Controls.Add(pnlQuick);
                dlg.Controls.Add(chkAppend);
                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string newTags = txtInput.Text.Trim();
                    bool append = chkAppend.Checked;

                    foreach (var prof in selected)
                    {
                        if (append)
                        {
                            string oldTags = prof.Tags ?? "";
                            if (string.IsNullOrEmpty(oldTags))
                            {
                                prof.Tags = newTags;
                            }
                            else
                            {
                                var existing = new List<string>(oldTags.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
                                var toAdd = newTags.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var item in toAdd)
                                {
                                    string itm = item.Trim();
                                    if (!string.IsNullOrEmpty(itm) && !existing.Exists(e => e.Trim().Equals(itm, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        existing.Add(itm);
                                    }
                                }
                                prof.Tags = string.Join(", ", existing.ToArray());
                            }
                        }
                        else
                        {
                            prof.Tags = newTags;
                        }
                    }

                    _accountManager.SaveProfiles();
                    FilterData();
                    _lblStatus.Text = string.Format("Đã cập nhật nhãn cho {0} profile thành công!", selected.Count);
                }
            }
        }

        private void LoadData()
        {
            _accountManager.LoadProfiles();
            PopulateFilterTags();
            FilterData();
            UpdateRunningStatus();
            UpdateSelectionUi();
        }

        private void FilterData()
        {
            _grid.Rows.Clear();
            string keyword = _txtSearch != null ? _txtSearch.Text.Trim() : "";
            if (keyword == "🔍 Tìm profile, proxy, tag...")
            {
                keyword = "";
            }
            keyword = keyword.ToLower();
            string statusFilter = _cboFilterStatus != null && _cboFilterStatus.SelectedIndex > 0 
                ? _cboFilterStatus.SelectedItem.ToString() : "Tất cả trạng thái";
            string tagFilter = _cboFilterTag != null && _cboFilterTag.SelectedIndex > 0 
                ? _cboFilterTag.SelectedItem.ToString() : "Tất cả nhãn";

            int matchCount = 0;
            int proxyCount = 0;

            foreach (var p in _accountManager.Profiles)
            {
                bool isRunning = _sessionManager.IsRunning(p.ProfileId);
                if (statusFilter == "🟢 Đang mở" && !isRunning) continue;
                if (statusFilter == "⚪ Đã tắt" && isRunning) continue;

                if (tagFilter != "Tất cả nhãn" && !string.IsNullOrEmpty(tagFilter))
                {
                    string cleanTag = tagFilter.StartsWith("#") ? tagFilter.Substring(1) : tagFilter;
                    string pTags = p.Tags ?? "";
                    if (pTags.IndexOf(cleanTag, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }

                if (!string.IsNullOrEmpty(keyword))
                {
                    bool matchName = (p.ProfileName ?? "").ToLower().Contains(keyword);
                    bool matchProxy = (p.Proxy ?? "").ToLower().Contains(keyword);
                    bool matchNote = (p.Notes ?? "").ToLower().Contains(keyword);
                    bool matchId = (p.ProfileId ?? "").ToLower().Contains(keyword);
                    bool matchTag = (p.Tags ?? "").ToLower().Contains(keyword);

                    if (!matchName && !matchProxy && !matchNote && !matchId && !matchTag)
                    {
                        continue;
                    }
                }

                string displayProxy = string.IsNullOrEmpty(p.Proxy) ? "⚪ Không Proxy" : p.Proxy;
                if (!string.IsNullOrEmpty(p.Proxy)) proxyCount++;

                string proxyStatus = string.IsNullOrEmpty(p.Proxy) ? "🟢 Direct" : "Chưa kiểm tra";
                string displayPath = string.IsNullOrEmpty(p.BrowserPath) ? "Mặc định" : p.BrowserPath;
                string displayTags = string.IsNullOrEmpty(p.Tags) ? "" : p.Tags;

                string runningStatus = "⚪ ĐÃ TẮT";
                if (isRunning)
                {
                    string activeTask = _sessionManager.GetRunningTask(p.ProfileId);
                    runningStatus = !string.IsNullOrEmpty(activeTask) ? ("▶ " + activeTask) : "🟢 ĐANG MỞ";
                }

                _grid.Rows.Add(
                    false,              // clCheck
                    "▶ Chạy",          // clAction
                    p.ProfileName,      // clName
                    runningStatus,      // clRunningStatus
                    displayProxy,       // clProxy
                    proxyStatus,        // clStatusProxy
                    displayTags,        // clTags
                    p.Notes,            // clNote
                    "✏️",               // clQuickEdit
                    p.ProfileId,        // clId
                    displayPath         // clPath
                );

                matchCount++;
            }

            _lblStatsTotal.Text = string.Format("Tổng: {0}", _accountManager.Profiles.Count);
            _lblStatsProxy.Text = string.Format("Proxy: {0}", proxyCount);
            _lblStatus.Text = string.Format("Hiển thị {0} / {1} profile phù hợp", matchCount, _accountManager.Profiles.Count);
            UpdateSelectionUi();
        }

        private List<UserProfile> GetSelectedProfiles()
        {
            var list = new List<UserProfile>();
            var checkedIds = new HashSet<string>();

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var val = _grid.Rows[i].Cells["clCheck"].Value;
                if (val != null && (bool)val)
                {
                    if (_grid.Rows[i].Cells["clId"].Value != null)
                    {
                        checkedIds.Add(_grid.Rows[i].Cells["clId"].Value.ToString());
                    }
                }
            }

            if (checkedIds.Count > 0)
            {
                foreach (var id in checkedIds)
                {
                    var p = _accountManager.GetProfile(id);
                    if (p != null) list.Add(p);
                }
                return list;
            }

            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                if (row.Cells["clId"].Value != null)
                {
                    string id = row.Cells["clId"].Value.ToString();
                    var p = _accountManager.GetProfile(id);
                    if (p != null && !list.Contains(p)) list.Add(p);
                }
            }
            return list;
        }

        private void BtnLaunch_Click(object sender, EventArgs e)
        {
            LaunchProfiles(_chkMobileMode != null && _chkMobileMode.Checked);
        }

        private void LaunchProfiles(bool isMobileMode)
        {
            var selectedProfiles = GetSelectedProfiles();
            if (selectedProfiles.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 profile để mở!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int rows = (int)_numRows.Value;
            int cols = (int)_numCols.Value;

            _lblStatus.Text = string.Format("Đang mở {0} profile...", selectedProfiles.Count);

            ThreadPool.QueueUserWorkItem((state) =>
            {
                for (int i = 0; i < selectedProfiles.Count; i++)
                {
                    var profile = selectedProfiles[i];
                    int windowIndex = i;

                    try
                    {
                        this.Invoke((MethodInvoker)(() =>
                        {
                            _lblStatus.Text = string.Format("Đang khởi chạy [{0}] (#{1})...", profile.ProfileName, windowIndex + 1);
                        }));

                        _launcherService.LaunchBrowser(profile, null, windowIndex, rows, cols, isMobileMode);
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((MethodInvoker)(() =>
                        {
                            _lblStatus.Text = string.Format("Lỗi mở [{0}]: {1}", profile.ProfileName, ex.Message);
                        }));
                    }

                    Thread.Sleep(300);
                }

                this.Invoke((MethodInvoker)(() =>
                {
                    _lblStatus.Text = string.Format("Đã hoàn tất mở {0} profile!", selectedProfiles.Count);
                    UpdateRunningStatus();
                }));
            });
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn profile cần tắt!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int closed = 0;
            foreach (var p in selected)
            {
                if (_sessionManager.CloseSession(p.ProfileId))
                {
                    closed++;
                }
            }

            UpdateRunningStatus();
            _lblStatus.Text = string.Format("Đã tắt {0} trình duyệt thành công!", closed);
        }

        private void BtnStopAll_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show("Bạn có chắc chắn muốn TẮT TOÀN BỘ các trình duyệt đang mở không?", "Xác Nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                int closed = _sessionManager.CloseAllSessions();
                UpdateRunningStatus();
                _lblStatus.Text = string.Format("Đã tắt toàn bộ {0} trình duyệt!", closed);
            }
        }

        private void BtnAutoCreate_Click(object sender, EventArgs e)
        {
            using (var form = new AutoCreateForm(_accountManager))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadData();
                    _lblStatus.Text = string.Format("Đã tự động tạo mới {0} profile!", form.CreatedCount);
                }
            }
        }

        private void BtnCheckProxy_Click(object sender, EventArgs e)
        {
            var selectedProfiles = GetSelectedProfiles();
            if (selectedProfiles.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn profile để kiểm tra Proxy!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                string id = row.Cells["clId"].Value.ToString();
                var profile = _accountManager.GetProfile(id);
                if (profile == null) continue;

                if (string.IsNullOrEmpty(profile.Proxy))
                {
                    row.Cells["clStatusProxy"].Value = "🟢 Direct";
                    continue;
                }

                row.Cells["clStatusProxy"].Value = "⏳ Đang kiểm tra...";

                int rowIndex = row.Index;
                ThreadPool.QueueUserWorkItem((state) =>
                {
                    var result = _proxyChecker.CheckProxyString(profile.Proxy);
                    this.Invoke((MethodInvoker)(() =>
                    {
                        if (rowIndex < _grid.Rows.Count)
                        {
                            _grid.Rows[rowIndex].Cells["clStatusProxy"].Value = result.ToString();
                            _grid.InvalidateCell(_grid.Rows[rowIndex].Cells["clStatusProxy"]);
                        }
                    }));
                });
            }
        }

        private void BtnCheckAllProxy_Click(object sender, EventArgs e)
        {
            int total = _grid.Rows.Count;
            if (total == 0) return;

            if (_itemCheckAllProxy != null) _itemCheckAllProxy.Enabled = false;
            _lblStatus.Text = "Đang kiểm tra toàn bộ danh sách Proxy...";

            ThreadPool.QueueUserWorkItem((state) =>
            {
                int liveCount = 0;
                int dieCount = 0;
                int directCount = 0;

                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    int rowIndex = i;
                    string id = "";
                    this.Invoke((MethodInvoker)(() =>
                    {
                        id = _grid.Rows[rowIndex].Cells["clId"].Value.ToString();
                        _grid.Rows[rowIndex].Cells["clStatusProxy"].Value = "⏳ Đang kiểm tra...";
                        _grid.InvalidateCell(_grid.Rows[rowIndex].Cells["clStatusProxy"]);
                    }));

                    var profile = _accountManager.GetProfile(id);
                    if (profile == null || string.IsNullOrEmpty(profile.Proxy))
                    {
                        directCount++;
                        this.Invoke((MethodInvoker)(() =>
                        {
                            if (rowIndex < _grid.Rows.Count)
                            {
                                _grid.Rows[rowIndex].Cells["clStatusProxy"].Value = "🟢 Direct";
                                _grid.InvalidateCell(_grid.Rows[rowIndex].Cells["clStatusProxy"]);
                            }
                        }));
                        continue;
                    }

                    var result = _proxyChecker.CheckProxyString(profile.Proxy);
                    if (result.IsLive) liveCount++; else dieCount++;

                    this.Invoke((MethodInvoker)(() =>
                    {
                        if (rowIndex < _grid.Rows.Count)
                        {
                            var cell = _grid.Rows[rowIndex].Cells["clStatusProxy"];
                            cell.Value = result.ToString();
                            cell.Style.ForeColor = result.IsLive ? Color.DarkGreen : Color.Red;
                            _grid.InvalidateCell(cell);
                            _lblStatus.Text = string.Format("Check Proxy: {0}/{1} (Live: {2} | Die: {3})", rowIndex + 1, total, liveCount, dieCount);
                        }
                    }));

                    Thread.Sleep(20);
                }

                this.Invoke((MethodInvoker)(() =>
                {
                    if (_itemCheckAllProxy != null) _itemCheckAllProxy.Enabled = true;
                    _lblStatus.Text = string.Format("Hoàn tất! Live: {0} | Die: {1} | Direct: {2}", liveCount, dieCount, directCount);
                }));
            });
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var form = new ProfileEditForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _accountManager.AddOrUpdateProfile(form.Profile);
                    LoadData();
                    _lblStatus.Text = "Đã thêm profile mới thành công!";
                }
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn 1 profile để chỉnh sửa!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OpenEditProfileDialog(selected[0]);
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 profile để xóa!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                string.Format("Bạn có chắc muốn xóa {0} profile đã chọn không?", selected.Count),
                "Xác Nhận Xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm == DialogResult.Yes)
            {
                foreach (var p in selected)
                {
                    _sessionManager.CloseSession(p.ProfileId);
                    _accountManager.RemoveProfile(p.ProfileId);
                }
                LoadData();
                _lblStatus.Text = string.Format("Đã xóa {0} profile thành công", selected.Count);
            }
        }

        private void BtnOpenFolder_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0) return;

            string targetDir = selected[0].BrowserPath;
            if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
            {
                targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files", "Profiles", selected[0].ProfileId);
            }

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            Process.Start("explorer.exe", targetDir);
        }

        private void BtnCopyProxy_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0) return;

            if (string.IsNullOrEmpty(selected[0].Proxy))
            {
                MessageBox.Show("Profile này dùng mạng trực tiếp (Direct).", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Clipboard.SetText(selected[0].Proxy);
            _lblStatus.Text = string.Format("Đã sao chép Proxy của [{0}]!", selected[0].ProfileName);
        }

        private void Grid_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hit = _grid.HitTest(e.X, e.Y);
                if (hit.RowIndex >= 0)
                {
                    if (!_grid.Rows[hit.RowIndex].Selected)
                    {
                        _grid.ClearSelection();
                        _grid.Rows[hit.RowIndex].Selected = true;
                    }
                }
            }
        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                BtnLaunch_Click(null, null);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                BtnDelete_Click(null, null);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                LoadData();
                e.Handled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            LocalApiService.Instance.Stop();
            base.OnFormClosing(e);
        }

        [STAThread]
        public static void Main()
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072 | (System.Net.SecurityProtocolType)768 | System.Net.SecurityProtocolType.Tls;
                System.Net.ServicePointManager.ServerCertificateValidationCallback = (s, c, ch, err) => true;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString());
                    MessageBox.Show(string.Format("Lỗi khởi chạy ứng dụng:\n{0}", ex.Message), "ADBLogin - Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
        }
    }
}
