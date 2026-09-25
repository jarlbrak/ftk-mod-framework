using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>
        /// Protect a registered class from gaining Poison or Curse while outside combat.
        /// This includes tile hazards and other exploration sources. It does not cure an
        /// existing ailment or prevent tile damage and resource loss.
        /// </summary>
        public static bool AddOverworldAilmentImmunity(FTK_playerGameStart classRow, string displayName)
        {
            if (classRow == null || string.IsNullOrEmpty(classRow.m_ID) ||
                string.IsNullOrEmpty(displayName) || displayName.Trim().Length == 0) return false;
            FTK_playerGameStartDB db = Db<FTK_playerGameStartDB>();
            int id = db.GetIntFromID(classRow.m_ID);
            if (id < 0 || !ContentRegistry.IsRegisteredSyntheticId(id, typeof(FTK_playerGameStartDB)) ||
                !object.ReferenceEquals(db.GetEntryByInt(id), classRow)) return false;
            return OverworldAilmentImmunity.RegisterClass(id, displayName);
        }
    }
}
