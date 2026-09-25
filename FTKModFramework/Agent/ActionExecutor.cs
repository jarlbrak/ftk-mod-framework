using System;
using System.Collections.Generic;

namespace FTKModFramework.Agent
{
    // Main-thread action boundary. Gameplay changes must go through native input so the game's
    // own UI, validation, and state transitions remain responsible for the result.
    internal static class ActionExecutor
    {
        public static object Execute(string action, IDictionary<string, object> args)
        {
            if (string.IsNullOrEmpty(action)) return Fail("missing 'action'");
            try
            {
                switch (action)
                {
                    case "native_input": return NativeInput.Submit(args);
                    case "input_cancel": return NativeInput.Cancel();
                    case "prepare_offline": return PrepareOffline();
                    default: return Fail("unsupported action '" + action + "'; use native_input for game controls and GET /state or /ui for observation");
                }
            }
            catch (Exception e) { return Fail(action + ": " + e.Message); }
        }

        private static object PrepareOffline()
        {
            if (uiStartGame.Instance == null || uiStartGame.Instance.m_GameStarted)
                return Fail("prepare_offline requires the title/setup screen before a run");
            string network = PhotonNetwork.connectionStateDetailed.ToString();
            if (!PhotonNetwork.offlineMode && network != "Disconnected" && network != "PeerCreated" && network != "Uninitialized")
                return Fail("prepare_offline requires a disconnected game");
            bool previous = uiStartGame.Instance.m_UseOnlineSinglePlayer;
            uiStartGame.Instance.m_UseOnlineSinglePlayer = false;
            return Ok(new Dictionary<string, object> {
                {"useOnlineSinglePlayer", false}, {"previousUseOnlineSinglePlayer", previous},
                {"scope", "title-screen single-player network configuration; no run started"}
            });
        }

        private static Dictionary<string, object> Ok(object result)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = true;
            d["error"] = null;
            d["result"] = result;
            return d;
        }

        private static Dictionary<string, object> Fail(string error)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = false;
            d["error"] = error;
            d["result"] = null;
            return d;
        }

    }
}
