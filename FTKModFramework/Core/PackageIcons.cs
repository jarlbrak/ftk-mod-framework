using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FTKModFramework.Core
{
    // Icons live as long as registered DB rows. Package activation changes require a restart.
    internal static class PackageIcons
    {
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        internal static Sprite Load(string identity)
        {
            Sprite cached;
            if (Icons.TryGetValue(identity, out cached)) return cached;
            string path = PackageModelPaths.Resolve(identity);
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 24 || bytes.Length > 4 * 1024 * 1024 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                throw new ArgumentException("Invalid or oversized package icon.");
            long width = ((long)bytes[16] << 24) | ((long)bytes[17] << 16) | ((long)bytes[18] << 8) | bytes[19];
            long height = ((long)bytes[20] << 24) | ((long)bytes[21] << 16) | ((long)bytes[22] << 8) | bytes[23];
            if (width < 1 || height < 1 || width > 1024 || height > 1024) throw new ArgumentException("Icon dimensions exceed 1024.");
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Sprite sprite = null;
            try
            {
                if (!texture.LoadImage(bytes)) throw new ArgumentException("Icon could not be decoded.");
                texture.name = identity; texture.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
                sprite.name = identity;
                UnityEngine.Object.DontDestroyOnLoad(texture); UnityEngine.Object.DontDestroyOnLoad(sprite);
                Icons.Add(identity, sprite);
                return sprite;
            }
            catch
            {
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
                UnityEngine.Object.Destroy(texture);
                throw;
            }
        }
    }
}
