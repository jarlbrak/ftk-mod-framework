package main

import (
	"context"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"time"
)

// Caller owns launch-update.lock and has recovered transactions and checked that
// FTK is stopped. Only the scripts shipped inside this launcher may run here.
func updateInstallBundled(ctx context.Context, game, bundle string) error {
	if !filepath.IsAbs(bundle) {
		return errors.New("absolute bundle directory required for initial install or repair")
	}
	var manifest updateBundle
	if e := marketRead(filepath.Join(bundle, "bundle-manifest.json"), &manifest, marketLimit); e != nil {
		return fmt.Errorf("bundled installation manifest: %w", e)
	}
	if manifest.SchemaVersion != 1 || !marketVersion.MatchString(manifest.FrameworkVersion) {
		return errors.New("unsupported bundled installation manifest")
	}
	if bundledFrameworkVersion != "" && manifest.FrameworkVersion != bundledFrameworkVersion {
		return errors.New("bundled manifest version does not match this launcher")
	}
	dll := filepath.Join(bundle, "FTKModFramework.dll")
	helper := filepath.Join(bundle, updateHelperName(game))
	for path, expected := range map[string]string{dll: manifest.DLLSHA256, helper: manifest.Helpers[updateHelperAsset(game)]} {
		info, e := os.Lstat(path)
		if e != nil || !info.Mode().IsRegular() || info.Size() == 0 || info.Size() > updaterAssetLimit {
			return fmt.Errorf("missing or unsafe bundled installation file: %s", path)
		}
		actual, e := marketHashFile(path)
		if e != nil || expected == "" || actual != expected {
			return fmt.Errorf("bundled installation checksum mismatch: %s", path)
		}
	}
	if e := updatePE(dll); e != nil {
		return e
	}
	script := filepath.Join(bundle, "install.sh")
	executable := "/bin/bash"
	args := []string{script, "--game-dir", game, "--framework", dll, "--helper", helper, "--yes"}
	if runtime.GOOS == "windows" {
		script = filepath.Join(bundle, "install.ps1")
		executable = filepath.Join(os.Getenv("SystemRoot"), "System32", "WindowsPowerShell", "v1.0", "powershell.exe")
		args = []string{"-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-GameDir", game, "-Framework", dll, "-Helper", helper}
	}
	if !filepath.IsAbs(executable) {
		return errors.New("system installer interpreter path is unavailable")
	}
	info, e := os.Lstat(script)
	if e != nil || !info.Mode().IsRegular() || info.Size() == 0 || info.Size() > 1<<20 {
		return errors.New("bundled installer script is missing or unsafe; download the complete launcher")
	}
	cmd := exec.CommandContext(ctx, executable, args...)
	cmd.Dir = bundle
	cmd.Stdout, cmd.Stderr = os.Stdout, os.Stderr
	cmd.WaitDelay = 5 * time.Second
	updateBootstrapProcess(cmd)
	if e = cmd.Run(); e != nil {
		return fmt.Errorf("bundled installation did not complete; game launch stopped: %w", e)
	}
	return nil
}
