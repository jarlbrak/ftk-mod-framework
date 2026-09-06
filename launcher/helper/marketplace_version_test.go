package main

import (
	"context"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestFrameworkVersionCompatibility(t *testing.T) {
	for _, c := range []struct {
		declared, running string
		ok                bool
	}{{"0.1.2", "0.1.3", true}, {"0.1.2", "0.2.0", true}, {"0.1.2", "0.1.1", false}, {"0.1.2", "1.0.0", false}, {"1.2.0", "1.9.0", true}, {"1.2.0", "2.0.0", false}, {"1.2.0", "1.1.9", false}} {
		if got := marketConfirmedCompatibility(c.declared, c.running) == ""; got != c.ok {
			t.Fatal(c, got)
		}
	}
	for _, bad := range []string{"", "v0.1.2", "01.2.3", "1.02.3", "1.2.3.4", "-1.2.3", "1.2", "1.2.2147483648", "2147483648.0.0", " 1.2.3"} {
		if marketStrictVersion(bad) {
			t.Fatal("invalid version accepted", bad)
		}
	}
	if s, ok := marketFrameworkRange("2147483647.0.0"); !ok || s != ">=2147483647.0.0 <2147483648.0.0" {
		t.Fatal(s, ok)
	}
}
func TestFrameworkVersionCatalogArchiveContract(t *testing.T) {
	_, p, _ := marketFixture(t)
	for _, mode := range []string{"valid", "missing-confirmation", "missing-range", "conflicting-range", "overflow-mod-version"} {
		c := marketCatalog{SchemaVersion: 1, Packages: []marketPackage{p}}
		switch mode {
		case "missing-confirmation":
			c.Packages[0].FrameworkVersion = ""
		case "missing-range":
			c.Packages[0].FrameworkRange = ""
		case "conflicting-range":
			c.Packages[0].FrameworkRange = ">=0.1.0 <0.2.0"
		case "overflow-mod-version":
			c.Packages[0].Version = "2147483648.0.0"
		}
		e := marketValidateCatalog(c)
		if (e == nil) != (mode == "valid") {
			t.Fatal(mode, e)
		}
	}
	for _, v := range []string{"0.1.0", "", "0.1.1", "v0.1.0"} {
		raw := []byte(`{"modGuid":"` + p.ModGUID + `","name":"Test","version":"1.0.0","frameworkVersion":"` + v + `"}`)
		if e := marketManifest(raw, p); (e == nil) != (v == "0.1.0") {
			t.Fatal(v, e)
		}
	}
}
func TestFrameworkVersionActivationRechecksCurrentMajor(t *testing.T) {
	for _, version := range []string{"0.2.0", "1.0.0"} {
		r, _, _ := marketFixture(t)
		out, e := marketRun("prepare", r)
		if e != nil {
			t.Fatal(e)
		}
		r.FrameworkVersion = version
		e = marketValidateGeneration(context.Background(), r, out.Pending.GenerationID)
		if (e == nil) != (version == "0.2.0") {
			t.Fatal(version, e)
		}
	}
}
func TestFrameworkVersionLegacyLocksAreMajorBounded(t *testing.T) {
	for _, c := range []struct {
		anchor, target string
		empty, ok      bool
	}{{"0.1.0", "0.2.0", false, true}, {"0.1.0", "1.0.0", false, false}, {"", "0.2.0", false, false}, {"bad", "0.2.0", false, false}, {"", "9.0.0", true, true}} {
		game, _, _, _, _ := updateFixture(t)
		id := strings.Repeat("a", 32)
		root := filepath.Join(updateRoot(game), "marketplace")
		packages := []marketPackage{}
		if !c.empty {
			packages = append(packages, marketPackage{Name: "Legacy", Version: "1.0.0", FrameworkRange: ">=0.1.0 <9.0.0"})
		}
		marketWrite(filepath.Join(root, "state.json"), marketState{SchemaVersion: 1, Current: id, Pending: id})
		marketWrite(filepath.Join(root, "generations", id, "lock.json"), marketLock{SchemaVersion: 1, FrameworkVersion: c.anchor, Packages: packages})
		e := updateManagedCompatibility(game, c.target)
		if (e == nil) != c.ok {
			t.Fatal(c, e)
		}
		got, e := updateManagedPackages(game)
		if e != nil {
			t.Fatal(e)
		}
		for _, p := range got {
			if p.FrameworkVersion != "" {
				t.Fatal("legacy metadata inferred author confirmation")
			}
		}
	}
}
func TestFrameworkVersionManualConfiguredRootParity(t *testing.T) {
	game, _, _, _, _ := updateFixture(t)
	defaultMod := filepath.Join(game, "BepInEx", "plugins", "ignored")
	os.MkdirAll(defaultMod, 0700)
	os.WriteFile(filepath.Join(defaultMod, "manifest.json"), []byte("broken"), 0600)
	selected := filepath.Join(game, "Selected", "mod")
	os.MkdirAll(selected, 0700)
	valid := `{"modGuid":"com.manual","name":"Manual","version":"1.0.0","frameworkVersion":"0.1.2"}`
	os.WriteFile(filepath.Join(selected, "manifest.json"), []byte(valid), 0600)
	config := filepath.Join(game, "BepInEx", "config", "com.ftkmf.framework.cfg")
	os.MkdirAll(filepath.Dir(config), 0700)
	os.WriteFile(config, []byte("[Data]\nEnableDataContent=true\nDataContentRoot=Selected\n"), 0600)
	if e := updateManagedCompatibility(game, "0.2.0"); e != nil {
		t.Fatal("configured root mixed with default root", e)
	}
	if e := updateManagedCompatibility(game, "1.0.0"); e == nil {
		t.Fatal("manual declaration permitted new major")
	}
	os.WriteFile(filepath.Join(selected, "manifest.json"), []byte("broken"), 0600)
	if e := updateManagedCompatibility(game, "0.2.0"); e == nil {
		t.Fatal("malformed manual manifest allowed update")
	}
	os.WriteFile(config, []byte("[Data]\nEnableDataContent=false\nDataContentRoot=Selected\n"), 0600)
	if e := updateManagedCompatibility(game, "2.0.0"); e != nil {
		t.Fatal("disabled data content scanned", e)
	}
}
func TestFrameworkVersionManualDeveloperAndNestedFiltering(t *testing.T) {
	game, _, _, _, _ := updateFixture(t)
	root := filepath.Join(game, "BepInEx", "plugins")
	nested := filepath.Join(root, "container", "nested")
	os.MkdirAll(nested, 0700)
	os.WriteFile(filepath.Join(nested, "manifest.json"), []byte("broken"), 0600)
	dev := filepath.Join(root, "fixture")
	os.MkdirAll(dev, 0700)
	os.WriteFile(filepath.Join(dev, "manifest.json"), []byte(`{"modGuid":"com.fixture","name":"Fixture","version":"1.0.0","developmentOnly":true}`), 0600)
	if e := updateManagedCompatibility(game, "1.0.0"); e != nil {
		t.Fatal("excluded fixtures or nested manifests blocked update", e)
	}
	config := filepath.Join(game, "BepInEx", "config", "com.ftkmf.framework.cfg")
	os.MkdirAll(filepath.Dir(config), 0700)
	os.WriteFile(config, []byte("[Diagnostics]\nRunSelfTests=true\n"), 0600)
	if e := updateManagedCompatibility(game, "1.0.0"); e == nil {
		t.Fatal("enabled fixture missing declaration allowed update")
	}
}
func TestFrameworkVersionWindowsRootedPathValidation(t *testing.T) {
	for _, c := range []struct {
		host, path  string
		windows, ok bool
	}{{"windows", `C:\mods`, true, true}, {"windows", `mods\extra`, true, true}, {"windows", `\\server\share\mods`, true, true}, {"windows", `\mods`, true, false}, {"windows", "/mods", true, false}, {"windows", "C:mods", true, false}, {"linux", `C:\mods`, true, false}, {"linux", "/mods", true, false}, {"linux", `mods\extra`, true, true}, {"linux", "/mods", false, true}, {"darwin", "/mods", false, true}} {
		if _, e := updateValidateManualPath(c.path, c.host, c.windows); (e == nil) != c.ok {
			t.Fatal(c, e)
		}
	}
}
