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
        private Label _lblStatus;

        private Button _btnLaunch;
        private Button _btnStop;
        private Button _btnStopAll;
        private Button _btnAutoCreate;
        private Button _btnCheckProxy;
        private Button _btnCheckAllProxy;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnDelete;
        private Button _btnOpenFolder;
        private Button _btnCopyProxy;
        private Button _btnRefresh;

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
            _accountManager = new AccountManager();
            _launcherService = new BrowserLauncherService();
            _proxyChecker = new ProxyCheckerService();
            _sessionManager = BrowserSessionManager.Instance;

            InitializeForm();
            InitializeContextMenu();
            LoadData();
            InitializeStatusTimer();
        }

        private void InitializeForm()
        {
            this.Text = "ADBLogin v2.0 - Profile Manager [Enterprise Unlimited]";
            this.Size = new Size(1300, 700);
            this.MinimumSize = new Size(980, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(243, 244, 246);

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

            // Ô tìm kiếm siêu gọn góc phải (Tự động co giãn theo mép phải)
            Panel searchBoxPanel = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(1300 - 325, 9),
                Size = new Size(300, 30),
                BackColor = Color.FromArgb(30, 41, 59)
            };

            Label lblSearchIcon = new Label
            {
                Text = "🔍",
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(6, 6),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F)
            };

            _txtSearch = new TextBox
            {
                Location = new Point(28, 6),
                Width = 265,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
            _txtSearch.TextChanged += (s, e) => FilterData();

            searchBoxPanel.Controls.Add(lblSearchIcon);
            searchBoxPanel.Controls.Add(_txtSearch);

            headerPanel.Controls.Add(lblLogo);
            headerPanel.Controls.Add(lblBadge);
            headerPanel.Controls.Add(_lblStatsTotal);
            headerPanel.Controls.Add(_lblStatsRunning);
            headerPanel.Controls.Add(_lblStatsProxy);
            headerPanel.Controls.Add(searchBoxPanel);

            // ================= 2. TOOLBAR PANEL (BỘ NÚT TINH TẾ, CHUẨN WINDOWS 11) =================
            Panel toolPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 43,
                BackColor = Color.White,
                Padding = new Padding(8, 6, 8, 6)
            };

            ToolTip toolTip = new ToolTip();

            // Group 1: Vận hành (Run Control)
            _btnLaunch = CreateCompactButton("▶ Mở", Color.FromArgb(16, 185, 129), Color.White, 68, true, "Khởi chạy các profile đã chọn (Enter)", Color.FromArgb(5, 150, 105));
            _btnLaunch.Click += BtnLaunch_Click;

            _btnStop = CreateCompactButton("⏹ Tắt", Color.FromArgb(239, 68, 68), Color.White, 58, true, "Đóng trình duyệt profile đang chọn", Color.FromArgb(220, 38, 38));
            _btnStop.Click += BtnStop_Click;

            _btnStopAll = CreateCompactButton("Tắt Hết", Color.FromArgb(254, 242, 242), Color.FromArgb(220, 38, 38), 62, false, "Đóng tất cả các trình duyệt đang mở", Color.FromArgb(254, 226, 226));
            _btnStopAll.Click += BtnStopAll_Click;

            // Group 2: Thao tác Profile
            _btnAutoCreate = CreateCompactButton("🚀 Tạo Nhanh", Color.FromArgb(99, 102, 241), Color.White, 88, true, "Tạo tự động hàng loạt profile kèm proxy & user-agent", Color.FromArgb(79, 70, 229));
            _btnAutoCreate.Click += BtnAutoCreate_Click;

            _btnAdd = CreateCompactButton("+ Thêm", Color.FromArgb(37, 99, 235), Color.White, 62, true, "Thêm một profile mới thủ công", Color.FromArgb(29, 78, 216));
            _btnAdd.Click += BtnAdd_Click;

            _btnEdit = CreateCompactButton("✏️ Sửa", Color.FromArgb(248, 250, 252), Color.FromArgb(51, 65, 85), 54, false, "Chỉnh sửa cấu hình profile đang chọn", Color.FromArgb(241, 245, 249));
            _btnEdit.Click += BtnEdit_Click;

            _btnDelete = CreateCompactButton("🗑️ Xóa", Color.FromArgb(255, 241, 242), Color.FromArgb(225, 29, 72), 54, false, "Xóa vĩnh viễn profile đang chọn (Delete)", Color.FromArgb(255, 228, 230));
            _btnDelete.Click += BtnDelete_Click;

            _btnOpenFolder = CreateCompactButton("📁 File", Color.FromArgb(248, 250, 252), Color.FromArgb(51, 65, 85), 54, false, "Mở thư mục lưu trữ profile trên máy", Color.FromArgb(241, 245, 249));
            _btnOpenFolder.Click += BtnOpenFolder_Click;

            // Group 3: Proxy & Công cụ
            _btnCheckProxy = CreateCompactButton("⚡ Check", Color.FromArgb(245, 158, 11), Color.White, 64, true, "Kiểm tra Proxy các profile được chọn", Color.FromArgb(217, 119, 6));
            _btnCheckProxy.Click += BtnCheckProxy_Click;

            _btnCheckAllProxy = CreateCompactButton("Tất Cả", Color.FromArgb(255, 251, 235), Color.FromArgb(180, 83, 9), 55, false, "Kiểm tra Proxy toàn bộ danh sách profile", Color.FromArgb(254, 243, 199));
            _btnCheckAllProxy.Click += BtnCheckAllProxy_Click;

            _btnCopyProxy = CreateCompactButton("📋 Copy", Color.FromArgb(248, 250, 252), Color.FromArgb(51, 65, 85), 56, false, "Sao chép Proxy vào Clipboard", Color.FromArgb(241, 245, 249));
            _btnCopyProxy.Click += BtnCopyProxy_Click;

            _btnRefresh = CreateCompactButton("🔄", Color.FromArgb(248, 250, 252), Color.FromArgb(51, 65, 85), 36, false, "Tải lại danh sách profile (F5)", Color.FromArgb(241, 245, 249));
            _btnRefresh.Click += (s, e) => LoadData();

            // Group 4: Bộ chọn phiên bản Chrome / Orbita
            Label lblBrowser = new Label { Text = "🌐", AutoSize = true, Font = new Font("Segoe UI", 9F) };
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

            // Group 5: Chế độ Mobile & Lưới
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

            toolPanel.Controls.Add(_btnLaunch);
            toolPanel.Controls.Add(_btnStop);
            toolPanel.Controls.Add(_btnStopAll);
            toolPanel.Controls.Add(CreateDivider());
            toolPanel.Controls.Add(_btnAutoCreate);
            toolPanel.Controls.Add(_btnAdd);
            toolPanel.Controls.Add(_btnEdit);
            toolPanel.Controls.Add(_btnDelete);
            toolPanel.Controls.Add(_btnOpenFolder);
            toolPanel.Controls.Add(CreateDivider());
            toolPanel.Controls.Add(_btnCheckProxy);
            toolPanel.Controls.Add(_btnCheckAllProxy);
            toolPanel.Controls.Add(_btnCopyProxy);
            toolPanel.Controls.Add(_btnRefresh);
            toolPanel.Controls.Add(CreateDivider());
            toolPanel.Controls.Add(lblBrowser);
            toolPanel.Controls.Add(_cboBrowserVersion);
            toolPanel.Controls.Add(CreateDivider());
            toolPanel.Controls.Add(_chkMobileMode);
            toolPanel.Controls.Add(lblGridConfig);
            toolPanel.Controls.Add(_numRows);
            toolPanel.Controls.Add(lblRowUnit);
            toolPanel.Controls.Add(_numCols);

            LayoutCompactToolbar(toolPanel);

            // ================= 3. DATAGRIDVIEW (TEXT NHỎ GỌN, DỄ NHÌN) =================
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
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowTemplate = { Height = 34 } // Chiều cao hàng vừa vặn, không quá dày
            };

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

            _grid.Columns.Add("clId", "ID");
            _grid.Columns.Add("clName", "Tên Profile / Email");
            _grid.Columns.Add("clRunningStatus", "Trạng Thái Mở");
            _grid.Columns.Add("clProxy", "Proxy");
            _grid.Columns.Add("clStatusProxy", "Trạng Thái Proxy");
            _grid.Columns.Add("clNote", "Ghi Chú");
            _grid.Columns.Add("clPath", "Thư Mục Dữ Liệu");

            _grid.Columns["clId"].Width = 85;
            _grid.Columns["clName"].Width = 200;
            _grid.Columns["clRunningStatus"].Width = 120;
            _grid.Columns["clProxy"].Width = 145;
            _grid.Columns["clStatusProxy"].Width = 185;
            _grid.Columns["clNote"].Width = 100;

            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnLaunch_Click(null, null); };
            _grid.MouseDown += Grid_MouseDown;
            _grid.KeyDown += Grid_KeyDown;
            _grid.CellPainting += Grid_CellPainting;

            // ================= 4. STATUS BAR =================
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
            this.Controls.Add(toolPanel);
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
                Size = new Size(width, 29),
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
                    c.Location = new Point(currentX, 7);
                    currentX += c.Width + gap;
                }
                else if (c is Panel && c.Width == 1) // Divider
                {
                    c.Location = new Point(currentX + 2, 11);
                    currentX += 7;
                }
                else if (c is CheckBox)
                {
                    c.Location = new Point(currentX + 2, 11);
                    currentX += c.Width + gap + 3;
                }
                else if (c is NumericUpDown)
                {
                    c.Location = new Point(currentX, 10);
                    currentX += c.Width + gap;
                }
                else if (c is Label)
                {
                    c.Location = new Point(currentX, 13);
                    currentX += c.Width + gap;
                }
                else if (c is ComboBox)
                {
                    c.Location = new Point(currentX, 10);
                    currentX += c.Width + gap;
                }
            }
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            string colName = _grid.Columns[e.ColumnIndex].Name;

            if (colName == "clRunningStatus" && e.Value != null)
            {
                e.PaintBackground(e.CellBounds, true);

                string val = e.Value.ToString();
                bool isRunning = val.Contains("ĐANG MỞ");

                Color pillBg = isRunning ? Color.FromArgb(220, 252, 231) : Color.FromArgb(243, 244, 246);
                Color pillBorder = isRunning ? Color.FromArgb(134, 239, 172) : Color.FromArgb(209, 213, 219);
                Color textColor = isRunning ? Color.FromArgb(21, 128, 61) : Color.FromArgb(107, 114, 128);

                DrawPillBadge(e.Graphics, e.CellBounds, val, pillBg, pillBorder, textColor);
                e.Handled = true;
            }
            else if (colName == "clStatusProxy" && e.Value != null)
            {
                e.PaintBackground(e.CellBounds, true);

                string val = e.Value.ToString();
                Color pillBg = Color.FromArgb(243, 244, 246);
                Color pillBorder = Color.FromArgb(229, 231, 235);
                Color textColor = Color.FromArgb(75, 85, 99);

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

                DrawPillBadge(e.Graphics, e.CellBounds, val, pillBg, pillBorder, textColor);
                e.Handled = true;
            }
        }

        private void DrawPillBadge(Graphics g, Rectangle bounds, string text, Color bg, Color border, Color textColor)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int padX = 6;
            int padY = 5;
            Rectangle pillRect = new Rectangle(bounds.X + padX, bounds.Y + padY, bounds.Width - (padX * 2), bounds.Height - (padY * 2));

            using (GraphicsPath path = GetRoundedRectangle(pillRect, 5))
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

            TextRenderer.DrawText(
                g,
                text,
                new Font("Segoe UI", 8F, FontStyle.Bold),
                pillRect,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis
            );
        }

        private GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
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
                string id = _grid.Rows[i].Cells["clId"].Value.ToString();
                bool isRunning = _sessionManager.IsRunning(id);

                if (isRunning)
                {
                    runningCount++;
                    _grid.Rows[i].Cells["clRunningStatus"].Value = "🟢 ĐANG MỞ";
                }
                else
                {
                    _grid.Rows[i].Cells["clRunningStatus"].Value = "⚪ ĐÃ TẮT";
                }
            }

            _lblStatsRunning.Text = string.Format("Đang mở: {0}", runningCount);
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

            _contextMenu.Items.Add(new ToolStripSeparator());

            var itemCheck = _contextMenu.Items.Add("⚡ Kiểm tra Proxy");
            itemCheck.Click += BtnCheckProxy_Click;

            _contextMenu.Items.Add(new ToolStripSeparator());

            var itemEdit = _contextMenu.Items.Add("✏️ Sửa cấu hình");
            itemEdit.Click += BtnEdit_Click;

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

        private void LoadData()
        {
            _accountManager.LoadProfiles();
            FilterData();
            UpdateRunningStatus();
        }

        private void FilterData()
        {
            _grid.Rows.Clear();
            string keyword = _txtSearch.Text.Trim().ToLower();

            int matchCount = 0;
            int proxyCount = 0;

            foreach (var p in _accountManager.Profiles)
            {
                if (!string.IsNullOrEmpty(keyword))
                {
                    bool matchName = (p.ProfileName ?? "").ToLower().Contains(keyword);
                    bool matchProxy = (p.Proxy ?? "").ToLower().Contains(keyword);
                    bool matchNote = (p.Notes ?? "").ToLower().Contains(keyword);
                    bool matchId = (p.ProfileId ?? "").ToLower().Contains(keyword);

                    if (!matchName && !matchProxy && !matchNote && !matchId)
                    {
                        continue;
                    }
                }

                string displayProxy = string.IsNullOrEmpty(p.Proxy) ? "Direct (Mạng gốc)" : p.Proxy;
                if (!string.IsNullOrEmpty(p.Proxy)) proxyCount++;

                string proxyStatus = string.IsNullOrEmpty(p.Proxy) ? "🟢 Direct" : "Chưa kiểm tra";
                string displayPath = string.IsNullOrEmpty(p.BrowserPath) ? "Mặc định" : p.BrowserPath;
                string runningStatus = _sessionManager.IsRunning(p.ProfileId) ? "🟢 ĐANG MỞ" : "⚪ ĐÃ TẮT";

                _grid.Rows.Add(
                    p.ProfileId,
                    p.ProfileName,
                    runningStatus,
                    displayProxy,
                    proxyStatus,
                    p.Notes,
                    displayPath
                );

                matchCount++;
            }

            _lblStatsTotal.Text = string.Format("Tổng: {0}", _accountManager.Profiles.Count);
            _lblStatsProxy.Text = string.Format("Proxy: {0}", proxyCount);
            _lblStatus.Text = string.Format("Hiển thị {0} / {1} profile phù hợp", matchCount, _accountManager.Profiles.Count);
        }

        private List<UserProfile> GetSelectedProfiles()
        {
            var list = new List<UserProfile>();
            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                string id = row.Cells["clId"].Value.ToString();
                var p = _accountManager.GetProfile(id);
                if (p != null) list.Add(p);
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

            _btnCheckAllProxy.Enabled = false;
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
                    _btnCheckAllProxy.Enabled = true;
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

            var profile = selected[0];
            using (var form = new ProfileEditForm(profile))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _accountManager.AddOrUpdateProfile(form.Profile);
                    LoadData();
                    _lblStatus.Text = string.Format("Đã cập nhật profile [{0}]!", profile.ProfileName);
                }
            }
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

        [STAThread]
        public static void Main()
        {
            try
            {
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
