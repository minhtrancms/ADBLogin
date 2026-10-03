using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenQA.Selenium;

namespace ADBLogin.Core.Services
{
    /// <summary>
    /// Quan ly va theo doi cac phien trinh duyet dang chay, ho tro phat hien va dong trinh duyet
    /// </summary>
    public class BrowserSessionManager
    {
        private static BrowserSessionManager _instance;
        private static readonly object _lock = new object();

        // Luu tru cac doi tuong IWebDriver theo ProfileId
        private readonly ConcurrentDictionary<string, IWebDriver> _activeDrivers = new ConcurrentDictionary<string, IWebDriver>();

        public static BrowserSessionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new BrowserSessionManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private BrowserSessionManager() { }

        /// <summary>
        /// Dang ky phien trinh duyet moi vua khoi chay
        /// </summary>
        public void RegisterSession(string profileId, IWebDriver driver)
        {
            if (string.IsNullOrEmpty(profileId) || driver == null) return;

            // Neu da co phien cu, dong truoc khi thay the
            CloseSession(profileId);

            _activeDrivers[profileId] = driver;
        }

        /// <summary>
        /// Kiem tra xem profile nay co dang mo trinh duyet hay khong
        /// </summary>
        public bool IsRunning(string profileId)
        {
            if (string.IsNullOrEmpty(profileId)) return false;

            IWebDriver driver;
            if (_activeDrivers.TryGetValue(profileId, out driver))
            {
                try
                {
                    // Kiem tra xem cua so con song hay nguoi dung da tat thu cong
                    var handles = driver.WindowHandles;
                    return handles != null && handles.Count > 0;
                }
                catch (Exception)
                {
                    // Cua so da bi tat, xoa khoi danh sach
                    IWebDriver removed;
                    _activeDrivers.TryRemove(profileId, out removed);
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Dong trinh duyet cua 1 profile cu the
        /// </summary>
        public bool CloseSession(string profileId)
        {
            if (string.IsNullOrEmpty(profileId)) return false;

            IWebDriver driver;
            if (_activeDrivers.TryRemove(profileId, out driver))
            {
                try
                {
                    driver.Quit();
                    driver.Dispose();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Dong tat ca cac phien trinh duyet dang mo
        /// </summary>
        public int CloseAllSessions()
        {
            int closedCount = 0;
            var keys = _activeDrivers.Keys.ToList();

            foreach (var key in keys)
            {
                if (CloseSession(key))
                {
                    closedCount++;
                }
            }

            return closedCount;
        }

        /// <summary>
        /// Lay danh sach tat ca cac ProfileId dang chay
        /// </summary>
        public List<string> GetRunningProfileIds()
        {
            var running = new List<string>();
            var keys = _activeDrivers.Keys.ToList();

            foreach (var key in keys)
            {
                if (IsRunning(key))
                {
                    running.Add(key);
                }
            }

            return running;
        }
    }
}
