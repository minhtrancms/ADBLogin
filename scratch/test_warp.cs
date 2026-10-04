using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;

namespace ADBLogin.Tests
{
    public class WarpTests
    {
        private static int _passed = 0;
        private static int _total = 0;

        private static void Check(string name, bool condition, string extra = "")
        {
            _total++;
            if (condition)
            {
                _passed++;
                Console.WriteLine(string.Format("  [PASS] {0} {1}", name, extra));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(string.Format("  [FAIL] {0} {1}", name, extra));
                Console.ResetColor();
            }
        }

        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=================================================");
            Console.WriteLine("🧪 RUNNING CLOUDFLARE WARP AUTOMATED TESTS");
            Console.WriteLine("=================================================");

            try
            {
                var service = CloudflareWarpService.Instance;
                Check("W1. CloudflareWarpService instance created", service != null);
                Check("W2. Tools available (wireproxy & wgcf)", service.AreToolsAvailable());

                service.InitializePorts(10811, 2);
                Check("W3. Initialized 2 WARP ports (10811, 10812)", service.PortItems.Count == 2);
                Check("W4. Port 1 is socks5://127.0.0.1:10811", service.PortItems[0].ProxyAddress == "socks5://127.0.0.1:10811");

                // Start port 10811
                Console.WriteLine("  [*] Đang khởi động cổng 10811 qua Cloudflare WARP...");
                bool started = service.StartPortAsync(10811).GetAwaiter().GetResult();
                Check("W5. Port 10811 started successfully", started && service.PortItems[0].Status == HmaTunnelStatus.Connected);

                // Check SOCKS5 connectivity
                var checker = new ProxyCheckerService();
                var res = checker.CheckProxyString("socks5://127.0.0.1:10811", 7000);
                Check("W6. SOCKS5 Proxy responds with clean Cloudflare IP", res.IsLive, "Result: " + res.ToString());

                // Assign to profile test
                var testProfile = new UserProfile { ProfileId = "p_warp", ProfileName = "WARP Profile" };
                int assigned = service.AssignProxiesToProfiles(new List<UserProfile> { testProfile }, new List<int> { 10811 });
                Check("W7. Assigned WARP proxy to profile", assigned == 1 && testProfile.Proxy == "socks5://127.0.0.1:10811");

                // Stop port
                service.StopPort(10811);
                Check("W8. Port 10811 stopped cleanly", service.PortItems[0].Status == HmaTunnelStatus.Stopped);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Exception in WARP tests: " + ex);
                Console.ResetColor();
            }

            Console.WriteLine(string.Format("\n📊 KẾT QUẢ KIỂM THỬ WARP: {0}/{1} TESTS PASSED ({2:P0})", _passed, _total, (double)_passed / Math.Max(1, _total)));
            Environment.Exit(_passed == _total ? 0 : 1);
        }
    }
}
