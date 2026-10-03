using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using OpenQA.Selenium;

namespace ADBLogin.Core.Services
{
    /// <summary>
    /// Service Đồng bộ Chuột & Phím thời gian thực (Multi-Control Synchronizer)
    /// Điều khiển 1 trình duyệt Master -> Tự động đồng bộ sang tất cả trình duyệt Slaves
    /// </summary>
    public class SynchronizerService
    {
        private static SynchronizerService _instance;
        public static SynchronizerService Instance
        {
            get
            {
                if (_instance == null) _instance = new SynchronizerService();
                return _instance;
            }
        }

        public string MasterProfileId { get; set; }
        public List<string> SlaveProfileIds { get; set; }
        public bool IsSyncing { get; private set; }

        public SynchronizerService()
        {
            SlaveProfileIds = new List<string>();
        }

        public void StartSync(string masterId, List<string> slaveIds)
        {
            MasterProfileId = masterId;
            SlaveProfileIds = slaveIds ?? new List<string>();
            IsSyncing = true;
        }

        public void StopSync()
        {
            IsSyncing = false;
        }

        /// <summary>
        /// Đồng bộ điều hướng tất cả các máy sang cùng một URL
        /// </summary>
        public void SyncNavigate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            Parallel.ForEach(SlaveProfileIds, (id) =>
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetSession(id);
                    if (driver != null)
                    {
                        driver.Navigate().GoToUrl(url);
                    }
                }
                catch { }
            });
        }

        /// <summary>
        /// Đồng bộ cuộn trang đồng thời trên tất cả các máy
        /// </summary>
        public void SyncScroll(int deltaY)
        {
            Parallel.ForEach(SlaveProfileIds, (id) =>
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetSession(id);
                    if (driver != null)
                    {
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                        js.ExecuteScript(string.Format("window.scrollBy({{ top: {0}, behavior: 'smooth' }});", deltaY));
                    }
                }
                catch { }
            });
        }

        /// <summary>
        /// Đồng bộ gửi phím hoặc văn bản vào phần tử đang focus trên tất cả các máy
        /// </summary>
        public void SyncSendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            Parallel.ForEach(SlaveProfileIds, (id) =>
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetSession(id);
                    if (driver != null)
                    {
                        var active = driver.SwitchTo().ActiveElement();
                        if (active != null)
                        {
                            active.SendKeys(text);
                        }
                    }
                }
                catch { }
            });
        }

        /// <summary>
        /// Đồng bộ bấm chuột theo tọa độ tỷ lệ phần trăm (0 - 100%) của màn hình
        /// </summary>
        public void SyncClickPercent(double xPercent, double yPercent)
        {
            Parallel.ForEach(SlaveProfileIds, (id) =>
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetSession(id);
                    if (driver != null)
                    {
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                        string script = string.Format(@"
                            var x = window.innerWidth * {0};
                            var y = window.innerHeight * {1};
                            var el = document.elementFromPoint(x, y);
                            if (el) el.click();
                        ", xPercent.ToString(System.Globalization.CultureInfo.InvariantCulture), yPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        js.ExecuteScript(script);
                    }
                }
                catch { }
            });
        }

        /// <summary>
        /// Đồng bộ thực thi JavaScript trên tất cả các máy
        /// </summary>
        public void SyncExecuteScript(string script)
        {
            if (string.IsNullOrEmpty(script)) return;

            Parallel.ForEach(SlaveProfileIds, (id) =>
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetSession(id);
                    if (driver != null)
                    {
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                        js.ExecuteScript(script);
                    }
                }
                catch { }
            });
        }

        /// <summary>
        /// Làm mới (Refresh) toàn bộ các máy cùng lúc
        /// </summary>
        public void SyncRefreshAll()
        {
            Parallel.ForEach(SlaveProfileIds, (id) =>
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetSession(id);
                    if (driver != null)
                    {
                        driver.Navigate().Refresh();
                    }
                }
                catch { }
            });
        }
    }
}
