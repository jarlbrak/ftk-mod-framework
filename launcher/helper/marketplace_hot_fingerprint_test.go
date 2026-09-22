package main

import (
	"encoding/json"
	"testing"
)

// The existing in-process fixture fills private request fields. Real CLI JSON
// cannot carry gameFingerprint, so hot operations must calculate it themselves.
func TestHotActivationComputesFingerprintFromCLIRequest(t *testing.T) {
	request, _, _ := marketFixture(t)
	prepared, err := marketRun("prepare", request)
	if err != nil {
		t.Fatal(err)
	}
	request.ExpectedPending = prepared.Pending.GenerationID
	serialized, err := json.Marshal(request)
	if err != nil {
		t.Fatal(err)
	}
	var fromCLI marketRequest
	if err = json.Unmarshal(serialized, &fromCLI); err != nil {
		t.Fatal(err)
	}
	if fromCLI.gameFingerprint != "" {
		t.Fatal("fixture accidentally preserved private fingerprint")
	}
	validated, err := marketRun("hot-validate", fromCLI)
	if err != nil || !validated.OK {
		t.Fatalf("CLI validation: %+v %v", validated, err)
	}
	committed, err := marketRun("hot-commit", fromCLI)
	if err != nil || !committed.OK || committed.Active == nil || committed.Active.GenerationID != request.ExpectedPending {
		t.Fatalf("CLI commit: %+v %v", committed, err)
	}
}
