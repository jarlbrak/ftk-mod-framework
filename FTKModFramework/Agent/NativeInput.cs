using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FTKModFramework.Agent
{
    // Opt-in input source below Rewired mapping and the native Unity UI pointer lifecycle.
    internal static class NativeInput
    {
        private static readonly string SessionId = Guid.NewGuid().ToString("N");
        private const string HarmonyId = "FTKModFramework.Agent.NativeInput";
        private static Harmony _harmony;
        private static bool _available, _advancing;
        private static string _error, _state = "idle", _requestId;
        private static NativeInputTimeline _timeline;
        private static int _startFrame, _lastFrame = -1, _textFrame = -1, _resetPointerFrame = -1, _totalFrames, _completedFrames;
        private static int _fieldEventFrame = -1, _countedTextFrame = -1;
        private static float _startedAt;
        private static bool _cancel;
        private static long _reads, _syntheticReads, _textCharacters;
        private static int _patchedMethods, _rewiredMethods, _uiMethods, _gameMethods, _frameworkMethods;
        private static readonly Dictionary<MethodInfo, MethodInfo> Replacements = new Dictionary<MethodInfo, MethodInfo>();
        private static readonly Dictionary<string, Dictionary<string, object>> History = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        private static readonly Queue<string> HistoryOrder = new Queue<string>();
        private static MethodInfo _keyPressed;

        internal static void Initialize()
        {
            if (_available || _harmony != null || Environment.GetEnvironmentVariable(AgentBridge.EnvFlag) != "1") return;
            try
            {
                AddRead("GetKey", typeof(KeyCode)); AddRead("GetKeyDown", typeof(KeyCode)); AddRead("GetKeyUp", typeof(KeyCode));
                AddRead("GetKey", typeof(string)); AddRead("GetKeyDown", typeof(string)); AddRead("GetKeyUp", typeof(string));
                AddRead("GetMouseButton", typeof(int)); AddRead("GetMouseButtonDown", typeof(int)); AddRead("GetMouseButtonUp", typeof(int));
                AddRead("GetButton", typeof(string)); AddRead("GetButtonDown", typeof(string)); AddRead("GetButtonUp", typeof(string));
                AddRead("GetAxis", typeof(string)); AddRead("GetAxisRaw", typeof(string));
                AddProperty("mousePosition", "MousePosition"); AddProperty("mouseScrollDelta", "MouseScrollDelta");
                AddProperty("mousePresent", "MousePresent"); AddProperty("anyKey", "AnyKey");
                AddProperty("anyKeyDown", "AnyKeyDown"); AddProperty("inputString", "InputString");
                _harmony = new Harmony(HarmonyId);
                var transpiler = new HarmonyMethod(typeof(NativeInput), "ReplaceInputReads");
                foreach (string name in new[] { "Rewired.ThreadSafeUnityInput+Keyboard", "Rewired.ThreadSafeUnityInput+Mouse" })
                {
                    Type type = AccessTools.TypeByName(name);
                    MethodInfo update = type == null ? null : AccessTools.DeclaredMethod(type, "Update");
                    if (update == null || !HasInputReads(update)) throw new InvalidOperationException("required Rewired input source missing: " + name);
                    _harmony.Patch(update, transpiler: transpiler);
                    _rewiredMethods++; _patchedMethods++;
                }
                PatchAssembly(typeof(GameLogic).Assembly, transpiler, false);
                PatchAssembly(typeof(BaseInput).Assembly, transpiler, true);
                PatchAssembly(typeof(NativeInput).Assembly, transpiler, false, true);
                if (_gameMethods == 0 || _uiMethods == 0) throw new InvalidOperationException("native UI/game input callsites unavailable");
                Type manager = AccessTools.TypeByName("Rewired.InputManager_Base");
                MethodInfo early = manager == null ? null : AccessTools.DeclaredMethod(manager, "DoEarlyUpdate");
                if (early == null) throw new InvalidOperationException("Rewired early update unavailable");
                _harmony.Patch(early, prefix: new HarmonyMethod(typeof(NativeInput), "BeforeEarlyUpdate"));
                _patchedMethods++;
                _keyPressed = AccessTools.Method(typeof(InputField), "KeyPressed", new[] { typeof(Event) });
                MethodInfo updateSelected = AccessTools.Method(typeof(InputField), "OnUpdateSelected", new[] { typeof(BaseEventData) });
                if (_keyPressed == null || updateSelected == null) throw new InvalidOperationException("native InputField event path unavailable");
                _harmony.Patch(updateSelected, prefix: new HarmonyMethod(typeof(NativeInput), "BeforeInputFieldUpdate"));
                _patchedMethods++;
                _available = true;
                _error = null;
                Plugin.Log.LogInfo("[agent] native input ready: " + _rewiredMethods + " Rewired, " + _gameMethods + " game, " + _uiMethods + " UI, " + _frameworkMethods + " framework hooks");
            }
            catch (Exception e)
            {
                _error = "native input initialization failed: " + e.Message;
                if (_harmony != null) _harmony.UnpatchSelf();
                _harmony = null;
                _available = false;
                Plugin.Log.LogError("[agent] " + _error);
            }
        }

        internal static void Shutdown()
        {
            if (_timeline != null) Finish("failed", "bridge stopped");
            _available = false;
            if (_harmony != null) _harmony.UnpatchSelf();
            _harmony = null;
        }

        internal static Dictionary<string, object> Submit(IDictionary<string, object> args)
        {
            try
            {
                if (!_available) throw new InvalidOperationException(_error ?? "native input is unavailable");
                Vector3 mouse = Input.mousePosition;
                NativeInputPlan plan = NativeInputPlan.Parse(args, ParseKey, mouse.x, mouse.y, Screen.width, Screen.height);
                if (_timeline != null && plan.RequestId == _requestId) return Envelope(Snapshot(), null, true);
                Dictionary<string, object> previous;
                if (History.TryGetValue(plan.RequestId, out previous)) return Envelope(new Dictionary<string, object>(previous), null, true);
                if (_timeline != null) throw new InvalidOperationException("another native input request is active");
                string guard = Guard();
                if (guard != null) throw new InvalidOperationException(guard);
                _timeline = new NativeInputTimeline(plan);
                _timeline.Current.X = mouse.x; _timeline.Current.Y = mouse.y;
                _requestId = plan.RequestId; _totalFrames = plan.TotalFrames; _completedFrames = 0;
                _state = "queued"; _error = null; _cancel = false;
                _startFrame = Time.frameCount + 1;
                _lastFrame = Time.frameCount;
                _startedAt = Time.realtimeSinceStartup;
                return Envelope(Snapshot(), null, false);
            }
            catch (Exception e) { return Envelope(Snapshot(), e.Message, false); }
        }

        internal static Dictionary<string, object> Status()
        {
            Tick();
            return Envelope(Snapshot(), null, false);
        }

        internal static Dictionary<string, object> Cancel()
        {
            if (_timeline != null)
            {
                ResetPointer();
                _cancel = true; _state = "cancelling";
            }
            return Envelope(Snapshot(), null, false);
        }

        internal static void Tick()
        {
            if (_resetPointerFrame >= 0 && Time.frameCount >= _resetPointerFrame && !_advancing)
            {
                _resetPointerFrame = -1;
                ResetPointer();
            }
            if (!_available || _timeline == null || _advancing) return;
            _advancing = true;
            try
            {
                int frame = Time.frameCount;
                if (frame < _startFrame || frame == _lastFrame) return;
                int previousFrame = _lastFrame;
                _lastFrame = frame;
                if (!_cancel && _timeline.Text.Length != 0 && _textFrame != previousFrame)
                {
                    Finish("failed", "text pulse had no focused native InputField or active inputString consumer");
                    return;
                }
                string guard = Guard();
                if (guard != null) { Finish("failed", guard); return; }
                if (Time.realtimeSinceStartup - _startedAt > 15f) { Finish("failed", "native input exceeded 15 seconds"); return; }
                // Cancel can arrive after Rewired sampled a press but before EventSystem processed it.
                // Clear again at the release boundary so that cached press cannot become a click/drop.
                if (_cancel) ResetPointer();
                if (_timeline.Neutral) { Finish(_cancel ? "cancelled" : "completed", null); return; }
                _timeline.Advance(_cancel);
                _completedFrames = _timeline.Frame;
                _state = _timeline.Neutral ? (_cancel ? "cancelling" : "releasing") : "running";
            }
            catch (Exception e) { Finish("failed", "native input frame failed: " + e.Message); }
            finally { _advancing = false; }
        }

        private static void Finish(string state, string error)
        {
            if (state == "failed" && _timeline != null && _timeline.Frame > 0)
            {
                ResetPointer();
                _resetPointerFrame = Time.frameCount + 1;
            }
            _timeline = null; _state = state; _error = error;
            if (_requestId == null) return;
            if (!History.ContainsKey(_requestId)) HistoryOrder.Enqueue(_requestId);
            History[_requestId] = Snapshot();
            while (HistoryOrder.Count > 64) History.Remove(HistoryOrder.Dequeue());
        }

        private static string Guard()
        {
            if (Environment.GetEnvironmentVariable(AgentBridge.EnvFlag) != "1") return "agent bridge is disabled";
            if (!Application.isFocused) return "native input requires game window focus";
            // Photon reports connected=true in offline mode. Only explicit offline or disconnected title
            // states are allowed; connection/join transitions fail closed before the next input sample.
            if (PhotonNetwork.offlineMode)
            {
                GameLogic game = GameLogic.Instance;
                if (game == null || !game.IsSinglePlayer()) return "native input requires single-player game mode";
                return null;
            }
            string state = PhotonNetwork.connectionStateDetailed.ToString();
            if (state == "Disconnected" || state == "PeerCreated" || state == "Uninitialized") return null;
            return "native input requires offline single-player or disconnected title screen (Photon: " + state + ")";
        }

        private static void ResetPointer()
        {
            // This native reset exits hover and discards cached presses/drags without click or drop.
            // A release after an abort must not activate a stale target, especially on an online transition.
            try
            {
                EventSystem events = EventSystem.current;
                var module = events == null ? null : events.currentInputModule as Rewired.Integration.UnityUI.RewiredStandaloneInputModule;
                if (module != null) module.MyClearSelection();
            }
            catch (Exception e) { Plugin.Log.LogError("[agent] native pointer reset failed: " + e.Message); }
        }

        private static Dictionary<string, object> Snapshot()
        {
            var hooks = new Dictionary<string, object> { { "methods", _patchedMethods }, { "rewired", _rewiredMethods }, { "game", _gameMethods }, { "ui", _uiMethods }, { "framework", _frameworkMethods }, { "reads", _reads }, { "syntheticReads", _syntheticReads }, { "textCharacters", _textCharacters } };
            return new Dictionary<string, object> { { "available", _available }, { "sessionId", SessionId }, { "retainedRequestLimit", 64 }, { "focused", Application.isFocused }, { "requestId", _requestId }, { "state", _state }, { "frame", _completedFrames }, { "totalFrames", _totalFrames }, { "progress", _totalFrames == 0 ? 0d : (double)_completedFrames / _totalFrames }, { "error", _error }, { "hooks", hooks }, { "screenWidth", Screen.width }, { "screenHeight", Screen.height } };
        }

        private static Dictionary<string, object> Envelope(Dictionary<string, object> result, string error, bool duplicate)
        {
            if (error == null && (string)result["state"] == "failed") error = result["error"] as string ?? "native input failed";
            var envelope = new Dictionary<string, object> { { "ok", error == null }, { "result", result } };
            if (error != null) envelope["error"] = error;
            if (duplicate) envelope["duplicate"] = true;
            return envelope;
        }

        private static int ParseKey(string name)
        {
            KeyCode key;
            try { key = (KeyCode)Enum.Parse(typeof(KeyCode), name, false); }
            catch { throw new ArgumentException("unknown Unity KeyCode: " + name); }
            if (!Enum.IsDefined(typeof(KeyCode), key) || key == KeyCode.None || (int)key >= (int)KeyCode.Mouse0 || char.IsDigit(name[0]))
                throw new ArgumentException("unsupported keyboard KeyCode: " + name);
            return (int)key;
        }

        private static void AddRead(string name, params Type[] parameters)
        {
            MethodInfo source = typeof(Input).GetMethod(name, parameters);
            MethodInfo target = typeof(NativeInput).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic, null, parameters, null);
            if (source == null || target == null) throw new MissingMethodException("input wrapper " + name);
            Replacements[source] = target;
        }
        private static void AddProperty(string name, string wrapper)
        {
            PropertyInfo property = typeof(Input).GetProperty(name);
            if (property == null) throw new MissingMemberException("Input." + name);
            Replacements[property.GetGetMethod()] = AccessTools.Method(typeof(NativeInput), wrapper);
        }
        private static bool HasInputReads(MethodInfo method)
        {
            foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(method))
            {
                MethodInfo called = instruction.operand as MethodInfo;
                if (called != null && Replacements.ContainsKey(called)) return true;
            }
            return false;
        }
        private static void PatchAssembly(Assembly assembly, HarmonyMethod transpiler, bool ui, bool framework = false)
        {
            foreach (Type type in assembly.GetTypes())
            {
                // Agent wrappers must keep their original hardware reads, including all nested helpers.
                if (framework && type.Namespace != null && (type.Namespace == typeof(NativeInput).Namespace ||
                    type.Namespace.StartsWith(typeof(NativeInput).Namespace + ".", StringComparison.Ordinal))) continue;
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (method.ContainsGenericParameters || method.IsAbstract || method.GetMethodBody() == null || !HasInputReads(method)) continue;
                    _harmony.Patch(method, transpiler: transpiler);
                    _patchedMethods++;
                    if (framework) _frameworkMethods++; else if (ui) _uiMethods++; else _gameMethods++;
                }
            }
        }
        private static IEnumerable<CodeInstruction> ReplaceInputReads(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo target;
                MethodInfo called = instruction.operand as MethodInfo;
                if (called != null && Replacements.TryGetValue(called, out target)) instruction.operand = target;
                yield return instruction;
            }
        }
        private static void BeforeEarlyUpdate() { Tick(); }
        private static bool Synthetic()
        {
            _reads++;
            Tick();
            bool active = _available && _timeline != null && Time.frameCount >= _startFrame;
            if (active) _syntheticReads++;
            return active;
        }

        private static bool HeldKey(int key) { return key >= (int)KeyCode.Mouse0 && key <= (int)KeyCode.Mouse6 ? _timeline.Button(key - (int)KeyCode.Mouse0) : _timeline.Key(key); }
        private static bool DownKey(int key) { return key >= (int)KeyCode.Mouse0 && key <= (int)KeyCode.Mouse6 ? _timeline.ButtonDown(key - (int)KeyCode.Mouse0) : _timeline.KeyDown(key); }
        private static bool UpKey(int key) { return key >= (int)KeyCode.Mouse0 && key <= (int)KeyCode.Mouse6 ? _timeline.ButtonUp(key - (int)KeyCode.Mouse0) : _timeline.KeyUp(key); }
        private static bool GetKey(KeyCode key) { return Synthetic() ? HeldKey((int)key) : Input.GetKey(key); }
        private static bool GetKeyDown(KeyCode key) { return Synthetic() ? DownKey((int)key) : Input.GetKeyDown(key); }
        private static bool GetKeyUp(KeyCode key) { return Synthetic() ? UpKey((int)key) : Input.GetKeyUp(key); }
        private static int StringKey(string key)
        {
            string normalized = key.Replace(" ", "");
            if (normalized.Length == 1 && normalized[0] >= '0' && normalized[0] <= '9') normalized = "Alpha" + normalized;
            try { return (int)(KeyCode)Enum.Parse(typeof(KeyCode), normalized, true); }
            catch { return -1; }
        }
        private static bool GetKey(string key) { return Synthetic() ? HeldKey(StringKey(key)) : Input.GetKey(key); }
        private static bool GetKeyDown(string key) { return Synthetic() ? DownKey(StringKey(key)) : Input.GetKeyDown(key); }
        private static bool GetKeyUp(string key) { return Synthetic() ? UpKey(StringKey(key)) : Input.GetKeyUp(key); }
        private static bool GetMouseButton(int button) { return Synthetic() ? _timeline.Button(button) : Input.GetMouseButton(button); }
        private static bool GetMouseButtonDown(int button) { return Synthetic() ? _timeline.ButtonDown(button) : Input.GetMouseButtonDown(button); }
        private static bool GetMouseButtonUp(int button) { return Synthetic() ? _timeline.ButtonUp(button) : Input.GetMouseButtonUp(button); }
        private static int NamedButton(string button)
        {
            if (button != null && button.StartsWith("MouseButton", StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(button.Substring(11), out index)) return index;
            }
            return -1;
        }
        private static bool GetButton(string button) { return Synthetic() ? _timeline.Button(NamedButton(button)) : Input.GetButton(button); }
        private static bool GetButtonDown(string button) { return Synthetic() ? _timeline.ButtonDown(NamedButton(button)) : Input.GetButtonDown(button); }
        private static bool GetButtonUp(string button) { return Synthetic() ? _timeline.ButtonUp(NamedButton(button)) : Input.GetButtonUp(button); }
        private static float Axis(string axis)
        {
            if (axis == "MouseAxis1" || axis == "Mouse X") return _timeline.Current.X - _timeline.Previous.X;
            if (axis == "MouseAxis2" || axis == "Mouse Y") return _timeline.Current.Y - _timeline.Previous.Y;
            if (axis == "MouseAxis3" || axis == "Mouse ScrollWheel") return _timeline.Scroll;
            return 0;
        }
        private static float GetAxis(string axis) { return Synthetic() ? Axis(axis) : Input.GetAxis(axis); }
        private static float GetAxisRaw(string axis) { return Synthetic() ? Axis(axis) : Input.GetAxisRaw(axis); }
        private static Vector3 MousePosition() { return Synthetic() ? new Vector3(_timeline.Current.X, _timeline.Current.Y, 0) : Input.mousePosition; }
        private static Vector2 MouseScrollDelta() { return Synthetic() ? new Vector2(0, _timeline.Scroll) : Input.mouseScrollDelta; }
        private static bool MousePresent() { return Synthetic() || Input.mousePresent; }
        private static bool AnyKey() { return Synthetic() ? _timeline.Current.Keys.Count > 0 || _timeline.Current.Buttons.Count > 0 : Input.anyKey; }
        private static bool AnyKeyDown()
        {
            if (!Synthetic()) return Input.anyKeyDown;
            foreach (int key in _timeline.Current.Keys) if (_timeline.KeyDown(key)) return true;
            foreach (int button in _timeline.Current.Buttons) if (_timeline.ButtonDown(button)) return true;
            return false;
        }
        private static string InputString()
        {
            if (!Synthetic()) return Input.inputString;
            string text = _timeline.Text;
            if (text.Length > 0) AcknowledgeText(text.Length);
            return _timeline.InputString((int)KeyCode.Backspace, (int)KeyCode.Return, (int)KeyCode.KeypadEnter, (int)KeyCode.Tab);
        }

        private static void AcknowledgeText(int characters)
        {
            _textFrame = Time.frameCount;
            // A native consumer can inspect inputString.Length and then read inputString again.
            // Count the pulse once while returning the same value to every read in that frame.
            if (_countedTextFrame == _textFrame) return;
            _countedTextFrame = _textFrame;
            _textCharacters += characters;
        }

        private static bool BeforeInputFieldUpdate(InputField __instance, BaseEventData __0)
        {
            if (!Synthetic()) return true;
            try { return DeliverInputFieldEvents(__instance, __0); }
            catch (Exception e)
            {
                Finish("failed", "native text event failed: " + (e.InnerException ?? e).Message);
                return false;
            }
        }
        private static bool DeliverInputFieldEvents(InputField __instance, BaseEventData __0)
        {
            if (!__instance.isFocused || _fieldEventFrame == Time.frameCount) return false;
            _fieldEventFrame = Time.frameCount;
            EventModifiers modifiers = EventModifiers.None;
            if (_timeline.Key((int)KeyCode.LeftShift) || _timeline.Key((int)KeyCode.RightShift)) modifiers |= EventModifiers.Shift;
            if (_timeline.Key((int)KeyCode.LeftControl) || _timeline.Key((int)KeyCode.RightControl)) modifiers |= EventModifiers.Control;
            if (_timeline.Key((int)KeyCode.LeftAlt) || _timeline.Key((int)KeyCode.RightAlt)) modifiers |= EventModifiers.Alt;
            if (_timeline.Key((int)KeyCode.LeftCommand) || _timeline.Key((int)KeyCode.RightCommand)) modifiers |= EventModifiers.Command;
            foreach (int key in _timeline.Current.Keys)
                if (_timeline.KeyDown(key) && !SendTextEvent(__instance, (KeyCode)key, '\0', modifiers)) break;
            int deliveredCharacters = 0;
            if (__instance.isFocused)
                foreach (char c in _timeline.Text)
                {
                    deliveredCharacters++;
                    if (!SendTextEvent(__instance, c == '\n' ? KeyCode.Return : KeyCode.None, c, modifiers)) break;
                }
            if (deliveredCharacters > 0) AcknowledgeText(deliveredCharacters);
            __instance.ForceLabelUpdate();
            __0.Use();
            return false;
        }
        private static bool SendTextEvent(InputField field, KeyCode key, char character, EventModifiers modifiers)
        {
            var input = new Event { type = EventType.KeyDown, keyCode = key, character = character, modifiers = modifiers };
            object result = _keyPressed.Invoke(field, new object[] { input });
            if (result != null && result.ToString() == "Finish") { field.DeactivateInputField(); return false; }
            return true;
        }
    }
}
