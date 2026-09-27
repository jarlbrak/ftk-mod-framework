using System;
using System.Collections.Generic;
using System.Text;

namespace FTKModFramework.Core.Diagnostics
{
    /// <summary>What the miss logger does with one failed text-table lookup.</summary>
    internal enum LocalizationMissDecision
    {
        /// <summary>Already seen, deliberately passed through by the framework, or logging has ended.</summary>
        Ignore,
        /// <summary>A new miss: capture the caller and write one line.</summary>
        Log,
        /// <summary>The key cap was just exceeded: write the cap line once, then stop.</summary>
        CapReached,
    }

    /// <summary>
    /// Filter, dedupe and cap decisions for <c>[Diagnostics] LogLocalizationMisses</c> (spec #242 FR-2).
    /// The Harmony postfix in <c>Core/LocalizationMissPatch.cs</c> calls <see cref="Observe"/> only
    /// after a <c>Google2u.Text*.GetGenRow(string)</c> lookup returned null. This class stays free of
    /// Unity and game types so <c>Tests/LocalizationMisses</c> covers every decision without the game.
    ///
    /// Not thread-safe. The game localizes on the Unity main thread.
    /// </summary>
    internal sealed class LocalizationMissLog
    {
        public const int DefaultCap = 256;

        /// <summary>
        /// Bound on remembered passthrough keys. Past it a passthrough is re-filtered on each miss
        /// instead of remembered, so a stream of distinct literal strings cannot grow memory.
        /// </summary>
        public const int MaxRememberedPassthroughs = 4096;

        private const string KeyPrefix = "STR_";
        private const string DisplaySuffix = "Display";

        private readonly int _cap;
        private readonly Func<string, bool> _isFrameworkContentId;
        private readonly Dictionary<string, HashSet<string>> _seen =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private int _logged;
        private int _passthroughs;
        private bool _capReached;

        /// <param name="cap">Distinct keys to log before the logger stops.</param>
        /// <param name="isFrameworkContentId">
        /// True for a content ID the framework registered a display name for (the part after
        /// <c>STR_</c>). May be null, which treats no ID as framework content.
        /// </param>
        public LocalizationMissLog(int cap, Func<string, bool> isFrameworkContentId)
        {
            _cap = cap < 0 ? 0 : cap;
            _isFrameworkContentId = isFrameworkContentId;
        }

        public int Cap { get { return _cap; } }
        public int LoggedCount { get { return _logged; } }
        public bool CapReached { get { return _capReached; } }

        /// <summary>
        /// Decide what to do with a miss of <paramref name="key"/> in <paramref name="table"/>.
        /// Each (table, key) pair returns <see cref="LocalizationMissDecision.Log"/> at most once.
        /// </summary>
        public LocalizationMissDecision Observe(string table, string key)
        {
            // After the cap line the logger is finished; this is the cheapest possible exit.
            if (_capReached || table == null || string.IsNullOrEmpty(key)) return LocalizationMissDecision.Ignore;

            HashSet<string> seen;
            if (!_seen.TryGetValue(table, out seen))
            {
                seen = new HashSet<string>(StringComparer.Ordinal);
                _seen[table] = seen;
            }
            if (seen.Contains(key)) return LocalizationMissDecision.Ignore;

            if (IsIntentionalPassthrough(key, _isFrameworkContentId))
            {
                if (_passthroughs < MaxRememberedPassthroughs)
                {
                    seen.Add(key);
                    _passthroughs++;
                }
                return LocalizationMissDecision.Ignore;
            }

            if (_logged >= _cap)
            {
                _capReached = true;
                return LocalizationMissDecision.CapReached;
            }
            seen.Add(key);
            _logged++;
            return LocalizationMissDecision.Log;
        }

        /// <summary>
        /// True for a key the framework hands to the text tables knowing it will miss, because the
        /// game then shows the key itself or a framework postfix replaces the result:
        /// <list type="bullet">
        /// <item>Literal text that is not a <c>STR_</c> key: <c>Content.AddEncounter</c> display
        /// names, adventure names and info text, and quest dialogue pages, all shown verbatim.</item>
        /// <item><c>STR_&lt;int&gt;Display</c>: a synthetic realm's name key from
        /// <c>HexLand.GetRealmDisplayValue</c>, substituted in <c>Core/Localization.cs</c>.</item>
        /// <item><c>STR_&lt;int&gt;</c>: a synthetic enemy ID in the decimal form it takes after a
        /// Photon round trip (<c>MiniHexEnemy.m_EnemyType</c>).</item>
        /// <item><c>STR_&lt;id&gt;</c> for a registered content ID: custom enemy, item, weapon,
        /// ability and class names, which the <c>Core/Localization.cs</c> postfixes replace.</item>
        /// </list>
        /// Every vanilla row ID in the 14 tables is a named enum member, so a synthetic integer never
        /// collides with a real key. Vanilla also has a few non-<c>STR_</c> row IDs; a miss on such a
        /// key is not logged, which is the price of ignoring literal text.
        /// </summary>
        public static bool IsIntentionalPassthrough(string key, Func<string, bool> isFrameworkContentId)
        {
            if (string.IsNullOrEmpty(key)) return true;
            if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal)) return true;
            if (IsSyntheticIdKey(key)) return true;
            return isFrameworkContentId != null
                && key.Length > KeyPrefix.Length
                && isFrameworkContentId(key.Substring(KeyPrefix.Length));
        }

        /// <summary>True for <c>STR_&lt;int&gt;</c> or <c>STR_&lt;int&gt;Display</c>.</summary>
        internal static bool IsSyntheticIdKey(string key)
        {
            if (key == null || !key.StartsWith(KeyPrefix, StringComparison.Ordinal)) return false;
            int i = KeyPrefix.Length;
            if (i < key.Length && key[i] == '-') i++;
            int digitsStart = i;
            while (i < key.Length && key[i] >= '0' && key[i] <= '9') i++;
            if (i == digitsStart) return false;
            if (i == key.Length) return true;
            return key.Length - i == DisplaySuffix.Length
                && string.CompareOrdinal(key, i, DisplaySuffix, 0, DisplaySuffix.Length) == 0;
        }

        /// <summary>
        /// Join up to <paramref name="maxFrames"/> caller frames, innermost first, after dropping the
        /// frames every miss shares: the logger itself, Harmony and reflection plumbing, the
        /// <c>GetGenRow</c> lookup and the <c>FTKHub</c> localization wrappers.
        /// </summary>
        public static string TrimCaller(IList<string> frames, int maxFrames)
        {
            if (frames == null || maxFrames <= 0) return "unknown";
            StringBuilder sb = new StringBuilder();
            int kept = 0;
            for (int i = 0; i < frames.Count && kept < maxFrames; i++)
            {
                string frame = frames[i];
                if (string.IsNullOrEmpty(frame) || IsPlumbingFrame(frame)) continue;
                if (kept > 0) sb.Append(" < ");
                sb.Append(frame);
                kept++;
            }
            return kept == 0 ? "unknown" : sb.ToString();
        }

        internal static bool IsPlumbingFrame(string frame)
        {
            return frame.IndexOf("GetGenRow", StringComparison.Ordinal) >= 0
                || frame.StartsWith("FTKHub.Localize", StringComparison.Ordinal)
                || frame.IndexOf("LocalizationMiss", StringComparison.Ordinal) >= 0
                || frame.StartsWith("HarmonyLib.", StringComparison.Ordinal)
                || frame.StartsWith("MonoMod.", StringComparison.Ordinal)
                || frame.StartsWith("System.Reflection.", StringComparison.Ordinal)
                || frame.StartsWith("DMD<", StringComparison.Ordinal);
        }

        public static string FormatMiss(string table, string key, string caller)
        {
            return "[loc-miss] " + table + " has no row '" + key + "' (caller: " + caller + ")";
        }

        public static string FormatCapReached(int cap)
        {
            return "[loc-miss] Logged " + cap + " distinct missing keys; further misses are not logged this session.";
        }
    }
}
