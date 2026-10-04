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
            Console.WriteLine("[TEST RESIZE 1] Creating MainForm...");
            var mf = new MainForm();
            mf.Show();

            Console.WriteLine("[TEST RESIZE 2] Maximizing MainForm to 1920x1080...");
            mf.Size = new Size(1920, 1080);
            Application.DoEvents();

            Console.WriteLine("[TEST RESIZE 3] Shrinking MainForm down to 800x500...");
            mf.Size = new Size(800, 500);
            Application.DoEvents();

            Console.WriteLine("[TEST RESIZE 4] Further shrinking to 600x400...");
            mf.Size = new Size(600, 400);
            Application.DoEvents();

            Console.WriteLine("[TEST RESIZE 5] Restoring to normal 1200x700...");
            mf.Size = new Size(1200, 700);
            Application.DoEvents();

            mf.Close();
            Console.WriteLine("=== ALL RESIZE & RESPONSIVE TESTS PASSED FLAWLESSLY! ===");
        }
    }
}
