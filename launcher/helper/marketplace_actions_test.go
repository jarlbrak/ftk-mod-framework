package main

import "testing"

func TestMarketClassAndDebuffCapabilities(t *testing.T) {
	for _, test := range []struct {
		name, kind, fields string
		valid              bool
	}{
		{"item spell", "item", `"proficiencies":["smite"]`, true},
		{"duplicate item spell", "item", `"proficiencies":["smite","smite"]`, false},
		{"empty item spell", "item", `"proficiencies":[]`, false},
		{"blank item spell", "item", `"proficiencies":[" "]`, false},
		{"class spell", "class", `"proficiencies":["smite"]`, true},
		{"duplicate class spell", "class", `"proficiencies":["smite","smite"]`, false},
		{"empty class spell", "class", `"proficiencies":[]`, false},
		{"blank class spell", "class", `"proficiencies":[" "]`, false},
		{"two debuffs", "proficiency", `"randomDebuffOutcomes":["test","resist"]`, true},
		{"missing original", "proficiency", `"randomDebuffOutcomes":["armor","resist"]`, false},
		{"duplicate outcome", "proficiency", `"randomDebuffOutcomes":["test","test"]`, false},
		{"extra outcome", "proficiency", `"randomDebuffOutcomes":["test","resist","other"]`, false},
		{"wrong outcome kind", "weapon", `"randomDebuffOutcomes":["test","resist"]`, false},
		{"conditional bonus", "proficiency", `"resistanceDamageBonus":{"sources":["resist"],"multiplier":6}`, true},
		{"duplicate source", "proficiency", `"resistanceDamageBonus":{"sources":["resist","resist"],"multiplier":6}`, false},
		{"no sources", "proficiency", `"resistanceDamageBonus":{"sources":[],"multiplier":6}`, false},
		{"neutral multiplier", "proficiency", `"resistanceDamageBonus":{"sources":["resist"],"multiplier":1}`, false},
		{"oversized multiplier", "proficiency", `"resistanceDamageBonus":{"sources":["resist"],"multiplier":17}`, false},
		{"wrong bonus kind", "class", `"resistanceDamageBonus":{"sources":["resist"],"multiplier":6}`, false},
		{"unknown bonus field", "proficiency", `"resistanceDamageBonus":{"sources":["resist"],"multiplier":6,"consume":true}`, false},
		{"mixed modifiers", "proficiency", `"randomDebuffOutcomes":["test","resist"],"resistanceDamageBonus":{"sources":["resist"],"multiplier":6}`, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			body := `{"entries":[{"kind":"` + test.kind + `","id":"test","template":"native",` + test.fields + `}]}`
			err := marketContent([]byte(body))
			if (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}

func TestMarketNormalizedDefenseAndMagicFields(t *testing.T) {
	body := []byte(`{"entries":[{"kind":"proficiency","id":"resist","template":"magicResistDown","fields":{"m_DmgTypeOverride":"none","m_WpnTypeOverride":"none","m_TargetFriendly":false,"m_Harmless":false,"m_PerSlotSkillRoll":0,"m_Quickness":0.3,"m_DamagePerAttack":0,"m_Suicide":false,"m_GunShot":false,"m_BoatDamage":0,"m_ChaosOption":false}}]}`)
	if err := marketContent(body); err != nil {
		t.Fatal(err)
	}
	unsafe := []byte(`{"entries":[{"kind":"proficiency","id":"resist","template":"magicResistDown","fields":{"m_ProficiencyPrefab":"arbitrary"}}]}`)
	if err := marketContent(unsafe); err == nil {
		t.Fatal("behavior object override must remain rejected")
	}
}
