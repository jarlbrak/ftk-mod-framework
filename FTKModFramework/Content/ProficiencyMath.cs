using UnityEngine;

namespace FTKModFramework
{
    /// <summary>
    /// Shared math for the steal-style proficiencies. Keeps the one gold formula in a single place so the
    /// Thief's Steal and the Cutpurse's Pilfer can never drift apart.
    /// </summary>
    internal static class ProficiencyMath
    {
        /// <summary>
        /// Half the Entertain payout: Entertain = Random(2..4) * (level+1) * GoldModifier; this returns that
        /// halved and rounded. Returns the RAW amount: callers apply their own clamps (Thief floors at 1,
        /// Cutpurse caps at what the victim carries).
        /// </summary>
        internal static int HalfEntertainGold(CharacterStats stats)
        {
            return FTKUtil.RoundToInt((float)(Random.Range(2, 5) * (stats.m_PlayerLevel + 1)) * stats.GoldModifier * 0.5f);
        }
    }
}
