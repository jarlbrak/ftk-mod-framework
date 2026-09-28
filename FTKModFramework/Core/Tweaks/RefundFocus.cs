using System.Collections.Generic;

namespace FTKModFramework.Core
{
    /// <summary>What the ConvertFocusToAction prefix saw before vanilla ran. Default is "nothing
    /// captured", so a prefix that returned early or faulted never counts.</summary>
    internal struct RefundFocusConversion
    {
        internal bool Captured { get; set; }
        /// <summary>FTKNetworkObject.IsOwner of Movement.m_CharacterOverworld.</summary>
        internal bool Owner { get; set; }
        internal int FocusBefore { get; set; }
        internal int ActionBefore { get; set; }
    }

    /// <summary>Why a refund is refused. None means every gate passed.</summary>
    internal enum RefundFocusBlock
    {
        None,
        Off,
        NothingToRefund,
        FocusFull,
        NoActionPoints,
        NotOwner,
        NotMyTurn,
        NotMovementCharacter,
        TrackerInactive,
        NotTracking,
        PathCommitted,
        SneakPick,
        InCombat,
    }

    /// <summary>The game state one refund decision reads. Every flag is phrased so its default,
    /// false, blocks the refund.</summary>
    internal struct RefundFocusState
    {
        /// <summary>The ledger's count for this character.</summary>
        internal int Count { get; set; }
        /// <summary>CharacterStats.m_FocusPoints.</summary>
        internal int FocusPoints { get; set; }
        /// <summary>CharacterStats.MaxFocus, the clamp UpdateFocusPoints applies.</summary>
        internal int MaxFocus { get; set; }
        /// <summary>CharacterStats.m_ActionPoints.</summary>
        internal int ActionPoints { get; set; }
        /// <summary>FTKNetworkObject.IsOwner.</summary>
        internal bool Owner { get; set; }
        /// <summary>CharacterStats.m_IsMyTurn.</summary>
        internal bool MyTurn { get; set; }
        /// <summary>Movement.Instance.m_CharacterOverworld is this character.</summary>
        internal bool MovementCharacter { get; set; }
        /// <summary>Movement.m_Mode is TrackingMode.Movement. TrackingPathFinished sets None before
        /// it hands a committed path to the walk FSM.</summary>
        internal bool TrackerActive { get; set; }
        /// <summary>Movement.m_MovementFSM.ActiveStateName is "Tracking".</summary>
        internal bool FsmTracking { get; set; }
        /// <summary>Movement.m_HexList.Count: the start hex plus every committed step.</summary>
        internal int CommittedHexes { get; set; }
        /// <summary>Movement.m_LockedInput (the sneak destination pick) or the FSM state
        /// "PickSneakHex" that the private IsInSneakPickMode tests.</summary>
        internal bool SneakPick { get; set; }
        /// <summary>CharacterStats.m_IsInCombat or EncounterSession.m_IsInCombat.</summary>
        internal bool InCombat { get; set; }
    }

    /// <summary>The game calls a refund makes, in order. The patch implements it over the vanilla
    /// setters; tests record the calls.</summary>
    internal interface IRefundFocusSetters
    {
        /// <summary>CharacterStats.UpdateFocusPoints(delta).</summary>
        void UpdateFocusPoints(int delta);
        /// <summary>CharacterOverworld.UpdatePlayerAction(delta).</summary>
        void UpdatePlayerAction(int delta);
        /// <summary>Movement.TrackResetList().</summary>
        void TrackResetList();
        /// <summary>uiPlayerMainHud.UpdateHud() of the character.</summary>
        void RefreshHud();
    }

    /// <summary>What reset a count.</summary>
    internal enum RefundFocusClear
    {
        /// <summary>CharacterOverworld.EndTurn, for that character.</summary>
        EndTurn,
        /// <summary>EncounterSession.StartEncounterSession_Actual, for every character.</summary>
        EncounterStart,
        /// <summary>CharacterOverworld.SetInCombat(true), for that character.</summary>
        EnterCombat,
        /// <summary>CharacterOverworld.SetDeath, for that character.</summary>
        Death,
    }

    /// <summary>Per-character conversion counts for convenience.refund-movement-focus, held on the
    /// owning client only and never saved. Keyed by FTKPlayerID (PhotonID and TurnIndex): in local
    /// multiplayer every character shares the loader's PhotonID. A change of the registry's Session
    /// generation (capture, SetSessionSet or clear, which covers leaving the room and returning to the
    /// title) empties it before any read or write. At most one character has a turn at a time, so the
    /// list stays tiny and a linear search does not allocate.</summary>
    internal sealed class RefundFocusLedger
    {
        private struct Entry
        {
            internal long Key;
            internal int Count;
        }

        private readonly TweakRegistry _registry;
        private readonly List<Entry> _entries = new List<Entry>(4);
        private int _generation;

        internal RefundFocusLedger(TweakRegistry registry)
        {
            _registry = registry;
            _generation = registry != null ? registry.SessionGeneration : 0;
        }

        internal static long Key(int photonId, int turnIndex)
        {
            return ((long)photonId << 32) | (uint)turnIndex;
        }

        internal int Count(long key)
        {
            Sync();
            int index = IndexOf(key);
            return index >= 0 ? _entries[index].Count : 0;
        }

        /// <summary>Characters with a nonzero count, for tests and diagnostics.</summary>
        internal int Characters
        {
            get
            {
                Sync();
                return _entries.Count;
            }
        }

        internal void Add(long key)
        {
            Sync();
            int index = IndexOf(key);
            if (index < 0)
            {
                _entries.Add(new Entry { Key = key, Count = 1 });
                return;
            }
            Entry entry = _entries[index];
            entry.Count++;
            _entries[index] = entry;
        }

        /// <summary>Removes one conversion. Returns false when there was none.</summary>
        internal bool Take(long key)
        {
            Sync();
            int index = IndexOf(key);
            if (index < 0) return false;
            Entry entry = _entries[index];
            entry.Count--;
            if (entry.Count <= 0) _entries.RemoveAt(index);
            else _entries[index] = entry;
            return true;
        }

        /// <summary>Applies one clear trigger. The encounter start clears every character; the
        /// others clear the named one. SetInCombat(false) clears nothing.</summary>
        internal void Clear(RefundFocusClear trigger, long key, bool inCombat = true)
        {
            Sync();
            if (trigger == RefundFocusClear.EncounterStart)
            {
                _entries.Clear();
                return;
            }
            if (trigger == RefundFocusClear.EnterCombat && !inCombat) return;
            int index = IndexOf(key);
            if (index >= 0) _entries.RemoveAt(index);
        }

        private void Sync()
        {
            int generation = _registry != null ? _registry.SessionGeneration : 0;
            if (generation == _generation) return;
            _entries.Clear();
            _generation = generation;
        }

        private int IndexOf(long key)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Key == key) return i;
            return -1;
        }
    }

    /// <summary>The refund rules (Spec #264 FR-1 to FR-3), free of game types.</summary>
    internal static class RefundFocus
    {
        /// <summary>ConvertFocusToAction's own guard, which the prefix captures.</summary>
        internal const int ActionPointCap = 9;

        /// <summary>The prefix's capture: only a conversion vanilla's guard would allow, by the owner.</summary>
        internal static RefundFocusConversion Before(bool owner, int focus, int action)
        {
            var state = new RefundFocusConversion();
            if (!owner || focus <= 0 || action >= ActionPointCap) return state;
            state.Captured = true;
            state.Owner = true;
            state.FocusBefore = focus;
            state.ActionBefore = action;
            return state;
        }

        /// <summary>The postfix's test: the capture held and vanilla really moved one point across.</summary>
        internal static bool Converted(RefundFocusConversion before, int focusAfter, int actionAfter)
        {
            return before.Captured && before.Owner && focusAfter < before.FocusBefore && actionAfter > before.ActionBefore;
        }

        /// <summary>min(count, action points): a converted point already walked is not refundable.</summary>
        internal static int Refundable(int count, int actionPoints)
        {
            int value = count < actionPoints ? count : actionPoints;
            return value > 0 ? value : 0;
        }

        /// <summary>The first gate that fails, in a fixed order, or None.</summary>
        internal static RefundFocusBlock Check(RefundFocusState state)
        {
            if (Refundable(state.Count, state.ActionPoints) <= 0)
                return state.Count <= 0 ? RefundFocusBlock.NothingToRefund : RefundFocusBlock.NoActionPoints;
            if (state.FocusPoints >= state.MaxFocus) return RefundFocusBlock.FocusFull;
            if (state.ActionPoints < 1) return RefundFocusBlock.NoActionPoints;
            if (!state.Owner) return RefundFocusBlock.NotOwner;
            if (!state.MyTurn) return RefundFocusBlock.NotMyTurn;
            if (!state.MovementCharacter) return RefundFocusBlock.NotMovementCharacter;
            if (!state.TrackerActive) return RefundFocusBlock.TrackerInactive;
            if (!state.FsmTracking) return RefundFocusBlock.NotTracking;
            if (state.CommittedHexes > 1) return RefundFocusBlock.PathCommitted;
            if (state.SneakPick) return RefundFocusBlock.SneakPick;
            if (state.InCombat) return RefundFocusBlock.InCombat;
            return RefundFocusBlock.None;
        }

        /// <summary>How many empty pips to show as refundable: none unless a refund is allowed now,
        /// and never more than the empty pips MaxFocus leaves.</summary>
        internal static int Pips(RefundFocusState state)
        {
            if (Check(state) != RefundFocusBlock.None) return 0;
            int room = state.MaxFocus - state.FocusPoints;
            int refundable = Refundable(state.Count, state.ActionPoints);
            return refundable < room ? refundable : room;
        }

        /// <summary>Whether pip index shows as refundable: the first shown empty pips after the filled
        /// ones, which are exactly the pips a refund would fill.</summary>
        internal static bool PipMarked(int index, int focusPoints, int shown)
        {
            if (shown <= 0 || focusPoints < 0) return false;
            return index >= focusPoints && index < focusPoints + shown;
        }

        /// <summary>The exact reversal of one conversion: the two vanilla setters, the count, then the
        /// path reset and the HUD. Nothing else: no analytics, no SpentFocus, no ability event and no
        /// sync beyond the setters' own.</summary>
        internal static void Apply(RefundFocusLedger ledger, long key, IRefundFocusSetters setters)
        {
            setters.UpdateFocusPoints(1);
            setters.UpdatePlayerAction(-1);
            ledger.Take(key);
            setters.TrackResetList();
            setters.RefreshHud();
        }
    }

    /// <summary>What the configured refund key is good for.</summary>
    internal enum RefundKeyStatus
    {
        /// <summary>None: clicks only.</summary>
        Off,
        Usable,
        /// <summary>A mouse or controller button, or not a key at all.</summary>
        NotKeyboard,
        /// <summary>A game control also uses it, so a press would also act in vanilla.</summary>
        Conflict,
    }

    /// <summary>The refund key rules. Keys are UnityEngine.KeyCode values as ints, so the rules stay
    /// Unity-free: None is 0, and the mouse buttons and joystick buttons start at Mouse0 (323).</summary>
    internal static class RefundFocusInput
    {
        internal const int None = 0;
        internal const int FirstNonKeyboard = 323;

        /// <summary>KeyCode.F. No default game binding uses it: it is absent from every list below,
        /// which were read from the installed game (see each list), and the 1.6.0 live run pressed F
        /// with the tweak on and logged one refund and nothing else.</summary>
        internal const int DefaultKey = 102;

        /// <summary>KeyCode.Backspace, the 1.6.0 default. It is the game's own End Turn key.</summary>
        internal const int LegacyDefaultKey = 8;

        /// <summary>Keyboard keys the game reads whatever the remap table says. The Rewired Input
        /// Manager's keyboard maps (level0, InputManager _userData.keyboardMaps) bind Return (Ok),
        /// Escape (UICancel, Pause), the four arrows (UI and hex navigation), Space, M and RightAlt;
        /// Assembly-CSharp also reads Return (uiChatBox, Lobby, uiStartGame), Escape
        /// (uiCharacterCreateRoot), Space (SplashScreen, uiStartGame) and I (uiQuickPlayerCreate)
        /// straight from UnityEngine.Input.</summary>
        internal static readonly int[] FixedBindings =
        {
            13, 27, 32, 105, 109, 273, 274, 275, 276, 307,
        };

        /// <summary>Every keyboard key and modifier in FTKInput's serialized m_DefaultKeys (sharedassets1,
        /// FTKInput): D and A (PanHorizontal), W and S (PanVertical), LeftControl (PanDrag), Q and E
        /// (Zoom), Space (Center), Tab (NextLocation, and with LeftShift PrevLocation), LeftShift
        /// (KeyMoreInfo modifier, WeaponInfo), Delete (EndTurn), I (Inventory), C (Chat), T
        /// (ToggleStats), LeftAlt (KeyPing modifier), Comma (EnemyPrev) and Period (EnemyNext).
        /// RestoreDefaultInputMap then applies m_DefaultKeysMacOverride on every platform, which sets
        /// EndTurn to Backspace; a player's saved custominput.bin shows EndTurn [8].</summary>
        internal static readonly int[] RemappableDefaults =
        {
            8, 9, 32, 44, 46, 97, 99, 100, 101, 105, 113, 115, 116, 119, 127, 304, 306, 308,
        };

        /// <summary>Old config values that mean "the default". BepInEx writes a default into the
        /// config file, so a saved Backspace cannot be told apart from one a player typed; it is
        /// always the game's End Turn key unless End Turn was remapped, so it is read as the new
        /// default.</summary>
        internal static int Migrate(int configured)
        {
            return configured == LegacyDefaultKey ? DefaultKey : configured;
        }

        /// <summary>Whether any default game binding, fixed or remappable, uses key.</summary>
        internal static bool IsDefaultBinding(int key)
        {
            return Contains(FixedBindings, key) || Contains(RemappableDefaults, key);
        }

        /// <summary>Checks the key against the fixed bindings and the remap table, keys and modifiers.
        /// remapped is every key and modifier key of FTKInput.m_RemappableKeys; null, when the table
        /// is not loaded, means the defaults, so a conflict is never missed for want of the table.</summary>
        internal static RefundKeyStatus Status(int key, IList<int> remapped)
        {
            if (key == None) return RefundKeyStatus.Off;
            if (key < None || key >= FirstNonKeyboard) return RefundKeyStatus.NotKeyboard;
            if (Contains(FixedBindings, key)) return RefundKeyStatus.Conflict;
            if (remapped == null) return Contains(RemappableDefaults, key) ? RefundKeyStatus.Conflict : RefundKeyStatus.Usable;
            return Contains(remapped, key) ? RefundKeyStatus.Conflict : RefundKeyStatus.Usable;
        }

        /// <summary>The one warning for a key that cannot be used, or null.</summary>
        internal static string Warning(RefundKeyStatus status, string keyName)
        {
            if (status == RefundKeyStatus.Conflict)
                return "Tweaks: the refund movement focus key " + keyName + " is also one of the game's own controls, so the tweak ignores it. "
                    + "Change [TweakKeys] RefundMovementFocus in BepInEx/config/com.ftkmf.framework.cfg, for example to F.";
            if (status == RefundKeyStatus.NotKeyboard)
                return "Tweaks: the refund movement focus key " + keyName + " is not a keyboard key, so the tweak ignores it. "
                    + "Change [TweakKeys] RefundMovementFocus in BepInEx/config/com.ftkmf.framework.cfg, for example to F.";
            return null;
        }

        private static bool Contains(IList<int> keys, int key)
        {
            for (int i = 0; i < keys.Count; i++)
                if (keys[i] == key) return true;
            return false;
        }
    }

    /// <summary>The refund tooltip. English only and raw, as the other tweak texts are.</summary>
    internal static class RefundFocusText
    {
        internal const string Title = "Refund focus";

        internal static string Detail(string keyName)
        {
            string text = "Click to take back 1 focus you spent on movement this turn. You lose the extra move it gave.";
            return string.IsNullOrEmpty(keyName) ? text : text + " Key: " + keyName + ".";
        }
    }
}
