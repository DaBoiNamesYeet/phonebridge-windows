using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PhoneBridge
{
    internal sealed class MainForm : Form
    {
        private readonly RuntimeManager runtime = new RuntimeManager();
        private readonly ComboBox devices = new ComboBox();
        private readonly TextBox phoneIp = new TextBox();
        private readonly NumericUpDown pairPort = new NumericUpDown();
        private readonly NumericUpDown connectPort = new NumericUpDown();
        private readonly TextBox pairingCode = new TextBox();
        private readonly ComboBox maxSize = new ComboBox();
        private readonly CheckBox stayAwake = new CheckBox();
        private readonly CheckBox turnScreenOff = new CheckBox();
        private readonly CheckBox audio = new CheckBox();
        private readonly CheckBox alwaysOnTop = new CheckBox();
        private readonly TextBox log = new TextBox();
        private readonly Label runtimeStatus = new Label();
        private readonly Label activity = new Label();
        private readonly Button refreshButton = new Button();
        private readonly Button mirrorButton = new Button();

        public MainForm()
        {
            Text = "PhoneBridge";
            ClientSize = new Size(900, 720);
            MinimumSize = new Size(820, 650);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(245, 247, 250);
            Icon = SystemIcons.Application;

            BuildInterface();
            Shown += async delegate
            {
                if (runtime.IsInstalled)
                    await RefreshDevicesAsync(true);
                else
                    AppendLog("Android tools will install automatically when you refresh or connect.");
            };
        }

        private void BuildInterface()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(22, 16, 22, 14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildUsbCard(), 0, 1);
            root.Controls.Add(BuildWirelessCard(), 0, 2);
            root.Controls.Add(BuildOptionsCard(), 0, 3);
            root.Controls.Add(BuildLogCard(), 0, 4);

            activity.Dock = DockStyle.Fill;
            activity.TextAlign = ContentAlignment.MiddleLeft;
            activity.ForeColor = Color.FromArgb(70, 78, 90);
            activity.Text = "Only connect phones you own or are authorised to administer.";
            root.Controls.Add(activity, 0, 5);
        }

        private Control BuildHeader()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var title = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 21F),
                ForeColor = Color.FromArgb(26, 37, 54),
                Text = "PhoneBridge",
                Location = new Point(0, 2)
            };
            var subtitle = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(79, 91, 108),
                Text = "Mirror and control your Android phone over USB or Wi‑Fi",
                Location = new Point(3, 43)
            };
            runtimeStatus.AutoSize = true;
            runtimeStatus.TextAlign = ContentAlignment.MiddleRight;
            runtimeStatus.ForeColor = runtime.IsInstalled ? Color.SeaGreen : Color.FromArgb(183, 112, 0);
            runtimeStatus.Text = runtime.IsInstalled
                ? "✓ scrcpy " + RuntimeManager.Version + " ready"
                : "Android tools install automatically on first use";
            runtimeStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            runtimeStatus.Location = new Point(600, 20);
            panel.Resize += delegate { runtimeStatus.Left = panel.ClientSize.Width - runtimeStatus.Width; };
            panel.Controls.Add(title);
            panel.Controls.Add(subtitle);
            panel.Controls.Add(runtimeStatus);
            return panel;
        }

        private Control BuildUsbCard()
        {
            var card = CreateCard("USB connection");
            var help = new Label
            {
                AutoSize = true,
                Text = "On your phone: enable Developer options → USB debugging, connect the cable, then approve the phone's prompt.",
                Location = new Point(18, 30),
                ForeColor = Color.FromArgb(70, 78, 90)
            };

            devices.DropDownStyle = ComboBoxStyle.DropDownList;
            devices.Location = new Point(18, 60);
            devices.Size = new Size(360, 25);

            ConfigureButton(refreshButton, "Refresh devices", Color.FromArgb(77, 91, 113), 392, 58, 125);
            refreshButton.Click += async delegate { await RefreshDevicesAsync(false); };

            ConfigureButton(mirrorButton, "Start mirroring", Color.FromArgb(39, 105, 214), 532, 58, 145);
            mirrorButton.Click += async delegate { await StartMirroringAsync(); };

            var wifiUsb = new Button();
            ConfigureButton(wifiUsb, "Enable Wi‑Fi (port 5555)", Color.FromArgb(19, 128, 103), 692, 58, 170);
            wifiUsb.Click += async delegate { await EnableLegacyWifiAsync(); };

            card.Controls.Add(help);
            card.Controls.Add(devices);
            card.Controls.Add(refreshButton);
            card.Controls.Add(mirrorButton);
            card.Controls.Add(wifiUsb);
            return card;
        }

        private Control BuildWirelessCard()
        {
            var card = CreateCard("Wireless connection");
            var help = new Label
            {
                AutoSize = true,
                Text = "Phone and PC must be on the same trusted Wi‑Fi. Android 11+: use Wireless debugging → Pair device with pairing code.",
                Location = new Point(18, 30),
                ForeColor = Color.FromArgb(70, 78, 90)
            };
            card.Controls.Add(help);

            AddFieldLabel(card, "Phone IP (for example 192.168.1.42)", 18, 61);
            phoneIp.Location = new Point(18, 80);
            phoneIp.Size = new Size(190, 25);

            AddFieldLabel(card, "Pairing port", 224, 61);
            pairPort.Location = new Point(224, 80);
            ConfigurePort(pairPort, 37000);

            AddFieldLabel(card, "Pairing code", 340, 61);
            pairingCode.Location = new Point(340, 80);
            pairingCode.Size = new Size(120, 25);
            pairingCode.MaxLength = 12;

            var pair = new Button();
            ConfigureButton(pair, "Pair (Android 11+)", Color.FromArgb(104, 72, 180), 476, 78, 150);
            pair.Click += async delegate { await PairAsync(); };

            AddFieldLabel(card, "Connection port", 18, 114);
            connectPort.Location = new Point(18, 133);
            ConfigurePort(connectPort, 5555);

            var connect = new Button();
            ConfigureButton(connect, "Connect over Wi‑Fi", Color.FromArgb(19, 128, 103), 144, 131, 160);
            connect.Click += async delegate { await ConnectAsync(); };

            var disconnect = new Button();
            ConfigureButton(disconnect, "Disconnect Wi‑Fi", Color.FromArgb(170, 76, 67), 318, 131, 145);
            disconnect.Click += async delegate { await DisconnectAsync(); };

            var note = new Label
            {
                AutoSize = true,
                Text = "Legacy mode: first select a USB device above and click “Enable Wi‑Fi”, then connect here using port 5555.",
                Location = new Point(485, 136),
                ForeColor = Color.FromArgb(92, 100, 112)
            };

            card.Controls.Add(phoneIp);
            card.Controls.Add(pairPort);
            card.Controls.Add(pairingCode);
            card.Controls.Add(pair);
            card.Controls.Add(connectPort);
            card.Controls.Add(connect);
            card.Controls.Add(disconnect);
            card.Controls.Add(note);
            return card;
        }

        private Control BuildOptionsCard()
        {
            var card = CreateCard("Mirroring options");

            AddFieldLabel(card, "Max resolution", 18, 31);
            maxSize.DropDownStyle = ComboBoxStyle.DropDownList;
            maxSize.Items.AddRange(new object[] { "Unlimited", "1280", "1600", "1920", "2560" });
            maxSize.SelectedItem = "1920";
            maxSize.Location = new Point(18, 51);
            maxSize.Size = new Size(110, 25);

            ConfigureCheck(stayAwake, "Keep phone awake", 160, 52, true);
            ConfigureCheck(turnScreenOff, "Turn phone screen off", 310, 52, false);
            ConfigureCheck(audio, "Play phone audio", 490, 52, true);
            ConfigureCheck(alwaysOnTop, "Always on top", 640, 52, false);
            card.Controls.Add(maxSize);
            card.Controls.Add(stayAwake);
            card.Controls.Add(turnScreenOff);
            card.Controls.Add(audio);
            card.Controls.Add(alwaysOnTop);
            return card;
        }

        private Control BuildLogCard()
        {
            var card = CreateCard("Activity log");
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.BackColor = Color.White;
            log.BorderStyle = BorderStyle.FixedSingle;
            log.Font = new Font("Consolas", 8.5F);
            log.Location = new Point(18, 32);
            log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            log.Size = new Size(card.Width - 36, card.Height - 48);
            card.Resize += delegate { log.Size = new Size(card.ClientSize.Width - 36, card.ClientSize.Height - 48); };
            card.Controls.Add(log);
            return card;
        }

        private async Task EnsureRuntimeAsync()
        {
            if (runtime.IsInstalled)
                return;

            var progress = new Progress<string>(delegate(string message)
            {
                activity.Text = message;
                AppendLog(message);
            });
            await runtime.EnsureInstalledAsync(progress);
            runtimeStatus.Text = "✓ scrcpy " + RuntimeManager.Version + " ready";
            runtimeStatus.ForeColor = Color.SeaGreen;
            runtimeStatus.Left = runtimeStatus.Parent.ClientSize.Width - runtimeStatus.Width;
        }

        private async Task RefreshDevicesAsync(bool quiet)
        {
            await RunBusyAsync(async delegate
            {
                await EnsureRuntimeAsync();
                var result = await ProcessRunner.RunAsync(runtime.AdbPath, "devices -l", runtime.RuntimeDirectory);
                var found = PhoneBridgeCore.ParseDevices(result.Output);
                devices.Items.Clear();
                foreach (var device in found)
                    devices.Items.Add(device);
                if (devices.Items.Count > 0)
                    devices.SelectedIndex = 0;

                if (!quiet || found.Count > 0)
                    AppendLog(found.Count == 0 ? "No Android devices found." : "Found " + found.Count + " Android device(s).");
                if (!String.IsNullOrWhiteSpace(result.Error))
                    AppendLog(result.Error);
            }, "Refreshing devices…");
        }

        private async Task StartMirroringAsync()
        {
            await RunBusyAsync(async delegate
            {
                await EnsureRuntimeAsync();
                var selected = devices.SelectedItem as DeviceInfo;
                if (selected != null && selected.State != "device")
                    throw new InvalidOperationException(
                        "That phone is not authorised. Unlock it and approve the USB debugging prompt, then refresh.");

                var selectedSize = maxSize.SelectedItem == null ? "1920" : maxSize.SelectedItem.ToString();
                var size = selectedSize == "Unlimited" ? 0 : Int32.Parse(selectedSize);
                var args = PhoneBridgeCore.BuildScrcpyArguments(
                    selected == null ? null : selected.Serial,
                    size,
                    stayAwake.Checked,
                    turnScreenOff.Checked,
                    audio.Checked,
                    alwaysOnTop.Checked);

                var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = runtime.ScrcpyPath,
                    Arguments = args,
                    WorkingDirectory = runtime.RuntimeDirectory,
                    UseShellExecute = false
                };
                if (!process.Start())
                    throw new InvalidOperationException("Windows could not start scrcpy.");
                AppendLog("Started mirroring" + (selected == null ? "." : " " + selected.Serial + "."));
                await Task.FromResult(0);
            }, "Starting mirroring…");
        }

        private async Task EnableLegacyWifiAsync()
        {
            var selected = devices.SelectedItem as DeviceInfo;
            if (selected == null)
            {
                MessageBox.Show(this, "Select a connected USB phone first.", "PhoneBridge",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await RunAdbAsync(
                "-s " + PhoneBridgeCore.Quote(selected.Serial) + " tcpip 5555",
                "Enabled legacy Wi‑Fi debugging on port 5555. You can unplug the cable after connecting over Wi‑Fi.");
        }

        private async Task PairAsync()
        {
            var endpoint = PhoneBridgeCore.BuildEndpoint(phoneIp.Text, (int)pairPort.Value);
            var code = pairingCode.Text.Trim();
            if (code.Length < 6)
                throw new ArgumentException("Enter the pairing code shown by Android.");

            await RunAdbAsync(
                "pair " + endpoint,
                "Pairing request finished. Your code was not saved or written to the log.",
                "adb pair " + endpoint + " ******",
                code);
            pairingCode.Clear();
        }

        private async Task ConnectAsync()
        {
            var endpoint = PhoneBridgeCore.BuildEndpoint(phoneIp.Text, (int)connectPort.Value);
            await RunAdbAsync("connect " + endpoint, "Connected to " + endpoint + ".");
            await RefreshDevicesAsync(true);
        }

        private async Task DisconnectAsync()
        {
            var endpoint = PhoneBridgeCore.BuildEndpoint(phoneIp.Text, (int)connectPort.Value);
            await RunAdbAsync("disconnect " + endpoint, "Disconnected " + endpoint + ".");
            await RefreshDevicesAsync(true);
        }

        private async Task RunAdbAsync(
            string arguments,
            string success,
            string displayCommand = null,
            string standardInput = null)
        {
            await RunBusyAsync(async delegate
            {
                await EnsureRuntimeAsync();
                AppendLog("> " + (displayCommand ?? "adb " + arguments));
                var result = await ProcessRunner.RunAsync(
                    runtime.AdbPath,
                    arguments,
                    runtime.RuntimeDirectory,
                    standardInput);
                if (!String.IsNullOrWhiteSpace(result.Combined))
                    AppendLog(result.Combined);
                if (result.ExitCode != 0)
                    throw new InvalidOperationException("ADB returned an error. Read the activity log for details.");
                AppendLog(success);
            }, "Working with your phone…");
        }

        private async Task RunBusyAsync(Func<Task> action, string status)
        {
            UseWaitCursor = true;
            activity.Text = status;
            try
            {
                await action();
                activity.Text = "Ready — only use PhoneBridge with devices you own or administer.";
            }
            catch (Exception ex)
            {
                activity.Text = "Action failed.";
                AppendLog("ERROR: " + ex.Message);
                MessageBox.Show(this, ex.Message, "PhoneBridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void AppendLog(string message)
        {
            if (String.IsNullOrWhiteSpace(message))
                return;
            log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message.Trim() + Environment.NewLine);
        }

        private static Panel CreateCard(string titleText)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            var title = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = Color.FromArgb(32, 42, 57),
                Location = new Point(14, 8),
                Text = titleText
            };
            panel.Controls.Add(title);
            return panel;
        }

        private static void ConfigureButton(Button button, string text, Color color, int x, int y, int width)
        {
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 30);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
        }

        private static void ConfigurePort(NumericUpDown control, int value)
        {
            control.Minimum = 1;
            control.Maximum = 65535;
            control.Value = value;
            control.Size = new Size(100, 25);
        }

        private static void ConfigureCheck(CheckBox control, string text, int x, int y, bool value)
        {
            control.Text = text;
            control.AutoSize = true;
            control.Location = new Point(x, y);
            control.Checked = value;
        }

        private static void AddFieldLabel(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label
            {
                AutoSize = true,
                Text = text,
                Location = new Point(x, y),
                ForeColor = Color.FromArgb(70, 78, 90)
            });
        }
    }

}
