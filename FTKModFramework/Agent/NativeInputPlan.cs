using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace FTKModFramework.Agent
{
    internal sealed class NativeInputStep
    {
        internal int Frames;
        internal readonly HashSet<int> Keys = new HashSet<int>();
        internal readonly HashSet<int> Buttons = new HashSet<int>();
        internal float X, Y, Scroll;
        internal string Text = "";
    }

    // Pure validated plan. No game state is touched until the complete request passes validation.
    internal sealed class NativeInputPlan
    {
        internal string RequestId;
        internal readonly List<NativeInputStep> Steps = new List<NativeInputStep>();
        internal int TotalFrames;

        internal static NativeInputPlan Parse(IDictionary<string, object> args, Func<string, int> keyCode,
            float initialX, float initialY, int width, int height)
        {
            if (args == null) throw new ArgumentException("missing input args");
            RejectUnknown(args, "requestId", "steps");
            object raw;
            string id = args.TryGetValue("requestId", out raw) ? raw as string : null;
            if (string.IsNullOrEmpty(id) || id.Length > 128) throw new ArgumentException("requestId must contain 1..128 characters");
            if (!args.TryGetValue("steps", out raw)) throw new ArgumentException("missing steps");
            IList steps = raw as IList;
            if (steps == null || steps.Count == 0 || steps.Count > 600) throw new ArgumentException("steps must be a nonempty array with at most 600 entries");
            var plan = new NativeInputPlan { RequestId = id };
            float x = initialX, y = initialY;
            int totalText = 0;
            foreach (object entry in steps)
            {
                var map = entry as IDictionary<string, object>;
                if (map == null) throw new ArgumentException("each step must be an object");
                RejectUnknown(map, "frames", "keys", "buttons", "x", "y", "scroll", "text");
                if (!map.TryGetValue("frames", out raw)) throw new ArgumentException("each step requires frames");
                int frames = Integer(raw, "frames", 1, 120);
                plan.TotalFrames += frames;
                if (plan.TotalFrames > 600) throw new ArgumentException("input plan exceeds 600 frames");
                if (map.ContainsKey("x") != map.ContainsKey("y")) throw new ArgumentException("x and y must be supplied together");
                if (map.TryGetValue("x", out raw))
                {
                    x = Number(raw, "x"); y = Number(map["y"], "y");
                    if (x < 0 || y < 0 || x >= width || y >= height) throw new ArgumentException("pointer outside current screen bounds");
                }
                var step = new NativeInputStep { Frames = frames, X = x, Y = y };
                if (map.TryGetValue("keys", out raw))
                {
                    IList keys = raw as IList;
                    if (keys == null || keys.Count > 132) throw new ArgumentException("keys must be an array of at most 132 KeyCode names");
                    foreach (object key in keys)
                    {
                        string name = key as string;
                        if (string.IsNullOrEmpty(name)) throw new ArgumentException("keys must contain KeyCode names");
                        if (!step.Keys.Add(keyCode(name))) throw new ArgumentException("duplicate key: " + name);
                    }
                }
                if (map.TryGetValue("buttons", out raw))
                {
                    IList buttons = raw as IList;
                    if (buttons == null || buttons.Count > 3) throw new ArgumentException("buttons must be an array of mouse button indices 0..2");
                    foreach (object button in buttons)
                        if (!step.Buttons.Add(Integer(button, "button", 0, 2))) throw new ArgumentException("duplicate mouse button");
                }
                if (map.TryGetValue("scroll", out raw))
                {
                    step.Scroll = Number(raw, "scroll");
                    if (Math.Abs(step.Scroll) > 10) throw new ArgumentException("scroll must be between -10 and 10");
                }
                if (map.TryGetValue("text", out raw))
                {
                    step.Text = raw as string;
                    if (step.Text == null || step.Text.Length > 256) throw new ArgumentException("text must be a string of at most 256 characters per step");
                    totalText += step.Text.Length;
                    if (totalText > 2048) throw new ArgumentException("plan text exceeds 2048 characters");
                    foreach (char c in step.Text)
                        if (char.IsControl(c) && c != '\n' && c != '\t') throw new ArgumentException("text contains unsupported control characters");
                }
                plan.Steps.Add(step);
            }
            return plan;
        }

        private static void RejectUnknown(IDictionary<string, object> args, params string[] allowed)
        {
            foreach (string key in args.Keys)
                if (Array.IndexOf(allowed, key) < 0) throw new ArgumentException("unknown input field: " + key);
        }

        private static float Number(object raw, string name)
        {
            if (!(raw is double || raw is float || raw is int || raw is long || raw is decimal)) throw new ArgumentException(name + " must be numeric");
            float value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException(name + " must be finite");
            return value;
        }

        private static int Integer(object raw, string name, int min, int max)
        {
            if (!(raw is double || raw is float || raw is int || raw is long || raw is decimal)) throw new ArgumentException(name + " must be numeric");
            double value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            if (double.IsNaN(value) || double.IsInfinity(value) || value != Math.Floor(value) || value < min || value > max) throw new ArgumentException(name + " must be an integer in " + min + ".." + max);
            return (int)value;
        }
    }

    // A step describes the complete held set, so transitions naturally generate press/release edges.
    internal sealed class NativeInputTimeline
    {
        internal readonly NativeInputPlan Plan;
        internal NativeInputStep Current, Previous;
        internal int Frame, StepIndex = -1, StepFrame;
        internal bool Neutral;
        internal NativeInputTimeline(NativeInputPlan plan)
        {
            Plan = plan;
            Current = Previous = new NativeInputStep { X = plan.Steps[0].X, Y = plan.Steps[0].Y };
        }
        internal void Advance(bool release)
        {
            Previous = Current;
            if (release || Frame >= Plan.TotalFrames)
            {
                Current = new NativeInputStep { X = Previous.X, Y = Previous.Y };
                Neutral = true;
                return;
            }
            if (StepIndex < 0 || StepFrame >= Current.Frames)
            {
                Current = Plan.Steps[++StepIndex];
                StepFrame = 0;
            }
            StepFrame++;
            Frame++;
        }
        internal bool Key(int code) { return Current.Keys.Contains(code); }
        internal bool KeyDown(int code) { return Key(code) && !Previous.Keys.Contains(code); }
        internal bool KeyUp(int code) { return !Key(code) && Previous.Keys.Contains(code); }
        internal bool Button(int code) { return Current.Buttons.Contains(code); }
        internal bool ButtonDown(int code) { return Button(code) && !Previous.Buttons.Contains(code); }
        internal bool ButtonUp(int code) { return !Button(code) && Previous.Buttons.Contains(code); }
        internal float Scroll { get { return !Neutral && StepFrame == 1 ? Current.Scroll : 0; } }
        internal string Text { get { return !Neutral && StepFrame == 1 ? Current.Text : ""; } }
        internal string InputString(int backspace, int enter, int keypadEnter, int tab)
        {
            // Explicit text is layout-independent. Only editing control keys also produce the native
            // inputString control characters; InputField still handles these through KeyPressed.
            string controls = "";
            if (KeyDown(backspace)) controls += "\b";
            if (KeyDown(enter)) controls += "\n";
            if (KeyDown(keypadEnter)) controls += "\n";
            if (KeyDown(tab)) controls += "\t";
            return controls + Text;
        }
    }
}
