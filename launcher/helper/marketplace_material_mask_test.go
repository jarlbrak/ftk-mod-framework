package main

import (
	"bytes"
	"encoding/binary"
	"fmt"
	"image"
	"image/color"
	"image/png"
	"testing"
)

func TestModelMetallicGlossMasks(t *testing.T) {
	for _, route := range []string{"itemModels", "displayModels", "offHandModels"} {
		raw := []byte(fmt.Sprintf(`{"entries":[{"kind":"weapon","id":"gear","template":"bluntSmithHammer","%s":[{"path":".","model":"assets/a.glb","texture":"assets/a.png","metallicGlossTexture":"assets/mask.png"}]}]}`, route))
		if err := marketContent(raw); err != nil {
			t.Fatalf("%s: %v", route, err)
		}
		data := map[string][]byte{"content.json": raw, "assets/a.glb": {}, "assets/a.png": {}}
		if marketModelReferences(data) == nil {
			t.Fatalf("missing %s mask accepted", route)
		}
		data["assets/mask.png"] = maskTestPNG(t, image.NewNRGBA(image.Rect(0, 0, 1, 1)))
		if err := marketModelReferences(data); err != nil {
			t.Fatal(err)
		}
	}
	for _, token := range []string{`""`, `"../mask.png"`, `"assets/mask.jpg"`, `"/assets/mask.png"`, `true`, `1`, `{}`} {
		raw := []byte(fmt.Sprintf(`{"entries":[{"kind":"weapon","id":"gear","template":"bluntSmithHammer","itemModels":[{"path":".","model":"assets/a.glb","texture":"assets/a.png","metallicGlossTexture":%s}]}]}`, token))
		if marketContent(raw) == nil {
			t.Fatalf("invalid mask accepted: %s", token)
		}
	}
	for _, field := range []string{"", `,"metallicGlossTexture":null`} {
		raw := []byte(fmt.Sprintf(`{"entries":[{"kind":"weapon","id":"gear","template":"bluntSmithHammer","itemModels":[{"path":".","model":"assets/a.glb","texture":"assets/a.png"%s}]}]}`, field))
		if err := marketContent(raw); err != nil {
			t.Fatal(err)
		}
	}
	mask := "assets/mask.png"
	if err := marketModelRenderers([]marketModelRenderer{{Path: "boots(Clone)", Model: "assets/a.glb", Texture: "assets/a.png", NativeMesh: "boots", MetallicGlossTexture: &mask}}, true); err != nil {
		t.Fatal(err)
	}
}

func maskTestPNG(t *testing.T, img image.Image) []byte {
	t.Helper()
	var buf bytes.Buffer
	if err := png.Encode(&buf, img); err != nil {
		t.Fatal(err)
	}
	return buf.Bytes()
}

func TestMaskPNGAdmissionMatchesRuntimeChannelContract(t *testing.T) {
	rgba := image.NewNRGBA(image.Rect(0, 0, 1, 1))
	rgba.SetNRGBA(0, 0, color.NRGBA{R: 190, A: 90})
	valid := maskTestPNG(t, rgba)
	if valid[24] != 8 || valid[25] != 6 {
		t.Fatal("fixture is not RGBA8")
	}
	if err := marketMetallicGlossPNG(valid); err != nil {
		t.Fatal(err)
	}
	opaque := image.NewNRGBA(image.Rect(0, 0, 1, 1))
	opaque.SetNRGBA(0, 0, color.NRGBA{R: 190, A: 255})
	rgb := maskTestPNG(t, opaque)
	if rgb[25] != 2 {
		t.Fatal("fixture must be a valid RGB PNG")
	}
	cases := map[string][]byte{
		"RGB":             rgb,
		"gray":            maskTestPNG(t, image.NewGray(image.Rect(0, 0, 1, 1))),
		"RGBA16":          maskTestPNG(t, image.NewNRGBA64(image.Rect(0, 0, 1, 1))),
		"palette":         maskTestPNG(t, image.NewPaletted(image.Rect(0, 0, 1, 1), color.Palette{color.NRGBA{R: 100, A: 90}})),
		"over byte limit": make([]byte, (16<<20)+1),
		"truncated":       valid[:28],
		"empty":           {},
	}
	for name, change := range map[string]func([]byte){
		"signature":        func(raw []byte) { raw[0] = 0 },
		"IHDR name":        func(raw []byte) { raw[12] = 'X' },
		"IHDR size":        func(raw []byte) { binary.BigEndian.PutUint32(raw[8:12], 12) },
		"zero width":       func(raw []byte) { binary.BigEndian.PutUint32(raw[16:20], 0) },
		"oversized width":  func(raw []byte) { binary.BigEndian.PutUint32(raw[16:20], 4097) },
		"oversized height": func(raw []byte) { binary.BigEndian.PutUint32(raw[20:24], 4097) },
		"compression":      func(raw []byte) { raw[26] = 1 },
		"filter":           func(raw []byte) { raw[27] = 1 },
		"interlace":        func(raw []byte) { raw[28] = 2 },
	} {
		raw := append([]byte(nil), valid...)
		change(raw)
		cases[name] = raw
	}
	for name, raw := range cases {
		t.Run(name, func(t *testing.T) {
			if marketMetallicGlossPNG(raw) == nil {
				t.Fatal("invalid mask admitted")
			}
		})
	}
	for _, route := range []string{"itemModels", "displayModels", "offHandModels"} {
		raw := []byte(fmt.Sprintf(`{"entries":[{"kind":"weapon","id":"gear","template":"bluntSmithHammer","%s":[{"path":".","model":"assets/a.glb","texture":"assets/a.png","metallicGlossTexture":"assets/mask.png"}]}]}`, route))
		data := map[string][]byte{"content.json": raw, "assets/a.glb": {}, "assets/a.png": rgb, "assets/mask.png": rgb}
		if marketModelReferences(data) == nil {
			t.Fatalf("%s admitted RGB mask", route)
		}
		data["assets/mask.png"] = valid
		if err := marketModelReferences(data); err != nil {
			t.Fatalf("RGBA mask with unchanged RGB albedo: %v", err)
		}
	}
	for _, mask := range []string{"", `,"metallicGlossTexture":null`} {
		raw := []byte(fmt.Sprintf(`{"entries":[{"kind":"weapon","id":"gear","template":"bluntSmithHammer","itemModels":[{"path":".","model":"assets/a.glb","texture":"assets/a.png"%s}]}]}`, mask))
		if err := marketModelReferences(map[string][]byte{"content.json": raw, "assets/a.glb": {}, "assets/a.png": rgb}); err != nil {
			t.Fatal(err)
		}
	}
}
