using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FTKModFramework.Core
{
    // Icons live with their generation; retirement requires all row and UI consumers to detach.
    internal static class PackageIcons
    {
        private static Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        internal static int ReloadIconCount { get { return Icons.Count; } }
        internal static Action SuspendForReload()
        {
            Dictionary<string, Sprite> old = Icons;
            Icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            return delegate { Icons = old; };
        }

        internal static Sprite Load(string identity)
        {
            Sprite cached;
            if (Icons.TryGetValue(identity, out cached)) return cached;
            string path = PackageModelPaths.Resolve(identity);
            byte[] bytes = File.ReadAllBytes(path);
            int width, height;
            PngStructure.Validate(bytes, 4 * 1024 * 1024, 1024, out width, out height);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            HotReload.PaladinResourceState.Own(texture);
            Sprite sprite = null;
            try
            {
                if (!texture.LoadImage(bytes) || texture.width != width || texture.height != height)
                    throw new ArgumentException("Icon could not be decoded at its declared dimensions.");
                texture.name = identity; texture.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
                HotReload.PaladinResourceState.Own(sprite);
                sprite.name = identity;
                UnityEngine.Object.DontDestroyOnLoad(texture); UnityEngine.Object.DontDestroyOnLoad(sprite);
                Icons.Add(identity, sprite);
                return sprite;
            }
            catch
            {
                if (sprite != null) HotReload.PaladinResourceState.DestroyTracked(sprite);
                HotReload.PaladinResourceState.DestroyTracked(texture);
                throw;
            }
        }
    }
}
