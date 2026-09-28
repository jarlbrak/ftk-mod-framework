using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core.Diagnostics
{
    /// <summary>
    /// Stuck-turn watchdog and host acknowledgement watch (spec #265 FR-1). Diagnostics only: it
    /// reads game state at about 2 Hz and writes one <c>STUCK-TURN</c> Warning line per stuck
    /// episode. It never patches, sends an RPC or changes game state. A Warning is not an error,
    /// so the reporting ring buffer keeps the line without raising an error prompt.
    ///
    /// The gate it watches is uiEndTurnButton.Update, which recomputes interactable every frame:
    /// in the overworld it is on only when the Movement FSM is in Tracking or NoMoreActions, or
    /// the active sub-FSM is OnStopAtHex, and off while the location menu, inventory, global
    /// message panel, portrait message panel or spectator mode is showing. The idle path (not in
    /// a run, not this machine's turn, button interactable) reads static singletons and value
    /// fields only, so it allocates nothing. The Movement, turn and GameFlowMC FSMs are PlayMaker
    /// components the framework does not reference, so the snapshot reads their state names by
    /// reflection and writes "unavailable" when it cannot.
    /// </summary>
    internal sealed class StuckTurnWatchdog : MonoBehaviour
    {
        private const float IntervalSeconds = 0.5f;

        private readonly StuckTurnWatch _turn = new StuckTurnWatch();
        private readonly HostAckWatch _host = new HostAckWatch();
        private float _next;
        private static AccessTools.FieldRef<GameFlowMC, WaitForClientAcknowledge> _ackRef;

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _next) return;
            _next = now + IntervalSeconds;
            try
            {
                Poll(now);
            }
            catch (Exception e)
            {
                // A diagnostic must never disturb play. Stop for the rest of the process.
                enabled = false;
                Plugin.Log.LogWarning("Stuck-turn watchdog stopped after an unexpected error: " + e.Message);
            }
        }

        private void Poll(float now)
        {
            uiStartGame start = uiStartGame.Instance;
            GameLogic logic = start != null && start.m_GameStarted ? GameLogic.Instance : null;
            bool inRun = logic != null;

            StuckTurnPoll poll = new StuckTurnPoll { InRun = inRun };
            if (inRun) ReadTurnGate(logic, ref poll);
            StuckTurnTrigger trigger = _turn.Observe(poll, now);
            if (trigger != StuckTurnTrigger.None)
            {
                float waited = trigger == StuckTurnTrigger.Panel ? _turn.PanelElapsed : _turn.UnexplainedElapsed;
                Write(trigger == StuckTurnTrigger.Panel ? "panel-90s" : "unexplained-20s", waited, poll, logic);
            }

            GameFlowMC flow = inRun ? GameFlowMC.Instance : null;
            WaitForClientAcknowledge wait = flow != null ? AckWait(flow) : null;
            List<int> pending = wait != null ? wait.m_WaitList : null;
            if (_host.Observe(inRun, PhotonNetwork.isMasterClient, wait, wait != null ? wait.m_WaitID : null,
                    pending != null ? pending.Count : 0, now))
                Write("host-ack-15s", _host.Elapsed, poll, logic);
        }

        private static void ReadTurnGate(GameLogic logic, ref StuckTurnPoll poll)
        {
            PhotonPlayer local = PhotonNetwork.player;
            poll.MyTurn = local != null && logic.m_CurrentPlayer.PhotonID == local.ID;
            if (!poll.MyTurn) return;
            poll.TurnKey = logic.m_CurrentPlayer.TurnIndex;
            GameFlowMC flow = GameFlowMC.Instance;
            poll.EndTurnCount = flow != null ? flow.m_EndTurnCount : 0;

            uiEndTurnButton button = uiEndTurnButton.Instance;
            OverworldCamera camera = OverworldCamera.Instance;
            poll.OverworldGate = button != null && button.gameObject.activeInHierarchy
                && camera != null && camera.m_Camera != null && camera.m_Camera.enabled;
            poll.Interactable = button != null && button.interactable;
            if (!poll.OverworldGate || poll.Interactable) return;

            poll.PanelShowing = LocationMenuShowing() || InventoryShowing() || GlobalMessageShowing() || PortraitMessageShowing();
            uiOptionsMenu options = uiOptionsMenu.Instance;
            poll.OptionsOpen = options != null && options.m_Showing;
            poll.ChatFocused = ChatFocused();
            poll.GameAborted = logic.m_GameAborted;
        }

        private static WaitForClientAcknowledge AckWait(GameFlowMC flow)
        {
            if (_ackRef == null)
                _ackRef = AccessTools.FieldRefAccess<GameFlowMC, WaitForClientAcknowledge>("m_WaitForClientAck");
            return _ackRef(flow);
        }

        private static bool LocationMenuShowing()
        {
            uiLocationMenuDisplay menu = uiLocationMenuDisplay.Instance;
            return menu != null && menu.IsShowing();
        }

        private static bool InventoryShowing()
        {
            uiPlayerInventory inventory = uiPlayerInventory.Instance;
            return inventory != null && inventory.m_IsShowing;
        }

        private static bool GlobalMessageShowing()
        {
            FTKUI ui = FTKUI.Instance;
            return ui != null && ui.m_GlobalMessage != null && ui.m_GlobalMessage.m_MessagePanel.gameObject.activeSelf;
        }

        private static bool PortraitMessageShowing()
        {
            FTKUI ui = FTKUI.Instance;
            return ui != null && ui.m_PortraitMessage != null && ui.m_PortraitMessage.m_MessagePanel.gameObject.activeSelf;
        }

        private static bool ChatFocused()
        {
            uiChatBox chat = uiChatBox.Instance;
            return chat != null && (chat.IsTextInputInFocus() || chat.HasFocus);
        }

        // Once per episode, so allocation and reflection are acceptable here. Every field is read
        // in its own guard, so one missing object cannot hide the rest of the snapshot.
        private void Write(string reason, float waited, StuckTurnPoll poll, GameLogic logic)
        {
            StuckTurnLine line = new StuckTurnLine();
            line.Add("reason", reason).Add("waited", waited);
            Field(line, "master", () => PhotonNetwork.isMasterClient ? "1" : "0");
            Field(line, "mode", () => logic.m_GameMode.ToString());
            line.Add("myTurn", poll.MyTurn).Add("turn", poll.TurnKey).Add("endTurns", poll.EndTurnCount);
            line.Add("button", poll.Interactable).Add("gate", poll.OverworldGate);

            Field(line, "movement", () => FsmState(Reflect.GetField(Movement.Instance, "m_MovementFSM")));
            Field(line, "movementSub", () => SubFsmState(Reflect.GetField(Movement.Instance, "m_MovementFSM")));
            Field(line, "turnEngage", () =>
            {
                CharacterOverworld cow = logic.GetCurrentCOW();
                return cow == null ? "no-cow" : FsmState(Reflect.GetField(cow, "m_TurnEngage"));
            });
            Field(line, "gameFlowMC", () => FsmState(Reflect.GetField(GameFlowMC.Instance, "m_GameFlowMCFSM")));

            Field(line, "panels", () => "loc:" + Bit(LocationMenuShowing()) + ",inv:" + Bit(InventoryShowing())
                + ",global:" + Bit(GlobalMessageShowing()) + ",portrait:" + Bit(PortraitMessageShowing())
                + ",spectator:" + Bit(GameFlow.Instance != null && GameFlow.Instance.m_SpectatorMode));
            line.Add("options", poll.OptionsOpen).Add("chat", poll.ChatFocused).Add("aborted", poll.GameAborted);
            Field(line, "popupWait", () => Bit(FTKInput.Instance.m_WaitingForPopup));
            Field(line, "focus", () =>
            {
                FTKInputFocus focus = FTKInput.Instance.m_CurrentInputFocus;
                return focus == null ? "none" : focus.GetType().Name + "/" + focus.gameObject.name;
            });
            Field(line, "uiInput", () => CanvasInput("other", uiCanvasGroupOtherPanel.Instance)
                + "," + CanvasInput("main", FTKUI.Instance.m_MainCanvas));

            Field(line, "acks", () =>
            {
                WaitForClientAcknowledge wait = GameFlowMC.Instance != null ? AckWait(GameFlowMC.Instance) : null;
                if (wait == null || wait.m_WaitList == null || wait.m_WaitList.Count == 0) return "none";
                string ids = "";
                foreach (int id in wait.m_WaitList) ids += (ids.Length == 0 ? "" : ",") + id;
                return wait.m_WaitID + ":[" + ids + "]";
            });
            Field(line, "message", () => MessageCoordinator.Instance.CurrentMessageType() + "/from:"
                + MessageCoordinator.Instance.CurrentMsgSender());
            Field(line, "logicUpdating", () => "gl:" + Bit(logic.IsWaiting()) + ",mc:"
                + Bit(GameFlowMC.Instance != null && GameFlowMC.Instance.m_GameLogicUpdating));

            Field(line, "camera", () => Bit(OverworldCamera.Instance.m_Camera.enabled));
            Field(line, "encounter", () => "menu:" + Bit(FTKUI.Instance.m_EncounterMenu.m_MenuOn)
                + ",combat:" + Bit(EncounterSession.Instance.m_IsInCombat));
            Field(line, "cows", () =>
            {
                string cows = "";
                foreach (CharacterOverworld cow in FTKHub.Instance.m_CharacterOverworlds)
                {
                    if (cow == null) continue;
                    cows += (cows.Length == 0 ? "" : ";") + "t" + cow.m_FTKPlayerID.TurnIndex
                        + ":dungeon" + Bit(cow.m_EnteredDungeon)
                        + ",combat" + Bit(cow.m_CharacterStats != null && cow.m_CharacterStats.m_IsInCombat)
                        + ",ap" + (cow.m_CharacterStats != null ? cow.m_CharacterStats.m_ActionPoints : -1);
                }
                return cows.Length == 0 ? "none" : cows;
            });

            Plugin.Log.LogWarning(line.ToString());
        }

        private static void Field(StuckTurnLine line, string key, Func<string> read)
        {
            string value;
            try
            {
                value = read();
            }
            catch (Exception)
            {
                value = "unavailable";
            }
            line.Add(key, value);
        }

        private static string Bit(bool value) { return value ? "1" : "0"; }

        private static string CanvasInput(string name, Component holder)
        {
            CanvasGroup group = holder != null ? holder.GetComponent<CanvasGroup>() : null;
            if (group == null) return name + ":none";
            return name + ":" + Bit(group.interactable) + Bit(group.blocksRaycasts);
        }

        private static string FsmState(object fsm)
        {
            if (fsm == null) return "none";
            PropertyInfo state = fsm.GetType().GetProperty("ActiveStateName");
            return state == null ? "unavailable" : (state.GetValue(fsm, null) as string ?? "none");
        }

        // FTKUtil.GetCurrentSubFSM(PlayMakerFSM) is the walk the end-turn gate itself uses. It only
        // reads SubFsmList and Active.
        private static string SubFsmState(object fsm)
        {
            if (fsm == null) return "none";
            MethodInfo walk = typeof(FTKUtil).GetMethod("GetCurrentSubFSM", new[] { fsm.GetType() });
            object sub = walk != null ? walk.Invoke(null, new[] { fsm }) : null;
            if (sub == null) return "unavailable";
            PropertyInfo name = sub.GetType().GetProperty("Name");
            return (name != null ? name.GetValue(sub, null) as string : "?") + ":" + FsmState(sub);
        }
    }
}
