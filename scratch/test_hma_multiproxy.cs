using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ADBLogin.Core.Models;
using ADBLogin.Core.Services;

namespace ADBLogin.Tests
{
    public class HmaMultiProxyTests
    {
        private static int _passed = 0;
        private static int _total = 0;

        private static void Assert(string testName, bool condition, string details = "")
        {
            _total++;
            if (condition)
            {
                _passed++;
                Console.WriteLine(string.Format("  [PASS] {0} {1}", testName, details));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(string.Format("  [FAIL] {0} {1}", testName, details));
                Console.ResetColor();
            }
        }

        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=================================================");
            Console.WriteLine("🧪 RUNNING HMA MULTI-PROXY AUTOMATED TESTS");
            Console.WriteLine("=================================================");

            try
            {
                var service = HmaMultiProxyService.Instance;
                Assert("T1. Service instance created", service != null);

                // Test 2: Initialize ports
                service.InitializePorts(10001, 3, new List<string>());
                Assert("T2. Initialized 3 ports (10001..10003)", service.PortItems.Count == 3);
                Assert("T3. Port 1 is 10001", service.PortItems[0].Port == 10001 && service.PortItems[0].ProxyAddress == "127.0.0.1:10001");
                Assert("T4. Port 2 is 10002", service.PortItems[1].Port == 10002 && service.PortItems[1].ProxyAddress == "127.0.0.1:10002");
                Assert("T5. Port 3 is 10003", service.PortItems[2].Port == 10003 && service.PortItems[2].ProxyAddress == "127.0.0.1:10003");

                // Test 6: Start local proxy on port 10001
                bool started = service.StartPortAsync(10001).GetAwaiter().GetResult();
                Assert("T6. Port 10001 started successfully", started && service.PortItems[0].Status == HmaTunnelStatus.Connected);

                // Test 7: Verify proxy connectivity through 127.0.0.1:10001
                var checker = new ProxyCheckerService();
                var result = checker.CheckProxyString("127.0.0.1:10001", 6000);
                Assert("T7. Proxy 127.0.0.1:10001 connects and passes HTTP traffic", result.IsLive, "Result: " + result.ToString());

                // Test 8: Assign proxy to profiles
                var p1 = new UserProfile { ProfileId = "test_p1", ProfileName = "Test Profile 1" };
                var p2 = new UserProfile { ProfileId = "test_p2", ProfileName = "Test Profile 2" };
                var testProfiles = new List<UserProfile> { p1, p2 };

                int assignedCount = service.AssignProxiesToProfiles(testProfiles, new List<int> { 10001, 10002 });
                Assert("T8. Assigned proxies to 2 profiles", assignedCount == 2);
                Assert("T9. Profile 1 assigned to 127.0.0.1:10001", p1.Proxy == "127.0.0.1:10001");
                Assert("T10. Profile 2 assigned to 127.0.0.1:10002", p2.Proxy == "127.0.0.1:10002");

                // Test 11: Stop port 10001
                service.StopPort(10001);
                Assert("T11. Port 10001 stopped cleanly", service.PortItems[0].Status == HmaTunnelStatus.Stopped);

                // Test 12: Config saving and loading
                service.Config.Username = "hma_test_user";
                service.Config.Password = "hma_test_pass";
                service.Config.StartPort = 10001;
                service.Config.PortCount = 5;
                service.SaveConfig();
                service.LoadConfig();
                Assert("T12. HMA Config persistence verified", service.Config.Username == "hma_test_user" && service.Config.PortCount == 5);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Exception in test suite: " + ex);
                Console.ResetColor();
            }

            Console.WriteLine(string.Format("\n📊 KẾT QUẢ KIỂM THỬ: {0}/{1} TESTS PASSED ({2:P0})", _passed, _total, (double)_passed / Math.Max(1, _total)));
            Environment.Exit(_passed == _total ? 0 : 1);
        }
    }
}
