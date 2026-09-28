package main

import "testing"

func TestMarketBlacksmithEquipmentDeclarations(t *testing.T) {
	for _, test := range []struct {
		name, kind, gear string
		valid            bool
	}{
		{"setHammer", "weapon", `{"setHammerArmor":2}`, true},
		{"overhand", "weapon", `{"overhandArmorPenalty":6}`, true},
		{"temper", "item", `{"temperArmor":5}`, true},
		{"noBenefit", "item", `{}`, false},
		{"multipleBenefits", "weapon", `{"setHammerArmor":2,"overhandArmorPenalty":2}`, false},
		{"temperWeapon", "weapon", `{"temperArmor":3}`, false},
		{"setHammerItem", "item", `{"setHammerArmor":2}`, false},
		{"classBenefit", "class", `{"temperArmor":3}`, false},
		{"tooMuchArmor", "weapon", `{"setHammerArmor":6}`, false},
		{"tooLittlePenalty", "weapon", `{"overhandArmorPenalty":1}`, false},
		{"negativePenalty", "weapon", `{"overhandArmorPenalty":-2}`, false},
		{"tooMuchTemper", "item", `{"temperArmor":6}`, false},
		{"fraction", "weapon", `{"setHammerArmor":2.5}`, false},
		{"wrongType", "item", `{"temperArmor":"3"}`, false},
		{"unknownField", "weapon", `{"setHammerArmor":2,"damageMultiplier":10}`, false},
		{"nullField", "weapon", `{"setHammerArmor":2,"temperArmor":null}`, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			body := `{"entries":[{"kind":"` + test.kind + `","id":"test","template":"template","blacksmithGear":` + test.gear + `}]}`
			err := marketContent([]byte(body))
			if (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}

func TestMarketItemAppearanceAlternatives(t *testing.T) {
	row := func(selector string) string {
		return `{"path":"armor","nativeMesh":"nativeArmor","model":"assets/armor.glb","texture":"assets/armor.png"` + selector + `}`
	}
	for _, test := range []struct {
		name, rows string
		valid      bool
	}{
		{"defaultAndCat", row("") + "," + row(`,"nativeSkinType":"Cat"`), true},
		{"twoSpecificAppearances", row(`,"nativeSkinType":"Cat"`) + "," + row(`,"nativeSkinType":"Demon"`), true},
		{"duplicateDefault", row("") + "," + row(""), false},
		{"duplicateCat", row(`,"nativeSkinType":"Cat"`) + "," + row(`,"nativeSkinType":"Cat"`), false},
		{"numericAppearance", row(`,"nativeSkinType":3`), false},
		{"numericString", row(`,"nativeSkinType":"3"`), false},
		{"noneAppearance", row(`,"nativeSkinType":"None"`), false},
		{"wrongCase", row(`,"nativeSkinType":"cat"`), false},
		{"unknownAppearance", row(`,"nativeSkinType":"CustomRace"`), false},
	} {
		t.Run(test.name, func(t *testing.T) {
			body := `{"entries":[{"kind":"item","id":"test","template":"armorHeavy1","apparelModels":{"femaleBinding":"blacksmith_Female","maleBinding":"blacksmith_Male","renderers":[` + test.rows + `]}}]}`
			if err := marketContent([]byte(body)); (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
	cat := "Cat"
	rows := []marketModelRenderer{{Path: "armor", NativeMesh: "nativeArmor", Model: "assets/armor.glb", Texture: "assets/armor.png", NativeSkinType: &cat}}
	if err := marketModelRenderers(rows, true); err == nil {
		t.Fatal("class apparel must reject item appearance selectors")
	}
	rows[0].NativeMesh = ""
	if err := marketModelRenderers(rows, false); err == nil {
		t.Fatal("rigid models must reject item appearance selectors")
	}
}
