using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADBLogin.Core.Models;
using Newtonsoft.Json;

namespace ADBLogin.Core.Services
{
    public enum ScheduleFrequency
    {
        Once,
        IntervalMinutes,
        IntervalHours,
        DailyAtTime
    }

    public enum ScheduledAutomationType
    {
        Facebook,
        TikTok,
        Google,
        Twitter,
        Telegram,
        Shopee,
        Discord,
        Instagram,
        Lazada,
        CustomBrowserOpen
    }

    public class ScheduledTask
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ScheduledAutomationType AutomationType { get; set; }
        public List<string> TargetProfileIds { get; set; }
        public ScheduleFrequency Frequency { get; set; }
        public int IntervalValue { get; set; } // minutes or hours
        public string DailyTime { get; set; } // "HH:mm" e.g. "08:30"
        public DateTime? NextRunTime { get; set; }
        public DateTime? LastRunTime { get; set; }
        public bool IsEnabled { get; set; }
        public string LastResult { get; set; }
        public string CustomUrl { get; set; }

        public ScheduledTask()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "Lịch chạy tự động";
            AutomationType = ScheduledAutomationType.Facebook;
            TargetProfileIds = new List<string>();
            Frequency = ScheduleFrequency.IntervalHours;
            IntervalValue = 4;
            DailyTime = "09:00";
            IsEnabled = true;
            LastResult = "Chưa chạy";
            CustomUrl = "https://google.com";
        }

        public void CalculateNextRun()
        {
            DateTime now = DateTime.Now;
            if (Frequency == ScheduleFrequency.Once)
            {
                if (!NextRunTime.HasValue || NextRunTime.Value <= now)
                {
                    NextRunTime = now.AddMinutes(5);
                }
            }
            else if (Frequency == ScheduleFrequency.IntervalMinutes)
            {
                int mins = IntervalValue > 0 ? IntervalValue : 30;
                NextRunTime = now.AddMinutes(mins);
            }
            else if (Frequency == ScheduleFrequency.IntervalHours)
            {
                int hrs = IntervalValue > 0 ? IntervalValue : 2;
                NextRunTime = now.AddHours(hrs);
            }
            else if (Frequency == ScheduleFrequency.DailyAtTime)
            {
                TimeSpan targetTime = TimeSpan.FromHours(9);
                if (!string.IsNullOrEmpty(DailyTime))
                {
                    TimeSpan parsed;
                    if (TimeSpan.TryParse(DailyTime, out parsed))
                    {
                        targetTime = parsed;
                    }
                }

                DateTime targetToday = DateTime.Today.Add(targetTime);
                if (targetToday <= now)
                {
                    NextRunTime = targetToday.AddDays(1);
                }
                else
                {
                    NextRunTime = targetToday;
                }
            }
        }
    }

    /// <summary>
    /// Service lập lịch tự động chạy các kịch bản Automation theo thời gian, chu kỳ hoặc hàng ngày
    /// </summary>
    public class AutomationSchedulerService
    {
        private static readonly string SchedulerFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "schedules.json");
        private readonly List<ScheduledTask> _tasks = new List<ScheduledTask>();
        private readonly object _lock = new object();
        private System.Threading.Timer _timer;
        private bool _isProcessing = false;

        private static AutomationSchedulerService _instance;
        private static readonly object _instanceLock = new object();

        public static AutomationSchedulerService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AutomationSchedulerService();
                        }
                    }
                }
                return _instance;
            }
        }

        public event Action<ScheduledTask, string> OnTaskExecutionProgress;
        public event Action OnTaskListChanged;

        public AutomationSchedulerService()
        {
            LoadTasks();
            StartTimer();
        }

        public List<ScheduledTask> GetAllTasks()
        {
            lock (_lock)
            {
                return new List<ScheduledTask>(_tasks);
            }
        }

        public void AddTask(ScheduledTask task)
        {
            if (task == null) return;
            lock (_lock)
            {
                if (task.NextRunTime == null || task.NextRunTime <= DateTime.Now)
                {
                    task.CalculateNextRun();
                }
                _tasks.Add(task);
                SaveTasks();
            }
            NotifyListChanged();
        }

        public void UpdateTask(ScheduledTask task)
        {
            if (task == null) return;
            lock (_lock)
            {
                var existing = _tasks.FirstOrDefault(t => t.Id == task.Id);
                if (existing != null)
                {
                    existing.Name = task.Name;
                    existing.AutomationType = task.AutomationType;
                    existing.TargetProfileIds = task.TargetProfileIds;
                    existing.Frequency = task.Frequency;
                    existing.IntervalValue = task.IntervalValue;
                    existing.DailyTime = task.DailyTime;
                    existing.IsEnabled = task.IsEnabled;
                    existing.CustomUrl = task.CustomUrl;
                    if (task.NextRunTime.HasValue) existing.NextRunTime = task.NextRunTime;
                    SaveTasks();
                }
            }
            NotifyListChanged();
        }

        public void DeleteTask(string taskId)
        {
            lock (_lock)
            {
                var existing = _tasks.FirstOrDefault(t => t.Id == taskId);
                if (existing != null)
                {
                    _tasks.Remove(existing);
                    SaveTasks();
                }
            }
            NotifyListChanged();
        }

        public void ToggleTask(string taskId)
        {
            lock (_lock)
            {
                var existing = _tasks.FirstOrDefault(t => t.Id == taskId);
                if (existing != null)
                {
                    existing.IsEnabled = !existing.IsEnabled;
                    if (existing.IsEnabled)
                    {
                        existing.CalculateNextRun();
                    }
                    SaveTasks();
                }
            }
            NotifyListChanged();
        }

        public void LoadTasks()
        {
            lock (_lock)
            {
                _tasks.Clear();
                try
                {
                    if (File.Exists(SchedulerFilePath))
                    {
                        string json = File.ReadAllText(SchedulerFilePath);
                        var loaded = JsonConvert.DeserializeObject<List<ScheduledTask>>(json);
                        if (loaded != null)
                        {
                            foreach (var item in loaded)
                            {
                                if (item.IsEnabled && (item.NextRunTime == null || item.NextRunTime <= DateTime.Now))
                                {
                                    item.CalculateNextRun();
                                }
                                _tasks.Add(item);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        public void SaveTasks()
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(_tasks, Formatting.Indented);
                    File.WriteAllText(SchedulerFilePath, json);
                }
                catch { }
            }
        }

        private void StartTimer()
        {
            // Kiểm tra mỗi 10 giây
            _timer = new System.Threading.Timer(TimerCallback, null, 10000, 10000);
        }

        private void TimerCallback(object state)
        {
            if (_isProcessing) return;

            List<ScheduledTask> dueTasks = new List<ScheduledTask>();
            DateTime now = DateTime.Now;

            lock (_lock)
            {
                foreach (var t in _tasks)
                {
                    if (t.IsEnabled && t.NextRunTime.HasValue && t.NextRunTime.Value <= now)
                    {
                        dueTasks.Add(t);
                    }
                }
            }

            if (dueTasks.Count > 0)
            {
                _isProcessing = true;
                Task.Factory.StartNew(delegate
                {
                    try
                    {
                        foreach (var task in dueTasks)
                        {
                            ExecuteScheduledTask(task);
                        }
                    }
                    finally
                    {
                        _isProcessing = false;
                    }
                });
            }
        }

        public void ExecuteScheduledTask(ScheduledTask task)
        {
            if (task == null) return;

            string startMsg = string.Format("[Scheduler] Bắt đầu thực thi lịch trình: \"{0}\" ({1})", task.Name, task.AutomationType);
            NotifyProgress(task, startMsg);

            DateTime runTime = DateTime.Now;
            int successCount = 0;
            int failCount = 0;

            try
            {
                var launcher = new BrowserLauncherService();
                var accountMgr = AccountManager.Instance;

                var profiles = new List<UserProfile>();
                if (task.TargetProfileIds != null && task.TargetProfileIds.Count > 0)
                {
                    foreach (var pid in task.TargetProfileIds)
                    {
                        var p = accountMgr.GetProfile(pid);
                        if (p != null) profiles.Add(p);
                    }
                }
                else
                {
                    profiles = accountMgr.GetAllProfiles();
                }

                NotifyProgress(task, string.Format("[Scheduler] Số lượng profile cần chạy: {0}", profiles.Count));

                foreach (var prof in profiles)
                {
                    try
                    {
                        NotifyProgress(task, string.Format("[Scheduler] Đang mở profile: {0}...", prof.ProfileName));
                        var driver = BrowserSessionManager.Instance.GetDriver(prof.ProfileId) ?? launcher.LaunchBrowser(prof);

                        if (driver != null)
                        {
                            // Thực hiện kịch bản theo loại
                            ExecuteActionByType(driver, task.AutomationType, task.CustomUrl, task);
                            successCount++;
                            NotifyProgress(task, string.Format("[Scheduler] [✓] Profile '{0}' hoàn thành tốt!", prof.ProfileName));
                        }
                        else
                        {
                            failCount++;
                            NotifyProgress(task, string.Format("[Scheduler] [-] Không thể khởi động profile '{0}'", prof.ProfileName));
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        NotifyProgress(task, string.Format("[Scheduler] [-] Lỗi profile '{0}': {1}", prof.ProfileName, ex.Message));
                    }

                    Thread.Sleep(2000);
                }

                task.LastResult = string.Format("Thành công: {0}/{1} (Lúc {2})", successCount, profiles.Count, runTime.ToString("HH:mm dd/MM"));
            }
            catch (Exception ex)
            {
                task.LastResult = string.Format("Lỗi: {0}", ex.Message);
                NotifyProgress(task, string.Format("[Scheduler] Ngoại lệ tổng: {0}", ex.Message));
            }
            finally
            {
                task.LastRunTime = runTime;
                if (task.Frequency == ScheduleFrequency.Once)
                {
                    task.IsEnabled = false;
                }
                else
                {
                    task.CalculateNextRun();
                }

                SaveTasks();
                NotifyListChanged();
                NotifyProgress(task, string.Format("[Scheduler] Hoàn tất lịch trình: \"{0}\". Kết quả: {1}", task.Name, task.LastResult));
            }
        }

        private void ExecuteActionByType(OpenQA.Selenium.IWebDriver driver, ScheduledAutomationType type, string customUrl, ScheduledTask task)
        {
            Action<string> log = delegate(string m) { NotifyProgress(task, "  -> " + m); };

            switch (type)
            {
                case ScheduledAutomationType.Facebook:
                    var fb = new Automation.FacebookAutomationService();
                    fb.SurfNewsfeed(driver, 45, true, 3, log, CancellationToken.None);
                    break;

                case ScheduledAutomationType.TikTok:
                    var tt = new Automation.TikTokAutomationService();
                    tt.SurfFyp(driver, 5, 5, 12, 40, false, log);
                    break;

                case ScheduledAutomationType.Google:
                    var gg = new Automation.GoogleAutomationService();
                    gg.SearchAndSeedGoogle(driver, "tin tuc cong nghe", "google.com", 30, log);
                    break;

                case ScheduledAutomationType.Shopee:
                    var sp = new Automation.ShopeeAutomationService();
                    sp.CheckInCoins(driver, log);
                    sp.CollectVouchers(driver, log);
                    break;

                case ScheduledAutomationType.Lazada:
                    var lz = new Automation.LazadaAutomationService();
                    lz.CheckinLazCoins(driver, log);
                    lz.CollectVouchers(driver, log);
                    break;

                case ScheduledAutomationType.Instagram:
                    var insta = new Automation.InstagramAutomationService();
                    insta.SurfFeed(driver, 45, true, 3, log, CancellationToken.None);
                    break;

                case ScheduledAutomationType.Twitter:
                    var tw = new Automation.TwitterAutomationService();
                    tw.SurfTimeline(driver, 5, 4, 10, 25, 10, log);
                    break;

                case ScheduledAutomationType.Telegram:
                    var tg = new Automation.TelegramAutomationService();
                    tg.OpenTelegramWeb(driver, log);
                    break;

                case ScheduledAutomationType.CustomBrowserOpen:
                default:
                    string url = string.IsNullOrEmpty(customUrl) ? "https://google.com" : customUrl;
                    log("Đang điều hướng tới: " + url);
                    driver.Navigate().GoToUrl(url);
                    Thread.Sleep(5000);
                    break;
            }
        }

        private void NotifyProgress(ScheduledTask task, string message)
        {
            if (OnTaskExecutionProgress != null)
            {
                try { OnTaskExecutionProgress(task, message); } catch { }
            }
        }

        private void NotifyListChanged()
        {
            if (OnTaskListChanged != null)
            {
                try { OnTaskListChanged(); } catch { }
            }
        }
    }
}
