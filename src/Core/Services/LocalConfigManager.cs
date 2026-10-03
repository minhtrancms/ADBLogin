using System;
using System.IO;
using ADBLogin.Core.Models;
using Newtonsoft.Json;

namespace ADBLogin.Core.Services
{
    /// <summary>
    /// Quản lý lưu trữ và nạp cấu hình hệ thống Offline-First từ tệp JSON nội bộ
    /// </summary>
    public class LocalConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private static LocalConfigManager _instance;
        private static readonly object _lock = new object();

        public AppConfig CurrentConfig { get; private set; }

        public static LocalConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LocalConfigManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private LocalConfigManager()
        {
            LoadConfig();
        }

        /// <summary>
        /// Nạp cấu hình từ tệp tin cục bộ. Nếu chưa có, tự động tạo cấu hình mặc định (Offline/Premium)
        /// </summary>
        public void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    CurrentConfig = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
                else
                {
                    CurrentConfig = new AppConfig();
                    SaveConfig();
                }
            }
            catch (Exception)
            {
                CurrentConfig = new AppConfig();
            }
        }

        /// <summary>
        /// Lưu cấu hình hiện tại xuống tệp config.json
        /// </summary>
        public bool SaveConfig()
        {
            try
            {
                string json = JsonConvert.SerializeObject(CurrentConfig, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Xác thực trạng thái bản quyền trực tiếp tại máy cục bộ (không gửi request mạng)
        /// </summary>
        public bool ValidateOfflineLicense()
        {
            if (CurrentConfig == null) return false;
            if (!CurrentConfig.IsOfflineMode) return false;
            return CurrentConfig.LicenseStatus.Equals("Activated", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kiểm tra xem có được phép hiển thị popup/quảng cáo hay không
        /// </summary>
        public bool ShouldShowPopup(string popupType)
        {
            if (CurrentConfig.SuppressNotificationPopups) return false;

            if (popupType.Equals("CoffeePromotion", StringComparison.OrdinalIgnoreCase) && !CurrentConfig.EnableCoffeePromotionPopup)
            {
                return false;
            }

            if (popupType.Equals("FreeTierWarning", StringComparison.OrdinalIgnoreCase) && !CurrentConfig.ShowFreeTierWarnings)
            {
                return false;
            }

            return true;
        }
    }
}
