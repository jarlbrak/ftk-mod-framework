using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
            Controls.Add(new Label { Text = "Your adventures. Your mods.\nFirst visit? Choose Install / Repair, then Play through Steam.", Location = new Point(38, 145), Size = new Size(520, 60) });
            AddButton("Play", 38, delegate { Process.Start(new ProcessStartInfo("steam://rungameid/527230") { UseShellExecute = true }); });
            AddButton("Install / Repair", 213, delegate {
                string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "install.ps1");
                if (!File.Exists(script)) throw new FileNotFoundException("The bundled install.ps1 is missing.", script);
                Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -NoExit -ExecutionPolicy Bypass -File \"" + script + "\"") { UseShellExecute = true, WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory });
            });
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
        }

        private void ApplyArtwork()
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = RunHelper("artwork"); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
                worker.Dispose();
                if (IsDisposed || Disposing) return;
                string details = e.Error == null ? (string)e.Result : e.Error.Message;
                _artworkStatus.Text = e.Error == null
                    ? "Steam artwork is ready. Restart Steam if its library still shows old artwork."
                    : "Artwork setup failed: " + details;
                _statusDetails.SetToolTip(_artworkStatus, details);
            };
            worker.RunWorkerAsync();
        }

        private static string RunHelper(string command)
        {
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            string helper = Path.Combine(directory, "ftkmf-launcher-helper.exe");
            if (!File.Exists(helper)) throw new FileNotFoundException("The bundled Steam helper is missing.", helper);
            using (Process process = Process.Start(new ProcessStartInfo(helper,
                command + " --launcher \"" + Application.ExecutablePath + "\" --art \"" + Path.Combine(directory, "assets", "steam") + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                string output = "";
                process.OutputDataReceived += delegate(object source, DataReceivedEventArgs line) { if (line.Data != null) { lock (process) { output += line.Data + Environment.NewLine; } } };
                process.ErrorDataReceived += delegate(object source, DataReceivedEventArgs line) { if (line.Data != null) { lock (process) { output += line.Data + Environment.NewLine; } } };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException(output.Length == 0 ? "Steam helper exited with code " + process.ExitCode : output.Trim());
                return output.Length == 0 ? "Steam setup completed." : output.Trim();
            }
        }

        private void AddButton(string text, int x, Action action)
        {
            Button button = new Button { Text = text, Location = new Point(x, 245), Size = new Size(165, 45), BackColor = Color.FromArgb(173, 128, 44), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            button.Click += delegate {
                try { action(); }
                catch (Exception error) { MessageBox.Show(this, error.Message, "For The King Modded", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            Controls.Add(button);
        }
    }
}
