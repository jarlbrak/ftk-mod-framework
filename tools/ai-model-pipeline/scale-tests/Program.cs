using System;
using System.Reflection;
using FTKModFramework.Core;
using UnityEngine;

// Runs the production scale component with minimal transform/value stubs. This tests baseline
// capture and arithmetic, not Unity AddComponent, Instantiate serialization or the Harmony hook.
internal static class Program
{
    static int checks;
    static void Expect(EnemyVisualScale body, float x, float y, float z)
    {
        Vector3 actual = body.transform.localScale;
        if (Math.Abs(actual.x - x) > 0.00001f || Math.Abs(actual.y - y) > 0.00001f ||
            Math.Abs(actual.z - z) > 0.00001f) throw new Exception("Unexpected body scale");
        checks++;
    }

    static EnemyVisualScale Body(float x, float y, float z)
    {
        EnemyVisualScale body = new EnemyVisualScale();
        body.transform.localScale = new Vector3(x, y, z);
        return body;
    }

    static void Main()
    {
        foreach (float native in new[] { 0.35f, 0.9f })
        {
            EnemyVisualScale body = Body(native, native, native);
            body.Apply(1f, 1f, false);
            Expect(body, native, native, native);
            body.Apply(2f, 1f, false);
            body.Apply(2f, 1f, false);
            Expect(body, native * 2f, native * 2f, native * 2f);
            body.Apply(1f, 1f, false);
            Expect(body, native, native, native);
        }
        EnemyVisualScale nonuniform = Body(0.35f, 0.9f, 1.2f);
        nonuniform.Apply(1f, 1f, false);
        Expect(nonuniform, 0.35f, 0.9f, 1.2f);
        nonuniform.Apply(2f, 1.5f, false);
        nonuniform.Apply(2f, 1.5f, false);
        Expect(nonuniform, 1.05f, 1.8f, 3.6f);

        // Approximate Unity's serialized-field copy to ensure the component exposes all required
        // baseline state. Real native clone behavior is a separate in-game validation gate.
        EnemyVisualScale clone = Body(1.05f, 1.8f, 3.6f);
        foreach (FieldInfo field in typeof(EnemyVisualScale).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            if (Attribute.IsDefined(field, typeof(SerializeField))) field.SetValue(clone, field.GetValue(nonuniform));
        clone.Apply(2f, 1.5f, false);
        Expect(clone, 1.05f, 1.8f, 3.6f);
        clone.Apply(1f, 0f, false);
        Expect(clone, 0.35f, 0.9f, 1.2f);
        Expect(nonuniform, 1.05f, 1.8f, 3.6f);

        EnemyVisualScale legacy = Body(0.35f, 0.9f, 1.2f);
        legacy.Apply(2f, 1.5f, true);
        legacy.Apply(2f, 1.5f, true);
        Expect(legacy, 3f, 2f, 3f);
        legacy.Apply(1f, 1f, false);
        Expect(legacy, 0.35f, 0.9f, 1.2f);
        Expect(Body(0.9f, 0.9f, 0.9f), 0.9f, 0.9f, 0.9f);
        Console.WriteLine("PASS: " + checks + " scale regression assertions (Unity stubs; not an in-game gate).");
    }
}

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] internal sealed class SerializeField : Attribute { }
    internal class MonoBehaviour { public readonly Transform transform = new Transform(); }
    internal class Transform { public Vector3 localScale = Vector3.one; }
    internal struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 one { get { return new Vector3(1f, 1f, 1f); } }
    }
}
