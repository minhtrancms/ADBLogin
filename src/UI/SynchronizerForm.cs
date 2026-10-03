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

        private void InitializeComponent()
        {
            this.Text = "⚡ BỘ ĐỒNG BỘ THAO TÁC ĐA TRÌNH DUYỆT THỜI GIAN THỰC (MULTI-CONTROL SYNCHRONIZER)";
            this.Size = new Size(1100, 750);
            this.MinimumSize = new Size(950, 600);
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

            // ================= 2. MAIN SPLIT =================
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 420,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(229, 231, 235)
            };
            this.Controls.Add(split);
            split.BringToFront();

            // ================= LEFT: SETUP MASTER & SLAVES =================
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };

            Label lblMasterTitle = new Label
            {
                Text = "1. CHỌN MÁY ĐIỀU KHIỂN (MASTER):",
                Dock = DockStyle.Top,
                Height = 22,
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
                Text = "2. DANH SÁCH MÁY NHẬN ĐỒNG BỘ (SLAVES):",
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

            _btnSelectAllSlaves = new Button { Text = "Tất cả", Width = 65, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnSelectAllSlaves.Click += delegate
            {
                for (int i = 0; i < _chkSlaves.Items.Count; i++) _chkSlaves.SetItemChecked(i, true);
            };

            _btnSelectRunningSlaves = new Button { Text = "Đang chạy", Width = 80, Height = 25, FlatStyle = FlatStyle.Flat };
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

            _btnDeselectSlaves = new Button { Text = "Bỏ chọn", Width = 65, Height = 25, FlatStyle = FlatStyle.Flat };
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
            split.Panel1.Controls.Add(pnlLeft);

            // ================= RIGHT: REALTIME SYNC & COMMANDS =================
            Panel pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12), AutoScroll = true };

            // 1. HERO REALTIME SYNC PANEL
            GroupBox grpRealtime = new GroupBox
            {
                Text = "⚡ TÍNH NĂNG ĐỒNG BỘ THỜI GIAN THỰC (REALTIME INPUT MIRRORING)",
                Dock = DockStyle.Top,
                Height = 120,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(79, 70, 229)
            };

            _btnToggleRealtime = new Button
            {
                Text = "▶ BẬT ĐỒNG BỘ THỜI GIAN THỰC",
                Location = new Point(16, 28),
                Size = new Size(270, 42),
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnToggleRealtime.FlatAppearance.BorderSize = 0;
            _btnToggleRealtime.Click += delegate { ToggleRealtimeSync(); };

            _lblRealtimeStatus = new Label
            {
                Text = "● Đang tắt đồng bộ thời gian thực",
                Location = new Point(300, 40),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(107, 114, 128)
            };

            Label lblRealtimeHelp = new Label
            {
                Text = "💡 Hướng dẫn: Khi BẬT, bạn chỉ cần thao tác bình thường trên cửa sổ Master (Click, Cuộn chuột, Gõ chữ, Đổi link). Toàn bộ các máy Slaves sẽ tự động làm y hệt trong tích tắc!",
                Location = new Point(16, 76),
                Size = new Size(610, 36),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(75, 85, 99)
            };

            grpRealtime.Controls.Add(_btnToggleRealtime);
            grpRealtime.Controls.Add(_lblRealtimeStatus);
            grpRealtime.Controls.Add(lblRealtimeHelp);
            pnlRight.Controls.Add(grpRealtime);

            // 2. MANUAL COMMANDS PANEL
            GroupBox grpManual = new GroupBox
            {
                Text = "🎯 THAO TÁC ĐỒNG BỘ THỦ CÔNG (INSTANT BROADCAST)",
                Dock = DockStyle.Top,
                Height = 250,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(12)
            };

            // A. URL
            Label lblUrl = new Label { Text = "🌐 Mở trang Web (URL):", Location = new Point(14, 26), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _txtUrl = new TextBox { Location = new Point(16, 46), Width = 450, Height = 26, Font = new Font("Segoe UI", 9F), Text = "https://www.google.com" };
            _btnSyncUrl = new Button { Text = "🚀 Mở Tất Cả", Location = new Point(472, 44), Width = 150, Height = 28, BackColor = Color.FromArgb(79, 70, 229), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            _btnSyncUrl.FlatAppearance.BorderSize = 0;
            _btnSyncUrl.Click += delegate
            {
                ApplySlaves();
                _syncService.SyncNavigate(_txtUrl.Text);
            };

            // B. Quick buttons (Scroll, Refresh, Click center)
            Label lblQuick = new Label { Text = "📜 Thao tác nhanh:", Location = new Point(14, 82), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            FlowLayoutPanel pnlQuickBtns = new FlowLayoutPanel { Location = new Point(16, 102), Width = 610, Height = 34, WrapContents = false };

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

            // C. Text send
            Label lblText = new Label { Text = "⌨️ Gửi chuỗi văn bản vào ô đang trỏ:", Location = new Point(14, 142), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _txtSendText = new TextBox { Location = new Point(16, 162), Width = 450, Height = 26, Font = new Font("Segoe UI", 9F), Text = "Xin chào" };
            _btnSendText = new Button { Text = "✍️ Gửi Văn Bản", Location = new Point(472, 160), Width = 150, Height = 28, BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            _btnSendText.FlatAppearance.BorderSize = 0;
            _btnSendText.Click += delegate
            {
                ApplySlaves();
                _syncService.SyncSendText(_txtSendText.Text);
            };

            // D. JS Execute
            Label lblJs = new Label { Text = "💻 Thực thi JavaScript:", Location = new Point(14, 196), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _txtCustomJs = new TextBox { Location = new Point(16, 216), Width = 450, Height = 26, Font = new Font("Segoe UI", 9F), Text = "window.scrollTo(0, document.body.scrollHeight);" };
            _btnRunJs = new Button { Text = "⚡ Chạy JS", Location = new Point(472, 214), Width = 150, Height = 28, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnRunJs.Click += delegate
            {
                ApplySlaves();
                _syncService.SyncExecuteScript(_txtCustomJs.Text);
            };

            grpManual.Controls.Add(lblUrl);
            grpManual.Controls.Add(_txtUrl);
            grpManual.Controls.Add(_btnSyncUrl);
            grpManual.Controls.Add(lblQuick);
            grpManual.Controls.Add(pnlQuickBtns);
            grpManual.Controls.Add(lblText);
            grpManual.Controls.Add(_txtSendText);
            grpManual.Controls.Add(_btnSendText);
            grpManual.Controls.Add(lblJs);
            grpManual.Controls.Add(_txtCustomJs);
            grpManual.Controls.Add(_btnRunJs);
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

            split.Panel2.Controls.Add(pnlRight);
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
