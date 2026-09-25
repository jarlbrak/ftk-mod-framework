package main

import (
	"encoding/json"
	"os"
	"strings"
	"testing"
)

// Check real publication metadata through the actual compatibility gate. Platform
// availability must not accidentally disable framework or game-build enforcement.
func TestPublishedModPlatformAvailability(t *testing.T) {
	data, err := os.ReadFile("../../marketplace/catalog.json")
	if err != nil {
		t.Fatal(err)
	}
	var catalog marketCatalog
	if err = json.Unmarshal(data, &catalog); err != nil {
		t.Fatal(err)
	}
	wanted := map[string]bool{"ftkmf.paladin": false, "ftkmf.thief": false, "ftkmf.possum": false}
	for _, p := range catalog.Packages {
		if _, ok := wanted[p.PackageID]; !ok {
			continue
		}
		wanted[p.PackageID] = true
		if len(p.GameFingerprints) == 0 {
			t.Fatalf("%s has no game-build allowlist", p.PackageID)
		}
		for _, platform := range []string{"windows", "macos", "linux"} {
			r := marketRequest{Platform: platform, FrameworkVersion: p.FrameworkVersion, gameFingerprint: p.GameFingerprints[0]}
			if why := marketCompatibility(p, r); why != "" {
				t.Fatalf("%s on %s: %s", p.PackageID, platform, why)
			}
			r.gameFingerprint = strings.Repeat("0", 64)
			if why := marketCompatibility(p, r); why == "" {
				t.Fatalf("%s accepts unknown game build", p.PackageID)
			}
			r.gameFingerprint = p.GameFingerprints[0]
			r.FrameworkVersion = "0.0.0"
			if why := marketCompatibility(p, r); why == "" {
				t.Fatalf("%s accepts old framework", p.PackageID)
			}
		}
	}
	for id, found := range wanted {
		if !found {
			t.Fatalf("Published package missing: %s", id)
		}
	}
}
