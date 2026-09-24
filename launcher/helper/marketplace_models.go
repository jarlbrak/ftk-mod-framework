package main

import (
	"bytes"
	"encoding/binary"
	"encoding/json"
	"errors"
	"fmt"
	"path"
	"strings"
)

// Models are data only. Validate the bounded container and every buffer range
// before installation; the runtime additionally validates its exact rig contract.
func marketModel(raw []byte) error {
	if len(raw) < 28 || len(raw) > 32<<20 || string(raw[:4]) != "glTF" || binary.LittleEndian.Uint32(raw[4:8]) != 2 || uint64(binary.LittleEndian.Uint32(raw[8:12])) != uint64(len(raw)) {
		return errors.New("invalid or oversized GLB header")
	}
	jsonLen := uint64(binary.LittleEndian.Uint32(raw[12:16]))
	if jsonLen == 0 || jsonLen > marketLimit || jsonLen%4 != 0 || jsonLen+28 > uint64(len(raw)) || string(raw[16:20]) != "JSON" {
		return errors.New("invalid GLB JSON chunk")
	}
	end := 20 + int(jsonLen)
	binLen := uint64(binary.LittleEndian.Uint32(raw[end : end+4]))
	if binLen == 0 || binLen%4 != 0 || string(raw[end+4:end+8]) != "BIN\x00" || uint64(end+8)+binLen != uint64(len(raw)) {
		return errors.New("invalid GLB binary chunk")
	}
	var tree interface{}
	decoder := json.NewDecoder(bytes.NewReader(raw[20:end]))
	if err := decoder.Decode(&tree); err != nil {
		return err
	}
	if err := rejectModelReferences(tree, 0); err != nil {
		return err
	}
	var doc struct {
		Asset struct {
			Version string `json:"version"`
		} `json:"asset"`
		Buffers []struct {
			ByteLength int64 `json:"byteLength"`
		} `json:"buffers"`
		Views []struct {
			Buffer int   `json:"buffer"`
			Offset int64 `json:"byteOffset"`
			Length int64 `json:"byteLength"`
			Stride int64 `json:"byteStride"`
		} `json:"bufferViews"`
		Accessors []struct {
			View      *int   `json:"bufferView"`
			Offset    int64  `json:"byteOffset"`
			Count     int64  `json:"count"`
			Component int    `json:"componentType"`
			Type      string `json:"type"`
		} `json:"accessors"`
		Meshes []struct {
			Primitives []struct {
				Attributes map[string]int `json:"attributes"`
				Indices    *int           `json:"indices"`
				Mode       *int           `json:"mode"`
			} `json:"primitives"`
		} `json:"meshes"`
	}
	if err := json.Unmarshal(raw[20:end], &doc); err != nil {
		return err
	}
	if doc.Asset.Version != "2.0" || len(doc.Buffers) != 1 || doc.Buffers[0].ByteLength <= 0 || uint64(doc.Buffers[0].ByteLength) > binLen || binLen-uint64(doc.Buffers[0].ByteLength) > 3 || len(doc.Views) == 0 || len(doc.Views) > 4096 || len(doc.Accessors) == 0 || len(doc.Accessors) > 4096 || len(doc.Meshes) != 1 {
		return errors.New("unsupported GLB structure")
	}
	for _, view := range doc.Views {
		if view.Buffer != 0 || view.Offset < 0 || view.Length <= 0 || view.Offset > doc.Buffers[0].ByteLength || view.Length > doc.Buffers[0].ByteLength-view.Offset || view.Stride != 0 {
			return errors.New("invalid GLB buffer view")
		}
	}
	sizes := map[int]int64{5123: 2, 5125: 4, 5126: 4}
	widths := map[string]int64{"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}
	for _, acc := range doc.Accessors {
		size, width := sizes[acc.Component], widths[acc.Type]
		if acc.View == nil || *acc.View < 0 || *acc.View >= len(doc.Views) || acc.Offset < 0 || acc.Count <= 0 || acc.Count > 1000000 || size == 0 || width == 0 {
			return errors.New("invalid GLB accessor")
		}
		view := doc.Views[*acc.View]
		if acc.Offset > view.Length || acc.Count > (view.Length-acc.Offset)/(size*width) || (view.Offset+acc.Offset)%size != 0 {
			return errors.New("GLB accessor outside buffer or misaligned")
		}
	}
	primitives := doc.Meshes[0].Primitives
	if len(primitives) == 0 || len(primitives) > 32 {
		return errors.New("invalid GLB primitive count")
	}
	for _, primitive := range primitives {
		pos, ok := primitive.Attributes["POSITION"]
		if !ok || pos < 0 || pos >= len(doc.Accessors) || primitive.Indices == nil || *primitive.Indices < 0 || *primitive.Indices >= len(doc.Accessors) || primitive.Mode != nil && *primitive.Mode != 4 {
			return errors.New("invalid GLB triangle primitive")
		}
		position := doc.Accessors[pos]
		indices := doc.Accessors[*primitive.Indices]
		if position.Type != "VEC3" || position.Component != 5126 || position.Count >= 65535 || indices.Type != "SCALAR" || indices.Component != 5123 && indices.Component != 5125 || indices.Count%3 != 0 {
			return errors.New("unsupported GLB position or index accessor")
		}
		for _, index := range primitive.Attributes {
			if index < 0 || index >= len(doc.Accessors) {
				return errors.New("invalid GLB attribute reference")
			}
		}
		view := doc.Views[*indices.View]
		start := int64(end+8) + view.Offset + indices.Offset
		size := sizes[indices.Component]
		for i := int64(0); i < indices.Count; i++ {
			offset := start + i*size
			var vertex uint32
			if size == 2 {
				vertex = uint32(binary.LittleEndian.Uint16(raw[offset : offset+2]))
			} else {
				vertex = binary.LittleEndian.Uint32(raw[offset : offset+4])
			}
			if int64(vertex) >= position.Count {
				return errors.New("GLB triangle index exceeds vertex count")
			}
		}
	}
	return nil
}

func rejectModelReferences(value interface{}, depth int) error {
	if depth > 32 {
		return errors.New("GLB JSON depth exceeded")
	}
	switch v := value.(type) {
	case map[string]interface{}:
		for key, child := range v {
			switch key {
			case "uri", "extensions", "extensionsUsed", "extensionsRequired", "animations", "sparse", "targets":
				return fmt.Errorf("unsupported GLB field %s", key)
			}
			if err := rejectModelReferences(child, depth+1); err != nil {
				return err
			}
		}
	case []interface{}:
		if len(v) > 65535 {
			return errors.New("GLB JSON array too large")
		}
		for _, child := range v {
			if err := rejectModelReferences(child, depth+1); err != nil {
				return err
			}
		}
	}
	return nil
}

type marketModelRenderer struct {
	Path       string `json:"path"`
	Model      string `json:"model"`
	Texture    string `json:"texture"`
	NativeMesh string `json:"nativeMesh,omitempty"`
}
type marketPlayerModel struct {
	Skinset  string                `json:"skinset"`
	Body     []marketModelRenderer `json:"body"`
	Apparel  []marketModelRenderer `json:"apparel,omitempty"`
	Backpack []marketModelRenderer `json:"backpack,omitempty"`
}

func marketModelRenderers(renderers []marketModelRenderer, apparel bool) error {
	if len(renderers) == 0 || len(renderers) > 32 {
		return errors.New("invalid model renderer count")
	}
	seen := map[string]bool{}
	for _, r := range renderers {
		if r.Path != "." && !marketSafePath(r.Path) || seen[r.Path] {
			return errors.New("invalid or duplicate renderer path")
		}
		seen[r.Path] = true
		if !marketSafePath(r.Model) || !strings.HasPrefix(r.Model, "assets/") || path.Ext(r.Model) != ".glb" || !marketSafePath(r.Texture) || !strings.HasPrefix(r.Texture, "assets/") || path.Ext(r.Texture) != ".png" {
			return errors.New("invalid model/texture package path")
		}
		if apparel && r.NativeMesh == "" || !apparel && r.NativeMesh != "" {
			return errors.New("nativeMesh is required only for conditional apparel")
		}
	}
	return nil
}
func marketModelReferences(data map[string][]byte) error {
	for name, raw := range data {
		if path.Ext(name) != ".json" || name == "manifest.json" {
			continue
		}
		var doc struct {
			Entries []struct {
				ItemModels    []marketModelRenderer `json:"itemModels"`
				DisplayModels []marketModelRenderer `json:"displayModels"`
				PlayerModels  []marketPlayerModel   `json:"playerModels"`
				Icon          string                `json:"icon"`
				ApparelModels *marketApparelModel   `json:"apparelModels"`
			} `json:"entries"`
		}
		if err := json.Unmarshal(raw, &doc); err != nil {
			return err
		}
		for _, e := range doc.Entries {
			if e.Icon != "" {
				if _, ok := data[e.Icon]; !ok {
					return errors.New("referenced icon absent from package: " + e.Icon)
				}
			}
			refs := append([]marketModelRenderer{}, e.ItemModels...)
			refs = append(refs, e.DisplayModels...)
			if e.ApparelModels != nil {
				refs = append(refs, e.ApparelModels.Renderers...)
			}
			for _, p := range e.PlayerModels {
				refs = append(refs, p.Body...)
				refs = append(refs, p.Apparel...)
				refs = append(refs, p.Backpack...)
			}
			for _, r := range refs {
				if _, ok := data[r.Model]; !ok {
					return errors.New("referenced model absent from package: " + r.Model)
				}
				if _, ok := data[r.Texture]; !ok {
					return errors.New("referenced texture absent from package: " + r.Texture)
				}
			}
		}
	}
	return nil
}

type marketItemModifiers struct {
	Armor      int     `json:"armor"`
	Resistance int     `json:"resistance"`
	Vitality   float64 `json:"vitality"`
	Speed      float64 `json:"speed"`
	Reflect    int     `json:"reflect"`
}

type marketAilmentImmunity struct {
	DisplayName string `json:"displayName"`
}

type marketGuardianBonuses struct {
	GuardHealPercent      int  `json:"guardHealPercent"`
	FocusHealBonusPercent int  `json:"focusHealBonusPercent"`
	RetaliationDamage     int  `json:"retaliationDamage"`
	WardDebuffs           bool `json:"wardDebuffs"`
	GuardFocusRestore     int  `json:"guardFocusRestore"`
	GuardReckoning        bool `json:"guardReckoning"`
	GuardCleanse          bool `json:"guardCleanse"`
}
type marketApparelModel struct {
	FemaleBinding string                `json:"femaleBinding"`
	MaleBinding   string                `json:"maleBinding"`
	Renderers     []marketModelRenderer `json:"renderers"`
}
