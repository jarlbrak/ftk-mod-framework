using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>Register a cosmetic race identity. Bind each supported class explicitly before selection.</summary>
        public static int AddRace(string modGuid, string id, string displayName)
        {
            return PlayerRaceRegistry.Register(modGuid, id, displayName);
        }

        /// <summary>
        /// Bind a race to an exact registered class row and a cloned native skinset. The race owns the
        /// body plan for this binding; native garments and equipped item apparel remain available.
        /// This never modifies the class's skinset array or the template skinset.
        /// </summary>
        public static bool SetRaceClassBodyMeshesFromGlb(int raceId, FTK_playerGameStart classRow,
            FTK_skinset.ID template, PlayerRendererMesh[] requiredBodyMeshes, PlayerApparelMesh[] conditionalApparel)
        {
            return PlayerRaceRegistry.Bind(raceId, classRow, template, requiredBodyMeshes, conditionalApparel);
        }
    }
}
