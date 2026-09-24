using System;
using System.Collections.Generic;
using FTKModFramework.Agent;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }
    private static Dictionary<string, object> Step(int frames)
    { return new Dictionary<string, object> { { "frames", frames } }; }
    private static NativeInputPlan Parse(params object[] steps)
    { return NativeInputPlan.Parse(new Dictionary<string, object> { { "requestId", "test" }, { "steps", steps } }, Key, 10, 20, 640, 480); }
    private static int Key(string name)
    {
        if (name == "A") return 97;
        if (name == "LeftShift") return 304;
        if (name == "Backspace") return 8;
        if (name == "Return") return 13;
        if (name == "KeypadEnter") return 271;
        if (name == "Tab") return 9;
        throw new ArgumentException("unknown key");
    }
    private static void Reject(Action action, string label)
    {
        try { action(); }
        catch (ArgumentException) { _checks++; return; }
        throw new Exception("Should reject " + label);
    }
    private static void Main()
    {
        var press = Step(3);
        press["keys"] = new object[] { "A", "LeftShift" };
        press["buttons"] = new object[] { 0 };
        press["x"] = 50d; press["y"] = 60d; press["scroll"] = 1d; press["text"] = "abc";
        var hold = Step(2);
        hold["keys"] = new object[] { "A" }; hold["buttons"] = new object[] { 0 };
        hold["x"] = 100d; hold["y"] = 120d;
        var release = Step(1);
        var plan = Parse(press, hold, release);
        Check(plan.TotalFrames == 6 && plan.Steps.Count == 3, "Plan frame budget");
        Check(plan.Steps[2].X == 100 && plan.Steps[2].Y == 120, "Pointer persists through absent coordinates");
        var timeline = new NativeInputTimeline(plan);
        timeline.Advance(false);
        Check(timeline.KeyDown(97) && timeline.KeyDown(304) && timeline.ButtonDown(0), "First frame edges");
        Check(timeline.Text == "abc" && timeline.Scroll == 1, "First frame pulses");
        timeline.Advance(false);
        Check(timeline.Key(97) && !timeline.KeyDown(97) && timeline.Button(0) && !timeline.ButtonDown(0), "Held inputs do not retrigger");
        Check(timeline.Text == "" && timeline.Scroll == 0, "Text/wheel pulses consumed after first frame");
        timeline.Advance(false);
        timeline.Advance(false);
        Check(timeline.Key(97) && !timeline.KeyDown(97) && timeline.KeyUp(304), "Step boundary preserves held key and releases absent modifier");
        Check(timeline.Button(0) && !timeline.ButtonDown(0) && timeline.Current.X == 100, "Native drag preserves mouse hold during movement");
        timeline.Advance(false);
        timeline.Advance(false);
        Check(timeline.KeyUp(97) && timeline.ButtonUp(0), "Empty held sets release keys and mouse");
        timeline.Advance(false);
        Check(timeline.Neutral && !timeline.KeyUp(97) && !timeline.ButtonUp(0), "Final neutral frame does not duplicate prior releases");
        var cancelled = new NativeInputTimeline(Parse(press));
        cancelled.Advance(false);
        cancelled.Advance(true);
        Check(cancelled.Neutral && cancelled.KeyUp(97) && cancelled.ButtonUp(0), "Cancellation emits native releases");
        var automatic = new NativeInputTimeline(Parse(Step(1), press));
        for (int i = 0; i < 5; i++) automatic.Advance(false);
        Check(automatic.Neutral && automatic.KeyUp(97) && automatic.ButtonUp(0), "Completion emits native releases");

        var editing = Step(2);
        editing["keys"] = new object[] { "Backspace", "Return", "KeypadEnter", "Tab" };
        editing["text"] = "replacement";
        var controls = new NativeInputTimeline(Parse(editing));
        controls.Advance(false);
        Check(controls.InputString(8, 13, 271, 9) == "\b\n\n\treplacement", "Editing controls precede explicit inputString text");
        Check(controls.InputString(8, 13, 271, 9) == "\b\n\n\treplacement", "Repeated inputString read retains control pulse");
        Check(controls.Text == "replacement", "InputField text excludes controls handled by KeyPressed");
        controls.Advance(false);
        Check(controls.InputString(8, 13, 271, 9) == "", "Held editing keys do not repeat inputString controls");
        controls.Advance(true);
        Check(controls.InputString(8, 13, 271, 9) == "", "Release does not produce editing control text");
        Reject(delegate { Parse(); }, "empty steps");
        Reject(delegate { Parse(Step(0)); }, "zero frames");
        Reject(delegate { Parse(Step(121)); }, "oversized step");
        Reject(delegate { Parse(Step(120), Step(120), Step(120), Step(120), Step(120), Step(1)); }, "total frame budget");
        Check(Parse(Step(120), Step(120), Step(120), Step(120), Step(120)).TotalFrames == 600, "Maximum frame budget accepted");
        foreach (object invalid in new object[] { "2", true, 1.5d, 1.00000001d, double.NaN, double.PositiveInfinity })
        {
            var step = Step(1); step["frames"] = invalid;
            Reject(delegate { Parse(step); }, "invalid frame number");
        }
        foreach (string field in new[] { "x", "y" })
        {
            var step = Step(1); step[field] = 5d;
            Reject(delegate { Parse(step); }, "partial pointer coordinates");
        }
        var outside = Step(1); outside["x"] = 640d; outside["y"] = 0d;
        Reject(delegate { Parse(outside); }, "outside screen");
        var invalidKey = Step(1); invalidKey["keys"] = new object[] { "Bogus" };
        Reject(delegate { Parse(invalidKey); }, "unknown key");
        invalidKey["keys"] = new object[] { "A", "A" };
        Reject(delegate { Parse(invalidKey); }, "duplicate key");
        var invalidButton = Step(1); invalidButton["buttons"] = new object[] { 3 };
        Reject(delegate { Parse(invalidButton); }, "unsupported mouse button");
        invalidButton["buttons"] = new object[] { 0, 0 };
        Reject(delegate { Parse(invalidButton); }, "duplicate mouse button");
        var invalidScroll = Step(1); invalidScroll["scroll"] = 11d;
        Reject(delegate { Parse(invalidScroll); }, "wheel budget");
        var invalidText = Step(1); invalidText["text"] = new string('a', 257);
        Reject(delegate { Parse(invalidText); }, "text budget");
        invalidText["text"] = "bad\0text";
        Reject(delegate { Parse(invalidText); }, "control text");
        var unknown = Step(1); unknown["hold"] = 5;
        Reject(delegate { Parse(unknown); }, "unknown field");
        Reject(delegate { Parse(press, unknown); }, "invalid later step atomically rejected");
        Check(press.ContainsKey("keys") && ((object[])press["keys"]).Length == 2, "Validation leaves caller payload untouched");
        Console.WriteLine("NativeInput: " + _checks + " checks passed");
    }
}
