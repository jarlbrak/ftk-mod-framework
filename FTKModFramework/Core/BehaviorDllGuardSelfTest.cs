using System;
using System.IO;
using FTKModFramework.Core.Data;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Startup self-test for the manifest behaviorDll path-traversal guard (P3, #32). It exercises
    /// <see cref="ModManifest.TryResolveBehaviorDll"/> as a PURE function against a synthetic absolute
    /// mod-folder path (no real files needed): a traversal value, a separator value, and a rooted value
    /// must all be REJECTED, and a plain filename must be ACCEPTED with a resolvedPath that equals
    /// Path.GetFullPath(Path.Combine(folder, name)) and stays under the folder root. Emits exactly one
    /// "SELF-TEST PASS [behavior-dll-guard]" line on success (or a matching FAIL line), in the same style
    /// as the class/enemy/behavior-primitives self-tests.
    ///
    /// Run UNCONDITIONALLY from the plugin postfix (it does not depend on EnableSampleContent): it touches
    /// no game state and no filesystem, only the guard's pure logic.
    /// </summary>
    internal static class BehaviorDllGuardSelfTest
    {
        public static void Run()
        {
            // A synthetic absolute mod-folder path: the guard never touches the filesystem, so this folder
            // need not exist. Built from the temp path so it is a valid rooted path on every OS.
            string folder = Path.Combine(Path.GetTempPath(), "ftkmf_guard_selftest_mod");

            try
            {
                string resolved;
                string reason;

                // 1) Traversal: "../evil.dll" must be REJECTED.
                bool traversalRejected = !ModManifest.TryResolveBehaviorDll(folder, "../evil.dll", out resolved, out reason);

                // 2) Separator: "sub/dir.dll" must be REJECTED.
                bool separatorRejected = !ModManifest.TryResolveBehaviorDll(folder, "sub/dir.dll", out resolved, out reason);

                // 3) Absolute: a rooted path must be REJECTED. "/etc/evil.dll" is rooted on Unix; on Windows
                //    Path.IsPathRooted treats a leading separator as rooted too, so this is rejected on both.
                bool absoluteRejected = !ModManifest.TryResolveBehaviorDll(folder, "/etc/evil.dll", out resolved, out reason);

                // 4) Plain filename: "steal.dll" must be ACCEPTED, resolve to the canonical combined path,
                //    and stay under the folder root.
                string expected = Path.GetFullPath(Path.Combine(folder, "steal.dll"));
                string root = Path.GetFullPath(folder);
                bool plainAccepted = ModManifest.TryResolveBehaviorDll(folder, "steal.dll", out resolved, out reason)
                    && string.Equals(resolved, expected, StringComparison.Ordinal)
                    && resolved.StartsWith(root, StringComparison.Ordinal);

                bool ok = traversalRejected && separatorRejected && absoluteRejected && plainAccepted;
                if (ok)
                    Plugin.Log.LogInfo("SELF-TEST PASS [behavior-dll-guard]: rejects '..'/separator/absolute, " +
                        "accepts a bare filename resolving under the mod root.");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [behavior-dll-guard]: traversalRejected=" + traversalRejected +
                        " separatorRejected=" + separatorRejected + " absoluteRejected=" + absoluteRejected +
                        " plainAccepted=" + plainAccepted + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [behavior-dll-guard]: " + e);
            }
        }
    }
}
