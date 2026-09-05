using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FtkModdedLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Launcher());
        }
    }

    internal sealed class Launcher : Form
    {
        private readonly Label _artworkStatus = new Label();
        private readonly ToolTip _statusDetails = new ToolTip();
        private readonly List<Button> _actions = new List<Button>();
        private bool _busy;
        private bool _playUsed;
        private Process _unstoppedChild;
        private readonly Timer _childMonitor = new Timer { Interval = 1000 };

        internal Launcher()
        {
            Text = "For The King Modded";
            ClientSize = new Size(600, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(18, 25, 31);
            ForeColor = Color.FromArgb(237, 225, 193);
            Font = new Font("Segoe UI", 11);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Label title = new Label { Text = "FOR THE KING\nMODDED", Font = new Font("Segoe UI", 27, FontStyle.Bold), AutoSize = false, Location = new Point(35, 30), Size = new Size(530, 100) };
            Controls.Add(title);
            Controls.Add(new Label { Text = "Your adventures. Your mods.\nPlay checks for framework updates before opening Steam.", Location = new Point(38, 145), Size = new Size(520, 60) });
            AddButton("Play", 38, delegate { PreparePlay(false); });
            AddButton("Restore bundled", 213, delegate { PreparePlay(true); });
            AddButton("Add to Steam / Art", 388, delegate {
                UseWaitCursor = true;
                try { MessageBox.Show(this, RunHelper("register"), "For The King Modded", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                finally { UseWaitCursor = false; }
            });
            _artworkStatus.Text = "Preparing Steam artwork...";
            _artworkStatus.Location = new Point(38, 310);
            _artworkStatus.Size = new Size(525, 60);
            _artworkStatus.Font = new Font("Segoe UI", 9);
            Controls.Add(_artworkStatus);
            Shown += delegate { ApplyArtwork(); };
            _childMonitor.Tick += delegate { CheckPreviousChild(); };
            Disposed += delegate { _childMonitor.Dispose(); };
        }

        private void ApplyArtwork()
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = RunHelper("artwork"); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
                worker.Dispose();
                if (IsDisposed || Disposing || _playUsed) return;
                string details = e.Error == null ? (string)e.Result : e.Error.Message;
                _artworkStatus.Text = e.Error == null
                    ? "Steam artwork is ready. Restart Steam if its library still shows old artwork."
                    : "Artwork setup failed: " + details;
                _statusDetails.SetToolTip(_artworkStatus, details);
            };
            worker.RunWorkerAsync();
        }

        private void PreparePlay(bool repairOnly)
        {
            if (_busy) return;
            _busy = true;
            _playUsed = true;
            foreach (Button button in _actions) button.Enabled = false;
            _artworkStatus.Text = repairOnly ? "Restoring the bundled framework and Stable updates..." : "Checking your mod installation and framework updates...";
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) {
                string directory = AppDomain.CurrentDomain.BaseDirectory;
                string script = Path.Combine(directory, "install.ps1");
                if (!File.Exists(script)) throw new FileNotFoundException("The bundled installer is missing.", script);
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                string scriptArguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script);
                string status = RunProcess(powershell, scriptArguments + " -Status", 30000);
                Dictionary<string, object> record = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(status);
                object path;
                object present;
                if (record == null || !record.TryGetValue("gameDir", out path) || !record.TryGetValue("frameworkInstalled", out present) || !(path is string) || !(present is bool))
                    throw new InvalidOperationException("The bundled installer returned an invalid game status.");
                string gameDir = (string)path;
                // The bundled helper serializes installer/update writes and Steam dispatch.
                string result = RunProcess(Path.Combine(directory, "ftkmf-launcher-helper.exe"),
                    "prepare-launch --game-dir " + Quote(gameDir) + " --bundle-dir " + Quote(directory) +
                    (repairOnly ? " --repair-only --reset-update-selection" : " --install-if-missing --launch"), repairOnly || !(bool)present ? 720000 : 120000);
                e.Result = repairOnly ? "Bundled version restored. Update choice is Stable. Choose Play when ready." : result;
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
                worker.Dispose();
                if (IsDisposed || Disposing) return;
                ChildStillRunningException running = e.Error as ChildStillRunningException;
                if (running != null)
                {
                    _unstoppedChild = running.Child;
                    _artworkStatus.Text = running.Message;
                    _statusDetails.SetToolTip(_artworkStatus, running.Message);
                    _childMonitor.Start();
                    return;
                }
                _busy = false;
                foreach (Button button in _actions) button.Enabled = true;
                string message = e.Error == null ? (string)e.Result : e.Error.Message;
                _artworkStatus.Text = e.Error == null ? message : "Play was stopped: " + message;
                _statusDetails.SetToolTip(_artworkStatus, message);
                if (e.Error != null) MessageBox.Show(this, message, "For The King Modded", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            worker.RunWorkerAsync();
        }

        private void CheckPreviousChild()
        {
            if (_unstoppedChild == null) { _childMonitor.Stop(); return; }
            try
            {
                if (!_unstoppedChild.HasExited) return;
                _unstoppedChild.WaitForExit();
                _unstoppedChild.Dispose();
                _unstoppedChild = null;
                _childMonitor.Stop();
                _busy = false;
                foreach (Button button in _actions) button.Enabled = true;
                _artworkStatus.Text = "The previous operation has exited. Choose Play to check the installation and recover.";
            }
            catch (Exception error) { _statusDetails.SetToolTip(_artworkStatus, "Still waiting for the previous operation to exit: " + error.Message); }
        }

        private sealed class ChildStillRunningException : Exception
        {
            internal readonly Process Child;
            internal ChildStillRunningException(Process child) : base("The previous setup or update process may still be running. Play and Repair remain blocked until it exits.") { Child = child; }
        }

        private static string RunHelper(string command)
        {
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            return RunProcess(Path.Combine(directory, "ftkmf-launcher-helper.exe"),
                command + " --launcher " + Quote(Application.ExecutablePath) + " --art " + Quote(Path.Combine(directory, "assets", "steam")), 60000);
        }

        // CommandLineToArgvW quoting, including trailing backslashes and paths containing quotes.
        internal static string Quote(string argument)
        {
            StringBuilder output = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in argument)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { output.Append('\\', slashes * 2 + 1).Append(c); slashes = 0; continue; }
                output.Append('\\', slashes).Append(c);
                slashes = 0;
            }
            return output.Append('\\', slashes * 2).Append('"').ToString();
        }

        private static string RunProcess(string executable, string arguments, int timeoutMs)
        {
            if (!File.Exists(executable)) throw new FileNotFoundException("A required bundled or system tool is missing.", executable);
            Process process = new Process();
            bool retainProcess = false;
            try
            {
                process.StartInfo = new ProcessStartInfo(executable, arguments) {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                StringBuilder output = new StringBuilder();
                DataReceivedEventHandler collect = delegate(object source, DataReceivedEventArgs line) {
                    if (line.Data != null) lock (output) { if (output.Length < 1024 * 1024) output.AppendLine(line.Data); }
                };
                process.OutputDataReceived += collect;
                process.ErrorDataReceived += collect;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(timeoutMs))
                {
                    // The updater may own an installer child and the shared write lock.
                    // Let its own bounded cancellation reap that child before releasing the lock.
                    if (arguments.StartsWith("prepare-launch ", StringComparison.Ordinal))
                    { retainProcess = true; throw new ChildStillRunningException(process); }
                    bool exited = false;
                    try { process.Kill(); } catch { }
                    try { exited = process.WaitForExit(5000); } catch { }
                    if (!exited) { retainProcess = true; throw new ChildStillRunningException(process); }
                    throw new TimeoutException("Setup or update checking timed out. No additional game launch was attempted. Close the game if it opened, then retry Play to recover.");
                }
                process.WaitForExit(); // Flush asynchronous output callbacks after the bounded process wait.
                string message = output.ToString().Trim();
                if (process.ExitCode != 0) throw new InvalidOperationException(message.Length == 0 ? "Tool exited with code " + process.ExitCode : message);
                return message.Length == 0 ? "Ready. Opening For The King through Steam." : message;
            }
            finally { if (!retainProcess) process.Dispose(); }
        }

        private void AddButton(string text, int x, Action action)
        {
            Button button = new Button { Text = text, Location = new Point(x, 245), Size = new Size(165, 45), BackColor = Color.FromArgb(173, 128, 44), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            button.Click += delegate {
                try { action(); }
                catch (Exception error) { MessageBox.Show(this, error.Message, "For The King Modded", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            _actions.Add(button);
            Controls.Add(button);
        }
    }
}
