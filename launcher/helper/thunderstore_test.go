package main

import (
	"archive/zip"
	"bytes"
	"crypto/sha256"
	"fmt"
	"os"
	"path/filepath"
	"testing"
)

func bootstrapFixture(t *testing.T, extra string) string {
	t.Helper()
	bundle := t.TempDir()
	var data bytes.Buffer
	writer := zip.NewWriter(&data)
	for _, name := range []string{"FtkModdedLauncher.exe", "FtkModdedLauncher.exe.config", "FTKModFramework.dll", "ftkmf-launcher-helper.exe", "bundle-manifest.json", "install.ps1", "assets/steam/icon.png"} {
		output, err := writer.Create("For The King Modded/" + name)
		if err != nil {
			t.Fatal(err)
		}
		output.Write([]byte("fixture " + name))
	}
	if extra != "" {
		output, err := writer.Create(extra)
		if err != nil {
			t.Fatal(err)
		}
		output.Write([]byte("unexpected"))
	}
	if err := writer.Close(); err != nil {
		t.Fatal(err)
	}
	os.WriteFile(filepath.Join(bundle, thunderstoreArchive), data.Bytes(), 0600)
	os.WriteFile(filepath.Join(bundle, "launcher.sha256"), []byte(fmt.Sprintf("%x\n", sha256.Sum256(data.Bytes()))), 0600)
	return bundle
}

func TestThunderstoreDurableHandoff(t *testing.T) {
	bundle := bootstrapFixture(t, "")
	root := filepath.Join(t.TempDir(), "launcher")
	executable, err := thunderstoreStage(bundle, root, "C:\\Games\\FTK")
	if err != nil {
		t.Fatal(err)
	}
	again, err := thunderstoreStage(bundle, root, "C:\\Games\\FTK")
	if err != nil || again != executable {
		t.Fatalf("repeat setup: %s %v", again, err)
	}
	if err = os.RemoveAll(bundle); err != nil {
		t.Fatal(err)
	}
	if _, err = os.Stat(executable); err != nil {
		t.Fatal(err)
	}
}
func TestThunderstoreRefusesTamperedLauncher(t *testing.T) {
	bundle := bootstrapFixture(t, "")
	root := t.TempDir()
	executable, err := thunderstoreStage(bundle, root, "C:\\Games\\FTK")
	if err != nil {
		t.Fatal(err)
	}
	os.WriteFile(executable, []byte("modified"), 0600)
	if _, err = thunderstoreStage(bundle, root, "C:\\Games\\FTK"); err == nil {
		t.Fatal("modified executable accepted")
	}
	actual, _ := os.ReadFile(executable)
	if string(actual) != "modified" {
		t.Fatal("setup overwrote existing data")
	}
}
func TestThunderstoreRejectsUnsafeArchives(t *testing.T) {
	for _, name := range []string{"outside.exe", "For The King Modded/../outside.exe", "For The King Modded/C:/evil", "For The King Modded/dir\\evil", "For The King Modded/CON.txt", "For The King Modded/foo. /evil", "For The King Modded/FTKMODDEDLAUNCHER.EXE"} {
		t.Run(name, func(t *testing.T) {
			if _, err := thunderstoreStage(bootstrapFixture(t, name), t.TempDir(), "C:\\Games\\FTK"); err == nil {
				t.Fatal("unsafe archive accepted")
			}
		})
	}
}
func TestThunderstoreRejectsChecksumMismatch(t *testing.T) {
	bundle := bootstrapFixture(t, "")
	os.WriteFile(filepath.Join(bundle, "launcher.sha256"), []byte("wrong"), 0600)
	if _, err := thunderstoreStage(bundle, t.TempDir(), "C:\\Games\\FTK"); err == nil {
		t.Fatal("bad checksum accepted")
	}
}
func TestThunderstoreRejectsLinkedOrIncompleteTarget(t *testing.T) {
	for _, linked := range []bool{false, true} {
		bundle := bootstrapFixture(t, "")
		root := t.TempDir()
		executable, err := thunderstoreStage(bundle, root, "C:\\Games\\FTK")
		if err != nil {
			t.Fatal(err)
		}
		os.Remove(executable)
		if linked {
			other := filepath.Join(t.TempDir(), "other.exe")
			os.WriteFile(other, []byte("fixture FtkModdedLauncher.exe"), 0600)
			if err = os.Symlink(other, executable); err != nil {
				t.Skip("symlinks unavailable: " + err.Error())
			}
		}
		if _, err = thunderstoreStage(bundle, root, "C:\\Games\\FTK"); err == nil {
			t.Fatal("incomplete or linked target accepted")
		}
	}
}
