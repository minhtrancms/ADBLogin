using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ADBLogin.Core.Models;

namespace ADBLogin.UI
{
    public class ProfileEditForm : Form
    {
        public UserProfile Profile { get; private set; }

        private TextBox _txtName;
        private TextBox _txtTags;
        private TextBox _txtProxy;
        private TextBox _txtNote;
        private TextBox _txtUserAgent;
        private ComboBox _cboBrowserVersion;
        private Button _btnSave;
        private Button _btnCancel;

        public ProfileEditForm(UserProfile profile = null)
        {
            Profile = profile != null ? profile : new UserProfile();
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            bool isNew = Profile.ProfileId == "DEFAULT_ROOT" || string.IsNullOrEmpty(Profile.ProfileName);
            this.Text = isNew ? "Tạo Hồ Sơ Mới" : "Chỉnh Sửa - " + Profile.ProfileName;
            this.Size = new Size(580, 545);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

            // ================= HEADER BANNER =================
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(20, 15, 20, 15)
            };

            Label lblTitle = new Label
            {
                Text = isNew ? "➕ TẠO HỒ SƠ PROFILE MỚI" : "✏️ CẬP NHẬT CẤU HÌNH PROFILE",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                Location = new Point(18, 18),
                AutoSize = true
            };
            header.Controls.Add(lblTitle);

            // ================= INPUT FIELDS =================
            int labelX = 25;
            int inputX = 145;
            int inputWidth = 385;

            // 1. Tên Profile
            Label lblName = new Label { Text = "Tên Profile:", Location = new Point(labelX, 78), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _txtName = new TextBox { Location = new Point(inputX, 75), Width = inputWidth, Font = new Font("Segoe UI", 10F) };

            // 2. Nhãn (Tags) & Nút gắn nhãn nhanh
            Label lblTags = new Label { Text = "🏷️ Nhãn (Tags):", Location = new Point(labelX, 114), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _txtTags = new TextBox { Location = new Point(inputX, 111), Width = inputWidth, Font = new Font("Segoe UI", 9.5F) };

            FlowLayoutPanel pnlQuickTags = new FlowLayoutPanel
            {
                Location = new Point(inputX, 140),
                Width = inputWidth,
                Height = 28,
                Margin = new Padding(0),
                Padding = new Padding(0),
                WrapContents = false,
                AutoScroll = false
            };

            string[] quickTagList = new string[] { "Facebook", "Shopee", "TikTok", "Google", "Nuôi nick", "Cày Xu", "Airdrop", "Acc Chính" };
            foreach (var qTag in quickTagList)
            {
                string tagValue = qTag;
                Button btnTag = new Button
                {
                    Text = "+" + tagValue,
                    AutoSize = true,
                    Height = 24,
                    BackColor = Color.FromArgb(241, 245, 249),
                    ForeColor = Color.FromArgb(51, 65, 85),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8F),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 0, 4, 0)
                };
                btnTag.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
                btnTag.Click += (s, e) => AddQuickTag(tagValue);
                pnlQuickTags.Controls.Add(btnTag);
            }

            // 3. Trình duyệt / Lõi Chrome
            Label lblBrowser = new Label { Text = "Lõi Trình duyệt:", Location = new Point(labelX, 178), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _cboBrowserVersion = new ComboBox
            {
                Location = new Point(inputX, 175),
                Width = inputWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5F)
            };
            _cboBrowserVersion.Items.Add("Mặc định theo hệ thống (Toolbar)");
            var availableBrowsers = ADBLogin.Core.Services.BrowserVersionService.GetAvailableBrowsers();
            foreach (var b in availableBrowsers)
            {
                if (b.VersionKey != "custom") _cboBrowserVersion.Items.Add(b.DisplayName);
            }
            _cboBrowserVersion.Items.Add("📁 Chọn file thực thi khác (.exe)...");
            _cboBrowserVersion.SelectedIndex = 0;

            // 4. Proxy
            Label lblProxy = new Label { Text = "Địa chỉ Proxy:", Location = new Point(labelX, 218), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _txtProxy = new TextBox { Location = new Point(inputX, 215), Width = inputWidth - 105, Font = new Font("Segoe UI", 10F) };

            Button btnTestProxy = new Button
            {
                Text = "🔍 Test Proxy",
                Location = new Point(inputX + inputWidth - 100, 214),
                Width = 100,
                Height = 28,
                BackColor = Color.FromArgb(14, 165, 233),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTestProxy.FlatAppearance.BorderSize = 0;

            Label lblProxyHint = new Label 
            { 
                Text = "Định dạng: IP:Port hoặc IP:Port:User:Pass (Để trống nếu dùng Direct)", 
                Location = new Point(inputX, 245), 
                AutoSize = true, 
                ForeColor = Color.FromArgb(100, 116, 139), 
                Font = new Font("Segoe UI", 8F) 
            };

            btnTestProxy.Click += (s, e) =>
            {
                string pxy = _txtProxy.Text.Trim();
                if (string.IsNullOrEmpty(pxy))
                {
                    lblProxyHint.Text = "🟢 Direct IP (Không dùng Proxy - Mạng gốc máy tính)";
                    lblProxyHint.ForeColor = Color.FromArgb(16, 185, 129);
                    return;
                }

                btnTestProxy.Enabled = false;
                lblProxyHint.Text = "⏳ Đang kết nối kiểm tra Proxy...";
                lblProxyHint.ForeColor = Color.FromArgb(217, 119, 6);

                System.Threading.ThreadPool.QueueUserWorkItem((state) =>
                {
                    var checker = new ADBLogin.Core.Services.ProxyCheckerService();
                    var result = checker.CheckProxyString(pxy);
                    this.Invoke((MethodInvoker)(() =>
                    {
                        btnTestProxy.Enabled = true;
                        if (result.IsLive)
                        {
                            lblProxyHint.Text = string.Format("✅ Proxy Hoạt Động! IP: {0} | Ping: {1}ms", result.ExternalIp, result.PingMs);
                            lblProxyHint.ForeColor = Color.FromArgb(16, 185, 129);
                        }
                        else
                        {
                            lblProxyHint.Text = string.Format("❌ Kết nối thất bại: {0}", string.IsNullOrEmpty(result.Message) ? "Không thể kết nối" : result.Message);
                            lblProxyHint.ForeColor = Color.FromArgb(239, 68, 68);
                        }
                    }));
                });
            };

            // 5. User-Agent
            Label lblUA = new Label { Text = "User-Agent:", Location = new Point(labelX, 268), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _txtUserAgent = new TextBox { Location = new Point(inputX, 265), Width = inputWidth, Font = new Font("Segoe UI", 10F) };
            Label lblUAHint = new Label 
            { 
                Text = "Để trống để hệ thống tự động gán User-Agent chuẩn", 
                Location = new Point(inputX, 293), 
                AutoSize = true, 
                ForeColor = Color.FromArgb(100, 116, 139), 
                Font = new Font("Segoe UI", 8F) 
            };

            // 6. Ghi chú
            Label lblNote = new Label { Text = "Ghi chú (Note):", Location = new Point(labelX, 318), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _txtNote = new TextBox 
            { 
                Location = new Point(inputX, 315), 
                Width = inputWidth, 
                Height = 70, 
                Multiline = true, 
                ScrollBars = ScrollBars.Vertical, 
                Font = new Font("Segoe UI", 9.5F) 
            };

            // ================= BUTTONS =================
            _btnSave = new Button
            {
                Text = "✔ Lưu Thay Đổi",
                Size = new Size(140, 38),
                Location = new Point(230, 435),
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new Button
            {
                Text = "Hủy Bỏ",
                Size = new Size(110, 38),
                Location = new Point(385, 435),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(71, 85, 105),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(header);
            this.Controls.Add(lblName);
            this.Controls.Add(_txtName);
            this.Controls.Add(lblTags);
            this.Controls.Add(_txtTags);
            this.Controls.Add(pnlQuickTags);
            this.Controls.Add(lblBrowser);
            this.Controls.Add(_cboBrowserVersion);
            this.Controls.Add(lblProxy);
            this.Controls.Add(_txtProxy);
            this.Controls.Add(btnTestProxy);
            this.Controls.Add(lblProxyHint);
            this.Controls.Add(lblUA);
            this.Controls.Add(_txtUserAgent);
            this.Controls.Add(lblUAHint);
            this.Controls.Add(lblNote);
            this.Controls.Add(_txtNote);
            this.Controls.Add(_btnSave);
            this.Controls.Add(_btnCancel);
        }

        private void AddQuickTag(string tag)
        {
            string current = (_txtTags.Text ?? "").Trim();
            if (string.IsNullOrEmpty(current))
            {
                _txtTags.Text = tag;
            }
            else
            {
                string[] parts = current.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                bool exists = false;
                foreach (var p in parts)
                {
                    if (p.Trim().Equals(tag, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    _txtTags.Text = current + ", " + tag;
                }
            }
            _txtTags.SelectionStart = _txtTags.Text.Length;
        }

        private void LoadData()
        {
            _txtName.Text = Profile.ProfileName ?? "";
            _txtTags.Text = Profile.Tags ?? "";
            _txtProxy.Text = Profile.Proxy ?? "";
            _txtUserAgent.Text = Profile.UserAgent ?? "";
            _txtNote.Text = Profile.Notes ?? "";

            if (!string.IsNullOrEmpty(Profile.BrowserVersion))
            {
                for (int i = 0; i < _cboBrowserVersion.Items.Count; i++)
                {
                    if (_cboBrowserVersion.Items[i].ToString().IndexOf(Profile.BrowserVersion, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _cboBrowserVersion.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Vui lòng nhập tên cho Profile!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            Profile.ProfileName = _txtName.Text.Trim();
            Profile.Tags = _txtTags.Text.Trim();
            Profile.Proxy = _txtProxy.Text.Trim();
            Profile.UserAgent = _txtUserAgent.Text.Trim();
            Profile.Notes = _txtNote.Text.Trim();

            if (_cboBrowserVersion.SelectedIndex > 0)
            {
                string sel = _cboBrowserVersion.SelectedItem.ToString();
                if (sel.Contains("Chọn file"))
                {
                    using (var ofd = new OpenFileDialog())
                    {
                        ofd.Title = "Chọn file thực thi Chrome / Orbita (chrome.exe)";
                        ofd.Filter = "Trình duyệt (*.exe)|*.exe|Tất cả tệp (*.*)|*.*";
                        if (ofd.ShowDialog() == DialogResult.OK)
                        {
                            Profile.BrowserPath = ofd.FileName;
                            Profile.BrowserVersion = Path.GetFileName(ofd.FileName);
                        }
                    }
                }
                else
                {
                    Profile.BrowserVersion = sel;
                }
            }
            else
            {
                Profile.BrowserVersion = string.Empty;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
