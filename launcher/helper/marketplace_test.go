package main

import (
	"archive/zip"
	"bufio"
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"image"
	"image/png"
	"io"
	"net/http"
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

func TestMarketplaceUnlistedInstalledLifecycle(t *testing.T) {
	r, p, _ := marketFixture(t)
	if _, e := marketRun("prepare", r); e != nil {
		t.Fatal(e)
	}
	initial, e := marketRun("activate", r)
	if e != nil || !initial.OK {
		t.Fatal(initial, e)
	}
	r.localCatalog = nil
	cache := filepath.Join(r.StateRoot, "catalog.json")
	if e = marketWrite(cache, marketCatalog{SchemaVersion: 1, Packages: []marketPackage{}}); e != nil {
		t.Fatal(e)
	}
	current := initial.Active.GenerationID
	apply := func(action string, selection []marketSelection) marketResult {
		t.Helper()
		r.Selection = selection
		r.DryRun = true
		plan, err := marketRun("prepare", r)
		if err != nil || len(plan.Plan) != 1 || plan.Plan[0].Action != action || plan.Pending != nil {
			t.Fatalf("%s review: %+v %v", action, plan, err)
		}
		r.DryRun = false
		r.ExpectedRevision = plan.PlanRevision
		pending, err := marketRun("prepare", r)
		if err != nil || pending.Pending == nil || pending.Active.GenerationID != current {
			t.Fatalf("%s preparation: %+v %v", action, pending, err)
		}
		if len(pending.Pending.Packages) != len(selection) || len(selection) > 0 && pending.Pending.Packages[0].Enabled != selection[0].Enabled {
			t.Fatalf("%s next-launch state does not match selection: %+v", action, pending.Pending)
		}
		result, err := marketRun("activate", r)
		if err != nil || !result.OK || result.Pending != nil || result.Active.GenerationID != pending.Pending.GenerationID {
			t.Fatalf("%s activation: %+v %v", action, result, err)
		}
		current = result.Active.GenerationID
		return result
	}
	disabled := apply("disable", []marketSelection{{p.PackageID, p.Version, false}})
	if disabled.Active.Packages[0].Enabled {
		t.Fatal("disabled package remained enabled")
	}
	enabled := apply("enable", []marketSelection{{p.PackageID, p.Version, true}})
	if !enabled.Active.Packages[0].Enabled {
		t.Fatal("enabled package remained disabled")
	}
	removed := apply("remove", nil)
	if len(removed.Active.Packages) != 0 {
		t.Fatal("removed package remained installed")
	}
	r.Selection = []marketSelection{{p.PackageID, p.Version, true}}
	r.DryRun = true
	if _, e = marketRun("prepare", r); e == nil || !strings.Contains(e.Error(), "missing exact package") {
		t.Fatal("old generation or cached archive authorized an unlisted reinstall", e)
	}
	if e = marketWrite(cache, marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p}}); e != nil {
		t.Fatal(e)
	}
	reinstalled := apply("install", r.Selection)
	if !reinstalled.Active.Packages[0].Enabled || reinstalled.Active.Packages[0].SHA256 != p.SHA256 {
		t.Fatal("reinstalled package differs from the verified selection")
	}
}

func TestMarketplaceUnlistedFallbackKeepsGuards(t *testing.T) {
	for _, mode := range []string{"archive-tampered", "revoked", "incompatible", "invalid-installed-descriptor"} {
		t.Run(mode, func(t *testing.T) {
			r, p, _ := marketFixture(t)
			if _, e := marketRun("prepare", r); e != nil {
				t.Fatal(e)
			}
			initial, e := marketRun("activate", r)
			if e != nil || !initial.OK {
				t.Fatal(initial, e)
			}
			r.localCatalog = nil
			cat := marketCatalog{SchemaVersion: 1, Packages: []marketPackage{}}
			want := ""
			switch mode {
			case "archive-tampered":
				if e = os.WriteFile(filepath.Join(r.StateRoot, "artifacts", p.SHA256+".zip"), []byte("tampered"), 0600); e != nil {
					t.Fatal(e)
				}
				want = "archive size or SHA-256 mismatch"
			case "revoked":
				p.Revoked = true
				cat.Packages = []marketPackage{p}
				want = "revoked"
			case "incompatible":
				p.Platforms = []string{"windows"}
				cat.Packages = []marketPackage{p}
				want = "platform"
			case "invalid-installed-descriptor":
				lockPath := filepath.Join(r.StateRoot, "generations", initial.Active.GenerationID, "lock.json")
				var lock marketLock
				if e = marketRead(lockPath, &lock, marketLimit); e != nil {
					t.Fatal(e)
				}
				lock.Packages[0].URL = "https://unapproved.example/package.zip"
				if e = marketWrite(lockPath, lock); e != nil {
					t.Fatal(e)
				}
				want = "unapproved package release"
			}
			if e = marketWrite(filepath.Join(r.StateRoot, "catalog.json"), cat); e != nil {
				t.Fatal(e)
			}
			r.Selection[0].Enabled = false
			r.DryRun = true
			plan, err := marketRun("prepare", r)
			if mode == "archive-tampered" {
				if err != nil {
					t.Fatal(err)
				}
				r.DryRun = false
				r.ExpectedRevision = plan.PlanRevision
				_, err = marketRun("prepare", r)
			}
			if err == nil || !strings.Contains(err.Error(), want) {
				t.Fatalf("%s guard failed: %v", mode, err)
			}
			status, err := marketRun("status", r)
			if err != nil || status.Active.GenerationID != initial.Active.GenerationID || status.Pending != nil {
				t.Fatalf("rejected change altered current or pending state: %+v %v", status, err)
			}
		})
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
func TestMarketplaceRevokedActivePackageIsKept(t *testing.T) {
	r, p, _ := marketFixture(t)
	if _, e := marketRun("prepare", r); e != nil {
		t.Fatal(e)
	}
	if _, e := marketRun("activate", r); e != nil {
		t.Fatal(e)
	}
	revoked := p
	revoked.Revoked = true
	other := p
	other.PackageID = "community.other"
	other.ModGUID = "com.community.other"
	other.Name = "Other equipment"
	r.localCatalog = &marketCatalog{SchemaVersion: 1, Packages: []marketPackage{revoked, other}}
	r.Selection = []marketSelection{{revoked.PackageID, revoked.Version, true}, {other.PackageID, other.Version, true}}
	r.DryRun = true
	plan, e := marketRun("prepare", r)
	if e != nil || len(plan.Plan) != 2 {
		t.Fatalf("prepare with kept revoked package: %+v %v", plan, e)
	}
	for _, entry := range plan.Plan {
		switch entry.PackageID {
		case other.PackageID:
			if entry.Action != "install" || entry.Notice != "" {
				t.Fatalf("unrelated install: %+v", entry)
			}
		case revoked.PackageID:
			if entry.Action != "keep" || entry.Notice == "" {
				t.Fatalf("kept revoked package lacks notice: %+v", entry)
			}
		}
	}
	r.Selection[0].Enabled = false
	if _, e = marketRun("prepare", r); e == nil {
		t.Fatal("toggled revoked package")
	}
	r.Selection = []marketSelection{{other.PackageID, other.Version, true}}
	if plan, e = marketRun("prepare", r); e != nil || len(plan.Plan) != 2 {
		t.Fatalf("removal alongside install: %+v %v", plan, e)
	}
	fresh, _, _ := marketFixture(t)
	fresh.localCatalog = &marketCatalog{SchemaVersion: 1, Packages: []marketPackage{revoked}}
	if _, e = marketRun("prepare", fresh); e == nil {
		t.Fatal("installed revoked package")
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
				r.Settings = map[string]interface{}{"EnableDataContent": false}
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

type marketRoundTrip func(*http.Request) (*http.Response, error)

func (f marketRoundTrip) RoundTrip(r *http.Request) (*http.Response, error) { return f(r) }

// The helper builds its clients on the default transport, so swapping it serves
// canned catalog and release responses without network access or a listener.
func marketServe(t *testing.T, handler func(*http.Request) (*http.Response, error)) {
	t.Helper()
	prior := http.DefaultTransport
	http.DefaultTransport = marketRoundTrip(handler)
	t.Cleanup(func() { http.DefaultTransport = prior })
}
func marketResponse(req *http.Request, status int, body []byte, location string) *http.Response {
	resp := &http.Response{StatusCode: status, Status: http.StatusText(status), Header: http.Header{}, Body: io.NopCloser(bytes.NewReader(body)), ContentLength: int64(len(body)), Request: req}
	if location != "" {
		resp.Header.Set("Location", location)
	}
	return resp
}
func TestMarketplacePublishedVersionHashChange(t *testing.T) {
	r, p, _ := marketFixture(t)
	cat := *r.localCatalog
	r.localCatalog = nil
	r.deadline = time.Now().Add(15 * time.Second)
	cache := filepath.Join(r.StateRoot, "catalog.json")
	if e := marketWrite(cache, cat); e != nil {
		t.Fatal(e)
	}
	published := cat
	published.Packages = []marketPackage{p}
	published.Packages[0].SHA256 = strings.Repeat("b", 64)
	served, _ := json.Marshal(published)
	marketServe(t, func(req *http.Request) (*http.Response, error) {
		if req.URL.String() != marketCatalogURL {
			t.Fatal("unexpected request", req.URL)
		}
		return marketResponse(req, 200, served, ""), nil
	})
	if _, _, e := marketGetCatalog(r); e == nil || !strings.Contains(e.Error(), "published version changed hash") {
		t.Fatal("republished bytes for an existing version accepted", e)
	}
	var kept marketCatalog
	if e := marketRead(cache, &kept, marketLimit); e != nil || kept.Packages[0].SHA256 != p.SHA256 {
		t.Fatal("rejected catalog replaced the cached one", kept, e)
	}
	published.Packages[0].Version = "1.0.1"
	served, _ = json.Marshal(published)
	got, offline, e := marketGetCatalog(r)
	if e != nil || offline || len(got.Packages) != 1 || got.Packages[0].Version != "1.0.1" {
		t.Fatal("new version with new bytes rejected", got, offline, e)
	}
	if e := marketRead(cache, &kept, marketLimit); e != nil || kept.Packages[0].SHA256 != strings.Repeat("b", 64) {
		t.Fatal("accepted catalog not cached", kept, e)
	}
}
func TestMarketplaceCatalogIdentityGuards(t *testing.T) {
	_, p, _ := marketFixture(t)
	remapped := p
	remapped.Version = "1.1.0"
	remapped.ModGUID = "com.community.other"
	if e := marketValidateCatalog(marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p, remapped}}); e == nil || !strings.Contains(e.Error(), "identity changed GUID") {
		t.Fatal("package ID remapped to another GUID accepted", e)
	}
	shared := p
	shared.PackageID = "community.alias"
	if e := marketValidateCatalog(marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p, shared}}); e == nil || !strings.Contains(e.Error(), "multiple IDs map to one GUID") {
		t.Fatal("two package IDs sharing one GUID accepted", e)
	}
	newer := p
	newer.Version = "1.1.0"
	if e := marketValidateCatalog(marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p, newer}}); e != nil {
		t.Fatal("stable identity across versions rejected", e)
	}
}
func TestMarketplaceArtifactDownloadHashMismatch(t *testing.T) {
	r, p, b := marketFixture(t)
	cache := filepath.Join(r.StateRoot, "artifacts", p.SHA256+".zip")
	os.Remove(cache)
	r.deadline = time.Now().Add(15 * time.Second)
	tampered := append([]byte{}, b...)
	tampered[len(tampered)-1] ^= 0xff
	body := tampered
	marketServe(t, func(req *http.Request) (*http.Response, error) {
		if req.URL.String() != p.URL {
			t.Fatal("unexpected request", req.URL)
		}
		return marketResponse(req, 200, body, ""), nil
	})
	if _, e := marketArtifact(r, p); e == nil || !strings.Contains(e.Error(), "SHA-256 mismatch") {
		t.Fatal("tampered download accepted", e)
	}
	if _, e := os.Stat(cache); e == nil {
		t.Fatal("mismatched download was cached")
	}
	body = b
	if got, e := marketArtifact(r, p); e != nil || !bytes.Equal(got, b) {
		t.Fatal("matching download rejected", e)
	}
	if cached, e := os.ReadFile(cache); e != nil || !bytes.Equal(cached, b) {
		t.Fatal("verified download not cached", e)
	}
	os.WriteFile(cache, tampered, 0600)
	body = nil
	if _, e := marketArtifact(r, p); e == nil || !strings.Contains(e.Error(), "SHA-256 mismatch") {
		t.Fatal("corrupted cache accepted", e)
	}
}
func TestMarketplaceRedirectHostRestriction(t *testing.T) {
	_, p, b := marketFixture(t)
	for _, target := range []string{"https://evil.test/asset.zip", "http://github.com/jarlbrak/ftk-mod-framework/releases/download/test/test.zip", "https://user@objects.githubusercontent.com/asset.zip"} {
		t.Run(target, func(t *testing.T) {
			marketServe(t, func(req *http.Request) (*http.Response, error) {
				if req.URL.String() == p.URL {
					return marketResponse(req, 302, nil, target), nil
				}
				return marketResponse(req, 200, b, ""), nil
			})
			if _, e := marketDownload(p.URL, archiveLimit, 5*time.Second); e == nil || !strings.Contains(e.Error(), "redirect left approved HTTPS hosts") {
				t.Fatal("redirect off approved hosts followed", e)
			}
		})
	}
	marketServe(t, func(req *http.Request) (*http.Response, error) {
		if req.URL.String() == p.URL {
			return marketResponse(req, 302, nil, "https://objects.githubusercontent.com/asset.zip"), nil
		}
		return marketResponse(req, 200, b, ""), nil
	})
	if got, e := marketDownload(p.URL, archiveLimit, 5*time.Second); e != nil || !bytes.Equal(got, b) {
		t.Fatal("redirect to approved release host rejected", e)
	}
	marketServe(t, func(req *http.Request) (*http.Response, error) {
		return marketResponse(req, 302, nil, "https://objects.githubusercontent.com/"+marketToken()), nil
	})
	if _, e := marketDownload(p.URL, archiveLimit, 5*time.Second); e == nil || !strings.Contains(e.Error(), "too many redirects") {
		t.Fatal("unbounded redirect chain followed", e)
	}
}

type marketRawEntry struct {
	name string
	size uint64
	data string
}

// Entries are written in order so a test can control which one trips a limit.
func marketRawZip(t *testing.T, entries []marketRawEntry) []byte {
	t.Helper()
	var b bytes.Buffer
	z := zip.NewWriter(&b)
	for _, entry := range entries {
		// CreateRaw trusts the header, so an entry can claim a huge expansion
		// without the test materializing hundreds of megabytes.
		w, e := z.CreateRaw(&zip.FileHeader{Name: entry.name, Method: zip.Store, UncompressedSize64: entry.size, CompressedSize64: entry.size})
		if e != nil {
			t.Fatal(e)
		}
		w.Write([]byte(entry.data))
	}
	if e := z.Close(); e != nil {
		t.Fatal(e)
	}
	return b.Bytes()
}
func TestMarketplacePackageLimits(t *testing.T) {
	_, p, _ := marketFixture(t)
	descriptor := map[string]func(*marketPackage){
		"compressed": func(x *marketPackage) { x.CompressedSize = archiveLimit + 1 },
		"expanded":   func(x *marketPackage) { x.ExpandedSize = expandedLimit + 1 },
		"files":      func(x *marketPackage) { x.FileCount = 5001 },
	}
	for name, mutate := range descriptor {
		t.Run("descriptor/"+name, func(t *testing.T) {
			over := p
			mutate(&over)
			if e := marketValidateCatalog(marketCatalog{SchemaVersion: 1, Packages: []marketPackage{over}}); e == nil || !strings.Contains(e.Error(), "descriptor exceeds package limits") {
				t.Fatal("oversized descriptor accepted", e)
			}
		})
	}
	edge := p
	edge.CompressedSize, edge.ExpandedSize, edge.FileCount = archiveLimit, expandedLimit, 5000
	if e := marketValidateCatalog(marketCatalog{SchemaVersion: 1, Packages: []marketPackage{edge}}); e != nil {
		t.Fatal("descriptor at the limits rejected", e)
	}
	manifest := `{"modGuid":"com.community.test","name":"Test","version":"1.0.0","frameworkVersion":"0.1.0"}`
	many := map[string]string{"manifest.json": manifest}
	for i := 0; i < 5000; i++ {
		many[fmt.Sprintf("f%d.json", i)] = `{"entries":[]}`
	}
	if _, e := marketExtract(marketZip(t, many), p, ""); e == nil || !strings.Contains(e.Error(), "archive file count exceeded") {
		t.Fatal("archive with more than 5,000 entries accepted", e)
	}
	if _, e := marketExtract(marketRawZip(t, []marketRawEntry{{"manifest.json", expandedLimit + 1, ""}}), p, ""); e == nil || !strings.Contains(e.Error(), "expanded file limit exceeded") {
		t.Fatal("single file over 250 MiB accepted", e)
	}
	// The first entry is a real, valid content file; the second claims exactly
	// the per-file limit, so only the running total crosses 250 MiB.
	small := `{"entries":[]}`
	if _, e := marketExtract(marketRawZip(t, []marketRawEntry{{"items.json", uint64(len(small)), small}, {"manifest.json", expandedLimit, ""}}), p, ""); e == nil || !strings.Contains(e.Error(), "expanded archive limit exceeded") {
		t.Fatal("archive expanding past 250 MiB in total accepted", e)
	}
	r, a, _ := marketFixture(t)
	a.CompressedSize, a.ExpandedSize, a.FileCount = 60<<20, 130<<20, 2600
	b := a
	b.PackageID = "community.second"
	b.ModGUID = "com.community.second"
	r.Selection = []marketSelection{{a.PackageID, a.Version, true}, {b.PackageID, b.Version, true}}
	if _, e := marketResolve(marketCatalog{SchemaVersion: 1, Packages: []marketPackage{a, b}}, r); e == nil || !strings.Contains(e.Error(), "complete generation exceeds size or file limits") {
		t.Fatal("generation whose packages sum past the limits accepted", e)
	}
	r.Selection = r.Selection[:1]
	if _, e := marketResolve(marketCatalog{SchemaVersion: 1, Packages: []marketPackage{a, b}}, r); e != nil {
		t.Fatal("single package within the limits rejected", e)
	}
}
func TestMarketplaceDeveloperFixtureDependency(t *testing.T) {
	r, p, _ := marketFixture(t)
	fixture := p
	fixture.PackageID = "ftkmf.sampledata"
	fixture.ModGUID = "com.ftkmf.sampledata"
	fixture.Classification = "dependency"
	p.Dependencies = []marketDependency{{fixture.PackageID, fixture.Version}}
	r.localCatalog = &marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p, fixture}}
	if _, e := marketRun("prepare", r); e == nil || !strings.Contains(e.Error(), "developer fixture excluded") {
		t.Fatal("catalog offering a developer fixture as a dependency accepted", e)
	}
	var state marketState
	if marketRead(filepath.Join(r.StateRoot, "state.json"), &state, marketLimit) == nil && state.Pending != "" {
		t.Fatal("rejected preparation left a pending generation")
	}
	// A lock that was tampered with after preparation must fail the same policy at activation.
	clean, _, _ := marketFixture(t)
	prepared, e := marketRun("prepare", clean)
	if e != nil {
		t.Fatal(e)
	}
	lockPath := filepath.Join(clean.StateRoot, "generations", prepared.Pending.GenerationID, "lock.json")
	var lock marketLock
	if e := marketRead(lockPath, &lock, marketLimit); e != nil {
		t.Fatal(e)
	}
	lock.Packages[0].Dependencies = []marketDependency{{fixture.PackageID, fixture.Version}}
	lock.Packages = append(lock.Packages, fixture)
	marketWrite(lockPath, lock)
	if e := marketValidateGeneration(context.Background(), clean, prepared.Pending.GenerationID); e == nil || !strings.Contains(e.Error(), "developer fixture excluded") {
		t.Fatal("locked dependency carrying a developer fixture GUID activated", e)
	}
}
func TestMarketplaceExportPayload(t *testing.T) {
	r, p, _ := marketFixture(t)
	marketRun("prepare", r)
	if out, e := marketRun("activate", r); e != nil || !out.OK {
		t.Fatal(out, e)
	}
	out, e := marketRun("export", r)
	if e != nil || out.ExportPath == "" {
		t.Fatal(out, e)
	}
	var export struct {
		SchemaVersion    int             `json:"schemaVersion"`
		FrameworkVersion string          `json:"frameworkVersion"`
		Partial          bool            `json:"partial"`
		Notice           string          `json:"notice"`
		Active           *marketSnapshot `json:"active"`
		Settings         interface{}     `json:"settings"`
	}
	if e := marketRead(out.ExportPath, &export, marketLimit); e != nil {
		t.Fatal(e)
	}
	if !export.Partial || !strings.Contains(export.Notice, "Manual mods") || export.SchemaVersion != 1 || export.FrameworkVersion != r.FrameworkVersion {
		t.Fatalf("export does not declare itself partial: %+v", export)
	}
	if export.Active == nil || len(export.Active.Packages) != 1 || export.Active.Packages[0].SHA256 != p.SHA256 || export.Active.Packages[0].Version != p.Version {
		t.Fatalf("export lacks exact version and hash: %+v", export.Active)
	}
}
