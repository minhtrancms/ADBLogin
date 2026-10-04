using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ADBLogin.Core.Models;
using Newtonsoft.Json;
using OpenQA.Selenium;

namespace ADBLogin.Core.Services
{
    public class SyncEvent
    {
        public string type { get; set; }
        public double rx { get; set; }
        public double ry { get; set; }
        public string selector { get; set; }
        public string value { get; set; }
        public string key { get; set; }
        public int top { get; set; }
        public int left { get; set; }
    }

    /// <summary>
    /// Service Đồng bộ Chuột & Phím thời gian thực (Multi-Control Synchronizer)
    /// Điều khiển 1 trình duyệt Master -> Tự động bám theo và đồng bộ sang tất cả trình duyệt Slaves
    /// </summary>
    public class SynchronizerService
    {
        private static SynchronizerService _instance;
        private static readonly object _instanceLock = new object();

        public static SynchronizerService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null) _instance = new SynchronizerService();
                    }
                }
                return _instance;
            }
        }

        public string MasterProfileId { get; set; }
        public List<string> SlaveProfileIds { get; set; }
        public bool IsRealtimeSyncing { get; private set; }

        public event Action<string> OnSyncLog;
        public event Action<bool> OnSyncStateChanged;

        private CancellationTokenSource _syncCts;
        private readonly WindowManagerService _windowManager = new WindowManagerService();

        public SynchronizerService()
        {
            SlaveProfileIds = new List<string>();
        }

        private void Log(string msg)
        {
            if (OnSyncLog != null)
            {
                try { OnSyncLog(msg); } catch { }
            }
        }

        #region 1. BẬT / TẮT ĐỒNG BỘ THỜI GIAN THỰC (REALTIME MIRRORING)

        public void StartRealtimeSync(string masterId, List<string> slaveIds)
        {
            if (IsRealtimeSyncing) StopRealtimeSync();

            MasterProfileId = masterId;
            SlaveProfileIds = slaveIds != null ? new List<string>(slaveIds) : new List<string>();

            if (string.IsNullOrEmpty(MasterProfileId))
            {
                Log("[-] Chưa chọn máy Master!");
                return;
            }

            var masterDriver = BrowserSessionManager.Instance.GetDriver(MasterProfileId);
            if (masterDriver == null)
            {
                Log("[-] Trình duyệt Master chưa được mở! Vui lòng mở Master trước.");
                return;
            }

            IsRealtimeSyncing = true;
            _syncCts = new CancellationTokenSource();
            var token = _syncCts.Token;

            BrowserSessionManager.Instance.SetRunningTask(MasterProfileId, "Đồng bộ: 👑 Master");
            foreach (var sid in SlaveProfileIds)
            {
                BrowserSessionManager.Instance.SetRunningTask(sid, "Đồng bộ: ⚡ Slave");
            }

            if (OnSyncStateChanged != null) OnSyncStateChanged(true);
            Log(string.Format("[*] ĐÃ BẬT ĐỒNG BỘ THỜI GIAN THỰC! Master: {0} -> {1} Slaves", MasterProfileId, SlaveProfileIds.Count));

            Task.Factory.StartNew(delegate
            {
                string lastMasterUrl = "";
                try { lastMasterUrl = masterDriver.Url; } catch { }

                while (!token.IsCancellationRequested && IsRealtimeSyncing)
                {
                    try
                    {
                        // Kiểm tra Master còn mở hay không
                        if (!BrowserSessionManager.Instance.IsRunning(MasterProfileId))
                        {
                            Log("[!] Trình duyệt Master đã bị đóng. Dừng đồng bộ.");
                            break;
                        }

                        IJavaScriptExecutor masterJs = masterDriver as IJavaScriptExecutor;
                        if (masterJs == null)
                        {
                            Thread.Sleep(200);
                            continue;
                        }

                        // 1. Kiểm tra URL thay đổi trên Master
                        string currentUrl = "";
                        try { currentUrl = masterDriver.Url; } catch { }
                        if (!string.IsNullOrEmpty(currentUrl) &&
                            currentUrl != lastMasterUrl &&
                            !currentUrl.StartsWith("data:") &&
                            !currentUrl.StartsWith("about:"))
                        {
                            lastMasterUrl = currentUrl;
                            Log(string.Format("[URL] Master đổi trang -> Đồng bộ: {0}", currentUrl));
                            SyncNavigate(currentUrl);
                        }

                        // 2. Cài đặt Hook trên Master nếu chưa có
                        InstallMasterHook(masterJs);

                        // 3. Đọc hàng đợi sự kiện từ Master
                        object rawEvents = masterJs.ExecuteScript(
                            "var q = window.__adb_sync_queue || []; window.__adb_sync_queue = []; return JSON.stringify(q);"
                        );

                        string jsonEvents = rawEvents as string;
                        if (!string.IsNullOrEmpty(jsonEvents) && jsonEvents != "[]")
                        {
                            var events = JsonConvert.DeserializeObject<List<SyncEvent>>(jsonEvents);
                            if (events != null && events.Count > 0)
                            {
                                foreach (var ev in events)
                                {
                                    if (token.IsCancellationRequested) break;
                                    BroadcastEventToSlaves(ev);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Bỏ qua lỗi tạm thời khi Master đang chuyển trang
                    }

                    Thread.Sleep(80);
                }

                IsRealtimeSyncing = false;
                if (OnSyncStateChanged != null) OnSyncStateChanged(false);
                Log("[*] Đã dừng đồng bộ thời gian thực.");
            }, token);
        }

        public void StopRealtimeSync()
        {
            if (_syncCts != null)
            {
                try { _syncCts.Cancel(); } catch { }
                _syncCts = null;
            }
            IsRealtimeSyncing = false;

            if (!string.IsNullOrEmpty(MasterProfileId))
            {
                BrowserSessionManager.Instance.ClearRunningTask(MasterProfileId);
            }
            if (SlaveProfileIds != null)
            {
                foreach (var sid in SlaveProfileIds)
                {
                    BrowserSessionManager.Instance.ClearRunningTask(sid);
                }
            }

            if (OnSyncStateChanged != null) OnSyncStateChanged(false);
        }

        private void InstallMasterHook(IJavaScriptExecutor masterJs)
        {
            string hookScript = @"
                if (!window.__adb_sync_installed) {
                    window.__adb_sync_installed = true;
                    window.__adb_sync_queue = [];

                    // 1. Hook Click
                    window.addEventListener('click', function(e) {
                        try {
                            var w = window.innerWidth || document.documentElement.clientWidth || 1;
                            var h = window.innerHeight || document.documentElement.clientHeight || 1;
                            var rx = e.clientX / w;
                            var ry = e.clientY / h;

                            var target = e.target;
                            var sel = '';
                            if (target) {
                                if (target.id) {
                                    sel = '#' + target.id;
                                } else if (target.name) {
                                    sel = '[name=""' + target.name + '""]';
                                } else if (target.className && typeof target.className === 'string') {
                                    var parts = target.className.trim().split(/\s+/).slice(0, 2);
                                    if (parts.length > 0 && parts[0]) sel = target.tagName.toLowerCase() + '.' + parts.join('.');
                                }
                            }

                            window.__adb_sync_queue.push({
                                type: 'click',
                                rx: rx,
                                ry: ry,
                                selector: sel
                            });
                        } catch(err){}
                    }, true);

                    // 2. Hook Scroll
                    var lastScrollTime = 0;
                    window.addEventListener('scroll', function(e) {
                        var now = Date.now();
                        if (now - lastScrollTime > 120) {
                            lastScrollTime = now;
                            var top = window.scrollY || document.documentElement.scrollTop || 0;
                            var left = window.scrollX || document.documentElement.scrollLeft || 0;
                            window.__adb_sync_queue.push({
                                type: 'scroll',
                                top: Math.round(top),
                                left: Math.round(left)
                            });
                        }
                    }, true);

                    // 3. Hook Input (Gõ chữ)
                    window.addEventListener('input', function(e) {
                        try {
                            var t = e.target;
                            if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable)) {
                                var val = t.value || t.innerText || '';
                                var sel = t.id ? ('#' + t.id) : (t.name ? ('[name=""' + t.name + '""]') : '');
                                window.__adb_sync_queue.push({
                                    type: 'input',
                                    value: val,
                                    selector: sel
                                });
                            }
                        } catch(err){}
                    }, true);

                    // 4. Hook Keydown (Enter, Backspace, Tab...)
                    window.addEventListener('keydown', function(e) {
                        try {
                            if (['Enter', 'Backspace', 'Escape', 'Tab', 'ArrowDown', 'ArrowUp'].indexOf(e.key) >= 0) {
                                window.__adb_sync_queue.push({
                                    type: 'key',
                                    key: e.key
                                });
                            }
                        } catch(err){}
                    }, true);
                }
            ";

            try
            {
                masterJs.ExecuteScript(hookScript);
            }
            catch { }
        }

        private void BroadcastEventToSlaves(SyncEvent ev)
        {
            if (ev == null || SlaveProfileIds == null || SlaveProfileIds.Count == 0) return;

            Parallel.ForEach(SlaveProfileIds, delegate(string slaveId)
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetDriver(slaveId);
                    if (driver == null) return;

                    IJavaScriptExecutor js = driver as IJavaScriptExecutor;
                    if (js == null) return;

                    if (ev.type == "click")
                    {
                        string clickScript = @"
                            (function(rx, ry, sel) {
                                var w = window.innerWidth || document.documentElement.clientWidth || 1;
                                var h = window.innerHeight || document.documentElement.clientHeight || 1;
                                var x = rx * w;
                                var y = ry * h;

                                var el = null;
                                if (sel) {
                                    try { el = document.querySelector(sel); } catch(e){}
                                }
                                if (!el) {
                                    el = document.elementFromPoint(x, y);
                                }

                                if (el) {
                                    var opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };
                                    el.dispatchEvent(new MouseEvent('mousedown', opts));
                                    el.dispatchEvent(new MouseEvent('mouseup', opts));
                                    el.dispatchEvent(new MouseEvent('click', opts));
                                    var clickable = el.closest ? el.closest('button, a, input, select, [role=""button""]') : null;
                                    if (clickable && clickable !== el) {
                                        clickable.dispatchEvent(new MouseEvent('click', opts));
                                        try { clickable.click(); } catch(e){}
                                    } else {
                                        try { el.click(); } catch(e){}
                                    }
                                }
                            })(arguments[0], arguments[1], arguments[2]);
                        ";
                        js.ExecuteScript(clickScript, ev.rx, ev.ry, ev.selector ?? "");
                    }
                    else if (ev.type == "scroll")
                    {
                        js.ExecuteScript(string.Format(CultureInfo.InvariantCulture, "window.scrollTo({{ left: {0}, top: {1}, behavior: 'instant' }});", ev.left, ev.top));
                    }
                    else if (ev.type == "input")
                    {
                        string inputScript = @"
                            (function(val, sel) {
                                var el = null;
                                if (sel) {
                                    try { el = document.querySelector(sel); } catch(e){}
                                }
                                if (!el) {
                                    el = document.activeElement;
                                }
                                if (el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA')) {
                                    el.value = val;
                                    el.dispatchEvent(new Event('input', { bubbles: true }));
                                    el.dispatchEvent(new Event('change', { bubbles: true }));
                                } else if (el && el.isContentEditable) {
                                    el.innerText = val;
                                    el.dispatchEvent(new Event('input', { bubbles: true }));
                                }
                            })(arguments[0], arguments[1]);
                        ";
                        js.ExecuteScript(inputScript, ev.value ?? "", ev.selector ?? "");
                    }
                    else if (ev.type == "key")
                    {
                        try
                        {
                            var active = driver.SwitchTo().ActiveElement();
                            if (active != null)
                            {
                                if (ev.key == "Enter") active.SendKeys(Keys.Enter);
                                else if (ev.key == "Backspace") active.SendKeys(Keys.Backspace);
                                else if (ev.key == "Tab") active.SendKeys(Keys.Tab);
                                else if (ev.key == "Escape") active.SendKeys(Keys.Escape);
                                else if (ev.key == "ArrowDown") active.SendKeys(Keys.ArrowDown);
                                else if (ev.key == "ArrowUp") active.SendKeys(Keys.ArrowUp);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            });
        }

        #endregion

        #region 2. CÁC LỆNH ĐIỀU HƯỚNG & THAO TÁC THỦ CÔNG CHUẨN XÁC

        public void SyncNavigate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            string targetUrl = url.Trim();
            if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                targetUrl = "https://" + targetUrl;
            }

            Parallel.ForEach(SlaveProfileIds, delegate(string id)
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetDriver(id);
                    if (driver != null)
                    {
                        driver.Navigate().GoToUrl(targetUrl);
                    }
                }
                catch { }
            });
        }

        public void SyncScroll(int deltaY)
        {
            Parallel.ForEach(SlaveProfileIds, delegate(string id)
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetDriver(id);
                    if (driver != null)
                    {
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                        js.ExecuteScript(string.Format(CultureInfo.InvariantCulture, "window.scrollBy({{ top: {0}, behavior: 'smooth' }}); document.documentElement.scrollTop += {0};", deltaY));
                    }
                }
                catch { }
            });
        }

        public void SyncSendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            Parallel.ForEach(SlaveProfileIds, delegate(string id)
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetDriver(id);
                    if (driver != null)
                    {
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                        string script = @"
                            var el = document.activeElement;
                            if (el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA')) {
                                el.value += arguments[0];
                                el.dispatchEvent(new Event('input', { bubbles: true }));
                                el.dispatchEvent(new Event('change', { bubbles: true }));
                            } else if (el && el.isContentEditable) {
                                document.execCommand('insertText', false, arguments[0]);
                            }
                        ";
                        js.ExecuteScript(script, text);
                    }
                }
                catch { }
            });
        }

        public void SyncClickPercent(double xPercent, double yPercent)
        {
            Parallel.ForEach(SlaveProfileIds, delegate(string id)
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetDriver(id);
                    if (driver != null)
                    {
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                        string script = @"
                            (function(rx, ry) {
                                var w = window.innerWidth || document.documentElement.clientWidth || 1;
                                var h = window.innerHeight || document.documentElement.clientHeight || 1;
                                var x = rx * w;
                                var y = ry * h;

                                var el = document.elementFromPoint(x, y);
                                if (el) {
                                    var opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };
                                    el.dispatchEvent(new MouseEvent('mousedown', opts));
                                    el.dispatchEvent(new MouseEvent('mouseup', opts));
                                    el.dispatchEvent(new MouseEvent('click', opts));
                                    var btn = el.closest ? el.closest('button, a, input, select, [role=""button""]') : null;
                                    if (btn && btn !== el) {
                                        btn.dispatchEvent(new MouseEvent('click', opts));
                                        try { btn.click(); } catch(e){}
                                    } else {
                                        try { el.click(); } catch(e){}
                                    }
                                }
                            })(arguments[0], arguments[1]);
                        ";
                        js.ExecuteScript(script, xPercent, yPercent);
                    }
                }
                catch { }
            });
        }

        public void SyncExecuteScript(string script)
        {
            if (string.IsNullOrEmpty(script)) return;

            Parallel.ForEach(SlaveProfileIds, delegate(string id)
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetDriver(id);
                    if (driver != null)
                    {
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                        js.ExecuteScript(script);
                    }
                }
                catch { }
            });
        }

        public void SyncRefreshAll(bool includeMaster = true)
        {
            var allIds = new List<string>(SlaveProfileIds);
            if (includeMaster && !string.IsNullOrEmpty(MasterProfileId))
            {
                allIds.Add(MasterProfileId);
            }

            Parallel.ForEach(allIds, delegate(string id)
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetDriver(id);
                    if (driver != null)
                    {
                        driver.Navigate().Refresh();
                    }
                }
                catch { }
            });
        }

        #endregion

        #region 3. TỰ ĐỘNG CHIA LƯỚI & SẮP XẾP CỬA SỔ (TILE GRID WINDOWS)

        public void TileWindows(string masterId, List<string> slaveIds, int rows = 0, int cols = 0)
        {
            var allIds = new List<string>();
            if (!string.IsNullOrEmpty(masterId)) allIds.Add(masterId);
            if (slaveIds != null)
            {
                foreach (var s in slaveIds)
                {
                    if (!allIds.Contains(s)) allIds.Add(s);
                }
            }

            if (allIds.Count == 0) return;

            int total = allIds.Count;
            int r = rows;
            int c = cols;

            if (r <= 0 || c <= 0)
            {
                if (total <= 2) { r = 1; c = 2; }
                else if (total <= 4) { r = 2; c = 2; }
                else if (total <= 6) { r = 2; c = 3; }
                else if (total <= 8) { r = 2; c = 4; }
                else if (total <= 12) { r = 3; c = 4; }
                else { r = 4; c = 4; }
            }

            for (int i = 0; i < allIds.Count; i++)
            {
                try
                {
                    var driver = BrowserSessionManager.Instance.GetDriver(allIds[i]);
                    if (driver != null)
                    {
                        var bounds = _windowManager.CalculateWindowBounds(i, r, c);
                        driver.Manage().Window.Position = new Point(bounds.X, bounds.Y);
                        driver.Manage().Window.Size = new Size(bounds.Width, bounds.Height);
                    }
                }
                catch { }
            }

            Log(string.Format("[*] Đã xếp lưới {0} cửa sổ ({1} hàng x {2} cột)", allIds.Count, r, c));
        }

        #endregion
    }
}
