using UnityEngine;
using FTKModFramework.Behaviors;

namespace FTKMF.SampleBehaviorMod
{
    /// <summary>
    /// A SELF-CONTAINED sample steal behaviour shipped by an EXTERNAL mod DLL (com.ftkmf.samplebehaviormod),
    /// to prove the framework's BehaviorLoader external-DLL path (#33). It references ONLY the public
    /// <see cref="ContentBehaviorAttribute"/> (from FTKModFramework.dll) and the stock game types; it touches
    /// NO framework Core/ internal (it cannot: those are internal) and NO main-plugin helper.
    ///
    /// The framework reflects on the <c>[ContentBehavior("Steal")]</c> attribute and registers this type under
    /// (modGuid + ":Steal"), so the mod's content entry <c>behavior:"Steal"</c> wires it into the proficiency
    /// row's m_ProficiencyPrefab. ProficiencyManager Instantiate()s it; combat calls AddToDummy() on a hit.
    ///
    /// Kept deliberately SIMPLE for #33: a master-guarded gold steal (half the Entertain payout, inlined).
    /// #34 will make this the full 50/50 item-or-gold faithful to the compiled Thief and add an
    /// identical-to-compiled gate. Logging uses UnityEngine.Debug.Log (the plugin's Plugin.Log lives in the
    /// main assembly and is not referenced here).
    /// </summary>
    [ContentBehavior("Steal")]
    public class SampleStealBehavior : ProficiencyBase
    {
        public override void AddToDummy(CharacterDummy _dummy)
        {
            if (_dummy == null) return;

            CharacterDummy attacker = GetAttacker(_dummy);
            if (attacker == null || !(bool)attacker.m_CharacterOverworld) return;

            CharacterStats stats = attacker.m_CharacterOverworld.m_CharacterStats;
            if (stats == null) return;

            // AddToDummy runs on EVERY client (it is reached via the combat resolution / [PunRPC] path), so
            // only the master actually GRANTS gold; ChangeGold itself broadcasts the result to all clients.
            // In single-player isMasterClient is true, so the same path runs. (Matches the compiled Thief.)
            bool grant = PhotonNetwork.isMasterClient;

            // Half the Entertain payout (inlined): Entertain = Random(2..4) * (level+1) * GoldModifier; halve
            // and round. Floor at 1 so a landed steal always grants something. This is the same formula the
            // framework's ProficiencyMath.HalfEntertainGold uses, reproduced here so the sample DLL stays
            // self-contained (no main-plugin reference).
            int gold = FTKUtil.RoundToInt((float)(Random.Range(2, 5) * (stats.m_PlayerLevel + 1)) * stats.GoldModifier * 0.5f);
            if (gold < 1) gold = 1;

            if (grant) stats.ChangeGold(gold); // _hud:true by default; ChangeGold broadcasts to all clients.

            // Show the amount on the steal HUD instead of "Nothing To Steal" (every client sets its own HUD).
            if (_dummy.m_DamageInfo != null)
            {
                m_Category = ProficiencyBase.Category.StealGold;
                _dummy.m_DamageInfo.m_ProfHasAmount = true;
                _dummy.m_DamageInfo.m_ProficiencyAmount = gold;
            }

            Debug.Log("[SampleBehaviorMod] Steal grabbed " + gold + " gold (level " + stats.m_PlayerLevel + ").");
        }
    }
}
