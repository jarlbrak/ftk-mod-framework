package main

import (
	"encoding/json"
	"testing"
)

func TestMarketplaceRaceBindings(t *testing.T) {
	renderer := map[string]interface{}{"path": "playerCat", "model": "assets/body.glb", "texture": "assets/body.png"}
	binding := func() map[string]interface{} {
		return map[string]interface{}{"class": "hunter", "skinset": "hunter_Cat", "body": []interface{}{renderer}}
	}
	entry := func() map[string]interface{} {
		return map[string]interface{}{"kind": "race", "id": "possum", "displayName": "Possum", "raceBindings": []interface{}{binding()}}
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
		"missing display":  func(e map[string]interface{}) { delete(e, "displayName") },
		"missing bindings": func(e map[string]interface{}) { delete(e, "raceBindings") },
		"empty bindings":   func(e map[string]interface{}) { e["raceBindings"] = []interface{}{} },
		"too many bindings": func(e map[string]interface{}) {
			b := make([]interface{}, 65)
			for i := range b {
				b[i] = binding()
			}
			e["raceBindings"] = b
		},
		"duplicate class": func(e map[string]interface{}) { e["raceBindings"] = []interface{}{binding(), binding()} },
		"missing body": func(e map[string]interface{}) {
			b := binding()
			delete(b, "body")
			e["raceBindings"] = []interface{}{b}
		},
		"blank class": func(e map[string]interface{}) { b := binding(); b["class"] = " "; e["raceBindings"] = []interface{}{b} },
		"missing donor": func(e map[string]interface{}) {
			b := binding()
			delete(b, "skinset")
			e["raceBindings"] = []interface{}{b}
		},
		"unconditional apparel": func(e map[string]interface{}) {
			b := binding()
			b["apparel"] = []interface{}{renderer}
			e["raceBindings"] = []interface{}{b}
		},
		"row overrides":   func(e map[string]interface{}) { e["fields"] = map[string]interface{}{"skinsets": []interface{}{1}} },
		"nonrace binding": func(e map[string]interface{}) { e["kind"] = "class"; e["template"] = "hunter" },
		"unknown binding field": func(e map[string]interface{}) {
			b := binding()
			b["backpack"] = []interface{}{renderer}
			e["raceBindings"] = []interface{}{b}
		},
		"unsafe asset": func(e map[string]interface{}) {
			b := binding()
			b["body"] = []interface{}{map[string]interface{}{"path": ".", "model": "../escape.glb", "texture": "assets/body.png"}}
			e["raceBindings"] = []interface{}{b}
		},
	}
	for name, mutate := range tests {
		t.Run(name, func(t *testing.T) {
			e := entry()
			mutate(e)
			if marketContent(encode(e)) == nil {
				t.Fatal("invalid race accepted")
			}
		})
	}
	e := entry()
	b := binding()
	b["apparel"] = []interface{}{map[string]interface{}{"path": "hairTop", "nativeMesh": "catRuff", "model": "assets/ruff.glb", "texture": "assets/ruff.png"}}
	e["raceBindings"] = []interface{}{b}
	raw := encode(e)
	if err := marketContent(raw); err != nil {
		t.Fatal(err)
	}
	files := map[string][]byte{"content.json": raw}
	for _, asset := range []string{"assets/body.glb", "assets/body.png", "assets/ruff.glb", "assets/ruff.png"} {
		if marketModelReferences(files) == nil {
			t.Fatalf("missing %s accepted", asset)
		}
		files[asset] = []byte{}
	}
	if err := marketModelReferences(files); err != nil {
		t.Fatal(err)
	}
}
