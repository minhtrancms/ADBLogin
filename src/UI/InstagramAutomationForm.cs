using System;
using System.Collections.Generic;
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
    public class InstagramAutomationForm : Form
    {
        private CheckedListBox _chkListProfiles;
        private TextBox _txtSearch;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRunning;
        private Label _lblProfileCount;

        // Tab 1: Nuôi nick & Reels
        private NumericUpDown _numFeedDuration;
        private CheckBox _chkAutoLikeFeed;
        private NumericUpDown _numMaxLikesFeed;
        private CheckBox _chkWatchReels;
        private NumericUpDown _numReelCount;

        // Tab 2: Follow
        private TextBox _txtFollowTarget;

        // Tab 3: Dang bai
        private TextBox _txtImagePath;
        private Button _btnBrowseImage;
        private TextBox _txtPostCaption;
        private Button _btnPreviewSpintax;

        // Bottom Controls
        private NumericUpDown _numThreads;
        private Button _btnStart;
        private Button _btnStop;
        private RichTextBox _rtbLog;
        private Label _lblRunningStatus;
        private Button _btnClearLog;
        private Button _btnCopyLog;

        private SplitContainer _splitMain;
        private SplitContainer _splitRight;
        private TabControl _tabs;

        private readonly List<UserProfile> _allProfiles;
        private readonly HashSet<string> _selectedProfileIds = new HashSet<string>();
        private readonly InstagramAutomationService _instaService = new InstagramAutomationService();
        private readonly BrowserLauncherService _launcherService = new BrowserLauncherService();
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        public InstagramAutomationForm(List<UserProfile> profiles)
        {
            _allProfiles = profiles ?? new List<UserProfile>();
            InitializeComponent();
            LoadProfileList();
        }

        private void InitializeComponent()
        {
            this.Text = "📸 BỘ CÔNG CỤ TỰ ĐỘNG HÓA INSTAGRAM (INSTAGRAM AUTOMATION STUDIO)";
            this.Size = new Size(1180, 780);
            this.MinimumSize = new Size(1000, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            // ================= 1. HEADER BANNER =================
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(131, 58, 180),
                Padding = new Padding(16, 8, 16, 8)
            };

            Label lblTitle = new Label
            {
                Text = "📸 INSTAGRAM AUTOMATION & REELS GROWTH STUDIO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Nuôi nick tương tác Feed, xem Reels lướt video ngắn, tự động Follow người dùng & đăng bài viết kèm ảnh chuẩn Spintax",
                ForeColor = Color.FromArgb(243, 232, 255),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(16, 32)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // ================= 2. MAIN SPLIT CONTAINER =================
            _splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 330,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(229, 231, 235)
            };
            this.Controls.Add(_splitMain);
            _splitMain.BringToFront();

            // ================= LEFT: PROFILE SELECTOR =================
            Panel pnlLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            Label lblLeftHeader = new Label
            {
                Text = "📋 CHỌN PROFILE INSTAGRAM",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(131, 58, 180),
                Dock = DockStyle.Top,
                Height = 26
            };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.Gray,
                Text = "🔍 Tìm kiếm profile...",
                Height = 26
            };
            _txtSearch.GotFocus += delegate
            {
                if (_txtSearch.Text == "🔍 Tìm kiếm profile...")
                {
                    _txtSearch.Text = "";
                    _txtSearch.ForeColor = Color.Black;
                }
            };
            _txtSearch.LostFocus += delegate
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "🔍 Tìm kiếm profile...";
                    _txtSearch.ForeColor = Color.Gray;
                }
            };
            _txtSearch.TextChanged += delegate { LoadProfileList(); };

            FlowLayoutPanel pnlQuickSelect = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(0, 4, 0, 4),
                WrapContents = false
            };

            _btnSelectAll = new Button { Text = "Tất cả", Width = 65, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnDeselectAll = new Button { Text = "Bỏ chọn", Width = 65, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnSelectRunning = new Button { Text = "Đang chạy", Width = 80, Height = 25, FlatStyle = FlatStyle.Flat };

            _btnSelectAll.Click += delegate
            {
                for (int i = 0; i < _chkListProfiles.Items.Count; i++)
                {
                    _chkListProfiles.SetItemChecked(i, true);
                    var p = _chkListProfiles.Items[i] as ProfileDisplayItem;
                    if (p != null) _selectedProfileIds.Add(p.Profile.ProfileId);
                }
                UpdateProfileCount();
            };

            _btnDeselectAll.Click += delegate
            {
                for (int i = 0; i < _chkListProfiles.Items.Count; i++)
                {
                    _chkListProfiles.SetItemChecked(i, false);
                    var p = _chkListProfiles.Items[i] as ProfileDisplayItem;
                    if (p != null) _selectedProfileIds.Remove(p.Profile.ProfileId);
                }
                UpdateProfileCount();
            };

            _btnSelectRunning.Click += delegate
            {
                for (int i = 0; i < _chkListProfiles.Items.Count; i++)
                {
                    var item = _chkListProfiles.Items[i] as ProfileDisplayItem;
                    bool running = item != null && BrowserSessionManager.Instance.IsRunning(item.Profile.ProfileId);
                    _chkListProfiles.SetItemChecked(i, running);
                    if (item != null)
                    {
                        if (running) _selectedProfileIds.Add(item.Profile.ProfileId);
                        else _selectedProfileIds.Remove(item.Profile.ProfileId);
                    }
                }
                UpdateProfileCount();
            };

            pnlQuickSelect.Controls.Add(_btnSelectAll);
            pnlQuickSelect.Controls.Add(_btnDeselectAll);
            pnlQuickSelect.Controls.Add(_btnSelectRunning);

            _chkListProfiles = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F)
            };
            _chkListProfiles.ItemCheck += delegate(object s, ItemCheckEventArgs e)
            {
                this.BeginInvoke(new Action(delegate
                {
                    var p = _chkListProfiles.Items[e.Index] as ProfileDisplayItem;
                    if (p != null)
                    {
                        if (e.NewValue == CheckState.Checked) _selectedProfileIds.Add(p.Profile.ProfileId);
                        else _selectedProfileIds.Remove(p.Profile.ProfileId);
                    }
                    UpdateProfileCount();
                }));
            };

            _lblProfileCount = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 25,
                Text = "Đã chọn: 0 / 0 profiles",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(75, 85, 99)
            };

            pnlLeft.Controls.Add(_chkListProfiles);
            pnlLeft.Controls.Add(pnlQuickSelect);
            pnlLeft.Controls.Add(_txtSearch);
            pnlLeft.Controls.Add(lblLeftHeader);
            pnlLeft.Controls.Add(_lblProfileCount);
            _splitMain.Panel1.Controls.Add(pnlLeft);

            // ================= RIGHT: TABS & LOG SPLITTER =================
            _splitRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 370,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(229, 231, 235)
            };
            _splitMain.Panel2.Controls.Add(_splitRight);

            // TAB CONTROL
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };
            _splitRight.Panel1.Controls.Add(_tabs);

            CreateNuoiFeedTab();
            CreateFollowTab();
            CreatePostTab();

            // LOG & ACTION PANEL (BOTTOM)
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            Panel pnlAction = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = Color.FromArgb(249, 250, 251),
                Padding = new Padding(8, 6, 8, 6)
            };

            Label lblThread = new Label { Text = "Số luồng:", AutoSize = true, Location = new Point(10, 12), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numThreads = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 2, Location = new Point(78, 10), Width = 55 };

            _btnStart = new Button
            {
                Text = "▶ BẮT ĐẦU CHẠY",
                Location = new Point(148, 6),
                Size = new Size(150, 32),
                BackColor = Color.FromArgb(131, 58, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnStart.FlatAppearance.BorderSize = 0;
            _btnStart.Click += delegate { StartAutomation(); };

            _btnStop = new Button
            {
                Text = "⏹ DỪNG",
                Location = new Point(306, 6),
                Size = new Size(95, 32),
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _btnStop.FlatAppearance.BorderSize = 0;
            _btnStop.Click += delegate { StopAutomation(); };

            _lblRunningStatus = new Label
            {
                Text = "Sẵn sàng",
                Location = new Point(415, 12),
                AutoSize = true,
                ForeColor = Color.FromArgb(107, 114, 128),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };

            _btnClearLog = new Button { Text = "Xóa log", Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(710, 8), Size = new Size(70, 26), FlatStyle = FlatStyle.Flat };
            _btnClearLog.Click += delegate { _rtbLog.Clear(); };

            _btnCopyLog = new Button { Text = "Copy log", Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(786, 8), Size = new Size(75, 26), FlatStyle = FlatStyle.Flat };
            _btnCopyLog.Click += delegate
            {
                if (!string.IsNullOrEmpty(_rtbLog.Text))
                {
                    Clipboard.SetText(_rtbLog.Text);
                    MessageBox.Show("Đã copy toàn bộ nhật ký vào clipboard!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            pnlAction.Controls.Add(lblThread);
            pnlAction.Controls.Add(_numThreads);
            pnlAction.Controls.Add(_btnStart);
            pnlAction.Controls.Add(_btnStop);
            pnlAction.Controls.Add(_lblRunningStatus);
            pnlAction.Controls.Add(_btnClearLog);
            pnlAction.Controls.Add(_btnCopyLog);

            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            pnlBottom.Controls.Add(_rtbLog);
            pnlBottom.Controls.Add(pnlAction);
            _splitRight.Panel2.Controls.Add(pnlBottom);
        }

        private void CreateNuoiFeedTab()
        {
            TabPage tab = new TabPage("🌟 Nuôi Nick & Reels");
            tab.BackColor = Color.White;
            tab.Padding = new Padding(16);

            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 5,
                AutoSize = true,
                Padding = new Padding(10)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            table.Controls.Add(new Label { Text = "Thời gian lướt Feed (giây):", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, 0, 0);
            _numFeedDuration = new NumericUpDown { Minimum = 10, Maximum = 1800, Value = 60, Width = 100 };
            table.Controls.Add(_numFeedDuration, 1, 0);

            _chkAutoLikeFeed = new CheckBox { Text = "Tự động thả tim bài viết ngẫu nhiên", Checked = true, AutoSize = true, Anchor = AnchorStyles.Left };
            table.Controls.Add(_chkAutoLikeFeed, 0, 1);
            table.SetColumnSpan(_chkAutoLikeFeed, 2);

            table.Controls.Add(new Label { Text = "Số lượt tim tối đa:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
            _numMaxLikesFeed = new NumericUpDown { Minimum = 1, Maximum = 50, Value = 5, Width = 100 };
            table.Controls.Add(_numMaxLikesFeed, 1, 2);

            _chkWatchReels = new CheckBox { Text = "Kết hợp xem Instagram Reels (Shorts video)", Checked = true, AutoSize = true, Anchor = AnchorStyles.Left };
            table.Controls.Add(_chkWatchReels, 0, 3);
            table.SetColumnSpan(_chkWatchReels, 2);

            table.Controls.Add(new Label { Text = "Số lượng video Reels xem:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
            _numReelCount = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 5, Width = 100 };
            table.Controls.Add(_numReelCount, 1, 4);

            Label lblDesc = new Label
            {
                Text = "💡 Tính năng: Cuộn trang mô phỏng người dùng thật (Human behavior), tự động dừng đọc bài 3-6s, thả tim ngẫu nhiên và xem Reels để tăng độ Trust cho tài khoản Instagram.",
                ForeColor = Color.FromArgb(75, 85, 99),
                Dock = DockStyle.Bottom,
                Height = 45,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            tab.Controls.Add(table);
            tab.Controls.Add(lblDesc);
            _tabs.TabPages.Add(tab);
        }

        private void CreateFollowTab()
        {
            TabPage tab = new TabPage("➕ Auto Follow");
            tab.BackColor = Color.White;
            tab.Padding = new Padding(16);

            Label lblHeader = new Label
            {
                Text = "Tự động điều hướng và Theo dõi (Follow) tài khoản Instagram mục tiêu:",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            _txtFollowTarget = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9.5F),
                Text = "instagram"
            };

            Label lblHelp = new Label
            {
                Text = "Nhập username (ví dụ: 'cristiano' hoặc '@nike') hoặc đường dẫn URL đầy đủ (ví dụ: 'https://www.instagram.com/cristiano/').",
                Dock = DockStyle.Top,
                Height = 35,
                ForeColor = Color.FromArgb(107, 114, 128),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            tab.Controls.Add(lblHelp);
            tab.Controls.Add(_txtFollowTarget);
            tab.Controls.Add(lblHeader);
            _tabs.TabPages.Add(tab);
        }

        private void CreatePostTab()
        {
            TabPage tab = new TabPage("📸 Đăng Bài Viết");
            tab.BackColor = Color.White;
            tab.Padding = new Padding(16);

            Label lblImg = new Label { Text = "Chọn tệp hình ảnh để tải lên (.jpg, .png, .jpeg):", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            Panel pnlImg = new Panel { Dock = DockStyle.Top, Height = 34 };
            _txtImagePath = new TextBox { Location = new Point(0, 4), Width = 480, Font = new Font("Segoe UI", 9F) };
            _btnBrowseImage = new Button { Text = "📁 Duyệt ảnh...", Location = new Point(488, 3), Width = 110, Height = 26, FlatStyle = FlatStyle.Flat };
            _btnBrowseImage.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|All files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        _txtImagePath.Text = ofd.FileName;
                    }
                }
            };
            pnlImg.Controls.Add(_txtImagePath);
            pnlImg.Controls.Add(_btnBrowseImage);

            Label lblCaption = new Label { Text = "Nội dung bài viết (Hỗ trợ cấu trúc Spintax {A|B|C}):", Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            _txtPostCaption = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9.5F),
                Text = "{Chào mừng|Xin chào|Hello} cả nhà! Chúc mọi người {ngày mới tràn đầy năng lượng|cuối tuần vui vẻ|buổi tối an lành}! #lifestyle #photooftheday"
            };

            Panel pnlBottomPost = new Panel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 6, 0, 0) };
            _btnPreviewSpintax = new Button { Text = "🎲 Test ngẫu nhiên Spintax", Width = 180, Height = 28, FlatStyle = FlatStyle.Flat };
            _btnPreviewSpintax.Click += delegate
            {
                string preview = SpintaxHelper.Process(_txtPostCaption.Text);
                MessageBox.Show("Mẫu văn bản ngẫu nhiên:\n\n" + preview, "Xem trước Spintax", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            pnlBottomPost.Controls.Add(_btnPreviewSpintax);

            tab.Controls.Add(_txtPostCaption);
            tab.Controls.Add(pnlBottomPost);
            tab.Controls.Add(lblCaption);
            tab.Controls.Add(pnlImg);
            tab.Controls.Add(lblImg);

            _tabs.TabPages.Add(tab);
        }

        private void LoadProfileList()
        {
            string keyword = _txtSearch.Text.Trim();
            if (keyword == "🔍 Tìm kiếm profile...") keyword = "";

            _chkListProfiles.BeginUpdate();
            _chkListProfiles.Items.Clear();

            var filtered = _allProfiles.Where(p =>
                string.IsNullOrEmpty(keyword) ||
                (p.ProfileName != null && p.ProfileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (p.Username != null && p.Username.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (p.ProfileId != null && p.ProfileId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (p.Notes != null && p.Notes.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            ).ToList();

            for (int i = 0; i < filtered.Count; i++)
            {
                var profile = filtered[i];
                var item = new ProfileDisplayItem(profile);
                bool isChecked = _selectedProfileIds.Contains(profile.ProfileId);
                _chkListProfiles.Items.Add(item, isChecked);
            }

            _chkListProfiles.EndUpdate();
            UpdateProfileCount();
        }

        private void UpdateProfileCount()
        {
            _lblProfileCount.Text = string.Format("Đã chọn: {0} / {1} profiles", _selectedProfileIds.Count, _allProfiles.Count);
        }

        private List<UserProfile> GetSelectedProfiles()
        {
            return _allProfiles.Where(p => _selectedProfileIds.Contains(p.ProfileId)).ToList();
        }

        private void Log(string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(Log), msg);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _rtbLog.AppendText(string.Format("[{0}] {1}\r\n", timestamp, msg));
            _rtbLog.ScrollToCaret();
        }

        private void StartAutomation()
        {
            if (_isRunning) return;
            var selected = GetSelectedProfiles();
            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 profile để thực thi!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selectedTab = _tabs.SelectedIndex;

            if (selectedTab == 1 && string.IsNullOrWhiteSpace(_txtFollowTarget.Text))
            {
                MessageBox.Show("Vui lòng nhập tài khoản Instagram cần Follow!", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (selectedTab == 2)
            {
                if (string.IsNullOrWhiteSpace(_txtImagePath.Text) || !File.Exists(_txtImagePath.Text))
                {
                    MessageBox.Show("Vui lòng chọn đường dẫn tệp ảnh hợp lệ để đăng lên Instagram!", "Thiếu ảnh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _lblRunningStatus.Text = "Đang chạy automation...";
            _lblRunningStatus.ForeColor = Color.FromArgb(131, 58, 180);

            int maxThreads = (int)_numThreads.Value;
            var token = _cts.Token;

            // Param snapshot
            int feedDuration = (int)_numFeedDuration.Value;
            bool autoLikeFeed = _chkAutoLikeFeed.Checked;
            int maxLikes = (int)_numMaxLikesFeed.Value;
            bool watchReels = _chkWatchReels.Checked;
            int reelCount = (int)_numReelCount.Value;

            string followTarget = _txtFollowTarget.Text.Trim();
            string imgPath = _txtImagePath.Text.Trim();
            string caption = _txtPostCaption.Text;

            Log(string.Format("=== BẮT ĐẦU CHIẾN DỊCH INSTAGRAM ({0} PROFILES, {1} LUỒNG) ===", selected.Count, maxThreads));

            Task.Factory.StartNew(delegate
            {
                using (var semaphore = new SemaphoreSlim(maxThreads))
                {
                    var tasks = new List<Task>();

                    foreach (var prof in selected)
                    {
                        if (token.IsCancellationRequested) break;

                        var currentProf = prof;
                        semaphore.Wait(token);

                        var t = Task.Factory.StartNew(delegate
                        {
                            try
                            {
                                RunSingleProfile(currentProf, selectedTab, feedDuration, autoLikeFeed, maxLikes, watchReels, reelCount, followTarget, imgPath, caption, token);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }, token);

                        tasks.Add(t);
                    }

                    Task.WaitAll(tasks.ToArray());
                }

                this.BeginInvoke(new Action(delegate
                {
                    _isRunning = false;
                    _btnStart.Enabled = true;
                    _btnStop.Enabled = false;
                    _lblRunningStatus.Text = token.IsCancellationRequested ? "Đã dừng bởi người dùng" : "Hoàn tất chiến dịch!";
                    _lblRunningStatus.ForeColor = Color.FromArgb(16, 185, 129);
                    Log("=== TẤT CẢ TIẾN TRÌNH INSTAGRAM ĐÃ KẾT THÚC ===");
                }));
            }, token);
        }

        private void RunSingleProfile(
            UserProfile profile,
            int tabIndex,
            int feedDuration,
            bool autoLikeFeed,
            int maxLikes,
            bool watchReels,
            int reelCount,
            string followTarget,
            string imgPath,
            string caption,
            CancellationToken token)
        {
            string profTag = string.Format("[{0}]", profile.ProfileName);
            Log(string.Format("{0} Đang khởi động trình duyệt...", profTag));

            IWebDriver driver = null;
            try
            {
                driver = BrowserSessionManager.Instance.GetDriver(profile.ProfileId);
                if (driver == null)
                {
                    driver = _launcherService.LaunchBrowser(profile);
                }

                if (driver == null)
                {
                    Log(string.Format("{0} Không thể mở trình duyệt profile!", profTag));
                    return;
                }

                Action<string> logger = delegate(string msg)
                {
                    Log(string.Format("{0} {1}", profTag, msg));
                };

                if (tabIndex == 0) // Nuoi nick & Reels
                {
                    _instaService.SurfFeed(driver, feedDuration, autoLikeFeed, maxLikes, logger, token);
                    if (watchReels && !token.IsCancellationRequested)
                    {
                        _instaService.WatchReels(driver, reelCount, logger, token);
                    }
                }
                else if (tabIndex == 1) // Follow
                {
                    _instaService.FollowUser(driver, followTarget, logger);
                }
                else if (tabIndex == 2) // Dang anh
                {
                    _instaService.CreatePost(driver, imgPath, caption, logger);
                }

                Log(string.Format("{0} Đã hoàn thành kịch bản thành công.", profTag));
            }
            catch (Exception ex)
            {
                Log(string.Format("{0} Lỗi ngoại lệ: {1}", profTag, ex.Message));
            }
        }

        private void StopAutomation()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                Log("[*] Đang gửi tín hiệu dừng tới tất cả các luồng...");
            }
        }

        private class ProfileDisplayItem
        {
            public UserProfile Profile { get; private set; }

            public ProfileDisplayItem(UserProfile profile)
            {
                Profile = profile;
            }

            public override string ToString()
            {
                string proxyStr = string.IsNullOrEmpty(Profile.Proxy) ? "Direct" : Profile.Proxy;
                return string.Format("{0} ({1})", Profile.ProfileName, proxyStr);
            }
        }
    }
}
