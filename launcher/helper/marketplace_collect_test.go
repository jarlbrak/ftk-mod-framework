package main

import (
	"os"
	"path/filepath"
	"testing"
)

func TestCollectPreservesEveryOwnershipRoot(t *testing.T) {
	root := t.TempDir()
	ids := []string{}
	for i := 0; i < 7; i++ {
		id := marketToken()
		ids = append(ids, id)
		if err := os.MkdirAll(filepath.Join(root, "generations", id), 0700); err != nil {
			t.Fatal(err)
		}
	}
	state := marketState{SchemaVersion: 1, Current: ids[0], Pending: ids[1], Previous: ids[2]}
	if err := marketWrite(filepath.Join(root, "hot-activation.json"), marketHotIntent{1, ids[3], ids[4]}); err != nil {
		t.Fatal(err)
	}
	if err := marketWrite(filepath.Join(root, "save-pins", "save.json"), map[string]interface{}{"schemaVersion": 1, "generation": ids[5]}); err != nil {
		t.Fatal(err)
	}
	n, err := marketCollect(root, state)
	if err != nil || n != 1 {
		t.Fatalf("collect %d: %v", n, err)
	}
	for _, id := range ids[:6] {
		if _, err := os.Stat(filepath.Join(root, "generations", id)); err != nil {
			t.Fatal("deleted protected generation", id)
		}
	}
	if _, err := os.Stat(filepath.Join(root, "generations", ids[6])); !os.IsNotExist(err) {
		t.Fatal("unused generation survived")
	}
}

func TestCollectRejectsLiveRuntimeAndMalformedPins(t *testing.T) {
	r, _, _ := marketFixture(t)
	if err := os.MkdirAll(r.StateRoot, 0700); err != nil {
		t.Fatal(err)
	}
	release, err := marketAcquire(filepath.Join(r.StateRoot, "runtime.lock"))
	if err != nil {
		t.Fatal(err)
	}
	if _, err := marketRun("collect", r); err == nil {
		t.Fatal("collected during live runtime")
	}
	release()
	if _, err := marketRun("collect", r); err != nil {
		t.Fatal(err)
	}
	orphan := marketToken()
	if err := os.MkdirAll(filepath.Join(r.StateRoot, "generations", orphan), 0700); err != nil {
		t.Fatal(err)
	}
	if err := marketWrite(filepath.Join(r.StateRoot, "save-pins", "broken.json"), map[string]interface{}{"schemaVersion": 99, "generation": orphan}); err != nil {
		t.Fatal(err)
	}
	if _, err := marketRun("collect", r); err == nil {
		t.Fatal("accepted malformed pin")
	}
	if _, err := os.Stat(filepath.Join(r.StateRoot, "generations", orphan)); err != nil {
		t.Fatal("deleted before validating all pins")
	}
}

func TestPreparationBudgetPreservesCurrentAndPending(t *testing.T) {
	r, _, _ := marketFixture(t)
	prepared, err := marketRun("prepare", r)
	if err != nil {
		t.Fatal(err)
	}
	before, err := os.ReadFile(filepath.Join(r.StateRoot, "state.json"))
	if err != nil {
		t.Fatal(err)
	}
	r.MaxGenerationBytes = 1
	if _, err = marketRun("prepare", r); err == nil {
		t.Fatal("over-budget preparation succeeded")
	}
	after, err := os.ReadFile(filepath.Join(r.StateRoot, "state.json"))
	if err != nil {
		t.Fatal(err)
	}
	if string(before) != string(after) {
		t.Fatal("budget rejection changed durable selection")
	}
	if _, err := os.Stat(prepared.Pending.ContentRoot); err != nil {
		t.Fatal("budget rejection removed pending")
	}
}
