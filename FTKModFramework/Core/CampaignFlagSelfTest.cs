using System;
using System.Collections.Generic;
using FullInspector;
using Newtonsoft.Json;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Startup self-test for the campaign-flag store (#41, spec #37 P3). It proves, WITHOUT a live
    /// <c>GameLogic</c> or any save file, that a populated <see cref="CampaignStateQuest"/> round-trips through
    /// BOTH serializers the store rides in production and recovers IDENTICAL flag keys/values AND the concrete
    /// subtype:
    /// <list type="bullet">
    /// <item>(a) <b>disk (FullSerializer):</b> <c>SerializationHelpers.Clone&lt;QuestLogicBase,
    /// FullSerializerSerializer&gt;(dummy)</c>. Storage type is <c>QuestLogicBase</c> -- the EXACT element type of
    /// the save's <c>m_QuestList</c> array -- so the runtime type (<c>CampaignStateQuest</c>) differs from the
    /// storage type and FullSerializer emits the <c>$type</c> discriminator (the named headline risk). Asserts the
    /// clone is a <see cref="CampaignStateQuest"/> with flag-equal <c>m_Flags</c>.</item>
    /// <item>(b) <b>co-op RPC (Newtonsoft <c>TypeNameHandling.Auto</c>):</b> round-trip a
    /// <c>List&lt;QuestLogicBase&gt;</c> -- the EXACT shape and settings <c>GameLogic</c> uses for the quest-table
    /// RPC sync (<c>SerializeObject(list, {TypeNameHandling.Auto})</c> /
    /// <c>DeserializeObject&lt;List&lt;QuestLogicBase&gt;&gt;(...)</c>, GameLogic.cs:529-545). The element's
    /// declared type is <c>QuestLogicBase</c>, so Auto emits/consumes the framework <c>$type</c>. Asserts the
    /// element comes back a <see cref="CampaignStateQuest"/> with flag-equal <c>m_Flags</c>.</item>
    /// </list>
    /// Emits exactly one <c>SELF-TEST PASS [campaign-flag-roundtrip]</c> line (or a matching FAIL). Wired the same
    /// gated way as <see cref="CampaignSelfTest"/>/<see cref="CollectNSelfTest"/> from
    /// <c>Content/AdventureContent.cs</c>. The store-injection / host-authority / load-rehydration paths are live
    /// <c>GameLogic</c> behaviour, exercised in-game; this gate isolates the serializer round-trip, which is the
    /// spec's named headline risk (does the FullSerializer <c>$type</c> recover the subtype?).
    /// </summary>
    internal static class CampaignFlagSelfTest
    {
        public static void Run()
        {
            try
            {
                CampaignStateQuest original = new CampaignStateQuest();
                original.m_Flags["intro_done"] = 1;
                original.m_Flags["boss_phase"] = 3;
                original.m_Flags["gold_milestone"] = 12345;

                string diskNote;
                bool diskOk = CheckDiskRoundTrip(original, out diskNote);

                string rpcNote;
                bool rpcOk = CheckRpcRoundTrip(original, out rpcNote);

                if (diskOk && rpcOk)
                    Plugin.Log.LogInfo("SELF-TEST PASS [campaign-flag-roundtrip]: " + original.m_Flags.Count +
                        " flags recovered identically by BOTH serializers (subtype CampaignStateQuest preserved). " +
                        "disk(FullSerializer): " + diskNote + "; rpc(Newtonsoft Auto): " + rpcNote + ".");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [campaign-flag-roundtrip]: diskOk=" + diskOk +
                        " (" + diskNote + ") rpcOk=" + rpcOk + " (" + rpcNote + ").");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [campaign-flag-roundtrip]: " + e);
            }
        }

        // (a) Disk path: FullSerializer Clone with storage type QuestLogicBase (the m_QuestList element type),
        // so $type is emitted for the non-sealed subtype. If $type does NOT round-trip the subtype the clone
        // comes back as a base QuestLogicBase (or a different type) and this surfaces the headline risk loudly.
        private static bool CheckDiskRoundTrip(CampaignStateQuest original, out string note)
        {
            QuestLogicBase clone = SerializationHelpers.Clone<QuestLogicBase, FullSerializerSerializer>(original);

            CampaignStateQuest typed = clone as CampaignStateQuest;
            if (typed == null)
            {
                note = "subtype LOST -- FullSerializer $type did not round-trip CampaignStateQuest (got " +
                    (clone == null ? "null" : clone.GetType().FullName) + "); storage type was QuestLogicBase";
                return false;
            }

            int matched;
            if (!FlagsEqual(original.m_Flags, typed.m_Flags, out matched))
            {
                note = "subtype OK but flags differ (recovered " + matched + "/" + original.m_Flags.Count + ")";
                return false;
            }

            note = "subtype preserved, " + matched + " flags equal";
            return true;
        }

        // (b) RPC path: Newtonsoft TypeNameHandling.Auto over a List<QuestLogicBase> -- the EXACT shape + settings
        // GameLogic uses to RPC-sync the quest table (GameLogic.cs:529-545). The element's declared type is
        // QuestLogicBase, so Auto emits the framework $type for the CampaignStateQuest element.
        private static bool CheckRpcRoundTrip(CampaignStateQuest original, out string note)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;

            List<QuestLogicBase> list = new List<QuestLogicBase> { original };
            string json = JsonConvert.SerializeObject(list, settings);
            List<QuestLogicBase> rtList = JsonConvert.DeserializeObject<List<QuestLogicBase>>(json, settings);

            QuestLogicBase rt = (rtList != null && rtList.Count > 0) ? rtList[0] : null;
            CampaignStateQuest typed = rt as CampaignStateQuest;
            if (typed == null)
            {
                note = "subtype LOST -- Newtonsoft $type did not round-trip CampaignStateQuest (got " +
                    (rt == null ? "null" : rt.GetType().FullName) + ")";
                return false;
            }

            int matched;
            if (!FlagsEqual(original.m_Flags, typed.m_Flags, out matched))
            {
                note = "subtype OK but flags differ (recovered " + matched + "/" + original.m_Flags.Count + ")";
                return false;
            }

            note = "subtype preserved, " + matched + " flags equal";
            return true;
        }

        /// <summary>Same keys, same count, same value for every key. Out-param reports how many keys matched.</summary>
        private static bool FlagsEqual(Dictionary<string, int> a, Dictionary<string, int> b, out int matched)
        {
            matched = 0;
            if (a == null || b == null || a.Count != b.Count) return false;

            foreach (KeyValuePair<string, int> kv in a)
            {
                int v;
                if (!b.TryGetValue(kv.Key, out v) || v != kv.Value) return false;
                matched++;
            }
            return matched == a.Count;
        }
    }
}
