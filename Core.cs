using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PhoneBridge
{
    internal sealed class DeviceInfo
    {
        public string Serial { get; private set; }
        public string State { get; private set; }
        public string Model { get; private set; }

        public DeviceInfo(string serial, string state, string model)
        {
            Serial = serial;
            State = state;
            Model = model;
        }

        public override string ToString()
        {
            var name = String.IsNullOrWhiteSpace(Model) ? Serial : Model + " — " + Serial;
            return State == "device" ? name : name + " (" + State + ")";
        }
    }

    internal static class PhoneBridgeCore
    {
        public static List<DeviceInfo> ParseDevices(string output)
        {
            var result = new List<DeviceInfo>();
            if (String.IsNullOrWhiteSpace(output))
                return result;

            foreach (var raw in output.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                var model = "";
                foreach (var part in parts.Skip(2))
                {
                    if (part.StartsWith("model:", StringComparison.OrdinalIgnoreCase))
                    {
                        model = part.Substring(6).Replace('_', ' ');
                        break;
                    }
                }

                result.Add(new DeviceInfo(parts[0], parts[1], model));
            }

            return result;
        }

        public static string BuildEndpoint(string address, int port)
        {
            IPAddress parsed;
            if (!IPAddress.TryParse((address ?? "").Trim(), out parsed))
                throw new ArgumentException("Enter the phone's IP address, for example 192.168.1.42.");
            if (parsed.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("PhoneBridge currently expects the IPv4 address shown on your phone.");
            if (port < 1 || port > 65535)
                throw new ArgumentException("The port must be between 1 and 65535.");
            return parsed + ":" + port.ToString(CultureInfo.InvariantCulture);
        }

        public static string Quote(string value)
        {
            if (value == null)
                return "\"\"";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        public static string BuildScrcpyArguments(
            string serial,
            int maxSize,
            bool stayAwake,
            bool turnScreenOff,
            bool audio,
            bool alwaysOnTop)
        {
            var args = new List<string>();
            if (!String.IsNullOrWhiteSpace(serial))
            {
                args.Add("--serial");
                args.Add(Quote(serial.Trim()));
            }
            if (maxSize > 0)
            {
                args.Add("--max-size");
                args.Add(maxSize.ToString(CultureInfo.InvariantCulture));
            }
            if (stayAwake)
                args.Add("--stay-awake");
            if (turnScreenOff)
                args.Add("--turn-screen-off");
            if (!audio)
                args.Add("--no-audio");
            if (alwaysOnTop)
                args.Add("--always-on-top");
            args.Add("--window-title");
            args.Add(Quote("PhoneBridge — Android"));
            return String.Join(" ", args);
        }
    }

    internal sealed class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }

        public string Combined
        {
            get
            {
                var both = ((Output ?? "") + Environment.NewLine + (Error ?? "")).Trim();
                return both;
            }
        }
    }

    internal static class ProcessRunner
    {
        public static async Task<CommandResult> RunAsync(
            string executable,
            string arguments,
            string workingDirectory,
            string standardInput = null)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();
            var completion = new TaskCompletionSource<int>();

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = standardInput != null,
                    CreateNoWindow = true
                };
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) output.AppendLine(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) error.AppendLine(e.Data);
                };
                process.Exited += delegate { completion.TrySetResult(process.ExitCode); };

                if (!process.Start())
                    throw new InvalidOperationException("Windows could not start " + Path.GetFileName(executable) + ".");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (standardInput != null)
                {
                    process.StandardInput.WriteLine(standardInput);
                    process.StandardInput.Close();
                }
                var exitCode = await completion.Task.ConfigureAwait(false);
                process.WaitForExit();

                return new CommandResult
                {
                    ExitCode = exitCode,
                    Output = output.ToString().Trim(),
                    Error = error.ToString().Trim()
                };
            }
        }
    }

    internal sealed class RuntimeManager
    {
        public const string Version = "4.1";
        public const string DownloadUrl =
            "https://github.com/Genymobile/scrcpy/releases/download/v4.1/scrcpy-win64-v4.1.zip";
        public const string ExpectedSha256 =
            "5b12172b3264b2889f4583ee64752ce832e29bc8b1089dca81093459697165db";

        public string BaseDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PhoneBridge");
            }
        }

        public string RuntimeDirectory
        {
            get { return Path.Combine(BaseDirectory, "scrcpy-v" + Version); }
        }

        public string AdbPath
        {
            get { return Path.Combine(RuntimeDirectory, "adb.exe"); }
        }

        public string ScrcpyPath
        {
            get { return Path.Combine(RuntimeDirectory, "scrcpy.exe"); }
        }

        public bool IsInstalled
        {
            get
            {
                return File.Exists(AdbPath) &&
                       File.Exists(ScrcpyPath) &&
                       File.Exists(Path.Combine(RuntimeDirectory, ".phonebridge-runtime-ok"));
            }
        }

        public async Task EnsureInstalledAsync(IProgress<string> progress)
        {
            if (IsInstalled)
                return;

            Directory.CreateDirectory(BaseDirectory);
            var zipPath = Path.Combine(BaseDirectory, "scrcpy-v" + Version + ".download.zip");
            var staging = Path.Combine(BaseDirectory, "installing-" + Guid.NewGuid().ToString("N"));

            try
            {
                if (progress != null)
                    progress.Report("Downloading official scrcpy " + Version + " (about 11 MB)…");

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "PhoneBridge/1.0");
                    await client.DownloadFileTaskAsync(new Uri(DownloadUrl), zipPath);
                }

                if (progress != null)
                    progress.Report("Checking the official SHA-256 checksum…");

                var actualHash = ComputeSha256(zipPath);
                if (!actualHash.Equals(ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "The scrcpy download did not match the checksum published by Genymobile. " +
                        "Nothing was installed.");

                if (progress != null)
                    progress.Report("Installing the verified Android tools…");

                Directory.CreateDirectory(staging);
                ExtractSafely(zipPath, staging);
                var scrcpy = Directory.GetFiles(staging, "scrcpy.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (scrcpy == null)
                    throw new InvalidDataException("The official archive did not contain scrcpy.exe.");

                var extractedRoot = Path.GetDirectoryName(scrcpy);
                if (Directory.Exists(RuntimeDirectory))
                    Directory.Delete(RuntimeDirectory, true);
                Directory.Move(extractedRoot, RuntimeDirectory);
                File.WriteAllText(
                    Path.Combine(RuntimeDirectory, ".phonebridge-runtime-ok"),
                    "scrcpy " + Version + Environment.NewLine + ExpectedSha256);

                if (progress != null)
                    progress.Report("Android tools are ready.");
            }
            finally
            {
                TryDeleteFile(zipPath);
                TryDeleteDirectory(staging);
            }
        }

        public void Reset()
        {
            if (Directory.Exists(RuntimeDirectory))
                Directory.Delete(RuntimeDirectory, true);
        }

        public static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                return String.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
            }
        }

        private static void ExtractSafely(string zipPath, string destination)
        {
            var destinationRoot = Path.GetFullPath(destination).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                    if (!target.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The archive contained an unsafe file path.");

                    if (String.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target, true);
                }
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { }
        }
    }
}
