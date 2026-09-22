namespace FTKModFramework.Core
{
    internal static class GuardianFocusedHit
    {
        internal static bool EligibleAttempt(int focusSpent, float slotSuccess, bool harmless, bool forcedMiss)
        {
            // Native zero-slot misses also produce Block, so the outcome alone cannot identify a hit.
            return focusSpent > 0 && slotSuccess > 0 && !harmless && !forcedMiss;
        }

        internal static bool Landed(int damage, CharacterDummy.AttackResponse response)
        {
            if (damage < 0 || response == CharacterDummy.AttackResponse.Dodge ||
                response == CharacterDummy.AttackResponse.HarmlessAttack) return false;
            // Armor and resistance absorption retain the successful attempt's Block/MagicBlock
            // response and still deliver RespondToHit. They need not remove health to count as hits.
            return damage > 0 || response == CharacterDummy.AttackResponse.Block || response == CharacterDummy.AttackResponse.MagicBlock;
        }
    }
}
