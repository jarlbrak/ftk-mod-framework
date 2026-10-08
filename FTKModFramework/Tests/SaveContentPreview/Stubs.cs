using System;
using System.Collections.Generic;

// Only the game members used by the actual preview patch are replaced. Harmony instructions,
// labels and exception metadata use the real HarmonyX library.
namespace StartGameFE
{
    public class ResumeBrowser
    {
        public void RefreshGameDetail() { }
    }
}

namespace GridEditor
{
    public class FTK_playerGameStart
    {
        public string Name;
        public Exception Failure;
        public int Calls;

        public string GetDisplayName()
        {
            Calls++;
            if (Failure != null) throw Failure;
            return Name;
        }

        public string GetDisplayName(int unrelated) { return "unrelated overload"; }
    }
}

namespace FTKModFramework
{
    internal static class Plugin
    {
        internal static readonly TestLog Log = new TestLog();
    }

    internal sealed class TestLog
    {
        internal readonly List<string> Warnings = new List<string>();
        internal void LogWarning(string value) { Warnings.Add(value); }
    }
}
