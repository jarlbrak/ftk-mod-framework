package main

import (
	"encoding/json"
	"os"
	"testing"
)

func TestActualUnreleasedProgressionPackages(t *testing.T) {
	for _, name := range []string{"paladin", "equipment-exchange"} {
		raw, err := os.ReadFile("../../marketplace/packages/" + name + "/content.json")
		if err != nil {
			t.Fatal(err)
		}
		if err := marketContent(raw); err != nil {
			t.Fatalf("%s: %v", name, err)
		}
	}
}

func TestProgressionAdmission(t *testing.T) {
	profile := map[string]interface{}{"physicalPercent": 75, "smitePercent": 100, "healingPercent": 100, "guardReductionPercent": 50}
	class := map[string]interface{}{"kind": "class", "id": "paladin", "template": "blacksmith", "guardian": true,
		"guardianProfile": profile, "guardianSmiteAction": "smite", "guardianEquipmentSets": []interface{}{
			map[string]interface{}{"id": "mercy", "head": "head", "body": "body", "feet": "feet", "oneHand": "hammer", "shield": "shield", "twoHand": "great",
				"minor": profile, "core": profile, "completion": map[string]interface{}{"wardDebuffs": true}, "coreProficiencies": []string{"smite"}},
		}}
	token := map[string]interface{}{"kind": "item", "id": "token", "template": "native",
		"enemyDropRule": map[string]interface{}{"minimumDisplayedLevel": 8, "ordinaryChancePercent": 10, "bossChancePercent": 50, "guaranteedByOpportunity": 6, "namedBossGroups": [][]string{{"boss", "arm"}}},
		"townExchange":  map[string]interface{}{"offers": []interface{}{map[string]interface{}{"item": "head", "name": "Mercy Head", "family": "Mercy", "slot": "Head"}}}}
	encode := func(entries ...map[string]interface{}) []byte {
		b, err := json.Marshal(map[string]interface{}{"entries": entries})
		if err != nil {
			t.Fatal(err)
		}
		return b
	}
	if err := marketContent(encode(class, token)); err != nil {
		t.Fatal(err)
	}
	tests := map[string]func(map[string]interface{}, map[string]interface{}){
		"non Guardian class": func(c, _ map[string]interface{}) { c["guardian"] = false },
		"missing profile value": func(c, _ map[string]interface{}) {
			delete(c["guardianProfile"].(map[string]interface{}), "healingPercent")
		},
		"profile out of bounds": func(c, _ map[string]interface{}) {
			c["guardianProfile"].(map[string]interface{})["physicalPercent"] = 250
		},
		"string percentage": func(c, _ map[string]interface{}) {
			c["guardianProfile"].(map[string]interface{})["physicalPercent"] = "75"
		},
		"negative Guard charge": func(c, _ map[string]interface{}) {
			c["guardianProfile"].(map[string]interface{})["guardPhysicalBonusPercent"] = -1
		},
		"excessive Guard charge": func(c, _ map[string]interface{}) {
			c["guardianProfile"].(map[string]interface{})["guardPhysicalBonusPercent"] = 51
		},
		"fractional Guard charge": func(c, _ map[string]interface{}) {
			c["guardianProfile"].(map[string]interface{})["guardPhysicalBonusPercent"] = 1.5
		},
		"string Guard charge": func(c, _ map[string]interface{}) {
			c["guardianProfile"].(map[string]interface{})["guardPhysicalBonusPercent"] = "50"
		},
		"string Guard bond": func(c, _ map[string]interface{}) {
			c["guardianProfile"].(map[string]interface{})["guardSmiteHealing"] = "true"
		},
		"null Guard bond": func(c, _ map[string]interface{}) {
			c["guardianProfile"].(map[string]interface{})["guardSmiteHealing"] = nil
		},
		"null Guard charge": func(c, _ map[string]interface{}) {
			c["guardianProfile"].(map[string]interface{})["guardPhysicalBonusPercent"] = nil
		},
		"null set Guard bond": func(c, _ map[string]interface{}) {
			c["guardianEquipmentSets"].([]interface{})[0].(map[string]interface{})["core"].(map[string]interface{})["guardSmiteHealing"] = nil
		},
		"null set Guard charge": func(c, _ map[string]interface{}) {
			c["guardianEquipmentSets"].([]interface{})[0].(map[string]interface{})["minor"].(map[string]interface{})["guardPhysicalBonusPercent"] = nil
		},
		"duplicate set member": func(c, _ map[string]interface{}) {
			c["guardianEquipmentSets"].([]interface{})[0].(map[string]interface{})["feet"] = "head"
		},
		"too many core actions": func(c, _ map[string]interface{}) {
			c["guardianEquipmentSets"].([]interface{})[0].(map[string]interface{})["coreProficiencies"] = []string{"a", "b", "c", "d", "e", "f", "g", "h", "i"}
		},
		"overlapping sets": func(c, _ map[string]interface{}) {
			sets := c["guardianEquipmentSets"].([]interface{})
			raw, _ := json.Marshal(sets[0])
			var second map[string]interface{}
			json.Unmarshal(raw, &second)
			second["id"] = "other"
			c["guardianEquipmentSets"] = append(sets, second)
		},
		"negative chance": func(_, c map[string]interface{}) {
			c["enemyDropRule"].(map[string]interface{})["ordinaryChancePercent"] = -1
		},
		"zero guarantee": func(_, c map[string]interface{}) {
			c["enemyDropRule"].(map[string]interface{})["guaranteedByOpportunity"] = 0
		},
		"missing chance": func(_, c map[string]interface{}) {
			delete(c["enemyDropRule"].(map[string]interface{}), "bossChancePercent")
		},
		"duplicate boss": func(_, c map[string]interface{}) {
			c["enemyDropRule"].(map[string]interface{})["namedBossGroups"] = [][]string{{"boss"}, {"boss"}}
		},
		"invalid slot": func(_, c map[string]interface{}) {
			c["townExchange"].(map[string]interface{})["offers"].([]interface{})[0].(map[string]interface{})["slot"] = "fake"
		},
		"unknown field": func(c, _ map[string]interface{}) {
			c["guardianProfile"].(map[string]interface{})["infiniteHealing"] = true
		},
	}
	baseline := encode(class, token)
	for name, mutate := range tests {
		t.Run(name, func(t *testing.T) {
			var copy struct {
				Entries []map[string]interface{} `json:"entries"`
			}
			if err := json.Unmarshal(baseline, &copy); err != nil {
				t.Fatal(err)
			}
			mutate(copy.Entries[0], copy.Entries[1])
			if err := marketContent(encode(copy.Entries...)); err == nil {
				t.Fatal("invalid progression accepted")
			}
		})
	}
}

func TestGuardThemeAdmission(t *testing.T) {
	for _, value := range []string{
		``,
		`,"guardSmiteHealing":false,"guardPhysicalBonusPercent":0`,
		`,"guardSmiteHealing":true,"guardPhysicalBonusPercent":50`,
	} {
		content := []byte(`{"entries":[{"kind":"class","id":"protector","template":"blacksmith","guardian":true,"guardianProfile":{"physicalPercent":100,"smitePercent":100,"healingPercent":100,"guardReductionPercent":50` + value + `}}]}`)
		if err := marketContent(content); err != nil {
			t.Fatal(err)
		}
	}
}
