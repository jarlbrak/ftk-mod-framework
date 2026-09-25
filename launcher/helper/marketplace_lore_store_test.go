package main

import (
	"encoding/json"
	"testing"
)

func TestMarketplaceLoreStoreUnlock(t *testing.T) {
	entry := func() map[string]interface{} {
		return map[string]interface{}{"kind": "loreStoreUnlock", "id": "all"}
	}
	encode := func(e map[string]interface{}) []byte {
		b, err := json.Marshal(map[string]interface{}{"entries": []interface{}{e}})
		if err != nil {
			t.Fatal(err)
		}
		return b
	}
	if err := marketContent(encode(entry())); err != nil {
		t.Fatal(err)
	}
	tests := map[string]func(map[string]interface{}){
		"other scope":    func(e map[string]interface{}) { e["id"] = "Busker" },
		"missing scope":  func(e map[string]interface{}) { delete(e, "id") },
		"template":       func(e map[string]interface{}) { e["template"] = "hunter" },
		"display name":   func(e map[string]interface{}) { e["displayName"] = "Everything" },
		"description":    func(e map[string]interface{}) { e["description"] = "Everything" },
		"row overrides":  func(e map[string]interface{}) { e["fields"] = map[string]interface{}{"goldvalue": 1} },
		"proficiencies":  func(e map[string]interface{}) { e["proficiencies"] = []interface{}{"x"} },
		"capability":     func(e map[string]interface{}) { e["guardian"] = true },
		"unknown field":  func(e map[string]interface{}) { e["scope"] = "all" },
		"wrong spelling": func(e map[string]interface{}) { e["kind"] = "lorestoreunlock" },
	}
	for name, mutate := range tests {
		t.Run(name, func(t *testing.T) {
			e := entry()
			mutate(e)
			if marketContent(encode(e)) == nil {
				t.Fatal("invalid Lore Store unlock accepted")
			}
		})
	}
}
