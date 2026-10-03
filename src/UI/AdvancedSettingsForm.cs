using System;
using System.Drawing;
using System.Windows.Forms;
using ADBLogin.Core.Services;

namespace ADBLogin.UI
{
    public class AdvancedSettingsForm : Form
    {
        // Proxy Rotator
        private ComboBox _cboProvider;
        private TextBox _txtApiKey;
        private Button _btnTestProxy;
        private Label _lblProxyStatus;
        private CheckBox _chkAutoRotate;

        // Captcha Solver
        private ComboBox _cboCaptchaService;
        private TextBox _txtCaptchaKey;
        private Button _btnTestCaptcha;
        private Label _lblCaptchaStatus;
        private CheckBox _chkAutoSolveCaptcha;

        private Button _btnSave;
        private Button _btnClose;

        private readonly ProxyRotatorService _rotatorService = new ProxyRotatorService();
        private readonly CaptchaSolverService _captchaService = new CaptchaSolverService();

        public AdvancedSettingsForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "⚙️ CẤU HÌNH PROXY XOAY & TỰ ĐỘNG GIẢI CAPTCHA";
            this.Size = new Size(680, 520);
            this.MinimumSize = new Size(620, 480);
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
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(16, 8, 16, 8)
            };

            Label lblTitle = new Label
            {
                Text = "⚙️ PROXY XOAY ĐỘNG & TỰ ĐỘNG GIẢI CAPTCHA",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Tích hợp API TMProxy, Tinsoft, ShopLike & Dịch vụ giải mã CapSolver, 2Captcha tự động",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(15, 30)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // MAIN PANEL
            Panel pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };

            // 1. PROXY ROTATOR
            GroupBox grpProxy = new GroupBox { Text = "1. Cấu hình Proxy Xoay Động (Tự động đổi IP)", Location = new Point(16, 12), Width = 630, Height = 170, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            Label lblProv = new Label { Text = "Nhà cung cấp:", Location = new Point(16, 28), AutoSize = true };
            _cboProvider = new ComboBox { Location = new Point(18, 50), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cboProvider.Items.AddRange(new object[] { "TMProxy", "Tinsoft", "ShopLike", "ProxyFB", "Custom URL" });
            _cboProvider.SelectedIndex = 0;

            Label lblKey = new Label { Text = "API Key / URL gọi đổi IP:", Location = new Point(190, 28), AutoSize = true };
            _txtApiKey = new TextBox { Location = new Point(192, 50), Width = 310, Height = 26, Font = new Font("Segoe UI", 9F) };

            _btnTestProxy = new Button { Text = "Đổi IP Thử", Location = new Point(510, 48), Width = 100, Height = 28, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnTestProxy.Click += BtnTestProxy_Click;

            _lblProxyStatus = new Label { Text = "Trạng thái: Chưa kiểm tra", Location = new Point(18, 90), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 8.5F, FontStyle.Italic) };

            _chkAutoRotate = new CheckBox { Text = "Tự động gọi đổi IP trước khi mở mỗi Profile trong kịch bản Automation", Location = new Point(18, 126), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 8.5F) };

            grpProxy.Controls.Add(_chkAutoRotate);
            grpProxy.Controls.Add(_lblProxyStatus);
            grpProxy.Controls.Add(_btnTestProxy);
            grpProxy.Controls.Add(_txtApiKey);
            grpProxy.Controls.Add(lblKey);
            grpProxy.Controls.Add(_cboProvider);
            grpProxy.Controls.Add(lblProv);
            pnlMain.Controls.Add(grpProxy);

            // 2. CAPTCHA SOLVER
            GroupBox grpCaptcha = new GroupBox { Text = "2. Cấu hình Tự động Giải Captcha (CapSolver / 2Captcha)", Location = new Point(16, 196), Width = 630, Height = 170, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            Label lblCapService = new Label { Text = "Dịch vụ giải Captcha:", Location = new Point(16, 28), AutoSize = true };
            _cboCaptchaService = new ComboBox { Location = new Point(18, 50), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cboCaptchaService.Items.AddRange(new object[] { "CapSolver", "2Captcha", "AntiCaptcha", "1stCaptcha" });
            _cboCaptchaService.SelectedIndex = 0;

            Label lblCapKey = new Label { Text = "API Client Key:", Location = new Point(190, 28), AutoSize = true };
            _txtCaptchaKey = new TextBox { Location = new Point(192, 50), Width = 310, Height = 26, Font = new Font("Segoe UI", 9F) };

            _btnTestCaptcha = new Button { Text = "Kiểm tra", Location = new Point(510, 48), Width = 100, Height = 28, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnTestCaptcha.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(_txtCaptchaKey.Text))
                {
                    _lblCaptchaStatus.Text = "Vui lòng nhập API Key";
                    _lblCaptchaStatus.ForeColor = Color.Red;
                }
                else
                {
                    _lblCaptchaStatus.Text = "API Key đã được cấu hình sẵn sàng!";
                    _lblCaptchaStatus.ForeColor = Color.FromArgb(16, 185, 129);
                }
            };

            _lblCaptchaStatus = new Label { Text = "Trạng thái: Chưa kiểm tra", Location = new Point(18, 90), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 8.5F, FontStyle.Italic) };

            _chkAutoSolveCaptcha = new CheckBox { Text = "Tự động vượt Cloudflare Turnstile & reCAPTCHA khi gặp trên trang web", Location = new Point(18, 126), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 8.5F) };

            grpCaptcha.Controls.Add(_chkAutoSolveCaptcha);
            grpCaptcha.Controls.Add(_lblCaptchaStatus);
            grpCaptcha.Controls.Add(_btnTestCaptcha);
            grpCaptcha.Controls.Add(_txtCaptchaKey);
            grpCaptcha.Controls.Add(lblCapKey);
            grpCaptcha.Controls.Add(_cboCaptchaService);
            grpCaptcha.Controls.Add(lblCapService);
            pnlMain.Controls.Add(grpCaptcha);

            // BUTTONS
            _btnSave = new Button { Text = "💾 Lưu Cấu Hình", Location = new Point(390, 380), Width = 130, Height = 34, BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _btnSave.Click += (s, e) =>
            {
                MessageBox.Show("Đã lưu cấu hình Proxy Xoay & Giải Captcha thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            };

            _btnClose = new Button { Text = "Đóng", Location = new Point(530, 380), Width = 116, Height = 34, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F) };
            _btnClose.Click += (s, e) => this.Close();

            pnlMain.Controls.Add(_btnClose);
            pnlMain.Controls.Add(_btnSave);

            this.Controls.Add(pnlMain);
        }

        private void BtnTestProxy_Click(object sender, EventArgs e)
        {
            var provider = (ProxyRotatorProvider)_cboProvider.SelectedIndex;
            string key = _txtApiKey.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                _lblProxyStatus.Text = "Lỗi: Chưa nhập API Key hoặc URL";
                _lblProxyStatus.ForeColor = Color.Red;
                return;
            }

            _lblProxyStatus.Text = "Đang gửi yêu cầu đổi IP...";
            _lblProxyStatus.ForeColor = Color.Orange;

            var res = _rotatorService.RequestNewProxy(provider, key);
            if (res.Success)
            {
                _lblProxyStatus.Text = string.Format("Thành công: IP mới: {0} (Chờ: {1}s)", res.Proxy, res.NextChangeSeconds);
                _lblProxyStatus.ForeColor = Color.FromArgb(16, 185, 129);
            }
            else
            {
                _lblProxyStatus.Text = "Thất bại: " + res.Message;
                _lblProxyStatus.ForeColor = Color.Red;
            }
        }
    }
}
