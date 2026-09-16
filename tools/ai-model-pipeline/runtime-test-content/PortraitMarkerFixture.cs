using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal static class PortraitMarkerFixture
{
    internal static string Validate(JToken token)
    {
        if (token == null || token.Type != JTokenType.String) throw new ArgumentException("portraitMarkerPath must be an exact child path string.");
        string path = (string)token;
        if (string.IsNullOrEmpty(path) || path.Length > 512 || path.IndexOf('\\') >= 0) throw new ArgumentException("Invalid portraitMarkerPath.");
        foreach (char value in path) if (char.IsControl(value)) throw new ArgumentException("Control character in portraitMarkerPath.");
        foreach (string part in path.Split('/')) if (part == "" || part == "." || part == "..") throw new ArgumentException("portraitMarkerPath must select a proper child.");
        return path;
    }
    internal static Transform Resolve(Transform root, string path)
    {
        Transform marker = null; int matches = 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate == root) continue;
            List<string> names = new List<string>(); Transform current = candidate;
            while (current != null && current != root) { names.Add(current.name); current = current.parent; }
            names.Reverse();
            if (current == root && string.Join("/", names.ToArray()) == path) { marker = candidate; matches++; }
        }
        if (matches != 1) throw new ArgumentException("Portrait marker path missing or ambiguous.");
        int leaves = 0; foreach (Transform candidate in transforms) if (candidate.name == marker.name) leaves++;
        if (leaves != 1) throw new ArgumentException("Portrait marker leaf name must be unique for native lookup.");
        return marker;
    }
}
