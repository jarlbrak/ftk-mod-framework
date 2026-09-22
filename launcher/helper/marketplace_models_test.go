package main

import (
	"encoding/binary"
	"encoding/json"
	"os"
	"path/filepath"
	"testing"
)

func modelFixture(t *testing.T, edit func(map[string]interface{})) []byte {
	t.Helper()
	doc := map[string]interface{}{
		"asset":       map[string]interface{}{"version": "2.0"},
		"buffers":     []interface{}{map[string]interface{}{"byteLength": 42}},
		"bufferViews": []interface{}{map[string]interface{}{"buffer": 0, "byteOffset": 0, "byteLength": 36}, map[string]interface{}{"buffer": 0, "byteOffset": 36, "byteLength": 6}},
		"accessors":   []interface{}{map[string]interface{}{"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3"}, map[string]interface{}{"bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR"}},
		"meshes":      []interface{}{map[string]interface{}{"primitives": []interface{}{map[string]interface{}{"attributes": map[string]interface{}{"POSITION": 0}, "indices": 1, "mode": 4}}}},
	}
	if edit != nil {
		edit(doc)
	}
	j, err := json.Marshal(doc)
	if err != nil {
		t.Fatal(err)
	}
	for len(j)%4 != 0 {
		j = append(j, ' ')
	}
	raw := make([]byte, 12+8+len(j)+8+44)
	copy(raw, "glTF")
	binary.LittleEndian.PutUint32(raw[4:], 2)
	binary.LittleEndian.PutUint32(raw[8:], uint32(len(raw)))
	binary.LittleEndian.PutUint32(raw[12:], uint32(len(j)))
	copy(raw[16:], "JSON")
	copy(raw[20:], j)
	end := 20 + len(j)
	binary.LittleEndian.PutUint32(raw[end:], 44)
	copy(raw[end+4:], "BIN\x00")
	binary.LittleEndian.PutUint16(raw[end+8+38:], 1)
	binary.LittleEndian.PutUint16(raw[end+8+40:], 2)
	return raw
}
func TestMarketModelValidation(t *testing.T) {
	if err := marketModel(modelFixture(t, nil)); err != nil {
		t.Fatal(err)
	}
	cases := map[string]func(map[string]interface{}){
		"external URI": func(d map[string]interface{}) {
			d["buffers"].([]interface{})[0].(map[string]interface{})["uri"] = "https://invalid/model.bin"
		},
		"sparse": func(d map[string]interface{}) {
			d["accessors"].([]interface{})[0].(map[string]interface{})["sparse"] = map[string]interface{}{}
		},
		"overflow": func(d map[string]interface{}) {
			d["accessors"].([]interface{})[0].(map[string]interface{})["count"] = 1000000000
		},
		"bad view": func(d map[string]interface{}) {
			d["accessors"].([]interface{})[0].(map[string]interface{})["bufferView"] = 99
		},
		"buffer escape": func(d map[string]interface{}) {
			d["bufferViews"].([]interface{})[0].(map[string]interface{})["byteLength"] = 1000
		},
		"negative offset": func(d map[string]interface{}) {
			d["bufferViews"].([]interface{})[0].(map[string]interface{})["byteOffset"] = -1
		},
		"extension": func(d map[string]interface{}) { d["extensionsRequired"] = []string{"KHR_draco_mesh_compression"} },
	}
	for name, edit := range cases {
		t.Run(name, func(t *testing.T) {
			if marketModel(modelFixture(t, edit)) == nil {
				t.Fatal("accepted invalid model")
			}
		})
	}
	raw := modelFixture(t, nil)
	raw[len(raw)-4] = 99
	if marketModel(raw) == nil {
		t.Fatal("accepted out-of-range triangle")
	}
	for _, length := range []int{0, 4, 12, 27, len(raw) - 1} {
		if marketModel(raw[:length]) == nil {
			t.Fatalf("accepted truncation %d", length)
		}
	}
}
func TestPaladinOriginalModels(t *testing.T) {
	files, err := filepath.Glob("../../art-experiments/paladin-equipment/*.glb")
	if err != nil {
		t.Fatal(err)
	}
	if len(files) == 0 {
		t.Skip("original equipment assets not in checkout")
	}
	for _, file := range files {
		t.Run(filepath.Base(file), func(t *testing.T) {
			raw, err := os.ReadFile(file)
			if err != nil {
				t.Fatal(err)
			}
			if err = marketModel(raw); err != nil {
				t.Fatal(err)
			}
		})
	}
}

func TestMarketModelDeclarations(t *testing.T) {
	good := []byte(`{"entries":[{"kind":"class","id":"paladin","template":"blacksmith","guardian":true,"playerModels":[{"skinset":"blacksmith_Female","body":[{"path":"playerBlacksmith","model":"assets/body.glb","texture":"assets/body.png"}]}]}]}`)
	if err := marketContent(good); err != nil {
		t.Fatal(err)
	}
	for _, raw := range []string{
		`{"entries":[{"kind":"weapon","id":"bad","template":"bluntSmithHammer","guardian":true}]}`,
		`{"entries":[{"kind":"weapon","id":"bad","template":"bluntSmithHammer","itemModels":[{"path":".","model":"../evil.glb","texture":"assets/a.png"}]}]}`,
		`{"entries":[{"kind":"class","id":"bad","template":"blacksmith","playerModels":[]}]}`,
		`{"entries":[{"kind":"class","id":"bad","template":"blacksmith","behavior":"arbitrary-code"}]}`,
	} {
		if marketContent([]byte(raw)) == nil {
			t.Fatalf("accepted %s", raw)
		}
	}
	data := map[string][]byte{"class.json": good}
	if marketModelReferences(data) == nil {
		t.Fatal("accepted missing assets")
	}
	data["assets/body.glb"] = []byte{1}
	data["assets/body.png"] = []byte{1}
	if err := marketModelReferences(data); err != nil {
		t.Fatal(err)
	}
}

func TestPaladinPackageSource(t *testing.T) {
	root := "../../marketplace/packages/paladin"
	raw, err := os.ReadFile(filepath.Join(root, "content.json"))
	if os.IsNotExist(err) {
		t.Skip("package source absent")
	}
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
		bytes, err := os.ReadFile(file)
		if err != nil {
			t.Fatal(err)
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

func TestMarketBackpackDeclarationAndReferences(t *testing.T) {
	raw := []byte(`{"entries":[{"kind":"class","id":"paladin","template":"blacksmith","playerModels":[{"skinset":"blacksmith_Female","body":[{"path":"body","model":"assets/body.glb","texture":"assets/palette.png"}],"backpack":[{"path":".","model":"assets/pack.glb","texture":"assets/palette.png"}]}]}]}`)
	if err := marketContent(raw); err != nil {
		t.Fatal(err)
	}
	assets := map[string][]byte{"content.json": raw, "assets/body.glb": {}, "assets/palette.png": {}}
	if marketModelReferences(assets) == nil {
		t.Fatal("accepted absent backpack asset")
	}
	assets["assets/pack.glb"] = []byte{}
	if err := marketModelReferences(assets); err != nil {
		t.Fatal(err)
	}
	var doc map[string]interface{}
	if err := json.Unmarshal(raw, &doc); err != nil {
		t.Fatal(err)
	}
	model := doc["entries"].([]interface{})[0].(map[string]interface{})["playerModels"].([]interface{})[0].(map[string]interface{})
	model["backpack"] = []interface{}{}
	invalid, _ := json.Marshal(doc)
	if marketContent(invalid) == nil {
		t.Fatal("accepted empty backpack declaration")
	}
	model["backpack"] = []interface{}{map[string]interface{}{"path": ".", "model": "../escape.glb", "texture": "assets/palette.png"}}
	invalid, _ = json.Marshal(doc)
	if marketContent(invalid) == nil {
		t.Fatal("accepted backpack traversal")
	}
}

func TestMarketDisplayModelScope(t *testing.T) {
	good := []byte(`{"entries":[{"kind":"item","id":"custom","template":"shieldWood2","displayModels":[{"path":"shieldChild","model":"assets/display.glb","texture":"assets/palette.png"}]}]}`)
	if err := marketContent(good); err != nil {
		t.Fatal(err)
	}
	files := map[string][]byte{"content.json": good, "assets/palette.png": {}}
	if marketModelReferences(files) == nil {
		t.Fatal("missing display asset accepted")
	}
	files["assets/display.glb"] = []byte{}
	if err := marketModelReferences(files); err != nil {
		t.Fatal(err)
	}
	for _, raw := range []string{
		`{"entries":[{"kind":"class","id":"custom","template":"blacksmith","displayModels":[{"path":".","model":"assets/display.glb","texture":"assets/palette.png"}]}]}`,
		`{"entries":[{"kind":"item","id":"custom","template":"shieldWood2","displayModels":[]}]}`,
		`{"entries":[{"kind":"item","id":"custom","template":"shieldWood2","displayModels":[{"path":"../outside","model":"assets/display.glb","texture":"assets/palette.png"}]}]}`,
	} {
		if marketContent([]byte(raw)) == nil {
			t.Fatalf("accepted invalid display declaration: %s", raw)
		}
	}
}
