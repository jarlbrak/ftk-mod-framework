package main

import (
	"os"
	"testing"
)

func TestMarketThiefCapabilities(t *testing.T) {
	for _, test := range []struct {
		name, kind, fields string
		valid              bool
	}{
		{"class", "class", `"opportunist":true,"icon":"assets/slip.png"`, true},
		{"paired", "weapon", `"precisionWeapon":"paired","replaceProficiencies":true,"proficiencies":["feint"]`, true},
		{"bow", "weapon", `"precisionWeapon":"bow"`, true},
		{"prepare", "proficiency", `"precisionAction":"prepare"`, true},
		{"pierce", "proficiency", `"precisionAction":"pierce"`, true},
		{"fortune", "weapon", `"precisionWeapon":"paired","thiefArtifact":"borrowedFortune"`, true},
		{"light", "weapon", `"precisionWeapon":"paired","thiefArtifact":"lastLight"`, true},
		{"road", "weapon", `"precisionWeapon":"bow","thiefArtifact":"looseAndLeave"`, true},
		{"wrongClassKind", "weapon", `"opportunist":true`, false},
		{"wrongClassType", "class", `"opportunist":"true"`, false},
		{"unsupportedClassIcon", "class", `"icon":"assets/slip.png"`, false},
		{"weaponKind", "item", `"precisionWeapon":"paired"`, false},
		{"weaponValue", "weapon", `"precisionWeapon":"dagger"`, false},
		{"actionKind", "weapon", `"precisionAction":"prepare"`, false},
		{"actionValue", "proficiency", `"precisionAction":"sneak"`, false},
		{"artifactKind", "item", `"thiefArtifact":"lastLight"`, false},
		{"artifactValue", "weapon", `"precisionWeapon":"paired","thiefArtifact":"unknown"`, false},
		{"artifactRequiresPrecision", "weapon", `"thiefArtifact":"lastLight"`, false},
		{"artifactRequiresPaired", "weapon", `"precisionWeapon":"bow","thiefArtifact":"lastLight"`, false},
		{"artifactRequiresBow", "weapon", `"precisionWeapon":"paired","thiefArtifact":"looseAndLeave"`, false},
		{"replacementKind", "enemy", `"replaceProficiencies":true`, false},
		{"replacementType", "weapon", `"replaceProficiencies":1`, false},
		{"unknownField", "class", `"opportunists":true`, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			err := marketContent([]byte(`{"entries":[{"kind":"` + test.kind + `","id":"test","template":"native",` + test.fields + `}]}`))
			if (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}

func TestMarketOffHandModels(t *testing.T) {
	renderer := `[{"path":".","model":"assets/left.glb","texture":"assets/left.png"}]`
	for _, test := range []struct {
		name, kind, models string
		valid              bool
	}{
		{"weapon", "weapon", renderer, true},
		{"item", "item", renderer, false},
		{"empty", "weapon", `[]`, false},
		{"traversal", "weapon", `[{"path":".","model":"../left.glb","texture":"assets/left.png"}]`, false},
		{"unknown", "weapon", `[{"path":".","model":"assets/left.glb","texture":"assets/left.png","code":"evil"}]`, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			raw := []byte(`{"entries":[{"kind":"` + test.kind + `","id":"test","template":"native","offHandModels":` + test.models + `}]}`)
			if err := marketContent(raw); (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
	raw := []byte(`{"entries":[{"kind":"weapon","id":"test","template":"native","offHandModels":` + renderer + `}]}`)
	assets := map[string][]byte{"content.json": raw}
	if marketModelReferences(assets) == nil {
		t.Fatal("accepted absent off-hand model")
	}
	assets["assets/left.glb"] = []byte{}
	if marketModelReferences(assets) == nil {
		t.Fatal("accepted absent off-hand texture")
	}
	assets["assets/left.png"] = []byte{}
	if err := marketModelReferences(assets); err != nil {
		t.Fatal(err)
	}
}

func TestThiefPackageContent(t *testing.T) {
	raw, err := os.ReadFile("../../marketplace/packages/thief/content.json")
	if err != nil {
		t.Fatal(err)
	}
	if err := marketContent(raw); err != nil {
		t.Fatal(err)
	}
}

func TestMarketThiefModifierBounds(t *testing.T) {
	for _, test := range []struct {
		fields string
		valid  bool
	}{
		{`"awareness":-1,"talent":1,"focusCapacity":10`, true},
		{`"awareness":1.01`, false}, {`"awareness":-1.01`, false},
		{`"talent":1.01`, false}, {`"talent":-1.01`, false},
		{`"focusCapacity":-1`, false}, {`"focusCapacity":11`, false},
		{`"focusCapacity":0.5`, false}, {`"talent":"fast"`, false},
		{`"focusCapacities":1`, false},
	} {
		t.Run(test.fields, func(t *testing.T) {
			raw := []byte(`{"entries":[{"kind":"item","id":"test","template":"native","modifiers":{` + test.fields + `}}]}`)
			if err := marketContent(raw); (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}

func TestMarketThiefNativeFields(t *testing.T) {
	for _, test := range []struct {
		kind, fields string
		valid        bool
	}{
		{"class", `"skills":{"m_Sneak":true,"m_TrapDisarm":true,"m_FindTreasure":false}`, true},
		{"class", `"skills":{"m_Sneak":1}`, false},
		{"class", `"skills":{"m_UnknownSkill":false}`, false},
		{"weapon", `"m_NoRegularAttack":false`, true},
		{"item", `"m_NoRegularAttack":false`, false},
		{"proficiency", `"m_Target":"None"`, true},
		{"weapon", `"m_Target":"None"`, false},
	} {
		raw := []byte(`{"entries":[{"kind":"` + test.kind + `","id":"test","template":"native","fields":{` + test.fields + `}}]}`)
		if err := marketContent(raw); (err == nil) != test.valid {
			t.Fatalf("%s %s: valid=%v, err=%v", test.kind, test.fields, test.valid, err)
		}
	}
}
