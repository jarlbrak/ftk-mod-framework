namespace FTKModFramework.Core
{
    /// <summary>
    /// The CLOSED set of moments a class-innate passive trait may hook. Phase 1 (spec #78) is DORMANT: a
    /// value names the intended trigger and rides on a <see cref="PassiveTraitDef"/>, but no combat patch
    /// reads it yet (the trigger patches land in a later work item). Kept closed on purpose: every value maps
    /// to exactly one concrete future patch site, so a modder cannot request a hook the framework has not
    /// wired. Do NOT reorder or rename members: once the trigger patches are live these names/order are the
    /// stable public contract.
    /// </summary>
    public enum PassiveTrigger
    {
        /// <summary>Fires when the owning character is about to be hit by an attack.</summary>
        IncomingAttack,

        /// <summary>Fires when a consumable would apply a debuff to the owning character.</summary>
        ConsumableDebuff,

        /// <summary>
        /// Fires when the owning character drinks a PERSONAL consumable, and shares its beneficial effects with
        /// the rest of the party. Only personal drinks qualify: <c>ConsumableBase.UseItemBuff</c> passes a
        /// single-target group, whereas an orb (<c>OrbItemBase.UsePartyBuffOrb</c>) already passes the whole
        /// party and is left alone.
        /// </summary>
        ConsumableBuff,
    }
}
