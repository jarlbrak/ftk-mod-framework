package main

import (
	"archive/zip"
	"bufio"
	"bytes"
	"context"
	"encoding/json"
	"image"
	"image/png"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

func marketFixture(t *testing.T) (marketRequest, marketPackage, []byte) {
	t.Helper()
	root := t.TempDir()
	game := filepath.Join(root, "Assembly-CSharp.dll")
	os.WriteFile(game, []byte("test-game-fingerprint"), 0600)
	r := marketRequest{SchemaVersion: 1, OperationID: strings.Repeat("a", 32), StateRoot: filepath.Join(root, "marketplace"), FrameworkVersion: "0.1.0", GameAssemblyPath: game, Platform: "macos"}
	r.gameFingerprint, _ = marketHashFile(game)
	p := marketPackage{PackageID: "community.test", ModGUID: "com.community.test", Name: "Test equipment", Author: "Community", Description: "Test JSON content", Category: "items", Version: "1.0.0", License: "MIT", FrameworkVersion: "0.1.0", FrameworkRange: ">=0.1.0 <1.0.0", GameFingerprints: []string{r.gameFingerprint}, Platforms: []string{"macos"}, Classification: "gameplay", URL: "https://github.com/jarlbrak/ftk-mod-framework/releases/download/test/test.zip"}
	files := map[string]string{"manifest.json": `{"modGuid":"com.community.test","name":"Test equipment","version":"1.0.0","frameworkVersion":"0.1.0"}`, "items.json": `{"entries":[{"kind":"weapon","id":"test_blade","template":"bladeDagger","displayName":"Test Blade","fields":{}}]}`}
	b := marketZip(t, files)
	p.CompressedSize = int64(len(b))
	for _, v := range files {
		p.ExpandedSize += int64(len(v))
	}
	p.FileCount = len(files)
	p.SHA256 = marketHash(b)
	r.Selection = []marketSelection{{p.PackageID, p.Version, true}}
	r.localCatalog = &marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p}}
	os.MkdirAll(filepath.Join(r.StateRoot, "artifacts"), 0700)
	os.WriteFile(filepath.Join(r.StateRoot, "artifacts", p.SHA256+".zip"), b, 0600)
	return r, p, b
}
func marketZip(t *testing.T, files map[string]string) []byte {
	t.Helper()
	var b bytes.Buffer
	z := zip.NewWriter(&b)
	for name, data := range files {
		w, e := z.Create(name)
		if e != nil {
			t.Fatal(e)
		}
		w.Write([]byte(data))
	}
	if e := z.Close(); e != nil {
		t.Fatal(e)
	}
	return b.Bytes()
}
func TestMarketplacePrepareActivateRollback(t *testing.T) {
	r, _, _ := marketFixture(t)
	r.DryRun = true
	plan, e := marketRun("prepare", r)
	if e != nil || len(plan.Plan) != 1 || plan.Plan[0].Action != "install" || plan.Pending != nil {
		t.Fatalf("dry run: %+v %v", plan, e)
	}
	r.DryRun = false
	prepared, e := marketRun("prepare", r)
	if e != nil || prepared.Active != nil || prepared.Pending == nil {
		t.Fatalf("prepare: %+v %v", prepared, e)
	}
	activated, e := marketRun("activate", r)
	if e != nil || !activated.OK || activated.Active == nil || activated.Pending != nil {
		t.Fatalf("activate: %+v %v", activated, e)
	}
	first := activated.Active.GenerationID
	r.Selection = nil
	prepared, e = marketRun("prepare", r)
	if e != nil || prepared.Active.GenerationID != first || len(prepared.Plan) != 1 || prepared.Plan[0].Action != "remove" {
		t.Fatalf("remove: %+v %v", prepared, e)
	}
	activated, e = marketRun("activate", r)
	if e != nil || !activated.OK || !activated.PreviousAvailable {
		t.Fatalf("empty activate: %+v %v", activated, e)
	}
	rollback, e := marketRun("rollback", r)
	if e != nil || rollback.Pending.GenerationID != first || rollback.Active.GenerationID == first {
		t.Fatalf("rollback: %+v %v", rollback, e)
	}
	activated, e = marketRun("activate", r)
	if e != nil || !activated.OK || activated.Active.GenerationID != first {
		t.Fatalf("rollback activation: %+v %v", activated, e)
	}
}
func TestMarketplaceFailurePreservesActive(t *testing.T) {
	for _, mode := range []string{"hash", "extra", "manual", "bundled", "cancel"} {
		t.Run(mode, func(t *testing.T) {
			r, p, _ := marketFixture(t)
			marketRun("prepare", r)
			prior, e := marketRun("activate", r)
			if e != nil || !prior.OK {
				t.Fatal(e, prior)
			}
			next, e := marketRun("prepare", r)
			if e != nil {
				t.Fatal(e)
			}
			switch mode {
			case "hash":
				os.WriteFile(filepath.Join(next.Pending.ContentRoot, p.PackageID, "items.json"), []byte("corrupt"), 0600)
			case "extra":
				os.WriteFile(filepath.Join(next.Pending.ContentRoot, p.PackageID, "evil.dll"), []byte("bad"), 0600)
			case "manual":
				dir := filepath.Join(t.TempDir(), "manual")
				os.MkdirAll(dir, 0700)
				os.WriteFile(filepath.Join(dir, "manifest.json"), []byte(`{"modGuid":"`+p.ModGUID+`","name":"Manual","version":"1.0.0"}`), 0600)
				r.ManualRoots = []string{filepath.Dir(dir)}
			case "bundled":
				r.BundledGUIDs = []string{p.ModGUID}
			case "cancel":
				os.MkdirAll(filepath.Join(r.StateRoot, "operations"), 0700)
				os.WriteFile(filepath.Join(r.StateRoot, "operations", r.OperationID+".cancel"), nil, 0600)
			}
			result, e := marketRun("activate", r)
			if e == nil && result.OK {
				t.Fatal("expected rejected activation")
			}
			var state marketState
			if e := marketRead(filepath.Join(r.StateRoot, "state.json"), &state, marketLimit); e != nil {
				t.Fatal(e)
			}
			if state.Current != prior.Active.GenerationID {
				t.Fatal("active pointer changed")
			}
		})
	}
}
func TestMarketplaceArchiveGuards(t *testing.T) {
	_, p, _ := marketFixture(t)
	base := `{"modGuid":"com.community.test","name":"Test","version":"1.0.0","frameworkVersion":"0.1.0"}`
	for _, name := range []string{"../evil.json", "/evil.json", "C:/evil.json", "folder\\evil.json", "evil.dll", "evil.ps1", "assets/fake.png", "MANIFEST.json", "content/../../evil.json", "con.json", "foo./bar.json"} {
		t.Run(name, func(t *testing.T) {
			files := map[string]string{"manifest.json": base, name: `{"entries":[]}`}
			b := marketZip(t, files)
			p.ExpandedSize = 0
			for _, v := range files {
				p.ExpandedSize += int64(len(v))
			}
			p.FileCount = len(files)
			if _, e := marketExtract(b, p, ""); e == nil {
				t.Fatal("accepted unsafe archive")
			}
		})
	}
	var b bytes.Buffer
	z := zip.NewWriter(&b)
	h := &zip.FileHeader{Name: "link.json"}
	h.SetMode(os.ModeSymlink | 0777)
	w, _ := z.CreateHeader(h)
	w.Write([]byte("/etc/passwd"))
	z.Close()
	if _, e := marketExtract(b.Bytes(), p, ""); e == nil {
		t.Fatal("accepted symlink")
	}
}
func TestMarketplaceJSONGuards(t *testing.T) {
	for _, s := range []string{`{"entries":[{"kind":"campaign","id":"a","template":"b"}]}`, `{"entries":[{"kind":"weapon","id":"a","template":"b","behavior":"evil"}]}`, `{"entries":[{"kind":"weapon","id":"a","template":"b","fields":{"$type":"evil"}}]}`, `{"entries":[]} {}`, `{"$type":"evil","entries":[]}`} {
		if marketContent([]byte(s)) == nil {
			t.Fatal("accepted", s)
		}
	}
}
func TestMarketplaceDependencyGuards(t *testing.T) {
	r, p, _ := marketFixture(t)
	dependency := p
	dependency.PackageID = "community.dep"
	dependency.ModGUID = "com.community.dep"
	dependency.Classification = "dependency"
	p.Dependencies = []marketDependency{{dependency.PackageID, dependency.Version}}
	cat := marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p, dependency}}
	selected, e := marketResolve(cat, r)
	if e != nil || len(selected) != 2 || !selected[0].Enabled {
		t.Fatal(selected, e)
	}
	dependency.Dependencies = []marketDependency{{p.PackageID, p.Version}}
	cat.Packages[1] = dependency
	if _, e = marketResolve(cat, r); e == nil {
		t.Fatal("accepted cycle")
	}
	cat.Packages = cat.Packages[:1]
	if _, e = marketResolve(cat, r); e == nil {
		t.Fatal("accepted missing dependency")
	}
	p.Revoked = true
	cat.Packages = []marketPackage{p}
	if _, e = marketResolve(cat, r); e == nil {
		t.Fatal("accepted revoked package")
	}
}
func TestMarketplaceLockAndAtomicState(t *testing.T) {
	root := t.TempDir()
	lock := filepath.Join(root, "lock")
	unlock, e := marketAcquire(lock)
	if e != nil {
		t.Fatal(e)
	}
	if unlock2, e := marketAcquire(lock); e == nil {
		unlock2()
		t.Fatal("concurrent lock accepted")
	}
	unlock()
	unlock, e = marketAcquire(lock)
	if e != nil {
		t.Fatal(e)
	}
	unlock()
	p := filepath.Join(root, "state.json")
	if e = marketWrite(p, marketState{SchemaVersion: 1, Current: "old"}); e != nil {
		t.Fatal(e)
	}
	if e = marketWrite(p, marketState{SchemaVersion: 1, Current: "new"}); e != nil {
		t.Fatal(e)
	}
	var state marketState
	marketRead(p, &state, marketLimit)
	if state.Current != "new" {
		t.Fatal("atomic replace failed")
	}
}
func TestMarketplaceCancellationAndDeadline(t *testing.T) {
	r, _, _ := marketFixture(t)
	os.MkdirAll(filepath.Join(r.StateRoot, "operations"), 0700)
	os.WriteFile(filepath.Join(r.StateRoot, "operations", r.OperationID+".cancel"), nil, 0600)
	if _, e := marketRun("prepare", r); e == nil {
		t.Fatal("cancelled prepare succeeded")
	}
	r.OperationID = strings.Repeat("b", 32)
	prepared, e := marketRun("prepare", r)
	if e != nil {
		t.Fatal(e)
	}
	ctx, cancel := context.WithDeadline(context.Background(), time.Now().Add(-time.Second))
	defer cancel()
	if marketValidateGeneration(ctx, r, prepared.Pending.GenerationID) == nil {
		t.Fatal("deadline ignored")
	}
	if _, e = marketRun("cancel", r); e != nil {
		t.Fatal(e)
	}
	var state marketState
	marketRead(filepath.Join(r.StateRoot, "state.json"), &state, marketLimit)
	if state.Pending != "" {
		t.Fatal("cancel kept pending")
	}
}
func TestMarketplaceBoundedUnknownAndURL(t *testing.T) {
	for _, url := range []string{"http://github.com/jarlbrak/ftk-mod-framework/releases/download/x/y", "https://evil.test/a", "https://github.com/evil/repo/releases/download/x/y", "https://user@github.com/jarlbrak/ftk-mod-framework/releases/download/x/y"} {
		if marketURL(url, false) == nil {
			t.Fatal("accepted", url)
		}
	}
	var r marketRequest
	if marketJSON([]byte(`{"schemaVersion":1,"downloadUrl":"https://evil.test"}`), &r) == nil {
		t.Fatal("arbitrary request field accepted")
	}
	b, _ := json.Marshal(map[string]string{"x": strings.Repeat("a", marketLimit)})
	p := filepath.Join(t.TempDir(), "large.json")
	os.WriteFile(p, b, 0600)
	if marketRead(p, &r, marketLimit) == nil {
		t.Fatal("oversized record accepted")
	}
}
func TestMarketplaceDependencyEnablePropagation(t *testing.T) {
	r, a, _ := marketFixture(t)
	b := a
	b.PackageID = "community.b"
	b.ModGUID = "com.community.b"
	c := a
	c.PackageID = "community.c"
	c.ModGUID = "com.community.c"
	a.Dependencies = []marketDependency{{b.PackageID, b.Version}}
	b.Dependencies = []marketDependency{{c.PackageID, c.Version}}
	r.Selection = []marketSelection{{b.PackageID, b.Version, false}, {a.PackageID, a.Version, true}}
	selected, e := marketResolve(marketCatalog{SchemaVersion: 1, Packages: []marketPackage{a, b, c}}, r)
	if e != nil {
		t.Fatal(e)
	}
	for _, p := range selected {
		if !p.Enabled || !p.Compatible {
			t.Fatal("dependency enable/compatibility not propagated", p)
		}
	}
}
func TestMarketplaceRollbackToEmptyBaseline(t *testing.T) {
	r, _, _ := marketFixture(t)
	marketRun("prepare", r)
	out, e := marketRun("activate", r)
	if e != nil || !out.PreviousAvailable {
		t.Fatal(out, e)
	}
	out, e = marketRun("rollback", r)
	if e != nil || out.Pending == nil || len(out.Pending.Packages) != 0 {
		t.Fatal(out, e)
	}
	out, e = marketRun("activate", r)
	if e != nil || !out.OK || out.Active == nil || len(out.Active.Packages) != 0 {
		t.Fatal(out, e)
	}
}
func TestMarketplaceRuntimeStateCrashGap(t *testing.T) {
	r, _, _ := marketFixture(t)
	marketRun("prepare", r)
	old, e := marketRun("activate", r)
	if e != nil {
		t.Fatal(e)
	}
	r.Selection = nil
	marketRun("prepare", r)
	marketWriteHook = func(p string) error {
		if filepath.Base(p) == "runtime-state.json" {
			return os.ErrPermission
		}
		return nil
	}
	defer func() { marketWriteHook = nil }()
	if _, e = marketRun("activate", r); e == nil {
		t.Fatal("fault injection did not fail")
	}
	var runtime marketResult
	if e = marketRead(filepath.Join(r.StateRoot, "runtime-state.json"), &runtime, marketLimit); e != nil {
		t.Fatal(e)
	}
	if runtime.Active.GenerationID != old.Active.GenerationID {
		t.Fatal("fallback active changed despite failed result")
	}
	marketWriteHook = nil
	status, e := marketRun("status", r)
	if e != nil || status.Active.GenerationID == old.Active.GenerationID {
		t.Fatal("atomic state did not recover completed activation", status, e)
	}
}
func TestMarketplaceFailedCommitPreservesPointer(t *testing.T) {
	r, _, _ := marketFixture(t)
	marketRun("prepare", r)
	prior, e := marketRun("activate", r)
	if e != nil {
		t.Fatal(e)
	}
	marketRun("prepare", r)
	marketWriteHook = func(p string) error {
		if filepath.Base(p) == "state.json" {
			return os.ErrPermission
		}
		return nil
	}
	defer func() { marketWriteHook = nil }()
	if _, e = marketRun("activate", r); e == nil {
		t.Fatal("fault injection did not fail")
	}
	var s marketState
	marketRead(filepath.Join(r.StateRoot, "state.json"), &s, marketLimit)
	if s.Current != prior.Active.GenerationID {
		t.Fatal("failed atomic commit changed active")
	}
}
func TestMarketplaceContentPolicy(t *testing.T) {
	for _, s := range []string{`{"entries":[],"entries":[]}`, `{"entries":[{"kind":"weapon","id":"same","template":"a"},{"kind":"item","id":"same","template":"a"}]}`, `{"entries":[{"kind":"weapon","id":"a","template":"b","fields":{"m_Prefab":"evil"}}]}`, `{"entries":[{"kind":"weapon","id":"a","template":"b","fields":{"damage":5,"_maxdmg":6}}]}`} {
		if marketContent([]byte(s)) == nil {
			t.Fatal("accepted forbidden content", s)
		}
	}
	if e := marketContent([]byte(`{"entries":[{"kind":"weapon","id":"a","template":"b","fields":{"damage":5,"goldvalue":10}}]}`)); e != nil {
		t.Fatal(e)
	}
}
func TestMarketplaceExpiredScreenshotBudget(t *testing.T) {
	r, p, _ := marketFixture(t)
	r.deadline = time.Now().Add(-time.Second)
	p.Screenshots = []string{"https://github.com/jarlbrak/ftk-mod-framework/releases/download/test/art.png"}
	cat := marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p}}
	start := time.Now()
	marketScreenshots(&cat, r, false)
	if time.Since(start) > time.Second || len(cat.Packages[0].ScreenshotPaths) != 0 {
		t.Fatal("optional screenshots exceeded catalog deadline")
	}
}
func TestMarketplaceManualDiscoveryParity(t *testing.T) {
	r, p, _ := marketFixture(t)
	manual := t.TempDir()
	r.ManualRoots = []string{manual}
	nested := filepath.Join(manual, "no-manifest", "nested")
	os.MkdirAll(nested, 0700)
	os.WriteFile(filepath.Join(nested, "manifest.json"), []byte(`{"modGuid":"`+p.ModGUID+`","name":"Nested ignored","version":"1.0.0"}`), 0600)
	out, e := marketRun("prepare", r)
	if e != nil {
		t.Fatal(e)
	}
	if e = marketValidateGeneration(context.Background(), r, out.Pending.GenerationID); e != nil {
		t.Fatal("nested manifest must not conflict", e)
	}
	direct := filepath.Join(manual, "direct")
	os.MkdirAll(direct, 0700)
	os.WriteFile(filepath.Join(direct, "manifest.json"), []byte(`{"modGuid":"`+p.ModGUID+`","name":"Developer","version":"1.0.0","developmentOnly":true}`), 0600)
	if e = marketValidateGeneration(context.Background(), r, out.Pending.GenerationID); e != nil {
		t.Fatal("excluded developer fixture must not conflict", e)
	}
	r.Settings = map[string]interface{}{"RunSelfTests": true}
	if e = marketValidateGeneration(context.Background(), r, out.Pending.GenerationID); e == nil {
		t.Fatal("enabled developer fixture conflict missed")
	}
}
func TestMarketplaceEmptySelectionWireArrays(t *testing.T) {
	r, _, _ := marketFixture(t)
	r.Selection = nil
	prepared, e := marketRun("prepare", r)
	if e != nil {
		t.Fatal(e)
	}
	b, e := json.Marshal(prepared)
	if e != nil {
		t.Fatal(e)
	}
	if bytes.Contains(b, []byte(`"packages":null`)) {
		t.Fatal("empty snapshot must serialize packages as []:", string(b))
	}
	var lock map[string]interface{}
	p := filepath.Join(r.StateRoot, "generations", prepared.Pending.GenerationID, "lock.json")
	raw, e := os.ReadFile(p)
	if e != nil {
		t.Fatal(e)
	}
	if bytes.Contains(raw, []byte(`"packages": null`)) || bytes.Contains(raw, []byte(`"files": null`)) {
		t.Fatal("empty lock arrays serialized null", string(raw))
	}
	json.Unmarshal(raw, &lock)
	lock["packages"] = nil
	marketWrite(p, lock)
	status, e := marketRun("status", r)
	if e != nil {
		t.Fatal(e)
	}
	b, _ = json.Marshal(status)
	if bytes.Contains(b, []byte(`"packages":null`)) {
		t.Fatal("legacy empty lock not normalized in snapshot")
	}
	active, e := marketRun("activate", r)
	if e != nil || !active.OK {
		t.Fatal(active, e)
	}
	b, _ = json.Marshal(active)
	if bytes.Contains(b, []byte(`"packages":null`)) {
		t.Fatal("active empty packages serialized null")
	}
}
func TestMarketplaceConfirmedPlanRevision(t *testing.T) {
	for _, change := range []string{"none", "state", "artifact", "settings", "display"} {
		t.Run(change, func(t *testing.T) {
			r, _, _ := marketFixture(t)
			cat := *r.localCatalog
			r.localCatalog = nil
			if e := marketWrite(filepath.Join(r.StateRoot, "catalog.json"), cat); e != nil {
				t.Fatal(e)
			}
			r.DryRun = true
			plan, e := marketRun("prepare", r)
			if e != nil || plan.PlanRevision == "" {
				t.Fatal(plan, e)
			}
			r.DryRun = false
			r.ExpectedRevision = plan.PlanRevision
			switch change {
			case "state":
				other := r
				other.OperationID = strings.Repeat("b", 32)
				if _, e = marketRun("cancel", other); e != nil {
					t.Fatal(e)
				}
			case "artifact":
				cat.Packages[0].SHA256 = strings.Repeat("b", 64)
				marketWrite(filepath.Join(r.StateRoot, "catalog.json"), cat)
			case "settings":
				r.Settings = map[string]interface{}{"EnableSampleContent": false}
			case "display":
				cat.Packages[0].Description = "Edited descriptive copy"
				marketWrite(filepath.Join(r.StateRoot, "catalog.json"), cat)
			}
			out, e := marketRun("prepare", r)
			if change == "none" || change == "display" {
				if e != nil || out.Pending == nil {
					t.Fatal(out, e)
				}
			} else if e == nil || !strings.Contains(e.Error(), "Review the plan again") {
				t.Fatal("changed plan was not rejected", out, e)
			}
		})
	}
}
func TestMarketplaceLockReleasedOnProcessDeath(t *testing.T) {
	if p := os.Getenv("FTKMF_LOCK_TEST_FILE"); p != "" {
		unlock, e := marketAcquire(p)
		if e != nil {
			os.Exit(2)
		}
		defer unlock()
		os.Stdout.Write([]byte("ready\n"))
		for {
			time.Sleep(time.Second)
		}
	}
	p := filepath.Join(t.TempDir(), "transaction.lock")
	cmd := exec.Command(os.Args[0], "-test.run=^TestMarketplaceLockReleasedOnProcessDeath$")
	cmd.Env = append(os.Environ(), "FTKMF_LOCK_TEST_FILE="+p)
	pipe, e := cmd.StdoutPipe()
	if e != nil {
		t.Fatal(e)
	}
	if e = cmd.Start(); e != nil {
		t.Fatal(e)
	}
	defer cmd.Process.Kill()
	ready, e := bufio.NewReader(pipe).ReadString('\n')
	if e != nil || ready != "ready\n" {
		t.Fatal("child lock failed", ready, e)
	}
	if unlock, e := marketAcquire(p); e == nil {
		unlock()
		t.Fatal("child process did not hold lock")
	}
	if e = cmd.Process.Kill(); e != nil {
		t.Fatal(e)
	}
	cmd.Wait()
	unlock, e := marketAcquire(p)
	if e != nil {
		t.Fatal("dead process left lock held", e)
	}
	unlock()
}
func TestMarketplaceDependencyRevision(t *testing.T) {
	r, p, _ := marketFixture(t)
	d := p
	d.PackageID = "community.dep"
	d.ModGUID = "com.community.dep"
	cat := marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p, d}}
	r.localCatalog = nil
	r.Selection = append(r.Selection, marketSelection{d.PackageID, d.Version, true})
	marketWrite(filepath.Join(r.StateRoot, "catalog.json"), cat)
	r.DryRun = true
	plan, e := marketRun("prepare", r)
	if e != nil {
		t.Fatal(e)
	}
	cat.Packages[0].Dependencies = []marketDependency{{d.PackageID, d.Version}}
	marketWrite(filepath.Join(r.StateRoot, "catalog.json"), cat)
	r.DryRun = false
	r.ExpectedRevision = plan.PlanRevision
	if _, e = marketRun("prepare", r); e == nil || !strings.Contains(e.Error(), "Review the plan again") {
		t.Fatal("dependency relationship change must invalidate confirmation", e)
	}
}
func TestMarketplaceCatalogStateMessages(t *testing.T) {
	var out marketResult
	cat := marketCatalog{SchemaVersion: 1, Packages: []marketPackage{}}
	marketCatalogStatus(&out, cat, true)
	if out.Status != "unavailable" || out.CatalogAgeSeconds != -1 || strings.Contains(out.Message, "verified") {
		t.Fatal("missing cache claimed verified catalog", out)
	}
	cat.FetchedAt = time.Now().Unix() - 100
	marketCatalogStatus(&out, cat, true)
	if out.Status != "offline" || out.CatalogAgeSeconds < 100 {
		t.Fatal("cached offline age missing", out)
	}
	marketCatalogStatus(&out, cat, false)
	if out.Status != "empty" {
		t.Fatal("online empty catalog conflated with unavailable", out)
	}
}
func TestMarketplacePlanExposesResolvedSelection(t *testing.T) {
	r, a, _ := marketFixture(t)
	b := a
	b.PackageID = "community.component"
	b.ModGUID = "com.community.component"
	b.Classification = "dependency"
	c := a
	c.PackageID = "community.nested"
	c.ModGUID = "com.community.nested"
	c.Classification = "dependency"
	a.Dependencies = []marketDependency{{b.PackageID, b.Version}}
	b.Dependencies = []marketDependency{{c.PackageID, c.Version}}
	r.localCatalog = &marketCatalog{SchemaVersion: 1, Packages: []marketPackage{a, b, c}}
	r.Selection = []marketSelection{{b.PackageID, b.Version, false}, {a.PackageID, a.Version, true}}
	r.DryRun = true
	out, e := marketRun("prepare", r)
	if e != nil {
		t.Fatal(e)
	}
	if len(out.Packages) != 3 {
		t.Fatal("confirmation omits resolved required components", out.Packages)
	}
	for _, p := range out.Packages {
		if !p.Enabled {
			t.Fatal("confirmation exposes requested state instead of dependency promotion", p)
		}
	}
}
func TestMarketplaceInstalledScreenshotCache(t *testing.T) {
	r, p, _ := marketFixture(t)
	s := "https://github.com/jarlbrak/ftk-mod-framework/releases/download/test/preview.png"
	p.Screenshots = []string{s}
	p.ScreenshotPaths = []string{"/untrusted/display/path.png"}
	r.localCatalog.Packages = []marketPackage{p}
	cache := filepath.Join(r.StateRoot, "cache", "screenshots", marketHash([]byte(s))+".png")
	os.MkdirAll(filepath.Dir(cache), 0700)
	var data bytes.Buffer
	if e := png.Encode(&data, image.NewRGBA(image.Rect(0, 0, 8, 8))); e != nil {
		t.Fatal(e)
	}
	os.WriteFile(cache, data.Bytes(), 0600)
	prepared, e := marketRun("prepare", r)
	if e != nil {
		t.Fatal(e)
	}
	if len(prepared.Pending.Packages[0].ScreenshotPaths) != 1 || prepared.Pending.Packages[0].ScreenshotPaths[0] != cache {
		t.Fatal("pending snapshot did not hydrate validated cached image", prepared.Pending.Packages)
	}
	active, e := marketRun("activate", r)
	if e != nil || !active.OK {
		t.Fatal(active, e)
	}
	if len(active.Active.Packages[0].ScreenshotPaths) != 1 || active.Active.Packages[0].ScreenshotPaths[0] != cache {
		t.Fatal("installed snapshot lost offline gallery", active.Active.Packages)
	}
}
