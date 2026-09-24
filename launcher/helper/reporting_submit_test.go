package main

import (
	"crypto/tls"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

const reportingTestRequest = `{"schemaVersion":1,"reportId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","captureId":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","kind":"manual","description":"test","includeDiagnostics":false}`

func reportingFixture(t *testing.T) (string, string) {
	t.Helper()
	dir := filepath.Join(t.TempDir(), "BepInEx", "ReportingDelivery", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
	if err := os.MkdirAll(dir, 0700); err != nil {
		t.Fatal(err)
	}
	req, res := filepath.Join(dir, "request.json"), filepath.Join(dir, "result.json")
	if err := os.WriteFile(req, []byte(reportingTestRequest), 0600); err != nil {
		t.Fatal(err)
	}
	return req, res
}

func reportingReadResult(t *testing.T, path string) reportingResult {
	t.Helper()
	raw, err := os.ReadFile(path)
	if err != nil {
		t.Fatal(err)
	}
	var result reportingResult
	if err = json.Unmarshal(raw, &result); err != nil {
		t.Fatal(err)
	}
	return result
}

func TestReportingSubmissionSendsUnchangedPayload(t *testing.T) {
	req, res := reportingFixture(t)
	server := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		data, _ := io.ReadAll(r.Body)
		if string(data) != reportingTestRequest || r.Method != "POST" || r.Header.Get("Content-Type") != "application/json" {
			t.Error("request changed")
		}
		if r.Header.Get("Authorization") != "" {
			t.Error("credential in client")
		}
		w.WriteHeader(201)
		fmt.Fprint(w, `{"schemaVersion":1,"status":"submitted","reportId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","issueNumber":123,"issueUrl":"https://github.com/jarlbrak/ftk-mod-framework/issues/123"}`)
	}))
	defer server.Close()
	if err := reportingSubmit(req, res, server.URL+"/v1/reports", server.Client()); err != nil {
		t.Fatal(err)
	}
	out := reportingReadResult(t, res)
	if out.Status != "submitted" || out.IssueNumber != 123 {
		t.Fatal(out)
	}
}

func TestReportingTransportsUsefulLogDumpAbovePreviousLimit(t *testing.T) {
	req, res := reportingFixture(t)
	var input map[string]interface{}
	if err := json.Unmarshal([]byte(reportingTestRequest), &input); err != nil {
		t.Fatal(err)
	}
	input["includeDiagnostics"] = true
	input["diagnostics"] = map[string]interface{}{
		"logs":            strings.Repeat("[Info] useful session context\n", 3500) + "CURRENT END",
		"previousSession": map[string]interface{}{"logs": strings.Repeat("[Warning] previous context\n", 3500) + "PREVIOUS END"},
	}
	body, err := json.Marshal(input)
	if err != nil || len(body) <= 128*1024 {
		t.Fatal("fixture did not exceed old bound")
	}
	if err = os.WriteFile(req, body, 0600); err != nil {
		t.Fatal(err)
	}
	server := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		observed, readErr := io.ReadAll(r.Body)
		if readErr != nil || string(observed) != string(body) {
			t.Error("useful log dump changed or truncated")
		}
		w.WriteHeader(201)
		fmt.Fprint(w, `{"schemaVersion":1,"status":"submitted","reportId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","issueNumber":123,"issueUrl":"https://github.com/jarlbrak/ftk-mod-framework/issues/123"}`)
	}))
	defer server.Close()
	if err = reportingSubmit(req, res, server.URL+"/v1/reports", server.Client()); err != nil {
		t.Fatal(err)
	}
}

func TestReportingPreservesFailureAndRejectsInvalidSuccess(t *testing.T) {
	for _, tc := range []struct {
		code        int
		body, error string
	}{
		{503, `{"schemaVersion":1,"status":"error","error":"submission_pending"}`, "submission_pending"},
		{429, `{"schemaVersion":1,"status":"error","error":"rate_limited"}`, "rate_limited"},
		{200, `{"schemaVersion":1,"status":"submitted","reportId":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","issueNumber":1,"issueUrl":"https://github.com/jarlbrak/ftk-mod-framework/issues/1"}`, "invalid_response"},
		{200, `{"schemaVersion":1,"status":"submitted","reportId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","issueNumber":1,"issueUrl":"https://evil.example/issues/1"}`, "invalid_response"},
		{200, strings.Repeat("x", reportingResponseLimit+1), "invalid_response"},
	} {
		t.Run(tc.error+fmt.Sprint(tc.code)+fmt.Sprint(len(tc.body)), func(t *testing.T) {
			req, res := reportingFixture(t)
			server := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.WriteHeader(tc.code); fmt.Fprint(w, tc.body) }))
			defer server.Close()
			if reportingSubmit(req, res, server.URL+"/v1/reports", server.Client()) == nil {
				t.Fatal("failure accepted")
			}
			if result := reportingReadResult(t, res); result.Error != tc.error || result.Status != "error" {
				t.Fatal(result)
			}
		})
	}
}

func TestReportingRejectsRedirectAndUntrustedTLS(t *testing.T) {
	req, res := reportingFixture(t)
	var reached bool
	server := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/v1/reports" {
			reached = true
		}
		http.Redirect(w, r, "/elsewhere", http.StatusTemporaryRedirect)
	}))
	defer server.Close()
	if reportingSubmit(req, res, server.URL+"/v1/reports", server.Client()) == nil || reached {
		t.Fatal("redirect followed")
	}
	// An empty trust store must fail. No production InsecureSkipVerify escape exists.
	client := &http.Client{Transport: &http.Transport{TLSClientConfig: &tls.Config{MinVersion: tls.VersionTLS12}}}
	if reportingSubmit(req, res, server.URL+"/v1/reports", client) == nil {
		t.Fatal("untrusted TLS accepted")
	}
	if reportingReadResult(t, res).Error != "transport_error" {
		t.Fatal("wrong TLS failure")
	}
}

func TestReportingPathAndEndpointBoundaries(t *testing.T) {
	req, res := reportingFixture(t)
	for _, endpoint := range []string{"http://localhost/v1/reports", "https://user:password@example.com/v1/reports", "https://example.com/v1/reports?token=a", "https://example.com/elsewhere", "https://example.com/v1/reports#x"} {
		if reportingEndpoint(endpoint) {
			t.Fatal(endpoint)
		}
	}
	if err := reportingPaths(req, res); err != nil {
		t.Fatal(err)
	}
	if reportingPaths(req, req) == nil {
		t.Fatal("overwrite request accepted")
	}
	if reportingPaths(req, filepath.Join(t.TempDir(), "result.json")) == nil {
		t.Fatal("unrelated result accepted")
	}
	outside := filepath.Join(t.TempDir(), "secret")
	if err := os.WriteFile(outside, []byte("unchanged"), 0600); err != nil {
		t.Fatal(err)
	}
	if err := os.Symlink(outside, res); err != nil {
		t.Skip("symlink unavailable")
	}
	if reportingPaths(req, res) == nil {
		t.Fatal("result symlink accepted")
	}
	data, _ := os.ReadFile(outside)
	if string(data) != "unchanged" {
		t.Fatal("symlink target changed")
	}
}

func TestReportingOversizeInputIsNotSent(t *testing.T) {
	req, res := reportingFixture(t)
	if err := os.WriteFile(req, []byte(strings.Repeat("x", reportingRequestLimit+1)), 0600); err != nil {
		t.Fatal(err)
	}
	if reportingSubmit(req, res, "https://reports.example/v1/reports", http.DefaultClient) == nil {
		t.Fatal("oversize accepted")
	}
}
