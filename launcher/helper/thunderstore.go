package main

import (
	"archive/zip"
	"bytes"
	"context"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"time"
)

const thunderstoreArchive = "FTKModdedLauncher-windows-x64.zip"

var thunderstoreLaunch = thunderstoreOpenLauncher

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
	entry, err := thunderstoreInstallEntry(*bundle, filepath.Join(local, "FTKModFramework", "Bootstrap"), *game)
	if err != nil {
		return err
	}
	if err = thunderstoreShortcut(entry); err != nil {
		return err
	}
	return thunderstoreRun(entry, *game, *parent)
}

func thunderstoreStandalone() error {
	entry, err := os.Executable()
	if err != nil {
		return err
	}
	raw, err := marketReadBytes(filepath.Join(filepath.Dir(entry), "ftkmf-game-directory.txt"), 32768)
	if err != nil {
		return err
	}
	game := string(raw)
	if !filepath.IsAbs(game) {
		return errors.New("bootstrap game directory is not absolute")
	}
	return thunderstoreRun(entry, game, 0)
}

func thunderstoreRun(entry, game string, parent int) error {
	ctx, cancel := context.WithTimeout(context.Background(), 120*time.Second)
	defer cancel()
	root := filepath.Dir(entry)
	cache := filepath.Join(root, "cache")
	launchers := filepath.Join(root, "launchers")
	launcher, chosen, err := thunderstoreSelectLauncher(ctx, cache, launchers, game, entry)
	if err != nil {
		return err
	}
	if err = thunderstoreLaunch(launcher, game, parent); err != nil {
		// A verified archive can still fail to initialize. Retain and try the last
		// launcher that acknowledged readiness rather than promoting a broken update.
		var prior thunderstoreCache
		if marketRead(filepath.Join(cache, "current.json"), &prior, marketLimit) == nil && prior.SHA256 != chosen.SHA256 {
			archive, readErr := thunderstoreReadCache(cache, prior)
			if readErr == nil {
				old, stageErr := thunderstoreStageBytes(archive, prior.SHA256, launchers, game, entry)
				if stageErr == nil && thunderstoreLaunch(old, game, parent) == nil {
					return nil
				}
			}
		}
		return err
	}
	unlock, err := marketAcquire(filepath.Join(cache, "selection.lock"))
	if err != nil {
		// Readiness already transferred ownership to the launcher. Failure to persist
		// a cache preference must not strand it waiting for a game we refuse to quit.
		fmt.Fprintln(os.Stderr, "Launcher ready; cache selection is busy, retaining its previous value.")
		return nil
	}
	defer unlock()
	var current thunderstoreCache
	if marketRead(filepath.Join(cache, "current.json"), &current, marketLimit) == nil && marketVersion.MatchString(current.Version) && marketCompare(current.Version, chosen.Version) > 0 {
		return nil
	}
	if err = marketWrite(filepath.Join(cache, "current.json"), chosen); err != nil {
		fmt.Fprintln(os.Stderr, "Launcher ready; could not save cache selection:", err)
	}
	return nil
}

func thunderstoreInstallEntry(bundle, root, game string) (string, error) {
	helper, err := marketReadBytes(filepath.Join(bundle, "ftkmf-bootstrap-helper.exe"), updaterAssetLimit)
	if err != nil {
		return "", err
	}
	if !bytes.HasPrefix(helper, []byte("MZ")) {
		return "", errors.New("bootstrap helper is not a Windows executable")
	}
	gameHash := marketHash([]byte(filepath.Clean(game)))
	target := filepath.Join(root, marketHash(helper)+"-"+gameHash[:16])
	if err = os.MkdirAll(target, 0700); err != nil {
		return "", err
	}
	if info, err := os.Lstat(target); err != nil || !info.IsDir() {
		return "", errors.New("bootstrap directory is missing or linked")
	}
	unlock, err := marketAcquire(filepath.Join(target, "setup.lock"))
	if err != nil {
		return "", err
	}
	defer unlock()
	files := map[string][]byte{"FTKModdedBootstrap.exe": helper, "ftkmf-game-directory.txt": []byte(game)}
	for name, expected := range files {
		path := filepath.Join(target, name)
		if info, err := os.Lstat(path); err == nil {
			if !info.Mode().IsRegular() {
				return "", errors.New("bootstrap entry contains a link or special file")
			}
			actual, err := marketReadBytes(path, updaterAssetLimit)
			if err != nil || !bytes.Equal(actual, expected) {
				return "", errors.New("installed bootstrap was modified; move it aside before retrying")
			}
		} else if !os.IsNotExist(err) {
			return "", err
		} else if err = updateAtomicBytes(path, expected, 0600); err != nil {
			return "", err
		}
	}
	return filepath.Join(target, "FTKModdedBootstrap.exe"), nil
}

// The bootstrap understands only the launcher archive contract. Installed
// framework/helper protocols and update preferences belong to the downloaded launcher.
type thunderstoreCache struct {
	SchemaVersion int    `json:"schemaVersion"`
	Version       string `json:"version"`
	SHA256        string `json:"sha256"`
}

func thunderstoreSelectLauncher(ctx context.Context, cache, root, game, entry string) (string, thunderstoreCache, error) {
	if err := os.MkdirAll(cache, 0700); err != nil {
		return "", thunderstoreCache{}, err
	}
	unlock, err := marketAcquire(filepath.Join(cache, "selection.lock"))
	if err != nil {
		return "", thunderstoreCache{}, errors.New("another launcher download is in progress; retry shortly")
	}
	defer unlock()
	var previous thunderstoreCache
	previousErr := marketRead(filepath.Join(cache, "current.json"), &previous, marketLimit)
	candidate, archive, networkErr := thunderstoreDownload(ctx, cache)
	if networkErr == nil && previousErr == nil && marketVersion.MatchString(previous.Version) && marketCompare(previous.Version, candidate.Version) > 0 {
		networkErr = errors.New("latest launcher is older than the cached launcher")
	}
	if networkErr == nil {
		launcher, err := thunderstoreStageBytes(archive, candidate.SHA256, root, game, entry)
		if err == nil {
			return launcher, candidate, nil
		}
		networkErr = err
	}
	if previousErr == nil {
		archive, err := thunderstoreReadCache(cache, previous)
		if err == nil {
			launcher, err := thunderstoreStageBytes(archive, previous.SHA256, root, game, entry)
			if err == nil {
				return launcher, previous, nil
			}
		}
	}
	return "", thunderstoreCache{}, fmt.Errorf("no verified launcher is available; first setup requires internet access: %w", networkErr)
}

func thunderstoreDownload(ctx context.Context, cache string) (thunderstoreCache, []byte, error) {
	var candidate thunderstoreCache
	metadataCtx, cancel := context.WithTimeout(ctx, 10*time.Second)
	defer cancel()
	raw, err := updateFetch(metadataCtx, updaterLatestURL, marketLimit)
	if err != nil {
		return candidate, nil, err
	}
	var release updateRelease
	if err = json.Unmarshal(raw, &release); err != nil {
		return candidate, nil, err
	}
	if release.Draft || release.Prerelease || !updateValidTag(release.TagName) {
		return candidate, nil, errors.New("latest release is not a stable framework release")
	}
	prefix := "https://github.com/jarlbrak/ftk-mod-framework/releases/download/" + release.TagName + "/"
	sums, err := updateFetch(metadataCtx, prefix+"SHA256SUMS", marketLimit)
	if err != nil {
		return candidate, nil, err
	}
	expected := ""
	for _, line := range strings.Split(string(sums), "\n") {
		parts := strings.Fields(line)
		if len(parts) != 2 {
			continue
		}
		name := strings.TrimPrefix(strings.TrimPrefix(parts[1], "*"), "./")
		if name == thunderstoreArchive {
			if expected != "" || !marketSHA.MatchString(parts[0]) {
				return candidate, nil, errors.New("invalid launcher release checksum")
			}
			expected = parts[0]
		}
	}
	if expected == "" {
		return candidate, nil, errors.New("release lacks the Windows launcher checksum")
	}
	candidate = thunderstoreCache{1, strings.TrimPrefix(release.TagName, "v"), expected}
	if archive, err := thunderstoreReadCache(cache, candidate); err == nil {
		return candidate, archive, nil
	}
	archive, err := updateFetch(ctx, prefix+thunderstoreArchive, updaterAssetLimit)
	if err != nil {
		return candidate, nil, err
	}
	if marketHash(archive) != expected {
		return candidate, nil, errors.New("downloaded launcher checksum mismatch")
	}
	if err = thunderstoreValidateBundle(archive, candidate.Version); err != nil {
		return candidate, nil, err
	}
	if err = updateAtomicBytes(filepath.Join(cache, expected+".zip"), archive, 0600); err != nil {
		return candidate, nil, err
	}
	return candidate, archive, nil
}

func thunderstoreReadCache(cache string, record thunderstoreCache) ([]byte, error) {
	if record.SchemaVersion != 1 || !marketVersion.MatchString(record.Version) || !marketSHA.MatchString(record.SHA256) {
		return nil, errors.New("invalid cached launcher identity")
	}
	archive, err := marketReadBytes(filepath.Join(cache, record.SHA256+".zip"), updaterAssetLimit)
	if err != nil {
		return nil, err
	}
	if marketHash(archive) != record.SHA256 {
		return nil, errors.New("cached launcher checksum mismatch")
	}
	if err = thunderstoreValidateBundle(archive, record.Version); err != nil {
		return nil, err
	}
	return archive, nil
}

func thunderstoreValidateBundle(archive []byte, version string) error {
	reader, err := zip.NewReader(bytes.NewReader(archive), int64(len(archive)))
	if err != nil {
		return err
	}
	files, err := thunderstoreFiles(reader)
	if err != nil {
		return err
	}
	var bundle updateBundle
	if err = json.Unmarshal(files["bundle-manifest.json"], &bundle); err != nil {
		return err
	}
	if bundle.SchemaVersion != 1 || bundle.FrameworkVersion != version || bundle.DLLSHA256 != marketHash(files["FTKModFramework.dll"]) || bundle.Helpers["ftkmf-helper-windows-amd64.exe"] != marketHash(files["ftkmf-launcher-helper.exe"]) {
		return errors.New("downloaded launcher bundle has an invalid framework/helper identity")
	}
	for _, name := range []string{"FtkModdedLauncher.exe", "FTKModFramework.dll", "ftkmf-launcher-helper.exe"} {
		if !bytes.HasPrefix(files[name], []byte("MZ")) {
			return errors.New("launcher bundle contains an invalid executable")
		}
	}
	return nil
}

func thunderstoreStageBytes(archive []byte, expected, root, game, entry string) (string, error) {
	digest := sha256.Sum256(archive)
	hash := hex.EncodeToString(digest[:])
	if expected != hash {
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
	gameDigest := sha256.Sum256([]byte(filepath.Clean(game)))
	files["ftkmf-game-directory.txt"] = []byte(game)
	if entry != "" {
		files["ftkmf-bootstrap-entry.txt"] = []byte(entry)
	}
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
