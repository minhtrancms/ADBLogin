using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;

namespace ADBLogin.UI
{
    public class SchedulerManagerForm : Form
    {
        private DataGridView _dgvTasks;
        private Button _btnAdd;
        private Button _btnToggle;
        private Button _btnRunNow;
        private Button _btnDelete;
        private Button _btnRefresh;
        private RichTextBox _rtbLog;

        private readonly AutomationSchedulerService _scheduler = AutomationSchedulerService.Instance;
        private readonly AccountManager _accountMgr = AccountManager.Instance;

        public SchedulerManagerForm()
        {
            InitializeComponent();
            _scheduler.OnTaskExecutionProgress += Scheduler_OnProgress;
            _scheduler.OnTaskListChanged += Scheduler_OnListChanged;
            LoadGridData();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _scheduler.OnTaskExecutionProgress -= Scheduler_OnProgress;
            _scheduler.OnTaskListChanged -= Scheduler_OnListChanged;
            base.OnFormClosing(e);
        }

        private void InitializeComponent()
        {
            this.Text = "⏰ BỘ QUẢN LÝ LẬP LỊCH TỰ ĐỘNG (AUTOMATION SCHEDULER STUDIO)";
            this.Size = new Size(1150, 700);
            this.MinimumSize = new Size(950, 550);
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
                Text = "⏰ AUTOMATION SCHEDULER & BACKGROUND TASK QUEUE",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8)
            };

            Label lblSubtitle = new Label
            {
                Text = "Lập lịch tự động chạy nuôi nick Facebook, TikTok, Shopee, Google, Lazada... theo chu kỳ giờ, phút hoặc hẹn giờ cố định hàng ngày",
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(16, 32)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // 2. Toolbar
            Panel pnlTool = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Color.White,
                Padding = new Padding(10, 7, 10, 7)
            };

            _btnAdd = new Button
            {
                Text = "➕ Thêm Lịch Mới",
                Width = 140,
                Height = 32,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnAdd.FlatAppearance.BorderSize = 0;
            _btnAdd.Click += delegate { ShowAddTaskDialog(); };

            _btnToggle = new Button
            {
                Text = "⏯ Bật / Tắt Lịch",
                Location = new Point(155, 7),
                Width = 125,
                Height = 32,
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnToggle.FlatAppearance.BorderSize = 0;
            _btnToggle.Click += delegate { ToggleSelectedTask(); };

            _btnRunNow = new Button
            {
                Text = "▶ Chạy Ngay",
                Location = new Point(285, 7),
                Width = 110,
                Height = 32,
                BackColor = Color.FromArgb(139, 92, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnRunNow.FlatAppearance.BorderSize = 0;
            _btnRunNow.Click += delegate { RunSelectedTaskNow(); };

            _btnDelete = new Button
            {
                Text = "🗑️ Xóa Lịch",
                Location = new Point(400, 7),
                Width = 100,
                Height = 32,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnDelete.FlatAppearance.BorderSize = 0;
            _btnDelete.Click += delegate { DeleteSelectedTask(); };

            _btnRefresh = new Button
            {
                Text = "🔄 Làm Mới",
                Location = new Point(505, 7),
                Width = 95,
                Height = 32,
                FlatStyle = FlatStyle.Flat
            };
            _btnRefresh.Click += delegate { LoadGridData(); };

            pnlTool.Controls.Add(_btnAdd);
            pnlTool.Controls.Add(_btnToggle);
            pnlTool.Controls.Add(_btnRunNow);
            pnlTool.Controls.Add(_btnDelete);
            pnlTool.Controls.Add(_btnRefresh);
            this.Controls.Add(pnlTool);

            // 3. Main Split
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 380,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(229, 231, 235)
            };
            this.Controls.Add(split);
            split.BringToFront();

            // Grid
            _dgvTasks = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Segoe UI", 9F)
            };

            _dgvTasks.Columns.Add("Id", "ID");
            _dgvTasks.Columns["Id"].Visible = false;
            _dgvTasks.Columns.Add("Name", "Tên Lịch Trình");
            _dgvTasks.Columns.Add("Type", "Kịch Bản");
            _dgvTasks.Columns.Add("Profiles", "Số Profile");
            _dgvTasks.Columns.Add("Schedule", "Lịch Chạy");
            _dgvTasks.Columns.Add("NextRun", "Lần Chạy Tới");
            _dgvTasks.Columns.Add("Status", "Trạng Thái");
            _dgvTasks.Columns.Add("Result", "Kết Quả Gần Nhất");

            split.Panel1.Controls.Add(_dgvTasks);

            // Log Console
            Panel pnlLog = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8) };
            Label lblLogTitle = new Label { Text = "📝 NHẬT KÝ HOẠT ĐỘNG SCHEDULER", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(75, 85, 99) };

            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            pnlLog.Controls.Add(_rtbLog);
            pnlLog.Controls.Add(lblLogTitle);
            split.Panel2.Controls.Add(pnlLog);
        }

        private void LoadGridData()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(LoadGridData));
                return;
            }

            _dgvTasks.Rows.Clear();
            var tasks = _scheduler.GetAllTasks();

            foreach (var t in tasks)
            {
                string schedStr = "";
                if (t.Frequency == ScheduleFrequency.Once) schedStr = "Chạy 1 lần";
                else if (t.Frequency == ScheduleFrequency.IntervalMinutes) schedStr = string.Format("Mỗi {0} phút", t.IntervalValue);
                else if (t.Frequency == ScheduleFrequency.IntervalHours) schedStr = string.Format("Mỗi {0} giờ", t.IntervalValue);
                else if (t.Frequency == ScheduleFrequency.DailyAtTime) schedStr = string.Format("Hàng ngày lúc {0}", t.DailyTime);

                string statusStr = t.IsEnabled ? "🟢 ĐANG BẬT" : "⚪ ĐÃ TẮT";
                string nextRunStr = t.NextRunTime.HasValue ? t.NextRunTime.Value.ToString("HH:mm:ss dd/MM") : "--";
                string profCountStr = (t.TargetProfileIds != null && t.TargetProfileIds.Count > 0) ? string.Format("{0} profiles", t.TargetProfileIds.Count) : "Tất cả";

                int rowIdx = _dgvTasks.Rows.Add(
                    t.Id,
                    t.Name,
                    t.AutomationType.ToString(),
                    profCountStr,
                    schedStr,
                    nextRunStr,
                    statusStr,
                    t.LastResult
                );

                if (!t.IsEnabled)
                {
                    _dgvTasks.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.Gray;
                }
            }
        }

        private ScheduledTask GetSelectedTask()
        {
            if (_dgvTasks.SelectedRows.Count == 0) return null;
            string id = _dgvTasks.SelectedRows[0].Cells["Id"].Value as string;
            if (string.IsNullOrEmpty(id)) return null;
            return _scheduler.GetAllTasks().FirstOrDefault(t => t.Id == id);
        }

        private void ToggleSelectedTask()
        {
            var task = GetSelectedTask();
            if (task == null)
            {
                MessageBox.Show("Vui lòng chọn 1 lịch trình trong bảng!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _scheduler.ToggleTask(task.Id);
            LoadGridData();
        }

        private void RunSelectedTaskNow()
        {
            var task = GetSelectedTask();
            if (task == null)
            {
                MessageBox.Show("Vui lòng chọn 1 lịch trình trong bảng!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult dr = MessageBox.Show(string.Format("Bạn có muốn thực thi lịch trình \"{0}\" ngay bây giờ?", task.Name), "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr == DialogResult.Yes)
            {
                System.Threading.Tasks.Task.Factory.StartNew(delegate
                {
                    _scheduler.ExecuteScheduledTask(task);
                });
            }
        }

        private void DeleteSelectedTask()
        {
            var task = GetSelectedTask();
            if (task == null)
            {
                MessageBox.Show("Vui lòng chọn 1 lịch trình trong bảng!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult dr = MessageBox.Show(string.Format("Bạn có chắc chắn muốn xóa lịch trình \"{0}\"?", task.Name), "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr == DialogResult.Yes)
            {
                _scheduler.DeleteTask(task.Id);
                LoadGridData();
            }
        }

        private void ShowAddTaskDialog()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "➕ THÊM LỊCH TRÌNH AUTOMATION MỚI";
                dlg.Size = new Size(580, 520);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Color.White;
                dlg.Font = new Font("Segoe UI", 9F);

                TableLayoutPanel tbl = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 6,
                    Padding = new Padding(20)
                };
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                // 1. Tên
                tbl.Controls.Add(new Label { Text = "Tên lịch trình:", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, 0, 0);
                TextBox txtName = new TextBox { Dock = DockStyle.Fill, Text = "Nuôi nick tự động hàng ngày" };
                tbl.Controls.Add(txtName, 1, 0);

                // 2. Loại Kịch bản
                tbl.Controls.Add(new Label { Text = "Loại kịch bản:", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, 0, 1);
                ComboBox cboType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
                foreach (var v in Enum.GetValues(typeof(ScheduledAutomationType)))
                {
                    cboType.Items.Add(v);
                }
                cboType.SelectedIndex = 0;
                tbl.Controls.Add(cboType, 1, 1);

                // 3. Chu kỳ
                tbl.Controls.Add(new Label { Text = "Tần suất chạy:", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, 0, 2);
                ComboBox cboFreq = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
                cboFreq.Items.Add("Khoảng thời gian (Giờ)");
                cboFreq.Items.Add("Khoảng thời gian (Phút)");
                cboFreq.Items.Add("Cố định hàng ngày (Giờ:Phút)");
                cboFreq.Items.Add("Chạy 1 lần");
                cboFreq.SelectedIndex = 0;
                tbl.Controls.Add(cboFreq, 1, 2);

                // 4. Giá trị chu kỳ / Giờ chạy
                tbl.Controls.Add(new Label { Text = "Giá trị thời gian:", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, 0, 3);
                Panel pnlTimeVal = new Panel { Dock = DockStyle.Fill };
                NumericUpDown numVal = new NumericUpDown { Minimum = 1, Maximum = 720, Value = 4, Width = 80, Location = new Point(0, 2) };
                TextBox txtDaily = new TextBox { Width = 90, Text = "08:30", Location = new Point(0, 2), Visible = false };
                pnlTimeVal.Controls.Add(numVal);
                pnlTimeVal.Controls.Add(txtDaily);
                tbl.Controls.Add(pnlTimeVal, 1, 3);

                cboFreq.SelectedIndexChanged += delegate
                {
                    if (cboFreq.SelectedIndex == 2)
                    {
                        numVal.Visible = false;
                        txtDaily.Visible = true;
                    }
                    else
                    {
                        numVal.Visible = true;
                        txtDaily.Visible = false;
                        if (cboFreq.SelectedIndex == 1) numVal.Value = 30; // phut
                        else numVal.Value = 4; // gio
                    }
                };

                // 5. Chọn profile
                tbl.Controls.Add(new Label { Text = "Chọn Profile:", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, 0, 4);
                CheckedListBox chkProfiles = new CheckedListBox { Dock = DockStyle.Fill, Height = 140, CheckOnClick = true };
                var allProfs = _accountMgr.GetAllProfiles();
                foreach (var p in allProfs)
                {
                    chkProfiles.Items.Add(string.Format("{0} ({1})", p.ProfileName, p.ProfileId), true);
                }
                tbl.Controls.Add(chkProfiles, 1, 4);

                // 6. Buttons
                Panel pnlDlgBtns = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(12) };
                Button btnSave = new Button { Text = "LƯU LỊCH TRÌNH", Dock = DockStyle.Right, Width = 140, BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), DialogResult = DialogResult.OK };
                Button btnCancel = new Button { Text = "Hủy bỏ", Dock = DockStyle.Right, Width = 90, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
                pnlDlgBtns.Controls.Add(btnSave);
                pnlDlgBtns.Controls.Add(btnCancel);

                dlg.Controls.Add(tbl);
                dlg.Controls.Add(pnlDlgBtns);

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var task = new ScheduledTask();
                    task.Name = txtName.Text.Trim();
                    task.AutomationType = (ScheduledAutomationType)cboType.SelectedItem;

                    if (cboFreq.SelectedIndex == 0)
                    {
                        task.Frequency = ScheduleFrequency.IntervalHours;
                        task.IntervalValue = (int)numVal.Value;
                    }
                    else if (cboFreq.SelectedIndex == 1)
                    {
                        task.Frequency = ScheduleFrequency.IntervalMinutes;
                        task.IntervalValue = (int)numVal.Value;
                    }
                    else if (cboFreq.SelectedIndex == 2)
                    {
                        task.Frequency = ScheduleFrequency.DailyAtTime;
                        task.DailyTime = txtDaily.Text.Trim();
                    }
                    else
                    {
                        task.Frequency = ScheduleFrequency.Once;
                    }

                    task.TargetProfileIds = new List<string>();
                    for (int i = 0; i < chkProfiles.Items.Count; i++)
                    {
                        if (chkProfiles.GetItemChecked(i))
                        {
                            task.TargetProfileIds.Add(allProfs[i].ProfileId);
                        }
                    }

                    task.CalculateNextRun();
                    _scheduler.AddTask(task);
                    LoadGridData();
                    MessageBox.Show("Đã thêm lịch trình thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void Scheduler_OnProgress(ScheduledTask task, string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(delegate { Scheduler_OnProgress(task, msg); }));
                return;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            _rtbLog.AppendText(string.Format("[{0}] {1}\r\n", time, msg));
            _rtbLog.ScrollToCaret();
        }

        private void Scheduler_OnListChanged()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(Scheduler_OnListChanged));
                return;
            }
            LoadGridData();
        }
    }
}
