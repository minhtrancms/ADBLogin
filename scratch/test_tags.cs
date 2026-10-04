using System;
using System.Drawing;
using System.Windows.Forms;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;
using ADBLogin.UI;

namespace ADBLogin.Tests
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            Console.WriteLine("[TEST TAG 1] UserProfile Tags default & assignment...");
            var p = new UserProfile();
            if (p.Tags != "") throw new Exception("Default Tags should be empty");
            p.Tags = "Facebook, Nuôi nick";
            Console.WriteLine("  -> Profile Tags: " + p.Tags);

            Console.WriteLine("[TEST TAG 2] BrowserSessionManager Task tracking...");
            var bsm = BrowserSessionManager.Instance;
            bsm.SetRunningTask("prof_123", "Auto FB: Lướt Newsfeed");
            string task = bsm.GetRunningTask("prof_123");
            if (task != "Auto FB: Lướt Newsfeed") throw new Exception("Task description mismatch: " + task);
            Console.WriteLine("  -> Active Task: " + task);

            bsm.ClearRunningTask("prof_123");
            if (bsm.GetRunningTask("prof_123") != null) throw new Exception("Task should be null after ClearRunningTask");
            Console.WriteLine("  -> Cleared successfully");

            Console.WriteLine("[TEST TAG 3] ProfileEditForm instantiation...");
            var editForm = new ProfileEditForm(p);
            IntPtr h = editForm.Handle;
            Console.WriteLine("  -> ProfileEditForm Handle: " + h);

            Console.WriteLine("[TEST TAG 4] MainForm instantiation with clTags & clRunningStatus...");
            var mf = new MainForm();
            IntPtr mh = mf.Handle;
            Console.WriteLine("  -> MainForm Handle: " + mh);

            Console.WriteLine("=== ALL TAG & STATUS TESTS PASSED! ===");
        }
    }
}
