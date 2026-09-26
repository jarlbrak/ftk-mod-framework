package main

import (
	"archive/zip"
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func thinArchive(t *testing.T, version string, marker byte) []byte {
	t.Helper()
	dll, helper := updateTestPE(marker), updateTestPE(marker+1)
	manifest, _ := json.Marshal(updateBundle{1, version, marketHash(dll), map[string]string{"ftkmf-helper-windows-amd64.exe": marketHash(helper)}})
	files := map[string][]byte{"FtkModdedLauncher.exe": updateTestPE(marker + 2), "FtkModdedLauncher.exe.config": []byte("config"), "FTKModFramework.dll": dll, "ftkmf-launcher-helper.exe": helper, "install.ps1": []byte("installer"), "bundle-manifest.json": manifest}
	var out bytes.Buffer
	writer := zip.NewWriter(&out)
	for name, data := range files {
		entry, err := writer.Create("For The King Modded/" + name)
		if err != nil {
			t.Fatal(err)
		}
		entry.Write(data)
	}
	if err := writer.Close(); err != nil {
		t.Fatal(err)
	}
	return out.Bytes()
}

func thinFeed(tag string, archive []byte) func(context.Context, string, int64) ([]byte, error) {
	return func(ctx context.Context, url string, limit int64) ([]byte, error) {
		if url == updaterLatestURL {
			return json.Marshal(map[string]interface{}{"tag_name": tag, "draft": false, "prerelease": false})
		}
		if strings.HasSuffix(url, "SHA256SUMS") {
			return []byte(fmt.Sprintf("%s  ./%s\n", marketHash(archive), thunderstoreArchive)), nil
		}
		if strings.HasSuffix(url, thunderstoreArchive) {
			return archive, nil
		}
		return nil, fmt.Errorf("unexpected URL %s", url)
	}
}

func thinHooks(t *testing.T) {
	t.Helper()
	oldFetch, oldLaunch := updateFetch, thunderstoreLaunch
	t.Cleanup(func() { updateFetch = oldFetch; thunderstoreLaunch = oldLaunch })
}

func TestThinBootstrapFirstRunAndOfflineReuse(t *testing.T) {
	thinHooks(t)
	root := t.TempDir()
	entry := filepath.Join(root, "FTKModdedBootstrap.exe")
	os.WriteFile(entry, []byte("MZthin"), 0600)
	archive := thinArchive(t, "1.5.1", 1)
	updateFetch = thinFeed("v1.5.1", archive)
	calls := 0
	thunderstoreLaunch = func(launcher, game string, parent int) error {
		calls++
		remembered, err := os.ReadFile(filepath.Join(filepath.Dir(launcher), "ftkmf-bootstrap-entry.txt"))
		if err != nil || string(remembered) != entry {
			t.Fatalf("lost thin entry: %s %v", remembered, err)
		}
		if game != "C:\\Games\\FTK" {
			t.Fatal("changed game target")
		}
		return nil
	}
	if err := thunderstoreRun(entry, "C:\\Games\\FTK", 0); err != nil {
		t.Fatal(err)
	}
	updateFetch = func(context.Context, string, int64) ([]byte, error) { return nil, errors.New("offline") }
	if err := thunderstoreRun(entry, "C:\\Games\\FTK", 0); err != nil {
		t.Fatal(err)
	}
	if calls != 2 {
		t.Fatal("cached launcher was not used offline")
	}
	os.WriteFile(filepath.Join(root, "cache", marketHash(archive)+".zip"), []byte("tampered"), 0600)
	if err := thunderstoreRun(entry, "C:\\Games\\FTK", 0); err == nil {
		t.Fatal("corrupted offline cache accepted")
	}
	if calls != 2 {
		t.Fatal("launched unverified cache")
	}
}

func TestThinBootstrapKeepsLastReadyLauncher(t *testing.T) {
	thinHooks(t)
	root := t.TempDir()
	entry := filepath.Join(root, "FTKModdedBootstrap.exe")
	old := thinArchive(t, "1.5.1", 2)
	updateFetch = thinFeed("v1.5.1", old)
	thunderstoreLaunch = func(string, string, int) error { return nil }
	if err := thunderstoreRun(entry, "C:\\Game", 0); err != nil {
		t.Fatal(err)
	}
	next := thinArchive(t, "1.6.0", 4)
	updateFetch = thinFeed("v1.6.0", next)
	attempts := 0
	thunderstoreLaunch = func(launcher, game string, parent int) error {
		attempts++
		if strings.Contains(launcher, marketHash(next)) {
			return errors.New("launcher failed readiness")
		}
		return nil
	}
	if err := thunderstoreRun(entry, "C:\\Game", 0); err != nil {
		t.Fatal(err)
	}
	var record thunderstoreCache
	if err := marketRead(filepath.Join(root, "cache", "current.json"), &record, marketLimit); err != nil {
		t.Fatal(err)
	}
	if record.Version != "1.5.1" || attempts != 2 {
		t.Fatal("failed update replaced ready launcher")
	}
}

func TestThinBootstrapRejectsOtherReleaseChannels(t *testing.T) {
	thinHooks(t)
	archive := thinArchive(t, "1.5.1", 1)
	for _, tag := range []string{"bootstrap-v1.0.0", "paladin-v1.4.0", "v1.5.1-beta"} {
		updateFetch = thinFeed(tag, archive)
		if _, _, err := thunderstoreDownload(context.Background(), t.TempDir()); err == nil {
			t.Fatalf("accepted %s", tag)
		}
	}
}

func TestThinBootstrapNoFirstRunOfflineFallback(t *testing.T) {
	thinHooks(t)
	updateFetch = func(context.Context, string, int64) ([]byte, error) { return nil, errors.New("offline") }
	thunderstoreLaunch = func(string, string, int) error { t.Fatal("started a missing launcher"); return nil }
	if err := thunderstoreRun(filepath.Join(t.TempDir(), "FTKModdedBootstrap.exe"), "C:\\Game", 0); err == nil {
		t.Fatal("fresh offline setup succeeded")
	}
}

func TestThinBootstrapChecksumMismatchPreservesCache(t *testing.T) {
	thinHooks(t)
	root := t.TempDir()
	entry := filepath.Join(root, "FTKModdedBootstrap.exe")
	old := thinArchive(t, "1.5.1", 1)
	updateFetch = thinFeed("v1.5.1", old)
	thunderstoreLaunch = func(string, string, int) error { return nil }
	if err := thunderstoreRun(entry, "C:\\Game", 0); err != nil {
		t.Fatal(err)
	}
	next := thinArchive(t, "1.6.0", 4)
	feed := thinFeed("v1.6.0", next)
	updateFetch = func(ctx context.Context, url string, limit int64) ([]byte, error) {
		if strings.HasSuffix(url, thunderstoreArchive) {
			return []byte("corrupted transfer"), nil
		}
		return feed(ctx, url, limit)
	}
	if err := thunderstoreRun(entry, "C:\\Game", 0); err != nil {
		t.Fatal(err)
	}
	var record thunderstoreCache
	marketRead(filepath.Join(root, "cache", "current.json"), &record, marketLimit)
	if record.Version != "1.5.1" {
		t.Fatal("bad download replaced fallback")
	}
}

func TestThinBootstrapSerializesDownloads(t *testing.T) {
	thinHooks(t)
	root := t.TempDir()
	cache := filepath.Join(root, "cache")
	entered, release := make(chan struct{}), make(chan struct{})
	feed := thinFeed("v1.5.1", thinArchive(t, "1.5.1", 1))
	updateFetch = func(ctx context.Context, url string, limit int64) ([]byte, error) {
		if url == updaterLatestURL {
			close(entered)
			<-release
		}
		return feed(ctx, url, limit)
	}
	done := make(chan error, 1)
	go func() {
		_, _, err := thunderstoreSelectLauncher(context.Background(), cache, filepath.Join(root, "launchers"), "C:\\Game", "entry.exe")
		done <- err
	}()
	<-entered
	_, _, second := thunderstoreSelectLauncher(context.Background(), cache, filepath.Join(root, "launchers"), "C:\\Game", "entry.exe")
	close(release)
	if err := <-done; err != nil {
		t.Fatal(err)
	}
	if second == nil {
		t.Fatal("concurrent download acquired same lock")
	}
}

func TestThinBootstrapCacheContentionAfterReadinessDoesNotStrandGame(t *testing.T) {
	thinHooks(t)
	root := t.TempDir()
	entry := filepath.Join(root, "FTKModdedBootstrap.exe")
	old := thinArchive(t, "1.5.1", 1)
	updateFetch = thinFeed("v1.5.1", old)
	thunderstoreLaunch = func(string, string, int) error { return nil }
	if err := thunderstoreRun(entry, "C:\\Game", 123); err != nil {
		t.Fatal(err)
	}
	updateFetch = thinFeed("v1.6.0", thinArchive(t, "1.6.0", 3))
	var release func()
	thunderstoreLaunch = func(string, string, int) error {
		var err error
		release, err = marketAcquire(filepath.Join(root, "cache", "selection.lock"))
		return err
	}
	err := thunderstoreRun(entry, "C:\\Game", 123)
	if release != nil {
		release()
	}
	if err != nil {
		t.Fatalf("ready launcher handoff reported failure: %v", err)
	}
	var record thunderstoreCache
	marketRead(filepath.Join(root, "cache", "current.json"), &record, marketLimit)
	if record.Version != "1.5.1" {
		t.Fatal("busy cache pointer was overwritten")
	}
}

func TestThinBootstrapRejectsBundleVersionMismatch(t *testing.T) {
	thinHooks(t)
	updateFetch = thinFeed("v1.6.0", thinArchive(t, "1.5.1", 1))
	if _, _, err := thunderstoreDownload(context.Background(), t.TempDir()); err == nil {
		t.Fatal("mismatched launcher release accepted")
	}
}

func TestThinBootstrapEntrySurvivesProfileRemoval(t *testing.T) {
	bundle := t.TempDir()
	os.WriteFile(filepath.Join(bundle, "ftkmf-bootstrap-helper.exe"), []byte("MZthin fixture"), 0600)
	entry, err := thunderstoreInstallEntry(bundle, t.TempDir(), "C:\\Game")
	if err != nil {
		t.Fatal(err)
	}
	if err = os.RemoveAll(bundle); err != nil {
		t.Fatal(err)
	}
	if _, err = os.Stat(entry); err != nil {
		t.Fatal(err)
	}
	target, err := os.ReadFile(filepath.Join(filepath.Dir(entry), "ftkmf-game-directory.txt"))
	if err != nil || string(target) != "C:\\Game" {
		t.Fatal("lost installed game association")
	}
}

func TestThinBootstrapCacheWriteFailureAfterReadinessDoesNotStrandGame(t *testing.T) {
	thinHooks(t)
	root := t.TempDir()
	entry := filepath.Join(root, "FTKModdedBootstrap.exe")
	updateFetch = thinFeed("v1.5.1", thinArchive(t, "1.5.1", 1))
	pointer := filepath.Join(root, "cache", "current.json")
	thunderstoreLaunch = func(string, string, int) error { return os.Mkdir(pointer, 0700) }
	if err := thunderstoreRun(entry, "C:\\Game", 123); err != nil {
		t.Fatalf("ready launcher was reported as failed: %v", err)
	}
	if info, err := os.Stat(pointer); err != nil || !info.IsDir() {
		t.Fatal("cache failure did not preserve existing entry")
	}
}
