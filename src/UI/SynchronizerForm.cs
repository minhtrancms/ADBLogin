using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        private TextBox _txtUrl;
        private Button _btnSyncUrl;
        private Button _btnRefreshAll;
        private Button _btnScrollDown;
        private Button _btnScrollUp;
        private TextBox _txtSendText;
        private Button _btnSendText;
        private Button _btnClickCenter;
        private TextBox _txtCustomJs;
        private Button _btnRunJs;
        private Button _btnSelectAllSlaves;
        private Label _lblStatus;

        private readonly List<UserProfile> _allProfiles;
        private readonly SynchronizerService _syncService = SynchronizerService.Instance;

        public SynchronizerForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            LoadProfiles();
        }

        private void InitializeComponent()
        {
            this.Text = "⚡ BỘ ĐỒNG BỘ THAO TÁC ĐA TRÌNH DUYỆT (SYNCHRONIZER)";
            this.Size = new Size(820, 640);
            this.MinimumSize = new Size(720, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // HEADER
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(99, 102, 241), // Indigo
                Padding = new Padding(16, 8, 16, 8)
            };

            Label lblTitle = new Label
            {
                Text = "⚡ MULTI-CONTROL SYNCHRONIZER (ĐỒNG BỘ ĐA CỬA SỔ)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Điều khiển 1 máy Master -> Tự động đồng bộ URL, cuộn trang, gõ văn bản và click chuột sang toàn bộ máy Slaves",
                ForeColor = Color.FromArgb(224, 231, 255),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(15, 30)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // MAIN PANEL
            Panel pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            // 1. CHỌN MASTER & SLAVES
            GroupBox grpSetup = new GroupBox { Text = "1. Thiết lập Máy Điều Khiển (Master) & Máy Đồng Bộ (Slaves)", Location = new Point(14, 10), Width = 770, Height = 170, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            Label lblMaster = new Label { Text = "Chọn máy Master (Gốc):", Location = new Point(16, 28), AutoSize = true };
            _cboMaster = new ComboBox { Location = new Point(18, 50), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cboMaster.SelectedIndexChanged += (s, e) => UpdateSlaveList();

            Label lblSlaves = new Label { Text = "Danh sách máy Slaves nhận đồng bộ:", Location = new Point(310, 28), AutoSize = true };
            _chkSlaves = new CheckedListBox { Location = new Point(312, 50), Width = 430, Height = 80, CheckOnClick = true, Font = new Font("Segoe UI", 8.5F) };

            _btnSelectAllSlaves = new Button { Text = "Chọn tất cả Slaves", Location = new Point(312, 134), Width = 130, Height = 26, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(241, 245, 249), Font = new Font("Segoe UI", 8F) };
            _btnSelectAllSlaves.Click += (s, e) =>
            {
                for (int i = 0; i < _chkSlaves.Items.Count; i++) _chkSlaves.SetItemChecked(i, true);
            };

            grpSetup.Controls.Add(_btnSelectAllSlaves);
            grpSetup.Controls.Add(_chkSlaves);
            grpSetup.Controls.Add(lblSlaves);
            grpSetup.Controls.Add(_cboMaster);
            grpSetup.Controls.Add(lblMaster);
            pnlMain.Controls.Add(grpSetup);

            // 2. CÁC LỆNH ĐỒNG BỘ TỨC THÌ
            GroupBox grpActions = new GroupBox { Text = "2. Thao Tác Đồng Bộ Tức Thì (Instant Broadcast Commands)", Location = new Point(14, 190), Width = 770, Height = 280, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            // A. Điều hướng URL
            Label lblUrl = new Label { Text = "🌐 Đồng bộ mở trang Web (URL):", Location = new Point(16, 28), AutoSize = true };
            _txtUrl = new TextBox { Location = new Point(18, 50), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "https://www.google.com" };
            _btnSyncUrl = new Button { Text = "🚀 Mở trên tất cả", Location = new Point(586, 48), Width = 156, Height = 28, BackColor = Color.FromArgb(99, 102, 241), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnSyncUrl.Click += BtnSyncUrl_Click;

            // B. Cuộn trang & Làm mới
            Label lblNav = new Label { Text = "📜 Cuộn trang & Tải lại:", Location = new Point(16, 88), AutoSize = true };
            _btnScrollDown = new Button { Text = "↓ Cuộn Xuống (+400px)", Location = new Point(18, 110), Width = 160, Height = 30, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnScrollDown.Click += (s, e) => { ApplySlaves(); _syncService.SyncScroll(400); SetStatus("Đã cuộn xuống +400px"); };

            _btnScrollUp = new Button { Text = "↑ Cuộn Lên (-400px)", Location = new Point(186, 110), Width = 160, Height = 30, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnScrollUp.Click += (s, e) => { ApplySlaves(); _syncService.SyncScroll(-400); SetStatus("Đã cuộn lên -400px"); };

            _btnRefreshAll = new Button { Text = "🔄 Làm mới tất cả (F5)", Location = new Point(354, 110), Width = 160, Height = 30, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnRefreshAll.Click += (s, e) => { ApplySlaves(); _syncService.SyncRefreshAll(); SetStatus("Đã làm mới tất cả các máy"); };

            _btnClickCenter = new Button { Text = "🖱 Click Giữa Màn Hình (50%)", Location = new Point(522, 110), Width = 220, Height = 30, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnClickCenter.Click += (s, e) => { ApplySlaves(); _syncService.SyncClickPercent(0.5, 0.5); SetStatus("Đã click tại tọa độ 50% giữa màn hình"); };

            // C. Gửi văn bản
            Label lblText = new Label { Text = "⌨️ Đồng bộ gửi văn bản / phím (vào ô đang trỏ):", Location = new Point(16, 152), AutoSize = true };
            _txtSendText = new TextBox { Location = new Point(18, 174), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "Xin chào" };
            _btnSendText = new Button { Text = "✍️ Gửi văn bản", Location = new Point(586, 172), Width = 156, Height = 28, BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnSendText.Click += (s, e) => { ApplySlaves(); _syncService.SyncSendText(_txtSendText.Text); SetStatus("Đã gửi văn bản sang tất cả các máy"); };

            // D. Chạy JavaScript
            Label lblJs = new Label { Text = "💻 Thực thi mã JavaScript trên tất cả các máy:", Location = new Point(16, 212), AutoSize = true };
            _txtCustomJs = new TextBox { Location = new Point(18, 234), Width = 560, Height = 26, Font = new Font("Segoe UI", 9.5F), Text = "window.scrollTo(0, document.body.scrollHeight);" };
            _btnRunJs = new Button { Text = "⚡ Chạy JavaScript", Location = new Point(586, 232), Width = 156, Height = 28, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnRunJs.Click += (s, e) => { ApplySlaves(); _syncService.SyncExecuteScript(_txtCustomJs.Text); SetStatus("Đã thực thi JavaScript"); };

            grpActions.Controls.Add(_btnRunJs);
            grpActions.Controls.Add(_txtCustomJs);
            grpActions.Controls.Add(lblJs);
            grpActions.Controls.Add(_btnSendText);
            grpActions.Controls.Add(_txtSendText);
            grpActions.Controls.Add(lblText);
            grpActions.Controls.Add(_btnClickCenter);
            grpActions.Controls.Add(_btnRefreshAll);
            grpActions.Controls.Add(_btnScrollUp);
            grpActions.Controls.Add(_btnScrollDown);
            grpActions.Controls.Add(lblNav);
            grpActions.Controls.Add(_btnSyncUrl);
            grpActions.Controls.Add(_txtUrl);
            grpActions.Controls.Add(lblUrl);
            pnlMain.Controls.Add(grpActions);

            // FOOTER STATUS
            _lblStatus = new Label { Text = "Trạng thái: Sẵn sàng đồng bộ", Location = new Point(18, 485), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 9F, FontStyle.Italic) };
            pnlMain.Controls.Add(_lblStatus);

            this.Controls.Add(pnlMain);
        }

        private void LoadProfiles()
        {
            _cboMaster.Items.Clear();
            var runningIds = BrowserSessionManager.Instance.GetActiveSessionIds();

            foreach (var p in _allProfiles)
            {
                bool isRunning = runningIds.Contains(p.ProfileId);
                _cboMaster.Items.Add(new ProfileSyncWrapper { Profile = p, DisplayText = string.Format("{0}{1}", p.ProfileName, isRunning ? " [▶ Đang chạy]" : "") });
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
                _chkSlaves.Items.Add(new ProfileSyncWrapper { Profile = p, DisplayText = string.Format("{0}{1}", p.ProfileName, isRunning ? " [▶ Đang chạy]" : "") }, isRunning);
            }
        }

        private void ApplySlaves()
        {
            var selectedMaster = _cboMaster.SelectedItem as ProfileSyncWrapper;
            string masterId = selectedMaster != null ? selectedMaster.Profile.ProfileId : "";

            var slaves = new List<string>();
            foreach (var item in _chkSlaves.CheckedItems)
            {
                var w = item as ProfileSyncWrapper;
                if (w != null) slaves.Add(w.Profile.ProfileId);
            }

            _syncService.StartSync(masterId, slaves);
        }

        private void BtnSyncUrl_Click(object sender, EventArgs e)
        {
            ApplySlaves();
            string url = _txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            if (!url.StartsWith("http")) url = "https://" + url;

            _syncService.SyncNavigate(url);
            SetStatus(string.Format("Đã đồng bộ mở {0} trên {1} máy Slaves", url, _syncService.SlaveProfileIds.Count));
        }

        private void SetStatus(string text)
        {
            _lblStatus.Text = string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), text);
            _lblStatus.ForeColor = Color.FromArgb(16, 185, 129);
        }

        private class ProfileSyncWrapper
        {
            public UserProfile Profile { get; set; }
            public string DisplayText { get; set; }
            public override string ToString() { return DisplayText; }
        }
    }
}
