using System;
using System.Collections.Generic;
using System.Text;
using FTKModFramework.Core.Reporting;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FTKModFramework.Core.UI
{
    // A single explicit send action authorizes the frozen description and selected diagnostics.
    internal sealed class ReportingPanel : FTKInputFocus
    {
        internal ReportingReport Report { get; private set; }
        internal bool IsOffer { get { return kind != "manual"; } }
        internal bool Submitted { get { return submitted; } }
        internal bool HasUnfinishedSubmission { get { return sending || storing || (frozen != null && !submitted); } }
        private uiOptionsMenu owner;
        private Text heading, status, preview, pageLabel, disclosure;
        private Button primary, secondary, metadataButton, metadataDetailsButton, previous, next, back, saveDraft, manageDrafts;
        private readonly Button[] draftOpen = new Button[5], draftDelete = new Button[5];
        private readonly Text[] draftLabels = new Text[5];
        private ReportingDraft[] listedDrafts = new ReportingDraft[0];
        private enum View { Editor, Drafts, Leave, Delete }
        private View view;
        private int draftPage;
        private bool dirty, storing;
        private string deletingId;
        private string editorHeading, editorStatus;
        private string cachedPendingPayload, cachedPendingId;
        private Action leaveAction;
        private FTKInputFieldSelectable narrative;
        private GameObject narrativeRoot, previewSurface;
        private readonly List<string> pages = new List<string>();
        private int page;
        private bool editing, expanded, sending, submitted, valid = true;
        private string kind = "manual", description = "", currentLogs, previousLogs, frozen, issueUrl, errorId, priorId;
        private string deferredPayload, deferredErrorId, deferredPriorId;
        private new void Awake() { m_IsOptionSubMenu = true; m_Cancel = Back; }
        public override void OnPreSetFocus() { gameObject.SetActive(true); Refresh(); base.OnPreSetFocus(); }
        public override void OnClose()
        {
            FinishTyping(); gameObject.SetActive(false);
            if (owner && owner.m_MainOptions && owner.m_MainOptions.m_SubBlocker) owner.m_MainOptions.m_SubBlocker.gameObject.SetActive(false);
            base.OnClose();
        }
        internal void BeginReport(ReportingReport report)
        {
            if (submitted && RestoreDeferred()) return;
            view = View.Editor; dirty = false; leaveAction = null;
            FinishTyping(); Report = report; description = ""; frozen = null; issueUrl = null; errorId = null; priorId = report.PreviousSessionId;
            kind = priorId == null ? "manual" : "unexpected_exit"; sending = submitted = false; valid = true;
            currentLogs = ReportingDiagnostics.CaptureCurrent(); previousLogs = ReportingDiagnostics.CapturePrevious(priorId);
            narrative.text = "";
            heading.text = "Report a problem";
            status.text = "What happened? A short description helps. Diagnostics are included automatically when you send.";
            RestorePending(); dirty = false; SetExpanded(false); Refresh();
        }
        internal void BeginSavedDraft(ReportingDraft saved)
        {
            BeginReport(saved.Report);
            if (frozen == null)
            {
                // Reopening preserves this capture rather than attaching a later launch's errors.
                currentLogs = saved.CurrentLogs; previousLogs = saved.PreviousLogs;
                kind = saved.Kind; errorId = saved.ErrorId;
                description = saved.Description;
                try
                {
                    if (description.StartsWith("FTKREPORT1|", StringComparison.Ordinal))
                    {
                        ReportingIssueNarrative old = ReportingIssueNarrative.Restore(description);
                        description = old.Summary + "\n" + old.Reproduction + "\n" + old.ExpectedActual;
                    }
                }
                catch { description = saved.Description; }
                narrative.text = description; valid = description.Length <= 4000;
                status.text = "Saved draft restored. Check what you want to share, then send.";
                dirty = false;
            }
            Refresh();
        }
        private void RestorePending()
        {
            if (!ReportingSubmission.Ready) return;
            string pending = ReportingSubmission.PendingPayload;
            if (pending == null || frozen != null || sending || submitted) return;
            try
            {
                JObject value = JObject.Parse(pending);
                frozen = pending; description = (string)value["description"] ?? ""; kind = (string)value["kind"] ?? "manual";
                errorId = null;
                Report.IncludeMetadata = (bool?)value["includeDiagnostics"] ?? false;
                narrative.text = description;
                priorId = (string)value["diagnostics"]?["previousSession"]?["sessionId"];
                status.text = "A report is waiting for confirmation. Retry sends the same saved report safely; it does not create a new report.";
                if (expanded) ShowPreview();
            }
            catch { status.text = "The saved outgoing report could not be read. It remains on this computer."; }
        }
        internal void ObserveDraftReadiness() { RestorePending(); Refresh(); }
        private void Refresh()
        {
            if (!primary || Report == null) return;
            bool editor = view == View.Editor;
            bool idle = !sending && !storing && !ReportingRuntime.DraftsBusy && !ReportingSubmission.Busy;
            primary.interactable = idle && ReportingSubmission.Ready && valid;
            SetLabel(primary, submitted ? "View issue" : frozen != null ? "Retry report" : "Send report");
            SetLabel(secondary, frozen != null && !submitted ? "Discard local copy" : deferredPayload != null ? "Return to your report" : submitted ? "Close" : IsOffer ? "Not now" : "Close");
            secondary.interactable = idle;
            back.interactable = !storing;
            metadataButton.interactable = ReportingSubmission.Ready && frozen == null && !sending && !submitted;
            SetLabel(metadataButton, Report.IncludeMetadata ? "Include diagnostics: yes" : "Include diagnostics: no");
            narrativeRoot.SetActive(editor && !storing && ReportingSubmission.Ready && frozen == null && !submitted);
            narrative.m_ButtonText.text = "Edit";
            SetLabel(metadataDetailsButton, expanded ? "Hide details" : "What will be sent?");
            metadataButton.gameObject.SetActive(editor); metadataDetailsButton.gameObject.SetActive(editor);
            saveDraft.gameObject.SetActive(editor && frozen == null && !submitted);
            manageDrafts.gameObject.SetActive(editor);
            saveDraft.interactable = idle && ReportingRuntime.DraftsReady && valid;
            manageDrafts.interactable = idle && ReportingRuntime.DraftsReady;
            SetLabel(manageDrafts, "Drafts (" + ReportingRuntime.SavedDrafts.Length + ")");
            previewSurface.SetActive(editor && expanded); preview.gameObject.SetActive(editor && expanded);
            previous.gameObject.SetActive((editor && expanded) || view == View.Drafts);
            next.gameObject.SetActive((editor && expanded) || view == View.Drafts);
            pageLabel.gameObject.SetActive((editor && expanded) || view == View.Drafts);
            Place(previous.GetComponent<RectTransform>(), -465, editor ? -183 : -245, 230, 48);
            Place(next.GetComponent<RectTransform>(), 465, editor ? -183 : -245, 230, 48);
            Place(pageLabel.GetComponent<RectTransform>(), 0, editor ? -183 : -245, 200, 28);
            disclosure.gameObject.SetActive(editor);
            for (int i = 0; i < draftOpen.Length; i++)
            {
                bool shown = view == View.Drafts && draftPage * 5 + i < listedDrafts.Length;
                draftLabels[i].gameObject.SetActive(shown); draftOpen[i].gameObject.SetActive(shown); draftDelete[i].gameObject.SetActive(shown);
                draftOpen[i].interactable = draftDelete[i].interactable = idle;
                if (shown && listedDrafts[draftPage * 5 + i].Report.ReportId == PendingId()) draftDelete[i].interactable = false;
            }
            if (!editor)
            {
                primary.interactable = secondary.interactable = idle && ReportingRuntime.DraftsReady;
                if (view == View.Drafts)
                {
                    SetLabel(primary, "New report"); SetLabel(secondary, "Back to game"); SetLabel(back, "Back to report");
                    primary.interactable &= ReportingSubmission.PendingPayload == null;
                    pageLabel.text = (draftPage + 1) + " / " + Math.Max(1, (listedDrafts.Length + 4) / 5);
                    previous.interactable = idle && draftPage > 0; next.interactable = idle && (draftPage + 1) * 5 < listedDrafts.Length;
                }
                else if (view == View.Leave)
                { SetLabel(primary, "Save and continue"); SetLabel(secondary, "Discard edits"); SetLabel(back, "Keep editing"); primary.interactable &= valid; }
                else
                { SetLabel(primary, "Delete draft"); SetLabel(secondary, "Keep draft"); SetLabel(back, "Back to drafts"); }
            }
            else SetLabel(back, "Back to game");
            SetupOwnedNavigation();
        }
        private void Primary()
        {
            if (storing || ReportingRuntime.DraftsBusy) return;
            if (view == View.Drafts) { BeginReport(ReportingRuntime.CreateReport(false)); return; }
            if (view == View.Leave) { SaveCurrent(ContinueLeaving); return; }
            if (view == View.Delete) { DeleteSelectedDraft(); return; }
            if (submitted) { if (!String.IsNullOrEmpty(issueUrl)) Application.OpenURL(issueUrl); return; }
            if (sending || ReportingSubmission.Busy) return;
            FinishTyping();
            try
            {
                if (frozen == null) frozen = ReportingSubmissionPayload.Create(Report, description, kind, currentLogs, previousLogs);
            }
            catch (Exception) { status.text = "The report is too large. Shorten the description or exclude diagnostics."; return; }
            sending = true; status.text = "Sending report and selected diagnostics... You can return to the game."; Refresh();
            string sentErrorId = errorId, sentPriorId = priorId, sentPayload = frozen;
            if (!ReportingSubmission.Start(frozen, delegate(ReportingSubmissionResult result) {
                // Bookkeeping does not depend on a scene-owned panel surviving the response.
                if (result.Success)
                {
                    ReportingRuntime.DeleteDraft(result.ReportId, null);
                    if (sentErrorId != null) ReportingDiagnostics.Acknowledge(sentErrorId);
                    ReportingIncident pending = ReportingRuntime.Pending;
                    if (sentPriorId != null && pending != null && pending.SessionId == sentPriorId) ReportingRuntime.DismissPending(null);
                }
                if (!this || frozen != sentPayload) return;
                sending = false;
                if (result.Success)
                {
                    submitted = true; issueUrl = result.IssueUrl;
                    status.text = "Report #" + result.IssueNumber + " sent. Thank you. " +
                        (Report.IncludeMetadata ? "Your selected diagnostics were included automatically." : "No diagnostics were included.");
                }
                else if (result.Error == "pending_report_exists")
                {
                    if (deferredPayload == null)
                    { deferredPayload = frozen; deferredErrorId = errorId; deferredPriorId = priorId; }
                    frozen = null; RestorePending();
                    status.text = "An earlier report needs confirmation first. Retry it or discard its local copy. Your new report is kept here for afterwards.";
                }
                else status.text = FailureMessage(result.Error);
                Refresh();
            }))
            { sending = false; status.text = "Reporting is still getting ready. Please try again in a moment."; Refresh(); }
        }
        private static string FailureMessage(string error)
        {
            if (error == "rate_limited") return "Too many reports right now. Your report is saved; try again later.";
            if (error == "service_unavailable" || error == "capacity_reached") return "The reporting service is unavailable. Your report is saved; try again later.";
            if (error == "pending_report_exists") return "Another saved report needs confirmation first. Close and reopen Report Bugs to retry it.";
            if (error == "helper_unavailable") return "The reporting helper needs updating. Your report is saved locally.";
            if (error == "local_delivery_failed") return "The report could not be sent or saved. Your text remains here; try again.";
            return "Submission is not confirmed. Retry sends this same report safely. A local copy is kept for seven days.";
        }
        private void Secondary()
        {
            if (sending || storing) return;
            if (view == View.Drafts) { ShowEditor(); Close(); return; }
            if (view == View.Delete) { ShowDrafts(); return; }
            if (view == View.Leave) { DiscardEdits(); ContinueLeaving(); return; }
            if (submitted && RestoreDeferred()) return;
            if (frozen != null && !submitted)
            {
                sending = true; Refresh();
                ReportingSubmission.DiscardPending(frozen, delegate(bool success) {
                    if (!this) return; sending = false;
                    if (success)
                    {
                        ReportingRuntime.DeleteDraft(PayloadId(frozen), null);
                        frozen = null; if (!RestoreDeferred()) BeginReport(ReportingRuntime.CreateReport(false));
                        status.text = "Local copy discarded. This does not delete a report that already reached GitHub.";
                    }
                    else status.text = "The local copy could not be removed. Try again.";
                    Refresh();
                });
                return;
            }
            LeaveOffer();
        }
        private void LeaveOffer()
        {
            string dismissedErrorId = errorId;
            string dismissedSessionId = kind == "unexpected_exit" ? priorId : null;
            RequestLeave(delegate {
                if (dismissedErrorId != null) ReportingDiagnostics.Acknowledge(dismissedErrorId);
                ReportingIncident pending = ReportingRuntime.Pending;
                if (dismissedSessionId != null && pending != null && pending.SessionId == dismissedSessionId)
                    ReportingRuntime.DismissPending(null);
                Report = null; Close();
            });
        }
        private bool RestoreDeferred()
        {
            if (deferredPayload == null) return false;
            JObject value = JObject.Parse(deferredPayload);
            frozen = deferredPayload; errorId = deferredErrorId; priorId = deferredPriorId;
            deferredPayload = deferredErrorId = deferredPriorId = null;
            description = (string)value["description"] ?? ""; kind = (string)value["kind"] ?? "manual";
            Report.IncludeMetadata = (bool?)value["includeDiagnostics"] ?? false;
            submitted = false; issueUrl = null;
            status.text = "Your new report is ready. Send it when you are ready.";
            Refresh(); if (expanded) ShowPreview(); return true;
        }
        private static string PayloadId(string payload)
        { try { return payload == null ? null : (string)JObject.Parse(payload)["reportId"]; } catch { return null; } }
        private string PendingId()
        {
            string payload = ReportingSubmission.PendingPayload;
            if (!System.Object.ReferenceEquals(payload, cachedPendingPayload))
            { cachedPendingPayload = payload; cachedPendingId = PayloadId(payload); }
            return cachedPendingId;
        }
        private static ReportingDraft FindDraft(string id)
        {
            foreach (ReportingDraft draft in ReportingRuntime.SavedDrafts) if (draft.Report.ReportId == id) return draft;
            return null;
        }
        private void SaveCurrent(Action completed)
        {
            if (storing || sending || frozen != null || submitted || !valid || ReportingRuntime.DraftsBusy) return;
            FinishTyping();
            ReportingDraft snapshot;
            try { snapshot = ReportingDraft.Create(Report, description, DateTime.UtcNow, kind, currentLogs, previousLogs, errorId); }
            catch { status.text = "This draft could not be saved. Shorten the description and try again."; return; }
            string expectedId = Report.ReportId;
            storing = true; Refresh();
            ReportingRuntime.SaveDraft(snapshot, delegate(bool success) {
                if (!this) return;
                storing = false;
                if (Report == null || Report.ReportId != expectedId) return;
                if (success)
                {
                    dirty = false; editorStatus = "Draft saved on this computer. Nothing was uploaded.";
                    status.text = editorStatus;
                    if (completed != null) completed();
                }
                else status.text = "Could not save the draft. Delete an older draft or check local storage, then try again.";
                Refresh();
            });
        }
        private void RequestLeave(Action action)
        {
            if (storing || sending) return;
            FinishTyping();
            if (!dirty || frozen != null || submitted) { action(); return; }
            RememberEditor(); leaveAction = action; view = View.Leave;
            heading.text = "Save this draft?";
            status.text = "You have unsaved edits. Save them on this computer, discard the edits, or keep editing.";
            Refresh();
        }
        private void ContinueLeaving()
        {
            Action action = leaveAction; leaveAction = null; ShowEditor();
            if (action != null) action();
        }
        private void DiscardEdits()
        {
            Action action = leaveAction;
            ReportingDraft saved = FindDraft(Report.ReportId);
            if (saved == null) BeginReport(ReportingRuntime.CreateReport(false)); else BeginSavedDraft(saved);
            editorHeading = heading.text; editorStatus = status.text; leaveAction = action;
        }
        private void RememberEditor()
        { if (view == View.Editor) { editorHeading = heading.text; editorStatus = status.text; } }
        private void ShowEditor()
        {
            view = View.Editor; heading.text = editorHeading ?? "Report a problem";
            status.text = editorStatus ?? "What happened? A short description helps.";
            Refresh();
        }
        private void ShowDrafts()
        {
            FinishTyping(); RememberEditor(); view = View.Drafts;
            listedDrafts = ReportingRuntime.SavedDrafts;
            draftPage = Math.Max(0, Math.Min(draftPage, (Math.Max(1, listedDrafts.Length) - 1) / 5));
            heading.text = "Bug report drafts";
            status.text = listedDrafts.Length == 0 ? "No saved drafts. Drafts stay on this computer for seven days; saving never uploads them." :
                "Open a draft to edit or send it, or delete it here. Up to 10 drafts are kept on this computer for seven days.";
            for (int i = 0; i < draftLabels.Length && draftPage * 5 + i < listedDrafts.Length; i++)
            {
                ReportingDraft draft = listedDrafts[draftPage * 5 + i];
                string title = draft.Description.Replace('\r', ' ').Replace('\n', ' ').Trim();
                if (title.Length == 0) title = "No description";
                if (title.Length > 95) title = title.Substring(0, 95) + "...";
                draftLabels[i].text = draft.SavedAtUtc.ToLocalTime().ToString("g") + " | Diagnostics " +
                    (draft.Report.IncludeMetadata ? "on" : "off") + "\n" + title;
                SetLabel(draftOpen[i], draft.Report.ReportId == PendingId() ? "Review pending" : "Open");
            }
            Refresh();
        }
        private void OpenDraft(int row)
        {
            int index = draftPage * 5 + row;
            if (view != View.Drafts || storing || index >= listedDrafts.Length) return;
            ReportingDraft draft = FindDraft(listedDrafts[index].Report.ReportId);
            if (draft == null) { ShowDrafts(); return; }
            string pending = PendingId();
            if (pending != null && pending != draft.Report.ReportId)
            { status.text = "A report is waiting for confirmation. Return to the report to retry it or discard its local copy before editing another draft."; return; }
            BeginSavedDraft(draft);
        }
        private void ConfirmDelete(int row)
        {
            int index = draftPage * 5 + row;
            if (view != View.Drafts || storing || index >= listedDrafts.Length) return;
            deletingId = listedDrafts[index].Report.ReportId;
            if (deletingId == PendingId()) return;
            view = View.Delete; heading.text = "Delete this draft?";
            status.text = "This removes the saved draft from this computer. It does not delete a report already posted to GitHub.";
            Refresh();
        }
        private void DeleteSelectedDraft()
        {
            string expectedId = deletingId;
            if (expectedId == null || expectedId == PendingId()) return;
            storing = true; Refresh();
            ReportingRuntime.DeleteDraft(expectedId, delegate(bool success) {
                if (!this) return;
                storing = false;
                if (success)
                {
                    if (Report != null && Report.ReportId == expectedId) BeginReport(ReportingRuntime.CreateReport(false));
                    ShowDrafts(); status.text = "Draft deleted from this computer.";
                }
                else status.text = "The draft could not be deleted. It is still saved; try again.";
                Refresh();
            });
        }
        private void Back()
        {
            if (storing) return;
            if (view == View.Delete) { ShowDrafts(); return; }
            if (view != View.Editor) { leaveAction = null; ShowEditor(); return; }
            if (sending) { Close(); return; }
            if (IsOffer && frozen == null && !submitted) LeaveOffer();
            else RequestLeave(Close);
        }
        private void ChangePage(int delta)
        {
            if (view == View.Drafts) { draftPage += delta; ShowDrafts(); }
            else if (page + delta >= 0 && page + delta < pages.Count) { page += delta; DisplayPage(); }
        }
        private void ToggleMetadata() { if (frozen != null || sending || storing) return; Report.IncludeMetadata = !Report.IncludeMetadata; dirty = true; Refresh(); if (expanded) ShowPreview(); }
        private void ToggleDetails() { SetExpanded(!expanded); if (expanded) ShowPreview(); }
        private void ShowPreview()
        {
            string content;
            try { content = frozen ?? ReportingSubmissionPayload.Create(Report, description, kind, currentLogs, previousLogs); }
            catch { content = "The description is too long to prepare."; }
            SetPages(ReportingSubmissionPayload.Disclosure + "\n\n" + content);
        }
        private void SetExpanded(bool value)
        {
            expanded = value; previewSurface.SetActive(value); preview.gameObject.SetActive(value);
            previous.gameObject.SetActive(value); next.gameObject.SetActive(value); pageLabel.gameObject.SetActive(value); Refresh();
        }
        private void SetPages(string text)
        {
            pages.Clear(); page = 0; StringBuilder chunk = new StringBuilder(); int columns = 0, lines = 0;
            foreach (char c in text)
            {
                chunk.Append(c);
                if (c == '\n') { columns = 0; lines++; }
                else if (++columns >= 88 && !Char.IsHighSurrogate(c)) { chunk.Append('\n'); columns = 0; lines++; }
                if (lines >= 6) { pages.Add(chunk.ToString()); chunk.Length = 0; lines = columns = 0; }
            }
            if (chunk.Length != 0 || pages.Count == 0) pages.Add(chunk.ToString()); DisplayPage();
        }
        private void DisplayPage() { preview.text = pages[page]; pageLabel.text = (page + 1) + " / " + pages.Count; previous.interactable = page > 0; next.interactable = page + 1 < pages.Count; SetupOwnedNavigation(); }
        private void FinishTyping()
        {
            if (!editing || !narrative) return;
            narrative.m_InputField.DeactivateInputField();
            if (editing) narrative.OnEditFinished(narrative.text);
            editing = false;
        }
        private void SetupOwnedNavigation()
        {
            List<Selectable> controls = new List<Selectable>();
            if (narrativeRoot && narrativeRoot.activeSelf) controls.Add(narrative.m_TextButton.m_UnitySelectable);
            if (metadataButton) controls.Add(metadataButton);
            if (metadataDetailsButton) controls.Add(metadataDetailsButton);
            if (saveDraft) controls.Add(saveDraft);
            if (manageDrafts) controls.Add(manageDrafts);
            if (view == View.Drafts) for (int i = 0; i < draftOpen.Length; i++) { controls.Add(draftOpen[i]); controls.Add(draftDelete[i]); }
            if (expanded || view == View.Drafts) { controls.Add(previous); controls.Add(next); }
            controls.Add(primary); controls.Add(secondary); controls.Add(back);
            controls.RemoveAll(delegate(Selectable x) { return !x || !x.interactable || !x.gameObject.activeInHierarchy; });
            for (int i = 0; i < controls.Count; i++)
            {
                Navigation n = new Navigation(); n.mode = Navigation.Mode.Explicit;
                n.selectOnUp = n.selectOnLeft = controls[(i + controls.Count - 1) % controls.Count];
                n.selectOnDown = n.selectOnRight = controls[(i + 1) % controls.Count]; controls[i].navigation = n;
            }
        }
        private static void SetLabel(Button button, string value) { button.GetComponentInChildren<Text>(true).text = value; }
        private static void Rewire(Button button, UnityAction action)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++) button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(action);
        }
        private Button AddButton(Button source, string name, string label, float x, float y, float width, UnityAction action)
        {
            Button button = Instantiate(source, transform); button.name = name;
            Place(button.GetComponent<RectTransform>(), x, y, width, 48);
            foreach (Text text in button.GetComponentsInChildren<Text>(true)) { text.supportRichText = false; text.fontSize = 18; }
            SetLabel(button, label); Rewire(button, action);
            FTKSelectable selectable = button.GetComponent<FTKSelectable>();
            selectable.m_InputFocus = this; selectable.m_UnitySelectable = button;
            selectable.m_OnSelect = selectable.m_OnDeselect = selectable.m_OnRightClick = null;
            ModsPanel.StyleNativeButton(button, false);
            button.gameObject.SetActive(true); return button;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, y);
        }
        private Text AddText(Font font, string name, float y, float height, int size, TextAnchor alignment, float width = 740)
        {
            GameObject node = new GameObject(name, typeof(RectTransform), typeof(Text)); node.transform.SetParent(transform, false);
            Place(node.GetComponent<RectTransform>(), 0, y, width, height);
            Text text = node.GetComponent<Text>(); text.font = font; text.fontSize = size;
            text.supportRichText = false; text.alignment = alignment; text.color = Color.white; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            ModsPanel.StyleNativeText(text, false);
            return text;
        }
        internal static ReportingPanel Create(uiOptionsMenu owner, Button source)
        {
            GameObject root = new GameObject("FrameworkReportingPanel", typeof(RectTransform), typeof(Image)); root.SetActive(false);
            try
            {
                root.transform.SetParent(owner.m_AudioOptions.transform.parent, false);
                Place(root.GetComponent<RectTransform>(), 0, 0, 1540, 1000);
                ModsPanel.CaptureNativeSkin();
                ModsPanel.StyleNativePanel(root, false);
                root.transform.SetAsLastSibling();
                // Native options controls have their own canvases. Own a canvas above those
                // controls so their captions cannot draw over the reporting preview.
                Canvas parentCanvas = owner.GetComponentInParent<Canvas>();
                int order = parentCanvas ? parentCanvas.sortingOrder : 0;
                foreach (Canvas nativeCanvas in owner.GetComponentsInChildren<Canvas>(true))
                    order = Math.Max(order, nativeCanvas.sortingOrder);
                if (order >= 32767) throw new InvalidOperationException("No reporting canvas order available.");
                Canvas reportingCanvas = root.AddComponent<Canvas>();
                reportingCanvas.overrideSorting = true;
                if (parentCanvas) reportingCanvas.sortingLayerID = parentCanvas.sortingLayerID;
                reportingCanvas.sortingOrder = order + 1;
                root.AddComponent<GraphicRaycaster>();
                // Options dims its parent group while a child owns focus.
                root.AddComponent<CanvasGroup>().ignoreParentGroups = true;
                ReportingPanel panel = root.AddComponent<ReportingPanel>(); panel.owner = owner;
                panel.m_IsModal = owner.m_AudioOptions.m_IsModal; panel.m_IsOptionSubMenu = true;
                panel.m_IsCloseOnLoseFocus = owner.m_AudioOptions.m_IsCloseOnLoseFocus;
                panel.m_InputMode = owner.m_AudioOptions.m_InputMode;
                panel.m_NavigationSetup = NavigationSetup.None; panel.m_SelectableParent = root.transform;
                Font font = source.GetComponentInChildren<Text>(true).font;
                GameObject plaque = new GameObject("ReportingTitlePlaque", typeof(RectTransform), typeof(Image));
                plaque.transform.SetParent(root.transform, false);
                Place(plaque.GetComponent<RectTransform>(), 0, 438, 980, 62);
                ModsPanel.StyleNativePlaque(plaque);
                panel.heading = panel.AddText(font, "Heading", 438, 56, 30, TextAnchor.MiddleCenter, 1180);
                ModsPanel.StyleNativeText(panel.heading, true);
                panel.status = panel.AddText(font, "Status", 360, 90, 22, TextAnchor.MiddleCenter, 1320);
                panel.disclosure = panel.AddText(font, "Disclosure", -274, 135, 20, TextAnchor.MiddleCenter, 1320);
                panel.disclosure.text = "Send publishes this report and selected diagnostics to the public GitHub tracker via our Railway service. Logs include recent messages, warnings and errors. No saves or screenshots. Filtering may miss personal information. Downloads expire after 30 days; GitHub text stays public. Automatic reports may send separately when enabled in Mods > Settings & Help.";
                panel.previewSurface = new GameObject("ReportingPreviewSurface", typeof(RectTransform), typeof(Image)); panel.previewSurface.transform.SetParent(root.transform, false);
                Place(panel.previewSurface.GetComponent<RectTransform>(), 0, -40, 1320, 230); ModsPanel.StyleNativePanel(panel.previewSurface, true);
                panel.preview = panel.AddText(font, "MetadataPreview", -40, 210, 20, TextAnchor.UpperLeft, 1260);
                ModsPanel.StyleNativeText(panel.preview, false, false); panel.preview.color = new Color(.13f, .12f, .10f, 1f);
                panel.pageLabel = panel.AddText(font, "Page", -183, 28, 18, TextAnchor.MiddleCenter, 200);
                panel.previous = panel.AddButton(source, "PreviousPage", "Previous", -465, -183, 230, delegate { panel.ChangePage(-1); });
                panel.next = panel.AddButton(source, "NextPage", "Next", 465, -183, 230, delegate { panel.ChangePage(1); });
                panel.primary = panel.AddButton(source, "SendReport", "Send report", -240, -375, 420, panel.Primary);
                panel.secondary = panel.AddButton(source, "ReportAction", "Not now", 240, -375, 420, panel.Secondary);
                panel.back = panel.AddButton(source, "ReportingBack", "Back to game", 0, -442, 330, panel.Back);
                panel.metadataButton = panel.AddButton(source, "IncludeMetadata", "Include diagnostics: yes", -330, 160, 530, panel.ToggleMetadata);
                panel.metadataDetailsButton = panel.AddButton(source, "MetadataDetails", "What will be sent?", 330, 160, 530, panel.ToggleDetails);
                panel.saveDraft = panel.AddButton(source, "SaveDraft", "Save draft", -330, 102, 530, delegate { panel.SaveCurrent(null); });
                panel.manageDrafts = panel.AddButton(source, "ManageDrafts", "Drafts", 330, 102, 530, delegate { panel.RequestLeave(panel.ShowDrafts); });
                for (int i = 0; i < panel.draftOpen.Length; i++)
                {
                    int row = i; float y = 260 - i * 110;
                    panel.draftLabels[i] = panel.AddText(font, "Draft" + i + "Summary", y, 82, 21, TextAnchor.MiddleLeft, 850);
                    Place(panel.draftLabels[i].GetComponent<RectTransform>(), -220, y, 850, 82);
                    panel.draftOpen[i] = panel.AddButton(source, "OpenDraft" + i, "Open", 345, y, 230, delegate { panel.OpenDraft(row); });
                    panel.draftDelete[i] = panel.AddButton(source, "DeleteDraft" + i, "Delete", 610, y, 230, delegate { panel.ConfirmDelete(row); });
                }
                panel.CreateNarrative(source, font); panel.SetExpanded(false); panel.SetPages("");
                panel.m_FirstSelected = panel.primary.GetComponent<FTKSelectable>();
                return panel;
            }
            catch { Destroy(root); throw; }
        }
        private void CreateNarrative(Button source, Font font)
        {
            narrativeRoot = new GameObject("Description", typeof(RectTransform), typeof(Image), typeof(InputField));
            narrativeRoot.transform.SetParent(transform, false); Place(narrativeRoot.GetComponent<RectTransform>(), 0, 253, 1260, 104);
            ModsPanel.StyleNativePanel(narrativeRoot, true);
            InputField input = narrativeRoot.GetComponent<InputField>(); input.targetGraphic = narrativeRoot.GetComponent<Image>();
            GameObject content = new GameObject("Text", typeof(RectTransform), typeof(Text)); content.transform.SetParent(narrativeRoot.transform, false);
            Place(content.GetComponent<RectTransform>(), -120, 0, 980, 90);
            Text text = content.GetComponent<Text>(); text.font = font; text.fontSize = 22; text.supportRichText = false;
            ModsPanel.StyleNativeText(text, false, false); text.color = new Color(.13f, .12f, .10f, 1f); input.textComponent = text;
            Text placeholder = Instantiate(text, narrativeRoot.transform); placeholder.name = "Placeholder";
            placeholder.text = "What happened? (optional)"; placeholder.fontStyle = FontStyle.Italic;
            placeholder.raycastTarget = false; input.placeholder = placeholder;
            input.characterLimit = 0; input.lineType = InputField.LineType.MultiLineNewline;
            narrative = narrativeRoot.AddComponent<FTKInputFieldSelectable>(); narrative.m_InputFocus = this; narrative.m_UnitySelectable = input;
            narrative.m_InputField = input; narrative.m_TextEditFinished = new FTKInputFieldSelectable.StringEvent(); narrative.m_TextEditChanged = new FTKInputFieldSelectable.StringEvent();
            Button edit = AddButton(source, "EditDescription", "Edit", 0, 253, 220, delegate { editing = true; narrative.OnTextButtonClick(); });
            edit.transform.SetParent(narrativeRoot.transform, true); Place(edit.GetComponent<RectTransform>(), 500, 0, 220, 48);
            narrative.m_TextButton = edit.GetComponent<FTKSelectable>(); narrative.m_ButtonText = edit.GetComponentInChildren<Text>(true);
            input.onValueChanged = new InputField.OnChangeEvent(); input.onValueChanged.AddListener(delegate(string value) {
                description = value; valid = value.Length <= 4000;
                if (Report != null) dirty = true;
                if (!valid) status.text = "Please keep the description within 4,000 characters.";
                Refresh();
            });
            input.onEndEdit = new InputField.SubmitEvent(); input.onEndEdit.AddListener(delegate(string value) { narrative.OnEditFinished(value); editing = false; Refresh(); });
            input.interactable = false;
        }
    }
}
