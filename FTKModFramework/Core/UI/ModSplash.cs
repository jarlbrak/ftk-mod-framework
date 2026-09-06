using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using FTKModFramework.Core.Data;

namespace FTKModFramework.Core.UI
{
    /// <summary>
    /// The framework's splash card. Shown ONCE, the first time the title screen appears (after the game's own
    /// logo sequence has played), so a player sees at a glance that the framework loaded and which mods are on.
    ///
    /// It is a full-screen uGUI overlay built in code from stock UnityEngine.UI pieces (the same approach as
    /// <see cref="ModsPanel"/>): its own Canvas sorted above the title, a raycast-blocking backdrop so a click
    /// cannot land on a title button underneath the card, the framework logo (embedded PNG), the version, and
    /// the enabled-mod list. A <see cref="CanvasGroup"/> fades it in, holds for UI/SplashSeconds, and fades it
    /// out; any key or click skips ahead. The whole build is guarded: a failure logs and leaves the title screen
    /// untouched, and the card destroys itself when done so nothing lingers.
    /// </summary>
    internal static class ModSplash
    {
        private const string LogoResource = "FTKModFramework.assets.brand.splash-logo.png";
        private const int MaxNamesListed = 6;
        private static bool _shown;

        /// <summary>Show the card once per process (a later return to the title never re-shows it).</summary>
        public static void ShowOnce()
        {
            if (_shown) return;
            _shown = true;

            if (Plugin.ShowSplash != null && !Plugin.ShowSplash.Value)
            {
                Plugin.Log.LogInfo("Splash: disabled (UI/ShowSplash=false).");
                return;
            }

            float hold = Plugin.SplashSeconds != null ? Plugin.SplashSeconds.Value : 4f;
            hold = Mathf.Clamp(hold, 0.5f, 120f);

            GameObject root = null;
            try
            {
                root = new GameObject("FtkmfSplash");
                Build(root, hold);
            }
            catch (Exception e)
            {
                if (root != null)
                {
                    root.SetActive(false);
                    UnityEngine.Object.Destroy(root);
                }
                Plugin.Log.LogError("Splash: build failed (title screen left unchanged): " + e);
            }
        }

        private static void Build(GameObject root, float hold)
        {
            UnityEngine.Object.DontDestroyOnLoad(root);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // above the title canvases and above the Mods panel (30000).

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            root.AddComponent<GraphicRaycaster>();

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            // Backdrop: near-black, a raycast target, so a click during the card goes nowhere near the title.
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.04f, 0.06f, 0.97f);
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);

            // Centered column. Children keep their own width (childControlWidth = false) so the logo keeps its
            // aspect ratio; heights come from each child's preferred height.
            GameObject card = NewChild("Card", root.transform);
            RectTransform cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(1200f, 700f);

            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Texture2D logo = LoadLogo();
            if (logo != null)
            {
                GameObject logoGo = NewChild("Logo", card.transform);
                RawImage img = logoGo.AddComponent<RawImage>();
                img.texture = logo;
                img.raycastTarget = false;
                float w = 940f;
                float h = w * logo.height / (float)logo.width;
                logoGo.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
                LayoutElement le = logoGo.AddComponent<LayoutElement>();
                le.preferredHeight = h;
                le.minHeight = h;
            }

            int enabledCount;
            string modsLine = BuildModsLine(out enabledCount);

            AddText(card.transform, "FTK Mod Framework " + Plugin.Version + " loaded",
                42, FontStyle.Bold, new Color(0.906f, 0.710f, 0.235f, 1f), 1100f);
            AddText(card.transform, modsLine, 26, FontStyle.Normal, new Color(0.92f, 0.92f, 0.94f, 1f), 1100f);
            AddText(card.transform, "Toggle mods with the Mods button on the title screen. Press any key to continue.",
                20, FontStyle.Normal, new Color(0.65f, 0.68f, 0.74f, 1f), 1100f);

            ModSplashBehaviour behaviour = root.AddComponent<ModSplashBehaviour>();
            behaviour.Init(group, hold);

            Plugin.Log.LogInfo("Splash: shown (" + enabledCount + " mod(s) enabled, logo=" + (logo != null) +
                ", hold=" + hold + "s).");
        }

        /// <summary>"N mods enabled: A, B (v1.2), C" from the registry, capped at a few names; or a nudge when none.</summary>
        private static string BuildModsLine(out int enabledCount)
        {
            enabledCount = 0;
            List<string> names = new List<string>();
            foreach (ModEntry e in ModRegistry.Entries)
            {
                if (!e.Enabled || !e.FrameworkCompatible) continue;
                enabledCount++;
                if (names.Count < MaxNamesListed)
                {
                    string n = e.DisplayName;
                    if (e.Version != null && e.Version.Trim().Length > 0) n += " (v" + e.Version + ")";
                    names.Add(n);
                }
            }

            if (enabledCount == 0)
                return "No mods enabled yet. Open Mods on the title screen to turn some on.";

            StringBuilder sb = new StringBuilder();
            sb.Append(enabledCount).Append(enabledCount == 1 ? " mod enabled: " : " mods enabled: ");
            sb.Append(string.Join(", ", names.ToArray()));
            if (enabledCount > names.Count) sb.Append(" and ").Append(enabledCount - names.Count).Append(" more");
            return sb.ToString();
        }

        /// <summary>The embedded logo PNG as a texture; null (with one warning) if it cannot be read.</summary>
        private static Texture2D LoadLogo()
        {
            try
            {
                Assembly asm = typeof(Plugin).Assembly;
                byte[] bytes;
                using (Stream s = asm.GetManifestResourceStream(LogoResource))
                {
                    if (s == null)
                    {
                        Plugin.Log.LogWarning("Splash: embedded logo '" + LogoResource + "' not found. Manifest resources: " +
                            string.Join(", ", asm.GetManifestResourceNames()));
                        return null;
                    }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        byte[] buffer = new byte[8192];
                        int read;
                        while ((read = s.Read(buffer, 0, buffer.Length)) > 0) ms.Write(buffer, 0, read);
                        bytes = ms.ToArray();
                    }
                }

                Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(tex, bytes))
                {
                    Plugin.Log.LogWarning("Splash: the embedded logo is not a readable PNG.");
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                return tex;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Splash: logo load failed: " + e.Message);
                return null;
            }
        }

        // ----- small UI builders -----

        private static void AddText(Transform parent, string text, int size, FontStyle style, Color color, float width)
        {
            GameObject go = NewChild("Text", parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 0f);
            Text t = go.AddComponent<Text>();
            t.text = text;                // a non-"STR_" literal: FTKLocalizationUI leaves it alone.
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
        }

        private static GameObject NewChild(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Drives the card's opacity: fade in, hold, fade out, then self-destruct. Any key or click (after the first
    /// few frames, so the keypress that opened the title cannot skip it) starts a quick fade-out. Uses unscaled
    /// time so a paused or slow-motion title screen cannot freeze it.
    /// </summary>
    internal sealed class ModSplashBehaviour : MonoBehaviour
    {
        private const float FadeIn = 0.35f;
        private const float FadeOut = 0.6f;
        private const float SkipFade = 0.25f;

        private CanvasGroup _group;
        private float _hold;
        private float _t;
        private bool _skipping;
        private float _skipT;
        private float _skipFrom;

        public void Init(CanvasGroup group, float hold)
        {
            _group = group;
            _hold = hold;
        }

        private void Update()
        {
            if (_group == null) { Destroy(gameObject); return; }

            float dt = Time.unscaledDeltaTime;
            _t += dt;

            if (!_skipping && _t > FadeIn * 0.5f && Input.anyKeyDown)
            {
                _skipping = true;
                _skipT = 0f;
                _skipFrom = _group.alpha;
            }

            float alpha;
            if (_skipping)
            {
                _skipT += dt;
                alpha = Mathf.Lerp(_skipFrom, 0f, _skipT / SkipFade);
                if (_skipT >= SkipFade) { Finish("skipped"); return; }
            }
            else if (_t < FadeIn)
            {
                alpha = _t / FadeIn;
            }
            else if (_t < FadeIn + _hold)
            {
                alpha = 1f;
            }
            else if (_t < FadeIn + _hold + FadeOut)
            {
                alpha = 1f - (_t - FadeIn - _hold) / FadeOut;
            }
            else
            {
                Finish("timed out");
                return;
            }

            _group.alpha = Mathf.Clamp01(alpha);
        }

        private void Finish(string how)
        {
            Plugin.Log.LogInfo("Splash: dismissed (" + how + ").");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// First appearance of the title screen == the moment the game has finished its own splash sequence and
    /// loaded. Same hook the Mods button uses (StartGameFE.MainScreen.OnSetFocus, verified against the real
    /// Assembly-CSharp; it fires on every return to the title, so ModSplash keeps its own once-guard).
    /// </summary>
    [HarmonyPatch(typeof(StartGameFE.MainScreen), "OnSetFocus")]
    internal static class MainScreen_Splash_Patch
    {
        private static void Postfix()
        {
            try { ModSplash.ShowOnce(); }
            catch (Exception e) { Plugin.Log.LogError("Splash: " + e); }
        }
    }
}
