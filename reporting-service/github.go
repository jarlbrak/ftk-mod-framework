package main

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strconv"
	"strings"
	"time"
)

type githubIssue struct {
	Number int    `json:"number"`
	URL    string `json:"html_url"`
	Body   string `json:"body"`
}

func marker(id string) string { return "<!-- ftk-report:" + id + " -->" }

func (s *service) githubRequest(ctx context.Context, method, path string, body []byte) (*http.Response, error) {
	r, err := http.NewRequestWithContext(ctx, method, s.cfg.apiURL+path, bytes.NewReader(body))
	if err != nil {
		return nil, err
	}
	r.Header.Set("Authorization", "Bearer "+s.cfg.token)
	r.Header.Set("Accept", "application/vnd.github+json")
	r.Header.Set("X-GitHub-Api-Version", "2022-11-28")
	r.Header.Set("Content-Type", "application/json")
	r.Header.Set("User-Agent", "FTK-Reporting-Service")
	return s.client.Do(r)
}

func (s *service) saveIssue(rec *receipt, issue githubIssue) bool {
	if issue.Number < 1 || issue.URL != "https://github.com/"+s.cfg.repository+"/issues/"+strconv.Itoa(issue.Number) {
		return false
	}
	copy := *rec
	copy.Status, copy.IssueNumber, copy.IssueURL = "submitted", issue.Number, issue.URL
	if s.persist(&copy) != nil {
		return false
	}
	*rec = copy
	return true
}

func (s *service) createIssue(rec *receipt) bool {
	r := rec.Report
	title := "In-game bug report"
	if r.Kind == "error" {
		title = "Detected game error"
	}
	if r.Kind == "unexpected_exit" {
		title = "Unexpected game exit"
	}
	if desc := strings.TrimSpace(strings.Split(r.Description, "\n")[0]); desc != "" {
		runes := []rune(desc)
		if len(runes) > 100 {
			runes = runes[:100]
		}
		title += ": " + string(runes)
	}
	descriptionHeading := "Player description"
	if r.SubmissionMode == "automatic" {
		descriptionHeading = "Automatically generated description"
	}
	body := marker(r.ReportID) + "\n## " + descriptionHeading + "\n\n" + fenced(r.Description) + "\n\nReport kind: `" + r.Kind + "`\nReport ID: `" + r.ReportID + "`\nCapture ID: `" + r.CaptureID + "`\n"
	if r.IncludeDiagnostics {
		excerpt := diagnosticExcerpt(r.Diagnostics)
		body += "\n## Diagnostics excerpt\n\n" + fenced(excerpt) + "\n\n[Download readable log dump](" + s.cfg.publicURL + "/diagnostics/" + r.ReportID + ".log) | [Download diagnostic JSON](" + s.cfg.publicURL + "/diagnostics/" + r.ReportID + ".json). These public downloads expire 30 days after submission. The excerpt above remains on GitHub.\n"
	} else {
		body += "\nThe player chose not to include diagnostics.\n"
	}
	if r.SubmissionMode == "automatic" {
		body += "\nSubmitted automatically by the game's default-on reporting setting. [Reporting disclosure](" + s.cfg.publicURL + "/privacy).\n"
	} else {
		body += "\nSubmitted from the game with the player's Send action. [Reporting disclosure](" + s.cfg.publicURL + "/privacy).\n"
	}
	payload, _ := json.Marshal(map[string]string{"title": title, "body": body})
	response, err := s.githubRequest(context.Background(), "POST", "/repos/"+s.cfg.repository+"/issues", payload)
	if err != nil {
		return false
	}
	defer response.Body.Close()
	data, err := io.ReadAll(io.LimitReader(response.Body, 512*1024))
	// These explicit client-error responses establish that no issue was created.
	// Persist that fact so fixing credentials or throttling can retry the same ID.
	switch response.StatusCode {
	case 400, 401, 403, 404, 415, 422, 429:
		copy := *rec
		copy.Status = "rejected"
		if s.persist(&copy) == nil {
			*rec = copy
		}
		return false
	}
	if err != nil || response.StatusCode != 201 {
		return false
	}
	var issue githubIssue
	return json.Unmarshal(data, &issue) == nil && s.saveIssue(rec, issue)
}

// GitHub has no idempotency key for issue creation. Search can lag. A failed
// reconciliation deliberately leaves the receipt pending instead of reposting.
func (s *service) reconcile(rec *receipt) bool {
	ctx, cancel := context.WithTimeout(context.Background(), 25*time.Second)
	defer cancel()
	since := url.QueryEscape(rec.Created.Add(-time.Minute).Format(time.RFC3339))
	for page := 1; page <= 20; page++ {
		path := fmt.Sprintf("/repos/%s/issues?state=all&sort=created&direction=asc&since=%s&per_page=100&page=%d", s.cfg.repository, since, page)
		response, err := s.githubRequest(ctx, "GET", path, nil)
		if err != nil {
			return false
		}
		data, readErr := io.ReadAll(io.LimitReader(response.Body, 4*1024*1024))
		_ = response.Body.Close()
		if readErr != nil || response.StatusCode != 200 {
			return false
		}
		var issues []githubIssue
		if json.Unmarshal(data, &issues) != nil {
			return false
		}
		for _, issue := range issues {
			if strings.HasPrefix(issue.Body, marker(rec.ReportID)) {
				return s.saveIssue(rec, issue)
			}
		}
		if len(issues) < 100 {
			return false
		}
	}
	return false
}

func fenced(text string) string {
	if strings.TrimSpace(text) == "" {
		text = "No description supplied."
	}
	return "```text\n" + strings.ReplaceAll(strings.ReplaceAll(text, "`", "'"), "@", "@\u200b") + "\n```"
}

// Put recent log tails first so a large metadata section or older log lines
// cannot hide the failure that prompted the report.
func diagnosticExcerpt(diagnostics map[string]interface{}) string {
	var parts []string
	if logs, ok := diagnostics["logs"].(string); ok && logs != "" {
		parts = append(parts, "Current session log tail:\n"+validTail(logs, 4000))
	}
	if previous, ok := diagnostics["previousSession"].(map[string]interface{}); ok {
		if logs, ok := previous["logs"].(string); ok && logs != "" {
			parts = append(parts, "Previous session log tail:\n"+validTail(logs, 4000))
		}
	}
	data, _ := json.MarshalIndent(diagnostics, "", "  ")
	if len(parts) == 0 {
		return validPrefix(string(data), 12000)
	}
	parts = append(parts, "Diagnostic context (bounded excerpt):\n"+validPrefix(string(data), 3000))
	return strings.Join(parts, "\n\n") + "\n[Download the bundle for full diagnostics.]"
}
