using UnityEngine;
using GridEditor;

namespace FTKModFramework
{
    /// <summary>
    /// The Cutpurse's signature combat behaviour — the Thief's Steal in reverse. When the Cutpurse lands
    /// its "Pilfer" attack on a party member it lifts a little of the party's shared gold (scaling with the
    /// victim's level, capped at what they actually carry).
    ///
    /// The gold mutation is MASTER-ONLY: CharacterStats.ChangeGold RPC-broadcasts the change to every
    /// client (RPCAllSelf -> ChangeGoldRPC), and AddToDummy itself runs on every client (it's reached via
    /// the [PunRPC] AddProfToDummy). If each client called ChangeGold the party would lose N x the gold, so
    /// only the master applies it and the broadcast reflects the single deduction everywhere. (In
    /// single-player PhotonNetwork.isMasterClient is true, so the same path works.)
    ///
    /// Wired in as the proficiency row's m_ProficiencyPrefab; ProficiencyManager Instantiate()s it and
    /// combat calls AddToDummy() on the victim when the action applies.
    ///
    /// Pilfer is gold-only: m_Category is set once to StealGold at seed time on the shared prefab and never
    /// mutated here, so it trivially satisfies the shared-instance rule (no per-hit category write needed).
    /// </summary>
    public class CutpurseStealProficiency : ProficiencyBase
    {
        public override void AddToDummy(CharacterDummy _dummy)
        {
            // Only the master mutates shared party gold; ChangeGold broadcasts the result to all clients.
            if (!PhotonNetwork.isMasterClient) return;

            CharacterOverworld victim = _dummy != null ? _dummy.m_CharacterOverworld : null;
            if (victim == null || victim.m_CharacterStats == null) return; // only players carry gold

            CharacterStats stats = victim.m_CharacterStats;

            // The same half-Entertain gold formula the Thief's Steal uses (shared helper).
            int gold = ProficiencyMath.HalfEntertainGold(stats);
            if (gold > stats.m_Gold) gold = stats.m_Gold; // can't steal more than they carry
            if (gold <= 0) return;

            stats.ChangeGold(-gold); // _hud:true -> the party sees the loss

            // Show the amount on the steal HUD instead of "Nothing To Steal".
            if (_dummy.m_DamageInfo != null)
            {
                _dummy.m_DamageInfo.m_ProfHasAmount = true;
                _dummy.m_DamageInfo.m_ProficiencyAmount = gold;
            }

            Plugin.Log.LogInfo("[Cutpurse] Pilfer stole " + gold + " gold from " + stats.m_CharacterName +
                " (level " + stats.m_PlayerLevel + ").");
        }
    }
}
