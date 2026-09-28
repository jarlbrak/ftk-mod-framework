package main

import (
	"os"
	"path/filepath"
	"testing"
)

func TestMarketClassAffinityDeclarations(t *testing.T) {
	for _, test := range []struct {
		name, kind, affinity string
		valid                bool
	}{
		{"vitalityItem", "item", `{"classId":"blacksmith","modifiers":{"vitality":0.01}}`, true},
		{"boundedWeapon", "weapon", `{"classId":"blacksmith","modifiers":{"armor":1,"resistance":1,"vitality":0.01,"speed":0.01,"reflect":1}}`, true},
		{"wrongKind", "class", `{"classId":"blacksmith","modifiers":{"vitality":0.01}}`, false},
		{"blankClass", "item", `{"classId":" ","modifiers":{"vitality":0.01}}`, false},
		{"missingModifiers", "item", `{"classId":"blacksmith"}`, false},
		{"emptyModifiers", "item", `{"classId":"blacksmith","modifiers":{}}`, false},
		{"armorTooHigh", "item", `{"classId":"blacksmith","modifiers":{"armor":2}}`, false},
		{"negativeResistance", "item", `{"classId":"blacksmith","modifiers":{"resistance":-1}}`, false},
		{"reflectTooHigh", "item", `{"classId":"blacksmith","modifiers":{"reflect":2}}`, false},
		{"vitalityTooHigh", "item", `{"classId":"blacksmith","modifiers":{"vitality":0.02}}`, false},
		{"vitalityFraction", "item", `{"classId":"blacksmith","modifiers":{"vitality":0.005}}`, false},
		{"negativeSpeed", "item", `{"classId":"blacksmith","modifiers":{"speed":-0.01}}`, false},
		{"integerMustBeInteger", "item", `{"classId":"blacksmith","modifiers":{"armor":1.0}}`, false},
		{"unknownOuterField", "item", `{"classId":"blacksmith","name":"smith","modifiers":{"vitality":0.01}}`, false},
		{"unknownModifier", "item", `{"classId":"blacksmith","modifiers":{"vitality":0.01,"quickness":0.01}}`, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			body := `{"entries":[{"kind":"` + test.kind + `","id":"test","template":"template","classAffinity":` + test.affinity + `}]}`
			err := marketContent([]byte(body))
			if (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}

func TestBlacksmithPackageSource(t *testing.T) {
	root := "../../marketplace/packages/classgear"
	raw, err := os.ReadFile(filepath.Join(root, "content.json"))
	if err != nil {
		t.Fatal(err)
	}
	if err = marketContent(raw); err != nil {
		t.Fatal(err)
	}
	data := map[string][]byte{"content.json": raw}
	files, err := filepath.Glob(filepath.Join(root, "assets", "*"))
	if err != nil {
		t.Fatal(err)
	}
	for _, file := range files {
		extension := filepath.Ext(file)
		if extension != ".glb" && extension != ".png" {
			continue
		}
		bytes, readErr := os.ReadFile(file)
		if readErr != nil {
			t.Fatal(readErr)
		}
		data["assets/"+filepath.Base(file)] = bytes
		if extension == ".glb" {
			if err = marketModel(bytes); err != nil {
				t.Fatalf("%s: %v", file, err)
			}
		}
	}
	if err = marketModelReferences(data); err != nil {
		t.Fatal(err)
	}
}
