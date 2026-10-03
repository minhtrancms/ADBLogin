using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADBLogin.Core.Models;
using Newtonsoft.Json;

namespace ADBLogin.Core.Services
{
    /// <summary>
    /// Quản lý hồ sơ người dùng và tài khoản trình duyệt (Hoàn toàn cục bộ, không đồng bộ cloud)
    /// </summary>
    public class AccountManager
    {
        private static readonly string ProfilesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles.json");
        private readonly List<UserProfile> _profiles = new List<UserProfile>();
        private readonly object _syncLock = new object();

        private static AccountManager _instance;
        private static readonly object _instanceLock = new object();

        public static AccountManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AccountManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public IReadOnlyList<UserProfile> Profiles
        {
            get
            {
                lock (_syncLock)
                {
                    return _profiles.AsReadOnly();
                }
            }
        }

        public List<UserProfile> GetAllProfiles()
        {
            lock (_syncLock)
            {
                return new List<UserProfile>(_profiles);
            }
        }

        public AccountManager()
        {
            LoadProfiles();
        }

        /// <summary>
        /// Nạp danh sách hồ sơ từ file profiles.json cục bộ
        /// </summary>
        public void LoadProfiles()
        {
            lock (_syncLock)
            {
                _profiles.Clear();
                try
                {
                    if (File.Exists(ProfilesFilePath))
                    {
                        string json = File.ReadAllText(ProfilesFilePath);
                        var loaded = JsonConvert.DeserializeObject<List<UserProfile>>(json);
                        if (loaded != null && loaded.Count > 0)
                        {
                            _profiles.AddRange(loaded);
                        }
                    }

                    // Nếu chưa có hồ sơ nào, tự động tạo hồ sơ mặc định với toàn quyền
                    if (_profiles.Count == 0)
                    {
                        CreateDefaultProfile();
                    }
                }
                catch (Exception)
                {
                    CreateDefaultProfile();
                }
            }
        }

        /// <summary>
        /// Tạo hồ sơ mặc định được mở khóa toàn quyền
        /// </summary>
        public UserProfile CreateDefaultProfile()
        {
            var defaultProfile = new UserProfile
            {
                ProfileId = "DEFAULT_ROOT",
                ProfileName = "Administrator (Offline Access)",
                Username = "Admin",
                Tier = AccountTier.Unlimited,
                IsActive = true,
                ExpiryDate = null
            };

            lock (_syncLock)
            {
                _profiles.Add(defaultProfile);
                SaveProfiles();
            }

            return defaultProfile;
        }

        /// <summary>
        /// Thêm hồ sơ mới (Mặc định cấp quyền cao nhất)
        /// </summary>
        public bool AddProfile(string profileName, string proxy = "", string userAgent = "")
        {
            lock (_syncLock)
            {
                var newProfile = new UserProfile
                {
                    ProfileId = Guid.NewGuid().ToString("N"),
                    ProfileName = profileName,
                    Username = profileName.Replace(" ", "_").ToLower(),
                    Tier = AccountTier.Unlimited,
                    IsActive = true,
                    ExpiryDate = null,
                    Proxy = proxy,
                    UserAgent = userAgent
                };

                _profiles.Add(newProfile);
                return SaveProfiles();
            }
        }

        /// <summary>
        /// Lấy hồ sơ theo ID
        /// </summary>
        public UserProfile GetProfile(string profileId)
        {
            lock (_syncLock)
            {
                return _profiles.FirstOrDefault(p => p.ProfileId == profileId);
            }
        }

        /// <summary>
        /// Cập nhật hoặc thêm mới hồ sơ
        /// </summary>
        public bool AddOrUpdateProfile(UserProfile profile)
        {
            if (profile == null) return false;

            lock (_syncLock)
            {
                var existing = _profiles.FirstOrDefault(p => p.ProfileId == profile.ProfileId);
                if (existing != null)
                {
                    existing.ProfileName = profile.ProfileName;
                    existing.Proxy = profile.Proxy;
                    existing.UserAgent = profile.UserAgent;
                    existing.Notes = profile.Notes;
                    existing.BrowserPath = profile.BrowserPath;
                    existing.Tier = profile.Tier;
                    existing.IsActive = profile.IsActive;
                }
                else
                {
                    _profiles.Add(profile);
                }

                return SaveProfiles();
            }
        }

        /// <summary>
        /// Xóa một hồ sơ theo ID
        /// </summary>
        public bool RemoveProfile(string profileId)
        {
            lock (_syncLock)
            {
                var existing = _profiles.FirstOrDefault(p => p.ProfileId == profileId);
                if (existing != null)
                {
                    _profiles.Remove(existing);
                    return SaveProfiles();
                }
                return false;
            }
        }

        /// <summary>
        /// Lưu danh sách hồ sơ xuống đĩa cục bộ
        /// </summary>
        public bool SaveProfiles()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_profiles, Formatting.Indented);
                File.WriteAllText(ProfilesFilePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
