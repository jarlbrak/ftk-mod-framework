using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Google2u;
using HarmonyLib;
using FTKModFramework.Core.Diagnostics;

namespace FTKModFramework.Core
{
    /// <summary>
    /// <c>[Diagnostics] LogLocalizationMisses</c> (spec #242 FR-2): logs each text key the game looks
    /// up and does not find, once per table, with a trimmed caller. It never changes text.
    ///
    /// Every <c>FTKHub.Localized</c> overload and <c>FTKHub.LocalizeString</c> resolve a key through
    /// the table's <c>GetGenRow(string)</c>. Each of the 14 compiled <c>Google2u.Text*</c> tables
    /// declares that method as a concrete, non-generic instance method whose body is
    /// <c>Rows[(int)Enum.Parse(typeof(rowIds), in_RowString)]</c> inside a
    /// <c>catch (ArgumentException)</c>, so a miss returns null. Those concrete methods are the hook:
    /// the generic <c>FTKHub.Localized&lt;T&gt;</c> must never be patched, because instantiating a
    /// patch on it corrupts Mono's shared generic code and blanks every text table
    /// (see <c>Core/Localization.cs</c>).
    ///
    /// The patches are installed by <see cref="Apply"/> from <c>Plugin.Awake</c> only when the key is
    /// true at startup; this class has no <c>[HarmonyPatch]</c> attribute, so <c>PatchAll</c> never
    /// installs it. The lookups run from per-frame UI such as <c>uiPlayerMainHud.Update</c>, so the
    /// postfix leaves on a non-null result before doing anything else, and the decisions live in
    /// the Unity-free <see cref="LocalizationMissLog"/>.
    /// </summary>
    internal static class LocalizationMissPatch
    {
        private static readonly Type[] Tables =
        {
            typeof(TextCharacters), typeof(TextDidYouKnow), typeof(TextEnemy), typeof(TextEnemyDescription),
            typeof(TextInfo), typeof(TextItems), typeof(TextItemsDescription), typeof(TextLore),
            typeof(TextLoreStore), typeof(TextMenu), typeof(TextMiniEncounters), typeof(TextMisc),
            typeof(TextQuest), typeof(TextStory),
        };

        private const int CallerFrames = 3;

        // Filled before any patch is installed and read-only afterwards, so the postfix can look up a
        // table name without allocating.
        private static readonly Dictionary<Type, string> TableNames = new Dictionary<Type, string>();
        private static LocalizationMissLog _log;
        private static bool _faulted;

        internal static void Apply(Harmony harmony)
        {
            if (harmony == null || _log != null) return;
            _log = new LocalizationMissLog(LocalizationMissLog.DefaultCap, IsFrameworkContentId);
            HarmonyMethod postfix = new HarmonyMethod(typeof(LocalizationMissPatch), nameof(GetGenRow_Postfix));

            int patched = 0;
            foreach (Type table in Tables)
            {
                try
                {
                    MethodInfo target = table.GetMethod("GetGenRow",
                        BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null);
                    if (target == null || target.IsGenericMethod || target.DeclaringType != table)
                    {
                        Plugin.Log.LogWarning("[loc-miss] " + table.Name + " has no concrete GetGenRow(string); not logged.");
                        continue;
                    }
                    TableNames[table] = table.Name;
                    harmony.Patch(target, postfix: postfix);
                    patched++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("[loc-miss] Could not patch " + table.Name + ".GetGenRow: " + e.Message);
                }
            }
            Plugin.Log.LogInfo("[loc-miss] Logging localization misses from " + patched + " of " + Tables.Length +
                " text tables, up to " + _log.Cap + " keys.");
        }

        private static bool IsFrameworkContentId(string id)
        {
            string ignored;
            return Localization.TryGetName(id, out ignored);
        }

        // __0 is GetGenRow's in_RowString; positional so a parameter rename cannot unbind it.
        private static void GetGenRow_Postfix(object __instance, string __0, IGoogle2uRow __result)
        {
            if (__result != null || _faulted) return;
            try
            {
                LocalizationMissLog log = _log;
                string table;
                if (log == null || __instance == null || !TableNames.TryGetValue(__instance.GetType(), out table)) return;

                switch (log.Observe(table, __0))
                {
                    case LocalizationMissDecision.Log:
                        Plugin.Log.LogInfo(LocalizationMissLog.FormatMiss(table, __0, Caller()));
                        break;
                    case LocalizationMissDecision.CapReached:
                        Plugin.Log.LogInfo(LocalizationMissLog.FormatCapReached(log.Cap));
                        break;
                }
            }
            catch (Exception e)
            {
                // A diagnostic must never break text lookup: stop logging and let the game continue.
                _faulted = true;
                try { Plugin.Log.LogWarning("[loc-miss] Stopped logging after an internal error: " + e.Message); }
                catch { }
            }
        }

        // Only reached on the first miss of a key, so the stack walk stays off the per-frame path.
        private static string Caller()
        {
            StackFrame[] frames = new StackTrace(1, false).GetFrames();
            List<string> names = new List<string>();
            if (frames != null)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    MethodBase method = frames[i].GetMethod();
                    if (method == null) continue;
                    Type type = method.DeclaringType;
                    names.Add(type == null ? method.Name : type.FullName + "." + method.Name);
                }
            }
            return LocalizationMissLog.TrimCaller(names, CallerFrames);
        }
    }
}
