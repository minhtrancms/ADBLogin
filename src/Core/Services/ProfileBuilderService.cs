using System;
using System.IO;
using ADBLogin.Core.Models;

namespace ADBLogin.Core.Services
{
    /// <summary>
    /// Service tao moi profile chuan hoa theo cau truc thu muc ADBLogin goc
    /// </summary>
    public class ProfileBuilderService
    {
        private readonly ChromiumPreferenceService _prefService = new ChromiumPreferenceService();

        /// <summary>
        /// Tao mot profile hoan chinh tren o dia tu mau zero_profile
        /// </summary>
        public UserProfile CreateProfile(string baseFolder, string profileName, string proxy = "", string userAgent = "", string note = "")
        {
            if (string.IsNullOrEmpty(baseFolder))
            {
                baseFolder = @"D:\Profiles";
            }

            if (!Directory.Exists(baseFolder))
            {
                Directory.CreateDirectory(baseFolder);
            }

            string profileDir = Path.Combine(baseFolder, profileName);
            if (!Directory.Exists(profileDir))
            {
                Directory.CreateDirectory(profileDir);
            }

            string baseAppDir = AppDomain.CurrentDomain.BaseDirectory;
            string templateDir = Path.Combine(baseAppDir, "Files", "zero_profile");

            // 1. Sao chep cau truc thu muc zero_profile sang profile moi
            if (Directory.Exists(templateDir))
            {
                CopyDirectory(templateDir, profileDir);
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(profileDir, "Default"));
            }

            // 2. Sao chep Extensions neu co
            string extSource = Path.Combine(baseAppDir, "Extensions");
            string extTarget = Path.Combine(profileDir, "Extensions");
            if (Directory.Exists(extSource))
            {
                CopyDirectory(extSource, extTarget);
            }

            // 3. Tao ID profile duy nhat (24 ky tu hex)
            string profileId = Guid.NewGuid().ToString("N").Substring(0, 24);

            // 4. Ghi cac tep thong tin chuan ADBLogin
            File.WriteAllText(Path.Combine(profileDir, "profile.txt"), profileId);
            File.WriteAllText(Path.Combine(profileDir, "proxy.txt"), string.IsNullOrEmpty(proxy) ? "none::::" : proxy);
            File.WriteAllText(Path.Combine(profileDir, "note.txt"), note ?? "");
            File.WriteAllText(Path.Combine(profileDir, "version.txt"), "144");

            // 5. Cap nhat Preferences cua Chromium
            ProxySettings proxySettings = ProxySettings.Parse(proxy);
            _prefService.UpdatePreferences(profileDir, proxySettings, userAgent);

            // 6. Tra ve doi tuong UserProfile
            var userProfile = new UserProfile
            {
                ProfileId = profileId,
                ProfileName = profileName,
                Username = profileName,
                Tier = AccountTier.Unlimited,
                IsActive = true,
                CreatedDate = DateTime.Now,
                ExpiryDate = null,
                Proxy = proxy,
                UserAgent = userAgent,
                BrowserPath = profileDir,
                Notes = note
            };

            return userProfile;
        }

        public static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            DirectoryInfo[] dirs = dir.GetDirectories();
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}
