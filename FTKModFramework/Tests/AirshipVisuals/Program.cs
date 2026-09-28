using System;
using System.IO;
using FTKModFramework.Core;
using Newtonsoft.Json.Linq;

internal static class Program
{
    const string Valid = @"{
      'schemaVersion':1,'longAxis':'z',
      'deck':{'y':1,'minX':-2,'maxX':2,'minZ':-3,'maxZ':3},
      'hull':{'minX':-3,'maxX':3,'minZ':-5,'maxZ':5},
      'upperworks':['lift_port','lift_starboard'] }";
    static int _checks;

    static void Main()
    {
        SkySpikeAssetContract c = SkySpikeAssetContract.Parse(Valid);
        Check(c.UniformScale(12f, 6f) == 2f, "length fit remains uniform");
        Check(c.UniformScale(9f, 12f) == 3f, "beam fit remains uniform");
        JObject xAxis = JObject.Parse(Valid); xAxis["longAxis"] = "x";
        Check(SkySpikeAssetContract.Parse(xAxis.ToString()).UniformScale(12f, 6f) == 3f, "explicit x axis swaps length and beam");
        Check(c.Deck.Contains(-2f, 3f, 0f), "boundary slot supported");
        Check(!c.Deck.Contains(-2.1f, 0f, 0f), "outside slot rejected");
        Check(!c.Deck.Contains(float.NaN, 0f, 0f), "NaN target rejected");
        Check(c.IsUpperwork("lift_port/p0"), "declared primitive cut away");
        Check(c.IsUpperwork("lift_port/p2#11"), "large primitive chunks cut away together");
        Check(!c.IsUpperwork("lift_port_support/p0"), "adjacent support remains opaque");
        Check(!c.IsUpperwork("lift_port/pedestal"), "similar named node remains opaque");
        Check(!c.IsUpperwork("deck/p0"), "deck remains opaque");
        c.ValidateUpperworkNodes(new[] { "hull", "lift_port", "lift_starboard" }, new string[0]);
        _checks++;
        Reject(delegate { c.ValidateUpperworkNodes(new[] { "lift_port" }, new string[0]); }, "missing exact source node");
        Reject(delegate { c.ValidateUpperworkNodes(new[] { "lift_port", "lift_starboard" }, new[] { "lift_port" }); }, "ambiguous exact source node");
        JObject partialNode = JObject.Parse(Valid); partialNode["upperworks"] = new JArray("hull/p0");
        SkySpikeAssetContract partial = SkySpikeAssetContract.Parse(partialNode.ToString());
        Reject(delegate { partial.ValidateUpperworkNodes(new[] { "hull" }, new string[0]); }, "generated primitive cannot be declared as a source node");
        partialNode["upperworks"] = new JArray("hull/p0#1");
        SkySpikeAssetContract chunk = SkySpikeAssetContract.Parse(partialNode.ToString());
        Reject(delegate { chunk.ValidateUpperworkNodes(new[] { "hull" }, new string[0]); }, "generated mesh chunk cannot be declared as a source node");
        partial.ValidateUpperworkNodes(new[] { "hull", "hull/p0" }, new string[0]);
        Check(partial.IsUpperwork("hull/p0/p0#1"), "literal suffixed source node still maps its own chunks");
        Check(!partial.IsUpperwork("hull/p0"), "literal suffixed node cannot remove another source primitive");
        c.ValidateBounds(-3f, 3f, -1f, 8f, -5f, 5f);
        Reject(delegate { c.ValidateBounds(-1f, 1f, -1f, 8f, -5f, 5f); }, "sidecar beyond geometry");
        Reject(delegate { c.ValidateBounds(float.NaN, 3f, -1f, 8f, -5f, 5f); }, "nonfinite loaded geometry");
        Reject(delegate { c.UniformScale(float.NaN, 1f); }, "nonfinite footprint");
        Reject(delegate { c.UniformScale(0f, 1f); }, "empty footprint");
        RejectChange("schemaVersion", new JValue(2), "unknown schema");
        RejectChange("longAxis", new JValue("y"), "vertical long axis");
        RejectChange("combatModel", new JValue("../other"), "traversal in variant");
        RejectChange("upperworks", new JArray("lift_port", "lift_port"), "duplicate node roles");
        RejectChange("upperworks", new JArray(1), "nonstring node role");
        RejectChange("upperworks", new JValue("lift_port"), "nonarray node roles");
        JObject missing = JObject.Parse(Valid); ((JObject)missing["deck"]).Remove("y");
        Reject(delegate { SkySpikeAssetContract.Parse(missing.ToString()); }, "missing deck plane");
        JObject outside = JObject.Parse(Valid); outside["deck"]["maxX"] = 4;
        Reject(delegate { SkySpikeAssetContract.Parse(outside.ToString()); }, "deck outside hull");
        JObject inverted = JObject.Parse(Valid); inverted["deck"]["minZ"] = 4;
        Reject(delegate { SkySpikeAssetContract.Parse(inverted.ToString()); }, "inverted deck rectangle");
        JObject overflow = JObject.Parse(Valid); overflow["hull"]["minX"] = -3e38; overflow["hull"]["maxX"] = 3e38;
        Reject(delegate { SkySpikeAssetContract.Parse(overflow.ToString()); }, "finite endpoints overflowing width");
        JObject narrow = JObject.Parse(Valid); narrow["deck"]["minX"] = 0; narrow["deck"]["maxX"] = 0.02;
        SkySpikeAssetContract tiny = SkySpikeAssetContract.Parse(narrow.ToString());
        Reject(delegate { tiny.UniformScale(1f, float.MaxValue); }, "overflowing scale");
        Check(Math.Abs(SkySpikeCameraMath.FitDistance(0, 10, 0, 90, 2, 1) - 10) < .001, "vertical camera fit");
        Check(Math.Abs(SkySpikeCameraMath.FitDistance(20, 0, 3, 90, 2, 1) - 13) < .001, "horizontal fit includes near-side depth");
        Check(SkySpikeCameraMath.FitDistance(10, 0, 0, 60, 1, .72f) > SkySpikeCameraMath.FitDistance(10, 0, 0, 60, 2, .72f), "narrow aspect moves camera back");
        Check(SkySpikeCameraMath.FitDistance(0, 10, 0, 60, 2, .72f) > SkySpikeCameraMath.FitDistance(0, 10, 0, 60, 2, 1), "UI safe margin moves camera back");
        bool invalidCamera = false;
        try { SkySpikeCameraMath.FitDistance(1, 1, 1, 0, 1, .72f); } catch (ArgumentException) { invalidCamera = true; }
        Check(invalidCamera, "invalid camera FOV fails safely");
        VerifyUpperViewport(30f, 1.6f);
        VerifyUpperViewport(35f, .8f);
        VerifyUpperViewport(60f, 1.6f);
        VerifyUpperViewport(75f, 2.3f);
        VerifyReveal(30f, 1.6f);
        VerifyReveal(35f, .8f);
        VerifyReveal(60f, 2.3f);
        VerifyRevealFallback();
        bool invalidBand = false;
        try { SkySpikeCameraMath.FitViewportDistance(1, 1, 1, 60, 1.6f, .82f, .55f, .8f); } catch (ArgumentException) { invalidBand = true; }
        Check(invalidBand, "inverted viewport rejected");
        Check(Math.Abs(SkySpikeCameraMath.FarClipForDepthRange(.5f, 140f, 218.79f, 258.79f) - 260.79f) < .001f, "wide shot far clip includes farthest point plus bounded margin");
        Check(SkySpikeCameraMath.FarClipForDepthRange(.5f, 140f, 25, 45) == 140, "idle retains wider native far clip");
        RejectCameraDepth(.5f, 140, .5f, 30, "near-plane intersection rejected");
        RejectCameraDepth(.5f, 140, 10, float.PositiveInfinity, "nonfinite far depth rejected");
        RejectCameraDepth(.5f, 140, 30, 10, "inverted depth range rejected");
        // Actual rendered frame 19892: this float quaternion's self-dot rounds below one.
        float qx = -.07367994f, qy = .9284858f, qz = -.235301971f, qw = -.2777019f;
        float selfDot = qx*qx + qy*qy + qz*qz + qw*qw;
        Check(2 * Math.Acos(Math.Min(1f, Math.Abs(selfDot))) * 180 / Math.PI > .01,
            "native trace reproduces old angular ownership false negative");
        Check(SkySpikeCameraMath.SameRotation(qx,qy,qz,qw,qx,qy,qz,qw),
            "identical nonunit float rotation retains pose ownership");
        Check(SkySpikeCameraMath.SameRotation(qx,qy,qz,qw,-qx,-qy,-qz,-qw),
            "quaternion sign flip retains same owned rotation");
        Check(SkySpikeCameraMath.SameRotation(qx,qy,qz,qw,qx,qy+1e-7f,qz,qw),
            "transform float round-trip retains pose ownership");
        Check(!SkySpikeCameraMath.SameRotation(qx,qy,qz,qw,qx,qy+1e-5f,qz,qw),
            "a newer changed rotation is not restored over");
        Check(!SkySpikeCameraMath.SameRotation(qx,qy,qz,qw,float.NaN,qy,qz,qw),
            "invalid rotation cannot claim pose ownership");
        Check(SkySpikeCameraMath.EaseDistance(40, 80, 0) == 40, "outward distance does not snap at transition start");
        Check(SkySpikeCameraMath.EaseDistance(80, 40, 0) == 80, "inward distance preserves initial radius");
        float outwardStep = SkySpikeCameraMath.EaseDistance(40, 80, 1f / 60f);
        Check(outwardStep > 40 && outwardStep < 42, "outward framing expansion is eased instead of clamped to target");
        float radius30 = 40, radius120 = 40;
        for (int i = 0; i < 30; i++) radius30 = SkySpikeCameraMath.EaseDistance(radius30, 80, 1f / 30f);
        for (int i = 0; i < 120; i++) radius120 = SkySpikeCameraMath.EaseDistance(radius120, 80, 1f / 120f);
        Check(Math.Abs(radius30 - radius120) < .0001f, "distance easing is independent of render rate");
        float interruptedRadius = SkySpikeCameraMath.EaseDistance(40, 80, .2f);
        Check(SkySpikeCameraMath.EaseDistance(interruptedRadius, 25, 0) == interruptedRadius,
            "reversed framing target preserves current radius at interruption");
        float reversedStep = SkySpikeCameraMath.EaseDistance(interruptedRadius, 25, 1f / 60f);
        Check(reversedStep < interruptedRadius && reversedStep > 25,
            "reversed framing eases toward the new target without overshooting");
        float resumedRadius = SkySpikeCameraMath.EaseDistance(reversedStep, 80, 1f / 60f);
        Check(resumedRadius > reversedStep && resumedRadius < 80,
            "second framing interruption uses the latest radius without resetting to an old endpoint");
        Check(SkySpikeCameraMath.ReturnBlend(.001f) < .000001f, "handoff starts with negligible velocity and acceleration");
        Check(SkySpikeCameraMath.ReturnBlend(0) == 0f, "return starts at exact native pose");
        Check(SkySpikeCameraMath.ReturnBlend(.75f) == 1f && SkySpikeCameraMath.ReturnBlend(2f) == 1f, "return reaches exact authored pose within750ms");
        Check(Math.Abs(SkySpikeCameraMath.ReturnBlend(.375f) - .5f) < .0001f, "return half time is midpoint");
        float previousReturn = 0;
        for (int step=1; step<=75; step++)
        {
            float currentReturn = SkySpikeCameraMath.ReturnBlend(step*.01f);
            Check(currentReturn >= previousReturn && currentReturn <= 1f && currentReturn-previousReturn < .034f,
                "return blend monotonic without discontinuous progress");
            previousReturn = currentReturn;
        }
        Check(SkySpikeCameraMath.FarClipForPresentation(.5f, 140, -12, 35, true) == 140, "native return permits fighters behind near plane");
        Check(SkySpikeCameraMath.FarClipForPresentation(.5f, 140, -12, 170, true) == 172, "native return extends only positive far range");
        Check(SkySpikeCameraMath.FarClipForPresentation(.5f, 140, -12, -1, true) == 140, "native return retains far plane when all points behind");
        RejectCameraDepth(.5f, 140, -12, 35, "completed authored return still rejects behind-camera fighters");
        float cloudScale;
        Check(SkySpikeCloudMath.TryTextureScale(476.672f, 0f, 1f, out cloudScale) && Math.Abs(cloudScale - 9.53344f) < .0001f,
            "expanded native water width gets a 50-unit texture period");
        Check(SkySpikeCloudMath.TryTextureScale(403.808f, 0f, 1f, out cloudScale) && Math.Abs(cloudScale - 8.07616f) < .0001f,
            "expanded native water length gets a 50-unit texture period");
        Check(SkySpikeCloudMath.TryTextureScale(500f, 0f, 10f, out cloudScale) && cloudScale == 1f,
            "native repeated UV range is included");
        Check(SkySpikeCloudMath.TryTextureScale(500f, -5f, 5f, out cloudScale) && cloudScale == 1f,
            "UV offset does not change scale");
        Check(SkySpikeCloudMath.TryTextureScale(400f, 2f, 2.5f, out cloudScale) && cloudScale == 16f &&
            400f / (.5f * cloudScale * 2f) == 25f, "fractional UV span preserves approximate billow width");
        Check(!SkySpikeCloudMath.TryTextureScale(0f, 0f, 1f, out cloudScale) && cloudScale == 1f, "zero world extent keeps safe fallback");
        Check(!SkySpikeCloudMath.TryTextureScale(-1f, 0f, 1f, out cloudScale), "negative world extent rejected");
        Check(!SkySpikeCloudMath.TryTextureScale(float.NaN, 0f, 1f, out cloudScale), "nonfinite world extent rejected");
        Check(!SkySpikeCloudMath.TryTextureScale(400f, float.NegativeInfinity, 1f, out cloudScale), "nonfinite UV rejected");
        Check(!SkySpikeCloudMath.TryTextureScale(400f, 1f, 1f, out cloudScale), "degenerate UV range rejected");
        Check(!SkySpikeCloudMath.TryTextureScale(400f, 1f, 0f, out cloudScale), "reversed UV range rejected");
        Check(!SkySpikeCloudMath.TryTextureScale(float.MaxValue, 0f, 1f, out cloudScale), "excessive tiling rejected");
        Check(!SkySpikeCloudMath.TryTextureScale(400f, -float.MaxValue, float.MaxValue, out cloudScale), "overflow-sized UV span keeps safe fallback");
        float planar;
        Check(SkySpikeCloudMath.TryPlanarCoordinate(-20, -20, 30, out planar) && planar == 0, "planar lower edge maps to zero");
        Check(SkySpikeCloudMath.TryPlanarCoordinate(30, -20, 30, out planar) && planar == 1, "planar upper edge maps to one");
        Check(SkySpikeCloudMath.TryPlanarCoordinate(5, -20, 30, out planar) && planar == .5f, "planar middle remains uniform");
        Check(SkySpikeCloudMath.TryPlanarCoordinate(-7.5f, -20, 30, out planar) && planar == .25f, "planar quarter avoids native border UV compression");
        Check(!SkySpikeCloudMath.TryPlanarCoordinate(0, 0, 0, out planar), "zero planar extent rejected");
        Check(!SkySpikeCloudMath.TryPlanarCoordinate(0, 1, -1, out planar), "inverted planar extent rejected");
        Check(!SkySpikeCloudMath.TryPlanarCoordinate(float.NaN, -1, 1, out planar), "nonfinite planar position rejected");
        Check(!SkySpikeCloudMath.TryPlanarCoordinate(0, float.NegativeInfinity, 1, out planar), "nonfinite planar bounds rejected");
        Check(!SkySpikeCloudMath.TryPlanarCoordinate(2, -1, 1, out planar), "out of bounds planar position rejected");
        Check(SkySpikeCloudMath.TryPlanarCoordinate(0, -float.MaxValue, float.MaxValue, out planar) && planar == .5f, "finite planar endpoints avoid float subtraction overflow");
        VerifyRotors();
        Console.WriteLine("Passed " + _checks + " airship contract checks.");
    }

    static JObject RotorContract()
    {
        JObject o = JObject.Parse(Valid);
        o["rotors"] = JArray.Parse("[{'node':'rotor_port','pivot':[1,2,3],'axis':[0,0,4],'degreesPerSecond':90}]");
        return o;
    }

    static void RejectRotor(string field, JToken value, string label)
    {
        JObject o = RotorContract(); o["rotors"][0][field] = value;
        Reject(delegate { SkySpikeAssetContract.Parse(o.ToString()); }, label);
    }

    static void VerifyRotors()
    {
        Check(SkySpikeAssetContract.Parse(Valid).Rotors.Length == 0, "legacy sidecar has no cosmetic motion");
        SkySpikeAssetContract c = SkySpikeAssetContract.Parse(RotorContract().ToString());
        Check(c.Rotors.Length == 1 && c.Rotors[0].Axis[2] == 1f && c.Rotors[0].Pivot[2] == 3f,
            "rotor axis normalized while authored pivot preserved");
        c.ValidateRotorNodes(new[] { "hull", "rotor_port" }, new string[0]);
        c.ValidateBounds(-3, 3, -1, 8, -5, 5);
        _checks++;
        Reject(delegate { c.ValidateRotorNodes(new[] { "hull" }, new string[0]); }, "missing rotor source rejected");
        Reject(delegate { c.ValidateRotorNodes(new[] { "rotor_port" }, new[] { "rotor_port" }); }, "ambiguous rotor source rejected");
        JObject primitive = RotorContract(); primitive["rotors"][0]["node"] = "hull/p0";
        SkySpikeAssetContract alias = SkySpikeAssetContract.Parse(primitive.ToString());
        Reject(delegate { alias.ValidateRotorNodes(new[] { "hull" }, new string[0]); }, "primitive alias cannot become rotor source");
        JObject duplicate = RotorContract(); ((JArray)duplicate["rotors"]).Add(duplicate["rotors"][0].DeepClone());
        Reject(delegate { SkySpikeAssetContract.Parse(duplicate.ToString()); }, "duplicate rotor source rejected");
        RejectRotor("node", new JValue("lift_port"), "cutaway rotor rejected");
        RejectRotor("node", new JValue(1), "nonnamed rotor rejected");
        RejectRotor("axis", new JArray(0, 0, 0), "zero rotor axle rejected");
        RejectRotor("axis", new JArray(0, 0, float.NaN), "nonfinite rotor axle rejected");
        RejectRotor("pivot", new JArray(0, 1), "partial pivot rejected");
        RejectRotor("pivot", new JArray(0, "1", 2), "string pivot rejected");
        RejectRotor("degreesPerSecond", new JValue(float.PositiveInfinity), "nonfinite rotor speed rejected");
        RejectRotor("degreesPerSecond", new JValue(-721), "excessive reverse speed rejected");
        JObject outside = RotorContract(); outside["rotors"][0]["pivot"] = new JArray(50, 2, 3);
        SkySpikeAssetContract outsideRotor = SkySpikeAssetContract.Parse(outside.ToString());
        Reject(delegate { outsideRotor.ValidateBounds(-3, 3, -1, 8, -5, 5); }, "out of model pivot rejected");
        Check(SkySpikeRotorMath.Advance(20, 90, 0) == 20, "paused game retains rotor pose");
        Check(SkySpikeRotorMath.Advance(0, 90, 1) == 90, "authored speed advances one second");
        Check(SkySpikeRotorMath.Advance(0, -90, 1) == 270, "counterrotating propeller wraps positive");
        Check(SkySpikeRotorMath.Advance(350, 90, 1) == 80, "forward angle wraps");
        Check(SkySpikeRotorMath.Advance(20, 90, float.NaN) == 20, "invalid delta preserves pose");
        Check(SkySpikeRotorMath.Advance(20, 90, -1) == 20, "negative delta preserves pose");
        float at30 = 0, at120 = 0;
        for (int i = 0; i < 30; i++) at30 = SkySpikeRotorMath.Advance(at30, 90, 1f / 30);
        for (int i = 0; i < 120; i++) at120 = SkySpikeRotorMath.Advance(at120, 90, 1f / 120);
        Check(Math.Abs(at30 - at120) < .001f && Math.Abs(at30 - 90) < .001f, "same elapsed time gives same rotor angle at different frame rates");
        Check(SkySpikeAssetContract.Finite(SkySpikeRotorMath.Advance(0, 720, float.MaxValue)), "large finite elapsed time cannot overflow rotor angle");
    }

    static void RejectCameraDepth(float nearClip, float farClip, float nearest, float farthest, string label)
    {
        bool rejected = false;
        try { SkySpikeCameraMath.FarClipForDepthRange(nearClip, farClip, nearest, farthest); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, label);
    }

    static void VerifyReveal(float fov, float aspect)
    {
        float[,] actors = { {-3,-1.3f,-4}, {-3,1.8f,-4}, {3,-1.3f,4}, {3,1.8f,4} };
        float[,] ship = { {-13,-8,-9}, {-13,3,-9}, {16,-8,7}, {16,7,7} };
        // Deliberately tilted axle: depth changes through the turn, not just projected X/Y.
        float[] orbit = { -12,-4,6, 2,1,2, -1,2,0 };
        SkySpikeCameraMath.ViewportEnvelope a = new SkySpikeCameraMath.ViewportEnvelope(fov, aspect, .55f, .82f, .8f);
        SkySpikeCameraMath.ViewportEnvelope s = new SkySpikeCameraMath.ViewportEnvelope(fov, aspect, .24f, .82f, .85f);
        for (int i=0;i<actors.GetLength(0);i++) a.Add(actors[i,0],actors[i,1],actors[i,2]);
        for (int i=0;i<ship.GetLength(0);i++) s.Add(ship[i,0],ship[i,1],ship[i,2]);
        s.AddSweep(orbit[0],orbit[1],orbit[2],orbit[3],orbit[4],orbit[5],orbit[6],orbit[7],orbit[8]);
        float distance, offset;
        Check(SkySpikeCameraMath.TryFitReveal(a,s,.5f,out distance,out offset), "two reveal bands admit a bounded common pose");
        for (int i=0;i<actors.GetLength(0);i++)
            Check(ProjectedInside(actors[i,0],actors[i,1],actors[i,2],distance,offset,fov,aspect,.1,.9,.55,.82), "reveal actors remain above HUD with nonzero depth");
        for (int i=0;i<ship.GetLength(0);i++)
            Check(ProjectedInside(ship[i,0],ship[i,1],ship[i,2],distance,offset,fov,aspect,.075,.925,.24,.82), "reveal ship occupies wider viewport without sacrificing actor band");
        bool sweepInside = true;
        for (int step=0;step<360;step++)
        {
            double angle=step*Math.PI/180, c=Math.Cos(angle), sin=Math.Sin(angle);
            sweepInside &= ProjectedInside(orbit[0]+orbit[3]*c+orbit[6]*sin, orbit[1]+orbit[4]*c+orbit[7]*sin,
                orbit[2]+orbit[5]*c+orbit[8]*sin,distance,offset,fov,aspect,.075,.925,.24,.82);
        }
        Check(sweepInside, "analytic rotor envelope contains every sampled phase around tilted axle");
        float al, au, sl, su;
        a.OffsetInterval(distance,out al,out au);s.OffsetInterval(distance,out sl,out su);
        Check(offset>=Math.Max(al,sl)-.0001 && offset<=Math.Min(au,su)+.0001, "chosen view-up offset satisfies both interval constraints");
        Check(distance-s.MaxZ>.5 && distance-a.MaxZ>.5, "all fitted points remain ahead of native near clip");
    }

    static bool ProjectedInside(double x,double y,double z,float distance,float offset,float fov,float aspect,
        double left,double right,double bottom,double top)
    {
        double depth=distance-z, tangent=Math.Tan(fov*Math.PI/360);
        double px=.5+x/(2*depth*tangent*aspect), py=.5+(y-offset)/(2*depth*tangent);
        return depth>.5 && px>=left-.00001 && px<=right+.00001 && py>=bottom-.00001 && py<=top+.00001;
    }

    static void VerifyRevealFallback()
    {
        SkySpikeCameraMath.ViewportEnvelope a = new SkySpikeCameraMath.ViewportEnvelope(30,1.6f,.55f,.82f,.8f);
        SkySpikeCameraMath.ViewportEnvelope s = new SkySpikeCameraMath.ViewportEnvelope(30,1.6f,.24f,.82f,.85f);
        float distance,offset;
        Check(!SkySpikeCameraMath.TryFitReveal(a,s,.5f,out distance,out offset) && distance==0 && offset==0, "empty reveal falls back without invalid pose");
        a.Add(0,0,0);s.Add(0,0,0);
        Check(!SkySpikeCameraMath.TryFitReveal(a,s,float.NaN,out distance,out offset), "invalid near clip rejects reveal");
        SkySpikeCameraMath.ViewportEnvelope otherLens = new SkySpikeCameraMath.ViewportEnvelope(60,1.6f,.24f,.82f,.85f);
        otherLens.Add(0,0,0);
        Check(!SkySpikeCameraMath.TryFitReveal(a,otherLens,.5f,out distance,out offset), "mixed lens envelopes rejected");
        SkySpikeCameraMath.ViewportEnvelope separated = new SkySpikeCameraMath.ViewportEnvelope(30,1.6f,.05f,.2f,.85f);
        separated.Add(0,0,0);
        Check(!SkySpikeCameraMath.TryFitReveal(a,separated,.5f,out distance,out offset), "nonoverlapping bands fail safely");
        s.Add(100000,0,0);
        Check(!SkySpikeCameraMath.TryFitReveal(a,s,.5f,out distance,out offset), "extreme reveal cannot replace close fallback with distant silhouette");
        bool invalid=false;
        try { a.AddSweep(0,0,0,0,float.NaN,0,0,0,0); } catch (ArgumentException) { invalid=true; }
        Check(invalid, "nonfinite rotor sweep rejected before caching");
    }

    static void VerifyUpperViewport(float fov, float aspect)
    {
        // Hull corners plus actor feet/heads with substantial near/far depth differences.
        float[,] points = { {-8,-5,-6}, {-8,-5,6}, {-8,5,-6}, {-8,5,6},
            {8,-5,-6}, {8,-5,6}, {8,5,-6}, {8,5,6}, {-3,-1.3f,2}, {-3,1.5f,2}, {3,-1.3f,-2}, {3,1.5f,-2} };
        float distance = 0;
        for (int i=0; i<points.GetLength(0); i++)
            distance = Math.Max(distance, SkySpikeCameraMath.FitViewportDistance(points[i,0], points[i,1], points[i,2], fov, aspect, .55f, .82f, .8f));
        double tangent = Math.Tan(fov*Math.PI/360.0);
        foreach (float extraDistance in new[] { 0f, 12f, distance * .12f })
        {
            float d = distance + extraDistance;
            float offset = SkySpikeCameraMath.ViewUpOffset(d, fov, .55f, .82f);
            for (int i=0; i<points.GetLength(0); i++)
            {
                double depth = d-points[i,2];
                double x = .5+.5*points[i,0]/(depth*tangent*aspect);
                double y = .5+.5*(points[i,1]-offset)/(depth*tangent);
                Check(depth>0 && x>=.09999 && x<=.90001 && y>=.54999 && y<=.82001,
                    "asymmetric projected corner/actor inside HUD-free viewport");
            }
        }
    }

    static void RejectChange(string field, JToken value, string name)
    {
        JObject o = JObject.Parse(Valid); o[field] = value;
        Reject(delegate { SkySpikeAssetContract.Parse(o.ToString()); }, name);
    }

    static void Reject(Action action, string name)
    {
        try { action(); }
        catch (InvalidDataException) { _checks++; return; }
        throw new Exception("Expected rejection: " + name);
    }

    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("Failed: " + name);
        _checks++;
    }
}
