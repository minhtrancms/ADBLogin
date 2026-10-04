using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;

namespace ADBLogin.UI
{
    public class HmaMultiProxyForm : Form
    {
        private readonly HmaMultiProxyService _openVpnService = HmaMultiProxyService.Instance;
        private readonly CloudflareWarpService _warpService = CloudflareWarpService.Instance;
        private readonly List<UserProfile> _allProfiles;

        // Provider Selector
        private ComboBox _cboProvider;
        private Panel _pnlProviderSettings;

        // Port Configuration
        private NumericUpDown _numStartPort;
        private NumericUpDown _numPortCount;
        private Button _btnInitPorts;

        // Action Buttons
        private Button _btnStartAll;
        private Button _btnStopAll;
        private Button _btnDeduplicate;
        private Button _btnCheckIp;
        private Button _btnAssign;
        private Button _btnCopy;

        // Dynamic Settings Controls for Providers
        // WARP
        private CheckBox _chkWarpAntiDuplicate;
        private Button _btnWarpResetAll;
        // NordVPN
        private TextBox _txtNordUser;
        private TextBox _txtNordPass;
        private CheckBox _chkShowNordPass;
        private Button _btnSaveNord;
        private ComboBox _cboNordCountry;
        private Button _btnDownloadNord;
        private Label _lblNordStatus;
        // VPN Gate
        private Button _btnDownloadVpnGate;
        private Label _lblVpnGateStatus;
        // Custom OpenVPN
        private TextBox _txtCustomDir;
        private Button _btnBrowseCustom;
        private Label _lblCustomStatus;

        // Single Central Grid
        private DataGridView _gridPorts;
        private Label _lblSummary;
        private TextBox _txtLog;

        // Server lists cache
        private List<KeyValuePair<string, string>> _nordServers = new List<KeyValuePair<string, string>>();
        private List<KeyValuePair<string, string>> _vpnGateServers = new List<KeyValuePair<string, string>>();
        private List<KeyValuePair<string, string>> _customServers = new List<KeyValuePair<string, string>>();

        public HmaMultiProxyForm(List<UserProfile> currentProfiles = null)
        {
            _allProfiles = currentProfiles ?? AccountManager.Instance.GetAllProfiles();
            InitializeComponent();
            LoadInitialData();
            WireEvents();
        }

        private void InitializeComponent()
        {
            this.Text = "🌐 MULTI-PROXY STUDIO (CLOUDFLARE WARP / NORDVPN / VPN GATE)";
            this.Size = new Size(1250, 780);
            this.MinimumSize = new Size(1020, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // ================= HEADER BANNER =================
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(16, 10, 16, 10)
            };

            Label lblTitle = new Label
            {
                Text = "🌐 MULTI-PROXY STUDIO (TRẠM KHỞI TẠO PROXY ĐA NGUỒN)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Chỉ cần chọn 1 nhà cung cấp -> Bấm 'Khởi Động Tất Cả' -> Tự động sinh dải Proxy SOCKS5/HTTP sạch, không lo trùng cổng hay chiếm mạng!",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(15, 34)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // ================= BOTTOM LOG PANEL =================
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 120,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(12, 4, 12, 6)
            };

            Label lblLogTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                Text = "📝 Nhật Ký Hoạt Động & Sự Kiện Proxy Thời Gian Thực:"
            };

            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 8.5F)
            };

            pnlBottom.Controls.Add(_txtLog);
            pnlBottom.Controls.Add(lblLogTitle);
            this.Controls.Add(pnlBottom);

            // ================= SUMMARY BAR =================
            _lblSummary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                BackColor = Color.FromArgb(241, 245, 249),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Text = "⚡ Tổng cộng: 0 cổng | Đang chạy: 0 | Profile đã gán: 0",
                Padding = new Padding(10, 4, 0, 0)
            };
            this.Controls.Add(_lblSummary);

            // ================= MAIN WORK AREA =================
            Panel pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 0)
            };

            // 1. Toolbar Top: Provider & Port Config & Main Actions
            Panel pnlControlBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.White,
                Padding = new Padding(10, 6, 10, 6)
            };

            Label lblChoose = new Label { Text = "Nguồn Proxy:", Location = new Point(10, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _cboProvider = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(96, 8),
                Width = 260,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _cboProvider.Items.Add("⚡ Cloudflare WARP (Tự Động - Free 100%)");
            _cboProvider.Items.Add("🛡️ NordVPN (Tài Khoản Riêng - 10 Server)");
            _cboProvider.Items.Add("🌐 VPN Gate (Cộng Đồng Miễn Phí)");
            _cboProvider.Items.Add("📁 File OpenVPN (.ovpn) Tùy Chỉnh");
            _cboProvider.SelectedIndex = 1; // Default to NordVPN since user just provided credentials
            _cboProvider.SelectedIndexChanged += (s, e) => SwitchProviderView();

            Label lblP1 = new Label { Text = "Cổng Đầu:", Location = new Point(368, 12), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _numStartPort = new NumericUpDown { Location = new Point(430, 9), Width = 75, Minimum = 1024, Maximum = 65530, Value = 11001, Font = new Font("Segoe UI", 9F) };

            Label lblP2 = new Label { Text = "Số Cổng:", Location = new Point(512, 12), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _numPortCount = new NumericUpDown { Location = new Point(568, 9), Width = 55, Minimum = 1, Maximum = 50, Value = 5, Font = new Font("Segoe UI", 9F) };

            _btnInitPorts = CreateButton("🔄 Nạp Cổng", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 90);
            _btnInitPorts.Location = new Point(630, 7);
            _btnInitPorts.Height = 28;
            _btnInitPorts.Click += (s, e) => RebuildCurrentPorts();

            _btnStartAll = CreateButton("▶️ Khởi Động Tất Cả", Color.FromArgb(16, 185, 129), Color.White, 140);
            _btnStartAll.Location = new Point(728, 7);
            _btnStartAll.Height = 28;
            _btnStartAll.Click += async (s, e) => await StartAllAsync();

            _btnStopAll = CreateButton("⏹️ Dừng", Color.FromArgb(239, 68, 68), Color.White, 70);
            _btnStopAll.Location = new Point(874, 7);
            _btnStopAll.Height = 28;
            _btnStopAll.Click += (s, e) => StopAll();

            _btnCheckIp = CreateButton("🔍 Check IP", Color.FromArgb(14, 165, 233), Color.White, 85);
            _btnCheckIp.Location = new Point(950, 7);
            _btnCheckIp.Height = 28;
            _btnCheckIp.Click += async (s, e) => await CheckAllIpsAsync();

            _btnAssign = CreateButton("⚡ Gán Profile", Color.FromArgb(99, 102, 241), Color.White, 95);
            _btnAssign.Location = new Point(1040, 7);
            _btnAssign.Height = 28;
            _btnAssign.Click += (s, e) => ShowAssignDialog();

            _btnCopy = CreateButton("📋 Copy", Color.FromArgb(71, 85, 105), Color.White, 70);
            _btnCopy.Location = new Point(1140, 7);
            _btnCopy.Height = 28;
            _btnCopy.Click += (s, e) => CopyProxyList();

            pnlControlBar.Controls.Add(lblChoose);
            pnlControlBar.Controls.Add(_cboProvider);
            pnlControlBar.Controls.Add(lblP1);
            pnlControlBar.Controls.Add(_numStartPort);
            pnlControlBar.Controls.Add(lblP2);
            pnlControlBar.Controls.Add(_numPortCount);
            pnlControlBar.Controls.Add(_btnInitPorts);
            pnlControlBar.Controls.Add(_btnStartAll);
            pnlControlBar.Controls.Add(_btnStopAll);
            pnlControlBar.Controls.Add(_btnCheckIp);
            pnlControlBar.Controls.Add(_btnAssign);
            pnlControlBar.Controls.Add(_btnCopy);

            // 2. Dynamic Settings Bar (Changes based on selected provider)
            _pnlProviderSettings = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(10, 6, 10, 6)
            };

            // 3. Central Grid
            _gridPorts = CreateStyledGrid();
            var btnCol = new DataGridViewButtonColumn
            {
                Name = "clAction",
                HeaderText = "Bật / Tắt",
                Text = "Bật / Tắt",
                UseColumnTextForButtonValue = true,
                Width = 85
            };
            _gridPorts.Columns.Add(btnCol);

            var btnRotateCol = new DataGridViewButtonColumn
            {
                Name = "clRotateIp",
                HeaderText = "Đổi IP",
                Text = "🔄 Đổi IP",
                UseColumnTextForButtonValue = true,
                Width = 85
            };
            _gridPorts.Columns.Add(btnRotateCol);

            var btnAssignRowCol = new DataGridViewButtonColumn
            {
                Name = "clAssignRow",
                HeaderText = "Gán Profile",
                Text = "⚡ Gán",
                UseColumnTextForButtonValue = true,
                Width = 80
            };
            _gridPorts.Columns.Add(btnAssignRowCol);

            // Context Menu & Selection
            var mnuContext = new ContextMenuStrip();
            var mnuAssign = new ToolStripMenuItem("⚡ Gán cổng này cho Profile...");
            mnuAssign.Click += (s, e) =>
            {
                if (_gridPorts.CurrentRow != null)
                {
                    var itm = _gridPorts.CurrentRow.Tag as HmaProxyPortItem;
                    if (itm != null) ShowAssignDialog(itm.Port);
                }
            };
            var mnuCopyProxy = new ToolStripMenuItem("📋 Copy Proxy (127.0.0.1:Port)");
            mnuCopyProxy.Click += (s, e) =>
            {
                if (_gridPorts.CurrentRow != null)
                {
                    var itm = _gridPorts.CurrentRow.Tag as HmaProxyPortItem;
                    if (itm != null)
                    {
                        string pxy = string.Format("127.0.0.1:{0}", itm.Port);
                        Clipboard.SetText(pxy);
                        MessageBox.Show("Đã copy: " + pxy, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };
            mnuContext.Items.Add(mnuAssign);
            mnuContext.Items.Add(mnuCopyProxy);
            _gridPorts.ContextMenuStrip = mnuContext;

            _gridPorts.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var hti = _gridPorts.HitTest(e.X, e.Y);
                    if (hti.RowIndex >= 0)
                    {
                        _gridPorts.ClearSelection();
                        _gridPorts.Rows[hti.RowIndex].Selected = true;
                        _gridPorts.CurrentCell = _gridPorts.Rows[hti.RowIndex].Cells[hti.ColumnIndex >= 0 ? hti.ColumnIndex : 0];
                    }
                }
            };

            _gridPorts.CellContentClick += GridPorts_CellContentClick;
            _gridPorts.CellPainting += Grid_CellPainting;

            pnlMain.Controls.Add(_gridPorts);
            pnlMain.Controls.Add(_pnlProviderSettings);
            pnlMain.Controls.Add(pnlControlBar);
            this.Controls.Add(pnlMain);
            pnlMain.BringToFront();
        }

        #region PROVIDER SWITCHING & SUB-PANELS
        private void SwitchProviderView()
        {
            _pnlProviderSettings.Controls.Clear();
            int idx = _cboProvider.SelectedIndex;

            if (idx == 0) // Cloudflare WARP
            {
                _chkWarpAntiDuplicate = new CheckBox
                {
                    Text = "🛡️ Tự động lọc & chống trùng IP khi Khởi động",
                    Location = new Point(14, 11),
                    AutoSize = true,
                    Checked = true,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(16, 185, 129)
                };

                _btnDeduplicate = CreateButton("🛡️ Đổi Cổng Trùng IP", Color.FromArgb(139, 92, 246), Color.White, 140);
                _btnDeduplicate.Location = new Point(360, 6);
                _btnDeduplicate.Height = 28;
                _btnDeduplicate.Click += async (s, e) =>
                {
                    _btnDeduplicate.Enabled = false;
                    try
                    {
                        int fixedCount = await _warpService.DeduplicateAllPortsAsync();
                        MessageBox.Show(string.Format("Đã quét hoàn tất! Đổi thành công {0} cổng bị trùng IP.", fixedCount), "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    finally
                    {
                        _btnDeduplicate.Enabled = true;
                    }
                };

                _btnWarpResetAll = CreateButton("🔄 Đổi Toàn Bộ IP", Color.FromArgb(245, 158, 11), Color.White, 120);
                _btnWarpResetAll.Location = new Point(510, 6);
                _btnWarpResetAll.Height = 28;
                _btnWarpResetAll.Click += async (s, e) =>
                {
                    _btnWarpResetAll.Enabled = false;
                    try
                    {
                        var ports = _warpService.PortItems.Select(p => p.Port).ToList();
                        foreach (var p in ports) await _warpService.ResetPortIpAsync(p);
                    }
                    finally { _btnWarpResetAll.Enabled = true; }
                };

                Label lblNote = new Label
                {
                    Text = "💡 SOCKS5 Userspace độc lập, không cần tài khoản, tốc độ cực nhanh.",
                    Location = new Point(645, 12),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Font = new Font("Segoe UI", 8F, FontStyle.Italic)
                };

                _pnlProviderSettings.Controls.Add(_chkWarpAntiDuplicate);
                _pnlProviderSettings.Controls.Add(_btnDeduplicate);
                _pnlProviderSettings.Controls.Add(_btnWarpResetAll);
                _pnlProviderSettings.Controls.Add(lblNote);
            }
            else if (idx == 1) // NordVPN
            {
                Label lblU = new Label { Text = "Tài khoản:", Location = new Point(14, 12), AutoSize = true, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
                _txtNordUser = new TextBox { Location = new Point(78, 9), Width = 150, Font = new Font("Segoe UI", 8.5F), Text = _openVpnService.Config.Username ?? "XPdwcohpCv7jCEaVFhZoLL2S" };

                Label lblP = new Label { Text = "Mật khẩu:", Location = new Point(236, 12), AutoSize = true, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
                _txtNordPass = new TextBox { Location = new Point(298, 9), Width = 130, Font = new Font("Segoe UI", 8.5F), UseSystemPasswordChar = true, Text = _openVpnService.Config.Password ?? "3UoFz2FcKvEJaPgycG118M5c" };

                _chkShowNordPass = new CheckBox { Text = "Hiện", Location = new Point(434, 11), AutoSize = true, Font = new Font("Segoe UI", 7.5F) };
                _chkShowNordPass.CheckedChanged += (s, e) => _txtNordPass.UseSystemPasswordChar = !_chkShowNordPass.Checked;

                _btnSaveNord = CreateButton("💾 Lưu", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 55);
                _btnSaveNord.Location = new Point(488, 8);
                _btnSaveNord.Height = 26;
                _btnSaveNord.Click += (s, e) =>
                {
                    _openVpnService.Config.Username = _txtNordUser.Text.Trim();
                    _openVpnService.Config.Password = _txtNordPass.Text.Trim();
                    _openVpnService.SaveConfig();
                    MessageBox.Show("Đã lưu thông tin tài khoản NordVPN thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                Label lblCountry = new Label { Text = "Quốc gia:", Location = new Point(550, 12), AutoSize = true, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
                _cboNordCountry = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(610, 8),
                    Width = 145,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
                };
                _cboNordCountry.Items.Add("🇻🇳 Việt Nam");
                _cboNordCountry.Items.Add("🌏 Toàn Cầu / Tối Ưu");
                _cboNordCountry.Items.Add("🇸🇬 Singapore");
                _cboNordCountry.Items.Add("🇯🇵 Nhật Bản");
                _cboNordCountry.Items.Add("🇭🇰 Hồng Kông");
                _cboNordCountry.Items.Add("🇺🇸 Hoa Kỳ");
                _cboNordCountry.SelectedIndex = 0;
                _cboNordCountry.SelectedIndexChanged += (s, e) => RebuildCurrentPorts();

                _btnDownloadNord = CreateButton("🔄 Tải Server", Color.FromArgb(219, 234, 254), Color.FromArgb(30, 64, 175), 90);
                _btnDownloadNord.Location = new Point(762, 8);
                _btnDownloadNord.Height = 26;
                _btnDownloadNord.Click += async (s, e) =>
                {
                    _btnDownloadNord.Enabled = false;
                    _btnDownloadNord.Text = "⏳ Đang tải...";
                    try
                    {
                        string nordDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "openvpn", "nordvpn_configs");
                        int cIdx = _cboNordCountry.SelectedIndex;
                        int cid = 234;
                        if (cIdx == 1) cid = 0;
                        else if (cIdx == 2) cid = 195;
                        else if (cIdx == 3) cid = 108;
                        else if (cIdx == 4) cid = 97;
                        else if (cIdx == 5) cid = 228;

                        await _openVpnService.DownloadNordVpnConfigsAsync(nordDir, 20, cid);
                        LoadNordServers();
                        RebuildCurrentPorts();
                        MessageBox.Show("Đã cập nhật danh sách server NordVPN thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    finally
                    {
                        _btnDownloadNord.Enabled = true;
                        _btnDownloadNord.Text = "🔄 Tải Server";
                    }
                };

                _lblNordStatus = new Label
                {
                    Text = string.Format("✅ {0} server NordVPN sẵn sàng", _nordServers.Count),
                    Location = new Point(860, 12),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(16, 185, 129),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
                };

                _pnlProviderSettings.Controls.Add(lblU);
                _pnlProviderSettings.Controls.Add(_txtNordUser);
                _pnlProviderSettings.Controls.Add(lblP);
                _pnlProviderSettings.Controls.Add(_txtNordPass);
                _pnlProviderSettings.Controls.Add(_chkShowNordPass);
                _pnlProviderSettings.Controls.Add(_btnSaveNord);
                _pnlProviderSettings.Controls.Add(lblCountry);
                _pnlProviderSettings.Controls.Add(_cboNordCountry);
                _pnlProviderSettings.Controls.Add(_btnDownloadNord);
                _pnlProviderSettings.Controls.Add(_lblNordStatus);
            }
            else if (idx == 2) // VPN Gate
            {
                _lblVpnGateStatus = new Label
                {
                    Text = string.Format("🌐 VPN Gate Free (Tài khoản: vpn / vpn) | Đã nạp: {0} server", _vpnGateServers.Count),
                    Location = new Point(14, 12),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(22, 101, 52),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
                };

                _btnDownloadVpnGate = CreateButton("🌐 Tải 20 Server VPN Gate Mới", Color.FromArgb(220, 252, 231), Color.FromArgb(22, 101, 52), 200);
                _btnDownloadVpnGate.Location = new Point(410, 8);
                _btnDownloadVpnGate.Height = 26;
                _btnDownloadVpnGate.Click += async (s, e) =>
                {
                    _btnDownloadVpnGate.Enabled = false;
                    _btnDownloadVpnGate.Text = "⏳ Đang tải từ GitHub...";
                    try
                    {
                        string vpnGateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "openvpn", "vpngate_configs");
                        await _openVpnService.DownloadVpnGateConfigsAsync(vpnGateDir, 20);
                        LoadVpnGateServers();
                        RebuildCurrentPorts();
                        MessageBox.Show("Đã tải 20 server VPN Gate miễn phí mới nhất thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    finally
                    {
                        _btnDownloadVpnGate.Enabled = true;
                        _btnDownloadVpnGate.Text = "🌐 Tải 20 Server VPN Gate Mới";
                    }
                };

                Label lblNote = new Label
                {
                    Text = "💡 Dự án VPN học thuật miễn phí từ Đại học Tsukuba (Nhật Bản).",
                    Location = new Point(625, 12),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Font = new Font("Segoe UI", 8F, FontStyle.Italic)
                };

                _pnlProviderSettings.Controls.Add(_lblVpnGateStatus);
                _pnlProviderSettings.Controls.Add(_btnDownloadVpnGate);
                _pnlProviderSettings.Controls.Add(lblNote);
            }
            else // Custom OpenVPN
            {
                Label lblDir = new Label { Text = "Thư mục .ovpn:", Location = new Point(14, 12), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
                _txtCustomDir = new TextBox { Location = new Point(110, 9), Width = 380, Font = new Font("Segoe UI", 8.5F) };

                _btnBrowseCustom = CreateButton("📂 Chọn Thư Mục", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 120);
                _btnBrowseCustom.Location = new Point(498, 8);
                _btnBrowseCustom.Height = 26;
                _btnBrowseCustom.Click += (s, e) => BrowseCustomOvpnFolder();

                _lblCustomStatus = new Label
                {
                    Text = string.Format("Đã nạp {0} file cấu hình", _customServers.Count),
                    Location = new Point(628, 12),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
                };

                _pnlProviderSettings.Controls.Add(lblDir);
                _pnlProviderSettings.Controls.Add(_txtCustomDir);
                _pnlProviderSettings.Controls.Add(_btnBrowseCustom);
                _pnlProviderSettings.Controls.Add(_lblCustomStatus);
            }

            RebuildCurrentPorts();
        }
        #endregion

        #region DATA & PORT MANAGEMENT
        private void LoadInitialData()
        {
            LoadNordServers();
            LoadVpnGateServers();
            SwitchProviderView();
        }

        private void LoadNordServers()
        {
            string nordDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "openvpn", "nordvpn_configs");
            if (Directory.Exists(nordDir))
            {
                _nordServers = _openVpnService.ScanOvpnFiles(nordDir);
            }
        }

        private List<string> GetFilteredNordServers()
        {
            int cIdx = _cboNordCountry != null ? _cboNordCountry.SelectedIndex : 0;
            List<string> list = null;
            if (cIdx == 0) // Việt Nam
            {
                list = _nordServers.Where(s => s.Key.IndexOf("vn", StringComparison.OrdinalIgnoreCase) >= 0).Select(s => s.Key).ToList();
            }
            else if (cIdx == 2) // Singapore
            {
                list = _nordServers.Where(s => s.Key.IndexOf("sg", StringComparison.OrdinalIgnoreCase) >= 0).Select(s => s.Key).ToList();
            }
            else if (cIdx == 3) // Nhật Bản
            {
                list = _nordServers.Where(s => s.Key.IndexOf("jp", StringComparison.OrdinalIgnoreCase) >= 0).Select(s => s.Key).ToList();
            }
            else if (cIdx == 4) // Hồng Kông
            {
                list = _nordServers.Where(s => s.Key.IndexOf("hk", StringComparison.OrdinalIgnoreCase) >= 0).Select(s => s.Key).ToList();
            }
            else if (cIdx == 5) // Hoa Kỳ
            {
                list = _nordServers.Where(s => s.Key.IndexOf("us", StringComparison.OrdinalIgnoreCase) >= 0).Select(s => s.Key).ToList();
            }

            if (list == null || list.Count == 0)
            {
                list = _nordServers.Select(s => s.Key).ToList();
            }
            return list;
        }

        private void LoadVpnGateServers()
        {
            string vpnGateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "openvpn", "vpngate_configs");
            if (Directory.Exists(vpnGateDir))
            {
                _vpnGateServers = _openVpnService.ScanOvpnFiles(vpnGateDir);
            }
        }

        private void BrowseCustomOvpnFolder()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục chứa các file cấu hình .ovpn";
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtCustomDir.Text = fbd.SelectedPath;
                    _customServers = _openVpnService.ScanOvpnFiles(fbd.SelectedPath);
                    if (_lblCustomStatus != null)
                    {
                        _lblCustomStatus.Text = string.Format("✅ Tìm thấy {0} file server", _customServers.Count);
                        _lblCustomStatus.ForeColor = Color.FromArgb(16, 185, 129);
                    }
                    RebuildCurrentPorts();
                }
            }
        }

        private void RebuildCurrentPorts()
        {
            int start = (int)_numStartPort.Value;
            int count = (int)_numPortCount.Value;
            int cleanStart = HmaMultiProxyService.FindCleanPortRange(start, count);
            if (cleanStart != start)
            {
                start = cleanStart;
                _numStartPort.Value = cleanStart;
            }

            int provider = _cboProvider.SelectedIndex;

            if (provider == 0) // WARP
            {
                _warpService.InitializePorts(start, count);
                RefreshGridFromItems(_warpService.PortItems, "Cloudflare WARP");
            }
            else // OpenVPN based (NordVPN, VPN Gate, Custom)
            {
                List<string> ovpnList = new List<string>();
                if (provider == 1) ovpnList = GetFilteredNordServers();
                else if (provider == 2) ovpnList = _vpnGateServers.Select(s => s.Key).ToList();
                else ovpnList = _customServers.Select(s => s.Key).ToList();

                _openVpnService.InitializePorts(start, count, ovpnList);
                string provName = provider == 1 ? "NordVPN" : (provider == 2 ? "VPN Gate" : "OpenVPN");
                RefreshGridFromItems(_openVpnService.PortItems, provName);
            }
        }

        private void RefreshGridFromItems(IEnumerable<HmaProxyPortItem> items, string providerName)
        {
            _gridPorts.Rows.Clear();
            foreach (var item in items)
            {
                int rIdx = _gridPorts.Rows.Add();
                var row = _gridPorts.Rows[rIdx];
                row.Tag = item;
                row.Cells["clPort"].Value = item.ProxyAddress;
                row.Cells["clServer"].Value = string.IsNullOrEmpty(item.ServerName) || item.ServerName == "---" ? providerName : item.ServerName;
                row.Cells["clStatus"].Value = item.StatusText;
                row.Cells["clIp"].Value = item.PublicIp;
                row.Cells["clCountry"].Value = !string.IsNullOrEmpty(item.City) && item.City != "---" ? string.Format("{0} ({1})", item.Country, item.City) : item.Country;
                row.Cells["clPing"].Value = item.PingMs > 0 ? item.PingMs + " ms" : "---";
                row.Cells["clAssigned"].Value = item.AssignedProfileNames.Count > 0 ? string.Join(", ", item.AssignedProfileNames) : "(Chưa gán)";
            }
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int provider = _cboProvider.SelectedIndex;
            int total = 0, running = 0, assigned = 0;

            if (provider == 0)
            {
                total = _warpService.PortItems.Count;
                running = _warpService.PortItems.Count(p => p.Status == HmaTunnelStatus.Connected);
                assigned = _warpService.PortItems.Sum(p => p.AssignedProfileNames.Count);
            }
            else
            {
                total = _openVpnService.PortItems.Count;
                running = _openVpnService.PortItems.Count(p => p.Status == HmaTunnelStatus.Connected);
                assigned = _openVpnService.PortItems.Sum(p => p.AssignedProfileNames.Count);
            }

            _lblSummary.Text = string.Format("⚡ Tổng cộng: {0} cổng | Đang chạy: {1} cổng | Profile đã gán: {2}", total, running, assigned);
        }
        #endregion

        #region START / STOP LOGIC (SMART ISOLATION)
        private async Task StartAllAsync()
        {
            _btnStartAll.Enabled = false;
            try
            {
                // Tự động dừng sạch sẽ mọi tiến trình cũ để tránh hoàn toàn xung đột cổng
                _warpService.StopAll();
                _openVpnService.StopAll();
                await Task.Delay(500);

                int start = (int)_numStartPort.Value;
                int count = (int)_numPortCount.Value;
                int cleanStart = HmaMultiProxyService.FindCleanPortRange(start, count);
                if (cleanStart != start)
                {
                    AppendLog(string.Format("[{0:HH:mm:ss}] 💡 Dải cổng {1} đang bị chiếm dụng trên Windows. Đã tự động đổi sang dải cổng trống: {2}", DateTime.Now, start, cleanStart));
                    start = cleanStart;
                    _numStartPort.Value = cleanStart;
                }

                int provider = _cboProvider.SelectedIndex;

                if (provider == 0) // Cloudflare WARP
                {
                    _warpService.InitializePorts(start, count);
                    RefreshGridFromItems(_warpService.PortItems, "Cloudflare WARP");
                    bool antiDup = _chkWarpAntiDuplicate != null && _chkWarpAntiDuplicate.Checked;
                    await _warpService.StartAllAsync(antiDup);
                }
                else if (provider == 1) // NordVPN
                {
                    if (_nordServers.Count == 0) LoadNordServers();
                    string user = _txtNordUser != null ? _txtNordUser.Text.Trim() : "XPdwcohpCv7jCEaVFhZoLL2S";
                    string pass = _txtNordPass != null ? _txtNordPass.Text.Trim() : "3UoFz2FcKvEJaPgycG118M5c";

                    _openVpnService.Config.Username = user;
                    _openVpnService.Config.Password = pass;
                    _openVpnService.Config.OvpnDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "openvpn", "nordvpn_configs");
                    _openVpnService.SaveConfig();

                    _openVpnService.InitializePorts(start, count, GetFilteredNordServers());
                    RefreshGridFromItems(_openVpnService.PortItems, "NordVPN");
                    await _openVpnService.StartAllAsync();
                }
                else if (provider == 2) // VPN Gate
                {
                    if (_vpnGateServers.Count == 0) LoadVpnGateServers();
                    _openVpnService.Config.Username = "vpn";
                    _openVpnService.Config.Password = "vpn";
                    _openVpnService.Config.OvpnDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "openvpn", "vpngate_configs");
                    _openVpnService.SaveConfig();

                    _openVpnService.InitializePorts(start, count, _vpnGateServers.Select(s => s.Key).ToList());
                    RefreshGridFromItems(_openVpnService.PortItems, "VPN Gate");
                    await _openVpnService.StartAllAsync();
                }
                else // Custom OpenVPN
                {
                    _openVpnService.InitializePorts(start, count, _customServers.Select(s => s.Key).ToList());
                    RefreshGridFromItems(_openVpnService.PortItems, "OpenVPN");
                    await _openVpnService.StartAllAsync();
                }
            }
            finally
            {
                _btnStartAll.Enabled = true;
            }
        }

        private void StopAll()
        {
            _warpService.StopAll();
            _openVpnService.StopAll();
            UpdateSummary();
        }

        private async Task CheckAllIpsAsync()
        {
            int provider = _cboProvider.SelectedIndex;
            if (provider == 0)
            {
                var tasks = _warpService.PortItems.Where(p => p.Status == HmaTunnelStatus.Connected)
                    .Select(p => _warpService.CheckPortPublicIpAsync(p)).ToList();
                await Task.WhenAll(tasks);
            }
            else
            {
                var tasks = _openVpnService.PortItems.Where(p => p.Status == HmaTunnelStatus.Connected)
                    .Select(p => _openVpnService.CheckPortPublicIpAsync(p)).ToList();
                await Task.WhenAll(tasks);
            }
        }

        private async void GridPorts_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = _gridPorts.Rows[e.RowIndex];
            var item = row.Tag as HmaProxyPortItem;
            if (item == null) return;

            int provider = _cboProvider.SelectedIndex;

            if (e.ColumnIndex == _gridPorts.Columns["clAction"].Index)
            {
                if (provider == 0) // WARP
                {
                    if (item.Status == HmaTunnelStatus.Connected || item.Status == HmaTunnelStatus.Starting)
                        _warpService.StopPort(item.Port);
                    else
                        await _warpService.StartPortAsync(item.Port);
                }
                else // OpenVPN
                {
                    if (item.Status == HmaTunnelStatus.Connected || item.Status == HmaTunnelStatus.Starting)
                        _openVpnService.StopPort(item.Port);
                    else
                        await _openVpnService.StartPortAsync(item.Port);
                }
            }
            else if (_gridPorts.Columns.Contains("clRotateIp") && e.ColumnIndex == _gridPorts.Columns["clRotateIp"].Index)
            {
                if (provider == 0)
                {
                    await _warpService.ResetPortIpAsync(item.Port);
                }
                else
                {
                    _openVpnService.StopPort(item.Port);
                    await Task.Delay(500);
                    await _openVpnService.StartPortAsync(item.Port);
                }
            }
            else if (_gridPorts.Columns.Contains("clAssignRow") && e.ColumnIndex == _gridPorts.Columns["clAssignRow"].Index)
            {
                ShowAssignDialog(item.Port);
            }
        }
        #endregion

        #region ASSIGN & EVENTS
        private void ShowAssignDialog(int? preSelectedPort = null)
        {
            if (!preSelectedPort.HasValue && _gridPorts.CurrentRow != null)
            {
                var currentItem = _gridPorts.CurrentRow.Tag as HmaProxyPortItem;
                if (currentItem != null)
                {
                    preSelectedPort = currentItem.Port;
                }
            }

            int provider = _cboProvider.SelectedIndex;
            var portItems = provider == 0 ? _warpService.PortItems : _openVpnService.PortItems;

            ShowGenericAssignDialog(portItems, preSelectedPort, (selectedProfiles, targetPorts) =>
            {
                if (provider == 0)
                {
                    _warpService.AssignProxiesToProfiles(selectedProfiles, targetPorts);
                    RefreshGridFromItems(_warpService.PortItems, "Cloudflare WARP");
                }
                else
                {
                    string provName = provider == 1 ? "NordVPN" : (provider == 2 ? "VPN Gate" : "OpenVPN");
                    _openVpnService.AssignProxiesToProfiles(selectedProfiles, targetPorts);
                    RefreshGridFromItems(_openVpnService.PortItems, provName);
                }
            });
        }

        private void WireEvents()
        {
            _warpService.PortStatusChanged += item =>
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                try { this.BeginInvoke(new Action(() => UpdateRow(item))); } catch { }
            };

            _openVpnService.PortStatusChanged += item =>
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                try { this.BeginInvoke(new Action(() => UpdateRow(item))); } catch { }
            };

            _warpService.LogReceived += msg => AppendLog(msg);
            _openVpnService.LogReceived += msg => AppendLog(msg);
        }

        private void UpdateRow(HmaProxyPortItem item)
        {
            foreach (DataGridViewRow row in _gridPorts.Rows)
            {
                var rowItem = row.Tag as HmaProxyPortItem;
                if (rowItem != null && rowItem.Port == item.Port)
                {
                    row.Cells["clStatus"].Value = item.StatusText;
                    row.Cells["clIp"].Value = item.PublicIp;
                    row.Cells["clCountry"].Value = !string.IsNullOrEmpty(item.City) && item.City != "---" ? string.Format("{0} ({1})", item.Country, item.City) : item.Country;
                    row.Cells["clPing"].Value = item.PingMs > 0 ? item.PingMs + " ms" : "---";
                    row.Cells["clAssigned"].Value = item.AssignedProfileNames.Count > 0 ? string.Join(", ", item.AssignedProfileNames) : "(Chưa gán)";
                    break;
                }
            }
            UpdateSummary();
        }

        private void AppendLog(string msg)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            try
            {
                this.BeginInvoke(new Action(() =>
                {
                    if (_txtLog != null && !_txtLog.IsDisposed)
                    {
                        if (_txtLog.TextLength > 30000) _txtLog.Clear();
                        _txtLog.AppendText(msg + Environment.NewLine);
                    }
                }));
            }
            catch { }
        }
        #endregion

        #region HELPERS & STYLED GRID
        private DataGridView CreateStyledGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(243, 244, 246),
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                RowTemplate = { Height = 32 },
                EnableHeadersVisualStyles = false
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(71, 85, 105);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            grid.ColumnHeadersHeight = 32;

            grid.Columns.Add("clPort", "Cổng Local (Proxy)");
            grid.Columns.Add("clServer", "Máy Chủ / Giao Thức");
            grid.Columns.Add("clStatus", "Trạng Thái");
            grid.Columns.Add("clIp", "Public IP");
            grid.Columns.Add("clCountry", "Nhà Mạng / Quốc Gia");
            grid.Columns.Add("clPing", "Độ Trễ");
            grid.Columns.Add("clAssigned", "Profile Đang Gán");

            grid.Columns["clPort"].Width = 160;
            grid.Columns["clServer"].Width = 190;
            grid.Columns["clStatus"].Width = 135;
            grid.Columns["clIp"].Width = 130;
            grid.Columns["clCountry"].Width = 160;
            grid.Columns["clPing"].Width = 80;
            grid.Columns["clAssigned"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            return grid;
        }

        private Button CreateButton(string text, Color bg, Color fg, int width)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 32,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid != null && e.RowIndex >= 0 && e.ColumnIndex == grid.Columns["clStatus"].Index && e.Value != null)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                string text = e.Value.ToString();
                Color bgColor = Color.FromArgb(241, 245, 249);
                Color fgColor = Color.FromArgb(71, 85, 105);

                if (text.Contains("🟢") || text.Contains("LIVE") || text.Contains("Đang chạy"))
                {
                    bgColor = Color.FromArgb(209, 250, 229);
                    fgColor = Color.FromArgb(6, 95, 70);
                }
                else if (text.Contains("Đang khởi tạo") || text.Contains("Chờ"))
                {
                    bgColor = Color.FromArgb(254, 243, 199);
                    fgColor = Color.FromArgb(146, 64, 14);
                }
                else if (text.Contains("Lỗi") || text.Contains("trùng"))
                {
                    bgColor = Color.FromArgb(254, 226, 226);
                    fgColor = Color.FromArgb(153, 27, 27);
                }

                var rect = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + 4, e.CellBounds.Width - 12, e.CellBounds.Height - 8);
                using (var brush = new SolidBrush(bgColor))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }

                TextRenderer.DrawText(e.Graphics, text, e.CellStyle.Font, rect, fgColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                e.Handled = true;
            }
        }

        private void CopyProxyList()
        {
            int provider = _cboProvider.SelectedIndex;
            var list = new List<string>();
            if (provider == 0)
            {
                foreach (var item in _warpService.PortItems)
                    list.Add(string.Format("socks5://127.0.0.1:{0}", item.Port));
            }
            else
            {
                foreach (var item in _openVpnService.PortItems)
                    list.Add(string.Format("http://127.0.0.1:{0}", item.Port));
            }

            if (list.Count > 0)
            {
                Clipboard.SetText(string.Join(Environment.NewLine, list));
                MessageBox.Show(string.Format("Đã copy {0} cổng proxy vào bộ nhớ tạm!", list.Count), "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private class ProxyPortChoice
        {
            public int? Port { get; set; }
            public string DisplayText { get; set; }
            public override string ToString() { return DisplayText; }
        }

        private class ProfileListItem
        {
            public UserProfile Profile { get; set; }
            public override string ToString()
            {
                string curr = string.IsNullOrEmpty(Profile.Proxy) ? "Trực tiếp (Không Proxy)" : Profile.Proxy;
                return string.Format("{0}   —   [Đang dùng: {1}]", Profile.ProfileName, curr);
            }
        }

        private void ShowGenericAssignDialog(List<HmaProxyPortItem> portItems, int? preSelectedPort, Action<List<UserProfile>, List<int>> onConfirm)
        {
            var dialog = new Form
            {
                Text = "⚡ GÁN CỔNG PROXY VÀO BROWSER PROFILES",
                Size = new Size(600, 560),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            var lblStep1 = new Label
            {
                Text = "1. Chọn cổng Proxy đích cần gán:",
                Location = new Point(16, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42)
            };

            var cboTargetPort = new ComboBox
            {
                Location = new Point(16, 36),
                Size = new Size(550, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };

            cboTargetPort.Items.Add(new ProxyPortChoice
            {
                Port = null,
                DisplayText = "🎯 Phân bổ đều cho tất cả các cổng (Tự động chia đều)"
            });

            int selectedIndex = 0;
            if (portItems != null)
            {
                for (int i = 0; i < portItems.Count; i++)
                {
                    var pItem = portItems[i];
                    string status = pItem.Status == HmaTunnelStatus.Connected ? "🟢 LIVE" : "⚪ " + pItem.Status;
                    string ip = string.IsNullOrEmpty(pItem.PublicIp) || pItem.PublicIp == "Chưa nhận IP" ? "Chưa có IP" : pItem.PublicIp;
                    string text = string.Format("Cổng {0} (127.0.0.1:{0}) | {1} | IP: {2} | {3}", pItem.Port, status, ip, pItem.ServerName ?? "");

                    cboTargetPort.Items.Add(new ProxyPortChoice
                    {
                        Port = pItem.Port,
                        DisplayText = text
                    });

                    if (preSelectedPort.HasValue && pItem.Port == preSelectedPort.Value)
                    {
                        selectedIndex = cboTargetPort.Items.Count - 1;
                    }
                }
            }

            cboTargetPort.SelectedIndex = selectedIndex;

            var lblStep2 = new Label
            {
                Text = "2. Chọn danh sách Profile để gán proxy:",
                Location = new Point(16, 76),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42)
            };

            var btnSelectAll = new Button
            {
                Text = "✅ Chọn tất cả",
                Location = new Point(16, 102),
                Size = new Size(95, 26),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F),
                BackColor = Color.FromArgb(226, 232, 240)
            };
            btnSelectAll.FlatAppearance.BorderSize = 0;

            var btnDeselectAll = new Button
            {
                Text = "❌ Bỏ chọn hết",
                Location = new Point(118, 102),
                Size = new Size(95, 26),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F),
                BackColor = Color.FromArgb(226, 232, 240)
            };
            btnDeselectAll.FlatAppearance.BorderSize = 0;

            var lblCount = new Label
            {
                Location = new Point(230, 106),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            var chkList = new CheckedListBox
            {
                Location = new Point(16, 134),
                Size = new Size(550, 316),
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9F)
            };

            var profiles = AccountManager.Instance.GetAllProfiles();
            foreach (var p in profiles)
            {
                chkList.Items.Add(new ProfileListItem { Profile = p }, false);
            }

            Action updateCounter = () =>
            {
                lblCount.Text = string.Format("Đã chọn: {0} / {1} profiles", chkList.CheckedItems.Count, profiles.Count);
            };

            btnSelectAll.Click += (s, e) =>
            {
                for (int i = 0; i < chkList.Items.Count; i++)
                    chkList.SetItemChecked(i, true);
                updateCounter();
            };

            btnDeselectAll.Click += (s, e) =>
            {
                for (int i = 0; i < chkList.Items.Count; i++)
                    chkList.SetItemChecked(i, false);
                updateCounter();
            };

            chkList.ItemCheck += (s, e) =>
            {
                this.BeginInvoke(new Action(updateCounter));
            };

            for (int i = 0; i < chkList.Items.Count; i++)
                chkList.SetItemChecked(i, true);
            updateCounter();

            var btnOk = CreateButton("⚡ Xác Nhận Gán", Color.FromArgb(16, 185, 129), Color.White, 140);
            btnOk.Location = new Point(306, 468);
            btnOk.Click += (s, e) =>
            {
                var selected = new List<UserProfile>();
                for (int i = 0; i < chkList.CheckedItems.Count; i++)
                {
                    var item = chkList.CheckedItems[i] as ProfileListItem;
                    if (item != null)
                        selected.Add(item.Profile);
                }

                if (selected.Count == 0)
                {
                    MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để gán proxy!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var chosen = cboTargetPort.SelectedItem as ProxyPortChoice;
                List<int> targetPorts = null;
                if (chosen != null && chosen.Port.HasValue)
                {
                    targetPorts = new List<int> { chosen.Port.Value };
                }

                onConfirm(selected, targetPorts);

                string msg = targetPorts != null
                    ? string.Format("Đã gán thành công {0} profile vào Cổng {1}!", selected.Count, targetPorts[0])
                    : string.Format("Đã phân bổ đều thành công {0} profile qua các cổng proxy!", selected.Count);

                MessageBox.Show(msg, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                dialog.Close();
            };

            var btnCancel = CreateButton("Hủy", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 100);
            btnCancel.Location = new Point(466, 468);
            btnCancel.Click += (s, e) => dialog.Close();

            dialog.Controls.Add(lblStep1);
            dialog.Controls.Add(cboTargetPort);
            dialog.Controls.Add(lblStep2);
            dialog.Controls.Add(btnSelectAll);
            dialog.Controls.Add(btnDeselectAll);
            dialog.Controls.Add(lblCount);
            dialog.Controls.Add(chkList);
            dialog.Controls.Add(btnOk);
            dialog.Controls.Add(btnCancel);

            dialog.ShowDialog(this);
        }
        #endregion
    }
}
