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
        private readonly HmaMultiProxyService _service = HmaMultiProxyService.Instance;
        private readonly List<UserProfile> _allProfiles;

        // Header controls
        private TextBox _txtUsername;
        private TextBox _txtPassword;
        private CheckBox _chkShowPass;
        private Button _btnSaveCredentials;

        private TextBox _txtOvpnDir;
        private Button _btnBrowseOvpn;
        private Label _lblOvpnCount;

        private NumericUpDown _numStartPort;
        private NumericUpDown _numPortCount;
        private Button _btnInitPorts;
        private Label _lblOpenVpnStatus;
        private Button _btnGetOpenVpn;

        // Action Toolbar
        private Button _btnStartAll;
        private Button _btnStopAll;
        private Button _btnCheckAllIp;
        private Button _btnAssignProfiles;
        private Button _btnCopyList;
        private Button _btnExportFile;

        // DataGrid
        private DataGridView _grid;
        private TextBox _txtLog;
        private Label _lblSummary;

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
            this.Text = "🌐 QUẢN LÝ HMA MULTI-PROXY (OPEN_VPN ĐA CỔNG CỤC BỘ)";
            this.Size = new Size(1100, 720);
            this.MinimumSize = new Size(950, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // ================= 1. HEADER BANNER =================
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(16, 10, 16, 10)
            };

            Label lblTitle = new Label
            {
                Text = "🌐 HMA MULTI-PROXY MANAGER (OPEN_VPN CONCURRENT RUNNER)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Biến tài khoản & file cấu hình HMA .ovpn thành các cổng Proxy cục bộ độc lập (127.0.0.1:10001, 10002...) không làm mất mạng máy tính",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(15, 34)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // ================= 2. CONFIGURATION PANEL =================
            Panel pnlConfig = new Panel
            {
                Dock = DockStyle.Top,
                Height = 175,
                BackColor = Color.White,
                Padding = new Padding(12)
            };

            // Group 1: Tài khoản HMA OpenVPN
            GroupBox grpAuth = new GroupBox
            {
                Text = "1. Tài Khoản OpenVPN HMA",
                Location = new Point(12, 6),
                Size = new Size(340, 158),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            Label lblUser = new Label { Text = "OpenVPN Username:", Location = new Point(12, 22), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _txtUsername = new TextBox { Location = new Point(14, 40), Width = 310, Font = new Font("Segoe UI", 9F) };

            Label lblPass = new Label { Text = "OpenVPN Password:", Location = new Point(12, 68), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _txtPassword = new TextBox { Location = new Point(14, 86), Width = 230, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 9F) };

            _chkShowPass = new CheckBox { Text = "Hiện", Location = new Point(252, 88), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _chkShowPass.CheckedChanged += (s, e) => _txtPassword.UseSystemPasswordChar = !_chkShowPass.Checked;

            _btnSaveCredentials = new Button
            {
                Text = "💾 Lưu Tài Khoản",
                Location = new Point(14, 118),
                Size = new Size(310, 28),
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
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
                Text = "2. Thư Mục Chứa File Cấu Hình (.ovpn)",
                Location = new Point(362, 6),
                Size = new Size(370, 158),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            Label lblDir = new Label { Text = "Đường dẫn thư mục chứa các file .ovpn:", Location = new Point(12, 22), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _txtOvpnDir = new TextBox { Location = new Point(14, 40), Width = 265, Font = new Font("Segoe UI", 9F) };

            _btnBrowseOvpn = new Button
            {
                Text = "📂 Chọn...",
                Location = new Point(285, 38),
                Size = new Size(72, 26),
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F)
            };
            _btnBrowseOvpn.Click += (s, e) => BrowseOvpnFolder();

            _lblOvpnCount = new Label
            {
                Text = "Chưa nạp thư mục cấu hình",
                Location = new Point(14, 74),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            Label lblHintOvpn = new Label
            {
                Text = "💡 Mẹo: Tải gói .ovpn từ HMA (Account > OpenVPN config) và giải nén vào 1 thư mục.",
                Location = new Point(14, 100),
                Size = new Size(345, 45),
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 7.5F)
            };

            grpOvpn.Controls.Add(lblDir);
            grpOvpn.Controls.Add(_txtOvpnDir);
            grpOvpn.Controls.Add(_btnBrowseOvpn);
            grpOvpn.Controls.Add(_lblOvpnCount);
            grpOvpn.Controls.Add(lblHintOvpn);

            // Group 3: Thiết lập dải cổng & OpenVPN
            GroupBox grpPort = new GroupBox
            {
                Text = "3. Thiết Lập Dải Cổng & OpenVPN",
                Location = new Point(742, 6),
                Size = new Size(330, 158),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            Label lblP1 = new Label { Text = "Cổng Bắt Đầu:", Location = new Point(14, 24), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _numStartPort = new NumericUpDown { Location = new Point(14, 42), Width = 110, Minimum = 1024, Maximum = 65530, Value = 10001, Font = new Font("Segoe UI", 9F) };

            Label lblP2 = new Label { Text = "Số Cổng Muốn Mở:", Location = new Point(140, 24), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _numPortCount = new NumericUpDown { Location = new Point(140, 42), Width = 80, Minimum = 1, Maximum = 50, Value = 5, Font = new Font("Segoe UI", 9F) };

            _btnInitPorts = new Button
            {
                Text = "🔄 Nạp",
                Location = new Point(230, 41),
                Size = new Size(85, 26),
                BackColor = Color.FromArgb(224, 231, 255),
                ForeColor = Color.FromArgb(67, 56, 202),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnInitPorts.Click += (s, e) => RebuildPortItems();

            _lblOpenVpnStatus = new Label
            {
                Text = "Đang kiểm tra OpenVPN...",
                Location = new Point(14, 82),
                Size = new Size(300, 30),
                Font = new Font("Segoe UI", 8F)
            };

            _btnGetOpenVpn = new Button
            {
                Text = "📥 Cài OpenVPN Community",
                Location = new Point(14, 118),
                Size = new Size(300, 28),
                BackColor = Color.FromArgb(254, 243, 199),
                ForeColor = Color.FromArgb(146, 64, 14),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnGetOpenVpn.Click += (s, e) => OpenVpnGuide();

            grpPort.Controls.Add(lblP1);
            grpPort.Controls.Add(_numStartPort);
            grpPort.Controls.Add(lblP2);
            grpPort.Controls.Add(_numPortCount);
            grpPort.Controls.Add(_btnInitPorts);
            grpPort.Controls.Add(_lblOpenVpnStatus);
            grpPort.Controls.Add(_btnGetOpenVpn);

            pnlConfig.Controls.Add(grpAuth);
            pnlConfig.Controls.Add(grpOvpn);
            pnlConfig.Controls.Add(grpPort);
            this.Controls.Add(pnlConfig);

            // ================= 3. ACTION TOOLBAR =================
            Panel pnlActions = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(12, 8, 12, 8)
            };

            _btnStartAll = CreateActionButton("▶️ Khởi Động Tất Cả", Color.FromArgb(16, 185, 129), Color.White, 150);
            _btnStartAll.Click += async (s, e) => await StartAllPortsAsync();

            _btnStopAll = CreateActionButton("⏹️ Dừng Tất Cả", Color.FromArgb(239, 68, 68), Color.White, 120);
            _btnStopAll.Click += (s, e) => StopAllPorts();

            _btnCheckAllIp = CreateActionButton("🔄 Kiểm Tra IP & Ping", Color.FromArgb(14, 165, 233), Color.White, 150);
            _btnCheckAllIp.Click += async (s, e) => await CheckAllPortsIpAsync();

            _btnAssignProfiles = CreateActionButton("⚡ Gán Vào Profile", Color.FromArgb(99, 102, 241), Color.White, 150);
            _btnAssignProfiles.Click += (s, e) => ShowAssignDialog();

            _btnCopyList = CreateActionButton("📋 Copy Danh Sách", Color.FromArgb(71, 85, 105), Color.White, 135);
            _btnCopyList.Click += (s, e) => CopyProxyList();

            _btnExportFile = CreateActionButton("💾 Xuất Proxy.txt", Color.FromArgb(71, 85, 105), Color.White, 130);
            _btnExportFile.Click += (s, e) => ExportProxyFile();

            pnlActions.Controls.Add(_btnStartAll);
            pnlActions.Controls.Add(_btnStopAll);
            pnlActions.Controls.Add(_btnCheckAllIp);
            pnlActions.Controls.Add(_btnAssignProfiles);
            pnlActions.Controls.Add(_btnCopyList);
            pnlActions.Controls.Add(_btnExportFile);

            LayoutActionButtons(pnlActions);
            pnlActions.Resize += (s, e) => LayoutActionButtons(pnlActions);
            this.Controls.Add(pnlActions);

            // ================= 4. STATUS SUMMARY & LOG PANEL =================
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 130,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(12, 6, 12, 6)
            };

            _lblSummary = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Text = "⚡ Tổng cộng: 0 cổng | Đang chạy: 0 | Profile đã gán: 0"
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
            pnlBottom.Controls.Add(_lblSummary);
            this.Controls.Add(pnlBottom);

            // ================= 5. DATAGRIDVIEW =================
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
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                RowTemplate = { Height = 32 },
                EnableHeadersVisualStyles = false
            };

            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(71, 85, 105);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _grid.ColumnHeadersHeight = 32;

            _grid.Columns.Add("clPort", "Cổng Local (Proxy)");
            _grid.Columns.Add("clServer", "Máy Chủ HMA / Cấu Hình");
            _grid.Columns.Add("clStatus", "Trạng Thái");
            _grid.Columns.Add("clIp", "Public IP");
            _grid.Columns.Add("clCountry", "Quốc Gia / Thành Phố");
            _grid.Columns.Add("clPing", "Độ Trễ");
            _grid.Columns.Add("clAssigned", "Profile Đang Gán");

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "clAction",
                HeaderText = "Thao Tác",
                Text = "Bật / Tắt",
                UseColumnTextForButtonValue = true,
                Width = 90
            };
            _grid.Columns.Add(btnCol);

            _grid.Columns["clPort"].Width = 140;
            _grid.Columns["clServer"].Width = 220;
            _grid.Columns["clStatus"].Width = 130;
            _grid.Columns["clIp"].Width = 130;
            _grid.Columns["clCountry"].Width = 170;
            _grid.Columns["clPing"].Width = 80;
            _grid.Columns["clAssigned"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CellPainting += Grid_CellPainting;

            this.Controls.Add(_grid);
            _grid.BringToFront();
        }

        private Button CreateActionButton(string text, Color bg, Color fg, int width)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 32,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void LayoutActionButtons(Panel panel)
        {
            int x = 12;
            foreach (Control c in panel.Controls)
            {
                if (c is Button)
                {
                    c.Location = new Point(x, 8);
                    x += c.Width + 8;
                }
            }
        }

        private void LoadFormConfig()
        {
            _txtUsername.Text = _service.Config.Username ?? string.Empty;
            _txtPassword.Text = _service.Config.Password ?? string.Empty;
            _txtOvpnDir.Text = _service.Config.OvpnDirectory ?? string.Empty;

            if (_service.Config.StartPort >= 1024) _numStartPort.Value = _service.Config.StartPort;
            if (_service.Config.PortCount >= 1) _numPortCount.Value = _service.Config.PortCount;

            CheckOpenVpnEngine();

            if (!string.IsNullOrEmpty(_txtOvpnDir.Text) && Directory.Exists(_txtOvpnDir.Text))
            {
                ScanAndCountOvpn();
            }

            RebuildPortItems();
        }

        private void WireEvents()
        {
            _service.PortStatusChanged += item =>
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                try
                {
                    this.BeginInvoke(new Action(() => UpdateRow(item)));
                }
                catch { }
            };

            _service.LogReceived += log =>
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
            };
        }

        private void CheckOpenVpnEngine()
        {
            string exe = _service.FindOpenVpnExecutable();
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
            _service.Config.Username = _txtUsername.Text.Trim();
            _service.Config.Password = _txtPassword.Text.Trim();
            _service.SaveConfig();
            MessageBox.Show("Đã lưu thông tin xác thực HMA OpenVPN thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BrowseOvpnFolder()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục chứa các file .ovpn của HMA VPN";
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtOvpnDir.Text = fbd.SelectedPath;
                    _service.Config.OvpnDirectory = fbd.SelectedPath;
                    _service.SaveConfig();
                    ScanAndCountOvpn();
                    RebuildPortItems();
                }
            }
        }

        private void ScanAndCountOvpn()
        {
            _discoveredOvpn = _service.ScanOvpnFiles(_txtOvpnDir.Text);
            if (_discoveredOvpn.Count > 0)
            {
                _lblOvpnCount.Text = string.Format("✅ Tìm thấy {0} file cấu hình server HMA", _discoveredOvpn.Count);
                _lblOvpnCount.ForeColor = Color.FromArgb(16, 185, 129);
            }
            else
            {
                _lblOvpnCount.Text = "⚠️ Không tìm thấy file .ovpn nào trong thư mục này";
                _lblOvpnCount.ForeColor = Color.FromArgb(217, 119, 6);
            }
        }

        private void RebuildPortItems()
        {
            int startPort = (int)_numStartPort.Value;
            int count = (int)_numPortCount.Value;

            _service.Config.StartPort = startPort;
            _service.Config.PortCount = count;
            _service.SaveConfig();

            var ovpnPaths = _discoveredOvpn.Select(o => o.Key).ToList();
            _service.InitializePorts(startPort, count, ovpnPaths);

            RefreshGrid();
        }

        private void RefreshGrid()
        {
            _grid.Rows.Clear();
            foreach (var item in _service.PortItems)
            {
                int rIdx = _grid.Rows.Add();
                var row = _grid.Rows[rIdx];
                row.Tag = item;
                row.Cells["clPort"].Value = item.ProxyAddress;
                row.Cells["clServer"].Value = item.ServerName;
                row.Cells["clStatus"].Value = item.StatusText;
                row.Cells["clIp"].Value = item.PublicIp;
                row.Cells["clCountry"].Value = !string.IsNullOrEmpty(item.City) && item.City != "---" ? string.Format("{0} ({1})", item.Country, item.City) : item.Country;
                row.Cells["clPing"].Value = item.PingMs > 0 ? item.PingMs + " ms" : "---";
                row.Cells["clAssigned"].Value = item.AssignedProfileNames.Count > 0 ? string.Join(", ", item.AssignedProfileNames) : "(Chưa gán)";
            }

            UpdateSummary();
        }

        private void UpdateRow(HmaProxyPortItem item)
        {
            foreach (DataGridViewRow row in _grid.Rows)
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

        private void UpdateSummary()
        {
            int total = _service.PortItems.Count;
            int running = _service.PortItems.Count(p => p.Status == HmaTunnelStatus.Connected);
            int assignedCount = _service.PortItems.Sum(p => p.AssignedProfileNames.Count);

            _lblSummary.Text = string.Format("⚡ Tổng cộng: {0} cổng | Đang chạy: {1} cổng | Profile đã gán: {2}", total, running, assignedCount);
        }

        private async void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _grid.Columns["clAction"].Index)
            {
                var row = _grid.Rows[e.RowIndex];
                var item = row.Tag as HmaProxyPortItem;
                if (item == null) return;

                if (item.Status == HmaTunnelStatus.Connected || item.Status == HmaTunnelStatus.Starting)
                {
                    _service.StopPort(item.Port);
                }
                else
                {
                    await _service.StartPortAsync(item.Port);
                }
            }
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _grid.Columns["clStatus"].Index && e.Value != null)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                string text = e.Value.ToString();
                Color bgColor = Color.FromArgb(241, 245, 249);
                Color fgColor = Color.FromArgb(71, 85, 105);

                if (text.Contains("🟢") || text.Contains("LIVE") || text.Contains("VPN") || text.Contains("Local"))
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

        private async Task StartAllPortsAsync()
        {
            _btnStartAll.Enabled = false;
            try
            {
                await _service.StartAllAsync();
            }
            finally
            {
                _btnStartAll.Enabled = true;
            }
        }

        private void StopAllPorts()
        {
            _service.StopAll();
        }

        private async Task CheckAllPortsIpAsync()
        {
            _btnCheckAllIp.Enabled = false;
            try
            {
                var tasks = _service.PortItems.Where(p => p.Status == HmaTunnelStatus.Connected)
                    .Select(p => _service.CheckPortPublicIpAsync(p)).ToList();
                await Task.WhenAll(tasks);
            }
            finally
            {
                _btnCheckAllIp.Enabled = true;
            }
        }

        private void ShowAssignDialog()
        {
            if (_service.PortItems.Count == 0)
            {
                MessageBox.Show("Vui lòng khởi tạo các cổng proxy trước khi gán!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dialog = new Form
            {
                Text = "⚡ GÁN PROXY HMA VÀO PROFILES",
                Size = new Size(540, 480),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            var lblPrompt = new Label
            {
                Text = "Chọn danh sách profile bạn muốn tự động gán dải cổng Proxy HMA:",
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

            var btnConfirm = new Button
            {
                Text = "✔ Thực Hiện Gán Ngay",
                Location = new Point(180, 390),
                Size = new Size(180, 36),
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            btnConfirm.Click += (s, e) =>
            {
                var selected = new List<UserProfile>();
                for (int i = 0; i < chkList.Items.Count; i++)
                {
                    if (chkList.GetItemChecked(i))
                    {
                        selected.Add(profiles[i]);
                    }
                }

                if (selected.Count == 0)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất 1 profile!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int count = _service.AssignProxiesToProfiles(selected);
                MessageBox.Show(string.Format("Đã gán thành công dải Proxy HMA vào {0} profile!", count), "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
                RefreshGrid();
            };

            dialog.Controls.Add(lblPrompt);
            dialog.Controls.Add(chkSelectAll);
            dialog.Controls.Add(chkList);
            dialog.Controls.Add(btnConfirm);

            dialog.ShowDialog(this);
        }

        private void CopyProxyList()
        {
            var lines = _service.PortItems.Select(p => p.ProxyAddress).ToList();
            if (lines.Count > 0)
            {
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
                MessageBox.Show(string.Format("Đã sao chép {0} địa chỉ Proxy (127.0.0.1:port) vào Clipboard!", lines.Count), "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportProxyFile()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Text Files (*.txt)|*.txt";
                sfd.FileName = "HMA_Proxy_List.txt";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    var lines = _service.PortItems.Select(p => p.ProxyAddress).ToList();
                    File.WriteAllLines(sfd.FileName, lines);
                    MessageBox.Show("Đã xuất danh sách proxy ra file thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void OpenVpnGuide()
        {
            string msg = "Hệ thống cần OpenVPN Community (để chạy driver Wintun tạo card mạng ảo độc lập cho từng cổng).\n\n" +
                         "Bạn có muốn mở trang tải OpenVPN chính thức không?\n" +
                         "(Tải bản OpenVPN Windows 64-bit MSI và cài đặt bình thường)";

            if (MessageBox.Show(msg, "Cài đặt OpenVPN Community", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://openvpn.net/community-downloads/") { UseShellExecute = true });
                }
                catch { }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            // Dọn dẹp tài nguyên nếu người dùng đóng hẳn
        }
    }
}
