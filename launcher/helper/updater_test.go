package main

import (
	"context"
	"encoding/binary"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"testing"
)

func updateTestPE(marker byte) []byte {
	b := make([]byte, 512)
	copy(b, []byte("MZ"))
	binary.LittleEndian.PutUint32(b[0x3c:], 0x80)
	copy(b[0x80:], []byte{'P', 'E', 0, 0})
	binary.LittleEndian.PutUint16(b[0x84:], 0x14c)
	binary.LittleEndian.PutUint16(b[0x94:], 224)
	binary.LittleEndian.PutUint16(b[0x96:], 0x2102)
	binary.LittleEndian.PutUint16(b[0x98:], 0x10b)
	binary.LittleEndian.PutUint32(b[0x98+92:], 16)
	binary.LittleEndian.PutUint32(b[0x98+96+14*8:], 0x2000)
	binary.LittleEndian.PutUint32(b[0x98+96+14*8+4:], 72)
	b[len(b)-1] = marker
	return b
}
func updateFixture(t *testing.T) (string, string, updateManifest, map[string][]byte, *int) {
	t.Helper()
	game := t.TempDir()
	bundle := t.TempDir()
	if runtime.GOOS == "linux" {
		os.WriteFile(filepath.Join(game, "FTK.exe"), []byte{127, 69, 76, 70}, 0600)
	}
	for _, rel := range []string{filepath.Join("BepInEx", "core", "BepInEx.dll"), "run_bepinex.sh", "libdoorstop.dylib", "libdoorstop.so", "winhttp.dll", "doorstop_config.ini"} {
		os.MkdirAll(filepath.Dir(filepath.Join(game, rel)), 0700)
		os.WriteFile(filepath.Join(game, rel), []byte("loader fixture"), 0600)
	}
	paths := updatePaths(game)
	oldDLL := updateTestPE(1)
	oldHelper := []byte("known original helper fixture")
	for _, p := range paths {
		os.MkdirAll(filepath.Dir(filepath.Join(game, p)), 0700)
	}
	os.WriteFile(filepath.Join(game, paths[0]), oldDLL, 0600)
	os.WriteFile(filepath.Join(game, paths[1]), oldHelper, 0755)
	marketWrite(filepath.Join(game, paths[2]), updateHelperRecord{1, 1, marketHash(oldHelper)})
	marketWrite(filepath.Join(bundle, "bundle-manifest.json"), updateBundle{1, "0.1.0", marketHash(oldDLL), map[string]string{updateHelperAsset(game): marketHash(oldHelper)}})
	gameAssembly := updateManagedAssembly(game)
	os.MkdirAll(filepath.Dir(gameAssembly), 0700)
	os.WriteFile(gameAssembly, []byte("verified game assembly fixture"), 0600)
	gameSHA, _ := marketHashFile(gameAssembly)
	newDLL := updateTestPE(2)
	newHelper := []byte("verified new helper fixture")
	assets := map[string][]byte{"FTKModFramework.dll": newDLL, updateHelperAsset(game): newHelper}
	m := updateManifest{SchemaVersion: 1, FrameworkVersion: "0.1.1", HelperProtocol: 1, AutoUpdateFrom: "0.1.0", GameAssemblySHA256: []string{gameSHA}, Assets: map[string]updateAsset{}}
	for name, b := range assets {
		m.Assets[name] = updateAsset{marketHash(b), int64(len(b))}
	}
	oldFetch, oldRunning, oldLaunch, oldHook := updateFetch, updateRunning, updateLaunch, updateMutationHook
	t.Cleanup(func() {
		updateFetch = oldFetch
		updateRunning = oldRunning
		updateLaunch = oldLaunch
		updateMutationHook = oldHook
	})
	calls := new(int)
	updateRunning = func(context.Context) (bool, error) { return false, nil }
	updateLaunch = func(context.Context) error { *calls++; return nil }
	updateFetch = updateFixtureFetch(m, assets)
	return game, bundle, m, assets, calls
}
func updateFixtureFetch(m updateManifest, assets map[string][]byte) func(context.Context, string, int64) ([]byte, error) {
	return func(ctx context.Context, s string, limit int64) ([]byte, error) {
		if e := ctx.Err(); e != nil {
			return nil, e
		}
		if s == updaterLatestURL {
			return []byte(`{"tag_name":"v` + m.FrameworkVersion + `","prerelease":false,"draft":false}`), nil
		}
		if strings.HasSuffix(s, "/update.json") {
			return json.Marshal(m)
		}
		for name, b := range assets {
			if strings.HasSuffix(s, "/"+name) {
				return b, nil
			}
		}
		return nil, errors.New("unexpected URL")
	}
}
func TestUpdaterSuccessfulVerifiedPair(t *testing.T) {
	game, bundle, _, assets, calls := updateFixture(t)
	msg, e := prepareLaunch(game, bundle, true)
	if e != nil || !strings.Contains(msg, "Updated") || *calls != 1 {
		t.Fatal(msg, e, *calls)
	}
	paths := updatePaths(game)
	dll, _ := os.ReadFile(filepath.Join(game, paths[0]))
	helper, _ := os.ReadFile(filepath.Join(game, paths[1]))
	if marketHash(dll) != marketHash(assets["FTKModFramework.dll"]) || marketHash(helper) != marketHash(assets[updateHelperAsset(game)]) {
		t.Fatal("update did not install matching pair")
	}
	receipt, known, e := updateVerifyBaseline(game, "")
	if e != nil || !known || receipt.FrameworkVersion != "0.1.1" {
		t.Fatal(receipt, known, e)
	}
	log, _ := os.ReadFile(filepath.Join(updateRoot(game), "launcher-update.log"))
	if !strings.Contains(string(log), "Updated") {
		t.Fatal("launcher status not persisted")
	}
}
func TestUpdaterNetworkFailureKeepsBaseline(t *testing.T) {
	game, bundle, _, _, calls := updateFixture(t)
	before, _ := marketHashFile(filepath.Join(game, updatePaths(game)[0]))
	updateFetch = func(context.Context, string, int64) ([]byte, error) { return nil, errors.New("offline") }
	msg, e := prepareLaunch(game, bundle, true)
	after, _ := marketHashFile(filepath.Join(game, updatePaths(game)[0]))
	if e != nil || !strings.Contains(msg, "deferred") || before != after || *calls != 1 {
		t.Fatal(msg, e, *calls)
	}
}
func TestUpdaterUnenrolledLocalPairIsPreserved(t *testing.T) {
	game, bundle, _, _, calls := updateFixture(t)
	os.WriteFile(filepath.Join(game, updatePaths(game)[0]), updateTestPE(99), 0600)
	fetches := 0
	updateFetch = func(context.Context, string, int64) ([]byte, error) {
		fetches++
		return nil, errors.New("should not fetch")
	}
	msg, e := prepareLaunch(game, bundle, true)
	if e != nil || fetches != 0 || *calls != 1 || !strings.Contains(msg, "unchanged") {
		t.Fatal(msg, e, fetches, *calls)
	}
}
func TestUpdaterDamagedReceiptForbidsLaunch(t *testing.T) {
	game, bundle, _, _, calls := updateFixture(t)
	_, known, e := updateVerifyBaseline(game, bundle)
	if e != nil || !known {
		t.Fatal(e)
	}
	os.WriteFile(filepath.Join(game, updatePaths(game)[0]), updateTestPE(99), 0600)
	if _, e = prepareLaunch(game, bundle, true); e == nil || *calls != 0 {
		t.Fatal("damaged pair launched", e, *calls)
	}
}
func TestUpdaterFailureAtEveryMutationRestoresPair(t *testing.T) {
	for step := 0; step < 4; step++ {
		t.Run(string(rune('0'+step)), func(t *testing.T) {
			game, bundle, _, _, calls := updateFixture(t)
			updateVerifyBaseline(game, bundle)
			before := map[string]string{}
			for _, p := range updatePaths(game) {
				before[p], _ = marketHashFile(filepath.Join(game, p))
			}
			updateMutationHook = func(i int) error {
				if i == step {
					return errors.New("simulated interrupted replacement")
				}
				return nil
			}
			msg, e := prepareLaunch(game, bundle, true)
			if e != nil || !strings.Contains(msg, "restored") || *calls != 1 {
				t.Fatal(msg, e, *calls)
			}
			for _, p := range updatePaths(game) {
				after, _ := marketHashFile(filepath.Join(game, p))
				if before[p] != after {
					t.Fatal("partial pair retained", p)
				}
			}
		})
	}
}
func TestUpdaterCorruptRecoveryForbidsLaunch(t *testing.T) {
	game, bundle, _, _, calls := updateFixture(t)
	updateMutationHook = func(i int) error {
		if i == 0 {
			var j updateJournal
			marketRead(filepath.Join(updateRoot(game), "update-journal.json"), &j, marketLimit)
			os.WriteFile(filepath.Join(updateRoot(game), "updates", j.TransactionID, "old-1"), []byte("corrupt backup"), 0600)
			return errors.New("interrupted")
		}
		return nil
	}
	if _, e := prepareLaunch(game, bundle, true); e == nil || *calls != 0 {
		t.Fatal("unrecoverable update launched", e, *calls)
	}
}
func TestUpdaterManagedCompatibilityAndCandidateGuards(t *testing.T) {
	for _, mode := range []string{"game", "protocol", "from", "hash", "current", "pending", "downgrade", "prerelease"} {
		t.Run(mode, func(t *testing.T) {
			game, bundle, m, assets, calls := updateFixture(t)
			old, _ := marketHashFile(filepath.Join(game, updatePaths(game)[0]))
			switch mode {
			case "game":
				m.GameAssemblySHA256 = []string{strings.Repeat("a", 64)}
			case "protocol":
				m.HelperProtocol = 2
			case "from":
				m.AutoUpdateFrom = "0.0.1"
			case "hash":
				a := m.Assets["FTKModFramework.dll"]
				a.SHA256 = strings.Repeat("a", 64)
				m.Assets["FTKModFramework.dll"] = a
			case "current", "pending":
				id := strings.Repeat("a", 32)
				root := filepath.Join(updateRoot(game), "marketplace")
				s := marketState{SchemaVersion: 1}
				if mode == "current" {
					s.Current = id
				} else {
					s.Pending = id
				}
				marketWrite(filepath.Join(root, "state.json"), s)
				marketWrite(filepath.Join(root, "generations", id, "lock.json"), marketLock{SchemaVersion: 1, Packages: []marketPackage{{Name: "Pinned mod", Version: "1.0.0", FrameworkRange: "0.1.0"}}})
			case "downgrade":
				m.FrameworkVersion = "0.0.9"
			}
			updateFetch = updateFixtureFetch(m, assets)
			if mode == "prerelease" {
				updateFetch = func(context.Context, string, int64) ([]byte, error) {
					return []byte(`{"tag_name":"v0.1.1","prerelease":true}`), nil
				}
			}
			msg, e := prepareLaunch(game, bundle, true)
			after, _ := marketHashFile(filepath.Join(game, updatePaths(game)[0]))
			if e != nil || after != old || *calls != 1 {
				t.Fatal("unsafe candidate changed baseline", mode, msg, e)
			}
		})
	}
}
func TestUpdaterGameStartsBeforeCommit(t *testing.T) {
	game, bundle, _, _, calls := updateFixture(t)
	checks := 0
	updateRunning = func(context.Context) (bool, error) { checks++; return checks > 1, nil }
	before, _ := marketHashFile(filepath.Join(game, updatePaths(game)[0]))
	msg, e := prepareLaunch(game, bundle, true)
	after, _ := marketHashFile(filepath.Join(game, updatePaths(game)[0]))
	if e != nil || before != after || *calls != 1 || !strings.Contains(msg, "started") {
		t.Fatal(msg, e, *calls)
	}
}
func TestUpdaterLockCoversDispatch(t *testing.T) {
	game, bundle, _, _, _ := updateFixture(t)
	updateLaunch = func(context.Context) error {
		unlock, e := marketAcquire(filepath.Join(updateRoot(game), "launch-update.lock"))
		if e == nil {
			unlock()
			t.Fatal("lock released before Steam dispatch")
		}
		return nil
	}
	if _, e := prepareLaunch(game, bundle, true); e != nil {
		t.Fatal(e)
	}
}
func TestUpdaterJournalRecoversOnNextInvocation(t *testing.T) {
	game, bundle, m, assets, _ := updateFixture(t)
	updateVerifyBaseline(game, bundle)
	old, _ := marketHashFile(filepath.Join(game, updatePaths(game)[0]))
	updateMutationHook = func(i int) error { return errors.New("simulated process termination") }
	if e := updateCommit(game, m, assets); e == nil {
		t.Fatal("interruption not injected")
	}
	updateMutationHook = nil
	updateFetch = func(context.Context, string, int64) ([]byte, error) { return nil, errors.New("offline") }
	if _, e := prepareLaunch(game, bundle, false); e != nil {
		t.Fatal(e)
	}
	after, _ := marketHashFile(filepath.Join(game, updatePaths(game)[0]))
	if old != after {
		t.Fatal("next invocation failed to recover previous DLL")
	}
}
func TestUpdaterMissingLoaderForbidsDispatch(t *testing.T) {
	game, bundle, _, _, calls := updateFixture(t)
	os.Rename(filepath.Join(game, "BepInEx", "core", "BepInEx.dll"), filepath.Join(game, "BepInEx", "core", "BepInEx.dll.backup"))
	if _, e := prepareLaunch(game, bundle, true); e == nil || *calls != 0 {
		t.Fatal("missing loader was allowed to launch", e, *calls)
	}
}
func TestUpdaterExactBundleRepairRefreshesReceipt(t *testing.T) {
	game, bundle, _, _, _ := updateFixture(t)
	if _, e := prepareLaunch(game, bundle, false); e != nil {
		t.Fatal(e)
	}
	paths := updatePaths(game)
	os.WriteFile(filepath.Join(game, paths[0]), updateTestPE(1), 0600)
	oldHelper := []byte("known original helper fixture")
	os.WriteFile(filepath.Join(game, paths[1]), oldHelper, 0755)
	marketWrite(filepath.Join(game, paths[2]), updateHelperRecord{1, 1, marketHash(oldHelper)})
	receipt, known, e := updateVerifyBaseline(game, bundle)
	if e != nil || !known || receipt.FrameworkVersion != "0.1.0" {
		t.Fatal("exact bundle repair did not refresh stale receipt", receipt, e)
	}
}
func TestUpdaterRecoveryPreservesUnexpectedExternalChanges(t *testing.T) {
	game, bundle, m, assets, _ := updateFixture(t)
	updateVerifyBaseline(game, bundle)
	updateMutationHook = func(int) error { return errors.New("interrupted") }
	if updateCommit(game, m, assets) == nil {
		t.Fatal("expected interruption")
	}
	updateMutationHook = nil
	path := filepath.Join(game, updatePaths(game)[0])
	external := updateTestPE(77)
	os.WriteFile(path, external, 0600)
	if e := updateRecover(game); e == nil {
		t.Fatal("recovery overwrote unexpected external edit")
	}
	after, _ := os.ReadFile(path)
	if marketHash(after) != marketHash(external) {
		t.Fatal("external edit lost")
	}
}
func TestUpdaterBootstrapAndRepairShareLock(t *testing.T) {
	game, bundle, _, _, calls := updateFixture(t)
	oldInstaller := updateInstaller
	defer func() { updateInstaller = oldInstaller }()
	installed := 0
	updateInstaller = func(ctx context.Context, g, b string) error {
		installed++
		unlock, e := marketAcquire(filepath.Join(updateRoot(game), "launch-update.lock"))
		if e == nil {
			unlock()
			t.Fatal("bundled installation ran outside shared lock")
		}
		return nil
	}
	message, e := prepareLaunchMode(game, bundle, false, false, true)
	if e != nil || installed != 1 || *calls != 0 || !strings.Contains(message, "installed and verified") {
		t.Fatal(message, e, installed, *calls)
	}
	os.Rename(filepath.Join(game, updatePaths(game)[0]), filepath.Join(game, "old-framework-fixture"))
	updateInstaller = func(context.Context, string, string) error { installed++; return errors.New("installer failure") }
	if _, e = prepareLaunchMode(game, bundle, true, true, false); e == nil || *calls != 0 {
		t.Fatal("failed bootstrap dispatched Steam", e, *calls)
	}
}
func TestUpdaterProtonSelectsWindowsGameHelper(t *testing.T) {
	if runtime.GOOS != "linux" {
		t.Skip("Linux game build selection")
	}
	game := t.TempDir()
	os.WriteFile(filepath.Join(game, "FTK.exe"), []byte{'M', 'Z', 0, 0}, 0600)
	if updateHelperAsset(game) != "ftkmf-helper-windows-amd64.exe" || updateHelperName(game) != "ftkmf-launcher-helper.exe" {
		t.Fatal("Proton selected host-native helper")
	}
	os.WriteFile(filepath.Join(game, "FTK.exe"), []byte{127, 69, 76, 70}, 0600)
	if updateWindowsGame(game) || strings.HasSuffix(updateHelperName(game), ".exe") {
		t.Fatal("native ELF FTK.exe mistaken for Windows depot")
	}
}
