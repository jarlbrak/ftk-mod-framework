using System;
using System.Collections.Generic;
using FTKModFramework.Core.Marketplace;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core.UI
{
    internal sealed partial class ModsPanel
    {
        private FrameworkRelease _frameworkRelease;
        private bool _updateReview;
        private string _reviewMode;
        private string _reviewTag;
        private long _reviewReleaseId;

        private void LoadUpdates(string operation)
        {
            if (PanelBusy) return;
            _updateReview = false;
            FrameworkUpdateRuntime.Start(operation, null, null, delegate(FrameworkUpdateResult result) {
                if (result.Ok)
                {
                    _frameworkRelease = result.Stable ?? result.Preview;
                    _detailPage = 0;
                }
                Refresh();
            });
            Refresh();
        }

        private void BuildUpdates()
        {
            Transform left;
            Transform right;
            CreateColumns(out left, out right);
            _container = left;
            FrameworkUpdateResult state = FrameworkUpdateRuntime.State;
            Transform heading = HorizontalRow("Update heading", 44);
            _container = heading;
            Text title = TextLine("Framework updates", 30, 44);
            title.GetComponent<LayoutElement>().minWidth = title.GetComponent<LayoutElement>().preferredWidth = 540;
            Button refresh = UpdateLink("Refresh", delegate { LoadUpdates("refresh"); }, !PanelBusy);
            SetWidth(refresh.gameObject, 170);
            _container = left;
            TextLine("Installed v" + Plugin.Version + (state == null || !FrameworkUpdateRuntime.SelectionKnown ? " / Saved choice not confirmed" : " / Saved: " + UpdateModeName(state.SelectedMode, state.SelectedTag)), 21, 52);
            TextLine(UpdateCacheStatus(state), 19, 36);
            UpdateChoice("Follow Stable", state == null ? "Checking stable releases..." : state.Stable == null ? "No stable release published yet" : state.Stable.Tag + " / Automatic stable releases", state != null && state.SelectedMode == "stable",
                delegate { ReviewUpdate("stable", state == null ? null : state.Stable); }, state != null && FrameworkUpdateRuntime.SelectionKnown && !PanelBusy);
            UpdateChoice("Follow Preview", state == null ? "Checking releases..." : state.Preview == null ? "No release published yet" : state.Preview.Tag + " / Newest release, including previews", state != null && state.SelectedMode == "preview",
                delegate { ReviewUpdate("preview", state == null ? null : state.Preview); }, state != null && FrameworkUpdateRuntime.SelectionKnown && !PanelBusy);
            TextLine("Or choose a version", 28, 36).color = Gold;
            List<FrameworkRelease> releases = state == null || state.Releases == null ? new List<FrameworkRelease>() : state.Releases;
            int pages = Math.Max(1, (releases.Count + 1) / 2);
            _page = Math.Min(_page, pages - 1);
            if (releases.Count == 0) TextLine(FrameworkUpdateRuntime.Busy ? "Checking available versions..." : "No release history is available yet. Refresh when you are online.", 22, 96);
            for (int i = _page * 2; i < Math.Min(releases.Count, (_page + 1) * 2); i++)
            {
                FrameworkRelease release = releases[i];
                bool pinned = state.SelectedMode == "pinned" && state.SelectedTag == release.Tag;
                string description = (release.Prerelease ? "Preview" : "Stable") + " / " + ReleaseDate(release) + (pinned ? " / PINNED" : "");
                UpdateChoice(release.Tag, description, _frameworkRelease != null && _frameworkRelease.Tag == release.Tag,
                    delegate { _frameworkRelease = release; _updateReview = false; _detailPage = 0; Refresh(); }, true);
            }
            PageButtons(pages);
            if (state != null && state.MoreAvailable) TextLine("Showing the " + releases.Count + " most recent releases.", 19, 30);
            _container = right;
            if (_updateReview) UpdateReview(state);
            else if (_frameworkRelease != null) UpdateReleaseDetails(state);
            else UpdateIntroduction(state);
            _container = _rootContent;
        }

        private void UpdateChoice(string title, string description, bool selected, Action action, bool enabled)
        {
            Button button = ActionButton(title, action, enabled, 64);
            Border(button.gameObject, selected ? Gold : WarmBorder, selected ? 2 : 1);
            Text heading = button.GetComponentInChildren<Text>();
            heading.fontSize = 23;
            TopBox(heading.rectTransform, 8, 37, 18);
            GameObject summary = NewChild("Summary", button.transform);
            Text text = summary.AddComponent<Text>();
            StyleText(text, description, 18);
            TopBox(text.rectTransform, 40, 63, 18);
        }

        private void UpdateIntroduction(FrameworkUpdateResult state)
        {
            TextLine("Your next adventure, up to date", 34, 100);
            TextLine("Follow an automatic release channel, or keep a particular version. Nothing is installed while you are playing.", 24, 140);
            if (state != null) TextLine("Saved choice: " + UpdateModeName(state.SelectedMode, state.SelectedTag), 23, 70);
            LauncherUpdateNotice(state);
            TextLine(Short(FrameworkUpdateRuntime.Notice, 240), 22, 130);
        }

        private void UpdateReleaseDetails(FrameworkUpdateResult state)
        {
            FrameworkRelease release = _frameworkRelease;
            TextLine("Framework " + release.Tag, 34, 50);
            TextLine((release.Prerelease ? "Preview release" : "Stable release") + " / " + ReleaseDate(release), 22, 26);
            LauncherUpdateNotice(state);
            List<string> notes = TextPages(new List<string> { string.IsNullOrEmpty(release.Notes) ? "No release notes were provided." : release.Notes });
            _detailPage = Math.Min(_detailPage, notes.Count - 1);
            TextLine(notes[_detailPage], 21, 196);
            if (notes.Count > 1) UpdateLink("Next notes page (" + (_detailPage + 1) + " of " + notes.Count + ")", delegate { _detailPage = (_detailPage + 1) % notes.Count; Refresh(); });
            if (!release.Available) TextLine(Short(string.IsNullOrEmpty(release.Reason) ? "This release is not available for this installation." : release.Reason, 115), 21, 52).color = Gold;
            bool alreadyPinned = state != null && state.SelectedMode == "pinned" && state.SelectedTag == release.Tag;
            PrimaryButton(alreadyPinned ? "Pinned for next launch" : "Pin " + release.Tag + "...", delegate { ReviewUpdate("pinned", release); },
                !alreadyPinned && release.Available && FrameworkUpdateRuntime.SelectionKnown && !PanelBusy);
            UpdateLink("Read release on GitHub", delegate { Application.OpenURL("https://github.com/jarlbrak/ftk-mod-framework/releases/tag/" + release.Tag); });
        }

        private void ReviewUpdate(string mode, FrameworkRelease release)
        {
            _reviewMode = mode;
            _reviewTag = mode == "pinned" && release != null ? release.Tag : null;
            _reviewReleaseId = mode == "pinned" && release != null ? release.ReleaseId : 0;
            _frameworkRelease = release;
            _updateReview = true;
            Refresh();
        }

        private void UpdateReview(FrameworkUpdateResult state)
        {
            TextLine("Review update choice", 34, 50);
            string target = FrameworkUpdatePresentation.EffectiveTarget(_reviewMode, _frameworkRelease, Plugin.Version);
            TextLine("Now: v" + Plugin.Version + "\nNext launch: " + target, 24, 88);
            TextLine(_reviewMode == "pinned" ? "Keep this exact version until you change your choice." : _reviewMode == "preview" ?
                "Follow the newest release, including previews. A newer stable release can also be selected." : "Follow stable releases automatically. Preview releases are skipped.", 22, 60);
            bool older = FrameworkUpdatePresentation.IsDowngrade(_reviewMode, _frameworkRelease, Plugin.Version);
            TextLine(older ? "An older framework can affect your mods and saves. Manually installed mods are not fully verified."
                : "Your current session stays unchanged. The launcher verifies this choice against managed mods before applying it.", 22, 60);
            if (older) TextLine("Older releases may not have this picker. Use Restore bundled in the launcher to return.", 21, 54).color = Gold;
            LauncherUpdateNotice(state);
            PrimaryButton("Save for next launch", delegate {
                FrameworkUpdateRuntime.Start("select", _reviewMode, _reviewTag, delegate(FrameworkUpdateResult result) {
                    if (result.Ok) _updateReview = false;
                    Refresh();
                }, _reviewReleaseId);
                Refresh();
            }, !PanelBusy && FrameworkUpdateRuntime.SelectionKnown);
            UpdateLink("Back to version details", delegate { _updateReview = false; Refresh(); });
        }

        private void LauncherUpdateNotice(FrameworkUpdateResult state)
        {
            if (state != null && state.LauncherCompatible) return;
            TextLine("Replace your old launcher files or point Steam at the new launcher. Version choices require that launcher.", 21, 52).color = Gold;
            UpdateLink("Get the latest launcher", delegate { Application.OpenURL("https://github.com/jarlbrak/ftk-mod-framework/releases"); });
        }

        private static string UpdateCacheStatus(FrameworkUpdateResult state)
        {
            if (FrameworkUpdateRuntime.Busy) return "Checking update preferences and release history...";
            if (FrameworkUpdateRuntime.LastStatus == "error") return "Refresh unavailable / showing the last saved information";
            if (state == null || state.CacheAgeSeconds < 0) return "No cached release list";
            return (state.Status == "offline" ? "Offline / cached " : "Release list saved ") + FriendlyAge(state.CacheAgeSeconds) + " ago";
        }

        private Button UpdateLink(string title, Action action, bool enabled = true)
        {
            Button button = LinkButton(title, action, enabled);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 32;
            return button;
        }

        private static string ReleaseDate(FrameworkRelease release)
        {
            return string.IsNullOrEmpty(release.PublishedAt) ? "Date not provided" : release.PublishedAt.Length >= 10 ? release.PublishedAt.Substring(0, 10) : release.PublishedAt;
        }
        private static string UpdateModeName(string mode, string tag) { return mode == "pinned" ? "Pinned " + tag : mode == "preview" ? "Follow Preview" : "Follow Stable"; }
    }
}
