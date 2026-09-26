using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace FTKThunderstoreBootstrap
{
    [BepInPlugin("com.ftkmf.thunderstore.bootstrap", "FTK Mod Framework Setup", BootstrapVersion.Value)]
    public sealed class BootstrapPlugin : BaseUnityPlugin
    {
        private readonly object errorLock = new object();
        private readonly StringBuilder helperErrors = new StringBuilder();
        private Process helper;
        private bool dismissed;
        private string status = "";
        private GUIStyle bodyStyle;

        private void OnGUI()
        {
            if (dismissed) return;
            if (bodyStyle == null) bodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            float width = Math.Min(560f, Screen.width - 24f);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, 40f, width, Math.Min(420f, Screen.height - 60f)), GUI.skin.box);
            GUILayout.Label("FTK Mod Framework: initial setup");
            GUILayout.Space(12f);
            GUILayout.Label("Thunderstore installs this setup tool. Setup opens the FTK Modded Launcher and closes this game session. The launcher installs and updates the framework and manages your mods.", bodyStyle);
            GUILayout.Space(8f);
            GUILayout.Label("After setup, always play through the FTK Modded Launcher. Launching this Thunderstore profile again returns to this setup screen.", bodyStyle);
            GUILayout.Space(12f);
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                GUILayout.Label("Thunderstore setup supports Windows only. Install the normal FTK Modded Launcher for your platform from the project website.", bodyStyle);
            }
            else if (helper == null)
            {
                if (GUILayout.Button("Set up and open FTK Modded Launcher")) StartSetup();
            }
            else
            {
                GUILayout.Label("Preparing the launcher. Keep this game open until setup completes.", bodyStyle);
            }
            if (status.Length > 0) GUILayout.Label(status, bodyStyle);
            if (helper == null && GUILayout.Button("Continue without setup for this session")) dismissed = true;
            GUILayout.EndArea();
        }

        private void StartSetup()
        {
            if (helper != null || Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            Process candidate = null;
            try
            {
                string bundleDirectory = Path.GetDirectoryName(Info.Location);
                string executable = Path.Combine(bundleDirectory, "ftkmf-bootstrap-helper.exe");
                if (!File.Exists(executable)) throw new FileNotFoundException("The setup helper is missing. Reinstall the Thunderstore package.");
                lock (errorLock) helperErrors.Length = 0;
                candidate = new Process();
                candidate.StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = bundleDirectory,
                    Arguments = "thunderstore-handoff --game-dir " + Quote(Paths.GameRootPath)
                        + " --bundle-dir " + Quote(bundleDirectory)
                        + " --wait-for-process " + Process.GetCurrentProcess().Id,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                candidate.ErrorDataReceived += CaptureError;
                if (!candidate.Start()) throw new InvalidOperationException("The setup helper could not start.");
                candidate.BeginErrorReadLine();
                helper = candidate;
                status = "";
            }
            catch (Exception ex)
            {
                if (candidate != null) candidate.Dispose();
                ShowFailure(ex.Message);
            }
        }

        private void CaptureError(object sender, DataReceivedEventArgs args)
        {
            if (String.IsNullOrEmpty(args.Data)) return;
            lock (errorLock)
            {
                // Bound retained process output; keep diagnostics out of the game's update thread.
                int remaining = 4096 - helperErrors.Length;
                if (remaining > 0) helperErrors.Append(args.Data.Substring(0, Math.Min(remaining, args.Data.Length))).AppendLine();
            }
        }

        private void Update()
        {
            if (helper == null) return;
            try
            {
                if (!helper.HasExited) return;
                int exitCode = helper.ExitCode;
                helper.Dispose();
                helper = null;
                if (exitCode == 0)
                {
                    // The helper reports success only after launching the durable launcher.
                    dismissed = true;
                    Application.Quit();
                }
                else
                {
                    string detail;
                    lock (errorLock) detail = helperErrors.ToString().Trim();
                    ShowFailure(detail.Length == 0 ? "Setup helper exited with code " + exitCode + "." : detail);
                }
            }
            catch (Exception ex)
            {
                if (helper != null) helper.Dispose();
                helper = null;
                ShowFailure(ex.Message);
            }
        }

        private void ShowFailure(string message)
        {
            status = "Setup did not complete. The game is still running. " + message;
            Logger.LogError(status);
        }

        private static string Quote(string argument)
        {
            // Windows command-line parsing doubles backslashes before quotes and the closing quote.
            StringBuilder result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\') { slashes++; continue; }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                result.Append(character);
                slashes = 0;
            }
            result.Append('\\', slashes * 2).Append('"');
            return result.ToString();
        }
    }
}
