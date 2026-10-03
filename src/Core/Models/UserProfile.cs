using System;

namespace ADBLogin.Core.Models
{
    public enum AccountTier
    {
        Standard,
        Premium,
        Unlimited
    }

    public class UserProfile
    {
        public string ProfileId { get; set; }
        public string ProfileName { get; set; }
        public string Username { get; set; }
        public AccountTier Tier { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public string Proxy { get; set; }
        public string UserAgent { get; set; }
        public string BrowserPath { get; set; }
        public string Notes { get; set; }

        public UserProfile()
        {
            ProfileId = Guid.NewGuid().ToString("N");
            ProfileName = "Default Profile";
            Username = "LocalUser";
            Tier = AccountTier.Unlimited;
            IsActive = true;
            CreatedDate = DateTime.Now;
            ExpiryDate = null;
            Proxy = string.Empty;
            UserAgent = string.Empty;
            BrowserPath = string.Empty;
            Notes = string.Empty;
        }

        public bool IsValid()
        {
            if (!IsActive) return false;
            if (ExpiryDate.HasValue && ExpiryDate.Value < DateTime.Now) return false;
            return true;
        }
    }
}
