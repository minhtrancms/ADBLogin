using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ADBLogin.Core.Automation;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;
using ADBLogin.UI;

namespace ADBLogin.Tests
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("[TEST 1] Testing TotpGenerator & SpintaxHelper...");
            string otp = TotpGenerator.GenerateTotpCode("JBSWY3DPEHPK3PXP");
            Console.WriteLine("  -> TOTP: " + otp);
            string spun = SpintaxHelper.Spin("{Shopee|Twitter|Telegram} Automation!");
            Console.WriteLine("  -> Spun: " + spun);

            var profiles = new List<UserProfile>
            {
                new UserProfile { ProfileId = "test_profile_1", ProfileName = "Test Account 1", Username = "user1@domain.com" },
                new UserProfile { ProfileId = "test_profile_2", ProfileName = "Test Account 2", Username = "user2@domain.com" }
            };

            Console.WriteLine("[TEST 2] Instantiating FacebookAutomationForm...");
            using (var f = new FacebookAutomationForm(profiles)) { IntPtr h = f.Handle; Console.WriteLine("  -> FB Form Handle: " + h); }

            Console.WriteLine("[TEST 3] Instantiating GoogleAutomationForm...");
            using (var f = new GoogleAutomationForm(profiles)) { IntPtr h = f.Handle; Console.WriteLine("  -> Google Form Handle: " + h); }

            Console.WriteLine("[TEST 4] Instantiating TikTokAutomationForm...");
            using (var f = new TikTokAutomationForm(profiles)) { IntPtr h = f.Handle; Console.WriteLine("  -> TikTok Form Handle: " + h); }

            Console.WriteLine("[TEST 5] Instantiating ShopeeAutomationForm...");
            using (var f = new ShopeeAutomationForm(profiles)) { IntPtr h = f.Handle; Console.WriteLine("  -> Shopee Form Handle: " + h); }

            Console.WriteLine("[TEST 6] Instantiating TwitterAutomationForm...");
            using (var f = new TwitterAutomationForm(profiles)) { IntPtr h = f.Handle; Console.WriteLine("  -> Twitter Form Handle: " + h); }

            Console.WriteLine("[TEST 7] Instantiating TelegramAutomationForm...");
            using (var f = new TelegramAutomationForm(profiles)) { IntPtr h = f.Handle; Console.WriteLine("  -> Telegram Form Handle: " + h); }

            Console.WriteLine("[TEST 8] Instantiating SynchronizerForm...");
            using (var f = new SynchronizerForm(profiles)) { IntPtr h = f.Handle; Console.WriteLine("  -> Synchronizer Form Handle: " + h); }

            Console.WriteLine("[TEST 9] Instantiating AdvancedSettingsForm...");
            using (var f = new AdvancedSettingsForm()) { IntPtr h = f.Handle; Console.WriteLine("  -> Advanced Settings Form Handle: " + h); }

            Console.WriteLine("[TEST 10] Testing ProxyRotatorService & CaptchaSolverService initializations...");
            var rotator = new ProxyRotatorService();
            var captcha = new CaptchaSolverService();
            Console.WriteLine("  -> Services initialized successfully!");

            Console.WriteLine("[TEST 11] Instantiating MainForm (2-Tier UI Toolbar)...");
            using (var f = new MainForm())
            {
                IntPtr h = f.Handle;
                Console.WriteLine("  -> MainForm Handle: " + h + ", Control Count: " + f.Controls.Count);
            }

            Console.WriteLine("\n=======================================================");
            Console.WriteLine("  [ALL 11 TESTS PASSED WITH 100% SUCCESSFUL INITIALIZATION!]");
            Console.WriteLine("=======================================================");
        }
    }
}
