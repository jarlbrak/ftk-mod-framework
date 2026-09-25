using System;
using System.Reflection;
using UnityEngine;
using FTKModFramework.Core;
class Program
{
    static int checks;
    static readonly FieldInfo Offset = typeof(ScrollingUVs).GetField("uvOffset", BindingFlags.Instance | BindingFlags.NonPublic);
    static void Check(bool condition) { checks++; if (!condition) throw new Exception("Assertion " + checks); }
    static bool Equal(Vector2 a, Vector2 b) { return BitConverter.SingleToInt32Bits(a.x) == BitConverter.SingleToInt32Bits(b.x) && BitConverter.SingleToInt32Bits(a.y) == BitConverter.SingleToInt32Bits(b.y); }
    static void Legacy(ScrollingUVs scroller)
    {
        Vector2 phase = (Vector2)Offset.GetValue(scroller);
        phase += scroller.uvAnimationRate * Time.deltaTime;
        Offset.SetValue(scroller, phase);
    }
    static void Main()
    {
        var material = new Material(); var owner = new EnemyMeshResources { Materials = new[] { material } };
        var renderer = new Renderer { Owner = owner };
        var scroller = new ScrollingUVs { Renderer = renderer, uvAnimationRate = new Vector2(-0.25f, 1.5f), Phase = new Vector2(0.25f, -1) };
        var legacy = new ScrollingUVs { uvAnimationRate = scroller.uvAnimationRate, Phase = scroller.Phase };
        Time.deltaTime = 0.125f;
        Legacy(legacy); Check(!scroller.InvokePrefix()); Check(Equal(scroller.Phase, legacy.Phase));
        Check(Equal(material.Offset, scroller.Phase) && material.Writes == 1 && owner.PrivateCalls == 1);
        renderer.enabled = false;
        Legacy(legacy); Check(!scroller.InvokePrefix()); Check(Equal(scroller.Phase, legacy.Phase));
        Check(material.Writes == 1 && owner.PrivateCalls == 1); // Disabled native renderer still advances phase.
        renderer.enabled = true;
        Vector2 unchanged = scroller.Phase;
        owner.ThrowOnPrivate = true; Check(scroller.InvokePrefix() && Equal(scroller.Phase, unchanged)); owner.ThrowOnPrivate = false;
        material.Supported = false; Check(scroller.InvokePrefix() && Equal(scroller.Phase, unchanged)); material.Supported = true;
        scroller.materialIndex = 1; Check(scroller.InvokePrefix() && Equal(scroller.Phase, unchanged)); scroller.materialIndex = 0;
        scroller.textureName = ""; Check(scroller.InvokePrefix() && Equal(scroller.Phase, unchanged)); scroller.textureName = "_MainTex";
        owner.Owned = false; Check(scroller.InvokePrefix() && Equal(scroller.Phase, unchanged)); owner.Owned = true;
        owner.Retained = false; Check(scroller.InvokePrefix() && Equal(scroller.Phase, unchanged)); owner.Retained = true;
        renderer.Owner = null; Check(scroller.InvokePrefix() && Equal(scroller.Phase, unchanged)); renderer.Owner = owner;
        scroller.Renderer = null; Check(scroller.InvokePrefix() && Equal(scroller.Phase, unchanged)); scroller.Renderer = renderer;
        var parameter = typeof(ExplicitScrollingUvs).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic).GetParameters()[1];
        Check(parameter.Name == "___uvOffset" && parameter.ParameterType == typeof(Vector2).MakeByRefType());
        Check((bool)typeof(ExplicitScrollingUvs).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
        // Warm reflection and linked prefix before allocation census. Stub material access allocates no arrays.
        for (int i = 0; i < 10000; i++) { Legacy(legacy); scroller.InvokePrefix(); }
        const int iterations = 100000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) Legacy(legacy);
        long reflected = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) scroller.InvokePrefix();
        long direct = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(reflected > 0 && direct == 0);
        Console.WriteLine(checks + " scrolling assertions passed. " + iterations + " warmed calls: reflection=" + reflected + " bytes, linked by-ref prefix=" + direct + " bytes.");
        Console.WriteLine("Allocation evidence is this host CLR with allocation-free Unity/owner stubs, not native Mono or whole-frame GC.");
    }
}
