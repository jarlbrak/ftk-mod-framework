using System;
using System.Diagnostics;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace FTKModFramework.Core.Marketplace
{
    // Update preferences only. The launcher owns all framework replacement on a later launch.
    internal static class FrameworkUpdateRuntime
    {
        private sealed class Operation
        {
            internal Process Process;
            internal string Id;
            internal string ResultPath;
            internal string Name;
            internal Stopwatch Clock;
            internal bool CannotStop;
            internal Action<FrameworkUpdateResult> Complete;
        }
        private static Operation _operation;
        internal static FrameworkUpdateResult State { get; private set; }
        internal static bool Busy { get { return _operation != null; } }
        internal static bool SelectionKnown { get; private set; }
        internal static string LastStatus = "empty";
        internal static string Notice = "Choose Refresh to check available framework versions.";

        internal static bool Start(string operation, string mode, string tag, Action<FrameworkUpdateResult> complete, long releaseId = 0)
        {
            if (Busy) return false;
            try
            {
                if (operation != "status" && operation != "refresh" && operation != "select") throw new ArgumentException("Unsupported framework update operation.");
                if (operation == "select" && (!ValidMode(mode) || (mode == "pinned" && (!ValidTag(tag) || releaseId <= 0)))) throw new ArgumentException("Invalid framework update selection.");
                string root = Path.Combine(Paths.GameRootPath, "BepInEx/ftkmf");
                string helper = Path.Combine(root, Environment.OSVersion.Platform == PlatformID.Win32NT ? "ftkmf-launcher-helper.exe" : "ftkmf-launcher-helper");
                MarketplaceProtocol.VerifyHelper(helper);
                string id = Guid.NewGuid().ToString("N");
                string operations = Path.Combine(root, "update-operations");
                Directory.CreateDirectory(operations);
                string requestPath = Path.Combine(operations, id + ".request.json");
                string resultPath = Path.Combine(operations, id + ".result.json");
                FrameworkUpdateRequest request = new FrameworkUpdateRequest { OperationId = id, GameDir = Paths.GameRootPath, Mode = mode, Tag = tag, ReleaseId = releaseId };
                File.WriteAllText(requestPath, JsonConvert.SerializeObject(request));
                ProcessStartInfo start = new ProcessStartInfo(helper, "framework-updates " + operation + " --request " + Quote(requestPath) + " --result " + Quote(resultPath));
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                _operation = new Operation { Id = id, ResultPath = resultPath, Name = operation, Clock = Stopwatch.StartNew(), Complete = complete, Process = Process.Start(start) };
                if (operation == "select") SelectionKnown = false;
                Notice = operation == "select" ? "Saving your choice for next launch..." : "Checking framework versions...";
                return true;
            }
            catch (Exception e)
            {
                LastStatus = "error";
                Notice = "Framework Updates is unavailable. Open the latest launcher once, then try again. " + e.Message;
                if (complete != null) complete(new FrameworkUpdateResult { Status = "error", Message = Notice });
                return false;
            }
        }

        internal static void Poll()
        {
            Operation operation = _operation;
            if (operation == null) return;
            FrameworkUpdateResult result = null;
            try
            {
                if (!operation.Process.HasExited)
                {
                    if (operation.CannotStop || operation.Clock.ElapsedMilliseconds < 25000) return;
                    Stop(operation);
                    throw new IOException("The framework update check timed out.");
                }
                result = ReadResult(operation.ResultPath, operation.Id);
                if (operation.Process.ExitCode != 0 && result.Ok) throw new IOException("Update helper reported success but exited with an error.");
                if (result.Ok) { State = result; SelectionKnown = true; }
            }
            catch (Exception e)
            {
                if (!Exited(operation))
                {
                    operation.CannotStop = true;
                    Notice = "The update helper is still running. New choices are blocked until it exits.";
                    return;
                }
                result = new FrameworkUpdateResult { Status = "error", Message = e.Message };
            }
            if (result == null) return;
            operation.Process.Dispose();
            _operation = null;
            LastStatus = result.Status;
            Notice = result.Message ?? (result.Ok ? "Update preference saved for next launch." : "Framework update check failed.");
            if (operation.Complete != null) operation.Complete(result);
            if (!result.Ok && operation.Name == "select") Start("status", null, null, null);
        }

        internal static void Cancel()
        {
            Operation operation = _operation;
            if (operation == null) return;
            try { Stop(operation); }
            catch (Exception e)
            {
                operation.CannotStop = true;
                Notice = "Waiting for the update helper to exit. " + e.Message;
                return;
            }
            operation.Process.Dispose();
            _operation = null;
            Notice = "Operation stopped. Reading the saved update preference...";
            Start("status", null, null, null);
        }

        internal static FrameworkUpdateResult ReadResult(string path, string operationId)
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists || info.Length > 2 * 1024 * 1024) throw new IOException("Framework update result is missing or too large.");
            FrameworkUpdateResult result = JsonConvert.DeserializeObject<FrameworkUpdateResult>(File.ReadAllText(path));
            if (result == null || result.SchemaVersion != 1 || result.OperationId != operationId) throw new IOException("Framework update result has an unsupported schema or operation identity.");
            if (!result.Ok) return result;
            if (!ValidMode(result.SelectedMode) || (result.SelectedMode == "pinned" && !ValidTag(result.SelectedTag))) throw new IOException("Framework update selection is invalid.");
            if (result.Releases == null || result.Releases.Count > 100) throw new IOException("Framework release list is missing or too large.");
            foreach (FrameworkRelease release in result.Releases) ValidateRelease(release);
            if (result.Stable != null) ValidateRelease(result.Stable);
            if (result.Preview != null) ValidateRelease(result.Preview);
            return result;
        }
        private static void ValidateRelease(FrameworkRelease release)
        {
            if (release == null || release.ReleaseId <= 0 || !ValidTag(release.Tag) || release.Tag != "v" + release.Version || (release.Notes != null && release.Notes.Length > 100000)) throw new IOException("Framework release metadata is invalid.");
        }
        internal static bool ValidTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith("v", StringComparison.Ordinal)) return false;
            try { Version version = new Version(tag.Substring(1)); return version.Build >= 0 && version.Revision < 0 && version.ToString() == tag.Substring(1); }
            catch { return false; }
        }
        private static bool ValidMode(string mode) { return mode == "stable" || mode == "preview" || mode == "pinned"; }
        private static bool Exited(Operation operation) { try { return operation.Process.HasExited; } catch { return false; } }
        private static void Stop(Operation operation)
        {
            if (!operation.Process.HasExited) operation.Process.Kill();
            if (!operation.Process.WaitForExit(1000)) throw new IOException("Update helper could not be reaped.");
        }
        private static string Quote(string path)
        {
            if (path.IndexOf('"') >= 0 || path.IndexOf('\r') >= 0 || path.IndexOf('\n') >= 0) throw new IOException("Unsupported installation path.");
            return "\"" + path + "\"";
        }
    }
}
