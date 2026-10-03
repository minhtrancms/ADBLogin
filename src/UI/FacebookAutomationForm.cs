using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADBLogin.Core.Automation;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;
using OpenQA.Selenium;

namespace ADBLogin.UI
{
    public class FacebookAutomationForm : Form
    {
        private CheckedListBox _chkListProfiles;
        private TextBox _txtSearch;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRunning;

        // Tab 1: Nuoi nick
        private CheckBox _chkSurfFeed;
        private NumericUpDown _numFeedDuration;
        private CheckBox _chkAutoLike;
        private NumericUpDown _numMaxLikes;
        private CheckBox _chkWatchReels;
        private NumericUpDown _numReelCount;
        private CheckBox _chkCheckNotif;

        // Tab 2: Dang nhap & Quan ly acc
        private RadioButton _radLoginCookie;
        private RadioButton _radLoginCreds;
        private TextBox _txtCookieInput;
        private TextBox _txtUsername;
        private TextBox _txtPassword;
        private TextBox _txtTwoFactor;
        private Button _btnCheckLive;
        private Button _btnExtractCookie;
        private Button _btnExtractToken;

        // Tab 3: Seeding
        private TextBox _txtPostUrl;
        private TextBox _txtCommentSpintax;
        private CheckBox _chkJoinGroup;
        private TextBox _txtGroupUrl;

        // Bottom Controls
        private NumericUpDown _numThreads;
        private Button _btnStart;
        private Button _btnStop;
        private RichTextBox _rtbLog;

        private readonly List<UserProfile> _allProfiles;
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        public FacebookAutomationForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            LoadProfileList();
        }

        private void InitializeComponent()
        {
            this.Text = "🤖 BỘ CÔNG CỤ TỰ ĐỘNG HÓA FACEBOOK (AUTOMATION SUITE)";
            this.Size = new Size(1060, 680);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Header Banner
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(24, 119, 242),
                Padding = new Padding(20, 10, 20, 10)
            };

            Label lblTitle = new Label
            {
                Text = "FACEBOOK AUTOMATION & CDP STUDIO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 10)
            };

            Label lblSubtitle = new Label
            {
                Text = "Tương tác nuôi nick, Tự động đăng nhập 2FA, Check Live UID, Seeding và Cổng điều khiển Remote DevTools (CDP)",
                ForeColor = Color.FromArgb(220, 235, 255),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(18, 34)
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // Main Split: Left = Profile Selector, Right = Tabs & Actions
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 330,
                IsSplitterFixed = true
            };

            // LEFT PANEL: Profile Selection
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            Label lblProfiles = new Label { Text = "Danh sách Profile áp dụng:", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Dock = DockStyle.Top, Height = 25 };

            _txtSearch = new TextBox { Dock = DockStyle.Top, Height = 26, Text = "" };
            _txtSearch.TextChanged += (s, e) => FilterProfileList();

            Panel pnlSelectButtons = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 4, 0, 4) };
            _btnSelectAll = new Button { Text = "Chọn tất", Width = 72, Height = 26, Location = new Point(0, 4), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F) };
            _btnSelectAll.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnSelectAll.Click += (s, e) => SetAllChecked(true);

            _btnDeselectAll = new Button { Text = "Bỏ chọn", Width = 72, Height = 26, Location = new Point(78, 4), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F) };
            _btnDeselectAll.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnDeselectAll.Click += (s, e) => SetAllChecked(false);

            _btnSelectRunning = new Button { Text = "Chỉ đang mở", Width = 95, Height = 26, Location = new Point(156, 4), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(2, 132, 199) };
            _btnSelectRunning.FlatAppearance.BorderColor = Color.FromArgb(186, 230, 253);
            _btnSelectRunning.Click += (s, e) => SelectRunningProfiles();

            pnlSelectButtons.Controls.Add(_btnSelectAll);
            pnlSelectButtons.Controls.Add(_btnDeselectAll);
            pnlSelectButtons.Controls.Add(_btnSelectRunning);

            _chkListProfiles = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            pnlLeft.Controls.Add(_chkListProfiles);
            pnlLeft.Controls.Add(pnlSelectButtons);
            pnlLeft.Controls.Add(_txtSearch);
            pnlLeft.Controls.Add(lblProfiles);
            split.Panel1.Controls.Add(pnlLeft);

            // RIGHT PANEL: Tabs + Log + Bottom Actions
            Panel pnlRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            TabControl tabs = new TabControl { Dock = DockStyle.Top, Height = 260, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };

            // TAB 1: Nuôi nick & Tương tác
            TabPage tabFarming = new TabPage("🌟 Nuôi Nick & Tương Tác");
            tabFarming.BackColor = Color.White;
            tabFarming.Padding = new Padding(15);

            _chkSurfFeed = new CheckBox { Text = "Lướt Newsfeed tự nhiên (cuộn mượt, dừng đọc ngẫu nhiên)", Location = new Point(20, 20), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            Label lblFeedSec = new Label { Text = "Thời gian lướt:", Location = new Point(45, 50), AutoSize = true };
            _numFeedDuration = new NumericUpDown { Location = new Point(140, 48), Width = 60, Minimum = 10, Maximum = 600, Value = 60 };
            Label lblSecUnit = new Label { Text = "giây / profile", Location = new Point(208, 50), AutoSize = true, ForeColor = Color.Gray };

            _chkAutoLike = new CheckBox { Text = "Thả cảm xúc ngẫu nhiên khi lướt bài", Location = new Point(20, 85), AutoSize = true, Checked = true };
            Label lblLikeCount = new Label { Text = "Tối đa like:", Location = new Point(45, 112), AutoSize = true };
            _numMaxLikes = new NumericUpDown { Location = new Point(140, 110), Width = 60, Minimum = 1, Maximum = 50, Value = 3 };
            Label lblLikeUnit = new Label { Text = "bài viết", Location = new Point(208, 112), AutoSize = true, ForeColor = Color.Gray };

            _chkWatchReels = new CheckBox { Text = "Xem video Reels / Watch", Location = new Point(360, 20), AutoSize = true, Checked = false };
            Label lblReel = new Label { Text = "Số lượng Reels:", Location = new Point(385, 50), AutoSize = true };
            _numReelCount = new NumericUpDown { Location = new Point(485, 48), Width = 60, Minimum = 1, Maximum = 20, Value = 3 };

            _chkCheckNotif = new CheckBox { Text = "Đọc và mở tab Thông báo (Tăng trust tài khoản)", Location = new Point(360, 85), AutoSize = true, Checked = true };

            tabFarming.Controls.Add(_chkSurfFeed);
            tabFarming.Controls.Add(lblFeedSec);
            tabFarming.Controls.Add(_numFeedDuration);
            tabFarming.Controls.Add(lblSecUnit);
            tabFarming.Controls.Add(_chkAutoLike);
            tabFarming.Controls.Add(lblLikeCount);
            tabFarming.Controls.Add(_numMaxLikes);
            tabFarming.Controls.Add(lblLikeUnit);
            tabFarming.Controls.Add(_chkWatchReels);
            tabFarming.Controls.Add(lblReel);
            tabFarming.Controls.Add(_numReelCount);
            tabFarming.Controls.Add(_chkCheckNotif);
            tabs.TabPages.Add(tabFarming);

            // TAB 2: Đăng nhập & Quản lý Acc
            TabPage tabLogin = new TabPage("🔑 Đăng Nhập & Quản Lý Acc");
            tabLogin.BackColor = Color.White;
            tabLogin.Padding = new Padding(15);

            _radLoginCookie = new RadioButton { Text = "Đăng nhập bằng Cookie (c_user=...; xs=...)", Location = new Point(20, 15), AutoSize = true, Checked = true };
            _txtCookieInput = new TextBox { Location = new Point(20, 42), Width = 640, Height = 26 };

            _radLoginCreds = new RadioButton { Text = "Đăng nhập bằng Tài khoản | Mật khẩu | 2FA Secret Key (Tự giải mã OTP):", Location = new Point(20, 80), AutoSize = true };
            Label lblU = new Label { Text = "User / UID:", Location = new Point(20, 110), AutoSize = true };
            _txtUsername = new TextBox { Location = new Point(90, 107), Width = 140 };

            Label lblP = new Label { Text = "Mật khẩu:", Location = new Point(245, 110), AutoSize = true };
            _txtPassword = new TextBox { Location = new Point(310, 107), Width = 120, PasswordChar = '•' };

            Label lbl2FA = new Label { Text = "2FA Secret:", Location = new Point(445, 110), AutoSize = true };
            _txtTwoFactor = new TextBox { Location = new Point(520, 107), Width = 140 };

            _btnCheckLive = new Button { Text = "🔍 Check Live / Die", Location = new Point(20, 155), Width = 135, Height = 32, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnCheckLive.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnCheckLive.Click += BtnCheckLive_Click;

            _btnExtractCookie = new Button { Text = "📥 Xuất Cookie", Location = new Point(165, 155), Width = 120, Height = 32, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnExtractCookie.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnExtractCookie.Click += BtnExtractCookie_Click;

            _btnExtractToken = new Button { Text = "🔑 Xuất Token EAAB", Location = new Point(295, 155), Width = 135, Height = 32, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnExtractToken.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnExtractToken.Click += BtnExtractToken_Click;

            tabLogin.Controls.Add(_radLoginCookie);
            tabLogin.Controls.Add(_txtCookieInput);
            tabLogin.Controls.Add(_radLoginCreds);
            tabLogin.Controls.Add(lblU);
            tabLogin.Controls.Add(_txtUsername);
            tabLogin.Controls.Add(lblP);
            tabLogin.Controls.Add(_txtPassword);
            tabLogin.Controls.Add(lbl2FA);
            tabLogin.Controls.Add(_txtTwoFactor);
            tabLogin.Controls.Add(_btnCheckLive);
            tabLogin.Controls.Add(_btnExtractCookie);
            tabLogin.Controls.Add(_btnExtractToken);
            tabs.TabPages.Add(tabLogin);

            // TAB 3: Seeding & Nhóm
            TabPage tabSeeding = new TabPage("💬 Seeding & Nhóm");
            tabSeeding.BackColor = Color.White;
            tabSeeding.Padding = new Padding(15);

            Label lblPostUrl = new Label { Text = "Link bài viết cần bình luận / seeding:", Location = new Point(20, 15), AutoSize = true };
            _txtPostUrl = new TextBox { Location = new Point(20, 36), Width = 640 };

            Label lblComment = new Label { Text = "Nội dung bình luận (Hỗ trợ Spintax ví dụ: {Hay quá|Tuyệt vời|Đẹp thế shop}):", Location = new Point(20, 70), AutoSize = true };
            _txtCommentSpintax = new TextBox { Location = new Point(20, 92), Width = 640, Height = 55, Multiline = true, Text = "{Sản phẩm tuyệt vời|Quá đẹp luôn ạ|Shop tư vấn nhiệt tình nha|10 điểm cho chất lượng}" };

            _chkJoinGroup = new CheckBox { Text = "Tham gia nhóm theo link:", Location = new Point(20, 160), AutoSize = true };
            _txtGroupUrl = new TextBox { Location = new Point(190, 158), Width = 470 };

            tabSeeding.Controls.Add(lblPostUrl);
            tabSeeding.Controls.Add(_txtPostUrl);
            tabSeeding.Controls.Add(lblComment);
            tabSeeding.Controls.Add(_txtCommentSpintax);
            tabSeeding.Controls.Add(_chkJoinGroup);
            tabSeeding.Controls.Add(_txtGroupUrl);
            tabs.TabPages.Add(tabSeeding);

            // TAB 4: Cổng CDP & Local API
            TabPage tabCdp = new TabPage("🌐 Cổng CDP & Local API");
            tabCdp.BackColor = Color.White;
            tabCdp.Padding = new Padding(15);

            int apiPort = LocalApiService.Instance.Port;
            Label lblApiBanner = new Label
            {
                Text = string.Format("⚡ Local API Server đang hoạt động tại: http://127.0.0.1:{0}", apiPort),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(20, 15),
                AutoSize = true
            };

            Label lblApiInfo = new Label
            {
                Text = "Bạn có thể điều khiển ADBLogin bằng Python / Node.js / Puppeteer / Playwright thông qua các endpoint:\n" +
                       string.Format("• GET  http://127.0.0.1:{0}/api/profiles        -> Lấy danh sách profiles\n", apiPort) +
                       string.Format("• GET  http://127.0.0.1:{0}/api/profile/start?id=ID -> Mở profile và nhận CDP port\n", apiPort) +
                       string.Format("• GET  http://127.0.0.1:{0}/api/profile/stop?id=ID  -> Đóng profile\n", apiPort) +
                       string.Format("• GET  http://127.0.0.1:{0}/api/fb/check_uid?uid=ID -> Check Live/Die UID", apiPort),
                Location = new Point(20, 45),
                Size = new Size(640, 85),
                Font = new Font("Consolas", 8.5F),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            Button btnOpenPythonFolder = new Button
            {
                Text = "📁 Mở thư mục code mẫu Python",
                Location = new Point(20, 140),
                Width = 220,
                Height = 32,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat
            };
            btnOpenPythonFolder.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnOpenPythonFolder.Click += (s, e) =>
            {
                string pyDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "automation", "python");
                if (Directory.Exists(pyDir)) Process.Start("explorer.exe", pyDir);
            };

            Button btnTestApi = new Button
            {
                Text = "🔗 Mở API Status trên trình duyệt",
                Location = new Point(250, 140),
                Width = 220,
                Height = 32,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat
            };
            btnTestApi.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnTestApi.Click += (s, e) =>
            {
                Process.Start(string.Format("http://127.0.0.1:{0}/api/profiles", apiPort));
            };

            tabCdp.Controls.Add(lblApiBanner);
            tabCdp.Controls.Add(lblApiInfo);
            tabCdp.Controls.Add(btnOpenPythonFolder);
            tabCdp.Controls.Add(btnTestApi);
            tabs.TabPages.Add(tabCdp);

            // Log Box
            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 8.5F),
                BorderStyle = BorderStyle.None,
                ReadOnly = true
            };

            // Bottom Actions Panel
            Panel pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(0, 8, 0, 0) };

            Label lblThread = new Label { Text = "Số luồng:", Location = new Point(0, 14), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numThreads = new NumericUpDown { Location = new Point(65, 12), Width = 45, Minimum = 1, Maximum = 10, Value = 2 };

            _btnStart = new Button
            {
                Text = "▶ BẮT ĐẦU CHẠY",
                Location = new Point(130, 8),
                Width = 140,
                Height = 32,
                BackColor = Color.FromArgb(24, 119, 242),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnStart.FlatAppearance.BorderSize = 0;
            _btnStart.Click += BtnStart_Click;

            _btnStop = new Button
            {
                Text = "⏹ DỪNG LẠI",
                Location = new Point(280, 8),
                Width = 110,
                Height = 32,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Enabled = false
            };
            _btnStop.FlatAppearance.BorderSize = 0;
            _btnStop.Click += BtnStop_Click;

            pnlBottom.Controls.Add(lblThread);
            pnlBottom.Controls.Add(_numThreads);
            pnlBottom.Controls.Add(_btnStart);
            pnlBottom.Controls.Add(_btnStop);

            pnlRight.Controls.Add(_rtbLog);
            pnlRight.Controls.Add(pnlBottom);
            pnlRight.Controls.Add(tabs);
            split.Panel2.Controls.Add(pnlRight);

            this.Controls.Add(split);
            this.Controls.Add(pnlHeader);

            Log("Hệ thống Automation Facebook sẵn sàng. Vui lòng chọn profile và cấu hình kịch bản.");
        }

        private void LoadProfileList()
        {
            _chkListProfiles.Items.Clear();
            foreach (var p in _allProfiles)
            {
                bool running = BrowserSessionManager.Instance.IsRunning(p.ProfileId);
                string display = string.Format("{0} [{1}]", p.ProfileName, running ? "RUNNING" : "STOP");
                _chkListProfiles.Items.Add(new ProfileItem { Profile = p, Display = display });
            }
        }

        private void FilterProfileList()
        {
            string q = _txtSearch.Text.Trim().ToLowerInvariant();
            _chkListProfiles.Items.Clear();
            foreach (var p in _allProfiles)
            {
                if (string.IsNullOrEmpty(q) || p.ProfileName.ToLowerInvariant().Contains(q))
                {
                    bool running = BrowserSessionManager.Instance.IsRunning(p.ProfileId);
                    string display = string.Format("{0} [{1}]", p.ProfileName, running ? "RUNNING" : "STOP");
                    _chkListProfiles.Items.Add(new ProfileItem { Profile = p, Display = display });
                }
            }
        }

        private void SetAllChecked(bool isChecked)
        {
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                _chkListProfiles.SetItemChecked(i, isChecked);
            }
        }

        private void SelectRunningProfiles()
        {
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                var item = _chkListProfiles.Items[i] as ProfileItem;
                if (item != null && BrowserSessionManager.Instance.IsRunning(item.Profile.ProfileId))
                {
                    _chkListProfiles.SetItemChecked(i, true);
                }
                else
                {
                    _chkListProfiles.SetItemChecked(i, false);
                }
            }
        }

        private List<UserProfile> GetSelectedProfiles()
        {
            var list = new List<UserProfile>();
            foreach (var item in _chkListProfiles.CheckedItems)
            {
                var pItem = item as ProfileItem;
                if (pItem != null) list.Add(pItem.Profile);
            }
            return list;
        }

        private void Log(string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => Log(msg)));
                return;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            _rtbLog.AppendText(string.Format("[{0}] {1}\n", time, msg));
            _rtbLog.SelectionStart = _rtbLog.Text.Length;
            _rtbLog.ScrollToCaret();
        }

        #region Thao tác Nút Bấm

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            var selectedProfiles = GetSelectedProfiles();
            if (selectedProfiles.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để thực hiện kịch bản!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _isRunning = true;
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _cts = new CancellationTokenSource();

            int threadCount = (int)_numThreads.Value;
            Log(string.Format("=== BẮT ĐẦU CHẠY KỊCH BẢN ({0} Profiles, {1} Luồng) ===", selectedProfiles.Count, threadCount));

            // Chụp cấu hình trước khi chạy async
            bool doSurf = _chkSurfFeed.Checked;
            int feedSec = (int)_numFeedDuration.Value;
            bool doLike = _chkAutoLike.Checked;
            int maxLike = (int)_numMaxLikes.Value;
            bool doReels = _chkWatchReels.Checked;
            int reelCount = (int)_numReelCount.Value;
            bool doNotif = _chkCheckNotif.Checked;

            bool doLoginCookie = _radLoginCookie.Checked && !string.IsNullOrWhiteSpace(_txtCookieInput.Text);
            string cookieStr = _txtCookieInput.Text.Trim();

            bool doLoginCreds = _radLoginCreds.Checked && !string.IsNullOrWhiteSpace(_txtUsername.Text);
            string userStr = _txtUsername.Text.Trim();
            string passStr = _txtPassword.Text;
            string secretStr = _txtTwoFactor.Text.Trim();

            string postUrl = _txtPostUrl.Text.Trim();
            string commentSpintax = _txtCommentSpintax.Text.Trim();
            bool doJoin = _chkJoinGroup.Checked && !string.IsNullOrWhiteSpace(_txtGroupUrl.Text);
            string groupUrl = _txtGroupUrl.Text.Trim();

            await Task.Run(() =>
            {
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = threadCount,
                    CancellationToken = _cts.Token
                };

                try
                {
                    Parallel.ForEach(selectedProfiles, options, (profile) =>
                    {
                        if (_cts.IsCancellationRequested) return;

                        RunProfileTask(profile, doSurf, feedSec, doLike, maxLike, doReels, reelCount, doNotif,
                            doLoginCookie, cookieStr, doLoginCreds, userStr, passStr, secretStr,
                            postUrl, commentSpintax, doJoin, groupUrl, _cts.Token);
                    });
                }
                catch (OperationCanceledException)
                {
                    Log("[!] Người dùng đã bấm dừng tác vụ.");
                }
                catch (Exception ex)
                {
                    Log("[-] Lỗi chạy luồng: " + ex.Message);
                }
            });

            _isRunning = false;
            _btnStart.Enabled = true;
            _btnStop.Enabled = false;
            Log("=== HOÀN TẤT TẤT CẢ KỊCH BẢN ===");
        }

        private void RunProfileTask(UserProfile profile, bool doSurf, int feedSec, bool doLike, int maxLike, bool doReels, int reelCount, bool doNotif,
            bool doLoginCookie, string cookieStr, bool doLoginCreds, string userStr, string passStr, string secretStr,
            string postUrl, string commentSpintax, bool doJoin, string groupUrl, CancellationToken ct)
        {
            string tag = string.Format("[{0}]", profile.ProfileName);
            Log(string.Format("{0} Đang chuẩn bị trình duyệt...", tag));

            IWebDriver driver = BrowserSessionManager.Instance.GetDriver(profile.ProfileId);

            if (driver == null)
            {
                try
                {
                    var launcher = new BrowserLauncherService();
                    driver = launcher.LaunchBrowser(profile);
                }
                catch (Exception ex)
                {
                    Log(string.Format("{0} Không thể mở trình duyệt: {1}", tag, ex.Message));
                    return;
                }
            }

            var fb = new FacebookAutomationService();

            // 1. Dang nhap Cookie neu co
            if (doLoginCookie)
            {
                Log(string.Format("{0} Đang đăng nhập bằng Cookie...", tag));
                fb.LoginWithCookie(driver, cookieStr, m => Log(tag + " " + m));
            }
            // Hoac dang nhap User/Pass/2FA
            else if (doLoginCreds)
            {
                Log(string.Format("{0} Đang đăng nhập bằng User/Pass/2FA...", tag));
                fb.LoginWithCredentials(driver, userStr, passStr, secretStr, m => Log(tag + " " + m));
            }

            // 2. Kiem tra trang thai
            string uid, status;
            fb.CheckAccountStatus(driver, out uid, out status);
            Log(string.Format("{0} Trạng thái hiện tại: {1} (UID: {2})", tag, status, uid));

            // 3. Nuoi nick Newfeed
            if (doSurf && !ct.IsCancellationRequested)
            {
                fb.SurfNewsfeed(driver, feedSec, doLike, maxLike, m => Log(tag + " " + m), ct);
            }

            // 4. Xem Reels
            if (doReels && !ct.IsCancellationRequested)
            {
                fb.WatchReels(driver, reelCount, m => Log(tag + " " + m), ct);
            }

            // 5. Xem Thong bao
            if (doNotif && !ct.IsCancellationRequested)
            {
                fb.ViewNotifications(driver, m => Log(tag + " " + m));
            }

            // 6. Tham gia nhom neu co
            if (doJoin && !ct.IsCancellationRequested)
            {
                fb.JoinGroup(driver, groupUrl, m => Log(tag + " " + m));
            }

            // 7. Seeding binh luan neu co
            if (!string.IsNullOrEmpty(postUrl) && !string.IsNullOrEmpty(commentSpintax) && !ct.IsCancellationRequested)
            {
                fb.CommentPost(driver, postUrl, commentSpintax, m => Log(tag + " " + m));
            }

            Log(string.Format("{0} Đã hoàn thành các tác vụ trên profile này!", tag));
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                Log("[*] Đang gửi tín hiệu dừng tới các luồng...");
            }
        }

        private async void BtnCheckLive_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 profile để kiểm tra!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Log(string.Format("[*] Đang kiểm tra Live/Die cho {0} profile...", selected.Count));

            await Task.Run(() =>
            {
                foreach (var p in selected)
                {
                    // Trích xuất UID nếu có từ tên profile hoặc note
                    string uid = "";
                    var match = System.Text.RegularExpressions.Regex.Match(p.ProfileName, @"\d{10,20}");
                    if (match.Success) uid = match.Value;

                    if (!string.IsNullOrEmpty(uid))
                    {
                        string name, err;
                        bool live = FacebookAutomationService.FastCheckUidLive(uid, out name, out err);
                        Log(string.Format("[{0}] UID {1}: {2} {3}", p.ProfileName, uid, live ? "LIVE (Hoạt động)" : "DIE (Khóa/Chết)", err));
                    }
                    else
                    {
                        // Kiểm tra qua trình duyệt nếu đang mở
                        var driver = BrowserSessionManager.Instance.GetDriver(p.ProfileId);
                        if (driver != null)
                        {
                            string curUid, status;
                            new FacebookAutomationService().CheckAccountStatus(driver, out curUid, out status);
                            Log(string.Format("[{0}] Trạng thái qua browser: {1} (UID: {2})", p.ProfileName, status, curUid));
                        }
                        else
                        {
                            Log(string.Format("[{0}] Không có UID trong tên và profile chưa mở để quét cookie.", p.ProfileName));
                        }
                    }
                }
            });
        }

        private void BtnExtractCookie_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn profile đang mở để trích xuất cookie!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (var p in selected)
            {
                var driver = BrowserSessionManager.Instance.GetDriver(p.ProfileId);
                if (driver != null)
                {
                    string cookies = new FacebookAutomationService().ExtractCookies(driver);
                    Log(string.Format("[{0}] COOKIE: {1}", p.ProfileName, cookies));
                    if (!string.IsNullOrEmpty(cookies))
                    {
                        Clipboard.SetText(cookies);
                        Log(string.Format("[+] Đã copy Cookie của {0} vào Clipboard!", p.ProfileName));
                    }
                }
                else
                {
                    Log(string.Format("[-] Profile {0} chưa mở trình duyệt!", p.ProfileName));
                }
            }
        }

        private void BtnExtractToken_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn profile đang mở để trích xuất Token!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (var p in selected)
            {
                var driver = BrowserSessionManager.Instance.GetDriver(p.ProfileId);
                if (driver != null)
                {
                    string token = new FacebookAutomationService().ExtractToken(driver);
                    if (!string.IsNullOrEmpty(token))
                    {
                        Log(string.Format("[+] [{0}] TOKEN: {1}", p.ProfileName, token));
                        Clipboard.SetText(token);
                        Log(string.Format("[+] Đã copy Token của {0} vào Clipboard!", p.ProfileName));
                    }
                    else
                    {
                        Log(string.Format("[-] [{0}] Không tìm thấy EAAB Token trên phiên duyệt hiện tại.", p.ProfileName));
                    }
                }
                else
                {
                    Log(string.Format("[-] Profile {0} chưa mở trình duyệt!", p.ProfileName));
                }
            }
        }

        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isRunning)
            {
                if (MessageBox.Show("Các kịch bản Facebook đang chạy trong nền. Bạn có chắc muốn dừng và đóng cửa sổ này không?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                if (_cts != null) _cts.Cancel();
            }
            base.OnFormClosing(e);
        }

        private class ProfileItem
        {
            public UserProfile Profile { get; set; }
            public string Display { get; set; }
            public override string ToString()
            {
                return Display;
            }
        }
    }
}
