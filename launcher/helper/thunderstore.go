package main

import (
	"archive/zip"
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"errors"
	"flag"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"runtime"
	"strings"
)

const thunderstoreArchive = "FTKModdedLauncher-windows-x64.zip"

// The manager owns only the bootstrap. The normal launcher is copied outside its
// profile so profile deletion cannot remove the managed installation's entry point.
func thunderstoreHandoffMain(args []string) error {
	fs := flag.NewFlagSet("thunderstore-handoff", flag.ContinueOnError)
	game := fs.String("game-dir", "", "actual game installation")
	bundle := fs.String("bundle-dir", "", "bootstrap package directory")
	parent := fs.Int("wait-for-process", 0, "game process to wait for before installation")
	if err := fs.Parse(args); err != nil {
		return err
	}
	if runtime.GOOS != "windows" {
		return errors.New("Thunderstore setup currently supports native Windows only; use the normal launcher on other platforms")
	}
	if !filepath.IsAbs(*game) || !filepath.IsAbs(*bundle) || *parent <= 0 || fs.NArg() != 0 {
		return errors.New("absolute game and bundle paths and a positive game process ID are required")
	}
	if info, err := os.Stat(filepath.Join(*game, "FTK.exe")); err != nil || !info.Mode().IsRegular() {
		return errors.New("the selected game directory does not contain FTK.exe")
	}
	local := os.Getenv("LOCALAPPDATA")
	if !filepath.IsAbs(local) {
		return errors.New("Windows local application data directory is unavailable")
	}
	launcher, err := thunderstoreStage(*bundle, filepath.Join(local, "FTKModFramework", "Launcher"), *game)
	if err != nil {
		return err
	}
	return thunderstoreOpenLauncher(launcher, *game, *parent)
}

func thunderstoreStage(bundle, root, game string) (string, error) {
	archive, err := marketReadBytes(filepath.Join(bundle, thunderstoreArchive), updaterAssetLimit)
	if err != nil {
		return "", err
	}
	expected, err := marketReadBytes(filepath.Join(bundle, "launcher.sha256"), 128)
	if err != nil {
		return "", err
	}
	digest := sha256.Sum256(archive)
	hash := hex.EncodeToString(digest[:])
	if strings.TrimSpace(string(expected)) != hash {
		return "", errors.New("bundled launcher archive checksum mismatch")
	}
	reader, err := zip.NewReader(bytes.NewReader(archive), int64(len(archive)))
	if err != nil {
		return "", err
	}
	files, err := thunderstoreFiles(reader)
	if err != nil {
		return "", err
	}
	// Only a new content-addressed directory is populated. Existing bytes must
	// still match the archive before executing; a retry never overwrites them.
	if err = os.MkdirAll(root, 0700); err != nil {
		return "", err
	}
	gameDigest := sha256.Sum256([]byte(strings.ToLower(filepath.Clean(game))))
	files["ftkmf-game-directory.txt"] = []byte(game)
	target := filepath.Join(root, hash+"-"+hex.EncodeToString(gameDigest[:8]))
	if _, err = os.Lstat(target); err == nil {
		if err = thunderstoreVerify(target, files); err != nil {
			return "", err
		}
	} else if !os.IsNotExist(err) {
		return "", err
	} else {
		staging, err := os.MkdirTemp(root, ".bootstrap-")
		if err != nil {
			return "", err
		}
		defer os.RemoveAll(staging)
		for name, data := range files {
			path := filepath.Join(staging, filepath.FromSlash(name))
			if err = os.MkdirAll(filepath.Dir(path), 0700); err != nil {
				return "", err
			}
			if err = os.WriteFile(path, data, 0600); err != nil {
				return "", err
			}
		}
		if err = os.Rename(staging, target); err != nil {
			// A simultaneous setup may have published the identical archive first.
			if check := thunderstoreVerify(target, files); check != nil {
				return "", err
			}
		}
	}
	return filepath.Join(target, "FtkModdedLauncher.exe"), nil
}

func thunderstoreFiles(reader *zip.Reader) (map[string][]byte, error) {
	if len(reader.File) > 2000 {
		return nil, errors.New("launcher archive has too many entries")
	}
	files := map[string][]byte{}
	seen := map[string]bool{}
	var total uint64
	for _, file := range reader.File {
		const prefix = "For The King Modded/"
		if !strings.HasPrefix(file.Name, prefix) {
			return nil, errors.New("unexpected launcher archive root")
		}
		name := strings.TrimPrefix(file.Name, prefix)
		if name == "" && file.FileInfo().IsDir() {
			continue
		}
		clean := strings.TrimSuffix(name, "/")
		if clean == "" || strings.ContainsAny(clean, "\\:\x00") {
			return nil, errors.New("unsafe launcher archive path")
		}
		for _, part := range strings.Split(clean, "/") {
			if part == "" || part == "." || part == ".." || strings.TrimRight(part, ". ") != part {
				return nil, errors.New("unsafe launcher archive path")
			}
			stem := strings.ToUpper(strings.SplitN(part, ".", 2)[0])
			if stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || (len(stem) == 4 && (strings.HasPrefix(stem, "COM") || strings.HasPrefix(stem, "LPT")) && stem[3] >= '0' && stem[3] <= '9') {
				return nil, errors.New("reserved launcher archive path")
			}
		}
		key := strings.ToLower(clean)
		if seen[key] {
			return nil, errors.New("duplicate launcher archive path")
		}
		seen[key] = true
		if file.Mode()&os.ModeSymlink != 0 {
			return nil, errors.New("launcher archive contains a link")
		}
		if file.FileInfo().IsDir() {
			continue
		}
		if !file.Mode().IsRegular() {
			return nil, errors.New("launcher archive contains a special file")
		}
		total += file.UncompressedSize64
		if file.UncompressedSize64 > updaterAssetLimit || total > 3*updaterAssetLimit {
			return nil, errors.New("launcher archive exceeds extraction limit")
		}
		input, err := file.Open()
		if err != nil {
			return nil, err
		}
		data, readErr := io.ReadAll(io.LimitReader(input, updaterAssetLimit+1))
		input.Close()
		if readErr != nil {
			return nil, readErr
		}
		if len(data) > updaterAssetLimit {
			return nil, errors.New("launcher archive entry exceeds extraction limit")
		}
		files[clean] = data
	}
	for _, required := range []string{"FtkModdedLauncher.exe", "FtkModdedLauncher.exe.config", "FTKModFramework.dll", "ftkmf-launcher-helper.exe", "bundle-manifest.json", "install.ps1"} {
		if len(files[required]) == 0 {
			return nil, fmt.Errorf("launcher archive is missing %s", required)
		}
	}
	return files, nil
}

func thunderstoreVerify(root string, files map[string][]byte) error {
	found := 0
	err := filepath.Walk(root, func(path string, info os.FileInfo, err error) error {
		if err != nil {
			return err
		}
		if info.Mode()&os.ModeSymlink != 0 {
			return errors.New("installed launcher contains a link; move it aside and retry setup")
		}
		if info.IsDir() {
			return nil
		}
		relative, err := filepath.Rel(root, path)
		if err != nil {
			return err
		}
		expected, ok := files[filepath.ToSlash(relative)]
		if !ok || !info.Mode().IsRegular() {
			return errors.New("installed launcher contains unexpected files; move it aside and retry setup")
		}
		actual, err := marketReadBytes(path, updaterAssetLimit)
		if err != nil {
			return err
		}
		if !bytes.Equal(actual, expected) {
			return errors.New("installed launcher has changed; move it aside and retry setup")
		}
		found++
		return nil
	})
	if err != nil {
		return err
	}
	if found != len(files) {
		return errors.New("installed launcher is incomplete; move it aside and retry setup")
	}
	return nil
}
