using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADBLogin.Core.Automation;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;
using OpenQA.Selenium;

namespace ADBLogin.UI
{
    public class DiscordAutomationForm : Form
    {
        private CheckedListBox _chkListProfiles;
        private TextBox _txtSearch;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRunning;
        private Label _lblProfileCount;
        private readonly HashSet<string> _selectedProfileIds = new HashSet<string>();

        // Tab 1: Join & Verify
        private TextBox _txtInviteLinks;
        private CheckBox _chkAutoVerify;
        private CheckBox _chkDirectJoin;

        // Tab 2: Chat & Leveling
        private TextBox _txtChatSpintax;
        private NumericUpDown _numMessageCount;
        private NumericUpDown _numDelayMin;
        private NumericUpDown _numDelayMax;
        private CheckBox _chkEnableChat;

        // Tab 3: Token Login
        private TextBox _txtDiscordToken;
        private Button _btnTestTokenLogin;

        // Bottom Controls
        private NumericUpDown _numThreads;
        private Button _btnStart;
        private Button _btnStop;
        private Label _lblRunningStatus;
        private RichTextBox _rtbLog;
        private Button _btnClearLog;
        private Button _btnCopyLog;

        private SplitContainer _splitMain;
        private SplitContainer _splitRight;
        private TabControl _tabs;

        private readonly List<UserProfile> _allProfiles;
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        public DiscordAutomationForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            LoadProfileList();
        }

        private void InitializeComponent()
        {
            this.Text = "🎮 BỘ CÔNG CỤ TỰ ĐỘNG HÓA DISCORD (AIRDROP & COMMUNITY SUITE)";
            this.Size = new Size(1220, 800);
            this.MinimumSize = new Size(1020, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9F);

            // Header Banner
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(88, 101, 242), // Discord Blurple
                Padding = new Padding(16, 8, 16, 8)
            };

            Label lblTitle = new Label
            {
                Text = "🎮 DISCORD AUTOMATION & COMMUNITY STUDIO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Tự động Join Server qua Invite Link • Tự động Verify nội quy • Chat Seeding & Cày Level XP • Đăng nhập Token siêu tốc",
                ForeColor = Color.FromArgb(224, 231, 255),
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(18, 32)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // Split Main (Left = Profiles, Right = Tabs + Log)
            _splitMain = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6 };

            // Left Panel: Profiles
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.FromArgb(248, 250, 252) };
            Label lblProfilesTitle = new Label { Dock = DockStyle.Top, Height = 26, Text = "📋 DANH SÁCH PROFILES", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59) };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Segoe UI", 9F),
                Text = "🔍 Tìm kiếm profile...",
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            _txtSearch.GotFocus += (s, e) => { if (_txtSearch.Text == "🔍 Tìm kiếm profile...") { _txtSearch.Text = ""; _txtSearch.ForeColor = Color.FromArgb(30, 41, 59); } };
            _txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_txtSearch.Text)) { _txtSearch.Text = "🔍 Tìm kiếm profile..."; _txtSearch.ForeColor = Color.FromArgb(100, 116, 139); } };
            _txtSearch.TextChanged += (s, e) => FilterProfiles();

            Panel pnlSelectButtons = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 4, 0, 4) };
            _btnSelectAll = new Button { Text = "Tất cả", Width = 60, Dock = DockStyle.Left, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnSelectAll.Click += (s, e) => SetCheckAll(true);

            _btnDeselectAll = new Button { Text = "Bỏ chọn", Width = 65, Dock = DockStyle.Left, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnDeselectAll.Click += (s, e) => SetCheckAll(false);

            _btnSelectRunning = new Button { Text = "Đang chạy", Width = 80, Dock = DockStyle.Left, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnSelectRunning.Click += (s, e) => SelectRunningProfiles();

            pnlSelectButtons.Controls.Add(_btnSelectRunning);
            pnlSelectButtons.Controls.Add(_btnDeselectAll);
            pnlSelectButtons.Controls.Add(_btnSelectAll);

            _chkListProfiles = new CheckedListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, CheckOnClick = true, Font = new Font("Segoe UI", 9F) };
            _lblProfileCount = new Label { Dock = DockStyle.Bottom, Height = 22, Text = "Đã chọn: 0 / 0 profiles", ForeColor = Color.FromArgb(100, 116, 139), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5F) };
            _chkListProfiles.ItemCheck += (s, e) =>
            {
                var item = _chkListProfiles.Items[e.Index] as ProfileItem;
                if (item != null)
                {
                    if (e.NewValue == CheckState.Checked) _selectedProfileIds.Add(item.Profile.ProfileId);
                    else _selectedProfileIds.Remove(item.Profile.ProfileId);
                }
                this.BeginInvoke(new Action(UpdateProfileCountLabel));
            };

            pnlLeft.Controls.Add(_chkListProfiles);
            pnlLeft.Controls.Add(pnlSelectButtons);
            pnlLeft.Controls.Add(_txtSearch);
            pnlLeft.Controls.Add(lblProfilesTitle);
            pnlLeft.Controls.Add(_lblProfileCount);
            _splitMain.Panel1.Controls.Add(pnlLeft);

            // Right Panel
            _splitRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Color.FromArgb(226, 232, 240) };
            _splitMain.Panel2.Controls.Add(_splitRight);

            // Tabs
            _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _tabs.TabPages.Add(CreateJoinVerifyTab());
            _tabs.TabPages.Add(CreateChatTab());
            _tabs.TabPages.Add(CreateTokenTab());
            _splitRight.Panel1.Controls.Add(_tabs);

            // Log Console & Controls
            Panel pnlLogContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
            Panel pnlControls = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(0, 0, 0, 8) };

            Label lblThreads = new Label { Text = "Số luồng chạy song song:", AutoSize = true, Location = new Point(4, 10), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numThreads = new NumericUpDown { Location = new Point(170, 8), Width = 55, Minimum = 1, Maximum = 30, Value = 2 };

            _btnStart = new Button { Text = "🚀 BẮT ĐẦU CHẠY", Location = new Point(236, 4), Width = 150, Height = 32, BackColor = Color.FromArgb(88, 101, 242), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            _btnStart.FlatAppearance.BorderSize = 0;
            _btnStart.Click += BtnStart_Click;

            _btnStop = new Button { Text = "⏹ DỪNG LẠI", Location = new Point(394, 4), Width = 110, Height = 32, BackColor = Color.FromArgb(239, 68, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Enabled = false, Cursor = Cursors.Hand };
            _btnStop.FlatAppearance.BorderSize = 0;
            _btnStop.Click += BtnStop_Click;

            _lblRunningStatus = new Label { Text = "Sẵn sàng", Location = new Point(515, 10), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 8.5F, FontStyle.Italic) };
            _btnClearLog = new Button { Text = "Xóa Log", Dock = DockStyle.Right, Width = 70, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnClearLog.Click += (s, e) => _rtbLog.Clear();

            _btnCopyLog = new Button { Text = "Copy Log", Dock = DockStyle.Right, Width = 75, BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
            _btnCopyLog.Click += (s, e) => { if (!string.IsNullOrEmpty(_rtbLog.Text)) Clipboard.SetText(_rtbLog.Text); };

            pnlControls.Controls.Add(_btnCopyLog);
            pnlControls.Controls.Add(_btnClearLog);
            pnlControls.Controls.Add(_lblRunningStatus);
            pnlControls.Controls.Add(_btnStop);
            pnlControls.Controls.Add(_btnStart);
            pnlControls.Controls.Add(_numThreads);
            pnlControls.Controls.Add(lblThreads);

            _rtbLog = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.FromArgb(226, 232, 240), Font = new Font("Consolas", 9F), ReadOnly = true, BorderStyle = BorderStyle.None };
            pnlLogContainer.Controls.Add(_rtbLog);
            pnlLogContainer.Controls.Add(pnlControls);
            _splitRight.Panel2.Controls.Add(pnlLogContainer);

            this.Controls.Add(_splitMain);
            this.Controls.Add(pnlHeader);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                if (_splitMain != null && _splitMain.Width > 400) _splitMain.SplitterDistance = Math.Min(310, Math.Max(240, _splitMain.Width / 4));
                if (_splitRight != null && _splitRight.Height > 400) _splitRight.SplitterDistance = Math.Min(420, Math.Max(280, _splitRight.Height - 240));
            }
            catch { }
        }

        #region Create Tabs

        private TabPage CreateJoinVerifyTab()
        {
            TabPage tab = new TabPage("📥 Tham Gia Server & Verify") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblInvite = new Label { Text = "Danh sách link Invite Discord (mỗi dòng 1 link hoặc invite code):", Location = new Point(14, 16), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

            _txtInviteLinks = new TextBox
            {
                Location = new Point(18, 44),
                Width = 700,
                Height = 120,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 9F),
                Text = "https://discord.gg/openai\nhttps://discord.gg/midjourney"
            };

            _chkAutoVerify = new CheckBox { Text = "Tự động bấm Xác nhận nội quy (Complete Rules) & click nút Verify thành viên", Location = new Point(18, 178), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(88, 101, 242) };
            _chkDirectJoin = new CheckBox { Text = "Tự động chấp nhận lời mời (Accept Invite) khi mở trang", Location = new Point(18, 208), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };

            Label lblGuide = new Label
            {
                Text = "💡 Cơ chế: Tự động điều hướng đến lời mời server, click Chấp nhận lời mời, vượt qua bảng\nđồng ý nội quy server và click nút Verify trong các kênh onboarding.",
                Location = new Point(18, 246),
                AutoSize = true,
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            pnl.Controls.Add(lblGuide);
            pnl.Controls.Add(_chkDirectJoin);
            pnl.Controls.Add(_chkAutoVerify);
            pnl.Controls.Add(_txtInviteLinks);
            pnl.Controls.Add(lblInvite);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateChatTab()
        {
            TabPage tab = new TabPage("💬 Chat Seeding & Cày Level (XP)") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            _chkEnableChat = new CheckBox { Text = "Kích hoạt chế độ gửi tin nhắn seeding / cày rank level vào kênh hiện tại", Location = new Point(14, 16), AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(88, 101, 242) };

            Label lblContent = new Label { Text = "Nội dung tin nhắn (Hỗ trợ Spintax cú pháp {A|B|C}):", Location = new Point(14, 48), AutoSize = true };
            _txtChatSpintax = new TextBox
            {
                Location = new Point(18, 72),
                Width = 700,
                Height = 90,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F),
                Text = "{Good morning|Hello everyone|Hi fam}! {Have a great day|LFG guys|Nice project}!"
            };

            Label lblCount = new Label { Text = "Số lượng tin gửi:", Location = new Point(18, 178), AutoSize = true };
            _numMessageCount = new NumericUpDown { Location = new Point(135, 176), Width = 55, Minimum = 1, Maximum = 100, Value = 3 };

            Label lblDelay = new Label { Text = "Delay giữa 2 tin:", Location = new Point(18, 212), AutoSize = true };
            _numDelayMin = new NumericUpDown { Location = new Point(135, 210), Width = 50, Minimum = 5, Maximum = 600, Value = 15 };
            Label lblTo = new Label { Text = "đến", Location = new Point(190, 212), AutoSize = true };
            _numDelayMax = new NumericUpDown { Location = new Point(220, 210), Width = 50, Minimum = 5, Maximum = 600, Value = 35 };
            Label lblUnit = new Label { Text = "giây (ngẫu nhiên tránh slowmode)", Location = new Point(275, 212), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };

            pnl.Controls.Add(lblUnit);
            pnl.Controls.Add(_numDelayMax);
            pnl.Controls.Add(lblTo);
            pnl.Controls.Add(_numDelayMin);
            pnl.Controls.Add(lblDelay);
            pnl.Controls.Add(_numMessageCount);
            pnl.Controls.Add(lblCount);
            pnl.Controls.Add(_txtChatSpintax);
            pnl.Controls.Add(lblContent);
            pnl.Controls.Add(_chkEnableChat);

            tab.Controls.Add(pnl);
            return tab;
        }

        private TabPage CreateTokenTab()
        {
            TabPage tab = new TabPage("🔑 Đăng Nhập User Token") { BackColor = Color.White };
            Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

            Label lblToken = new Label { Text = "Nhập Discord User Token (Đăng nhập 1 chạm vào thẳng tài khoản):", Location = new Point(14, 16), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

            _txtDiscordToken = new TextBox
            {
                Location = new Point(18, 44),
                Width = 700,
                Height = 50,
                Multiline = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 9F)
            };

            _btnTestTokenLogin = new Button
            {
                Text = "🚀 ĐĂNG NHẬP TOKEN VÀO CÁC PROFILE ĐÃ CHỌN",
                Location = new Point(18, 108),
                Width = 380,
                Height = 36,
                BackColor = Color.FromArgb(88, 101, 242),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnTestTokenLogin.FlatAppearance.BorderSize = 0;
            _btnTestTokenLogin.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtDiscordToken.Text))
                {
                    MessageBox.Show("Vui lòng nhập Token Discord hợp lệ!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                BtnStart_Click(s, e);
            };

            pnl.Controls.Add(_btnTestTokenLogin);
            pnl.Controls.Add(_txtDiscordToken);
            pnl.Controls.Add(lblToken);

            tab.Controls.Add(pnl);
            return tab;
        }

        #endregion

        #region Profile Helpers

        private void LoadProfileList()
        {
            _chkListProfiles.Items.Clear();
            foreach (var p in _allProfiles)
            {
                bool running = BrowserSessionManager.Instance.IsRunning(p.ProfileId);
                string display = string.Format("{0} [{1}]", p.ProfileName, running ? "RUNNING" : "STOP");
                int idx = _chkListProfiles.Items.Add(new ProfileItem { Profile = p, Display = display });
                if (_selectedProfileIds.Contains(p.ProfileId)) _chkListProfiles.SetItemChecked(idx, true);
            }
            UpdateProfileCountLabel();
        }

        private void FilterProfiles()
        {
            string q = _txtSearch.Text.Trim();
            if (q.StartsWith("🔍")) q = "";
            q = q.ToLowerInvariant();

            _chkListProfiles.BeginUpdate();
            _chkListProfiles.Items.Clear();
            foreach (var p in _allProfiles)
            {
                bool match = string.IsNullOrEmpty(q) ||
                    (!string.IsNullOrEmpty(p.ProfileName) && p.ProfileName.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(p.Username) && p.Username.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(p.ProfileId) && p.ProfileId.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(p.Proxy) && p.Proxy.ToLowerInvariant().Contains(q));

                if (match)
                {
                    bool running = BrowserSessionManager.Instance.IsRunning(p.ProfileId);
                    string display = string.Format("{0} [{1}]", p.ProfileName, running ? "RUNNING" : "STOP");
                    int idx = _chkListProfiles.Items.Add(new ProfileItem { Profile = p, Display = display });
                    if (_selectedProfileIds.Contains(p.ProfileId)) _chkListProfiles.SetItemChecked(idx, true);
                }
            }
            _chkListProfiles.EndUpdate();
            UpdateProfileCountLabel();
        }

        private void SetCheckAll(bool isChecked)
        {
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                var item = _chkListProfiles.Items[i] as ProfileItem;
                if (item != null)
                {
                    if (isChecked) _selectedProfileIds.Add(item.Profile.ProfileId);
                    else _selectedProfileIds.Remove(item.Profile.ProfileId);
                }
                _chkListProfiles.SetItemChecked(i, isChecked);
            }
            UpdateProfileCountLabel();
        }

        private void SelectRunningProfiles()
        {
            for (int i = 0; i < _chkListProfiles.Items.Count; i++)
            {
                var item = _chkListProfiles.Items[i] as ProfileItem;
                if (item != null)
                {
                    bool running = BrowserSessionManager.Instance.IsRunning(item.Profile.ProfileId);
                    if (running) _selectedProfileIds.Add(item.Profile.ProfileId);
                    else _selectedProfileIds.Remove(item.Profile.ProfileId);
                    _chkListProfiles.SetItemChecked(i, running);
                }
            }
            UpdateProfileCountLabel();
        }

        private void UpdateProfileCountLabel()
        {
            if (_lblProfileCount != null)
            {
                _lblProfileCount.Text = string.Format("Đã chọn: {0} / {1} profiles", _selectedProfileIds.Count, _allProfiles.Count);
            }
        }

        private List<UserProfile> GetSelectedProfiles()
        {
            var list = new List<UserProfile>();
            foreach (var p in _allProfiles)
            {
                if (_selectedProfileIds.Contains(p.ProfileId)) list.Add(p);
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

        #endregion

        #region Execution

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (_isRunning) return;
            var selectedProfiles = GetSelectedProfiles();
            if (selectedProfiles.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để thực hiện tác vụ!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _isRunning = true;
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _cts = new CancellationTokenSource();

            if (_lblRunningStatus != null)
            {
                _lblRunningStatus.Text = string.Format("● Đang chạy ({0} profiles)...", selectedProfiles.Count);
                _lblRunningStatus.ForeColor = Color.FromArgb(88, 101, 242);
            }

            int threadCount = (int)_numThreads.Value;
            Log(string.Format("=== BẮT ĐẦU CHẠY AUTOMATION DISCORD ({0} Profiles, {1} Luồng) ===", selectedProfiles.Count, threadCount));

            string[] inviteUrls = _txtInviteLinks.Lines;
            bool doAutoVerify = _chkAutoVerify.Checked;
            bool doChat = _chkEnableChat.Checked;
            string chatSpintax = _txtChatSpintax.Text;
            int msgCount = (int)_numMessageCount.Value;
            int delayMin = (int)_numDelayMin.Value;
            int delayMax = (int)_numDelayMax.Value;
            string tokenToLogin = _txtDiscordToken.Text.Trim();

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

                        RunProfileDiscord(profile, inviteUrls, doAutoVerify, doChat, chatSpintax, msgCount, delayMin, delayMax, tokenToLogin, _cts.Token);
                    });
                }
                catch (OperationCanceledException)
                {
                    Log("[!] Người dùng đã bấm dừng tác vụ.");
                }
                catch (Exception ex)
                {
                    Log("[-] Lỗi chạy song song: " + ex.Message);
                }
            });

            _isRunning = false;
            _btnStart.Enabled = true;
            _btnStop.Enabled = false;
            if (_lblRunningStatus != null)
            {
                _lblRunningStatus.Text = "● Đã hoàn tất";
                _lblRunningStatus.ForeColor = Color.FromArgb(16, 185, 129);
            }
            Log("=== HOÀN TẤT TẤT CẢ KỊCH BẢN DISCORD ===");
        }

        private void RunProfileDiscord(UserProfile profile, string[] inviteUrls, bool doAutoVerify, bool doChat, string chatSpintax, int msgCount, int delayMin, int delayMax, string tokenToLogin, CancellationToken ct)
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

            var ds = new DiscordAutomationService();

            // 1. Đăng nhập Token nếu có
            if (!string.IsNullOrEmpty(tokenToLogin))
            {
                Log(string.Format("{0} Đang nạp Discord Token để đăng nhập...", tag));
                ds.LoginWithToken(driver, tokenToLogin, m => Log(tag + " " + m));
            }

            // 2. Tham gia các server
            if (inviteUrls != null && inviteUrls.Length > 0)
            {
                foreach (var inv in inviteUrls)
                {
                    if (ct.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(inv)) continue;

                    Log(string.Format("{0} Bắt đầu tham gia server: {1}", tag, inv));
                    ds.JoinServer(driver, inv, m => Log(tag + " " + m));

                    if (doAutoVerify && !ct.IsCancellationRequested)
                    {
                        ds.AutoVerify(driver, m => Log(tag + " " + m));
                    }
                    Thread.Sleep(2000);
                }
            }

            // 3. Chat Seeding / Cày cấp
            if (doChat && !string.IsNullOrEmpty(chatSpintax) && !ct.IsCancellationRequested)
            {
                Log(string.Format("{0} Bắt đầu chat seeding / cày cấp ({1} tin)...", tag, msgCount));
                var rnd = new Random();
                for (int i = 1; i <= msgCount; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    ds.SendChatMessage(driver, chatSpintax, m => Log(tag + " " + m));

                    if (i < msgCount)
                    {
                        int sleepSec = rnd.Next(delayMin, delayMax);
                        Log(string.Format("{0} Nghỉ {1}s trước tin tiếp theo...", tag, sleepSec));
                        Thread.Sleep(sleepSec * 1000);
                    }
                }
            }

            Log(string.Format("{0} Đã hoàn thành các tác vụ Discord!", tag));
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (_lblRunningStatus != null)
                {
                    _lblRunningStatus.Text = "● Đang dừng lại...";
                    _lblRunningStatus.ForeColor = Color.FromArgb(239, 68, 68);
                }
            }
        }

        private class ProfileItem
        {
            public UserProfile Profile { get; set; }
            public string Display { get; set; }

            public override string ToString()
            {
                return Display ?? (Profile != null ? Profile.ProfileName : "Profile");
            }
        }

        #endregion
    }
}
