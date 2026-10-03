using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;

namespace ADBLogin.UI
{
    /// <summary>
    /// Giao diện điều khiển Đồng bộ Chuột & Phím thời gian thực (Multi-Control Synchronizer)
    /// Đã tối ưu hóa bố cục linh hoạt (Fluid Responsive Layout), chống tràn tuyệt đối trên mọi màn hình.
    /// </summary>
    public class SynchronizerForm : Form
    {
        private ComboBox _cboMaster;
        private CheckedListBox _chkSlaves;
        private Button _btnSelectAllSlaves;
        private Button _btnSelectRunningSlaves;
        private Button _btnDeselectSlaves;
        private Button _btnLaunchSlaves;
        private Button _btnTileWindows;
        private SplitContainer _split;

        // Realtime Sync Toggle
        private Button _btnToggleRealtime;
        private Label _lblRealtimeStatus;

        // Manual Command Controls
        private TextBox _txtUrl;
        private Button _btnSyncUrl;
        private Button _btnScrollDown;
        private Button _btnScrollUp;
        private Button _btnRefreshAll;
        private Button _btnClickCenter;
        private TextBox _txtSendText;
        private Button _btnSendText;
        private TextBox _txtCustomJs;
        private Button _btnRunJs;

        // Log Console
        private RichTextBox _rtbLog;
        private Button _btnClearLog;

        private readonly List<UserProfile> _allProfiles;
        private readonly SynchronizerService _syncService = SynchronizerService.Instance;
        private readonly BrowserLauncherService _launcherService = new BrowserLauncherService();

        public SynchronizerForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            _syncService.OnSyncLog += SyncService_OnSyncLog;
            _syncService.OnSyncStateChanged += SyncService_OnSyncStateChanged;
            LoadProfiles();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _syncService.OnSyncLog -= SyncService_OnSyncLog;
            _syncService.OnSyncStateChanged -= SyncService_OnSyncStateChanged;
            _syncService.StopRealtimeSync();
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            UpdateSplitRatio();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateSplitRatio();
        }

        private void UpdateSplitRatio()
        {
            try
            {
                if (_split != null && _split.Width > 500 && this.WindowState != FormWindowState.Minimized)
                {
                    // Tỉ lệ layout 7-5: Cột trái 5 phần (~41.7%), Cột phải 7 phần (~58.3%)
                    int target = (_split.Width * 5) / 12;
                    if (target >= _split.Panel1MinSize && (_split.Width - target) >= _split.Panel2MinSize)
                    {
                        _split.SplitterDistance = target;
                    }
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "⚡ BỘ ĐỒNG BỘ THAO TÁC ĐA TRÌNH DUYỆT THỜI GIAN THỰC (MULTI-CONTROL SYNCHRONIZER)";
            this.Size = new Size(1180, 780);
            this.MinimumSize = new Size(900, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // ================= 1. HEADER BANNER =================
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(79, 70, 229), // Indigo Dark
                Padding = new Padding(16, 8, 16, 8)
            };

            Label lblTitle = new Label
            {
                Text = "⚡ MULTI-CONTROL SYNCHRONIZER (ĐỒNG BỘ ĐA CỬA SỔ THỜI GIAN THỰC)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Bật Đồng Bộ Thời Gian Thực: Mọi click chuột, cuộn trang, gõ phím trên máy Master sẽ TỰ ĐỘNG PHẢN CHIẾU 100% sang toàn bộ máy Slaves!",
                ForeColor = Color.FromArgb(224, 231, 255),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(15, 31)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // ================= 2. MAIN SPLIT (LAYOUT 7-5) =================
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 485, // Layout 7-5: Bên trái 5 phần (~41.7%), bên phải 7 phần (~58.3%)
                Panel1MinSize = 250,
                Panel2MinSize = 400,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(229, 231, 235)
            };
            this.Controls.Add(_split);
            _split.BringToFront();

            // ================= LEFT: SETUP MASTER & SLAVES =================
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };

            Label lblMasterTitle = new Label
            {
                Text = "1. MÁY MASTER (GỐC):",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(79, 70, 229)
            };

            _cboMaster = new ComboBox
            {
                Dock = DockStyle.Top,
                Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            _cboMaster.SelectedIndexChanged += delegate { UpdateSlaveList(); };

            Label lblSlavesTitle = new Label
            {
                Text = "2. MÁY SLAVES (ĐỒNG BỘ):",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(0, 8, 0, 0)
            };

            FlowLayoutPanel pnlQuickSelect = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(0, 2, 0, 2),
                WrapContents = false
            };

            _btnSelectAllSlaves = new Button { Text = "Tất cả", Width = 70, Height = 25, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F) };
            _btnSelectAllSlaves.Click += delegate
            {
                for (int i = 0; i < _chkSlaves.Items.Count; i++) _chkSlaves.SetItemChecked(i, true);
            };

            _btnSelectRunningSlaves = new Button { Text = "Đang chạy", Width = 95, Height = 25, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F) };
            _btnSelectRunningSlaves.Click += delegate
            {
                var running = BrowserSessionManager.Instance.GetActiveSessionIds();
                for (int i = 0; i < _chkSlaves.Items.Count; i++)
                {
                    var item = _chkSlaves.Items[i] as ProfileSyncWrapper;
                    bool isRun = item != null && running.Contains(item.Profile.ProfileId);
                    _chkSlaves.SetItemChecked(i, isRun);
                }
            };

            _btnDeselectSlaves = new Button { Text = "Bỏ chọn", Width = 70, Height = 25, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F) };
            _btnDeselectSlaves.Click += delegate
            {
                for (int i = 0; i < _chkSlaves.Items.Count; i++) _chkSlaves.SetItemChecked(i, false);
            };

            pnlQuickSelect.Controls.Add(_btnSelectAllSlaves);
            pnlQuickSelect.Controls.Add(_btnSelectRunningSlaves);
            pnlQuickSelect.Controls.Add(_btnDeselectSlaves);

            _chkSlaves = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.5F)
            };

            Panel pnlLeftBottom = new Panel { Dock = DockStyle.Bottom, Height = 75, Padding = new Padding(0, 6, 0, 0) };

            _btnLaunchSlaves = new Button
            {
                Text = "🚀 Mở Tất Cả Slaves Đang Chọn",
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnLaunchSlaves.FlatAppearance.BorderSize = 0;
            _btnLaunchSlaves.Click += delegate { LaunchSelectedSlaves(); };

            _btnTileWindows = new Button
            {
                Text = "🪟 Xếp Lưới Cửa Sổ (Tile Grid)",
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnTileWindows.Click += delegate { TileGridNow(); };

            pnlLeftBottom.Controls.Add(_btnTileWindows);
            pnlLeftBottom.Controls.Add(_btnLaunchSlaves);

            pnlLeft.Controls.Add(_chkSlaves);
            pnlLeft.Controls.Add(pnlLeftBottom);
            pnlLeft.Controls.Add(pnlQuickSelect);
            pnlLeft.Controls.Add(lblSlavesTitle);
            pnlLeft.Controls.Add(_cboMaster);
            pnlLeft.Controls.Add(lblMasterTitle);
            _split.Panel1.Controls.Add(pnlLeft);

            // ================= RIGHT: REALTIME SYNC & COMMANDS =================
            Panel pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };

            // 1. HERO REALTIME SYNC PANEL (RESPONSIVE)
            GroupBox grpRealtime = new GroupBox
            {
                Text = "⚡ TÍNH NĂNG ĐỒNG BỘ THỜI GIAN THỰC (REALTIME INPUT MIRRORING)",
                Dock = DockStyle.Top,
                Height = 115,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(79, 70, 229),
                Padding = new Padding(12)
            };

            FlowLayoutPanel pnlRealtimeHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                WrapContents = true,
                AutoSize = true
            };

            _btnToggleRealtime = new Button
            {
                Text = "▶ BẬT ĐỒNG BỘ THỜI GIAN THỰC",
                Size = new Size(260, 38),
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnToggleRealtime.FlatAppearance.BorderSize = 0;
            _btnToggleRealtime.Click += delegate { ToggleRealtimeSync(); };

            _lblRealtimeStatus = new Label
            {
                Text = "● Đang tắt đồng bộ thời gian thực",
                AutoSize = true,
                Margin = new Padding(12, 10, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(107, 114, 128)
            };

            pnlRealtimeHeader.Controls.Add(_btnToggleRealtime);
            pnlRealtimeHeader.Controls.Add(_lblRealtimeStatus);

            Label lblRealtimeHelp = new Label
            {
                Text = "💡 Hướng dẫn: Khi BẬT, bạn chỉ cần thao tác bình thường trên cửa sổ Master (Click, Cuộn chuột, Gõ chữ, Đổi link). Toàn bộ các máy Slaves sẽ tự động làm y hệt trong tích tắc!",
                Dock = DockStyle.Bottom,
                Height = 36,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(75, 85, 99)
            };

            grpRealtime.Controls.Add(lblRealtimeHelp);
            grpRealtime.Controls.Add(pnlRealtimeHeader);
            pnlRight.Controls.Add(grpRealtime);

            // 2. MANUAL COMMANDS PANEL (RESPONSIVE FLUID ROWS)
            GroupBox grpManual = new GroupBox
            {
                Text = "🎯 THAO TÁC ĐỒNG BỘ THỦ CÔNG (INSTANT BROADCAST)",
                Dock = DockStyle.Top,
                Height = 270,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(12)
            };

            // ROW 1: URL Navigation
            Label lblUrl = new Label { Text = "🌐 Mở trang Web (URL):", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            Panel pnlUrlRow = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(0, 2, 0, 4) };
            _btnSyncUrl = new Button
            {
                Text = "🚀 Mở Tất Cả",
                Dock = DockStyle.Right,
                Width = 120,
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSyncUrl.FlatAppearance.BorderSize = 0;
            _btnSyncUrl.Click += delegate
            {
                ApplySlaves();
                _syncService.SyncNavigate(_txtUrl.Text);
            };

            _txtUrl = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), Text = "https://www.google.com" };

            // Panel wrap to ensure margin between textbox and button
            Panel pnlUrlFill = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0) };
            pnlUrlFill.Controls.Add(_txtUrl);
            pnlUrlRow.Controls.Add(pnlUrlFill);
            pnlUrlRow.Controls.Add(_btnSyncUrl);

            // ROW 2: Quick buttons
            Label lblQuick = new Label { Text = "📜 Thao tác nhanh (Cuộn trang, Làm mới, Click):", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Padding = new Padding(0, 4, 0, 0) };
            FlowLayoutPanel pnlQuickBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                WrapContents = true,
                AutoSize = true
            };

            _btnScrollDown = new Button { Text = "↓ Cuộn Xuống (+400px)", Width = 145, Height = 28, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            _btnScrollDown.Click += delegate { ApplySlaves(); _syncService.SyncScroll(400); };

            _btnScrollUp = new Button { Text = "↑ Cuộn Lên (-400px)", Width = 140, Height = 28, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            _btnScrollUp.Click += delegate { ApplySlaves(); _syncService.SyncScroll(-400); };

            _btnRefreshAll = new Button { Text = "🔄 Làm Mới (F5)", Width = 120, Height = 28, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            _btnRefreshAll.Click += delegate { ApplySlaves(); _syncService.SyncRefreshAll(true); };

            _btnClickCenter = new Button { Text = "🖱 Click Giữa (50%)", Width = 135, Height = 28, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            _btnClickCenter.Click += delegate { ApplySlaves(); _syncService.SyncClickPercent(0.5, 0.5); };

            pnlQuickBtns.Controls.Add(_btnScrollDown);
            pnlQuickBtns.Controls.Add(_btnScrollUp);
            pnlQuickBtns.Controls.Add(_btnRefreshAll);
            pnlQuickBtns.Controls.Add(_btnClickCenter);

            // ROW 3: Text send
            Label lblText = new Label { Text = "⌨️ Gửi chuỗi văn bản vào ô đang trỏ:", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Padding = new Padding(0, 4, 0, 0) };
            Panel pnlTextRow = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(0, 2, 0, 4) };
            _btnSendText = new Button
            {
                Text = "✍️ Gửi Văn Bản",
                Dock = DockStyle.Right,
                Width = 120,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSendText.FlatAppearance.BorderSize = 0;
            _btnSendText.Click += delegate
            {
                ApplySlaves();
                _syncService.SyncSendText(_txtSendText.Text);
            };

            _txtSendText = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), Text = "Xin chào" };
            Panel pnlTextFill = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0) };
            pnlTextFill.Controls.Add(_txtSendText);
            pnlTextRow.Controls.Add(pnlTextFill);
            pnlTextRow.Controls.Add(_btnSendText);

            // ROW 4: JS Execute
            Label lblJs = new Label { Text = "💻 Thực thi JavaScript:", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Padding = new Padding(0, 4, 0, 0) };
            Panel pnlJsRow = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(0, 2, 0, 4) };
            _btnRunJs = new Button
            {
                Text = "⚡ Chạy JS",
                Dock = DockStyle.Right,
                Width = 120,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnRunJs.Click += delegate
            {
                ApplySlaves();
                _syncService.SyncExecuteScript(_txtCustomJs.Text);
            };

            _txtCustomJs = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), Text = "window.scrollTo(0, document.body.scrollHeight);" };
            Panel pnlJsFill = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0) };
            pnlJsFill.Controls.Add(_txtCustomJs);
            pnlJsRow.Controls.Add(pnlJsFill);
            pnlJsRow.Controls.Add(_btnRunJs);

            grpManual.Controls.Add(pnlJsRow);
            grpManual.Controls.Add(lblJs);
            grpManual.Controls.Add(pnlTextRow);
            grpManual.Controls.Add(lblText);
            grpManual.Controls.Add(pnlQuickBtns);
            grpManual.Controls.Add(lblQuick);
            grpManual.Controls.Add(pnlUrlRow);
            grpManual.Controls.Add(lblUrl);
            pnlRight.Controls.Add(grpManual);

            // 3. LOG CONSOLE PANEL
            Panel pnlLog = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
            Panel pnlLogBar = new Panel { Dock = DockStyle.Top, Height = 26 };
            Label lblLogTitle = new Label { Text = "📝 NHẬT KÝ ĐỒNG BỘ THỜI GIAN THỰC (REALTIME LOG):", Dock = DockStyle.Left, AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(75, 85, 99) };
            _btnClearLog = new Button { Text = "Xóa log", Dock = DockStyle.Right, Width = 65, Height = 24, FlatStyle = FlatStyle.Flat };
            _btnClearLog.Click += delegate { _rtbLog.Clear(); };

            pnlLogBar.Controls.Add(lblLogTitle);
            pnlLogBar.Controls.Add(_btnClearLog);

            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 8.5F),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            pnlLog.Controls.Add(_rtbLog);
            pnlLog.Controls.Add(pnlLogBar);
            pnlRight.Controls.Add(pnlLog);

            pnlLog.BringToFront();
            grpManual.BringToFront();
            grpRealtime.BringToFront();

            _split.Panel2.Controls.Add(pnlRight);
        }

        private void LoadProfiles()
        {
            _cboMaster.Items.Clear();
            var runningIds = BrowserSessionManager.Instance.GetActiveSessionIds();

            foreach (var p in _allProfiles)
            {
                bool isRunning = runningIds.Contains(p.ProfileId);
                _cboMaster.Items.Add(new ProfileSyncWrapper { Profile = p, DisplayText = string.Format("{0} {1}", p.ProfileName, isRunning ? "[🟢 Đang chạy]" : "[⚪ Đã tắt]") });
            }

            if (_cboMaster.Items.Count > 0)
            {
                _cboMaster.SelectedIndex = 0;
            }
        }

        private void UpdateSlaveList()
        {
            _chkSlaves.Items.Clear();
            var selectedMaster = _cboMaster.SelectedItem as ProfileSyncWrapper;
            string masterId = selectedMaster != null ? selectedMaster.Profile.ProfileId : "";
            var runningIds = BrowserSessionManager.Instance.GetActiveSessionIds();

            foreach (var p in _allProfiles)
            {
                if (p.ProfileId == masterId) continue; // Bỏ qua Master
                bool isRunning = runningIds.Contains(p.ProfileId);
                _chkSlaves.Items.Add(new ProfileSyncWrapper { Profile = p, DisplayText = string.Format("{0} {1}", p.ProfileName, isRunning ? "[🟢 Đang chạy]" : "[⚪ Đã tắt]") }, isRunning);
            }
        }

        private List<string> GetSelectedSlaveIds()
        {
            var slaves = new List<string>();
            foreach (var item in _chkSlaves.CheckedItems)
            {
                var w = item as ProfileSyncWrapper;
                if (w != null) slaves.Add(w.Profile.ProfileId);
            }
            return slaves;
        }

        private void ApplySlaves()
        {
            var selectedMaster = _cboMaster.SelectedItem as ProfileSyncWrapper;
            string masterId = selectedMaster != null ? selectedMaster.Profile.ProfileId : "";
            var slaves = GetSelectedSlaveIds();
            _syncService.MasterProfileId = masterId;
            _syncService.SlaveProfileIds = slaves;
        }

        private void ToggleRealtimeSync()
        {
            if (_syncService.IsRealtimeSyncing)
            {
                _syncService.StopRealtimeSync();
            }
            else
            {
                var selectedMaster = _cboMaster.SelectedItem as ProfileSyncWrapper;
                if (selectedMaster == null)
                {
                    MessageBox.Show("Vui lòng chọn 1 máy Master!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string masterId = selectedMaster.Profile.ProfileId;
                if (!BrowserSessionManager.Instance.IsRunning(masterId))
                {
                    DialogResult dr = MessageBox.Show(string.Format("Máy Master '{0}' chưa được mở trình duyệt.\nBạn có muốn mở ngay bây giờ?", selectedMaster.Profile.ProfileName), "Mở Master", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dr == DialogResult.Yes)
                    {
                        _launcherService.LaunchBrowser(selectedMaster.Profile);
                        LoadProfiles();
                    }
                    else
                    {
                        return;
                    }
                }

                var slaves = GetSelectedSlaveIds();
                if (slaves.Count == 0)
                {
                    MessageBox.Show("Vui lòng tích chọn ít nhất 1 máy Slave để đồng bộ!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _syncService.StartRealtimeSync(masterId, slaves);
            }
        }

        private void LaunchSelectedSlaves()
        {
            var selectedMaster = _cboMaster.SelectedItem as ProfileSyncWrapper;
            var slaves = GetSelectedSlaveIds();
            if (slaves.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 máy Slave!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Task.Factory.StartNew(delegate
            {
                int idx = 0;
                // Nếu Master chưa mở thì mở Master trước
                if (selectedMaster != null && !BrowserSessionManager.Instance.IsRunning(selectedMaster.Profile.ProfileId))
                {
                    _launcherService.LaunchBrowser(selectedMaster.Profile, null, idx++);
                }

                foreach (var sid in slaves)
                {
                    if (!BrowserSessionManager.Instance.IsRunning(sid))
                    {
                        var prof = _allProfiles.FirstOrDefault(p => p.ProfileId == sid);
                        if (prof != null)
                        {
                            _launcherService.LaunchBrowser(prof, null, idx++);
                        }
                    }
                    else
                    {
                        idx++;
                    }
                }

                this.BeginInvoke(new Action(delegate
                {
                    UpdateSlaveList();
                    TileGridNow();
                }));
            });
        }

        private void TileGridNow()
        {
            var selectedMaster = _cboMaster.SelectedItem as ProfileSyncWrapper;
            string masterId = selectedMaster != null ? selectedMaster.Profile.ProfileId : "";
            var slaves = GetSelectedSlaveIds();
            _syncService.TileWindows(masterId, slaves);
        }

        private void SyncService_OnSyncLog(string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(delegate { SyncService_OnSyncLog(msg); }));
                return;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            _rtbLog.AppendText(string.Format("[{0}] {1}\r\n", time, msg));
            _rtbLog.ScrollToCaret();
        }

        private void SyncService_OnSyncStateChanged(bool isSyncing)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(delegate { SyncService_OnSyncStateChanged(isSyncing); }));
                return;
            }

            if (isSyncing)
            {
                _btnToggleRealtime.Text = "⏹ DỪNG ĐỒNG BỘ THỜI GIAN THỰC";
                _btnToggleRealtime.BackColor = Color.FromArgb(239, 68, 68);
                _lblRealtimeStatus.Text = string.Format("● ĐANG ĐỒNG BỘ THỜI GIAN THỰC ({0} Slaves)", _syncService.SlaveProfileIds.Count);
                _lblRealtimeStatus.ForeColor = Color.FromArgb(16, 185, 129);
            }
            else
            {
                _btnToggleRealtime.Text = "▶ BẬT ĐỒNG BỘ THỜI GIAN THỰC";
                _btnToggleRealtime.BackColor = Color.FromArgb(16, 185, 129);
                _lblRealtimeStatus.Text = "● Đang tắt đồng bộ thời gian thực";
                _lblRealtimeStatus.ForeColor = Color.FromArgb(107, 114, 128);
            }
        }

        private class ProfileSyncWrapper
        {
            public UserProfile Profile { get; set; }
            public string DisplayText { get; set; }
            public override string ToString() { return DisplayText; }
        }
    }
}
