package main

import (
	"fmt"
	"strings"
	"testing"
)

func TestMarketHeadProfileAdmission(t *testing.T) {
	model := `{"path":"helmKettle/mesh","model":"assets/hood.glb","texture":"assets/hood.png","matte":true}`
	fallback := `"itemModels":[` + model + `]`
	tests := []struct {
		name, profiles string
		valid          bool
	}{
		{"native", `[{"nativeSkinset":"treasureHunter_Female","model":` + model + `}]`, true},
		{"custom dormant", `[{"customRace":{"modGuid":"com.ftkmf.possum","key":"possum"},"model":` + model + `}]`, true},
		{"both selectors", `[{"nativeSkinset":"treasureHunter_Female","customRace":{"modGuid":"x","key":"y"},"model":` + model + `}]`, false},
		{"missing selector", `[{"model":` + model + `}]`, false},
		{"duplicate", `[{"nativeSkinset":"treasureHunter_Female","model":` + model + `},{"nativeSkinset":"treasureHunter_Female","model":` + model + `}]`, false},
		{"unknown field", `[{"nativeSkinset":"treasureHunter_Female","unknown":1,"model":` + model + `}]`, false},
		{"bad matte", `[{"nativeSkinset":"treasureHunter_Female","model":{"path":"helmKettle/mesh","model":"assets/hood.glb","texture":"assets/hood.png","matte":"true"}}]`, false},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			entry := `{"entries":[{"kind":"item","id":"hood","template":"helmetHeavy1",` + fallback + `,"headProfiles":` + tt.profiles + `}]}`
			err := marketContent([]byte(entry))
			if (err == nil) != tt.valid {
				t.Fatalf("valid=%v err=%v", tt.valid, err)
			}
		})
	}
	if err := marketContent([]byte(`{"entries":[{"kind":"weapon","id":"hood","template":"dualKnife",` + fallback + `,"headProfiles":[{"nativeSkinset":"treasureHunter_Female","model":` + model + `}]}]}`)); err == nil {
		t.Fatal("weapon headProfiles admitted")
	}
}

func TestMarketHeadProfileLimit(t *testing.T) {
	fallback := []marketModelRenderer{{Path: "helmKettle", Model: "assets/head.glb", Texture: "assets/head.png"}}
	profiles := make([]marketHeadProfile, 256)
	for i := range profiles {
		profiles[i] = marketHeadProfile{NativeSkinset: fmt.Sprintf("skin_%d", i), Model: fallback[0]}
	}
	if err := marketHeadProfiles(profiles, fallback); err != nil {
		t.Fatal(err)
	}
	profiles = append(profiles, marketHeadProfile{NativeSkinset: "overflow", Model: fallback[0]})
	if err := marketHeadProfiles(profiles, fallback); err == nil {
		t.Fatal("257 profiles admitted")
	}
}

func TestMarketHeadFaceAdmission(t *testing.T) {
	model := `{"path":"helmKettle","model":"assets/head.glb","texture":"assets/head.png"}`
	base := `{"bodyPath":"body","planes":[{"normal":[1,0,0],"distance":1},{"normal":[-1,0,0],"distance":1},{"normal":[0,1,0],"distance":1},{"normal":[0,-1,0],"distance":1}],"upperHair":"preserve","lowerHair":"clipStrictHead"}`
	wrap := func(face string) []byte {
		return []byte(`{"entries":[{"kind":"item","id":"hood","template":"helmetHeavy1","itemModels":[` + model + `],"headProfiles":[{"nativeSkinset":"treasureHunter_Female","model":` + model + `,"faceOcclusion":` + face + `}]}]}`)
	}
	if err := marketContent(wrap(base)); err != nil {
		t.Fatal(err)
	}
	for _, bad := range []string{
		strings.Replace(base, `"upperHair":"preserve"`, `"upperHair":"hideRenderer"`, 1),
		strings.Replace(base, `"normal":[1,0,0]`, `"normal":[2,0,0]`, 1),
		strings.Replace(base, `"distance":1`, `"distance":"1"`, 1),
		strings.Replace(base, `"bodyPath":"body"`, `"unknown":1`, 1),
	} {
		if err := marketContent(wrap(bad)); err == nil {
			t.Fatalf("invalid face admitted: %s", bad)
		}
	}
}

func TestMarketSharedExchangeAdmission(t *testing.T) {
	tests := []struct {
		name  string
		entry string
		valid bool
	}{
		{"armor", `{"kind":"item","id":"hood","template":"helmetHeavy1","thiefArmor":{"family":"locksmith","slot":"head"}}`, true},
		{"armament", `{"kind":"weapon","id":"blade","template":"dualKnife","thiefArmament":"nightblade"}`, true},
		{"wrong armor slot", `{"kind":"item","id":"hood","template":"helmetHeavy1","thiefArmor":{"family":"locksmith","slot":"trinket"}}`, false},
		{"wrong armor kind", `{"kind":"weapon","id":"blade","template":"dualKnife","thiefArmor":{"family":"locksmith","slot":"head"}}`, false},
		{"unknown family", `{"kind":"item","id":"hood","template":"helmetHeavy1","thiefArmor":{"family":"guardian","slot":"head"}}`, false},
		{"empty armament", `{"kind":"weapon","id":"blade","template":"dualKnife","thiefArmament":""}`, false},
		{"ordinary drop", `{"kind":"item","id":"token","template":"royalJewel","enemyDropRule":{"minimumDisplayedLevel":8,"ordinaryChancePercent":10,"bossChancePercent":50,"guaranteedByOpportunity":6,"namedBossGroups":[["krakenHead","krakenTentacle"]]}}`, true},
		{"missing pity", `{"kind":"item","id":"token","template":"royalJewel","enemyDropRule":{"minimumDisplayedLevel":8,"ordinaryChancePercent":10,"bossChancePercent":50}}`, false},
		{"duplicate boss", `{"kind":"item","id":"token","template":"royalJewel","enemyDropRule":{"minimumDisplayedLevel":8,"ordinaryChancePercent":10,"bossChancePercent":50,"guaranteedByOpportunity":6,"namedBossGroups":[["krakenHead"],["krakenHead"]]}}`, false},
		{"class offers", `{"kind":"class","id":"thief","template":"hunter","townExchange":{"offers":[{"item":"blade","name":"Blade","family":"Nightblade","slot":"RightHand"}]}}`, true},
		{"other currency", `{"kind":"class","id":"thief","template":"hunter","townExchange":{"token":"other","offers":[{"item":"blade","name":"Blade","family":"Nightblade","slot":"RightHand"}]}}`, false},
		{"bad offer slot", `{"kind":"class","id":"thief","template":"hunter","townExchange":{"offers":[{"item":"blade","name":"Blade","family":"Nightblade","slot":"Charm"}]}}`, false},
		{"hair", `{"kind":"item","id":"hood","template":"helmetHeavy1","helmetHairVisibility":{"top":true,"bottom":false}}`, true},
		{"coerced hair", `{"kind":"item","id":"hood","template":"helmetHeavy1","helmetHairVisibility":{"top":"true","bottom":false}}`, false},
		{"pistol", `{"kind":"weapon","id":"pistol","template":"firearm","precisionWeapon":"pistol","fields":{"m_ObjectSlot":"twoHands","m_NoRegularAttack":true,"m_NoFocus":true}}`, true},
		{"shot", `{"kind":"proficiency","id":"shot","template":"attack","precisionAction":"shot"}`, true},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			err := marketContent([]byte(`{"entries":[` + test.entry + `]}`))
			if (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
	if err := marketContent([]byte(`{"entries":[],"townExchangeCatalogs":[{"ownerClass":"com.example:thief","offers":[{"item":"com.example:hood","name":"Hood","family":"Locksmith","slot":"Head"}]}]}`)); err != nil {
		t.Fatalf("standalone catalog: %v", err)
	}
}

func TestMarketItemMatteAdmission(t *testing.T) {
	tests := []struct {
		name  string
		body  string
		valid bool
	}{
		{"rigid", `{"kind":"item","id":"hood","template":"helmetHeavy1","itemModels":[{"path":".","model":"assets/hood.glb","texture":"assets/hood.png","matte":true}]}`, true},
		{"display false", `{"kind":"item","id":"hood","template":"helmetHeavy1","displayModels":[{"path":".","model":"assets/hood.glb","texture":"assets/hood.png","matte":false}]}`, true},
		{"apparel", `{"kind":"item","id":"boots","template":"boots1","apparelModels":{"femaleBinding":"female","maleBinding":"male","renderers":[{"path":"boots","nativeMesh":"nativeBoots","model":"assets/boots.glb","texture":"assets/boots.png","matte":true}]}}`, true},
		{"coerced", `{"kind":"item","id":"hood","template":"helmetHeavy1","itemModels":[{"path":".","model":"assets/hood.glb","texture":"assets/hood.png","matte":"true"}]}`, false},
		{"null", `{"kind":"item","id":"hood","template":"helmetHeavy1","itemModels":[{"path":".","model":"assets/hood.glb","texture":"assets/hood.png","matte":null}]}`, false},
		{"class body", `{"kind":"class","id":"thief","template":"hunter","playerModels":[{"skinset":"female","body":[{"path":"body","model":"assets/body.glb","texture":"assets/body.png","matte":true}]}]}`, false},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			err := marketContent([]byte(`{"entries":[` + test.body + `]}`))
			if (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}
