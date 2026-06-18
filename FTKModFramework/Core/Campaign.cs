using System.Collections.Generic;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Public modder-facing accessors for the Core-owned campaign-flag store (#41, spec #37 P3). Flags are
    /// arbitrary string-keyed integers persisted on a single invisible dummy quest (<see cref="CampaignStateQuest"/>)
    /// living in <c>GameLogic._fullQuestTable</c> under <see cref="SentinelKey"/>. The store is saved to disk and
    /// synced to co-op clients for free because it rides the game's existing quest-table serialize/RPC path (see
    /// <see cref="CampaignStateQuest"/> for the mechanism).
    ///
    /// HOST AUTHORITY (NFR-4 / co-op determinism): writes (<see cref="SetFlag"/>) are HOST-ONLY. On a non-host
    /// peer <see cref="SetFlag"/> is a no-op + warning; clients receive flag changes through the host's quest-table
    /// RPC sync, exactly like every other quest-state field. Reads (<see cref="GetFlag"/>/<see cref="HasFlag"/>)
    /// work on any peer. All accessors null-guard <c>GameLogic.Instance</c>: called outside a game they are inert
    /// (read -&gt; 0/false, write -&gt; no-op) and never throw.
    ///
    /// No engine type appears in any public signature (only <c>string</c>/<c>int</c>/<c>bool</c>), keeping the
    /// Content-facing surface free of internal plumbing.
    /// </summary>
    public static class Campaign
    {
        /// <summary>
        /// The fixed quest-table key the dummy lives under. <c>int.MinValue</c> sits outside BOTH quest-id bands
        /// (story = negative, generated = positive off <c>GameLogic.m_QuestID</c>), so it can never collide with a
        /// real quest id. The dummy's own <c>m_QuestID</c> is set to match at injection time.
        /// </summary>
        public const int SentinelKey = int.MinValue;

        /// <summary>
        /// Set a campaign flag. HOST-ONLY: on a client this logs a warning and does nothing (the client will
        /// receive the value via the host's quest-table sync). No-op (no throw) when called outside a live game.
        /// </summary>
        public static void SetFlag(string key, int value)
        {
            if (string.IsNullOrEmpty(key))
            {
                Plugin.Log.LogWarning("Campaign.SetFlag: empty key ignored.");
                return;
            }

            // Host authority: the same guard the framework's combat behaviours use (true in single-player).
            if (!PhotonNetwork.isMasterClient)
            {
                Plugin.Log.LogWarning("Campaign.SetFlag('" + key + "'): ignored on a non-host client " +
                    "(flags are host-authoritative and arrive via quest-table sync).");
                return;
            }

            CampaignStateQuest store = ResolveOrInject();
            if (store == null) return; // no live game -> no-op (ResolveOrInject logged once if needed)

            store.m_Flags[key] = value;
        }

        /// <summary>Read a campaign flag, returning 0 when absent (or when called outside a live game).</summary>
        public static int GetFlag(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;

            CampaignStateQuest store = Resolve();
            if (store == null) return 0;

            int value;
            return store.m_Flags.TryGetValue(key, out value) ? value : 0;
        }

        /// <summary>True when the flag is present (false when absent or called outside a live game).</summary>
        public static bool HasFlag(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            CampaignStateQuest store = Resolve();
            return store != null && store.m_Flags.ContainsKey(key);
        }

        /// <summary>
        /// One-shot host-only registration entry point for a fresh game (NFR-4). Inject-if-absent: it only
        /// creates and inserts a fresh empty dummy when the sentinel key is NOT already present. After a LOAD the
        /// deserialized dummy is already in the table with its saved flags (see <see cref="CampaignStateQuest"/>),
        /// so this never overwrites a rehydrated store. Idempotent and safe to call more than once. Reads/writes
        /// also materialize the store lazily, so calling this explicitly is optional; it exists so a host can
        /// guarantee the store is present from game-start even before the first flag access.
        /// </summary>
        public static void EnsureStore()
        {
            if (!PhotonNetwork.isMasterClient) return; // host creates the store; clients receive it via sync
            ResolveOrInject();
        }

        // ---- internals -------------------------------------------------------------------------------------

        /// <summary>Resolve the existing dummy WITHOUT creating one. Returns null outside a live game or before injection.</summary>
        private static CampaignStateQuest Resolve()
        {
            GameLogic gl = GameLogic.Instance;
            if (gl == null) return null;

            Dictionary<int, QuestLogicBase> table = gl.GetQuestTable();
            if (table == null) return null;

            QuestLogicBase q;
            return table.TryGetValue(SentinelKey, out q) ? q as CampaignStateQuest : null;
        }

        /// <summary>
        /// Resolve the dummy, lazily injecting a fresh empty one under <see cref="SentinelKey"/> if absent. Returns
        /// null only when there is no live <c>GameLogic</c> (outside a game). Inject-if-absent: never overwrites an
        /// existing (possibly rehydrated) dummy. Injects DIRECTLY into the table (NOT via
        /// <c>GameLogic.RegisterQuest</c>, which assigns ids, expands subquests, RPC-broadcasts, and calls
        /// AssignDescription on a def-less quest -- all wrong + heavy for an invisible store).
        /// </summary>
        private static CampaignStateQuest ResolveOrInject()
        {
            GameLogic gl = GameLogic.Instance;
            if (gl == null)
            {
                Plugin.Log.LogWarning("Campaign: no live GameLogic; flag store unavailable (call inside a game).");
                return null;
            }

            Dictionary<int, QuestLogicBase> table = gl.GetQuestTable(); // creates the dict if null (GameLogic.cs:381)

            QuestLogicBase existing;
            if (table.TryGetValue(SentinelKey, out existing))
            {
                CampaignStateQuest store = existing as CampaignStateQuest;
                if (store != null) return store; // already present (fresh-injected earlier or rehydrated on load)
                // Sentinel occupied by an unexpected type: do not clobber game state; surface and bail.
                Plugin.Log.LogError("Campaign: sentinel key occupied by " + existing.GetType().FullName +
                    " (expected CampaignStateQuest); leaving it untouched.");
                return null;
            }

            CampaignStateQuest fresh = new CampaignStateQuest();
            fresh.m_QuestID = SentinelKey; // keep the dummy's id consistent with its table key
            table[SentinelKey] = fresh;
            Plugin.Log.LogInfo("Campaign: injected fresh flag store at sentinel key " + SentinelKey + ".");
            return fresh;
        }
    }
}
