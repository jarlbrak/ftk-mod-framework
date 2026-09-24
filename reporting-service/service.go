package main

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"io"
	"mime"
	"net"
	"net/http"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"sync"
	"time"
	"unicode/utf8"
)

const maxPayload = 128 * 1024
const retention = 30 * 24 * time.Hour

var idPattern = regexp.MustCompile(`^[a-f0-9]{32}$`)

type report struct {
	SchemaVersion      int                    `json:"schemaVersion"`
	ReportID           string                 `json:"reportId"`
	CaptureID          string                 `json:"captureId"`
	Kind               string                 `json:"kind"`
	Description        string                 `json:"description"`
	IncludeDiagnostics bool                   `json:"includeDiagnostics"`
	Diagnostics        map[string]interface{} `json:"diagnostics,omitempty"`
}

type receipt struct {
	SchemaVersion int       `json:"schemaVersion"`
	Status        string    `json:"status"`
	ReportID      string    `json:"reportId"`
	IssueNumber   int       `json:"issueNumber,omitempty"`
	IssueURL      string    `json:"issueUrl,omitempty"`
	Hash          string    `json:"hash"`
	Created       time.Time `json:"created"`
	Report        *report   `json:"report,omitempty"`
}

type bucket struct {
	start time.Time
	count int
}

type service struct {
	cfg      config
	client   *http.Client
	mu       sync.Mutex
	receipts map[string]*receipt
	ips      map[string]bucket
	global   bucket
	daily    bucket
	now      func() time.Time
	inflight chan struct{}
}

func newService(cfg config, client *http.Client) (*service, error) {
	if err := os.MkdirAll(cfg.dataDir, 0700); err != nil {
		return nil, err
	}
	s := &service{cfg: cfg, client: client, receipts: make(map[string]*receipt), ips: make(map[string]bucket), now: time.Now, inflight: make(chan struct{}, 8)}
	entries, err := os.ReadDir(cfg.dataDir)
	if err != nil {
		return nil, err
	}
	for _, entry := range entries {
		if !strings.HasSuffix(entry.Name(), ".json") {
			continue
		}
		id := strings.TrimSuffix(entry.Name(), ".json")
		if !idPattern.MatchString(id) || entry.IsDir() {
			return nil, errors.New("unexpected receipt file")
		}
		data, err := os.ReadFile(filepath.Join(cfg.dataDir, entry.Name()))
		if err != nil || len(data) > maxPayload*2 {
			return nil, errors.New("cannot read receipt")
		}
		var rec receipt
		if json.Unmarshal(data, &rec) != nil || rec.ReportID != id || rec.Hash == "" || rec.Created.IsZero() || (rec.Status != "pending" && rec.Status != "submitted" && rec.Status != "rejected") {
			return nil, errors.New("invalid receipt")
		}
		s.receipts[id] = &rec
	}
	if err := s.expire(); err != nil {
		return nil, err
	}
	return s, nil
}

// Sync the file and containing directory before attempting GitHub. A pending receipt
// never becomes permission to POST again, even after a timeout or process restart.
func (s *service) persist(rec *receipt) error {
	data, err := json.Marshal(rec)
	if err != nil {
		return err
	}
	f, err := os.CreateTemp(s.cfg.dataDir, ".receipt-*")
	if err != nil {
		return err
	}
	name := f.Name()
	defer os.Remove(name)
	if err = f.Chmod(0600); err == nil {
		_, err = f.Write(data)
	}
	if err == nil {
		err = f.Sync()
	}
	closeErr := f.Close()
	if err == nil {
		err = closeErr
	}
	if err != nil {
		return err
	}
	if err = os.Rename(name, filepath.Join(s.cfg.dataDir, rec.ReportID+".json")); err != nil {
		return err
	}
	dir, err := os.Open(s.cfg.dataDir)
	if err != nil {
		return err
	}
	defer dir.Close()
	return dir.Sync()
}

func (s *service) expire() error {
	s.mu.Lock()
	defer s.mu.Unlock()
	for _, rec := range s.receipts {
		if rec.Report != nil && s.now().Sub(rec.Created) >= retention {
			copy := *rec
			copy.Report = nil
			if err := s.persist(&copy); err != nil {
				return err
			}
			*rec = copy
		}
	}
	return nil
}

func writeJSON(w http.ResponseWriter, status int, value interface{}) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(value)
}

func fail(w http.ResponseWriter, status int, code string) {
	if status == 429 || status == 503 {
		w.Header().Set("Retry-After", "60")
	}
	writeJSON(w, status, map[string]interface{}{"schemaVersion": 1, "status": "error", "error": code})
}

func success(w http.ResponseWriter, status int, rec *receipt) {
	writeJSON(w, status, map[string]interface{}{"schemaVersion": 1, "status": "submitted", "reportId": rec.ReportID, "issueNumber": rec.IssueNumber, "issueUrl": rec.IssueURL})
}

func (s *service) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("X-Content-Type-Options", "nosniff")
	w.Header().Set("Cache-Control", "no-store")
	w.Header().Set("Referrer-Policy", "no-referrer")
	switch {
	case r.URL.Path == "/healthz" && r.Method == "GET":
		writeJSON(w, 200, map[string]interface{}{"status": "ok", "configured": s.cfg.token != ""})
	case (r.URL.Path == "/privacy" || r.URL.Path == "/") && r.Method == "GET":
		w.Header().Set("Content-Type", "text/html; charset=utf-8")
		w.Header().Set("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'")
		_, _ = io.WriteString(w, privacyHTML)
	case r.URL.Path == "/v1/reports" && r.Method == "POST":
		s.submit(w, r)
	case strings.HasPrefix(r.URL.Path, "/diagnostics/") && r.Method == "GET":
		s.download(w, r)
	default:
		fail(w, 404, "not_found")
	}
}

func (s *service) submit(w http.ResponseWriter, r *http.Request) {
	select {
	case s.inflight <- struct{}{}:
		defer func() { <-s.inflight }()
	default:
		fail(w, 503, "service_unavailable")
		return
	}

	if s.cfg.token == "" {
		fail(w, 503, "service_unavailable")
		return
	}
	mediaType, _, err := mime.ParseMediaType(r.Header.Get("Content-Type"))
	if err != nil || mediaType != "application/json" {
		fail(w, 415, "unsupported_media_type")
		return
	}
	data, err := io.ReadAll(http.MaxBytesReader(w, r.Body, maxPayload))
	if err != nil {
		var tooLarge *http.MaxBytesError
		if errors.As(err, &tooLarge) {
			fail(w, 413, "payload_too_large")
		} else {
			fail(w, 400, "invalid_report")
		}
		return
	}
	var req report
	decoder := json.NewDecoder(bytes.NewReader(data))
	decoder.DisallowUnknownFields()
	decoder.UseNumber()
	if !utf8.Valid(data) || decoder.Decode(&req) != nil || decoder.Decode(new(interface{})) != io.EOF || !validReport(req) {
		fail(w, 400, "invalid_report")
		return
	}
	// Hash the canonical submitted content before filtering, so changing content while
	// reusing an ID is a conflict even when filtering would remove that difference.
	canonical, _ := json.Marshal(req)
	digest := sha256.Sum256(canonical)
	hash := hex.EncodeToString(digest[:])
	s.mu.Lock()
	defer s.mu.Unlock()
	if rec := s.receipts[req.ReportID]; rec != nil {
		if rec.Hash != hash {
			fail(w, 409, "report_conflict")
			return
		}
		if rec.Status == "submitted" {
			success(w, 200, rec)
			return
		}
		if !s.allow(s.address(r)) {
			fail(w, 429, "rate_limited")
			return
		}
		if rec.Status == "rejected" {
			if rec.Report == nil {
				fail(w, 503, "service_unavailable")
				return
			}
			rec.Status = "pending"
			if s.persist(rec) != nil {
				fail(w, 503, "service_unavailable")
				return
			}
			if s.createIssue(rec) {
				success(w, 200, rec)
			} else {
				s.submissionFailure(w, rec)
			}
		} else if s.reconcile(rec) {
			success(w, 200, rec)
		} else {
			fail(w, 503, "submission_pending")
		}
		return
	}
	if len(s.receipts) >= s.cfg.maxReports {
		fail(w, 503, "capacity_reached")
		return
	}
	if !s.allow(s.address(r)) {
		fail(w, 429, "rate_limited")
		return
	}
	req.Description = redact(req.Description)
	if req.Diagnostics != nil {
		req.Diagnostics = sanitize(req.Diagnostics).(map[string]interface{})
	}
	rec := &receipt{SchemaVersion: 1, Status: "pending", ReportID: req.ReportID, Hash: hash, Created: s.now().UTC(), Report: &req}
	// Reserve the ID in memory even if persistence fails; a partially persisted file
	// must never allow a second attempt with a different payload in this process.
	s.receipts[rec.ReportID] = rec
	if err := s.persist(rec); err != nil {
		fail(w, 503, "service_unavailable")
		return
	}
	if !s.createIssue(rec) {
		s.submissionFailure(w, rec)
		return
	}
	success(w, 201, rec)
}

func validReport(r report) bool {
	if r.SchemaVersion != 1 || !idPattern.MatchString(r.ReportID) || !idPattern.MatchString(r.CaptureID) || utf8.RuneCountInString(r.Description) > 4000 {
		return false
	}
	if r.Kind != "manual" && r.Kind != "error" && r.Kind != "unexpected_exit" {
		return false
	}
	if !r.IncludeDiagnostics && r.Diagnostics != nil {
		return false
	}
	if r.IncludeDiagnostics && r.Diagnostics == nil {
		return false
	}
	return validValue(r.Diagnostics, 0)
}

func validValue(value interface{}, depth int) bool {
	if depth > 12 {
		return false
	}
	switch v := value.(type) {
	case map[string]interface{}:
		for key, item := range v {
			if len(key) > 128 || !validValue(item, depth+1) {
				return false
			}
		}
	case []interface{}:
		for _, item := range v {
			if !validValue(item, depth+1) {
				return false
			}
		}
	}
	return true
}

func (s *service) download(w http.ResponseWriter, r *http.Request) {
	id := strings.TrimSuffix(strings.TrimPrefix(r.URL.Path, "/diagnostics/"), ".json")
	if !idPattern.MatchString(id) || r.URL.Path != "/diagnostics/"+id+".json" {
		fail(w, 404, "not_found")
		return
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	rec := s.receipts[id]
	if rec == nil || rec.Status != "submitted" || rec.Report == nil || !rec.Report.IncludeDiagnostics || s.now().Sub(rec.Created) >= retention {
		fail(w, 404, "not_found")
		return
	}
	w.Header().Set("Content-Disposition", `attachment; filename="ftk-diagnostics-`+id+`.json"`)
	writeJSON(w, 200, rec.Report.Diagnostics)
}

func (s *service) address(r *http.Request) string {
	host, _, err := net.SplitHostPort(r.RemoteAddr)
	if err != nil {
		host = r.RemoteAddr
	}
	if s.cfg.trustedProxyHops > 0 {
		chain := strings.Split(r.Header.Get("X-Forwarded-For"), ",")
		index := len(chain) - s.cfg.trustedProxyHops
		if index >= 0 {
			candidate := strings.TrimSpace(chain[index])
			if net.ParseIP(candidate) != nil {
				host = candidate
			}
		}
	}
	return host
}

func (s *service) allow(address string) bool {
	now := s.now()
	for key, b := range s.ips {
		if now.Sub(b.start) >= time.Hour {
			delete(s.ips, key)
		}
	}
	if now.Sub(s.global.start) >= time.Hour {
		s.global = bucket{start: now}
	}
	if now.Sub(s.daily.start) >= 24*time.Hour {
		s.daily = bucket{start: now}
	}
	b, exists := s.ips[address]
	if !exists {
		b = bucket{start: now}
	}
	if (!exists && len(s.ips) >= 4096) || b.count >= s.cfg.perIP || s.global.count >= s.cfg.global || s.daily.count >= s.cfg.daily {
		return false
	}
	b.count++
	s.ips[address] = b
	s.global.count++
	s.daily.count++
	return true
}

func (s *service) submissionFailure(w http.ResponseWriter, rec *receipt) {
	if rec.Status == "rejected" {
		fail(w, 503, "service_unavailable")
	} else {
		fail(w, 503, "submission_pending")
	}
}
