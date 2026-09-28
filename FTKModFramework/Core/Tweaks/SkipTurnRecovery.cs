namespace FTKModFramework.Core
{
    /// <summary>Why an open skip-turn episode is recovered.</summary>
    internal enum SkipTurnTrigger
    {
        None,
        /// <summary>uiMovementSlots.ForceHide ran with m_Root active, so it called StopAllCoroutines.</summary>
        ForceHide,
        /// <summary>m_Root was deactivated before SkipTurnUI.Close ran.</summary>
        RootHidden,
        /// <summary>The uiMovementSlots object left the hierarchy, and Unity stopped its coroutines.</summary>
        HostInactive,
        /// <summary>Ten seconds of scaled time passed without a close.</summary>
        Timeout,
    }

    internal enum SkipTurnEpisodeState
    {
        Idle,
        /// <summary>Armed by InitializeSkipTurn; the popup coroutine should close it.</summary>
        Open,
        /// <summary>The coroutine can no longer close it; recovery waits for an active host.</summary>
        Pending,
    }

    /// <summary>
    /// One skip-turn popup episode (fix.stuck-skip-turn-popup, spec #265 FR-4). Unity-free, so
    /// Tests/Tweaks drives it with a fake clock.
    ///
    /// Vanilla: uiMovementSlots.InitializeSkipTurn calls ForceHide, BaseInitialize (m_Root on),
    /// RemoveSkipTurn (the skip is spent before the popup shows) and then starts the coroutine
    /// SlotDisplayExpandSkipTurn: m_TransitionTime (1 s) and m_SlotAppearDelay (0.8 s) of Time.time,
    /// then VisualParams.m_ActionSlotDisplayTimeout (3 s) of Time.deltaTime, each cut short by a skip
    /// click, and finally SkipTurnUI.Close(GetCurrentCOW().m_FTKPlayerID.IsLocal()). Close is the only
    /// place the owner's ContinueFSM continues. If that coroutine is stopped, nothing closes the popup
    /// and the turn waits.
    ///
    /// The episode is armed after InitializeSkipTurn and ends when Close runs. A ForceHide that stops
    /// the coroutine, a hidden m_Root, an inactive host or <see cref="TimeoutSeconds"/> of scaled time
    /// moves it to Pending. Poll returns the trigger once, only while the host is active, because
    /// Close starts a fade coroutine on it; the episode is then idle, so recovery happens at most
    /// once. A new InitializeSkipTurn or an action roll (Initialize) abandons it without recovery.
    /// </summary>
    internal sealed class SkipTurnEpisode
    {
        /// <summary>About twice vanilla's 4.8 s, in Time.time, which a pause freezes as it freezes
        /// the coroutine.</summary>
        internal const float TimeoutSeconds = 10f;

        private SkipTurnEpisodeState _state;
        private SkipTurnTrigger _trigger;
        private float _armedAt;
        private long _player;

        internal SkipTurnEpisodeState State { get { return _state; } }
        internal SkipTurnTrigger Trigger { get { return _trigger; } }
        /// <summary>The armed character's key, compared with the current turn's at recovery.</summary>
        internal long Player { get { return _player; } }

        internal void Arm(long player, float now)
        {
            _state = SkipTurnEpisodeState.Open;
            _trigger = SkipTurnTrigger.None;
            _player = player;
            _armedAt = now;
        }

        /// <summary>Drops an open or pending episode without recovery. True when there was one.</summary>
        internal bool Abandon()
        {
            bool had = _state != SkipTurnEpisodeState.Idle;
            _state = SkipTurnEpisodeState.Idle;
            _trigger = SkipTurnTrigger.None;
            return had;
        }

        /// <summary>SkipTurnUI.Close ran, from vanilla's coroutine or from a recovery.</summary>
        internal void Closed()
        {
            _state = SkipTurnEpisodeState.Idle;
            _trigger = SkipTurnTrigger.None;
        }

        /// <summary>uiMovementSlots.ForceHide is about to run. It stops coroutines only while m_Root
        /// is active.</summary>
        internal void ForceHide(bool rootActive)
        {
            if (_state == SkipTurnEpisodeState.Open && rootActive) Pend(SkipTurnTrigger.ForceHide);
        }

        /// <summary>One frame's check. Returns the trigger when recovery should run now, else None.</summary>
        /// <param name="rootActive">uiMovementSlots.m_Root.gameObject.activeSelf.</param>
        /// <param name="hostActive">uiMovementSlots.gameObject.activeInHierarchy.</param>
        /// <param name="now">Time.time.</param>
        internal SkipTurnTrigger Poll(bool rootActive, bool hostActive, float now)
        {
            if (_state == SkipTurnEpisodeState.Idle) return SkipTurnTrigger.None;
            if (_state == SkipTurnEpisodeState.Open)
            {
                if (!hostActive) Pend(SkipTurnTrigger.HostInactive);
                else if (!rootActive) Pend(SkipTurnTrigger.RootHidden);
                else if (now - _armedAt >= TimeoutSeconds) Pend(SkipTurnTrigger.Timeout);
                else return SkipTurnTrigger.None;
            }
            if (!hostActive) return SkipTurnTrigger.None;
            SkipTurnTrigger trigger = _trigger;
            _state = SkipTurnEpisodeState.Idle;
            _trigger = SkipTurnTrigger.None;
            return trigger;
        }

        private void Pend(SkipTurnTrigger trigger)
        {
            _state = SkipTurnEpisodeState.Pending;
            _trigger = trigger;
        }
    }

    /// <summary>SkipTurnUI keeps its ContinueFSM in m_ContinueFSM and never clears it, and a
    /// continuation made with WaitClients.None (FTKCallMethod's) fires again on every Continue. The
    /// guard lets each continuation through Close once: a second Close(true) with the same object is
    /// turned into Close(false), which still hides the popup. A normal skip turn closes once with a
    /// fresh continuation, so it is never blocked.</summary>
    internal sealed class ContinueOnceGuard
    {
        private object _last;

        internal bool Allow(object continuation)
        {
            if (continuation == null) return true;
            if (ReferenceEquals(continuation, _last)) return false;
            _last = continuation;
            return true;
        }
    }
}
