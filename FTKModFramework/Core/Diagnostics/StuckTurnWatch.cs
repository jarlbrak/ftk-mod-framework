using System;
using System.Text;

namespace FTKModFramework.Core.Diagnostics
{
    /// <summary>One poll's view of the local end-turn gate (spec #265 FR-1). The watchdog in
    /// <c>Core/Diagnostics/StuckTurnWatchdog.cs</c> fills it from the game; every flag defaults to the
    /// value that keeps the watch idle or suppressed.</summary>
    internal struct StuckTurnPoll
    {
        /// <summary>A run is on screen: uiStartGame.m_GameStarted and GameLogic exist.</summary>
        internal bool InRun;
        /// <summary>GameLogic.m_CurrentPlayer belongs to this machine's Photon player.</summary>
        internal bool MyTurn;
        /// <summary>The overworld camera is enabled and the end-turn button is active, so
        /// uiEndTurnButton.Update is running its overworld branch.</summary>
        internal bool OverworldGate;
        /// <summary>uiEndTurnButton.Instance.interactable.</summary>
        internal bool Interactable;
        /// <summary>The gate's own panel flags: location menu, inventory, global message or portrait
        /// message showing.</summary>
        internal bool PanelShowing;
        /// <summary>uiOptionsMenu.m_Showing.</summary>
        internal bool OptionsOpen;
        /// <summary>The chat box holds text input focus.</summary>
        internal bool ChatFocused;
        /// <summary>GameLogic.m_GameAborted: a disconnect is ending the run.</summary>
        internal bool GameAborted;
        /// <summary>GameFlowMC.m_EndTurnCount. It advances only on the master.</summary>
        internal int EndTurnCount;
        /// <summary>The current player's turn index, so a client also sees the turn change.</summary>
        internal int TurnKey;
    }

    /// <summary>Why a turn snapshot was written.</summary>
    internal enum StuckTurnTrigger
    {
        None,
        /// <summary>The button stayed off with no panel explaining it.</summary>
        Unexplained,
        /// <summary>The button stayed off behind a gate panel for the long threshold.</summary>
        Panel,
    }

    /// <summary>The stuck-turn episode state machine. Unity-free, so Tests/StuckTurn drives it
    /// with a fake clock. An episode starts when it is this machine's turn, the overworld gate is
    /// running and the button is not interactable. It ends when the button becomes interactable,
    /// the turn changes (end-turn count or turn index), or the gate stops applying. Time only
    /// accumulates while not suppressed; each step is clamped so a load hitch counts at most
    /// <see cref="MaxStepSeconds"/>. At most one snapshot is reported per episode, and at most
    /// <see cref="MaxSnapshots"/> per watch.</summary>
    internal sealed class StuckTurnWatch
    {
        internal const float UnexplainedSeconds = 20f;
        internal const float PanelSeconds = 90f;
        internal const float MaxStepSeconds = 1f;
        internal const int MaxSnapshots = 10;

        private bool _active, _reported;
        private int _endTurnCount, _turnKey, _snapshots;
        private float _last, _unexplained, _panel;

        internal bool InEpisode { get { return _active; } }
        internal float UnexplainedElapsed { get { return _unexplained; } }
        internal float PanelElapsed { get { return _panel; } }
        internal int Snapshots { get { return _snapshots; } }

        internal StuckTurnTrigger Observe(StuckTurnPoll poll, float now)
        {
            if (!poll.InRun || !poll.MyTurn || !poll.OverworldGate || poll.Interactable)
            {
                _active = false;
                return StuckTurnTrigger.None;
            }
            if (_active && (poll.EndTurnCount != _endTurnCount || poll.TurnKey != _turnKey)) _active = false;
            if (!_active)
            {
                _active = true;
                _reported = false;
                _endTurnCount = poll.EndTurnCount;
                _turnKey = poll.TurnKey;
                _unexplained = 0f;
                _panel = 0f;
                _last = now;
                return StuckTurnTrigger.None;
            }

            float step = Step(ref _last, now);
            if (poll.OptionsOpen || poll.ChatFocused || poll.GameAborted) return StuckTurnTrigger.None;
            if (poll.PanelShowing) _panel += step;
            else _unexplained += step;

            if (_reported || _snapshots >= MaxSnapshots) return StuckTurnTrigger.None;
            StuckTurnTrigger trigger = _unexplained >= UnexplainedSeconds ? StuckTurnTrigger.Unexplained
                : _panel >= PanelSeconds ? StuckTurnTrigger.Panel : StuckTurnTrigger.None;
            if (trigger != StuckTurnTrigger.None)
            {
                _reported = true;
                _snapshots++;
            }
            return trigger;
        }

        internal static float Step(ref float last, float now)
        {
            float step = now - last;
            last = now;
            if (step < 0f) return 0f;
            return step > MaxStepSeconds ? MaxStepSeconds : step;
        }
    }

    /// <summary>The master's client acknowledgement watch. GameFlowMC keeps one
    /// WaitForClientAcknowledge at a time and replaces it for each barrier; an entry is pending
    /// while its m_WaitList is not empty. The same wait object and wait ID pending for
    /// <see cref="PendingSeconds"/> reports once. A new object, a new wait ID or an empty list
    /// ends the episode.</summary>
    internal sealed class HostAckWatch
    {
        internal const float PendingSeconds = 15f;

        private object _wait;
        private string _waitId;
        private bool _active, _reported;
        private float _last, _elapsed;
        private int _snapshots;

        internal float Elapsed { get { return _elapsed; } }
        internal int Snapshots { get { return _snapshots; } }

        internal bool Observe(bool inRun, bool isMaster, object wait, string waitId, int pendingCount, float now)
        {
            if (!inRun || !isMaster || wait == null || pendingCount <= 0)
            {
                _active = false;
                _wait = null;
                return false;
            }
            if (_active && (!ReferenceEquals(_wait, wait) || !string.Equals(_waitId, waitId, StringComparison.Ordinal)))
                _active = false;
            if (!_active)
            {
                _active = true;
                _reported = false;
                _wait = wait;
                _waitId = waitId;
                _elapsed = 0f;
                _last = now;
                return false;
            }
            _elapsed += StuckTurnWatch.Step(ref _last, now);
            if (_reported || _snapshots >= StuckTurnWatch.MaxSnapshots || _elapsed < PendingSeconds) return false;
            _reported = true;
            _snapshots++;
            return true;
        }
    }

    /// <summary>Builds the one-line snapshot: <c>STUCK-TURN key=value ...</c> in insertion order.
    /// Values are reduced to printable ASCII with no spaces, so the line is one log entry and its
    /// character count equals its UTF-8 byte count; the whole line is capped at
    /// <see cref="MaxLength"/>. The softlock signatures reuse it with their own prefix.</summary>
    internal sealed class StuckTurnLine
    {
        internal const string Prefix = "STUCK-TURN";
        internal const int MaxLength = 2000;
        internal const int MaxValueLength = 160;
        private const string Truncated = " truncated=1";

        private readonly StringBuilder _text;

        internal StuckTurnLine() : this(Prefix) { }

        internal StuckTurnLine(string prefix)
        {
            _text = new StringBuilder(prefix, 512);
        }

        internal StuckTurnLine Add(string key, string value)
        {
            return Add(key, value, MaxValueLength);
        }

        /// <summary>Adds a value with its own length cap, for the one field (a stack trace) that
        /// needs more than <see cref="MaxValueLength"/>. The line cap still applies.</summary>
        internal StuckTurnLine Add(string key, string value, int maxValueLength)
        {
            _text.Append(' ').Append(key).Append('=');
            if (string.IsNullOrEmpty(value))
            {
                _text.Append('-');
                return this;
            }
            if (maxValueLength < 1) maxValueLength = 1;
            int length = Math.Min(value.Length, maxValueLength);
            for (int i = 0; i < length; i++)
            {
                char c = value[i];
                _text.Append(c > ' ' && c < 127 ? c : '_');
            }
            if (value.Length > maxValueLength) _text.Append("...");
            return this;
        }

        internal StuckTurnLine Add(string key, bool value) { return Add(key, value ? "1" : "0"); }

        internal StuckTurnLine Add(string key, int value)
        {
            return Add(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        internal StuckTurnLine Add(string key, float seconds)
        {
            return Add(key, seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s");
        }

        public override string ToString()
        {
            if (_text.Length <= MaxLength) return _text.ToString();
            return _text.ToString(0, MaxLength - Truncated.Length) + Truncated;
        }
    }
}
