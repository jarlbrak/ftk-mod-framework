package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"
)

type conditionalTestStore struct {
	mu              sync.Mutex
	data            map[string][]byte
	failReads       bool
	failPutAt       int
	commitFailedPut bool
	writes          int
	raceReads       chan struct{}
	raceCount       int
}

func testStore() *conditionalTestStore { return &conditionalTestStore{data: make(map[string][]byte)} }
func (m *conditionalTestStore) list() ([]string, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	var ids []string
	for id := range m.data {
		ids = append(ids, id)
	}
	return ids, nil
}
func (m *conditionalTestStore) get(id string) (*receipt, string, error) {
	m.mu.Lock()
	if m.failReads {
		m.mu.Unlock()
		return nil, "", errors.New("unavailable")
	}
	data := append([]byte(nil), m.data[id]...)
	barrier := m.raceReads
	if len(data) == 0 && barrier != nil {
		m.raceCount++
		if m.raceCount == 2 {
			close(barrier)
			m.raceReads = nil
		}
	}
	m.mu.Unlock()
	if barrier != nil && len(data) == 0 {
		<-barrier
	}
	if len(data) == 0 {
		return nil, "", nil
	}
	rec, err := decodeReceipt(data, id)
	return rec, receiptVersion(data), err
}
func (m *conditionalTestStore) put(rec *receipt, expected string) (string, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	old, exists := m.data[rec.ReportID]
	if (expected == "" && exists) || (expected != "" && (!exists || receiptVersion(old) != expected)) {
		return "", errReceiptConflict
	}
	data, err := encodeReceipt(rec)
	if err != nil {
		return "", err
	}
	m.writes++
	if m.writes == m.failPutAt {
		if m.commitFailedPut {
			m.data[rec.ReportID] = data
		}
		return "", errors.New("write outcome unknown")
	}
	m.data[rec.ReportID] = data
	return receiptVersion(data), nil
}
func storedService(t *testing.T, store receiptStore, handler http.HandlerFunc) *service {
	t.Helper()
	api := httptest.NewServer(handler)
	t.Cleanup(api.Close)
	s, err := newServiceWithStore(config{repository: "owner/repo", token: "synthetic-token", publicURL: "https://reports.example", apiURL: api.URL, maxReports: 100, perIP: 100, global: 100, daily: 500}, api.Client(), store)
	if err != nil {
		t.Fatal(err)
	}
	return s
}
func TestConditionalCreateAcrossProcessesPostsOnce(t *testing.T) {
	store := testStore()
	store.raceReads = make(chan struct{})
	var posts atomic.Int32
	handler := func(w http.ResponseWriter, r *http.Request) {
		if r.Method == "POST" {
			posts.Add(1)
			w.WriteHeader(201)
			fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
		} else {
			fmt.Fprint(w, `[]`)
		}
	}
	a := storedService(t, store, handler)
	b := storedService(t, store, handler)
	var wait sync.WaitGroup
	wait.Add(2)
	for _, svc := range []*service{a, b} {
		go func(s *service) {
			defer wait.Done()
			w := post(s, requestBody())
			if w.Code != 201 && w.Code != 503 {
				t.Errorf("unexpected response%d", w.Code)
			}
		}(svc)
	}
	wait.Wait()
	if posts.Load() != 1 {
		t.Fatalf("POST calls%d", posts.Load())
	}
}
func TestAmbiguousStorageCreateNeverPostsAfterRestart(t *testing.T) {
	store := testStore()
	store.failPutAt = 1
	store.commitFailedPut = true
	var posts atomic.Int32
	handler := func(w http.ResponseWriter, r *http.Request) {
		if r.Method == "POST" {
			posts.Add(1)
			t.Error("ambiguous storage write authorized POST")
		}
		fmt.Fprint(w, `[]`)
	}
	s := storedService(t, store, handler)
	if w := post(s, requestBody()); w.Code != 503 {
		t.Fatal(w.Code)
	}
	restarted := storedService(t, store, handler)
	for i := 0; i < 2; i++ {
		if w := post(restarted, requestBody()); w.Code != 503 || !strings.Contains(w.Body.String(), "submission_pending") {
			t.Fatal(w.Code, w.Body.String())
		}
	}
	if posts.Load() != 0 {
		t.Fatal("created issue")
	}
}
func TestCrashAfterIssueCreationOnlyReconciles(t *testing.T) {
	store := testStore()
	store.failPutAt = 2
	var posts, reads atomic.Int32
	handler := func(w http.ResponseWriter, r *http.Request) {
		if r.Method == "POST" {
			posts.Add(1)
			w.WriteHeader(201)
			fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
		} else {
			reads.Add(1)
			_ = json.NewEncoder(w).Encode([]githubIssue{{Number: 12, URL: "https://github.com/owner/repo/issues/12", Body: marker(strings.Repeat("a", 32))}})
		}
	}
	s := storedService(t, store, handler)
	if w := post(s, requestBody()); w.Code != 503 {
		t.Fatal(w.Code)
	}
	restarted := storedService(t, store, handler)
	if w := post(restarted, requestBody()); w.Code != 200 {
		t.Fatal(w.Code, w.Body.String())
	}
	if posts.Load() != 1 || reads.Load() != 1 {
		t.Fatal("creation was repeated", posts.Load(), reads.Load())
	}
}
func TestAuthoritativeStorageReadFailsClosedForRetryAndDownload(t *testing.T) {
	store := testStore()
	var calls atomic.Int32
	s := storedService(t, store, func(w http.ResponseWriter, r *http.Request) {
		calls.Add(1)
		w.WriteHeader(201)
		fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
	})
	if w := post(s, requestBody()); w.Code != 201 {
		t.Fatal(w.Code)
	}
	store.mu.Lock()
	store.failReads = true
	store.mu.Unlock()
	if w := post(s, requestBody()); w.Code != 503 {
		t.Fatal("trusted stale cache", w.Code)
	}
	w := httptest.NewRecorder()
	s.ServeHTTP(w, httptest.NewRequest("GET", "/diagnostics/"+strings.Repeat("a", 32)+".json", nil))
	if w.Code != 503 {
		t.Fatal("stale diagnostics exposed", w.Code)
	}
	if calls.Load() != 1 {
		t.Fatal("storage failure contacted GitHub")
	}
}
func TestConditionalReplacementDoesNotOverwriteNewerReceipt(t *testing.T) {
	store := testStore()
	rec := &receipt{SchemaVersion: 1, Status: "pending", ReportID: strings.Repeat("a", 32), Hash: strings.Repeat("c", 64), Created: time.Now().UTC()}
	version, err := store.put(rec, "")
	if err != nil {
		t.Fatal(err)
	}
	newer := *rec
	newer.Status = "submitted"
	newer.IssueNumber = 12
	newer.IssueURL = "https://github.com/owner/repo/issues/12"
	if _, err := store.put(&newer, version); err != nil {
		t.Fatal(err)
	}
	stale := *rec
	stale.Status = "rejected"
	if _, err := store.put(&stale, version); !errors.Is(err, errReceiptConflict) {
		t.Fatal("stale replacement accepted", err)
	}
	got, _, _ := store.get(rec.ReportID)
	if got.Status != "submitted" || got.IssueNumber != 12 {
		t.Fatal("issue reference overwritten")
	}
}
func TestDurableExpiryRetainsReceiptIdentityAndIssue(t *testing.T) {
	store := testStore()
	s := storedService(t, store, func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(201)
		fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
	})
	if post(s, requestBody()).Code != 201 {
		t.Fatal("submit failed")
	}
	before, _, _ := store.get(strings.Repeat("a", 32))
	s.now = func() time.Time { return before.Created.Add(retention + time.Second) }
	if err := s.expire(); err != nil {
		t.Fatal(err)
	}
	after, _, _ := store.get(before.ReportID)
	if after.Report != nil || after.ReportID != before.ReportID || after.Hash != before.Hash || after.Status != before.Status || after.IssueNumber != before.IssueNumber || after.IssueURL != before.IssueURL || !after.Created.Equal(before.Created) {
		t.Fatal("expiry removed or changed receipt metadata")
	}
}
func TestBridgeActivationAndOriginFailClosed(t *testing.T) {
	for _, origin := range []string{"https://example.com", "http://receipts.internal/other", "http://receipts.internal?x=y", "http://user@receipts.internal", "http://127.0.0.1"} {
		if _, err := newBridgeReceiptStore(origin); err == nil {
			t.Fatal("accepted public or malformed bridge", origin)
		}
	}
	if _, err := newService(config{receiptBridge: "http://receipts.internal"}, http.DefaultClient); err == nil {
		t.Fatal("disabled bridge activated")
	}
	if _, err := newService(config{receiptBridgeEnabled: true}, http.DefaultClient); err == nil {
		t.Fatal("missing bridge fell back to files")
	}
}

func TestUncommittedStorageFailureDoesNotPostUntilDurableRetry(t *testing.T) {
	store := testStore()
	store.failPutAt = 1
	var posts atomic.Int32
	s := storedService(t, store, func(w http.ResponseWriter, r *http.Request) {
		posts.Add(1)
		w.WriteHeader(201)
		fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
	})
	if post(s, requestBody()).Code != 503 || posts.Load() != 0 {
		t.Fatal("failed persistence authorized POST")
	}
	if post(s, requestBody()).Code != 503 || posts.Load() != 0 {
		t.Fatal("same process forgot uncertain receipt")
	}
	// No issue POST occurred; authoritative missing storage after restart can
	// accept a newly persisted request, unlike an uncertain GitHub creation.
	restarted := storedService(t, store, func(w http.ResponseWriter, r *http.Request) {
		posts.Add(1)
		w.WriteHeader(201)
		fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
	})
	if post(restarted, requestBody()).Code != 201 || posts.Load() != 1 {
		t.Fatal("durable retry failed")
	}
}

func TestFinalReceiptWriteCommittedWithoutAcknowledgmentIsNotReposted(t *testing.T) {
	store := testStore()
	store.failPutAt = 2
	store.commitFailedPut = true
	var posts atomic.Int32
	handler := func(w http.ResponseWriter, r *http.Request) {
		if r.Method != "POST" {
			t.Error("final receipt should avoid reconciliation")
			return
		}
		posts.Add(1)
		w.WriteHeader(201)
		fmt.Fprint(w, `{"number":12,"html_url":"https://github.com/owner/repo/issues/12"}`)
	}
	s := storedService(t, store, handler)
	if post(s, requestBody()).Code != 503 {
		t.Fatal("expected ambiguous acknowledgment")
	}
	restarted := storedService(t, store, handler)
	if post(restarted, requestBody()).Code != 200 || posts.Load() != 1 {
		t.Fatal("final receipt was reposted")
	}
}

type receiptRoundTrip func(*http.Request) (*http.Response, error)

func (f receiptRoundTrip) RoundTrip(r *http.Request) (*http.Response, error) { return f(r) }
func TestBridgeProtocolUsesConditionalWritesAndRejectsBadReads(t *testing.T) {
	bridge, err := newBridgeReceiptStore("http://receipts.internal")
	if err != nil {
		t.Fatal(err)
	}
	rec := &receipt{SchemaVersion: 1, Status: "pending", ReportID: strings.Repeat("a", 32), Hash: "synthetic-hash", Created: time.Now().UTC()}
	for _, version := range []string{"", `"abcd"`} {
		bridge.client.Transport = receiptRoundTrip(func(r *http.Request) (*http.Response, error) {
			if r.URL.Host != "receipts.internal" || r.Method != "PUT" {
				t.Fatal("unexpected bridge target")
			}
			if version == "" && r.Header.Get("If-None-Match") != "*" {
				t.Fatal("missing conditional create")
			}
			if version != "" && r.Header.Get("If-Match") != version {
				t.Fatal("missing conditional replacement")
			}
			return &http.Response{StatusCode: 200, Header: http.Header{"Etag": []string{`"efab"`}}, Body: http.NoBody}, nil
		})
		if got, err := bridge.put(rec, version); err != nil || got != `"efab"` {
			t.Fatal("conditional write failed", err)
		}
	}
	for _, status := range []int{200, 302, 503} {
		bridge.client.Transport = receiptRoundTrip(func(r *http.Request) (*http.Response, error) {
			return &http.Response{StatusCode: status, Header: make(http.Header), Body: http.NoBody}, nil
		})
		if _, _, err := bridge.get(rec.ReportID); err == nil {
			t.Fatal("missing ETag or failure accepted")
		}
	}
}
