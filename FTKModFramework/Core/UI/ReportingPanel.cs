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
        internal Action Closed;
        internal ReportingReport Report { get; private set; }
        internal bool IsOffer { get { return kind != "manual"; } }
        internal bool Submitted { get { return submitted; } }
        private uiOptionsMenu owner;
        private Text heading, status, preview, pageLabel, disclosure;
        private Button primary, secondary, metadataButton, metadataDetailsButton, previous, next, back;
        private FTKInputFieldSelectable narrative;
        private GameObject narrativeRoot, previewSurface;
        private readonly List<string> pages = new List<string>();
        private int page;
        private bool editing, expanded, sending, submitted, valid = true;
        private string kind = "manual", description = "", currentLogs, previousLogs, frozen, issueUrl, errorId, priorId;
        private string deferredPayload, deferredErrorId, deferredPriorId;
        private new void Awake() { m_IsOptionSubMenu = true; m_Cancel = Close; }
        public override void OnPreSetFocus() { gameObject.SetActive(true); Refresh(); base.OnPreSetFocus(); }
        public override void OnClose()
        {
            FinishTyping(); gameObject.SetActive(false);
            if (owner && owner.m_MainOptions && owner.m_MainOptions.m_SubBlocker) owner.m_MainOptions.m_SubBlocker.gameObject.SetActive(false);
            base.OnClose(); if (Closed != null) Closed();
        }
        internal void ShowOffer()
        {
            if (frozen != null && !submitted) return;
            BeginReport(ReportingRuntime.CreateReport(ReportingRuntime.Pending));
            if (frozen != null) return;
            kind = "unexpected_exit"; priorId = Report.PreviousSessionId;
            heading.text = "Report the previous session?";
            status.text = "The last session ended unexpectedly. Send its saved diagnostics with one click. Force quit or power loss can also cause this offer.";
            Refresh();
        }
        internal void ShowErrorOffer()
        {
            if (frozen != null && !submitted) return;
            ReportingDiagnosticsError error = ReportingDiagnostics.PendingError;
            BeginReport(ReportingRuntime.CreateReport(false));
            if (frozen != null) return;
            kind = "error"; errorId = error == null ? null : error.Id;
            heading.text = "An error was detected";
            status.text = "Help us diagnose it with one click. You can add what you were doing, or keep playing.";
            Refresh();
        }
        internal void BeginReport(ReportingReport report)
        {
            if (submitted && RestoreDeferred()) return;
            FinishTyping(); Report = report; description = ""; frozen = null; issueUrl = null; errorId = null; priorId = report.PreviousSessionId;
            kind = priorId == null ? "manual" : "unexpected_exit"; sending = submitted = false; valid = true;
            currentLogs = ReportingDiagnostics.CaptureCurrent(); previousLogs = ReportingDiagnostics.CapturePrevious(priorId);
            narrative.text = "";
            heading.text = "Report a problem";
            status.text = "What happened? A short description helps. Diagnostics are included automatically when you send.";
            RestorePending(); SetExpanded(false); Refresh();
        }
        internal void BeginSavedDraft(ReportingDraft saved)
        {
            BeginReport(saved.Report);
            if (frozen == null)
            {
                // A recovered metadata capture is not evidence that this launch's errors occurred then.
                currentLogs = "";
                try { ReportingIssueNarrative old = ReportingIssueNarrative.Restore(saved.Description); description = old.Summary + "\n" + old.Reproduction + "\n" + old.ExpectedActual; }
                catch { description = saved.Description; }
                narrative.text = description; valid = description.Length <= 4000;
                status.text = "Saved draft restored. Check what you want to share, then send.";
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
            primary.interactable = !sending && !ReportingSubmission.Busy && ReportingSubmission.Ready && valid;
            SetLabel(primary, submitted ? "View issue" : frozen != null ? "Retry report" : "Send report");
            SetLabel(secondary, frozen != null && !submitted ? "Discard local copy" : deferredPayload != null ? "Return to your report" : submitted ? "Close" : IsOffer ? "Not now" : "Close");
            secondary.interactable = !sending;
            metadataButton.interactable = ReportingSubmission.Ready && frozen == null && !sending && !submitted;
            SetLabel(metadataButton, Report.IncludeMetadata ? "Include diagnostics: yes" : "Include diagnostics: no");
            narrativeRoot.SetActive(ReportingSubmission.Ready && frozen == null && !submitted);
            narrative.m_ButtonText.text = "Edit";
            SetLabel(metadataDetailsButton, expanded ? "Hide details" : "What will be sent?");
            SetupOwnedNavigation();
        }
        private void Primary()
        {
            if (submitted) { if (!String.IsNullOrEmpty(issueUrl)) Application.OpenURL(issueUrl); return; }
            if (sending || ReportingSubmission.Busy) return;
            FinishTyping();
            try
            {
                if (frozen == null) frozen = ReportingSubmissionPayload.Create(Report, description, kind, currentLogs, previousLogs);
            }
            catch (Exception) { status.text = "The report is too large. Shorten the description or exclude diagnostics."; return; }
            sending = true; status.text = "Sending report and selected diagnostics... You can return to the game."; Refresh();
            string sentErrorId = errorId, sentPriorId = priorId;
            if (!ReportingSubmission.Start(frozen, delegate(ReportingSubmissionResult result) {
                // Bookkeeping does not depend on a scene-owned panel surviving the response.
                if (result.Success)
                {
                    ReportingRuntime.DeleteDraft(result.ReportId, null);
                    if (sentErrorId != null) ReportingDiagnostics.Acknowledge(sentErrorId);
                    ReportingIncident pending = ReportingRuntime.Pending;
                    if (sentPriorId != null && pending != null && pending.SessionId == sentPriorId) ReportingRuntime.DismissPending(null);
                }
                if (!this) return;
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
            if (sending) return;
            if (submitted && RestoreDeferred()) return;
            if (frozen != null && !submitted)
            {
                sending = true; Refresh();
                ReportingSubmission.DiscardPending(frozen, delegate(bool success) {
                    if (!this) return; sending = false;
                    if (success) { frozen = null; if (!RestoreDeferred()) BeginReport(ReportingRuntime.CreateReport(false)); status.text = "Local copy discarded. This does not delete a report that already reached GitHub."; }
                    else status.text = "The local copy could not be removed. Try again.";
                    Refresh();
                });
                return;
            }
            if (errorId != null) ReportingDiagnostics.Acknowledge(errorId);
            if (kind == "unexpected_exit") ReportingRuntime.DismissPending(null);
            Report = null;
            Close();
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
        private void ToggleMetadata() { if (frozen != null || sending) return; Report.IncludeMetadata = !Report.IncludeMetadata; Refresh(); if (expanded) ShowPreview(); }
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
            if (expanded) { controls.Add(previous); controls.Add(next); }
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
                panel.disclosure.text = "Send publishes your description and selected error logs, versions, mods and session context to the public GitHub tracker via our Railway service. No saves or screenshots. Logs are filtered but may contain personal information. Diagnostic downloads expire after 30 days; GitHub text stays public. Nothing is uploaded until you press Send.";
                panel.previewSurface = new GameObject("ReportingPreviewSurface", typeof(RectTransform), typeof(Image)); panel.previewSurface.transform.SetParent(root.transform, false);
                Place(panel.previewSurface.GetComponent<RectTransform>(), 0, -40, 1320, 230); ModsPanel.StyleNativePanel(panel.previewSurface, true);
                panel.preview = panel.AddText(font, "MetadataPreview", -40, 210, 20, TextAnchor.UpperLeft, 1260);
                ModsPanel.StyleNativeText(panel.preview, false, false); panel.preview.color = new Color(.13f, .12f, .10f, 1f);
                panel.pageLabel = panel.AddText(font, "Page", -183, 28, 18, TextAnchor.MiddleCenter, 200);
                panel.previous = panel.AddButton(source, "PreviousPage", "Previous", -465, -183, 230, delegate { if (panel.page > 0) { panel.page--; panel.DisplayPage(); } });
                panel.next = panel.AddButton(source, "NextPage", "Next", 465, -183, 230, delegate { if (panel.page + 1 < panel.pages.Count) { panel.page++; panel.DisplayPage(); } });
                panel.primary = panel.AddButton(source, "SendReport", "Send report", -240, -375, 420, panel.Primary);
                panel.secondary = panel.AddButton(source, "ReportAction", "Not now", 240, -375, 420, panel.Secondary);
                panel.back = panel.AddButton(source, "ReportingBack", "Back to game", 0, -442, 330, panel.Close);
                panel.metadataButton = panel.AddButton(source, "IncludeMetadata", "Include diagnostics: yes", -330, 160, 530, panel.ToggleMetadata);
                panel.metadataDetailsButton = panel.AddButton(source, "MetadataDetails", "What will be sent?", 330, 160, 530, panel.ToggleDetails);
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
                if (!valid) status.text = "Please keep the description within 4,000 characters.";
                Refresh();
            });
            input.onEndEdit = new InputField.SubmitEvent(); input.onEndEdit.AddListener(delegate(string value) { narrative.OnEditFinished(value); editing = false; Refresh(); });
            input.interactable = false;
        }
    }
}
