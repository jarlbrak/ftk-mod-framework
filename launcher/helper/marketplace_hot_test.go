package main

import (
	"os"
	"path/filepath"
	"testing"
)

func TestHotActivationCompareAndSwapAndRecovery(t *testing.T) {
	r, _, _ := marketFixture(t)
	prepared, err := marketRun("prepare", r)
	if err != nil {
		t.Fatal(err)
	}
	r.ExpectedPending = prepared.Pending.GenerationID
	validated, err := marketRun("hot-validate", r)
	if err != nil || !validated.OK || validated.Active != nil {
		t.Fatalf("validate: %+v %v", validated, err)
	}
	recovered, err := marketRun("activate", r)
	if err != nil || recovered.Active != nil || recovered.Pending == nil {
		t.Fatalf("uncommitted recovery: %+v %v", recovered, err)
	}
	wrong := r
	wrong.ExpectedCurrent = "different"
	if _, err = marketRun("hot-commit", wrong); err == nil {
		t.Fatal("accepted changed current")
	}
	wrong = r
	wrong.ExpectedPending = "different"
	if _, err = marketRun("hot-commit", wrong); err == nil {
		t.Fatal("accepted changed pending")
	}
	committed, err := marketRun("hot-commit", r)
	if err != nil || committed.Active == nil || committed.Active.GenerationID != r.ExpectedPending || committed.Pending != nil {
		t.Fatalf("commit: %+v %v", committed, err)
	}
	recovered, err = marketRun("activate", r)
	if err != nil || recovered.Active == nil || recovered.Active.GenerationID != r.ExpectedPending {
		t.Fatalf("committed recovery: %+v %v", recovered, err)
	}
}
func TestHotCommitRequiresValidationIntent(t *testing.T) {
	r, _, _ := marketFixture(t)
	prepared, err := marketRun("prepare", r)
	if err != nil {
		t.Fatal(err)
	}
	r.ExpectedPending = prepared.Pending.GenerationID
	if _, err = marketRun("hot-commit", r); err == nil {
		t.Fatal("commit without intent")
	}
}

func TestHotCommitRevalidatesBytesAndPendingRevision(t *testing.T) {
	r, _, _ := marketFixture(t)
	prepared, err := marketRun("prepare", r)
	if err != nil {
		t.Fatal(err)
	}
	r.ExpectedPending = prepared.Pending.GenerationID
	if _, err = marketRun("hot-validate", r); err != nil {
		t.Fatal(err)
	}
	r.Selection = nil
	replacement, err := marketRun("prepare", r)
	if err != nil {
		t.Fatal(err)
	}
	if replacement.Pending.GenerationID == r.ExpectedPending {
		t.Fatal("fixture did not change pending")
	}
	if _, err = marketRun("hot-commit", r); err == nil {
		t.Fatal("committed a replaced selection")
	}
}

func TestHotCommitRejectsChangedGenerationBytes(t *testing.T) {
	r, _, _ := marketFixture(t)
	prepared, err := marketRun("prepare", r)
	if err != nil {
		t.Fatal(err)
	}
	r.ExpectedPending = prepared.Pending.GenerationID
	if _, err = marketRun("hot-validate", r); err != nil {
		t.Fatal(err)
	}
	path := filepath.Join(prepared.Pending.ContentRoot, "community.test", "items.json")
	if err = os.WriteFile(path, []byte("{}"), 0600); err != nil {
		t.Fatal(err)
	}
	if _, err = marketRun("hot-commit", r); err == nil {
		t.Fatal("accepted changed bytes")
	}
	var state marketState
	if err = marketRead(filepath.Join(r.StateRoot, "state.json"), &state, marketLimit); err != nil {
		t.Fatal(err)
	}
	if state.Current != "" || state.Pending != r.ExpectedPending {
		t.Fatalf("changed active state: %+v", state)
	}
}
