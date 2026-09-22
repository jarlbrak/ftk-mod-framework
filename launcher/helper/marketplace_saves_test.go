package main

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func saveSettings() map[string]interface{} {
	return map[string]interface{}{"EnableDataContent": true, "EnableCampaignEngine": true, "EnableSampleContent": false, "EnableBehaviorLoading": false}
}
func TestSaveFingerprintCrossLanguageVector(t *testing.T) {
	got, err := marketSaveFingerprint(strings.Repeat("a", 64), "1.0.0", []marketPackage{{ModGUID: "com.ftkmf.paladin", SHA256: strings.Repeat("b", 64), Enabled: true}}, saveSettings())
	if err != nil || got != "c29b7774b66ecd083cb64456430920bd6ecd70d4e92f1158b0a04f5a602f71d7" {
		t.Fatal(got, err)
	}
}
func TestRestoreSavedSetAfterLaterSelections(t *testing.T) {
	r, _, _ := marketFixture(t)
	r.Settings = saveSettings()
	prepared, err := marketRun("prepare", r)
	if err != nil {
		t.Fatal(err)
	}
	first := prepared.Pending
	fingerprint, err := marketSaveFingerprint(r.gameFingerprint, r.FrameworkVersion, first.Packages, r.Settings)
	if err != nil {
		t.Fatal(err)
	}
	if err = marketWrite(filepath.Join(r.StateRoot, "save-pins", fingerprint+".json"), map[string]interface{}{"schemaVersion": 1, "generation": first.GenerationID}); err != nil {
		t.Fatal(err)
	}
	// Historical generations omitted settings from lock.json. The pinned
	// fingerprint must still restore only when today's settings reproduce it.
	lockPath := filepath.Join(r.StateRoot, "generations", first.GenerationID, "lock.json")
	var historical marketLock
	if err = marketRead(lockPath, &historical, marketLimit); err != nil {
		t.Fatal(err)
	}
	historical.Settings = nil
	if err = marketWrite(lockPath, historical); err != nil {
		t.Fatal(err)
	}
	if _, err = marketRun("activate", r); err != nil {
		t.Fatal(err)
	}
	r.Selection = nil
	for i := 0; i < 3; i++ {
		if _, err = marketRun("prepare", r); err != nil {
			t.Fatal(err)
		}
		current, e := marketRun("activate", r)
		if e != nil {
			t.Fatal(e)
		}
		r.ExpectedCurrent = current.Active.GenerationID
	}
	r.ExpectedPending = ""
	r.SaveFingerprint = fingerprint
	restored, err := marketRun("restore-save-set", r)
	if err != nil || restored.Pending == nil || restored.Pending.GenerationID != first.GenerationID || restored.Active.GenerationID != r.ExpectedCurrent {
		t.Fatalf("restore %+v %v", restored, err)
	}
	if _, err = marketRun("restore-save-set", r); err == nil {
		t.Fatal("stale pending CAS succeeded")
	}
	r.ExpectedPending = first.GenerationID
	r.FrameworkVersion = "0.2.0"
	if _, err = marketRun("restore-save-set", r); err == nil {
		t.Fatal("wrong framework accepted")
	}
	r.FrameworkVersion = "0.1.0"
	r.Settings["EnableBehaviorLoading"] = true
	if _, err = marketRun("restore-save-set", r); err == nil {
		t.Fatal("changed settings accepted")
	}
	r.Settings = saveSettings()
	if err = os.WriteFile(filepath.Join(first.ContentRoot, "community.test", "items.json"), []byte("corrupt"), 0600); err != nil {
		t.Fatal(err)
	}
	if _, err = marketRun("restore-save-set", r); err == nil {
		t.Fatal("changed saved artifact accepted")
	}
}
func TestCanonicalEmptyForSaveLibraries(t *testing.T) {
	r, _, _ := marketFixture(t)
	r.Settings = saveSettings()
	r.EnsureEmptyGeneration = true
	result, err := marketRun("activate", r)
	if err != nil || result.Active == nil || len(result.Active.Packages) != 0 {
		t.Fatalf("empty %+v %v", result, err)
	}
	again, err := marketRun("activate", r)
	if err != nil || again.Active.GenerationID != result.Active.GenerationID {
		t.Fatal("empty identity changed", err)
	}
}
