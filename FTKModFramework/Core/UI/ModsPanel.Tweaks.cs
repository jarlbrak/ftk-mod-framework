using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core.UI
{
    internal sealed partial class ModsPanel
    {
        /// <summary>The Tweaks tab. Text, heights and paging come from ModsPanelTweaks, which the
        /// game-free harness covers; this method only lays them out with the panel's controls, so
        /// mouse, keyboard and controller share the FTKSelectable path of every other view.</summary>
        private void TweaksView()
        {
            TweakRegistry registry = Tweaks.Registry;
            string status = ModsPanelTweaks.Status(registry.IsInitialized, registry.Count);
            Transform body = _container;
            _container = HorizontalRow("Tweaks header", 46);
            Text intro = TextLine(ModsPanelTweaks.Intro, 22, 46);
            intro.alignment = TextAnchor.MiddleLeft;
            intro.GetComponent<LayoutElement>().flexibleWidth = 1;
            if (status == null)
            {
                Button reset = ActionButton(ModsPanelTweaks.ResetCaption, delegate {
                    _message = registry.ResetAll() ? ModsPanelTweaks.ResetDone : ModsPanelTweaks.SaveFailed;
                    Refresh();
                }, true, 44);
                SetWidth(reset.gameObject, 290);
            }
            _container = body;
            if (status != null)
            {
                Spacer(24);
                TextLine(status, 24, 110).color = Gold;
                return;
            }
            List<List<ModsPanelTweaks.Item>> pages = ModsPanelTweaks.Pages(registry, ModsPanelTweaks.PageBudget);
            _page = Math.Max(0, Math.Min(_page, pages.Count - 1));
            foreach (ModsPanelTweaks.Item item in pages[_page])
            {
                if (item.Heading != null) TextLine(item.Heading, 26, ModsPanelTweaks.HeadingHeight).color = Gold;
                else TweakRow(registry, item.Row);
            }
            PageButtons(pages.Count);
        }

        private void TweakRow(TweakRegistry registry, ModsPanelTweaks.Row row)
        {
            Transform body = _container;
            GameObject block = NewChild("Tweak", body);
            Height(block, row.Height);
            VerticalLayoutGroup layout = block.AddComponent<VerticalLayoutGroup>();
            layout.spacing = ModsPanelTweaks.LineGap;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _container = block.transform;
            int handle = row.Handle;
            ActionButton(row.Caption, delegate {
                // The registry writes through the config store, which saves the file immediately.
                _message = registry.Toggle(handle) ? "" : ModsPanelTweaks.SaveFailed;
                Refresh();
            }, true, ModsPanelTweaks.ButtonHeight);
            foreach (ModsPanelTweaks.Line line in row.Lines)
            {
                int size = line.Height >= ModsPanelTweaks.TallLine ? 20 : 19;
                Text text = TextLine(line.Text, size, line.Height);
                // Text truncates rather than scrolls; shrink a long line instead of cutting it off.
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 14;
                text.resizeTextMaxSize = size;
                if (line.Warning) text.color = Gold;
            }
            _container = body;
        }
    }
}
