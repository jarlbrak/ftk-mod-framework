package main

import (
	"context"
	"debug/pe"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"time"
)

const updaterLatestURL = "https://api.github.com/repos/jarlbrak/ftk-mod-framework/releases/latest"
const updaterAssetLimit = 100 << 20

var bundledFrameworkVersion string

type updateAsset struct {
	SHA256 string `json:"sha256"`
	Size   int64  `json:"size"`
}
type updateManifest struct {
	SchemaVersion      int                    `json:"schemaVersion"`
	FrameworkVersion   string                 `json:"frameworkVersion"`
	HelperProtocol     int                    `json:"helperProtocol"`
	AutoUpdateFrom     string                 `json:"autoUpdateFrom"`
	GameAssemblySHA256 []string               `json:"gameAssemblySha256"`
	Assets             map[string]updateAsset `json:"assets"`
}
type updateBundle struct {
	SchemaVersion    int               `json:"schemaVersion"`
	FrameworkVersion string            `json:"frameworkVersion"`
	DLLSHA256        string            `json:"dllSha256"`
	Helpers          map[string]string `json:"helpers"`
}
type updateReceipt struct {
	SchemaVersion    int    `json:"schemaVersion"`
	FrameworkVersion string `json:"frameworkVersion"`
	DLLSHA256        string `json:"dllSha256"`
	HelperSHA256     string `json:"helperSha256"`
	HelperProtocol   int    `json:"helperProtocol"`
	Source           string `json:"source"`
}
type updateHelperRecord struct {
	SchemaVersion   int    `json:"schemaVersion"`
	ProtocolVersion int    `json:"protocolVersion"`
	SHA256          string `json:"sha256"`
}
type updateJournalFile struct {
	Path      string `json:"path"`
	OldSHA256 string `json:"oldSha256"`
	NewSHA256 string `json:"newSha256"`
}
type updateJournal struct {
	SchemaVersion int                 `json:"schemaVersion"`
	TransactionID string              `json:"transactionId"`
	Status        string              `json:"status"`
	Files         []updateJournalFile `json:"files"`
}
type updateRelease struct {
	TagName    string `json:"tag_name"`
	Prerelease bool   `json:"prerelease"`
	Draft      bool   `json:"draft"`
}

var updateFetch = updateHTTP
var updateRunning = updateGameRunning
var updateLaunch = updateSteamLaunch
var updateMutationHook func(int) error
var updateInstaller = func(ctx context.Context, game, bundle string) error { return updateInstallBundled(ctx, game, bundle) }

func prepareLaunchMain(args []string) error {
	fs := flag.NewFlagSet("prepare-launch", flag.ContinueOnError)
	game := fs.String("game-dir", "", "installed game directory")
	bundle := fs.String("bundle-dir", "", "trusted bundled manifest directory")
	launch := fs.Bool("launch", false, "dispatch original Steam entry after verification")
	installMissing := fs.Bool("install-if-missing", false, "install the verified bundled framework if missing")
	repairOnly := fs.Bool("repair-only", false, "repair from the verified local bundle without launching")
	if e := fs.Parse(args); e != nil {
		return e
	}
	if *game == "" || !filepath.IsAbs(*game) || fs.NArg() != 0 {
		return errors.New("absolute --game-dir required")
	}
	if *repairOnly && *launch {
		return errors.New("--repair-only cannot be combined with --launch")
	}
	message, e := prepareLaunchMode(*game, *bundle, *launch, *installMissing, *repairOnly)
	if message != "" {
		fmt.Println(message)
	}
	return e
}
func updateHelperAsset(game string) string {
	if updateWindowsGame(game) {
		return "ftkmf-helper-windows-amd64.exe"
	}
	switch runtime.GOOS {
	case "darwin":
		return "ftkmf-helper-macos-universal"
	case "windows":
		return "ftkmf-helper-windows-amd64.exe"
	case "linux":
		if runtime.GOARCH == "arm64" {
			return "ftkmf-helper-linux-arm64"
		}
		return "ftkmf-helper-linux-amd64"
	}
	return ""
}
func updateHelperName(game string) string {
	if updateWindowsGame(game) {
		return "ftkmf-launcher-helper.exe"
	}
	return "ftkmf-launcher-helper"
}
func updatePaths(game string) []string {
	return []string{filepath.Join("BepInEx", "plugins", "FTKModFramework.dll"), filepath.Join("BepInEx", "ftkmf", updateHelperName(game)), filepath.Join("BepInEx", "ftkmf", "helper.json"), filepath.Join("BepInEx", "ftkmf", "installation.json")}
}
func updateRoot(game string) string { return filepath.Join(game, "BepInEx", "ftkmf") }
func updateManagedAssembly(game string) string {
	if runtime.GOOS == "darwin" {
		return filepath.Join(game, "FTK.app", "Contents", "Resources", "Data", "Managed", "Assembly-CSharp.dll")
	}
	return filepath.Join(game, "FTK_Data", "Managed", "Assembly-CSharp.dll")
}
func prepareLaunch(game, bundle string, launch bool) (string, error) {
	return prepareLaunchMode(game, bundle, launch, false, false)
}
func prepareLaunchMode(game, bundle string, launch, installMissing, repairOnly bool) (message string, resultErr error) {
	ctx, cancel := context.WithTimeout(context.Background(), 350*time.Second)
	defer cancel()
	if info, e := os.Stat(game); e != nil || !info.IsDir() {
		return "", errors.New("game directory is unavailable")
	}
	root := updateRoot(game)
	if e := os.MkdirAll(root, 0700); e != nil {
		return "", e
	}
	defer func() {
		line := time.Now().UTC().Format(time.RFC3339) + " " + message
		if resultErr != nil {
			line += " " + resultErr.Error()
		}
		if len(line) > 8192 {
			line = line[:8192]
		}
		_ = updateAtomicBytes(filepath.Join(root, "launcher-update.log"), []byte(line+"\n"), 0600)
	}()
	unlock, e := marketAcquire(filepath.Join(root, "launch-update.lock"))
	if e != nil {
		return "", errors.New("another launcher is updating or starting the game; try again shortly")
	}
	defer unlock()
	running, e := updateRunning(ctx)
	if e != nil {
		return "", e
	}
	if running {
		if repairOnly {
			return "", errors.New("close For The King before running Install / Repair")
		}
		if updateRecoveryNeeded(root) {
			return "", errors.New("an interrupted update requires recovery; close For The King before starting again")
		}
		if launch {
			if e = updateLaunch(ctx); e != nil {
				return "", e
			}
		}
		return "For The King is already running; framework files were left unchanged.", nil
	}
	if e = updateRecover(game); e != nil {
		return "", fmt.Errorf("framework update recovery failed; do not launch until repaired: %w", e)
	}
	_, dllErr := os.Stat(filepath.Join(game, updatePaths(game)[0]))
	if repairOnly || (installMissing && os.IsNotExist(dllErr)) {
		if bundle == "" {
			return "", errors.New("a bundled installation directory is required for Install / Repair")
		}
		installCtx, installCancel := context.WithTimeout(ctx, 300*time.Second)
		e = updateInstaller(installCtx, game, bundle)
		installCancel()
		if e != nil {
			return "", fmt.Errorf("bundled installation did not complete; use Install / Repair before launching: %w", e)
		}
	}
	receipt, known, e := updateVerifyBaseline(game, bundle)
	if e != nil {
		return "", fmt.Errorf("framework installation needs repair: %w", e)
	}
	if repairOnly {
		if !known {
			return "", errors.New("repaired framework does not match the bundled manifest")
		}
		return "Bundled FTK Mod Framework installed and verified.", nil
	}
	message = "Framework verified."
	if !known {
		message = "Existing framework kept unchanged. Automatic upgrades require a matching bundled installation receipt."
	} else {
		networkCtx, networkCancel := context.WithTimeout(ctx, 20*time.Second)
		candidate, files, checkErr := updateCandidate(networkCtx, game, receipt)
		networkCancel()
		if checkErr != nil {
			message = "Update check deferred; keeping the verified installed framework. " + checkErr.Error()
		} else if candidate != nil {
			if e = ctx.Err(); e != nil {
				return "", e
			}
			running, e = updateRunning(ctx)
			if e != nil {
				return "", e
			}
			if running {
				message = "For The King started during the update check; changes deferred."
			} else {
				if e = updateCommit(game, *candidate, files); e != nil {
					if recoveryErr := updateRecover(game); recoveryErr != nil {
						return "", fmt.Errorf("update interrupted and recovery failed: %v; %w", e, recoveryErr)
					}
					if _, _, recheck := updateVerifyBaseline(game, ""); recheck != nil {
						return "", fmt.Errorf("restored framework could not be verified: %w", recheck)
					}
					message = "Update could not finish; restored the verified previous framework. " + e.Error()
				} else {
					message = "Updated FTK Mod Framework to " + candidate.FrameworkVersion + "."
				}
			}
		}
	}
	if launch {
		if e = updateLaunch(ctx); e != nil {
			return message, e
		}
	}
	return message, nil
}
func updatePE(p string) error {
	f, e := pe.Open(p)
	if e != nil {
		return errors.New("framework DLL is not a readable PE assembly")
	}
	defer f.Close()
	var dir pe.DataDirectory
	switch h := f.OptionalHeader.(type) {
	case *pe.OptionalHeader32:
		dir = h.DataDirectory[14]
	case *pe.OptionalHeader64:
		dir = h.DataDirectory[14]
	default:
		return errors.New("framework DLL has no PE optional header")
	}
	if dir.VirtualAddress == 0 || dir.Size < 72 {
		return errors.New("framework DLL has no managed assembly header")
	}
	return nil
}
func updateVerifyBaseline(game, bundle string) (updateReceipt, bool, error) {
	var receipt updateReceipt
	paths := updatePaths(game)
	if e := updateLoader(game); e != nil {
		return receipt, false, e
	}
	if e := updatePE(filepath.Join(game, paths[0])); e != nil {
		return receipt, false, e
	}
	dllHash, e := marketHashFile(filepath.Join(game, paths[0]))
	if e != nil {
		return receipt, false, e
	}
	helperHash, e := marketHashFile(filepath.Join(game, paths[1]))
	if e != nil {
		return receipt, false, e
	}
	var helper updateHelperRecord
	if e = marketRead(filepath.Join(game, paths[2]), &helper, marketLimit); e != nil {
		return receipt, false, e
	}
	if helper.SchemaVersion != 1 || helper.ProtocolVersion != 1 || helper.SHA256 != helperHash {
		return receipt, false, errors.New("installed helper checksum or protocol mismatch")
	}

	if bundle != "" {
		var b updateBundle
		if e = marketRead(filepath.Join(bundle, "bundle-manifest.json"), &b, marketLimit); e == nil && b.SchemaVersion == 1 && marketVersion.MatchString(b.FrameworkVersion) && b.DLLSHA256 == dllHash && b.Helpers[updateHelperAsset(game)] == helperHash {
			receipt = updateReceipt{1, b.FrameworkVersion, dllHash, helperHash, 1, "bundle"}
			if e = marketWrite(filepath.Join(game, paths[3]), receipt); e != nil {
				return receipt, false, e
			}
			return receipt, true, nil
		}
	}
	e = marketRead(filepath.Join(game, paths[3]), &receipt, marketLimit)
	if e == nil {
		if receipt.SchemaVersion != 1 || receipt.HelperProtocol != 1 || !marketVersion.MatchString(receipt.FrameworkVersion) || receipt.DLLSHA256 != dllHash || receipt.HelperSHA256 != helperHash {
			return receipt, false, errors.New("installed framework/helper pair differs from its receipt")
		}
		return receipt, true, nil
	}
	if !os.IsNotExist(e) {
		return receipt, false, e
	}
	return receipt, false, nil
}
func updateHTTP(ctx context.Context, s string, limit int64) ([]byte, error) {
	if s != updaterLatestURL {
		if e := marketURL(s, false); e != nil {
			return nil, e
		}
	}
	req, e := http.NewRequestWithContext(ctx, http.MethodGet, s, nil)
	if e != nil {
		return nil, e
	}
	req.Header.Set("User-Agent", "FTK-Mod-Framework-Launcher")
	req.Header.Set("Accept", "application/vnd.github+json")
	client := http.Client{CheckRedirect: func(req *http.Request, via []*http.Request) error {
		if len(via) > 5 || req.URL.Scheme != "https" || req.URL.User != nil || !contains([]string{"github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com"}, req.URL.Host) {
			return errors.New("release redirect left approved hosts")
		}
		return nil
	}}
	resp, e := client.Do(req)
	if e != nil {
		return nil, e
	}
	defer resp.Body.Close()
	if resp.StatusCode != 200 {
		return nil, fmt.Errorf("official release endpoint returned HTTP %d", resp.StatusCode)
	}
	b, e := io.ReadAll(io.LimitReader(resp.Body, limit+1))
	if int64(len(b)) > limit {
		return nil, errors.New("release response exceeds size limit")
	}
	return b, e
}
func updateCandidate(ctx context.Context, game string, current updateReceipt) (*updateManifest, map[string][]byte, error) {
	metadataCtx, metadataCancel := context.WithTimeout(ctx, 5*time.Second)
	b, e := updateFetch(metadataCtx, updaterLatestURL, marketLimit)
	metadataCancel()
	if e != nil {
		return nil, nil, e
	}
	var release updateRelease
	if e = json.Unmarshal(b, &release); e != nil {
		return nil, nil, e
	}
	version := strings.TrimPrefix(release.TagName, "v")
	if release.Draft || release.Prerelease || !marketVersion.MatchString(version) {
		return nil, nil, errors.New("latest release is not a supported stable version")
	}
	if marketCompare(version, current.FrameworkVersion) <= 0 {
		return nil, nil, nil
	}
	prefix := "https://github.com/jarlbrak/ftk-mod-framework/releases/download/" + url.PathEscape(release.TagName) + "/"
	metadataCtx, metadataCancel = context.WithTimeout(ctx, 5*time.Second)
	b, e = updateFetch(metadataCtx, prefix+"update.json", marketLimit)
	metadataCancel()
	if e != nil {
		return nil, nil, e
	}
	var manifest updateManifest
	if e = marketJSON(b, &manifest); e != nil {
		return nil, nil, e
	}
	if manifest.SchemaVersion != 1 || manifest.HelperProtocol != 1 || manifest.FrameworkVersion != version || !marketRange(manifest.AutoUpdateFrom, current.FrameworkVersion) {
		return nil, nil, errors.New("release does not support automatic upgrade from the installed framework")
	}
	gameHash, e := marketHashFile(updateManagedAssembly(game))
	if e != nil || !contains(manifest.GameAssemblySHA256, gameHash) {
		return nil, nil, errors.New("release has not approved this game build for automatic updates")
	}
	if e = updateManagedCompatibility(game, version); e != nil {
		return nil, nil, e
	}
	files := map[string][]byte{}
	for _, name := range []string{"FTKModFramework.dll", updateHelperAsset(game)} {
		a, ok := manifest.Assets[name]
		if !ok || !marketSHA.MatchString(a.SHA256) || a.Size <= 0 || a.Size > updaterAssetLimit {
			return nil, nil, errors.New("release manifest lacks a valid required asset")
		}
		b, e = updateFetch(ctx, prefix+name, a.Size)
		if e != nil {
			return nil, nil, e
		}
		if int64(len(b)) != a.Size || marketHash(b) != a.SHA256 {
			return nil, nil, errors.New("release asset checksum/size mismatch: " + name)
		}
		files[name] = b
	}
	return &manifest, files, nil
}
func updateManagedCompatibility(game, version string) error {
	root := filepath.Join(updateRoot(game), "marketplace")
	var state marketState
	e := marketRead(filepath.Join(root, "state.json"), &state, marketLimit)
	if os.IsNotExist(e) {
		return nil
	}
	if e != nil {
		return e
	}
	if state.SchemaVersion != 1 {
		return errors.New("unsupported managed state schema")
	}
	for _, id := range []string{state.Current, state.Pending} {
		if id == "" {
			continue
		}
		if !marketHex.MatchString(id) {
			return errors.New("invalid managed generation identity")
		}
		var lock marketLock
		if e = marketRead(filepath.Join(root, "generations", id, "lock.json"), &lock, marketLimit); e != nil {
			return e
		}
		for _, p := range lock.Packages {
			if !marketRange(p.FrameworkRange, version) {
				return fmt.Errorf("%s %s requires framework %s; update deferred", p.Name, p.Version, p.FrameworkRange)
			}
		}
	}
	return nil
}
func updateCommit(game string, manifest updateManifest, downloads map[string][]byte) error {
	paths := updatePaths(game)
	id := marketToken()
	dir := filepath.Join(updateRoot(game), "updates", id)
	if e := os.MkdirAll(dir, 0700); e != nil {
		return e
	}
	helperAsset := manifest.Assets[updateHelperAsset(game)]
	receipt := updateReceipt{1, manifest.FrameworkVersion, manifest.Assets["FTKModFramework.dll"].SHA256, helperAsset.SHA256, 1, "release"}
	helper := updateHelperRecord{1, 1, helperAsset.SHA256}
	helperJSON, _ := json.Marshal(helper)
	receiptJSON, _ := json.Marshal(receipt)
	staged := [][]byte{downloads["FTKModFramework.dll"], downloads[updateHelperAsset(game)], helperJSON, receiptJSON}
	journal := updateJournal{SchemaVersion: 1, TransactionID: id, Status: "prepared"}
	for i, rel := range paths {
		prior, e := marketReadBytes(filepath.Join(game, rel), updaterAssetLimit)
		if e != nil {
			return e
		}
		oldPath := filepath.Join(dir, fmt.Sprintf("old-%d", i))
		newPath := filepath.Join(dir, fmt.Sprintf("new-%d", i))
		if e = updateWriteFile(oldPath, prior, 0600); e != nil {
			return e
		}
		mode := os.FileMode(0600)
		if i == 1 {
			mode = 0755
		}
		if e = updateWriteFile(newPath, staged[i], mode); e != nil {
			return e
		}
		journal.Files = append(journal.Files, updateJournalFile{rel, marketHash(prior), marketHash(staged[i])})
	}
	if e := updatePE(filepath.Join(dir, "new-0")); e != nil {
		return fmt.Errorf("downloaded framework is not a managed assembly: %w", e)
	}
	if e := marketWrite(filepath.Join(updateRoot(game), "update-journal.json"), journal); e != nil {
		return e
	}
	for i, rel := range paths {
		data, e := marketReadBytes(filepath.Join(dir, fmt.Sprintf("new-%d", i)), updaterAssetLimit)
		if e != nil {
			return e
		}
		mode := os.FileMode(0600)
		if i == 1 {
			mode = 0755
		}
		if e = updateAtomicBytes(filepath.Join(game, rel), data, mode); e != nil {
			return e
		}
		if updateMutationHook != nil {
			if e = updateMutationHook(i); e != nil {
				return e
			}
		}
	}
	if _, known, e := updateVerifyBaseline(game, ""); e != nil || !known {
		return errors.New("updated framework pair failed verification")
	}
	journal.Status = "complete"
	return marketWrite(filepath.Join(updateRoot(game), "update-journal.json"), journal)
}
func updateWriteFile(p string, b []byte, mode os.FileMode) error {
	f, e := os.OpenFile(p, os.O_WRONLY|os.O_CREATE|os.O_EXCL, mode)
	if e != nil {
		return e
	}
	if _, e = f.Write(b); e == nil {
		e = f.Sync()
	}
	ce := f.Close()
	if e == nil {
		e = ce
	}
	return e
}
func updateAtomicBytes(p string, b []byte, mode os.FileMode) error {
	temp := filepath.Join(filepath.Dir(p), ".update-"+marketToken())
	if e := updateWriteFile(temp, b, mode); e != nil {
		return e
	}
	return marketReplace(temp, p)
}
func updateRecoveryNeeded(root string) bool {
	var j updateJournal
	e := marketRead(filepath.Join(root, "update-journal.json"), &j, marketLimit)
	return !os.IsNotExist(e) && (e != nil || j.Status != "complete")
}
func updateRecover(game string) error {
	var j updateJournal
	p := filepath.Join(updateRoot(game), "update-journal.json")
	e := marketRead(p, &j, marketLimit)
	if os.IsNotExist(e) {
		return nil
	}
	if e != nil {
		return e
	}
	if j.SchemaVersion != 1 || !marketHex.MatchString(j.TransactionID) || !contains([]string{"prepared", "complete"}, j.Status) {
		return errors.New("invalid update recovery journal")
	}
	if j.Status == "complete" {
		return nil
	}
	paths := updatePaths(game)
	if len(j.Files) != len(paths) {
		return errors.New("incomplete update recovery journal")
	}
	dir := filepath.Join(updateRoot(game), "updates", j.TransactionID)
	backups := make([][]byte, len(paths))
	for i, f := range j.Files {
		if f.Path != paths[i] || !marketSHA.MatchString(f.OldSHA256) || !marketSHA.MatchString(f.NewSHA256) {
			return errors.New("unsafe recovery target")
		}
		b, e := marketReadBytes(filepath.Join(dir, fmt.Sprintf("old-%d", i)), updaterAssetLimit)
		if e != nil {
			return e
		}
		if marketHash(b) != f.OldSHA256 {
			return errors.New("update backup checksum mismatch")
		}
		current, e := marketHashFile(filepath.Join(game, f.Path))
		if e != nil {
			return e
		}
		if current != f.OldSHA256 && current != f.NewSHA256 {
			return errors.New("framework files changed outside the interrupted update; repair requires review")
		}
		backups[i] = b
	}
	for i, rel := range paths {
		mode := os.FileMode(0600)
		if i == 1 {
			mode = 0755
		}
		if e = updateAtomicBytes(filepath.Join(game, rel), backups[i], mode); e != nil {
			return e
		}
	}
	if _, known, e := updateVerifyBaseline(game, ""); e != nil || !known {
		return errors.New("restored framework pair could not be verified")
	}
	j.Status = "complete"
	return marketWrite(p, j)
}
func updateGameRunning(ctx context.Context) (bool, error) {
	var cmd *exec.Cmd
	if runtime.GOOS == "windows" {
		cmd = exec.CommandContext(ctx, "tasklist.exe", "/FO", "CSV", "/NH")
		b, e := cmd.Output()
		if e != nil {
			return false, errors.New("could not check whether For The King is running")
		}
		for _, line := range strings.Split(string(b), "\n") {
			if strings.HasPrefix(strings.ToLower(strings.TrimSpace(line)), `"ftk.exe",`) {
				return true, nil
			}
		}
		return false, nil
	}
	cmd = exec.CommandContext(ctx, "pgrep", "-x", `FTK|FTK\.exe|FTK\.x86_64`)
	e := cmd.Run()
	if e == nil {
		return true, nil
	}
	if code, ok := e.(*exec.ExitError); ok && code.ExitCode() == 1 {
		return false, nil
	}
	return false, errors.New("could not check whether For The King is running")
}
func updateSteamLaunch(ctx context.Context) error {
	var cmd *exec.Cmd
	uri := "steam://rungameid/527230"
	switch runtime.GOOS {
	case "darwin":
		cmd = exec.CommandContext(ctx, "open", uri)
	case "windows":
		cmd = exec.CommandContext(ctx, "rundll32.exe", "url.dll,FileProtocolHandler", uri)
	case "linux":
		if steam, e := exec.LookPath("steam"); e == nil {
			cmd = exec.Command(steam, uri)
			if e = cmd.Start(); e != nil {
				return e
			}
			return cmd.Process.Release()
		}
		if flatpak, e := exec.LookPath("flatpak"); e == nil {
			checkCtx, cancel := context.WithTimeout(ctx, 3*time.Second)
			e = exec.CommandContext(checkCtx, flatpak, "info", "com.valvesoftware.Steam").Run()
			cancel()
			if e == nil {
				cmd = exec.Command(flatpak, "run", "com.valvesoftware.Steam", uri)
				if e = cmd.Start(); e != nil {
					return e
				}
				return cmd.Process.Release()
			}
		}
		cmd = exec.CommandContext(ctx, "xdg-open", uri)
	default:
		return errors.New("Steam launch is unsupported on this platform")
	}
	if e := cmd.Run(); e != nil {
		return fmt.Errorf("could not ask Steam to start For The King: %w", e)
	}
	return nil
}
func updateWindowsGame(game string) bool {
	if runtime.GOOS == "windows" {
		return true
	}
	if runtime.GOOS != "linux" {
		return false
	}
	f, e := os.Open(filepath.Join(game, "FTK.exe"))
	if e != nil {
		return false
	}
	defer f.Close()
	var magic [2]byte
	_, e = io.ReadFull(f, magic[:])
	return e == nil && magic == [2]byte{'M', 'Z'}
}
func updateLoader(game string) error {
	required := []string{filepath.Join("BepInEx", "core", "BepInEx.dll")}
	if updateWindowsGame(game) {
		required = append(required, "winhttp.dll", "doorstop_config.ini")
	} else if runtime.GOOS == "darwin" {
		required = append(required, "run_bepinex.sh", "libdoorstop.dylib")
	} else {
		required = append(required, "run_bepinex.sh", "libdoorstop.so")
	}
	for _, rel := range required {
		info, e := os.Stat(filepath.Join(game, rel))
		if e != nil || !info.Mode().IsRegular() || info.Size() == 0 {
			return fmt.Errorf("required loader file %s is missing; use Install / Repair", rel)
		}
	}
	return nil
}
