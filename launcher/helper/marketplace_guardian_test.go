package main

import "testing"

func TestMarketLegendaryGuardianCapabilities(t *testing.T) {
	for _, test := range []struct {
		name, kind, bonuses string
		valid               bool
	}{
		{"watch", "weapon", `{"guardFocusRestore":1}`, true},
		{"reckoning", "weapon", `{"guardReckoning":true}`, true},
		{"cleanse", "item", `{"guardCleanse":true}`, true},
		{"legacy", "item", `{"guardHealPercent":8,"wardDebuffs":true}`, true},
		{"negativeFocus", "weapon", `{"guardFocusRestore":-1}`, false},
		{"excessFocus", "weapon", `{"guardFocusRestore":2}`, false},
		{"fractionFocus", "weapon", `{"guardFocusRestore":0.5}`, false},
		{"wrongType", "weapon", `{"guardReckoning":50}`, false},
		{"wrongKind", "class", `{"guardCleanse":true}`, false},
		{"watchRequiresWeapon", "item", `{"guardFocusRestore":1}`, false},
		{"reckoningRequiresWeapon", "item", `{"guardReckoning":true}`, false},
		{"unknownCapability", "weapon", `{"guardFocusRestores":1}`, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			body := `{"entries":[{"kind":"` + test.kind + `","id":"test","template":"hammer","guardianBonuses":` + test.bonuses + `}]}`
			err := marketContent([]byte(body))
			if (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}
