package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

func fixture(t *testing.T, handler http.HandlerFunc) *service {
	t.Helper()
	api := httptest.NewServer(handler)
	t.Cleanup(api.Close)
	s, err := newService(config{dataDir: t.TempDir(), repository: "owner/repo", token: "test-token", publicURL: "https://reports.example", apiURL: api.URL, maxReports: 100, perIP: 20, global: 100, daily: 500}, api.Client())
	if err != nil {
		t.Fatal(err)
	}
	return s
}

func requestBody() string {
	return `{"schemaVersion":1,"reportId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","captureId":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","kind":"error","description":"A test error","includeDiagnostics":true,"diagnostics":{"version":"1","logs":["failure at /Users/alice/mod.cs password=hunter2 person@example.com ghp_abcdef 76561191234567890"],"token":"never publish"}}`
}

func post(s *service, body string) *httptest.ResponseRecorder {
	r := httptest.NewRequest("POST", "/v1/reports", strings.NewReader(body))
	r.Header.Set("Content-Type", "application/json")
	r.RemoteAddr = "192.0.2.1:4567"
	w := httptest.NewRecorder()
	s.ServeHTTP(w, r)
	return w
}

func TestSubmitRedactsAndSurvivesRestart(t *testing.T) {
	var calls atomic.Int32
	var issueBody string
	s := fixture(t, func(w http.ResponseWriter, r *http.Request) {
		calls.Add(1)
		if r.Header.Get("Authorization") != "Bearer test-token" {
			t.Error("missing server authentication")
		}
		var issue map[string]string
		_ = json.NewDecoder(r.Body).Decode(&issue)
		issueBody = issue["body"]
		w.WriteHeader(201)
		fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
	})
	w := post(s, requestBody())
	if w.Code != 201 {
		t.Fatalf("%d %s", w.Code, w.Body)
	}
	for _, secret := range []string{"alice", "hunter2", "person@example.com", "ghp_abcdef", "76561191234567890", "never publish"} {
		if strings.Contains(issueBody, secret) {
			t.Errorf("leaked %s", secret)
		}
	}
	if !strings.Contains(issueBody, "/diagnostics/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.json") {
		t.Fatal("missing automatic bundle")
	}
	stored, err := os.ReadFile(filepath.Join(s.cfg.dataDir, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.json"))
	if err != nil || bytes.Contains(stored, []byte("hunter2")) {
		t.Fatal("unredacted disk content")
	}
	restarted, err := newService(s.cfg, s.client)
	if err != nil {
		t.Fatal(err)
	}
	w = post(restarted, requestBody())
	if w.Code != 200 || calls.Load() != 1 {
		t.Fatalf("duplicate created: %d calls %d", w.Code, calls.Load())
	}
	w = post(restarted, strings.Replace(requestBody(), "A test error", "Changed content", 1))
	if w.Code != 409 {
		t.Fatalf("expected content conflict, got %d", w.Code)
	}
	get := httptest.NewRecorder()
	restarted.ServeHTTP(get, httptest.NewRequest("GET", "/diagnostics/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.json", nil))
	if get.Code != 200 || strings.Contains(get.Body.String(), "hunter2") {
		t.Fatal("invalid download")
	}
}

func TestAmbiguousCreateNeverPostsTwiceAndCanReconcile(t *testing.T) {
	var posts atomic.Int32
	var found atomic.Bool
	s := fixture(t, func(w http.ResponseWriter, r *http.Request) {
		if r.Method == "POST" {
			posts.Add(1)
			w.WriteHeader(502)
			return
		}
		if !found.Load() {
			fmt.Fprint(w, "[]")
			return
		}
		fmt.Fprint(w, `[{"number":14,"html_url":"https://github.com/owner/repo/issues/14","body":"<!-- ftk-report:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa -->\nTest"}]`)
	})
	if w := post(s, requestBody()); w.Code != 503 {
		t.Fatal(w.Code)
	}
	restarted, err := newService(s.cfg, s.client)
	if err != nil {
		t.Fatal(err)
	}
	if w := post(restarted, requestBody()); w.Code != 503 {
		t.Fatal(w.Code)
	}
	found.Store(true)
	if w := post(restarted, requestBody()); w.Code != 200 {
		t.Fatalf("%d %s", w.Code, w.Body)
	}
	if posts.Load() != 1 {
		t.Fatal("uncertain POST was repeated")
	}
}

func TestDefiniteRejectionCanRetryAfterCredentialFixAndRestart(t *testing.T) {
	var posts atomic.Int32
	for _, status := range []int{400, 401, 403, 404, 415, 422, 429} {
		t.Run(fmt.Sprint(status), func(t *testing.T) {
			posts.Store(0)
			s := fixture(t, func(w http.ResponseWriter, r *http.Request) {
				if r.Method != "POST" {
					t.Error("definite rejection should not need reconciliation")
				}
				if posts.Add(1) == 1 {
					w.WriteHeader(status)
					return
				}
				w.WriteHeader(201)
				fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
			})
			w := post(s, requestBody())
			if w.Code != 503 || !strings.Contains(w.Body.String(), "service_unavailable") {
				t.Fatal(w.Code, w.Body.String())
			}
			restarted, err := newService(s.cfg, s.client)
			if err != nil {
				t.Fatal(err)
			}
			if w = post(restarted, requestBody()); w.Code != 200 {
				t.Fatal(w.Code, w.Body.String())
			}
			if posts.Load() != 2 {
				t.Fatal("did not retry rejected submission")
			}
		})
	}
}

func TestValidationLimitsAndNoDiagnostics(t *testing.T) {
	s := fixture(t, func(w http.ResponseWriter, r *http.Request) { t.Fatal("invalid input reached GitHub") })
	for _, body := range []string{
		strings.Replace(requestBody(), `"schemaVersion":1`, `"schemaVersion":2`, 1),
		strings.Replace(requestBody(), `"kind":"error"`, `"kind":"anything"`, 1),
		strings.Replace(requestBody(), `"includeDiagnostics":true`, `"includeDiagnostics":false`, 1),
		strings.Replace(requestBody(), `"description":"A test error"`, `"description":"`+strings.Repeat("x", 4001)+`"`, 1),
		strings.Replace(requestBody(), `"schemaVersion":1`, `"schemaVersion":1,"repository":"evil/repo"`, 1),
		requestBody() + ` {}`,
	} {
		if w := post(s, body); w.Code != 400 {
			t.Fatalf("got %d for %s", w.Code, validPrefix(body, 100))
		}
	}
	if w := post(s, strings.Repeat("x", maxPayload+1)); w.Code != 413 {
		t.Fatal(w.Code)
	}
	var req report
	_ = json.Unmarshal([]byte(requestBody()), &req)
	req.IncludeDiagnostics = false
	req.Diagnostics = nil
	if !validReport(req) {
		t.Fatal("diagnostics opt-out rejected")
	}
}

func TestRetentionKeepsIdempotencyAndExpiresDownload(t *testing.T) {
	s := fixture(t, func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(201)
		fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
	})
	if post(s, requestBody()).Code != 201 {
		t.Fatal("submission failed")
	}
	future := time.Now().Add(retention + time.Hour)
	s.now = func() time.Time { return future }
	if err := s.expire(); err != nil {
		t.Fatal(err)
	}
	if s.receipts["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"].Report != nil {
		t.Fatal("payload retained")
	}
	w := httptest.NewRecorder()
	s.ServeHTTP(w, httptest.NewRequest("GET", "/diagnostics/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.json", nil))
	if w.Code != 404 {
		t.Fatal(w.Code)
	}
	if post(s, requestBody()).Code != 200 {
		t.Fatal("receipt lost")
	}
}

func TestCapacityAndRates(t *testing.T) {
	s := fixture(t, func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(201)
		fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
	})
	s.cfg.perIP = 1
	if post(s, requestBody()).Code != 201 {
		t.Fatal("first failed")
	}
	second := strings.Replace(requestBody(), "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "cccccccccccccccccccccccccccccccc", 1)
	if post(s, second).Code != 429 {
		t.Fatal("IP limit missing")
	}
	if post(s, requestBody()).Code != 200 {
		t.Fatal("idempotent retry rate limited")
	}
	s.cfg.maxReports = 1
	if post(s, second).Code != 503 {
		t.Fatal("capacity limit missing")
	}
	s.cfg.maxReports = 100
	s.cfg.perIP = 100
	s.cfg.daily = 1
	if post(s, second).Code != 429 {
		t.Fatal("daily limit missing")
	}
}

func TestProxyTrustAndUnconfiguredHealth(t *testing.T) {
	s := fixture(t, func(w http.ResponseWriter, r *http.Request) { t.Fatal("unexpected GitHub request") })
	r := httptest.NewRequest("GET", "/", nil)
	r.RemoteAddr = "192.0.2.1:80"
	r.Header.Set("X-Forwarded-For", "198.51.100.2, 203.0.113.9")
	if s.address(r) != "192.0.2.1" {
		t.Fatal("trusted spoofed header")
	}
	s.cfg.trustedProxyHops = 1
	if s.address(r) != "203.0.113.9" {
		t.Fatal("incorrect proxy boundary")
	}
	s.cfg.token = ""
	if post(s, requestBody()).Code != 503 {
		t.Fatal("unconfigured accepted")
	}
	w := httptest.NewRecorder()
	s.ServeHTTP(w, httptest.NewRequest("GET", "/healthz", nil))
	if w.Code != 200 || !strings.Contains(w.Body.String(), `"configured":false`) {
		t.Fatal("health not explicit")
	}
}

func TestBoundedInFlightAndRecentErrorExcerpt(t *testing.T) {
	s := fixture(t, func(w http.ResponseWriter, r *http.Request) { t.Fatal("busy request reached GitHub") })
	for i := 0; i < cap(s.inflight); i++ {
		s.inflight <- struct{}{}
	}
	if post(s, requestBody()).Code != 503 {
		t.Fatal("in-flight cap missing")
	}
	excerpt := diagnosticExcerpt(map[string]interface{}{
		"logs":            strings.Repeat("old line\n", 3000) + "NEW_CURRENT_FAILURE",
		"metadata":        strings.Repeat("context", 5000),
		"previousSession": map[string]interface{}{"logs": strings.Repeat("old line\n", 3000) + "PREVIOUS_CRASH"},
	})
	if len(excerpt) > 12000 || !strings.Contains(excerpt, "NEW_CURRENT_FAILURE") || !strings.Contains(excerpt, "PREVIOUS_CRASH") {
		t.Fatal("latest failures omitted or excerpt too large")
	}
}
