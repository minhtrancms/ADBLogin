using System;

namespace ADBLogin.Core.Models
{
    public class AppConfig
    {
        public bool IsOfflineMode { get; set; }
        public bool SkipServerAuthentication { get; set; }
        public bool AllowAutoUpdateCheck { get; set; }

        public string LicenseKey { get; set; }
        public string LicenseStatus { get; set; }
        public string PlanName { get; set; }
        public DateTime ActivationDate { get; set; }

        public bool SuppressNotificationPopups { get; set; }
        public bool EnableCoffeePromotionPopup { get; set; }
        public bool ShowFreeTierWarnings { get; set; }

        public string ProfilesDirectory { get; set; }
        public string ProxiesFilePath { get; set; }
        public string UserAgentFilePath { get; set; }
        public int MaxConcurrentBrowsers { get; set; }

        public AppConfig()
        {
            IsOfflineMode = true;
            SkipServerAuthentication = true;
            AllowAutoUpdateCheck = false;

            LicenseKey = "STANDALONE_LIFETIME_ACCESS";
            LicenseStatus = "Activated";
            PlanName = "Enterprise Unlimited";
            ActivationDate = DateTime.Now;

            SuppressNotificationPopups = true;
            EnableCoffeePromotionPopup = false;
            ShowFreeTierWarnings = false;

            ProfilesDirectory = "Files/zero_profile";
            ProxiesFilePath = "Files/Proxy.txt";
            UserAgentFilePath = "Files/UserAgent.txt";
            MaxConcurrentBrowsers = 50;
        }
    }
}
