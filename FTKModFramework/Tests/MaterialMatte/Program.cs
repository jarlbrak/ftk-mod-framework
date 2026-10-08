using System;
using System.Collections.Generic;
using FTKModFramework.Core;
using UnityEngine;

internal static class Program
{
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); }

    private static void Main()
    {
        Material native = new Material();
        native.SetTexture("_MainTex", new object());
        native.SetTexture("_MetallicGlossMap", new object());
        native.SetTexture("_SpecGlossMap", new object());
        native.SetFloat("_Metallic", 0.8f);
        native.SetFloat("_Glossiness", 0.7f);
        native.SetFloat("_GlossMapScale", 0.6f);
        native.EnableKeyword("_METALLICGLOSSMAP");
        native.EnableKeyword("_SPECGLOSSMAP");
        native.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        native.EnableKeyword("_EMISSION");
        Material untouched = new Material(native);
        ExplicitMaterialOptions.ApplyMatte(untouched, false);
        Check(untouched.GetFloat("_Metallic") == 0.8f && untouched.GetTexture("_MetallicGlossMap") != null,
            "default policy changed inherited material");
        Material privateCopy = new Material(native);
        ExplicitMaterialOptions.ApplyMatte(privateCopy, true);
        Check(privateCopy.GetFloat("_Metallic") == 0f && privateCopy.GetFloat("_Glossiness") == 0f &&
            privateCopy.GetFloat("_GlossMapScale") == 0f, "matte scalars");
        Check(privateCopy.GetTexture("_MetallicGlossMap") == null && privateCopy.GetTexture("_SpecGlossMap") == null &&
            !privateCopy.HasKeyword("_METALLICGLOSSMAP") && !privateCopy.HasKeyword("_SPECGLOSSMAP") &&
            !privateCopy.HasKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"), "matte maps and keywords");
        Check(privateCopy.GetTexture("_MainTex") != null && privateCopy.HasKeyword("_EMISSION"),
            "diffuse and emission preserved");
        Check(native.GetFloat("_Metallic") == 0.8f && native.GetTexture("_MetallicGlossMap") != null &&
            native.HasKeyword("_METALLICGLOSSMAP"), "native source material changed");
        Console.WriteLine("PASS: matte policy applies only to a private opt-in material copy");
    }
}

namespace UnityEngine
{
    public sealed class Texture2D { }
    public struct Color
    {
        public static readonly Color black = new Color();
        public Color(float r, float g, float b, float a) { }
    }

    public sealed class Material
    {
        private readonly Dictionary<string, object> textures = new Dictionary<string, object>();
        private readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        private readonly HashSet<string> keywords = new HashSet<string>();
        public string name = "matte-test";
        public Material() { }
        public Material(Material other)
        {
            name = other.name;
            foreach (var pair in other.textures) textures.Add(pair.Key, pair.Value);
            foreach (var pair in other.floats) floats.Add(pair.Key, pair.Value);
            foreach (string keyword in other.keywords) keywords.Add(keyword);
        }
        public bool HasProperty(string key) { return true; }
        public void SetTexture(string key, object value) { textures[key] = value; }
        public object GetTexture(string key) { object value; return textures.TryGetValue(key, out value) ? value : null; }
        public void SetFloat(string key, float value) { floats[key] = value; }
        public float GetFloat(string key) { float value; return floats.TryGetValue(key, out value) ? value : 0f; }
        public void SetColor(string key, Color value) { }
        public void EnableKeyword(string keyword) { keywords.Add(keyword); }
        public void DisableKeyword(string keyword) { keywords.Remove(keyword); }
        public bool HasKeyword(string keyword) { return keywords.Contains(keyword); }
    }
}
