using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;

namespace ADBLogin.UI
{
    public class BatchAccountImporterForm : Form
    {
        private TextBox _txtInput;
        private ComboBox _cboFormat;
        private TextBox _txtPrefix;
        private NumericUpDown _numStartIndex;
        private DataGridView _dgvPreview;
        private Button _btnParse;
        private Button _btnImport;
        private Button _btnClear;
        private Label _lblStatus;

        private readonly List<UserProfile> _parsedProfiles = new List<UserProfile>();
        private readonly AccountManager _accountMgr = AccountManager.Instance;

        public BatchAccountImporterForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "📥 BỘ NHẬP TÀI KHOẢN HÀNG LOẠT CHUYÊN NGHIỆP (BATCH ACCOUNT IMPORTER PRO)";
            this.Size = new Size(1180, 780);
            this.MinimumSize = new Size(1000, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // 1. Header
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(16, 8, 16, 8)
            };

            Label lblTitle = new Label
            {
                Text = "📥 BATCH ACCOUNT & CREDENTIAL IMPORTER PRO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Tự động phân tích đa định dạng UID|Pass|2FA|Email|Cookie|Proxy, tạo hàng loạt Profile trình duyệt sạch và gán Proxy tức thì",
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(16, 32)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // 2. Main Split
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 460,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(229, 231, 235)
            };
            this.Controls.Add(split);
            split.BringToFront();

            // Left: Input controls
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14) };

            Label lblFormat = new Label { Text = "1. Chọn cấu trúc định dạng dòng dữ liệu:", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _cboFormat = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28, Font = new Font("Segoe UI", 9F) };
            _cboFormat.Items.Add("UID | Password | 2FA | Email | PassMail | Cookie | Proxy");
            _cboFormat.Items.Add("UID | Password | 2FA | Email | PassMail");
            _cboFormat.Items.Add("Username | Password | Proxy");
            _cboFormat.Items.Add("ProfileName | Proxy | UserAgent");
            _cboFormat.Items.Add("Proxy Only (IP:Port or IP:Port:User:Pass)");
            _cboFormat.SelectedIndex = 0;

            Panel pnlSettings = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(0, 10, 0, 0) };
            Label lblPrefix = new Label { Text = "Tiền tố tên Profile:", Location = new Point(0, 14), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtPrefix = new TextBox { Text = "Profile_", Location = new Point(125, 12), Width = 140, Font = new Font("Segoe UI", 9F) };

            Label lblStart = new Label { Text = "Bắt đầu từ số:", Location = new Point(280, 14), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numStartIndex = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 1, Location = new Point(375, 12), Width = 65 };

            pnlSettings.Controls.Add(lblPrefix);
            pnlSettings.Controls.Add(_txtPrefix);
            pnlSettings.Controls.Add(lblStart);
            pnlSettings.Controls.Add(_numStartIndex);

            Label lblPaste = new Label { Text = "2. Dán danh sách tài khoản (Mỗi tài khoản 1 dòng):", Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                WordWrap = false
            };

            Panel pnlLeftAction = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(0, 8, 0, 0) };
            _btnParse = new Button
            {
                Text = "🔍 Xem Trước & Phân Tích (Parse)",
                Width = 230,
                Height = 36,
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnParse.FlatAppearance.BorderSize = 0;
            _btnParse.Click += delegate { ParseInputData(); };

            _btnClear = new Button
            {
                Text = "Xóa Trắng",
                Location = new Point(240, 8),
                Width = 90,
                Height = 36,
                FlatStyle = FlatStyle.Flat
            };
            _btnClear.Click += delegate { _txtInput.Clear(); _parsedProfiles.Clear(); _dgvPreview.Rows.Clear(); UpdateStatus(0); };

            pnlLeftAction.Controls.Add(_btnParse);
            pnlLeftAction.Controls.Add(_btnClear);

            pnlLeft.Controls.Add(_txtInput);
            pnlLeft.Controls.Add(pnlLeftAction);
            pnlLeft.Controls.Add(lblPaste);
            pnlLeft.Controls.Add(pnlSettings);
            pnlLeft.Controls.Add(_cboFormat);
            pnlLeft.Controls.Add(lblFormat);
            split.Panel1.Controls.Add(pnlLeft);

            // Right: Preview & Execution
            Panel pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14) };

            Label lblPreview = new Label { Text = "3. Bảng dữ liệu xem trước kết quả chuẩn bị tạo Profile:", Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            _dgvPreview = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Segoe UI", 8.5F)
            };

            _dgvPreview.Columns.Add("Name", "Tên Profile");
            _dgvPreview.Columns.Add("User", "Tài khoản / UID");
            _dgvPreview.Columns.Add("Proxy", "Proxy");
            _dgvPreview.Columns.Add("Notes", "Ghi chú (Pass, 2FA, Email)");

            Panel pnlRightAction = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(0, 8, 0, 0) };

            _btnImport = new Button
            {
                Text = "🚀 BẮT ĐẦU TẠO PROFILE HÀNG LOẠT",
                Dock = DockStyle.Right,
                Width = 280,
                Height = 38,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnImport.FlatAppearance.BorderSize = 0;
            _btnImport.Click += delegate { ExecuteBatchImport(); };

            _lblStatus = new Label
            {
                Text = "Sẵn sàng phân tích dữ liệu",
                Dock = DockStyle.Left,
                AutoSize = true,
                Location = new Point(0, 16),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(107, 114, 128)
            };

            pnlRightAction.Controls.Add(_lblStatus);
            pnlRightAction.Controls.Add(_btnImport);

            pnlRight.Controls.Add(_dgvPreview);
            pnlRight.Controls.Add(pnlRightAction);
            pnlRight.Controls.Add(lblPreview);
            split.Panel2.Controls.Add(pnlRight);
        }

        private void ParseInputData()
        {
            string raw = _txtInput.Text;
            if (string.IsNullOrWhiteSpace(raw))
            {
                MessageBox.Show("Vui lòng dán dữ liệu tài khoản vào ô văn bản!", "Chưa có dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string[] lines = raw.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            _parsedProfiles.Clear();
            _dgvPreview.Rows.Clear();

            int format = _cboFormat.SelectedIndex;
            string prefix = _txtPrefix.Text.Trim();
            int counter = (int)_numStartIndex.Value;

            foreach (var line in lines)
            {
                string tr = line.Trim();
                if (string.IsNullOrEmpty(tr)) continue;

                string[] parts = tr.Split(new char[] { '|', '\t' });
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

                var profile = new UserProfile
                {
                    ProfileId = Guid.NewGuid().ToString("N"),
                    Tier = AccountTier.Unlimited,
                    IsActive = true
                };

                if (format == 0) // UID|Pass|2FA|Email|PassMail|Cookie|Proxy
                {
                    string uid = parts.Length > 0 ? parts[0] : "";
                    string pass = parts.Length > 1 ? parts[1] : "";
                    string twoFa = parts.Length > 2 ? parts[2] : "";
                    string email = parts.Length > 3 ? parts[3] : "";
                    string passMail = parts.Length > 4 ? parts[4] : "";
                    string cookie = parts.Length > 5 ? parts[5] : "";
                    string proxy = parts.Length > 6 ? parts[6] : "";

                    profile.ProfileName = string.Format("{0}{1:D3}_{2}", prefix, counter, uid);
                    profile.Username = uid;
                    profile.Proxy = proxy;
                    profile.Notes = string.Format("Pass: {0} | 2FA: {1} | Mail: {2}:{3} | Cookie: {4}", pass, twoFa, email, passMail, cookie);
                }
                else if (format == 1) // UID|Pass|2FA|Email|PassMail
                {
                    string uid = parts.Length > 0 ? parts[0] : "";
                    string pass = parts.Length > 1 ? parts[1] : "";
                    string twoFa = parts.Length > 2 ? parts[2] : "";
                    string email = parts.Length > 3 ? parts[3] : "";
                    string passMail = parts.Length > 4 ? parts[4] : "";

                    profile.ProfileName = string.Format("{0}{1:D3}_{2}", prefix, counter, uid);
                    profile.Username = uid;
                    profile.Notes = string.Format("Pass: {0} | 2FA: {1} | Mail: {2}:{3}", pass, twoFa, email, passMail);
                }
                else if (format == 2) // Username|Pass|Proxy
                {
                    string u = parts.Length > 0 ? parts[0] : "";
                    string pass = parts.Length > 1 ? parts[1] : "";
                    string prx = parts.Length > 2 ? parts[2] : "";

                    profile.ProfileName = string.Format("{0}{1:D3}_{2}", prefix, counter, u);
                    profile.Username = u;
                    profile.Proxy = prx;
                    profile.Notes = "Pass: " + pass;
                }
                else if (format == 3) // ProfileName|Proxy|UserAgent
                {
                    string pName = parts.Length > 0 ? parts[0] : string.Format("{0}{1:D3}", prefix, counter);
                    string prx = parts.Length > 1 ? parts[1] : "";
                    string ua = parts.Length > 2 ? parts[2] : "";

                    profile.ProfileName = pName;
                    profile.Proxy = prx;
                    profile.UserAgent = ua;
                }
                else // Proxy Only
                {
                    profile.ProfileName = string.Format("{0}{1:D3}", prefix, counter);
                    profile.Proxy = tr;
                }

                _parsedProfiles.Add(profile);
                _dgvPreview.Rows.Add(profile.ProfileName, profile.Username, profile.Proxy, profile.Notes);
                counter++;
            }

            UpdateStatus(_parsedProfiles.Count);
            _btnImport.Enabled = _parsedProfiles.Count > 0;
        }

        private void UpdateStatus(int count)
        {
            _lblStatus.Text = string.Format("Đã phân tích sẵn sàng: {0} profiles", count);
            _lblStatus.ForeColor = count > 0 ? Color.FromArgb(16, 185, 129) : Color.FromArgb(107, 114, 128);
        }

        private void ExecuteBatchImport()
        {
            if (_parsedProfiles.Count == 0) return;

            DialogResult dr = MessageBox.Show(
                string.Format("Bạn có chắc chắn muốn nhập {0} profile vào hệ thống ADBLogin?", _parsedProfiles.Count),
                "Xác nhận tạo Profile hàng loạt",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr != DialogResult.Yes) return;

            int added = 0;
            foreach (var p in _parsedProfiles)
            {
                if (_accountMgr.AddOrUpdateProfile(p))
                {
                    added++;
                }
            }

            MessageBox.Show(
                string.Format("ĐÃ NHẬP THÀNH CÔNG {0} PROFILES VÀO HỆ THỐNG!\nDanh sách profile sẽ tự động cập nhật.", added),
                "Thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
