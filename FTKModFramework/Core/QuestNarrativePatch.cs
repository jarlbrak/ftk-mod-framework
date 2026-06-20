using System;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Repairs a latent game bug on the custom-NPC quest-narrative path so a Reeve-Maddow-style story event (a
    /// <c>StoryEvent.Event</c> with <c>m_UserNPC</c> set) cannot softlock the game.
    ///
    /// <c>QuestLogicBase</c> has TWO <c>SetMessageTalkerParam</c> overloads. The <c>FTK_talkingHead.ID</c> overload
    /// calls <c>GetMessageParams()</c> first, which lazy-initializes the private <c>_messageParams</c> array
    /// (<c>new string[15]</c> via <c>SetMessageParams()</c>). The <c>string</c>/UserNPC overload FORGETS that call:
    /// <code>_messageParams[2] = GameLogic.Instance.GetGameDef().GetUserNPC(_userNPC).Name;</code>
    /// so when <c>_messageParams</c> is still null it stores into a null array and throws a NullReferenceException.
    /// That NRE is raised from a PlayMaker FSM action (<c>MessagePresenter.DeliverStartQuestMsgPart</c>) that retries
    /// every frame, which presents in-game as a SOFTLOCK. Vanilla content never triggers it because vanilla story
    /// events always use <c>m_Talker</c> (a talking head), never <c>m_UserNPC</c>; the framework's custom-NPC
    /// narrative is the first consumer of the UserNPC path.
    ///
    /// This prefix initializes the params array through the game's own <c>GetMessageParams()</c> (identical to what
    /// the sibling overload does), null-guards the UserNPC lookup, performs the [2] assignment safely (falling back
    /// to the raw key if the NPC is somehow missing), and skips the buggy original. It targets a CONCRETE,
    /// non-generic method (disambiguated to the <c>string</c> overload), so it is safe to patch.
    /// </summary>
    [HarmonyPatch(typeof(QuestLogicBase), "SetMessageTalkerParam", new[] { typeof(string) })]
    internal static class QuestUserNpcTalkerParam_Patch
    {
        private static bool Prefix(QuestLogicBase __instance, string _userNPC)
        {
            try
            {
                // GetMessageParams() lazy-inits _messageParams (string[15]) when null/empty and returns it. This is
                // exactly the init the FTK_talkingHead overload performs and the UserNPC overload omits.
                string[] prms = __instance.GetMessageParams();

                UserNPC npc = null;
                if (GameLogic.Instance != null)
                {
                    GameDefinition gd = GameLogic.Instance.GetGameDef();
                    if (gd != null) npc = gd.GetUserNPC(_userNPC);
                }

                if (prms != null && prms.Length > 2)
                    prms[2] = (npc != null && npc.Name != null) ? npc.Name : _userNPC;
            }
            catch (Exception e)
            {
                // Never throw out of an FSM-driven message step (that is what softlocks); log and move on.
                Plugin.Log.LogWarning("QuestUserNpcTalkerParam_Patch: " + e.Message);
            }
            return false; // assignment done safely above; skip the buggy original
        }
    }
}
