using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static JObject ThiefStateProbe(JObject command)
    {
        int heroId = Int(command, "heroInstanceId", 0);
        if (heroId == 0 || EncounterSession.Instance == null)
            throw new InvalidOperationException("An exact active heroInstanceId and encounter are required.");
        CharacterDummy actor = null;
        foreach (CharacterDummy candidate in EncounterSession.Instance.m_PlayerDummies.Values)
            if (candidate != null && candidate.m_CharacterOverworld != null &&
                candidate.m_CharacterOverworld.GetInstanceID() == heroId) actor = candidate;
        if (actor == null) throw new InvalidOperationException("The hero is absent from this encounter.");

        Type runtime = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name == "FTKModFramework")
                runtime = assembly.GetType("FTKModFramework.Core.ThiefRuntime", true);
        if (runtime == null) throw new InvalidOperationException("Thief runtime is unavailable.");
        Type guardian = runtime.Assembly.GetType("FTKModFramework.Core.GuardianRuntime", true);
        string identity = (string)guardian.GetMethod("Identity", Statics, null,
            new[] { typeof(CharacterDummy) }, null).Invoke(null, new object[] { actor });
        object state = runtime.GetField("state", Statics).GetValue(null);
        Type rules = state.GetType();
        bool isThief = (bool)runtime.GetMethod("IsThief", Statics).Invoke(null, new object[] { actor });
        bool prepared = (bool)rules.GetMethod("HasPrepared", Members).Invoke(state, new object[] { identity });
        bool evasion = (bool)rules.GetMethod("HasEvasion", Members).Invoke(state, new object[] { identity });
        bool slipAwayAvailable = (bool)rules.GetMethod("SlipAwayAvailable", Members).Invoke(state, new object[] { identity });
        IDictionary pending = runtime.GetField("pending", Statics).GetValue(null) as IDictionary;
        return new JObject {
            { "ok", true }, { "provenance", "read-only private Thief state in an isolated single-player encounter" },
            { "heroInstanceId", heroId }, { "identity", identity },
            { "weaponId", (int)actor.m_CharacterOverworld.m_WeaponID }, { "isThief", isThief },
            { "prepared", prepared }, { "evasionArmed", evasion },
            { "slipAwayAvailable", slipAwayAvailable },
            { "pendingArtifactReceipts", pending == null ? -1 : pending.Count }
        };
    }
}
