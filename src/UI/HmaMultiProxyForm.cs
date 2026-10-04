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

        // UI Tabs
        private TabControl _tabControl;
        private TabPage _tabWarp;
        private TabPage _tabOpenVpn;

        // Tab WARP Controls
        private NumericUpDown _numWarpStartPort;
        private NumericUpDown _numWarpPortCount;
        private CheckBox _chkWarpAntiDuplicate;
        private Button _btnWarpStartAll;
        private Button _btnWarpDeduplicate;
        private Button _btnWarpStopAll;
        private Button _btnWarpCheckIp;
        private Button _btnWarpResetIps;
        private Button _btnWarpAssign;
        private Button _btnWarpCopy;
        private DataGridView _gridWarp;
        private Label _lblWarpSummary;

        // Tab OpenVPN Controls
        private TextBox _txtUsername;
        private TextBox _txtPassword;
        private CheckBox _chkShowPass;
        private Button _btnSaveCredentials;
        private TextBox _txtOvpnDir;
        private Button _btnBrowseOvpn;
        private Label _lblOvpnCount;
        private NumericUpDown _numOvpnStartPort;
        private NumericUpDown _numOvpnPortCount;
        private Button _btnInitOvpnPorts;
        private Label _lblOpenVpnStatus;
        private Button _btnGetOpenVpn;
        private Button _btnOvpnStartAll;
        private Button _btnOvpnStopAll;
        private Button _btnOvpnCheckIp;
        private Button _btnOvpnAssign;
        private Button _btnOvpnCopy;
        private DataGridView _gridOvpn;
        private Label _lblOvpnSummary;

        // Shared Logs
        private TextBox _txtLog;
        private List<KeyValuePair<string, string>> _discoveredOvpn = new List<KeyValuePair<string, string>>();

        public HmaMultiProxyForm(List<UserProfile> currentProfiles = null)
        {
            _allProfiles = currentProfiles ?? AccountManager.Instance.GetAllProfiles();
            InitializeComponent();
            LoadFormConfig();
            WireEvents();
        }

        private void InitializeComponent()
        {
            this.Text = "🌐 VPN & CLOUDFLARE MULTI-PROXY MANAGER (ĐA CỔNG CỤC BỘ)";
            this.Size = new Size(1140, 760);
            this.MinimumSize = new Size(980, 620);
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
                Text = "🌐 MULTI-PROXY STUDIO (CLOUDFLARE WARP & OPENVPN RUNNER)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Tạo dải Proxy cục bộ độc lập (127.0.0.1:10001, 10002...): Hỗ trợ Cloudflare WARP Miễn Phí 100% & HMA / NordVPN OpenVPN",
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
                Text = "📝 Nhật Ký Hoạt Động & Sự Kiện Mạng Thời Gian Thực:"
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

            // ================= TAB CONTROL =================
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Padding = new Point(14, 6)
            };

            _tabWarp = new TabPage("⚡ Cloudflare WARP (Tự Động)");
            _tabWarp.BackColor = Color.FromArgb(248, 250, 252);
            InitializeWarpTab(_tabWarp);

            _tabOpenVpn = new TabPage("🌐 VPN Gate (Miễn Phí 100%) & OpenVPN Runner");
            _tabOpenVpn.BackColor = Color.FromArgb(248, 250, 252);
            InitializeOpenVpnTab(_tabOpenVpn);

            _tabControl.TabPages.Add(_tabWarp);
            _tabControl.TabPages.Add(_tabOpenVpn);
            this.Controls.Add(_tabControl);
            _tabControl.BringToFront();
        }

        #region TAB 1: CLOUDFLARE WARP
        private void InitializeWarpTab(TabPage tab)
        {
            // Toolbar Top
            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 94,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };

            Label lblIntro = new Label
            {
                Text = "⚡ Tự động tạo tài khoản Cloudflare WARP & Chạy Proxy SOCKS5 Userspace độc lập, không chiếm mạng máy tính!",
                Location = new Point(14, 8),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(217, 119, 6)
            };

            _chkWarpAntiDuplicate = new CheckBox
            {
                Text = "🛡️ Tự động lọc & chống trùng IP khi Khởi động",
                Location = new Point(620, 8),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129)
            };

            Label lblP1 = new Label { Text = "Cổng Đầu:", Location = new Point(14, 40), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _numWarpStartPort = new NumericUpDown { Location = new Point(80, 38), Width = 80, Minimum = 1024, Maximum = 65530, Value = 10001, Font = new Font("Segoe UI", 9F) };

            Label lblP2 = new Label { Text = "Số Cổng:", Location = new Point(168, 40), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _numWarpPortCount = new NumericUpDown { Location = new Point(228, 38), Width = 60, Minimum = 1, Maximum = 50, Value = 5, Font = new Font("Segoe UI", 9F) };

            Button btnInitWarp = CreateButton("🔄 Nạp", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 65);
            btnInitWarp.Location = new Point(296, 36);
            btnInitWarp.Click += (s, e) => RebuildWarpPorts();

            _btnWarpStartAll = CreateButton("▶️ Chạy Tất Cả", Color.FromArgb(16, 185, 129), Color.White, 115);
            _btnWarpStartAll.Location = new Point(368, 36);
            _btnWarpStartAll.Click += async (s, e) => await StartAllWarpPortsAsync();

            _btnWarpDeduplicate = CreateButton("🛡️ Đổi Cổng Trùng IP", Color.FromArgb(139, 92, 246), Color.White, 145);
            _btnWarpDeduplicate.Location = new Point(489, 36);
            _btnWarpDeduplicate.Click += async (s, e) =>
            {
                _btnWarpDeduplicate.Enabled = false;
                try
                {
                    int fixedCount = await _warpService.DeduplicateAllPortsAsync();
                    MessageBox.Show(string.Format("Đã hoàn tất kiểm tra! Đổi thành công {0} cổng bị trùng IP.", fixedCount), "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    _btnWarpDeduplicate.Enabled = true;
                }
            };

            _btnWarpStopAll = CreateButton("⏹️ Dừng", Color.FromArgb(239, 68, 68), Color.White, 80);
            _btnWarpStopAll.Location = new Point(640, 36);
            _btnWarpStopAll.Click += (s, e) => _warpService.StopAll();

            _btnWarpResetIps = CreateButton("🔄 Đổi Toàn Bộ IP", Color.FromArgb(245, 158, 11), Color.White, 125);
            _btnWarpResetIps.Location = new Point(726, 36);
            _btnWarpResetIps.Click += async (s, e) => await ResetAllWarpIpsAsync();

            _btnWarpCheckIp = CreateButton("🔍 Check IP", Color.FromArgb(14, 165, 233), Color.White, 95);
            _btnWarpCheckIp.Location = new Point(857, 36);
            _btnWarpCheckIp.Click += async (s, e) =>
            {
                var tasks = _warpService.PortItems.Where(p => p.Status == HmaTunnelStatus.Connected)
                    .Select(p => _warpService.CheckPortPublicIpAsync(p)).ToList();
                await Task.WhenAll(tasks);
            };

            _btnWarpAssign = CreateButton("⚡ Gán Profile", Color.FromArgb(99, 102, 241), Color.White, 110);
            _btnWarpAssign.Location = new Point(958, 36);
            _btnWarpAssign.Click += (s, e) => ShowAssignDialogWarp();

            _btnWarpCopy = CreateButton("📋 Copy", Color.FromArgb(71, 85, 105), Color.White, 75);
            _btnWarpCopy.Location = new Point(1074, 36);
            _btnWarpCopy.Click += (s, e) => CopyProxyList(_warpService.PortItems);

            pnlTop.Controls.Add(lblIntro);
            pnlTop.Controls.Add(_chkWarpAntiDuplicate);
            Button btnGoVpnGate = CreateButton("🌐 Sang Tab VPN Gate (Free)", Color.FromArgb(220, 252, 231), Color.FromArgb(22, 101, 52), 190);
            btnGoVpnGate.Location = new Point(915, 6);
            btnGoVpnGate.Height = 26;
            btnGoVpnGate.Click += (s, e) => _tabControl.SelectedTab = _tabOpenVpn;
            pnlTop.Controls.Add(btnGoVpnGate);

            pnlTop.Controls.Add(lblP1);
            pnlTop.Controls.Add(_numWarpStartPort);
            pnlTop.Controls.Add(lblP2);
            pnlTop.Controls.Add(_numWarpPortCount);
            pnlTop.Controls.Add(btnInitWarp);
            pnlTop.Controls.Add(_btnWarpStartAll);
            pnlTop.Controls.Add(_btnWarpDeduplicate);
            pnlTop.Controls.Add(_btnWarpStopAll);
            pnlTop.Controls.Add(_btnWarpResetIps);
            pnlTop.Controls.Add(_btnWarpCheckIp);
            pnlTop.Controls.Add(_btnWarpAssign);
            pnlTop.Controls.Add(_btnWarpCopy);
            tab.Controls.Add(pnlTop);

            // Summary Label
            _lblWarpSummary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                BackColor = Color.FromArgb(241, 245, 249),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Text = "⚡ Tổng cộng: 0 cổng WARP | Đang chạy: 0 | Profile đã gán: 0",
                Padding = new Padding(10, 3, 0, 0)
            };
            tab.Controls.Add(_lblWarpSummary);

            // Grid WARP
            _gridWarp = CreateStyledGrid();
            var btnCol = new DataGridViewButtonColumn
            {
                Name = "clAction",
                HeaderText = "Bật / Tắt",
                Text = "Bật / Tắt",
                UseColumnTextForButtonValue = true,
                Width = 85
            };
            _gridWarp.Columns.Add(btnCol);

            var btnRotateCol = new DataGridViewButtonColumn
            {
                Name = "clRotateIp",
                HeaderText = "Đổi IP",
                Text = "🔄 Đổi IP",
                UseColumnTextForButtonValue = true,
                Width = 85
            };
            _gridWarp.Columns.Add(btnRotateCol);

            _gridWarp.CellContentClick += GridWarp_CellContentClick;
            _gridWarp.CellPainting += Grid_CellPainting;
            tab.Controls.Add(_gridWarp);
            _gridWarp.BringToFront();
        }

        private void RebuildWarpPorts()
        {
            int start = (int)_numWarpStartPort.Value;
            int count = (int)_numWarpPortCount.Value;
            _warpService.InitializePorts(start, count);
            RefreshGridWarp();
        }

        private void RefreshGridWarp()
        {
            _gridWarp.Rows.Clear();
            foreach (var item in _warpService.PortItems)
            {
                int rIdx = _gridWarp.Rows.Add();
                var row = _gridWarp.Rows[rIdx];
                row.Tag = item;
                row.Cells["clPort"].Value = item.ProxyAddress;
                row.Cells["clServer"].Value = item.ServerName;
                row.Cells["clStatus"].Value = item.StatusText;
                row.Cells["clIp"].Value = item.PublicIp;
                row.Cells["clCountry"].Value = item.Isp;
                row.Cells["clPing"].Value = item.PingMs > 0 ? item.PingMs + " ms" : "---";
                row.Cells["clAssigned"].Value = item.AssignedProfileNames.Count > 0 ? string.Join(", ", item.AssignedProfileNames) : "(Chưa gán)";
            }
            UpdateWarpSummary();
        }

        private void UpdateWarpSummary()
        {
            int total = _warpService.PortItems.Count;
            int running = _warpService.PortItems.Count(p => p.Status == HmaTunnelStatus.Connected);
            int assigned = _warpService.PortItems.Sum(p => p.AssignedProfileNames.Count);
            _lblWarpSummary.Text = string.Format("⚡ Tổng cộng: {0} cổng Cloudflare WARP | Đang chạy: {1} cổng | Profile đã gán: {2}", total, running, assigned);
        }

        private async void GridWarp_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                var row = _gridWarp.Rows[e.RowIndex];
                var item = row.Tag as HmaProxyPortItem;
                if (item == null) return;

                if (e.ColumnIndex == _gridWarp.Columns["clAction"].Index)
                {
                    if (item.Status == HmaTunnelStatus.Connected || item.Status == HmaTunnelStatus.Starting)
                    {
                        _warpService.StopPort(item.Port);
                    }
                    else
                    {
                        await _warpService.StartPortAsync(item.Port);
                    }
                }
                else if (_gridWarp.Columns.Contains("clRotateIp") && e.ColumnIndex == _gridWarp.Columns["clRotateIp"].Index)
                {
                    await _warpService.ResetPortIpAsync(item.Port);
                }
            }
        }

        private async Task StartAllWarpPortsAsync()
        {
            _btnWarpStartAll.Enabled = false;
            try
            {
                bool antiDup = _chkWarpAntiDuplicate != null && _chkWarpAntiDuplicate.Checked;
                await _warpService.StartAllAsync(antiDup);
            }
            finally
            {
                _btnWarpStartAll.Enabled = true;
            }
        }

        private async Task ResetAllWarpIpsAsync()
        {
            _btnWarpResetIps.Enabled = false;
            try
            {
                var ports = _warpService.PortItems.Select(p => p.Port).ToList();
                foreach (var p in ports)
                {
                    await _warpService.ResetPortIpAsync(p);
                }
            }
            finally
            {
                _btnWarpResetIps.Enabled = true;
            }
        }

        private void ShowAssignDialogWarp()
        {
            if (_warpService.PortItems.Count == 0)
            {
                MessageBox.Show("Vui lòng nạp các cổng WARP trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowGenericAssignDialog((selected) =>
            {
                int count = _warpService.AssignProxiesToProfiles(selected);
                MessageBox.Show(string.Format("Đã gán thành công dải Proxy Cloudflare WARP (SOCKS5) vào {0} profile!", count), "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshGridWarp();
            });
        }
        #endregion

        #region TAB 2: OPENVPN (HMA / NORDVPN)
        private void InitializeOpenVpnTab(TabPage tab)
        {
            Panel pnlConfig = new Panel
            {
                Dock = DockStyle.Top,
                Height = 175,
                BackColor = Color.White,
                Padding = new Padding(12)
            };

            // Group 1: Tài khoản
            GroupBox grpAuth = new GroupBox
            {
                Text = "1. Service Credentials (HMA / NordVPN)",
                Location = new Point(12, 6),
                Size = new Size(340, 158),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            Label lblUser = new Label { Text = "OpenVPN / Service Username:", Location = new Point(12, 22), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _txtUsername = new TextBox { Location = new Point(14, 40), Width = 310, Font = new Font("Segoe UI", 9F) };

            Label lblPass = new Label { Text = "OpenVPN / Service Password:", Location = new Point(12, 68), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _txtPassword = new TextBox { Location = new Point(14, 86), Width = 230, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 9F) };

            _chkShowPass = new CheckBox { Text = "Hiện", Location = new Point(252, 88), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _chkShowPass.CheckedChanged += (s, e) => _txtPassword.UseSystemPasswordChar = !_chkShowPass.Checked;

            _btnSaveCredentials = CreateButton("💾 Lưu Tài Khoản", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 310);
            _btnSaveCredentials.Location = new Point(14, 118);
            _btnSaveCredentials.Click += (s, e) => SaveAuthCredentials();

            grpAuth.Controls.Add(lblUser);
            grpAuth.Controls.Add(_txtUsername);
            grpAuth.Controls.Add(lblPass);
            grpAuth.Controls.Add(_txtPassword);
            grpAuth.Controls.Add(_chkShowPass);
            grpAuth.Controls.Add(_btnSaveCredentials);

            // Group 2: Thư mục OVPN
            GroupBox grpOvpn = new GroupBox
            {
                Text = "2. Thư Mục File Cấu Hình (.ovpn)",
                Location = new Point(362, 6),
                Size = new Size(370, 158),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            Label lblDir = new Label { Text = "Đường dẫn thư mục chứa file .ovpn:", Location = new Point(12, 22), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _txtOvpnDir = new TextBox { Location = new Point(14, 40), Width = 265, Font = new Font("Segoe UI", 9F) };

            _btnBrowseOvpn = CreateButton("📂 Chọn...", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 72);
            _btnBrowseOvpn.Location = new Point(285, 38);
            _btnBrowseOvpn.Height = 26;
            _btnBrowseOvpn.Click += (s, e) => BrowseOvpnFolder();

            _lblOvpnCount = new Label
            {
                Text = "Chưa nạp thư mục cấu hình",
                Location = new Point(14, 70),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            Button btnDownloadNord = CreateButton("⚡ Tải Server NordVPN", Color.FromArgb(219, 234, 254), Color.FromArgb(30, 64, 175), 168);
            btnDownloadNord.Location = new Point(14, 94);
            btnDownloadNord.Height = 28;
            btnDownloadNord.Click += async (s, e) =>
            {
                btnDownloadNord.Enabled = false;
                btnDownloadNord.Text = "⏳ Đang tải...";
                try
                {
                    string nordDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "openvpn", "nordvpn_configs");
                    int downloaded = await _openVpnService.DownloadNordVpnConfigsAsync(nordDir, 20);
                    _txtOvpnDir.Text = nordDir;
                    _openVpnService.Config.OvpnDirectory = nordDir;
                    _openVpnService.SaveConfig();
                    _discoveredOvpn = _openVpnService.ScanOvpnFiles(nordDir);
                    if (_discoveredOvpn.Count > 0)
                    {
                        _lblOvpnCount.Text = string.Format("✅ Tìm thấy {0} file cấu hình server", _discoveredOvpn.Count);
                        _lblOvpnCount.ForeColor = Color.FromArgb(16, 185, 129);
                    }
                    RebuildOvpnPorts();
                    MessageBox.Show(string.Format("Đã tải thành công {0} file cấu hình server NordVPN tối ưu!", downloaded), "NordVPN Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    btnDownloadNord.Enabled = true;
                    btnDownloadNord.Text = "⚡ Tải Server NordVPN";
                }
            };

            Button btnDownloadVpnGate = CreateButton("🌐 Tải VPN Gate (Free)", Color.FromArgb(220, 252, 231), Color.FromArgb(22, 101, 52), 168);
            btnDownloadVpnGate.Location = new Point(188, 94);
            btnDownloadVpnGate.Height = 28;
            btnDownloadVpnGate.Click += async (s, e) =>
            {
                btnDownloadVpnGate.Enabled = false;
                btnDownloadVpnGate.Text = "⏳ Đang tải...";
                try
                {
                    string vpnGateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "openvpn", "vpngate_configs");
                    int downloaded = await _openVpnService.DownloadVpnGateConfigsAsync(vpnGateDir, 20);
                    _txtOvpnDir.Text = vpnGateDir;
                    _openVpnService.Config.OvpnDirectory = vpnGateDir;

                    // Tự động điền tài khoản VPN Gate mặc định: vpn / vpn
                    _txtUsername.Text = "vpn";
                    _txtPassword.Text = "vpn";
                    _openVpnService.Config.Username = "vpn";
                    _openVpnService.Config.Password = "vpn";
                    _openVpnService.SaveConfig();

                    _discoveredOvpn = _openVpnService.ScanOvpnFiles(vpnGateDir);
                    if (_discoveredOvpn.Count > 0)
                    {
                        _lblOvpnCount.Text = string.Format("✅ Tìm thấy {0} file cấu hình server", _discoveredOvpn.Count);
                        _lblOvpnCount.ForeColor = Color.FromArgb(16, 185, 129);
                    }
                    RebuildOvpnPorts();
                    MessageBox.Show(string.Format("Đã tải thành công {0} file server VPN Gate miễn phí!\nUsername & Mật khẩu 'vpn/vpn' đã được tự động điền.", downloaded), "VPN Gate Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    btnDownloadVpnGate.Enabled = true;
                    btnDownloadVpnGate.Text = "🌐 Tải VPN Gate (Free)";
                }
            };

            Label lblHintOvpn = new Label
            {
                Text = "💡 Hỗ trợ đầy đủ .ovpn từ VPN Gate (Free), NordVPN, HMA, v.v.",
                Location = new Point(14, 130),
                Size = new Size(345, 20),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 7.5F)
            };

            grpOvpn.Controls.Add(lblDir);
            grpOvpn.Controls.Add(_txtOvpnDir);
            grpOvpn.Controls.Add(_btnBrowseOvpn);
            grpOvpn.Controls.Add(_lblOvpnCount);
            grpOvpn.Controls.Add(btnDownloadNord);
            grpOvpn.Controls.Add(btnDownloadVpnGate);
            grpOvpn.Controls.Add(lblHintOvpn);

            // Group 3: Thiết lập dải cổng
            GroupBox grpPort = new GroupBox
            {
                Text = "3. Thiết Lập Dải Cổng",
                Location = new Point(742, 6),
                Size = new Size(350, 158),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            Label lblP1 = new Label { Text = "Cổng Bắt Đầu:", Location = new Point(14, 24), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _numOvpnStartPort = new NumericUpDown { Location = new Point(14, 42), Width = 110, Minimum = 1024, Maximum = 65530, Value = 10001, Font = new Font("Segoe UI", 9F) };

            Label lblP2 = new Label { Text = "Số Cổng Mở:", Location = new Point(140, 24), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _numOvpnPortCount = new NumericUpDown { Location = new Point(140, 42), Width = 80, Minimum = 1, Maximum = 50, Value = 5, Font = new Font("Segoe UI", 9F) };

            _btnInitOvpnPorts = CreateButton("🔄 Nạp", Color.FromArgb(224, 231, 255), Color.FromArgb(67, 56, 202), 95);
            _btnInitOvpnPorts.Location = new Point(235, 41);
            _btnInitOvpnPorts.Height = 26;
            _btnInitOvpnPorts.Click += (s, e) => RebuildOvpnPorts();

            _lblOpenVpnStatus = new Label
            {
                Text = "Đang kiểm tra OpenVPN...",
                Location = new Point(14, 82),
                Size = new Size(320, 30),
                Font = new Font("Segoe UI", 8F)
            };

            _btnGetOpenVpn = CreateButton("📥 Cài OpenVPN Community", Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14), 320);
            _btnGetOpenVpn.Location = new Point(14, 118);
            _btnGetOpenVpn.Click += (s, e) => OpenVpnGuide();

            grpPort.Controls.Add(lblP1);
            grpPort.Controls.Add(_numOvpnStartPort);
            grpPort.Controls.Add(lblP2);
            grpPort.Controls.Add(_numOvpnPortCount);
            grpPort.Controls.Add(_btnInitOvpnPorts);
            grpPort.Controls.Add(_lblOpenVpnStatus);
            grpPort.Controls.Add(_btnGetOpenVpn);

            pnlConfig.Controls.Add(grpAuth);
            pnlConfig.Controls.Add(grpOvpn);
            pnlConfig.Controls.Add(grpPort);
            tab.Controls.Add(pnlConfig);

            // Action bar OpenVPN
            Panel pnlActions = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(12, 6, 12, 6)
            };

            _btnOvpnStartAll = CreateButton("▶️ Khởi Động Tất Cả", Color.FromArgb(16, 185, 129), Color.White, 150);
            _btnOvpnStartAll.Click += async (s, e) => await _openVpnService.StartAllAsync();

            _btnOvpnStopAll = CreateButton("⏹️ Dừng Tất Cả", Color.FromArgb(239, 68, 68), Color.White, 120);
            _btnOvpnStopAll.Click += (s, e) => _openVpnService.StopAll();

            _btnOvpnCheckIp = CreateButton("🔄 Kiểm Tra IP", Color.FromArgb(14, 165, 233), Color.White, 130);
            _btnOvpnCheckIp.Click += async (s, e) =>
            {
                var tasks = _openVpnService.PortItems.Where(p => p.Status == HmaTunnelStatus.Connected)
                    .Select(p => _openVpnService.CheckPortPublicIpAsync(p)).ToList();
                await Task.WhenAll(tasks);
            };

            _btnOvpnAssign = CreateButton("⚡ Gán Vào Profile", Color.FromArgb(99, 102, 241), Color.White, 150);
            _btnOvpnAssign.Click += (s, e) => ShowAssignDialogOvpn();

            _btnOvpnCopy = CreateButton("📋 Copy Danh Sách", Color.FromArgb(71, 85, 105), Color.White, 140);
            _btnOvpnCopy.Click += (s, e) => CopyProxyList(_openVpnService.PortItems);

            pnlActions.Controls.Add(_btnOvpnStartAll);
            pnlActions.Controls.Add(_btnOvpnStopAll);
            pnlActions.Controls.Add(_btnOvpnCheckIp);
            pnlActions.Controls.Add(_btnOvpnAssign);
            pnlActions.Controls.Add(_btnOvpnCopy);

            int x = 12;
            foreach (Control c in pnlActions.Controls)
            {
                c.Location = new Point(x, 6);
                x += c.Width + 8;
            }
            tab.Controls.Add(pnlActions);

            _lblOvpnSummary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                BackColor = Color.FromArgb(241, 245, 249),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Text = "⚡ Tổng cộng: 0 cổng OpenVPN | Đang chạy: 0 | Profile đã gán: 0",
                Padding = new Padding(10, 3, 0, 0)
            };
            tab.Controls.Add(_lblOvpnSummary);

            _gridOvpn = CreateStyledGrid();
            var btnCol2 = new DataGridViewButtonColumn
            {
                Name = "clAction",
                HeaderText = "Thao Tác",
                Text = "Bật / Tắt",
                UseColumnTextForButtonValue = true,
                Width = 90
            };
            _gridOvpn.Columns.Add(btnCol2);
            _gridOvpn.CellContentClick += GridOvpn_CellContentClick;
            _gridOvpn.CellPainting += Grid_CellPainting;
            tab.Controls.Add(_gridOvpn);
            _gridOvpn.BringToFront();
        }

        private void RebuildOvpnPorts()
        {
            int start = (int)_numOvpnStartPort.Value;
            int count = (int)_numOvpnPortCount.Value;
            var ovpnPaths = _discoveredOvpn.Select(o => o.Key).ToList();
            _openVpnService.InitializePorts(start, count, ovpnPaths);
            RefreshGridOvpn();
        }

        private void RefreshGridOvpn()
        {
            _gridOvpn.Rows.Clear();
            foreach (var item in _openVpnService.PortItems)
            {
                int rIdx = _gridOvpn.Rows.Add();
                var row = _gridOvpn.Rows[rIdx];
                row.Tag = item;
                row.Cells["clPort"].Value = item.ProxyAddress;
                row.Cells["clServer"].Value = item.ServerName;
                row.Cells["clStatus"].Value = item.StatusText;
                row.Cells["clIp"].Value = item.PublicIp;
                row.Cells["clCountry"].Value = !string.IsNullOrEmpty(item.City) && item.City != "---" ? string.Format("{0} ({1})", item.Country, item.City) : item.Country;
                row.Cells["clPing"].Value = item.PingMs > 0 ? item.PingMs + " ms" : "---";
                row.Cells["clAssigned"].Value = item.AssignedProfileNames.Count > 0 ? string.Join(", ", item.AssignedProfileNames) : "(Chưa gán)";
            }
            UpdateOvpnSummary();
        }

        private void UpdateOvpnSummary()
        {
            int total = _openVpnService.PortItems.Count;
            int running = _openVpnService.PortItems.Count(p => p.Status == HmaTunnelStatus.Connected);
            int assigned = _openVpnService.PortItems.Sum(p => p.AssignedProfileNames.Count);
            _lblOvpnSummary.Text = string.Format("⚡ Tổng cộng: {0} cổng OpenVPN | Đang chạy: {1} cổng | Profile đã gán: {2}", total, running, assigned);
        }

        private async void GridOvpn_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _gridOvpn.Columns["clAction"].Index)
            {
                var row = _gridOvpn.Rows[e.RowIndex];
                var item = row.Tag as HmaProxyPortItem;
                if (item == null) return;

                if (item.Status == HmaTunnelStatus.Connected || item.Status == HmaTunnelStatus.Starting)
                {
                    _openVpnService.StopPort(item.Port);
                }
                else
                {
                    await _openVpnService.StartPortAsync(item.Port);
                }
            }
        }

        private void ShowAssignDialogOvpn()
        {
            if (_openVpnService.PortItems.Count == 0)
            {
                MessageBox.Show("Vui lòng nạp các cổng OpenVPN trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowGenericAssignDialog((selected) =>
            {
                int count = _openVpnService.AssignProxiesToProfiles(selected);
                MessageBox.Show(string.Format("Đã gán thành công dải Proxy OpenVPN vào {0} profile!", count), "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshGridOvpn();
            });
        }
        #endregion

        #region HELPERS & SHARED
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
            grid.Columns["clServer"].Width = 180;
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

                if (text.Contains("🟢") || text.Contains("LIVE"))
                {
                    bgColor = Color.FromArgb(209, 250, 229);
                    fgColor = Color.FromArgb(6, 95, 70);
                }
                else if (text.Contains("Đang khởi tạo") || text.Contains("Chờ"))
                {
                    bgColor = Color.FromArgb(254, 243, 199);
                    fgColor = Color.FromArgb(146, 64, 14);
                }
                else if (text.Contains("Lỗi"))
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

        private void ShowGenericAssignDialog(Action<List<UserProfile>> onConfirm)
        {
            var dialog = new Form
            {
                Text = "⚡ GÁN DẢI PROXY VÀO PROFILES",
                Size = new Size(540, 480),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            var lblPrompt = new Label
            {
                Text = "Chọn danh sách profile bạn muốn tự động gán dải cổng Proxy:",
                Location = new Point(16, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var chkSelectAll = new CheckBox
            {
                Text = "Chọn tất cả",
                Location = new Point(16, 36),
                AutoSize = true
            };

            var chkList = new CheckedListBox
            {
                Location = new Point(16, 62),
                Size = new Size(492, 310),
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9F)
            };

            var profiles = AccountManager.Instance.GetAllProfiles();
            foreach (var p in profiles)
            {
                chkList.Items.Add(string.Format("{0}  [Hiện tại: {1}]", p.ProfileName, string.IsNullOrEmpty(p.Proxy) ? "Direct" : p.Proxy), true);
            }

            chkSelectAll.Checked = true;
            chkSelectAll.CheckedChanged += (s, e) =>
            {
                for (int i = 0; i < chkList.Items.Count; i++) chkList.SetItemChecked(i, chkSelectAll.Checked);
            };

            var btnConfirm = CreateButton("✔ Thực Hiện Gán Ngay", Color.FromArgb(16, 185, 129), Color.White, 180);
            btnConfirm.Location = new Point(180, 390);
            btnConfirm.Height = 36;
            btnConfirm.Click += (s, e) =>
            {
                var selected = new List<UserProfile>();
                for (int i = 0; i < chkList.Items.Count; i++)
                {
                    if (chkList.GetItemChecked(i)) selected.Add(profiles[i]);
                }

                if (selected.Count == 0)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất 1 profile!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                onConfirm(selected);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            };

            dialog.Controls.Add(lblPrompt);
            dialog.Controls.Add(chkSelectAll);
            dialog.Controls.Add(chkList);
            dialog.Controls.Add(btnConfirm);

            dialog.ShowDialog(this);
        }

        private void CopyProxyList(List<HmaProxyPortItem> items)
        {
            var lines = items.Select(p => p.ProxyAddress).ToList();
            if (lines.Count > 0)
            {
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
                MessageBox.Show(string.Format("Đã sao chép {0} địa chỉ Proxy vào Clipboard!", lines.Count), "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void LoadFormConfig()
        {
            // WARP default 5 ports
            RebuildWarpPorts();

            // OpenVPN
            _txtUsername.Text = _openVpnService.Config.Username ?? string.Empty;
            _txtPassword.Text = _openVpnService.Config.Password ?? string.Empty;
            _txtOvpnDir.Text = _openVpnService.Config.OvpnDirectory ?? string.Empty;

            CheckOpenVpnEngine();
            if (!string.IsNullOrEmpty(_txtOvpnDir.Text) && Directory.Exists(_txtOvpnDir.Text))
            {
                _discoveredOvpn = _openVpnService.ScanOvpnFiles(_txtOvpnDir.Text);
                if (_discoveredOvpn.Count > 0)
                {
                    _lblOvpnCount.Text = string.Format("✅ Tìm thấy {0} file cấu hình server", _discoveredOvpn.Count);
                    _lblOvpnCount.ForeColor = Color.FromArgb(16, 185, 129);
                }
            }
            RebuildOvpnPorts();
        }

        private void WireEvents()
        {
            _warpService.PortStatusChanged += item =>
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                try
                {
                    this.BeginInvoke(new Action(() => UpdateWarpRow(item)));
                }
                catch { }
            };

            _warpService.LogReceived += log => AppendLog(log);

            _openVpnService.PortStatusChanged += item =>
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                try
                {
                    this.BeginInvoke(new Action(() => UpdateOvpnRow(item)));
                }
                catch { }
            };

            _openVpnService.LogReceived += log => AppendLog(log);
        }

        private void AppendLog(string log)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            try
            {
                this.BeginInvoke(new Action(() =>
                {
                    if (_txtLog.TextLength > 30000) _txtLog.Clear();
                    _txtLog.AppendText(log + Environment.NewLine);
                }));
            }
            catch { }
        }

        private void UpdateWarpRow(HmaProxyPortItem item)
        {
            foreach (DataGridViewRow row in _gridWarp.Rows)
            {
                var rowItem = row.Tag as HmaProxyPortItem;
                if (rowItem != null && rowItem.Port == item.Port)
                {
                    row.Cells["clStatus"].Value = item.StatusText;
                    row.Cells["clIp"].Value = item.PublicIp;
                    row.Cells["clCountry"].Value = item.Isp;
                    row.Cells["clPing"].Value = item.PingMs > 0 ? item.PingMs + " ms" : "---";
                    row.Cells["clAssigned"].Value = item.AssignedProfileNames.Count > 0 ? string.Join(", ", item.AssignedProfileNames) : "(Chưa gán)";
                    break;
                }
            }
            UpdateWarpSummary();
        }

        private void UpdateOvpnRow(HmaProxyPortItem item)
        {
            foreach (DataGridViewRow row in _gridOvpn.Rows)
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
            UpdateOvpnSummary();
        }

        private void CheckOpenVpnEngine()
        {
            string exe = _openVpnService.FindOpenVpnExecutable();
            if (!string.IsNullOrEmpty(exe))
            {
                _lblOpenVpnStatus.Text = string.Format("✅ OpenVPN: {0}", Path.GetFileName(exe));
                _lblOpenVpnStatus.ForeColor = Color.FromArgb(16, 185, 129);
                _btnGetOpenVpn.Visible = false;
            }
            else
            {
                _lblOpenVpnStatus.Text = "⚠️ Chưa phát hiện OpenVPN.exe (Chạy chế độ Local Bridge)";
                _lblOpenVpnStatus.ForeColor = Color.FromArgb(217, 119, 6);
                _btnGetOpenVpn.Visible = true;
            }
        }

        private void SaveAuthCredentials()
        {
            _openVpnService.Config.Username = _txtUsername.Text.Trim();
            _openVpnService.Config.Password = _txtPassword.Text.Trim();
            _openVpnService.SaveConfig();
            MessageBox.Show("Đã lưu thông tin xác thực OpenVPN thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BrowseOvpnFolder()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục chứa file .ovpn (NordVPN / HMA)";
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtOvpnDir.Text = fbd.SelectedPath;
                    _openVpnService.Config.OvpnDirectory = fbd.SelectedPath;
                    _openVpnService.SaveConfig();
                    _discoveredOvpn = _openVpnService.ScanOvpnFiles(fbd.SelectedPath);
                    if (_discoveredOvpn.Count > 0)
                    {
                        _lblOvpnCount.Text = string.Format("✅ Tìm thấy {0} file cấu hình server", _discoveredOvpn.Count);
                        _lblOvpnCount.ForeColor = Color.FromArgb(16, 185, 129);
                    }
                    RebuildOvpnPorts();
                }
            }
        }

        private void OpenVpnGuide()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://openvpn.net/community-downloads/") { UseShellExecute = true });
            }
            catch { }
        }
        #endregion
    }
}
