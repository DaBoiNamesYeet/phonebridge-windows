using System;

namespace PhoneBridge
{
    internal static class Tests
    {
        private static int failures;

        private static void Main()
        {
            TestDeviceParsing();
            TestEndpoint();
            TestArguments();

            Console.WriteLine(failures == 0 ? "All PhoneBridge tests passed." : failures + " test(s) failed.");
            Environment.ExitCode = failures == 0 ? 0 : 1;
        }

        private static void TestDeviceParsing()
        {
            var sample =
                "List of devices attached\r\n" +
                "R58M123 device product:a model:Pixel_8 device:x transport_id:1\r\n" +
                "192.168.1.20:5555 unauthorized transport_id:2\r\n";
            var devices = PhoneBridgeCore.ParseDevices(sample);
            Expect(devices.Count == 2, "parses two devices");
            Expect(devices[0].Model == "Pixel 8", "formats model name");
            Expect(devices[1].State == "unauthorized", "keeps device state");
        }

        private static void TestEndpoint()
        {
            Expect(PhoneBridgeCore.BuildEndpoint("192.168.1.42", 5555) == "192.168.1.42:5555",
                "builds IPv4 endpoint");
            ExpectThrows(delegate { PhoneBridgeCore.BuildEndpoint("not-an-ip", 5555); },
                "rejects invalid address");
        }

        private static void TestArguments()
        {
            var args = PhoneBridgeCore.BuildScrcpyArguments("abc:5555", 1920, true, false, false, true);
            Expect(args.Contains("--serial \"abc:5555\""), "quotes serial");
            Expect(args.Contains("--max-size 1920"), "sets size");
            Expect(args.Contains("--stay-awake"), "sets stay awake");
            Expect(args.Contains("--no-audio"), "disables audio");
            Expect(args.Contains("--always-on-top"), "sets always-on-top");
            Expect(!args.Contains("--turn-screen-off"), "does not set disabled option");

            var unlimitedArgs = PhoneBridgeCore.BuildScrcpyArguments(null, 0, false, false, true, false);
            Expect(!unlimitedArgs.Contains("--max-size"), "omits size limit for unlimited resolution");
        }

        private static void Expect(bool condition, string name)
        {
            if (condition)
                Console.WriteLine("PASS: " + name);
            else
            {
                Console.WriteLine("FAIL: " + name);
                failures++;
            }
        }

        private static void ExpectThrows(Action action, string name)
        {
            try
            {
                action();
                Expect(false, name);
            }
            catch (ArgumentException)
            {
                Expect(true, name);
            }
        }
    }
}
