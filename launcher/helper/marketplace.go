package main

import (
	"archive/zip"
	"bytes"
	"context"
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"image"
	_ "image/jpeg"
	_ "image/png"
	"io"
	"net/http"
	"net/url"
	"os"
	"path"
	"path/filepath"
	"regexp"
	"sort"
	"strings"
	"time"
)

const marketCatalogURL = "https://raw.githubusercontent.com/jarlbrak/ftk-mod-framework/master/marketplace/catalog.json"
const marketLimit = 2 << 20
const archiveLimit = 100 << 20
const expandedLimit = 250 << 20

var marketWriteHook func(string) error
var marketID = regexp.MustCompile(`^[a-z0-9][a-z0-9._-]{0,127}$`)
var marketHex = regexp.MustCompile(`^[a-f0-9]{32}$`)
var marketSHA = regexp.MustCompile(`^[a-f0-9]{64}$`)
var marketVersion = regexp.MustCompile(`^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$`)

type marketDependency struct {
	PackageID string `json:"packageId"`
	Version   string `json:"version"`
}
type marketPackage struct {
	ScreenshotPaths     []string           `json:"screenshotPaths,omitempty"`
	PackageID           string             `json:"packageId"`
	ModGUID             string             `json:"modGuid"`
	Name                string             `json:"name"`
	Author              string             `json:"author"`
	Description         string             `json:"description"`
	Category            string             `json:"category"`
	Version             string             `json:"version"`
	License             string             `json:"license"`
	FrameworkRange      string             `json:"frameworkRange"`
	GameFingerprints    []string           `json:"gameFingerprints"`
	Platforms           []string           `json:"platforms"`
	Dependencies        []marketDependency `json:"dependencies"`
	SHA256              string             `json:"sha256"`
	URL                 string             `json:"packageUrl"`
	CompressedSize      int64              `json:"compressedSize"`
	ExpandedSize        int64              `json:"expandedSize"`
	FileCount           int                `json:"fileCount"`
	Classification      string             `json:"classification"`
	Requirements        []string           `json:"requirements"`
	ContentChanges      []string           `json:"contentChanges"`
	Changelog           string             `json:"changelog"`
	SourceURL           string             `json:"sourceUrl"`
	SupportURL          string             `json:"supportUrl"`
	Screenshots         []string           `json:"screenshots"`
	Compatible          bool               `json:"compatible"`
	CompatibilityReason string             `json:"compatibilityReason"`
	Revoked             bool               `json:"revoked"`
	Enabled             bool               `json:"enabled"`
}
type marketCatalog struct {
	SchemaVersion int             `json:"schemaVersion"`
	Packages      []marketPackage `json:"packages"`
	FetchedAt     int64           `json:"fetchedAt,omitempty"`
}
type marketSelection struct {
	PackageID string `json:"packageId"`
	Version   string `json:"version"`
	Enabled   bool   `json:"enabled"`
}
type marketRequest struct {
	ExpectedRevision string `json:"expectedRevision,omitempty"`
	deadline         time.Time
	gameFingerprint  string
	localCatalog     *marketCatalog
	cachedOnly       bool
	SchemaVersion    int                    `json:"schemaVersion"`
	OperationID      string                 `json:"operationId"`
	StateRoot        string                 `json:"stateRoot"`
	FrameworkVersion string                 `json:"frameworkVersion"`
	GameAssemblyPath string                 `json:"gameAssemblyPath"`
	Platform         string                 `json:"platform"`
	ManualRoots      []string               `json:"manualRoots"`
	ManualGUIDs      []string               `json:"manualGuids"`
	BundledGUIDs     []string               `json:"bundledGuids"`
	Selection        []marketSelection      `json:"selection"`
	DryRun           bool                   `json:"dryRun"`
	Settings         map[string]interface{} `json:"settings"`
}
type marketSnapshot struct {
	GenerationID string          `json:"generationId"`
	ContentRoot  string          `json:"contentRoot"`
	Packages     []marketPackage `json:"packages"`
}
type marketPlan struct {
	Action      string `json:"action"`
	PackageID   string `json:"packageId"`
	Name        string `json:"name"`
	FromVersion string `json:"fromVersion"`
	ToVersion   string `json:"toVersion"`
	Dependency  bool   `json:"dependency"`
}
type marketResult struct {
	PlanRevision      string          `json:"planRevision,omitempty"`
	SchemaVersion     int             `json:"schemaVersion"`
	OperationID       string          `json:"operationId"`
	OK                bool            `json:"ok"`
	Status            string          `json:"status"`
	Message           string          `json:"message"`
	CatalogAgeSeconds int64           `json:"catalogAgeSeconds"`
	Packages          []marketPackage `json:"packages"`
	Active            *marketSnapshot `json:"active"`
	Pending           *marketSnapshot `json:"pending"`
	PreviousAvailable bool            `json:"previousAvailable"`
	Plan              []marketPlan    `json:"plan"`
	ExportPath        string          `json:"exportPath,omitempty"`
	ProtocolVersion   int             `json:"protocolVersion,omitempty"`
}
type marketState struct {
	SchemaVersion int    `json:"schemaVersion"`
	OperationID   string `json:"operationId"`
	Current       string `json:"current"`
	Previous      string `json:"previous"`
	Pending       string `json:"pending"`
}
type marketFile struct {
	Path   string `json:"path"`
	SHA256 string `json:"sha256"`
	Size   int64  `json:"size"`
}
type marketLock struct {
	SchemaVersion    int                    `json:"schemaVersion"`
	Packages         []marketPackage        `json:"packages"`
	Files            []marketFile           `json:"files"`
	FrameworkVersion string                 `json:"frameworkVersion"`
	Settings         map[string]interface{} `json:"settings"`
}

func marketRead(p string, v interface{}, limit int64) error {
	f, e := os.Open(p)
	if e != nil {
		return e
	}
	defer f.Close()
	b, e := io.ReadAll(io.LimitReader(f, limit+1))
	if e != nil {
		return e
	}
	if int64(len(b)) > limit {
		return errors.New("JSON size limit exceeded")
	}
	return marketJSON(b, v)
}
func marketJSON(b []byte, v interface{}) error {
	if e := marketUniqueJSON(b); e != nil {
		return e
	}
	d := json.NewDecoder(bytes.NewReader(b))
	d.DisallowUnknownFields()
	if e := d.Decode(v); e != nil {
		return e
	}
	var extra interface{}
	if d.Decode(&extra) != io.EOF {
		return errors.New("trailing JSON")
	}
	return nil
}
func marketWrite(p string, v interface{}) error {
	b, e := json.MarshalIndent(v, "", "  ")
	if e != nil {
		return e
	}
	if len(b) > marketLimit {
		return errors.New("result exceeds size limit")
	}
	if e = os.MkdirAll(filepath.Dir(p), 0700); e != nil {
		return e
	}
	f, e := os.CreateTemp(filepath.Dir(p), ".write-")
	if e != nil {
		return e
	}
	name := f.Name()
	if _, e = f.Write(b); e == nil {
		e = f.Sync()
	}
	ce := f.Close()
	if e == nil {
		e = ce
	}
	if e != nil {
		return e
	}
	if marketWriteHook != nil {
		if e := marketWriteHook(p); e != nil {
			return e
		}
	}
	return marketReplace(name, p)
}
func marketToken() string        { b := make([]byte, 16); _, _ = rand.Read(b); return hex.EncodeToString(b) }
func marketHash(b []byte) string { h := sha256.Sum256(b); return hex.EncodeToString(h[:]) }
func marketHashFile(p string) (string, error) {
	f, e := os.Open(p)
	if e != nil {
		return "", e
	}
	defer f.Close()
	h := sha256.New()
	_, e = io.Copy(h, f)
	return hex.EncodeToString(h.Sum(nil)), e
}
func marketMainError(r marketRequest, e error) marketResult {
	status := "error"
	if strings.Contains(e.Error(), "unsupported catalog schema") {
		status = "unsupported"
	}
	return marketResult{SchemaVersion: 1, OperationID: r.OperationID, Status: status, Message: e.Error(), Packages: []marketPackage{}, Plan: []marketPlan{}}
}
func marketplaceMain(args []string) error {
	if len(args) == 0 {
		return errors.New("marketplace operation required")
	}
	op := args[0]
	fs := flag.NewFlagSet("marketplace", flag.ContinueOnError)
	req := fs.String("request", "", "request JSON")
	res := fs.String("result", "", "result JSON")
	if e := fs.Parse(args[1:]); e != nil {
		return e
	}
	if *req == "" || *res == "" || fs.NArg() != 0 {
		return errors.New("request and result paths required")
	}
	var r marketRequest
	if e := marketRead(*req, &r, marketLimit); e != nil {
		return e
	}
	out, e := marketRun(op, r)
	if e != nil {
		out = marketMainError(r, e)
	}
	if we := marketWrite(*res, out); we != nil {
		return we
	}
	return e
}
func marketRun(op string, r marketRequest) (marketResult, error) {
	r.deadline = time.Now().Add(120 * time.Second)
	if op == "catalog" {
		r.deadline = time.Now().Add(15 * time.Second)
	}
	out := marketResult{SchemaVersion: 1, OperationID: r.OperationID, OK: true, Status: "ready", Packages: []marketPackage{}, Plan: []marketPlan{}}
	if r.SchemaVersion != 1 || !marketHex.MatchString(r.OperationID) || !filepath.IsAbs(r.StateRoot) {
		return out, errors.New("unsupported request schema, operation ID or state root")
	}
	if op == "catalog" || op == "prepare" || op == "activate" {
		r.gameFingerprint, _ = marketHashFile(r.GameAssemblyPath)
	}
	if op == "protocol" {
		out.ProtocolVersion = 1
		return out, nil
	}
	if !contains([]string{"catalog", "status", "prepare", "activate", "cancel", "rollback", "export"}, op) {
		return out, errors.New("unknown marketplace operation")
	}
	if e := os.MkdirAll(r.StateRoot, 0700); e != nil {
		return out, e
	}
	unlock, e := marketAcquire(filepath.Join(r.StateRoot, "transaction.lock"))
	if e != nil {
		return out, e
	}
	defer unlock()
	state := marketState{SchemaVersion: 1}
	e = marketRead(filepath.Join(r.StateRoot, "state.json"), &state, marketLimit)
	if e != nil && !os.IsNotExist(e) {
		return out, e
	}
	if state.SchemaVersion != 1 {
		return out, errors.New("unsupported activation schema")
	}
	snapshot := func(id string) (*marketSnapshot, error) {
		if id == "" {
			return nil, nil
		}
		if !marketHex.MatchString(id) {
			return nil, errors.New("invalid generation ID")
		}
		var l marketLock
		if e := marketRead(filepath.Join(r.StateRoot, "generations", id, "lock.json"), &l, marketLimit); e != nil {
			return nil, e
		}
		if l.Packages == nil {
			l.Packages = []marketPackage{}
		}
		return &marketSnapshot{id, filepath.Join(r.StateRoot, "generations", id, "content"), l.Packages}, nil
	}
	out.Active, e = snapshot(state.Current)
	if e != nil {
		return out, e
	}
	out.Pending, e = snapshot(state.Pending)
	if e != nil && op != "activate" && op != "cancel" {
		return out, e
	}
	out.PreviousAvailable = state.Previous != ""
	switch op {
	case "catalog":
		cat, offline, e := marketGetCatalog(r)
		if e != nil {
			return out, e
		}
		marketCatalogStatus(&out, cat, offline)
	case "prepare":
		r.cachedOnly = true
		cat, _, e := marketGetCatalog(r)
		if e != nil {
			return out, e
		}
		selected, e := marketResolve(cat, r)
		if e != nil {
			return out, e
		}
		out.Plan = marketMakePlan(out.Active, selected, r.Selection)
		type revisionPackage struct {
			Dependencies []marketDependency
			ID           string
			Version      string
			SHA256       string
			Enabled      bool
		}
		revisionSelection := []revisionPackage{}
		for _, p := range selected {
			dependencies := append([]marketDependency{}, p.Dependencies...)
			sort.Slice(dependencies, func(i, j int) bool {
				return dependencies[i].PackageID+"@"+dependencies[i].Version < dependencies[j].PackageID+"@"+dependencies[j].Version
			})
			revisionSelection = append(revisionSelection, revisionPackage{dependencies, p.PackageID, p.Version, p.SHA256, p.Enabled})
		}
		revisionBytes, _ := json.Marshal(struct {
			State     marketState
			Selection []revisionPackage
			Settings  map[string]interface{}
		}{state, revisionSelection, r.Settings})
		out.PlanRevision = marketHash(revisionBytes)
		if r.DryRun {
			return out, nil
		}
		if r.localCatalog == nil && r.ExpectedRevision != out.PlanRevision {
			return out, errors.New("The mod plan changed or was not confirmed. Review the plan again before preparing.")
		}
		id := marketToken()
		dir := filepath.Join(r.StateRoot, "generations", id)
		if e = os.MkdirAll(filepath.Join(dir, "content"), 0700); e != nil {
			return out, e
		}
		lock := marketLock{SchemaVersion: 1, Files: []marketFile{}, Packages: selected, FrameworkVersion: r.FrameworkVersion, Settings: r.Settings}
		for _, p := range selected {
			if e = marketCancelled(r); e != nil {
				return out, e
			}
			b, e := marketArtifact(r, p)
			if e != nil {
				return out, e
			}
			files, e := marketExtract(b, p, filepath.Join(dir, "content", p.PackageID))
			if e != nil {
				return out, e
			}
			for _, f := range files {
				f.Path = p.PackageID + "/" + f.Path
				lock.Files = append(lock.Files, f)
			}
		}
		if e = marketCancelled(r); e != nil {
			return out, e
		}
		if e = marketWrite(filepath.Join(dir, "lock.json"), lock); e != nil {
			return out, e
		}
		state.Pending = id
		state.OperationID = r.OperationID
		if e = marketWrite(filepath.Join(r.StateRoot, "state.json"), state); e != nil {
			return out, e
		}
		out.Pending, _ = snapshot(id)
		out.Message = "Changes prepared for next launch. Existing saves can depend on the current mod set; start a new run."
	case "activate":
		if state.Pending != "" {
			ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
			defer cancel()
			if e = marketValidateGeneration(ctx, r, state.Pending); e != nil {
				out.Status = "error"
				out.Message = "Pending changes were not activated: " + e.Error()
				out.OK = false
				break
			}
			if e = ctx.Err(); e != nil {
				return out, e
			}
			if e = marketCancelled(r); e != nil {
				return out, e
			}
			state.Previous = state.Current
			if state.Previous == "" {
				state.Previous, e = marketEmptyGeneration(r)
				if e != nil {
					return out, e
				}
			}
			state.Current = state.Pending
			state.Pending = ""
			state.OperationID = r.OperationID
			if e = marketWrite(filepath.Join(r.StateRoot, "state.json"), state); e != nil {
				return out, e
			}
			out.Active, _ = snapshot(state.Current)
			out.Pending = nil
			out.PreviousAvailable = state.Previous != ""
		}
	case "cancel":
		state.Pending = ""
		state.OperationID = r.OperationID
		if e = marketWrite(filepath.Join(r.StateRoot, "state.json"), state); e != nil {
			return out, e
		}
		out.Pending = nil
	case "rollback":
		if state.Previous == "" {
			return out, errors.New("no previous generation is available")
		}
		state.Pending = state.Previous
		state.OperationID = r.OperationID
		if e = marketWrite(filepath.Join(r.StateRoot, "state.json"), state); e != nil {
			return out, e
		}
		out.Pending, _ = snapshot(state.Pending)
		out.Message = "Rollback prepared for next launch. Saves are unchanged."
	case "export":
		out.ExportPath = filepath.Join(r.StateRoot, "exports", "managed-set-"+r.OperationID+".json")
		export := map[string]interface{}{"schemaVersion": 1, "frameworkVersion": r.FrameworkVersion, "active": out.Active, "settings": r.Settings, "partial": true, "notice": "Managed packages only. Manual mods and complete co-op compatibility are not verified."}
		if e = marketWrite(out.ExportPath, export); e != nil {
			return out, e
		}
	}
	if op != "catalog" {
		if e = marketWrite(filepath.Join(r.StateRoot, "runtime-state.json"), out); e != nil {
			return out, e
		}
	}
	return out, nil
}
func contains(a []string, s string) bool {
	for _, x := range a {
		if x == s {
			return true
		}
	}
	return false
}
func marketCancelled(r marketRequest) error {
	if _, e := os.Stat(filepath.Join(r.StateRoot, "operations", r.OperationID+".cancel")); e == nil {
		return errors.New("preparation cancelled")
	}
	return nil
}
func marketURL(s string, catalog bool) error {
	u, e := url.Parse(s)
	if e != nil || u.Scheme != "https" || u.User != nil || u.Port() != "" {
		return errors.New("only approved HTTPS URLs are accepted")
	}
	if catalog {
		if s != marketCatalogURL {
			return errors.New("unapproved catalog origin")
		}
	} else if u.Host != "github.com" || !strings.HasPrefix(u.Path, "/jarlbrak/ftk-mod-framework/releases/download/") {
		return errors.New("unapproved package release host or repository")
	}
	return nil
}
func marketDownload(s string, limit int64, timeout time.Duration) ([]byte, error) {
	if e := marketURL(s, s == marketCatalogURL); e != nil {
		return nil, e
	}
	client := http.Client{Timeout: timeout, CheckRedirect: func(req *http.Request, via []*http.Request) error {
		if len(via) > 5 {
			return errors.New("too many redirects")
		}
		if req.URL.Scheme != "https" || req.URL.User != nil || !contains([]string{"github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com", "raw.githubusercontent.com"}, req.URL.Host) {
			return errors.New("redirect left approved HTTPS hosts")
		}
		return nil
	}}
	resp, e := client.Get(s)
	if e != nil {
		return nil, e
	}
	defer resp.Body.Close()
	if resp.StatusCode != 200 {
		return nil, fmt.Errorf("catalog or release returned HTTP %d", resp.StatusCode)
	}
	b, e := io.ReadAll(io.LimitReader(resp.Body, limit+1))
	if int64(len(b)) > limit {
		return nil, errors.New("download size limit exceeded")
	}
	return b, e
}
func marketGetCatalog(r marketRequest) (marketCatalog, bool, error) {
	if r.localCatalog != nil {
		return *r.localCatalog, true, marketValidateCatalog(*r.localCatalog)
	}
	if r.cachedOnly {
		var cached marketCatalog
		if e := marketRead(filepath.Join(r.StateRoot, "catalog.json"), &cached, marketLimit); e == nil {
			return cached, true, marketValidateCatalog(cached)
		}
	}
	var cat marketCatalog
	cache := filepath.Join(r.StateRoot, "catalog.json")
	timeout := time.Until(r.deadline)
	if timeout <= 0 {
		return cat, true, errors.New("catalog time budget exceeded")
	}
	b, e := marketDownload(marketCatalogURL, marketLimit, timeout)
	offline := e != nil
	if e == nil {
		e = marketJSON(b, &cat)
		if e == nil {
			e = marketValidateCatalog(cat)
		}
		if e != nil {
			return cat, false, e
		}
		var old marketCatalog
		if marketRead(cache, &old, marketLimit) == nil {
			known := map[string]string{}
			for _, p := range old.Packages {
				known[p.PackageID+"@"+p.Version] = p.SHA256
			}
			for _, p := range cat.Packages {
				if h, ok := known[p.PackageID+"@"+p.Version]; ok && h != p.SHA256 {
					return cat, false, errors.New("published version changed hash; maintainer must publish a new version")
				}
			}
		}
		cat.FetchedAt = time.Now().Unix()
		if e = marketWrite(cache, cat); e != nil {
			return cat, false, e
		}
	} else {
		e = marketRead(cache, &cat, marketLimit)
		if os.IsNotExist(e) {
			return marketCatalog{SchemaVersion: 1, Packages: []marketPackage{}, FetchedAt: 0}, true, nil
		}
		if e != nil {
			return cat, true, e
		}
		if e = marketValidateCatalog(cat); e != nil {
			return cat, true, e
		}
	}
	for i := range cat.Packages {
		reason := marketCompatibility(cat.Packages[i], r)
		cat.Packages[i].Compatible = reason == ""
		cat.Packages[i].CompatibilityReason = reason
	}
	marketScreenshots(&cat, r, offline)
	return cat, offline, nil
}
func marketValidateCatalog(c marketCatalog) error {
	if c.SchemaVersion != 1 {
		return errors.New("unsupported catalog schema")
	}
	ids := map[string]string{}
	versions := map[string]bool{}
	for _, p := range c.Packages {
		if !marketID.MatchString(p.PackageID) || !marketID.MatchString(p.ModGUID) || !marketVersion.MatchString(p.Version) || !marketSHA.MatchString(p.SHA256) || p.Name == "" || p.Author == "" || p.Description == "" || p.License == "" || p.Category == "" || p.FrameworkRange == "" || len(p.GameFingerprints) == 0 || len(p.Platforms) == 0 {
			return fmt.Errorf("invalid descriptor for %s", p.PackageID)
		}
		if p.Classification != "gameplay" && p.Classification != "dependency" {
			return errors.New("only gameplay and dependency packages are allowed")
		}
		if strings.Contains(p.ModGUID, "sampledata") || strings.Contains(p.ModGUID, "behaviorguard") || strings.Contains(p.ModGUID, "brokendll") || strings.Contains(p.ModGUID, "danglingbehavior") || strings.Contains(p.ModGUID, "samplebehaviormod") {
			return errors.New("developer fixture excluded")
		}
		if p.CompressedSize <= 0 || p.CompressedSize > archiveLimit || p.ExpandedSize <= 0 || p.ExpandedSize > expandedLimit || p.FileCount < 1 || p.FileCount > 5000 {
			return errors.New("descriptor exceeds package limits")
		}
		if e := marketURL(p.URL, false); e != nil {
			return e
		}
		if old, ok := ids[p.PackageID]; ok && old != p.ModGUID {
			return errors.New("package identity changed GUID")
		}
		for id, g := range ids {
			if id != p.PackageID && g == p.ModGUID {
				return errors.New("multiple IDs map to one GUID")
			}
		}
		ids[p.PackageID] = p.ModGUID
		key := p.PackageID + "@" + p.Version
		if versions[key] {
			return errors.New("duplicate package version")
		}
		versions[key] = true
		if len(p.Screenshots) > 3 {
			return errors.New("at most three screenshots per package")
		}
		for _, s := range p.Screenshots {
			if e := marketURL(s, false); e != nil {
				return e
			}
		}
		for _, h := range p.GameFingerprints {
			if !marketSHA.MatchString(h) {
				return errors.New("invalid game fingerprint")
			}
		}
		for _, d := range p.Dependencies {
			if !marketID.MatchString(d.PackageID) || !marketVersion.MatchString(d.Version) {
				return errors.New("invalid exact dependency")
			}
		}
	}
	return nil
}
func marketCompatibility(p marketPackage, r marketRequest) string {
	if p.Revoked {
		return "This package was revoked; new preparation is blocked."
	}
	if !contains(p.Platforms, r.Platform) {
		return "This platform has no advertised support."
	}
	if !marketRange(p.FrameworkRange, r.FrameworkVersion) {
		return "Framework version does not satisfy " + p.FrameworkRange
	}
	h := r.gameFingerprint
	if h == "" || !contains(p.GameFingerprints, h) {
		return "This game build has not been tested for this package."
	}
	return ""
}
func marketRange(s, v string) bool {
	if !marketVersion.MatchString(v) {
		return false
	}
	if marketVersion.MatchString(s) {
		return s == v
	}
	parts := strings.Fields(s)
	if len(parts) != 2 || !strings.HasPrefix(parts[0], ">=") || !strings.HasPrefix(parts[1], "<") {
		return false
	}
	lo, hi := parts[0][2:], parts[1][1:]
	return marketVersion.MatchString(lo) && marketVersion.MatchString(hi) && marketCompare(v, lo) >= 0 && marketCompare(v, hi) < 0
}
func marketCompare(a, b string) int {
	var x, y [3]int
	fmt.Sscanf(a, "%d.%d.%d", &x[0], &x[1], &x[2])
	fmt.Sscanf(b, "%d.%d.%d", &y[0], &y[1], &y[2])
	for i := range x {
		if x[i] < y[i] {
			return -1
		}
		if x[i] > y[i] {
			return 1
		}
	}
	return 0
}
func marketResolve(c marketCatalog, r marketRequest) ([]marketPackage, error) {
	index := map[string]marketPackage{}
	for _, p := range c.Packages {
		index[p.PackageID+"@"+p.Version] = p
	}
	chosen := map[string]marketPackage{}
	visiting := map[string]bool{}
	var visit func(string, string, bool) error
	visit = func(id, v string, enabled bool) error {
		if visiting[id] {
			return errors.New("dependency cycle at " + id)
		}
		if p, ok := chosen[id]; ok {
			if p.Version != v {
				return errors.New("conflicting exact versions for " + id)
			}
			if enabled && !p.Enabled {
				p.Enabled = true
				chosen[id] = p
				for _, d := range p.Dependencies {
					if e := visit(d.PackageID, d.Version, true); e != nil {
						return e
					}
				}
			}
			return nil
		}
		p, ok := index[id+"@"+v]
		if !ok {
			return errors.New("missing exact package " + id + "@" + v)
		}
		if why := marketCompatibility(p, r); why != "" {
			return errors.New(p.Name + ": " + why)
		}
		visiting[id] = true
		for _, d := range p.Dependencies {
			if e := visit(d.PackageID, d.Version, enabled); e != nil {
				return e
			}
		}
		visiting[id] = false
		p.Enabled = enabled
		p.Compatible = true
		p.CompatibilityReason = ""
		chosen[id] = p
		return nil
	}
	for _, s := range r.Selection {
		if e := visit(s.PackageID, s.Version, s.Enabled); e != nil {
			return nil, e
		}
	}
	out := []marketPackage{}
	var expanded, compressed int64
	files := 0
	for _, p := range chosen {
		out = append(out, p)
		expanded += p.ExpandedSize
		compressed += p.CompressedSize
		files += p.FileCount
	}
	if expanded > expandedLimit || compressed > archiveLimit || files > 5000 {
		return nil, errors.New("complete generation exceeds size or file limits")
	}
	sort.Slice(out, func(i, j int) bool { return out[i].ModGUID < out[j].ModGUID })
	return out, nil
}
func marketMakePlan(active *marketSnapshot, selected []marketPackage, explicit []marketSelection) []marketPlan {
	old := map[string]marketPackage{}
	direct := map[string]bool{}
	for _, s := range explicit {
		direct[s.PackageID] = true
	}
	if active != nil {
		for _, p := range active.Packages {
			old[p.PackageID] = p
		}
	}
	out := []marketPlan{}
	for _, p := range selected {
		o, ok := old[p.PackageID]
		action := "install"
		if ok {
			action = "keep"
			if o.Version != p.Version {
				action = "update"
			} else if o.Enabled != p.Enabled {
				action = "disable"
				if p.Enabled {
					action = "enable"
				}
			}
		}
		out = append(out, marketPlan{action, p.PackageID, p.Name, o.Version, p.Version, !direct[p.PackageID]})
		delete(old, p.PackageID)
	}
	for _, p := range old {
		out = append(out, marketPlan{"remove", p.PackageID, p.Name, p.Version, "", false})
	}
	sort.Slice(out, func(i, j int) bool { return out[i].PackageID < out[j].PackageID })
	return out
}
func marketArtifact(r marketRequest, p marketPackage) ([]byte, error) {
	cache := filepath.Join(r.StateRoot, "artifacts", p.SHA256+".zip")
	b, e := marketReadBytes(cache, archiveLimit)
	if e != nil {
		timeout := time.Until(r.deadline)
		if timeout <= 0 {
			return nil, errors.New("preparation time budget exceeded")
		}
		b, e = marketDownload(p.URL, archiveLimit, timeout)
		if e != nil {
			return nil, e
		}
	}
	if int64(len(b)) != p.CompressedSize || marketHash(b) != p.SHA256 {
		return nil, errors.New("archive size or SHA-256 mismatch for " + p.Name)
	}
	if e = os.MkdirAll(filepath.Dir(cache), 0700); e != nil {
		return nil, e
	}
	if e = os.WriteFile(cache, b, 0600); e != nil {
		return nil, e
	}
	return b, nil
}
func marketSafePath(s string) bool {
	if s == "" || strings.ContainsAny(s, "\\:\x00") || strings.HasPrefix(s, "/") || path.Clean(s) != s {
		return false
	}
	for _, part := range strings.Split(s, "/") {
		if part == ".." || part == "." || strings.HasSuffix(part, ".") || strings.HasSuffix(part, " ") {
			return false
		}
		base := strings.ToUpper(strings.Split(part, ".")[0])
		if contains([]string{"CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"}, base) {
			return false
		}
	}
	return true
}
func marketExtract(b []byte, p marketPackage, dest string) ([]marketFile, error) {
	z, e := zip.NewReader(bytes.NewReader(b), int64(len(b)))
	if e != nil {
		return nil, e
	}
	if len(z.File) > 5000 {
		return nil, errors.New("archive file count exceeded")
	}
	seen := map[string]string{}
	explicit := map[string]bool{}
	data := map[string][]byte{}
	contentIDs := map[string]bool{}
	var total int64
	count := 0
	manifest := false
	for _, f := range z.File {
		name := strings.TrimSuffix(f.Name, "/")
		if !marketSafePath(name) || f.Mode()&os.ModeSymlink != 0 || f.Mode()&os.ModeType != 0 && !f.FileInfo().IsDir() {
			return nil, errors.New("unsafe archive path or file type: " + f.Name)
		}
		lower := strings.ToLower(name)
		if old, ok := seen[lower]; ok && (old != name || explicit[lower]) {
			return nil, errors.New("duplicate or case-colliding archive path")
		}
		explicit[lower] = true
		seen[lower] = name
		for parent := path.Dir(name); parent != "."; parent = path.Dir(parent) {
			if old, ok := seen[strings.ToLower(parent)]; ok && old != parent {
				return nil, errors.New("case-colliding directory")
			}
			seen[strings.ToLower(parent)] = parent
		}
		if f.FileInfo().IsDir() {
			continue
		}
		count++
		if f.UncompressedSize64 > expandedLimit {
			return nil, errors.New("expanded file limit exceeded")
		}
		total += int64(f.UncompressedSize64)
		if total > expandedLimit {
			return nil, errors.New("expanded archive limit exceeded")
		}
		ext := strings.ToLower(path.Ext(name))
		if ext != ".json" && ext != ".png" && ext != ".jpg" && ext != ".jpeg" {
			return nil, errors.New("unsupported archive file: " + name)
		}
		rd, e := f.Open()
		if e != nil {
			return nil, e
		}
		raw, e := io.ReadAll(io.LimitReader(rd, expandedLimit+1))
		rd.Close()
		if e != nil {
			return nil, e
		}
		if int64(len(raw)) != int64(f.UncompressedSize64) {
			return nil, errors.New("expanded size mismatch")
		}
		if ext == ".json" {
			if path.Ext(name) != ".json" {
				return nil, errors.New("runtime JSON extension must be lowercase")
			}
			if strings.Contains(name, "/") {
				return nil, errors.New("runtime JSON must be in the package root")
			}
			if len(raw) > marketLimit {
				return nil, errors.New("content JSON exceeds limit")
			}
			if name == "manifest.json" {
				if e = marketManifest(raw, p); e != nil {
					return nil, e
				}
				manifest = true
			} else {
				if path.Base(name) == "manifest.json" {
					return nil, errors.New("nested manifest not allowed")
				}
				if e = marketContent(raw); e != nil {
					return nil, fmt.Errorf("%s: %w", name, e)
				}
			}
		} else {
			if !strings.HasPrefix(name, "assets/") {
				return nil, errors.New("images must be inside assets/")
			}
			if ext == ".png" && !bytes.HasPrefix(raw, []byte{137, 80, 78, 71, 13, 10, 26, 10}) {
				return nil, errors.New("invalid PNG signature")
			}
			if (ext == ".jpg" || ext == ".jpeg") && !bytes.HasPrefix(raw, []byte{255, 216, 255}) {
				return nil, errors.New("invalid JPEG signature")
			}
		}
		if ext == ".json" && name != "manifest.json" {
			var c struct {
				Entries []struct {
					ID string `json:"id"`
				} `json:"entries"`
			}
			json.Unmarshal(raw, &c)
			for _, entry := range c.Entries {
				if contentIDs[entry.ID] {
					return nil, errors.New("duplicate content ID across package files")
				}
				contentIDs[entry.ID] = true
			}
		}
		data[name] = raw
	}
	if !manifest || total != p.ExpandedSize || count != p.FileCount {
		return nil, errors.New("manifest missing or descriptor expanded size/file count mismatch")
	}
	files := []marketFile{}
	for name, raw := range data {
		files = append(files, marketFile{name, marketHash(raw), int64(len(raw))})
	}
	sort.Slice(files, func(i, j int) bool { return files[i].Path < files[j].Path })
	if dest != "" {
		for _, f := range files {
			target := filepath.Join(dest, filepath.FromSlash(f.Path))
			if e = os.MkdirAll(filepath.Dir(target), 0700); e != nil {
				return nil, e
			}
			if e = os.WriteFile(target, data[f.Path], 0600); e != nil {
				return nil, e
			}
		}
	}
	return files, nil
}
func marketManifest(b []byte, p marketPackage) error {
	var m struct {
		ModGUID         string `json:"modGuid"`
		Name            string `json:"name"`
		Version         string `json:"version"`
		Description     string `json:"description"`
		Author          string `json:"author"`
		DevelopmentOnly bool   `json:"developmentOnly"`
	}
	if e := marketJSON(b, &m); e != nil {
		return e
	}
	if m.ModGUID != p.ModGUID || m.Version != p.Version || m.Name == "" || m.DevelopmentOnly {
		return errors.New("runtime manifest identity mismatch or developer fixture")
	}
	return nil
}
func marketContent(b []byte) error {
	var c struct {
		Entries []struct {
			Kind          string                 `json:"kind"`
			ID            string                 `json:"id"`
			Template      string                 `json:"template"`
			DisplayName   string                 `json:"displayName"`
			Fields        map[string]interface{} `json:"fields"`
			Proficiencies []string               `json:"proficiencies"`
			Flavor        string                 `json:"flavor"`
			Description   string                 `json:"description"`
		} `json:"entries"`
	}
	if e := marketJSON(b, &c); e != nil {
		return e
	}
	if c.Entries == nil || len(c.Entries) > 10000 {
		return errors.New("invalid entries")
	}
	seen := map[string]bool{}
	for _, entry := range c.Entries {
		if !contains([]string{"item", "weapon", "proficiency", "class", "enemy", "encounter"}, entry.Kind) || entry.ID == "" || entry.Template == "" {
			return errors.New("unsupported kind or missing identity/template")
		}
		key := entry.ID
		if seen[key] {
			return errors.New("duplicate content ID")
		}
		seen[key] = true
		if e := marketAllowedFields(entry.Kind, entry.Fields); e != nil {
			return e
		}
		if e := marketValues(entry.Fields, 0); e != nil {
			return e
		}
	}
	return nil
}
func marketValues(v interface{}, depth int) error {
	if depth > 20 {
		return errors.New("JSON nesting limit exceeded")
	}
	switch x := v.(type) {
	case map[string]interface{}:
		for k, value := range x {
			if strings.HasPrefix(k, "$") || strings.ContainsAny(k, "/\\") || len(k) > 256 {
				return errors.New("serialized type metadata or invalid field")
			}
			if e := marketValues(value, depth+1); e != nil {
				return e
			}
		}
	case []interface{}:
		if len(x) > 10000 {
			return errors.New("array limit exceeded")
		}
		for _, v := range x {
			if e := marketValues(v, depth+1); e != nil {
				return e
			}
		}
	case string:
		if len(x) > 65536 {
			return errors.New("string limit exceeded")
		}
	}
	return nil
}
func marketValidateGeneration(ctx context.Context, r marketRequest, id string) error {
	if !marketHex.MatchString(id) {
		return errors.New("invalid pending generation ID")
	}
	root := filepath.Join(r.StateRoot, "generations", id)
	var lock marketLock
	if e := marketRead(filepath.Join(root, "lock.json"), &lock, marketLimit); e != nil {
		return e
	}
	if lock.SchemaVersion != 1 || len(lock.Files) > 5000 {
		return errors.New("unsupported or oversized generation lock")
	}
	if e := marketValidateCatalog(marketCatalog{SchemaVersion: 1, Packages: lock.Packages}); e != nil {
		return e
	}
	conflicts := map[string]string{}
	for _, g := range append(append([]string{}, r.ManualGUIDs...), r.BundledGUIDs...) {
		conflicts[g] = "manual/bundled content"
	}
	for _, manual := range r.ManualRoots {
		folders, e := os.ReadDir(manual)
		if os.IsNotExist(e) {
			continue
		}
		if e != nil {
			return e
		}
		if len(folders) > 5000 {
			return errors.New("manual discovery folder limit exceeded")
		}
		for _, folder := range folders {
			if ctx.Err() != nil {
				return ctx.Err()
			}
			full := filepath.Join(manual, folder.Name())
			info, e := os.Stat(full)
			if e != nil || !info.IsDir() {
				continue
			}
			manifestPath := filepath.Join(full, "manifest.json")
			data, e := marketReadBytes(manifestPath, marketLimit)
			if os.IsNotExist(e) {
				continue
			}
			if e != nil {
				return fmt.Errorf("manual manifest %s: %w", manifestPath, e)
			}
			var m struct {
				ModGUID         string `json:"modGuid"`
				Name            string `json:"name"`
				Version         string `json:"version"`
				DevelopmentOnly bool   `json:"developmentOnly"`
			}
			if json.Unmarshal(data, &m) != nil || strings.TrimSpace(m.ModGUID) == "" || strings.TrimSpace(m.Name) == "" || strings.TrimSpace(m.Version) == "" {
				continue
			}
			dev, _ := r.Settings["RunSelfTests"].(bool)
			if !dev && (m.DevelopmentOnly || marketDeveloperGUID(m.ModGUID)) {
				continue
			}
			if m.ModGUID == "com.ftkmf.synthetic" && folder.Name() != "__ftkmf_synthetic__" {
				continue
			}
			conflicts[m.ModGUID] = manifestPath
		}
	}

	packages := map[string]marketPackage{}
	for _, p := range lock.Packages {
		if source, ok := conflicts[p.ModGUID]; ok {
			return fmt.Errorf("GUID %s conflicts with %s", p.ModGUID, source)
		}
		if why := marketCompatibility(p, r); why != "" {
			return errors.New(p.Name + ": " + why)
		}
		packages[p.PackageID] = p
	}
	for _, p := range lock.Packages {
		for _, d := range p.Dependencies {
			actual, ok := packages[d.PackageID]
			if !ok || actual.Version != d.Version || p.Enabled && !actual.Enabled {
				return errors.New("invalid locked dependency selection")
			}
		}
	}
	expected := map[string]bool{}
	var total int64
	for _, f := range lock.Files {
		if ctx.Err() != nil {
			return ctx.Err()
		}
		if !marketSafePath(f.Path) || !marketSHA.MatchString(f.SHA256) || f.Size < 0 {
			return errors.New("unsafe generation file record")
		}
		if expected[strings.ToLower(f.Path)] {
			return errors.New("duplicate generation path")
		}
		expected[strings.ToLower(f.Path)] = true
		full := filepath.Join(root, "content", filepath.FromSlash(f.Path))
		if e := marketNoSymlink(root, full); e != nil {
			return e
		}
		info, e := os.Lstat(full)
		if e != nil {
			return e
		}
		if !info.Mode().IsRegular() || info.Size() != f.Size {
			return errors.New("generation file changed")
		}
		total += f.Size
		if total > expandedLimit {
			return errors.New("generation expansion limit exceeded")
		}
		h, e := marketHashFile(full)
		if e != nil || h != f.SHA256 {
			return errors.New("generation hash mismatch")
		}
	}
	count := 0
	e := filepath.Walk(filepath.Join(root, "content"), func(p string, info os.FileInfo, e error) error {
		if ctx.Err() != nil {
			return ctx.Err()
		}
		if e != nil {
			return e
		}
		if info.Mode()&os.ModeSymlink != 0 {
			return errors.New("symlink in generation")
		}
		if info.IsDir() {
			return nil
		}
		rel, e := filepath.Rel(filepath.Join(root, "content"), p)
		if e != nil {
			return e
		}
		if !expected[strings.ToLower(filepath.ToSlash(rel))] {
			return errors.New("untracked file in generation")
		}
		count++
		return nil
	})
	if e != nil {
		return e
	}
	if count != len(lock.Files) {
		return errors.New("generation file count mismatch")
	}
	return ctx.Err()
}
func marketReadBytes(p string, limit int64) ([]byte, error) {
	f, e := os.Open(p)
	if e != nil {
		return nil, e
	}
	defer f.Close()
	b, e := io.ReadAll(io.LimitReader(f, limit+1))
	if int64(len(b)) > limit {
		return nil, errors.New("file size limit exceeded")
	}
	return b, e
}

// Local author tooling is a separate CLI entry point, never an in-game request operation.
func marketDeveloper(args []string) error {
	if args[0] == "marketplace-catalog-validate" {
		fs := flag.NewFlagSet(args[0], flag.ContinueOnError)
		catalog := fs.String("catalog", "", "local catalog JSON")
		if e := fs.Parse(args[1:]); e != nil {
			return e
		}
		if *catalog == "" || fs.NArg() != 0 {
			return errors.New("catalog path required")
		}
		var c marketCatalog
		if e := marketRead(*catalog, &c, marketLimit); e != nil {
			return e
		}
		if e := marketValidateCatalog(c); e != nil {
			return e
		}
		fmt.Println("Catalog validation passed. Release and game review remain required.")
		return nil
	}
	fs := flag.NewFlagSet(args[0], flag.ContinueOnError)
	descriptor := fs.String("descriptor", "", "local descriptor JSON")
	archive := fs.String("archive", "", "local ZIP")
	request := fs.String("request", "", "fixture request JSON")
	result := fs.String("result", "", "fixture result JSON")
	if e := fs.Parse(args[1:]); e != nil {
		return e
	}
	if *descriptor == "" || *archive == "" || fs.NArg() != 0 {
		return errors.New("descriptor and archive required")
	}
	var p marketPackage
	if e := marketRead(*descriptor, &p, marketLimit); e != nil {
		return e
	}
	cat := marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p}, FetchedAt: time.Now().Unix()}
	if e := marketValidateCatalog(cat); e != nil {
		return e
	}
	b, e := marketReadBytes(*archive, archiveLimit)
	if e != nil {
		return e
	}
	if int64(len(b)) != p.CompressedSize || marketHash(b) != p.SHA256 {
		return errors.New("archive size or hash mismatch")
	}
	if _, e = marketExtract(b, p, ""); e != nil {
		return e
	}
	if args[0] == "marketplace-validate" {
		fmt.Println("Package descriptor and archive validation passed. Game review remains required.")
		return nil
	}
	if *request == "" || *result == "" {
		return errors.New("fixture request and result required")
	}
	var r marketRequest
	if e = marketRead(*request, &r, marketLimit); e != nil {
		return e
	}
	if !filepath.IsAbs(r.StateRoot) || !marketHex.MatchString(r.OperationID) {
		return errors.New("invalid fixture request")
	}
	if e = os.MkdirAll(filepath.Join(r.StateRoot, "artifacts"), 0700); e != nil {
		return e
	}
	if e = os.WriteFile(filepath.Join(r.StateRoot, "artifacts", p.SHA256+".zip"), b, 0600); e != nil {
		return e
	}
	r.localCatalog = &cat
	r.Selection = []marketSelection{{p.PackageID, p.Version, true}}
	out, e := marketRun("prepare", r)
	if e != nil {
		out = marketMainError(r, e)
	}
	if we := marketWrite(*result, out); we != nil {
		return we
	}
	return e
}
func marketEmptyGeneration(r marketRequest) (string, error) {
	id := marketToken()
	dir := filepath.Join(r.StateRoot, "generations", id)
	if e := os.MkdirAll(filepath.Join(dir, "content"), 0700); e != nil {
		return "", e
	}
	e := marketWrite(filepath.Join(dir, "lock.json"), marketLock{SchemaVersion: 1, Packages: []marketPackage{}, Files: []marketFile{}, FrameworkVersion: r.FrameworkVersion, Settings: r.Settings})
	return id, e
}
func marketDeveloperGUID(g string) bool {
	return contains([]string{"com.ftkmf.behaviorguard", "com.ftkmf.brokendll", "com.ftkmf.danglingbehavior", "com.ftkmf.samplebehaviormod", "com.ftkmf.sampledata"}, g)
}

// Deliberately narrower than the manual loader's raw field escape hatch. These
// aliases and exact serialized targets are verified in Core/Data/AliasTable.cs.
func marketAllowedFields(kind string, fields map[string]interface{}) error {
	aliases := map[string]string{}
	add := func(spec string) {
		for _, pair := range strings.Fields(spec) {
			kv := strings.Split(pair, "=")
			aliases[kv[0]] = kv[1]
		}
	}
	if kind == "item" || kind == "weapon" {
		add("rarity=m_ItemRarity goldvalue=_goldValue minlevel=m_MinLevel maxlevel=m_MaxLevel dropable=m_Dropable townmarket=m_TownMarket dlc=m_DLC")
	}
	switch kind {
	case "weapon":
		add("damage=_maxdmg damagetype=_dmgtype skill=_skilltest slots=_slots damagegain=_dmggain")
	case "proficiency":
		add("damage=m_DmgMultiplier ignoresarmor=m_IgnoresArmor chancetoaffect=m_ChanceToAffect slots=m_SlotOverride")
	case "class":
		add("strength=_toughness intelligence=_fortitude awareness=_awareness talent=_talent speed=_quickness vitality=_vitality startinggold=_startinggold focus=_basefocus primarystat=m_PrimaryWeaponStat startweapon=m_StartWeapon startitems=m_StartItems dlc=m_DLC")
	}
	resolved := map[string]bool{}
	for key, value := range fields {
		target, ok := aliases[strings.ToLower(key)]
		if !ok {
			for _, raw := range aliases {
				if raw == key {
					target = raw
					ok = true
					break
				}
			}
		}
		if !ok {
			return errors.New("unsupported marketplace field " + kind + "." + key)
		}
		if resolved[target] {
			return errors.New("duplicate alias/raw field " + target)
		}
		resolved[target] = true
		switch v := value.(type) {
		case map[string]interface{}:
			return errors.New("nested objects are not supported in marketplace fields")
		case []interface{}:
			if target != "m_StartItems" {
				return errors.New("unsupported array field")
			}
			for _, item := range v {
				if _, ok := item.(string); !ok {
					return errors.New("starting items must be string IDs")
				}
			}
		case string, bool, float64:
		default:
			return errors.New("unsupported marketplace field value")
		}
	}
	return nil
}
func marketUniqueJSON(b []byte) error {
	d := json.NewDecoder(bytes.NewReader(b))
	var read func(int) error
	read = func(depth int) error {
		if depth > 32 {
			return errors.New("JSON depth limit exceeded")
		}
		token, e := d.Token()
		if e != nil {
			return e
		}
		switch token {
		case json.Delim('{'):
			seen := map[string]bool{}
			for d.More() {
				key, e := d.Token()
				if e != nil {
					return e
				}
				s, ok := key.(string)
				if !ok || seen[s] {
					return errors.New("duplicate JSON property")
				}
				seen[s] = true
				if e = read(depth + 1); e != nil {
					return e
				}
			}
			_, e = d.Token()
			return e
		case json.Delim('['):
			for d.More() {
				if e = read(depth + 1); e != nil {
					return e
				}
			}
			_, e = d.Token()
			return e
		}
		return nil
	}
	if e := read(0); e != nil {
		return e
	}
	if _, e := d.Token(); e != io.EOF {
		return errors.New("trailing JSON")
	}
	return nil
}
func marketImage(b []byte) error {
	if len(b) > marketLimit {
		return errors.New("image exceeds 2 MiB")
	}
	config, kind, e := image.DecodeConfig(bytes.NewReader(b))
	if e != nil {
		return e
	}
	if (kind != "png" && kind != "jpeg") || config.Width <= 0 || config.Height <= 0 || config.Width > 4096 || config.Height > 4096 || config.Width*config.Height > 8388608 {
		return errors.New("unsupported image dimensions")
	}
	return nil
}
func marketScreenshots(c *marketCatalog, r marketRequest, offline bool) {
	deadline := r.deadline
	downloads := 0
	for i := range c.Packages {
		p := &c.Packages[i]
		p.ScreenshotPaths = nil
		for _, s := range p.Screenshots {
			if marketURL(s, false) != nil {
				continue
			}
			ext := ".png"
			if strings.HasSuffix(strings.ToLower(s), ".jpg") || strings.HasSuffix(strings.ToLower(s), ".jpeg") {
				ext = ".jpg"
			}
			local := filepath.Join(r.StateRoot, "cache", "screenshots", marketHash([]byte(s))+ext)
			b, e := marketReadBytes(local, marketLimit)
			if e != nil && !offline && downloads < 12 && time.Until(deadline) > 0 {
				downloads++
				timeout := time.Until(deadline)
				if timeout > 3*time.Second {
					timeout = 3 * time.Second
				}
				b, e = marketDownload(s, marketLimit, timeout)
				if e == nil && marketImage(b) == nil {
					if os.MkdirAll(filepath.Dir(local), 0700) == nil {
						e = os.WriteFile(local, b, 0600)
					}
				}
			}
			if e == nil && marketImage(b) == nil {
				p.ScreenshotPaths = append(p.ScreenshotPaths, local)
			}
		}
	}
}
func marketNoSymlink(root, full string) error {
	for p := full; ; p = filepath.Dir(p) {
		info, e := os.Lstat(p)
		if e != nil {
			return e
		}
		if info.Mode()&os.ModeSymlink != 0 {
			return errors.New("symlink in generation path")
		}
		if p == root {
			break
		}
		if filepath.Dir(p) == p {
			return errors.New("generation path escaped root")
		}
	}
	return nil
}

func marketCatalogStatus(out *marketResult, cat marketCatalog, offline bool) {
	out.Packages = cat.Packages
	out.CatalogAgeSeconds = time.Now().Unix() - cat.FetchedAt
	if out.CatalogAgeSeconds < 0 {
		out.CatalogAgeSeconds = 0
	}
	out.Status = "online"
	if offline {
		if cat.FetchedAt == 0 {
			out.Status = "unavailable"
			out.CatalogAgeSeconds = -1
			out.Message = "Catalog unavailable; no cached packages."
		} else {
			out.Status = "offline"
			out.Message = "Showing the last verified catalog. Downloads may be unavailable."
		}
	} else if len(cat.Packages) == 0 {
		out.Status = "empty"
		out.Message = "The community catalog has no published packages yet."
	}
}
