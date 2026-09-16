using System;
using Newtonsoft.Json.Linq;
class Program
{
    static int passed;
    static void Reject(Action run) { try { run(); } catch (InvalidOperationException) { passed++; return; } throw new Exception("Expected rejection"); }
    static void Main()
    {
        bool toggle = NativePartyClassPolicy.ExplicitDirectionalControl("toggleClass", "right");
        bool next = NativePartyClassPolicy.ExplicitDirectionalControl("classArrowNext", "right");
        bool previous = NativePartyClassPolicy.ExplicitDirectionalControl("classArrowPrevious", "left");
        if (toggle || !next || !previous) throw new Exception("Native directional control contract differs"); passed += 3;
        if (!NativePartyClassPolicy.EligibleArrow(next, true, 2, true, true, true, true))
            throw new Exception("Exact native explicit arrow rejected"); passed++;
        if (NativePartyClassPolicy.EligibleArrow(toggle, true, 2, true, true, true, true)
            || NativePartyClassPolicy.EligibleArrow(next, false, 2, true, true, true, true)
            || NativePartyClassPolicy.EligibleArrow(next, true, 2, false, true, true, true)
            || NativePartyClassPolicy.EligibleArrow(next, true, 0, true, true, true, true))
            throw new Exception("Name bypassed callback/owner/enabled authority"); passed += 4;
        if (NativePartyClassPolicy.UniqueEligibleArrow(new[] { -19394, -19432 }, new[] { toggle, next }) != 1)
            throw new Exception("Legitimate toggle coexistence rejected"); passed++;
        foreach (string name in new[] { "ClassArrowNext", "classArrowNext(Clone)", "classArrowNext ", "classArrowPrevious", "toggleClass", null })
        { if (NativePartyClassPolicy.ExplicitDirectionalControl(name, "right")) throw new Exception("Misleading native name accepted"); passed++; }
        if (NativePartyClassPolicy.UniqueEligibleArrow(new[] { 10, 20 }, new[] { next, next }) != -2)
            throw new Exception("Duplicate explicit controls accepted"); passed++;
        if (NativePartyClassPolicy.UniqueEligibleArrow(new[] { 10 }, new[] { toggle }) != -1)
            throw new Exception("Missing explicit control accepted"); passed++;
        Reject(() => NativePartyClassPolicy.ExplicitDirectionalControl("classArrowNext", "forward"));
        var originalName = JObject.Parse("{arrowName:'classArrowNext',arrowInstanceId:10}");
        var replacedName = JObject.Parse("{arrowName:'toggleClass',arrowInstanceId:10}");
        Reject(() => new NativePartyStartPolicy().Submit("token", "token", originalName, replacedName,
            () => { throw new Exception("Renamed control clicked"); }));
        if (!NativePartyClassPolicy.GraphicVisible(true, true, false, 0.001f))
            throw new Exception("Invented opacity threshold"); passed++;
        foreach (float alpha in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        { if (NativePartyClassPolicy.GraphicVisible(true, true, false, alpha)) throw new Exception("Nonvisible alpha accepted"); passed++; }
        if (NativePartyClassPolicy.GraphicVisible(false, true, false, 1f)
            || NativePartyClassPolicy.GraphicVisible(true, false, false, 1f)
            || NativePartyClassPolicy.GraphicVisible(true, true, true, 1f))
            throw new Exception("Disabled/culled graphic accepted"); passed += 3;
        if (NativePartyClassPolicy.UniqueEligibleArrow(new[] { 10, 20 }, new[] { false, true }) != 1)
            throw new Exception("Inactive matching arrow incorrectly ambiguous"); passed++;
        if (NativePartyClassPolicy.UniqueEligibleArrow(new[] { 10, 10 }, new[] { true, true }) != 0)
            throw new Exception("Repeated native reference not deduplicated"); passed++;
        if (NativePartyClassPolicy.UniqueEligibleArrow(new[] { 10, 20 }, new[] { true, true }) != -2)
            throw new Exception("Distinct eligible arrows not rejected"); passed++;
        if (NativePartyClassPolicy.UniqueEligibleArrow(new[] { 10, 20 }, new[] { false, false }) != -1)
            throw new Exception("Missing eligible arrow not rejected"); passed++;
        if (NativePartyClassPolicy.UniqueEligibleArrow(new[] { 0, -10 }, new[] { false, true }) != 1)
            throw new Exception("Native negative instance ID rejected"); passed++;
        Reject(() => NativePartyClassPolicy.UniqueEligibleArrow(new[] { 0 }, new[] { true }));
        Reject(() => NativePartyClassPolicy.UniqueEligibleArrow(new int[9], new bool[9]));
        Reject(() => NativePartyClassPolicy.UniqueEligibleArrow(new int[1], new bool[2]));
        var arrowPins = JObject.Parse("{arrowCandidates:[{instanceId:10,eligible:false},{instanceId:20,eligible:true}]}");
        var changedArrows = JObject.Parse("{arrowCandidates:[{instanceId:10,eligible:true},{instanceId:20,eligible:true}]}");
        Reject(() => new NativePartyStartPolicy().Submit("token", "token", arrowPins, changedArrows,
            () => { throw new Exception("Changed arrow eligibility invoked callback"); }));
        foreach (string identity in new[] { "targetGraphicInstanceId", "canvasInstanceId" })
        {
            var original = JObject.Parse("{targetGraphicInstanceId:10,canvasInstanceId:20,visible:true}");
            var replaced = JObject.Parse(original.ToString()); replaced[identity] = 30;
            Reject(() => new NativePartyStartPolicy().Submit("token", "token", original, replaced,
                () => { throw new Exception("Same-valued replacement accepted"); }));
        }
        if (!NativePartyClassPolicy.GraphicVisible(true, true, false, float.Epsilon))
            throw new Exception("Positive alpha underflowed"); passed++;
        NativePartyClassPolicy.RequireSelectionOpen(false, false, 10, 9); passed++;
        Reject(() => NativePartyClassPolicy.RequireSelectionOpen(true, false, 10, 9));
        Reject(() => NativePartyClassPolicy.RequireSelectionOpen(false, true, 10, 9));
        Reject(() => NativePartyClassPolicy.RequireSelectionOpen(false, false, 10, 10));
        bool[] visible = { true, false, true, false, true };
        if (NativePartyClassPolicy.Next(visible, 0, "right") != 2
            || NativePartyClassPolicy.Next(visible, 0, "left") != 4
            || NativePartyClassPolicy.Next(visible, 4, "right") != 0
            || NativePartyClassPolicy.Next(visible, 2, "left") != 0)
            throw new Exception("Native class visibility/wrap route differs"); passed += 4;
        Reject(() => NativePartyClassPolicy.Next(new bool[4], 0, "right"));
        Reject(() => NativePartyClassPolicy.Next(new bool[513], 0, "right"));
        Reject(() => NativePartyClassPolicy.Next(visible, -1, "right"));
        Reject(() => NativePartyClassPolicy.Next(visible, 5, "right"));
        Reject(() => NativePartyClassPolicy.Next(visible, 0, "arbitrary"));
        NativePartyStartPolicy.RequireNativePreview(1, 1, 0, 1); passed++;
        NativePartyStartPolicy.RequireNativePreview(1, 1, 2, 3); passed++;
        Reject(() => NativePartyStartPolicy.RequireNativePreview(0, 0, 0, 1));
        Reject(() => NativePartyStartPolicy.RequireNativePreview(1, 2, 0, 1));
        Reject(() => NativePartyStartPolicy.RequireNativePreview(1, 1, -1, 3));
        Reject(() => NativePartyStartPolicy.RequireNativePreview(1, 1, 3, 3));
        Reject(() => NativePartyStartPolicy.RequireNativePreview(1, 1, 0, 0));
        Reject(() => NativePartyStartPolicy.RequireNativePreview(1, 1, 0, 4));
        JObject pins = JObject.Parse("{session:'a',menu:1,root:2,button:3,gameDef:4,targetClassId:114,direction:'right',route:[{visible:true}],previews:[{owner:5,avatar:6,classId:7,skin:8,photon:9}]}");
        int invoked = 0;
        var policy = new NativePartyStartPolicy();
        Reject(() => policy.Submit("token", "stale", pins, pins, () => invoked++));
        foreach (string field in new [] {"session", "menu", "root", "button", "gameDef", "previews", "targetClassId", "direction", "route"})
        {
            JObject changed = JObject.Parse(pins.ToString()); changed[field] = "changed";
            Reject(() => policy.Submit("token", "token", pins, changed, () => invoked++));
        }
        foreach (string field in new [] {"owner", "avatar", "classId", "skin", "photon"})
        {
            JObject changed = JObject.Parse(pins.ToString()); changed["previews"][0][field] = 100;
            Reject(() => policy.Submit("token", "token", pins, changed, () => invoked++));
        }
        if (invoked != 0 || policy.Consumed) throw new Exception("Rejected preflight consumed or invoked");
        policy.Submit("token", "token", pins, JObject.Parse(pins.ToString()), () => { if (!policy.Consumed) throw new Exception("Late claim"); invoked++; });
        Reject(() => NativePartyClassPolicy.RequireSelectionOpen(policy.Consumed, false, 10, 9));
        if (invoked != 1 || !policy.Consumed) throw new Exception("Not submitted once"); passed++;
        Reject(() => policy.Submit("token", "token", pins, pins, () => invoked++));
        var throwing = new NativePartyStartPolicy();
        Reject(() => throwing.Submit("token", "token", pins, pins, () => { throw new InvalidOperationException("Native RPC uncertainty"); }));
        Reject(() => throwing.Submit("token", "token", pins, pins, () => invoked++));
        if (invoked != 1) throw new Exception("Retry after uncertainty");
        Console.WriteLine(passed + " Party Start policy tests PASS");
    }
}
