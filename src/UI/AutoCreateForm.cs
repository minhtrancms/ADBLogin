using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;

namespace ADBLogin.UI
{
    public class AutoCreateForm : Form
    {
        private NumericUpDown _numCount;
        private ComboBox _cboBrowserVersion;
        private TextBox _txtPrefix;
        private CheckBox _chkUseFakerEmail;
        private TextBox _txtFolder;
        private Button _btnBrowseFolder;
        private TextBox _txtProxies;
        private Button _btnLoadProxyFile;
        private TextBox _txtNote;
        private ProgressBar _progressBar;
        private Label _lblStatus;
        private Button _btnStart;
        private Button _btnClose;

        private readonly ProfileBuilderService _builderService;
        private readonly AccountManager _accountManager;

        public int CreatedCount { get; private set; }

        public AutoCreateForm(AccountManager accountManager)
        {
            _accountManager = accountManager;
            _builderService = new ProfileBuilderService();
            CreatedCount = 0;

            InitializeComponent();
            LoadDefaultFiles();
        }

        private void InitializeComponent()
        {
            this.Text = "Tự Động Tạo Hàng Loạt Profile (Auto Create)";
            this.Size = new Size(640, 600);
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
                Height = 62,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(20, 15, 20, 15)
            };

            Label lblTitle = new Label
            {
                Text = "🚀 TỰ ĐỘNG KHỞI TẠO PROFILE HÀNG LOẠT",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(18, 18),
                AutoSize = true
            };
            header.Controls.Add(lblTitle);

            int labelX = 25;
            int inputX = 160;
            int inputWidth = 430;

            // 1. Số lượng cần tạo & Phiên bản Chrome
            Label lblCount = new Label { Text = "Số lượng Profile:", Location = new Point(labelX, 85), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
            _numCount = new NumericUpDown { Location = new Point(inputX, 83), Width = 70, Minimum = 1, Maximum = 1000, Value = 5, Font = new Font("Segoe UI", 10F) };

            Label lblBrowser = new Label { Text = "Lõi Chrome:", Location = new Point(inputX + 85, 85), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
            _cboBrowserVersion = new ComboBox
            {
                Location = new Point(inputX + 175, 82),
                Width = 255,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            _cboBrowserVersion.Items.Add("Orbita 144 (Mặc định)");
            var availableBrowsers = BrowserVersionService.GetAvailableBrowsers();
            foreach (var b in availableBrowsers)
            {
                if (b.VersionKey != "custom" && b.DisplayName != "Orbita 144 (Mặc định)")
                {
                    _cboBrowserVersion.Items.Add(b.DisplayName);
                }
            }
            _cboBrowserVersion.SelectedIndex = 0;

            // 2. Thư mục lưu
            Label lblFolder = new Label { Text = "Thư mục lưu:", Location = new Point(labelX, 125), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
            _txtFolder = new TextBox { Text = @"D:\Profiles", Location = new Point(inputX, 123), Width = 330, Font = new Font("Segoe UI", 10F) };
            _btnBrowseFolder = new Button { Text = "Chọn...", Location = new Point(inputX + 340, 121), Width = 88, Height = 29, FlatStyle = FlatStyle.Flat };
            _btnBrowseFolder.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnBrowseFolder.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = _txtFolder.Text;
                    if (fbd.ShowDialog() == DialogResult.OK) _txtFolder.Text = fbd.SelectedPath;
                }
            };

            // 3. Quy tắc đặt tên
            Label lblPrefix = new Label { Text = "Quy tắc đặt tên:", Location = new Point(labelX, 165), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
            _txtPrefix = new TextBox { Text = "Profile_", Location = new Point(inputX, 163), Width = 120, Font = new Font("Segoe UI", 10F) };
            _chkUseFakerEmail = new CheckBox 
            { 
                Text = "Dùng Faker sinh Email ngẫu nhiên (Khuyên dùng)", 
                Location = new Point(inputX + 130, 164), 
                AutoSize = true, 
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(99, 102, 241)
            };

            // 4. Danh sách Proxy
            Label lblProxies = new Label { Text = "Danh sách Proxy:\n(1 dòng / 1 proxy)", Location = new Point(labelX, 205), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
            _txtProxies = new TextBox 
            { 
                Location = new Point(inputX, 203), 
                Width = inputWidth, 
                Height = 150, 
                Multiline = true, 
                ScrollBars = ScrollBars.Vertical, 
                Font = new Font("Consolas", 9.5F) 
            };

            _btnLoadProxyFile = new Button { Text = "📄 Nạp từ Files/Proxy.txt", Location = new Point(inputX, 360), Width = 190, Height = 30, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(51, 65, 85) };
            _btnLoadProxyFile.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnLoadProxyFile.Click += (s, e) => LoadProxyFile();

            // 5. Ghi chú chung
            Label lblNote = new Label { Text = "Ghi chú (Note):", Location = new Point(labelX, 405), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
            _txtNote = new TextBox { Text = "Auto Created", Location = new Point(inputX, 403), Width = inputWidth, Font = new Font("Segoe UI", 10F) };

            // 6. Thanh tiến trình & Trạng thái
            _progressBar = new ProgressBar { Location = new Point(labelX, 450), Width = 565, Height = 18 };
            _lblStatus = new Label { Text = "Sẵn sàng tạo hồ sơ...", Location = new Point(labelX, 475), AutoSize = true, ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 9F, FontStyle.Italic) };

            // 7. Nút Bắt đầu & Đóng
            _btnStart = new Button
            {
                Text = "▶ BẮT ĐẦU TẠO",
                Size = new Size(160, 42),
                Location = new Point(245, 505),
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnStart.FlatAppearance.BorderSize = 0;
            _btnStart.Click += BtnStart_Click;

            _btnClose = new Button
            {
                Text = "Đóng",
                Size = new Size(100, 42),
                Location = new Point(420, 505),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(71, 85, 105),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand
            };
            _btnClose.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(header);
            this.Controls.Add(lblCount);
            this.Controls.Add(_numCount);
            this.Controls.Add(lblBrowser);
            this.Controls.Add(_cboBrowserVersion);
            this.Controls.Add(lblFolder);
            this.Controls.Add(_txtFolder);
            this.Controls.Add(_btnBrowseFolder);
            this.Controls.Add(lblPrefix);
            this.Controls.Add(_txtPrefix);
            this.Controls.Add(_chkUseFakerEmail);
            this.Controls.Add(lblProxies);
            this.Controls.Add(_txtProxies);
            this.Controls.Add(_btnLoadProxyFile);
            this.Controls.Add(lblNote);
            this.Controls.Add(_txtNote);
            this.Controls.Add(_progressBar);
            this.Controls.Add(_lblStatus);
            this.Controls.Add(_btnStart);
            this.Controls.Add(_btnClose);
        }

        private void LoadDefaultFiles()
        {
            LoadProxyFile();
        }

        private void LoadProxyFile()
        {
            string proxyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files", "Proxy.txt");
            if (File.Exists(proxyPath))
            {
                try
                {
                    _txtProxies.Text = File.ReadAllText(proxyPath);
                }
                catch { }
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            int total = (int)_numCount.Value;
            string targetFolder = _txtFolder.Text.Trim();
            if (string.IsNullOrEmpty(targetFolder))
            {
                MessageBox.Show("Vui lòng chọn thư mục lưu profile!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var proxyLines = new List<string>();
            foreach (var line in _txtProxies.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line)) proxyLines.Add(line.Trim());
            }

            var uaLines = new List<string>();
            string uaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files", "UserAgent.txt");
            if (File.Exists(uaPath))
            {
                foreach (var line in File.ReadAllLines(uaPath))
                {
                    if (!string.IsNullOrWhiteSpace(line)) uaLines.Add(line.Trim());
                }
            }

            _btnStart.Enabled = false;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = total;
            _progressBar.Value = 0;

            string selectedBrowserVer = _cboBrowserVersion.SelectedItem != null ? _cboBrowserVersion.SelectedItem.ToString() : "144";

            ThreadPool.QueueUserWorkItem((state) =>
            {
                int success = 0;
                var rand = new Random();

                for (int i = 0; i < total; i++)
                {
                    string profileName;
                    if (_chkUseFakerEmail.Checked)
                    {
                        try
                        {
                            profileName = Faker.User.Email();
                        }
                        catch
                        {
                            profileName = string.Format("user_{0}@gmail.com", Guid.NewGuid().ToString("N").Substring(0, 8));
                        }
                    }
                    else
                    {
                        profileName = string.Format("{0}{1:D3}", _txtPrefix.Text, i + 1);
                    }

                    string proxy = proxyLines.Count > 0 ? proxyLines[i % proxyLines.Count] : "";
                    string ua = uaLines.Count > 0 ? uaLines[rand.Next(uaLines.Count)] : "";
                    string note = _txtNote.Text;

                    try
                    {
                        var newProfile = _builderService.CreateProfile(targetFolder, profileName, proxy, ua, note, selectedBrowserVer);
                        _accountManager.AddOrUpdateProfile(newProfile);
                        success++;
                    }
                    catch { }

                    int currentStep = i + 1;
                    this.Invoke((MethodInvoker)(() =>
                    {
                        _progressBar.Value = currentStep;
                        _lblStatus.Text = string.Format("Đang tạo: {0}/{1} ({2})", currentStep, total, profileName);
                    }));

                    Thread.Sleep(50);
                }

                CreatedCount = success;

                this.Invoke((MethodInvoker)(() =>
                {
                    _btnStart.Enabled = true;
                    _lblStatus.Text = string.Format("🎉 ĐÃ TẠO THÀNH CÔNG {0}/{1} PROFILE!", success, total);
                    MessageBox.Show(string.Format("Đã tạo thành công {0} profile mới tại:\n{1}", success, targetFolder), "Hoàn Tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }));
            });
        }
    }
}
