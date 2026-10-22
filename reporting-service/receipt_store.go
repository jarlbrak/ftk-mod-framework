package main

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"
)

var errReceiptConflict = errors.New("receipt changed")
var errReceiptCapacity = errors.New("receipt capacity reached")

// An empty version creates a new ID. Every replacement compares the version read
// from authoritative storage. A failed or ambiguous write never permits a POST.
type receiptStore interface {
	list() ([]string, error)
	get(string) (*receipt, string, error)
	put(*receipt, string) (string, error)
}

func decodeReceipt(data []byte, id string) (*receipt, error) {
	var rec receipt
	if len(data) > maxStoredReceipt || json.Unmarshal(data, &rec) != nil || rec.ReportID != id || rec.Hash == "" || rec.Created.IsZero() || (rec.Status != "pending" && rec.Status != "submitted" && rec.Status != "rejected") {
		return nil, errors.New("invalid receipt")
	}
	return &rec, nil
}

func receiptVersion(data []byte) string {
	hash := sha256.Sum256(data)
	return `"` + hex.EncodeToString(hash[:]) + `"`
}

// The legacy file store still requires one process and one mounted volume.
type fileReceiptStore struct {
	dir string
	mu  sync.Mutex
}

func newFileReceiptStore(dir string) (*fileReceiptStore, error) {
	if err := os.MkdirAll(dir, 0700); err != nil {
		return nil, err
	}
	return &fileReceiptStore{dir: dir}, nil
}

func (f *fileReceiptStore) list() ([]string, error) {
	entries, err := os.ReadDir(f.dir)
	if err != nil {
		return nil, err
	}
	var ids []string
	for _, entry := range entries {
		if !strings.HasSuffix(entry.Name(), ".json") {
			continue
		}
		id := strings.TrimSuffix(entry.Name(), ".json")
		if !idPattern.MatchString(id) || entry.IsDir() {
			return nil, errors.New("unexpected receipt file")
		}
		ids = append(ids, id)
	}
	return ids, nil
}

func (f *fileReceiptStore) get(id string) (*receipt, string, error) {
	if !idPattern.MatchString(id) {
		return nil, "", errors.New("invalid receipt ID")
	}
	file, err := os.Open(filepath.Join(f.dir, id+".json"))
	if os.IsNotExist(err) {
		return nil, "", nil
	}
	if err != nil {
		return nil, "", err
	}
	defer file.Close()
	data, err := io.ReadAll(io.LimitReader(file, maxStoredReceipt+1))
	if err != nil {
		return nil, "", err
	}
	rec, err := decodeReceipt(data, id)
	return rec, receiptVersion(data), err
}

func (f *fileReceiptStore) put(rec *receipt, expected string) (string, error) {
	f.mu.Lock()
	defer f.mu.Unlock()
	data, err := encodeReceipt(rec)
	if err != nil {
		return "", err
	}
	current, version, err := f.get(rec.ReportID)
	if err != nil {
		return "", err
	}
	if (expected == "" && current != nil) || (expected != "" && (current == nil || version != expected)) {
		return "", errReceiptConflict
	}
	file, err := os.CreateTemp(f.dir, ".receipt-*")
	if err != nil {
		return "", err
	}
	name := file.Name()
	defer os.Remove(name)
	if err = file.Chmod(0600); err == nil {
		_, err = file.Write(data)
	}
	if err == nil {
		err = file.Sync()
	}
	closeErr := file.Close()
	if err == nil {
		err = closeErr
	}
	if err != nil {
		return "", err
	}
	target := filepath.Join(f.dir, rec.ReportID+".json")
	if expected == "" {
		// Linking creates an ID without replacing any existing receipt.
		if err = os.Link(name, target); os.IsExist(err) {
			return "", errReceiptConflict
		}
	} else {
		err = os.Rename(name, target)
	}
	if err != nil {
		return "", err
	}
	dir, err := os.Open(f.dir)
	if err != nil {
		return "", err
	}
	defer dir.Close()
	if err = dir.Sync(); err != nil {
		return "", err
	}
	return receiptVersion(data), nil
}

// Only the Container's private outbound binding exposes this hostname. No R2
// credential is carried into Go, and redirects must never escape that binding.
type bridgeReceiptStore struct {
	base   string
	client *http.Client
}

func newBridgeReceiptStore(base string) (*bridgeReceiptStore, error) {
	u, err := url.Parse(base)
	if err != nil || u.Scheme != "http" || u.Host != "receipts.internal" || u.Path != "" || u.RawQuery != "" || u.Fragment != "" || u.User != nil {
		return nil, errors.New("receipt bridge must be the private receipts.internal origin")
	}
	return &bridgeReceiptStore{base: base, client: &http.Client{Timeout: 10 * time.Second, CheckRedirect: func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }}}, nil
}

func (b *bridgeReceiptStore) list() ([]string, error) {
	response, err := b.client.Get(b.base + "/receipts")
	if err != nil {
		return nil, errors.New("receipt list unavailable")
	}
	defer response.Body.Close()
	data, err := io.ReadAll(io.LimitReader(response.Body, 1024*1024+1))
	var ids []string
	if err != nil || response.StatusCode != 200 || len(data) > 1024*1024 || json.Unmarshal(data, &ids) != nil {
		return nil, errors.New("invalid receipt list")
	}
	seen := make(map[string]bool)
	for _, id := range ids {
		if !idPattern.MatchString(id) || seen[id] {
			return nil, errors.New("invalid receipt ID list")
		}
		seen[id] = true
	}
	return ids, nil
}

func (b *bridgeReceiptStore) get(id string) (*receipt, string, error) {
	if !idPattern.MatchString(id) {
		return nil, "", errors.New("invalid receipt ID")
	}
	response, err := b.client.Get(b.base + "/receipts/" + id)
	if err != nil {
		return nil, "", errors.New("receipt read unavailable")
	}
	defer response.Body.Close()
	if response.StatusCode == 404 {
		return nil, "", nil
	}
	if response.StatusCode != 200 || response.Header.Get("ETag") == "" {
		return nil, "", errors.New("invalid receipt read")
	}
	data, err := io.ReadAll(io.LimitReader(response.Body, maxStoredReceipt+1))
	if err != nil {
		return nil, "", errors.New("receipt read unavailable")
	}
	rec, err := decodeReceipt(data, id)
	return rec, response.Header.Get("ETag"), err
}

func (b *bridgeReceiptStore) put(rec *receipt, expected string) (string, error) {
	data, err := encodeReceipt(rec)
	if err != nil {
		return "", err
	}
	request, err := http.NewRequest("PUT", b.base+"/receipts/"+rec.ReportID, bytes.NewReader(data))
	if err != nil {
		return "", err
	}
	request.Header.Set("Content-Type", "application/json")
	if expected == "" {
		request.Header.Set("If-None-Match", "*")
	} else {
		request.Header.Set("If-Match", expected)
	}
	response, err := b.client.Do(request)
	if err != nil {
		return "", errors.New("receipt write outcome unknown")
	}
	defer response.Body.Close()
	if response.StatusCode == 412 {
		return "", errReceiptConflict
	}
	if response.StatusCode == 507 {
		return "", errReceiptCapacity
	}
	version := response.Header.Get("ETag")
	if response.StatusCode != 200 || version == "" {
		return "", errors.New("receipt write outcome unknown")
	}
	return version, nil
}
