package main

import (
	"bytes"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"testing"
)

func TestShortcutRoundtripPreservesOtherEntries(t *testing.T) {
	other := field{kind: 0, name: "7", children: []field{textField("AppName", "Other game"), textField("Exe", "\"/other\""), intField("appid", 123), {kind: 7, name: "unknown", value: []byte{1, 2, 3, 4, 5, 6, 7, 8}}}}
	source := encode([]field{{kind: 0, name: "shortcuts", children: []field{other}}})
	added, id, found, err := update(source, "/Games with spaces/FTK.app", "/icon.png", true)
	if err != nil || !found || id == 0 {
		t.Fatalf("add: %v", err)
	}
	parsed, err := decode(added)
	if err != nil {
		t.Fatal(err)
	}
	if !bytes.Equal(encode([]field{parsed[0].children[0]}), encode([]field{other})) {
		t.Fatal("other entry changed")
	}
	again, id2, found, err := update(added, "/Games with spaces/FTK.app", "/different.png", true)
	if err != nil || !found || id != id2 || !bytes.Equal(added, again) {
		t.Fatal("not idempotent")
	}
}
func TestManualShortcutStoredIDAndNamePreserved(t *testing.T) {
	src := encode([]field{{kind: 0, name: "shortcuts", children: []field{{kind: 0, name: "0", children: []field{textField("AppName", "My renamed FTK"), textField("Exe", "\"/FTK.app\""), intField("appid", 0xf1234567)}}}}})
	out, id, found, e := update(src, "/FTK.app", "/icon", false)
	if e != nil || !found || id != 0xf1234567 || !bytes.Equal(src, out) {
		t.Fatal("manual shortcut was not preserved")
	}
}
func TestMalformedAndUnsupportedVDFRejected(t *testing.T) {
	for _, b := range [][]byte{{0}, {0, 'x', 0, 99, 'z', 0, 8}, {0, 'x', 0, 8, 8, 0}, {0, 's', 0, 2, 'i', 0, 1}} {
		if _, e := decode(b); e == nil {
			t.Fatalf("accepted %v", b)
		}
	}
}
func TestIDCollisionRejected(t *testing.T) {
	id := idFor("\"/FTK.app\"", appName)
	src := encode([]field{{kind: 0, name: "shortcuts", children: []field{{kind: 0, name: "0", children: []field{intField("appid", id), textField("Exe", "/other")}}}}})
	for _, create := range []bool{true, false} {
		if _, _, _, e := update(src, "/FTK.app", "/icon", create); e == nil {
			t.Fatal("collision accepted")
		}
	}
}
func TestArtworkOnlyDoesNotCreateShortcut(t *testing.T) {
	out, id, found, e := update(nil, "/FTK.app", "/icon", false)
	if e != nil || found || len(out) != 0 || id != idFor("\"/FTK.app\"", appName) {
		t.Fatal("artwork-only created shortcut")
	}
}
func TestCustomArtworkPreserved(t *testing.T) {
	root := t.TempDir()
	art := filepath.Join(root, "art")
	config := filepath.Join(root, "config")
	os.MkdirAll(art, 0755)
	os.MkdirAll(filepath.Join(config, "grid"), 0755)
	for _, n := range []string{"header.png", "capsule.png", "hero.png", "logo.png", "icon.png"} {
		os.WriteFile(filepath.Join(art, n), []byte("bundled"), 0644)
	}
	custom := filepath.Join(config, "grid", "42.png")
	os.WriteFile(custom, []byte("custom"), 0644)
	if e := copyArt(config, art, 42); e != nil {
		t.Fatal(e)
	}
	b, _ := os.ReadFile(custom)
	if string(b) != "custom" {
		t.Fatal("overwrote custom art")
	}
	for _, n := range []string{"42p.png", "42_hero.png", "42_logo.png", "42_icon.png"} {
		if _, e := os.Stat(filepath.Join(config, "grid", n)); e != nil {
			t.Fatal(e)
		}
	}
}

func TestJPEGArtworkPreserved(t *testing.T) {
	root := t.TempDir()
	art := filepath.Join(root, "art")
	config := filepath.Join(root, "config")
	os.MkdirAll(art, 0755)
	os.MkdirAll(filepath.Join(config, "grid"), 0755)
	for _, n := range []string{"header.png", "capsule.png", "hero.png", "logo.png", "icon.png"} {
		os.WriteFile(filepath.Join(art, n), []byte("bundled"), 0644)
	}
	existing := filepath.Join(config, "grid", "42p.JPG")
	os.WriteFile(existing, []byte("custom"), 0644)
	if e := copyArt(config, art, 42); e != nil {
		t.Fatal(e)
	}
	if _, e := os.Stat(filepath.Join(config, "grid", "42p.png")); !os.IsNotExist(e) {
		t.Fatal("competing PNG installed")
	}
}

func TestRegisterRefusesRunningSteam(t *testing.T) {
	if runtime.GOOS == "windows" {
		t.Skip("Unix PATH process shim")
	}
	root := t.TempDir()
	shim := filepath.Join(root, "pgrep")
	if e := os.WriteFile(shim, []byte("#!/bin/sh\nexit 0\n"), 0755); e != nil {
		t.Fatal(e)
	}
	t.Setenv("PATH", root+string(os.PathListSeparator)+os.Getenv("PATH"))
	art := filepath.Join(root, "art")
	os.MkdirAll(art, 0755)
	for _, n := range []string{"header.png", "capsule.png", "hero.png", "logo.png", "icon.png"} {
		os.WriteFile(filepath.Join(art, n), []byte("art"), 0644)
	}
	target := filepath.Join(root, "launcher")
	os.WriteFile(target, []byte("launcher"), 0755)
	e := run([]string{"register", "--launcher", target, "--art", art, "--steam-root", root})
	if e == nil || !strings.Contains(e.Error(), "close Steam") {
		t.Fatalf("expected Steam refusal, got %v", e)
	}
}
