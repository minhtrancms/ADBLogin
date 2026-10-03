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
            this.Size = new Size(560, 520);
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
            int labelX = 30;
            int inputX = 150;
            int inputWidth = 350;

            // 1. Tên Profile
            Label lblName = new Label { Text = "Tên Profile:", Location = new Point(labelX, 85), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _txtName = new TextBox { Location = new Point(inputX, 82), Width = inputWidth, Font = new Font("Segoe UI", 10F) };

            // 2. Trình duyệt / Lõi Chrome
            Label lblBrowser = new Label { Text = "Lõi Trình duyệt:", Location = new Point(labelX, 125), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _cboBrowserVersion = new ComboBox
            {
                Location = new Point(inputX, 122),
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

            // 3. Proxy
            Label lblProxy = new Label { Text = "Địa chỉ Proxy:", Location = new Point(labelX, 165), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _txtProxy = new TextBox { Location = new Point(inputX, 162), Width = inputWidth, Font = new Font("Segoe UI", 10F) };
            Label lblProxyHint = new Label 
            { 
                Text = "Định dạng: IP:Port hoặc IP:Port:User:Pass (Để trống nếu dùng Direct)", 
                Location = new Point(inputX, 190), 
                AutoSize = true, 
                ForeColor = Color.FromArgb(100, 116, 139), 
                Font = new Font("Segoe UI", 8F) 
            };

            // 4. User-Agent
            Label lblUA = new Label { Text = "User-Agent:", Location = new Point(labelX, 215), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _txtUserAgent = new TextBox { Location = new Point(inputX, 212), Width = inputWidth, Font = new Font("Segoe UI", 10F) };
            Label lblUAHint = new Label 
            { 
                Text = "Để trống để hệ thống tự động gán User-Agent chuẩn", 
                Location = new Point(inputX, 240), 
                AutoSize = true, 
                ForeColor = Color.FromArgb(100, 116, 139), 
                Font = new Font("Segoe UI", 8F) 
            };

            // 5. Ghi chú
            Label lblNote = new Label { Text = "Ghi chú (Note):", Location = new Point(labelX, 265), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _txtNote = new TextBox 
            { 
                Location = new Point(inputX, 262), 
                Width = inputWidth, 
                Height = 85, 
                Multiline = true, 
                ScrollBars = ScrollBars.Vertical, 
                Font = new Font("Segoe UI", 9.5F) 
            };

            // ================= BUTTONS =================
            _btnSave = new Button
            {
                Text = "✔ Lưu Thay Đổi",
                Size = new Size(140, 40),
                Location = new Point(230, 395),
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
                Size = new Size(110, 40),
                Location = new Point(385, 395),
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
            this.Controls.Add(lblBrowser);
            this.Controls.Add(_cboBrowserVersion);
            this.Controls.Add(lblProxy);
            this.Controls.Add(_txtProxy);
            this.Controls.Add(lblProxyHint);
            this.Controls.Add(lblUA);
            this.Controls.Add(_txtUserAgent);
            this.Controls.Add(lblUAHint);
            this.Controls.Add(lblNote);
            this.Controls.Add(_txtNote);
            this.Controls.Add(_btnSave);
            this.Controls.Add(_btnCancel);
        }

        private void LoadData()
        {
            _txtName.Text = Profile.ProfileName ?? "";
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
