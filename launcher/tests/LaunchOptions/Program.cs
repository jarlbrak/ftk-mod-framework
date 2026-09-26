using System;
using System.IO;
using FtkModdedLauncher;

internal static class Program
{
    private static void Main()
    {
        LaunchOptions defaults = LaunchOptions.Parse(new string[0]);
        Require(defaults.GameDir == null && !defaults.WaitForProcess.HasValue, "Normal launch changed.");
        const string readyEvent = @"Local\FTKMFBootstrap-0123456789abcdef0123456789abcdef";
        Reject("--ready-event");
        Reject("--ready-event", readyEvent);
        Reject("--ready-event", "Global\\FTKMFBootstrap-0123456789abcdef0123456789abcdef");
        Reject("--ready-event", @"Local\FTKMFBootstrap-short");
        Reject("--ready-event", @"Local\FTKMFBootstrap-0123456789abcdef0123456789abcdeg");
        Reject("--ready-event", readyEvent, "--ready-event", readyEvent);
        string directory = Path.Combine(Path.GetTempPath(), "ftkmf-launch-options-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Require(LaunchOptions.FromDirectory(new string[0], directory).GameDir == null, "Missing metadata changed defaults.");
            File.WriteAllText(Path.Combine(directory, "ftkmf-game-directory.txt"), "relative\n");
            try { LaunchOptions.FromDirectory(new string[0], directory); throw new Exception("Accepted relative metadata."); }
            catch (ArgumentException) { }
            if (OperatingSystem.IsWindows())
            {
                LaunchOptions explicitPath = LaunchOptions.FromDirectory(new[] { "--game-dir", @"D:\Explicit Game" }, directory);
                Require(explicitPath.GameDir == @"D:\Explicit Game", "Explicit path did not override metadata.");
                File.WriteAllText(Path.Combine(directory, "ftkmf-game-directory.txt"), "C:\\Games\\For The King\r\n");
                LaunchOptions remembered = LaunchOptions.FromDirectory(new string[0], directory);
                Require(remembered.GameDir == @"C:\Games\For The King", "Remembered directory changed.");
                LaunchOptions acknowledged = LaunchOptions.FromDirectory(new[] { "--wait-for-process", "42", "--ready-event", readyEvent }, directory);
                Require(acknowledged.ReadyEvent == readyEvent && acknowledged.WaitForProcess == 42, "Readiness contract changed.");
            }
        }
        finally { Directory.Delete(directory, true); }
        Reject("--unknown");
        Reject("--game-dir");
        Reject("--wait-for-process");
        Reject("--game-dir", "--wait-for-process", "1");
        Reject("--game-dir", "relative/game");
        Reject("--game-dir", @"C:relative");
        Reject("--wait-for-process", "0");
        Reject("--wait-for-process", "-1");
        Reject("--wait-for-process", "2147483648");
        Reject("--wait-for-process", "1", "--wait-for-process", "2");
        Reject("--wait-for-process", "1");
        if (OperatingSystem.IsWindows())
        {
            LaunchOptions handoff = LaunchOptions.Parse(new[] { "--game-dir", @"C:\Games\For The King", "--wait-for-process", "42" });
            Require(handoff.GameDir == @"C:\Games\For The King" && handoff.WaitForProcess == 42, "Explicit handoff changed.");
            LaunchOptions unc = LaunchOptions.Parse(new[] { "--game-dir", @"\\server\share\For The King" });
            Require(unc.GameDir == @"\\server\share\For The King", "UNC directory changed.");
            Reject("--game-dir", @"C:\Game", "--game-dir", @"D:\Game");
        }
        else Console.WriteLine("SKIP: Windows path normalization requires Windows.");
        Console.WriteLine("PASS: launcher argument contract");
    }

    private static void Reject(params string[] args)
    {
        try { LaunchOptions.Parse(args); }
        catch (ArgumentException) { return; }
        throw new Exception("Accepted invalid arguments: " + string.Join(" ", args));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
