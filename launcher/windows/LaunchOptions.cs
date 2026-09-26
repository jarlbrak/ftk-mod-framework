using System;
using System.Globalization;
using System.IO;

namespace FtkModdedLauncher
{
    internal sealed class LaunchOptions
    {
        internal string GameDir { get; private set; }
        internal int? WaitForProcess { get; private set; }
        internal string ReadyEvent { get; private set; }

        internal static LaunchOptions FromDirectory(string[] args, string directory)
        {
            string defaultGameDir = null;
            if (Array.IndexOf(args, "--game-dir") < 0)
            {
                string metadata = Path.Combine(directory, "ftkmf-game-directory.txt");
                if (File.Exists(metadata)) defaultGameDir = File.ReadAllText(metadata).TrimEnd('\r', '\n');
            }
            return Parse(args, defaultGameDir);
        }

        internal static LaunchOptions Parse(string[] args, string defaultGameDir = null)
        {
            LaunchOptions options = new LaunchOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string name = args[i];
                if (name != "--game-dir" && name != "--wait-for-process" && name != "--ready-event")
                    throw new ArgumentException("Unknown launcher argument: " + name);
                if (++i == args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("Missing value for " + name + ".");
                if (name == "--game-dir")
                {
                    if (options.GameDir != null) throw new ArgumentException("Duplicate --game-dir argument.");
                    options.GameDir = ValidateGameDirectory(args[i]);
                }
                else if (name == "--ready-event")
                {
                    if (options.ReadyEvent != null) throw new ArgumentException("Duplicate --ready-event argument.");
                    const string prefix = @"Local\FTKMFBootstrap-";
                    string value = args[i];
                    if (!value.StartsWith(prefix, StringComparison.Ordinal) || value.Length != prefix.Length + 32)
                        throw new ArgumentException("Invalid bootstrap readiness event name.");
                    for (int j = prefix.Length; j < value.Length; j++)
                    {
                        char c = value[j];
                        if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F'))
                            throw new ArgumentException("Invalid bootstrap readiness event name.");
                    }
                    options.ReadyEvent = value;
                }
                else
                {
                    if (options.WaitForProcess.HasValue) throw new ArgumentException("Duplicate --wait-for-process argument.");
                    int pid;
                    if (!int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out pid) || pid <= 0)
                        throw new ArgumentException("--wait-for-process requires a positive process ID.");
                    options.WaitForProcess = pid;
                }
            }
            if (options.GameDir == null && defaultGameDir != null)
                options.GameDir = ValidateGameDirectory(defaultGameDir);
            if (options.ReadyEvent != null && !options.WaitForProcess.HasValue)
                throw new ArgumentException("--ready-event requires --wait-for-process.");
            if (options.WaitForProcess.HasValue && options.GameDir == null)
                throw new ArgumentException("--wait-for-process requires --game-dir.");
            return options;
        }

        private static string ValidateGameDirectory(string value)
        {
            // Path.IsPathRooted also accepts drive-relative paths such as C:game.
            bool driveRoot = value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' &&
                (value[2] == '\\' || value[2] == '/');
            bool uncRoot = value.StartsWith("\\\\", StringComparison.Ordinal) && value.Length > 2;
            if ((!driveRoot && !uncRoot) || !Path.IsPathRooted(value))
                throw new ArgumentException("--game-dir requires an absolute game directory.");
            return Path.GetFullPath(value);
        }
    }
}
