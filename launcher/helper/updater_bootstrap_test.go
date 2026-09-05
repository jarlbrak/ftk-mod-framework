package main

import (
	"context"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"testing"
	"time"
)

func TestUpdaterBundledBootstrap(t *testing.T) {
	if runtime.GOOS == "windows" {
		t.Skip("Unix script fixture; Windows script is separately mocked in PowerShell")
	}
	game, bundle, _, _, _ := updateFixture(t)
	oldVersion := bundledFrameworkVersion
	bundledFrameworkVersion = "0.1.0"
	t.Cleanup(func() { bundledFrameworkVersion = oldVersion })
	dll, _ := os.ReadFile(filepath.Join(game, updatePaths(game)[0]))
	helper, _ := os.ReadFile(filepath.Join(game, updatePaths(game)[1]))
	os.WriteFile(filepath.Join(bundle, "FTKModFramework.dll"), dll, 0600)
	os.WriteFile(filepath.Join(bundle, updateHelperName(game)), helper, 0755)
	script := `#!/bin/bash
set -eu
[ "$#" = 7 ] && [ "$1" = --game-dir ] && [ "$3" = --framework ] && [ "$5" = --helper ] && [ "$7" = --yes ]
[ "$4" = "$PWD/FTKModFramework.dll" ]
printf 'installed' > "$2/bootstrap-ran"
`
	os.WriteFile(filepath.Join(bundle, "install.sh"), []byte(script), 0600)
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	if e := updateInstallBundled(ctx, game, bundle); e != nil {
		t.Fatal(e)
	}
	if b, e := os.ReadFile(filepath.Join(game, "bootstrap-ran")); e != nil || string(b) != "installed" {
		t.Fatal("fixed bundled installer was not invoked", e)
	}
	os.WriteFile(filepath.Join(bundle, "FTKModFramework.dll"), []byte("wrong bytes"), 0600)
	if e := updateInstallBundled(ctx, game, bundle); e == nil || !strings.Contains(e.Error(), "checksum") {
		t.Fatal("modified bundled DLL reached installer", e)
	}
}

func TestUpdaterBundledBootstrapCancellation(t *testing.T) {
	if runtime.GOOS == "windows" {
		t.Skip("Unix process-group cancellation fixture")
	}
	game, bundle, _, _, _ := updateFixture(t)
	oldVersion := bundledFrameworkVersion
	bundledFrameworkVersion = "0.1.0"
	t.Cleanup(func() { bundledFrameworkVersion = oldVersion })
	dll, _ := os.ReadFile(filepath.Join(game, updatePaths(game)[0]))
	helper, _ := os.ReadFile(filepath.Join(game, updatePaths(game)[1]))
	os.WriteFile(filepath.Join(bundle, "FTKModFramework.dll"), dll, 0600)
	os.WriteFile(filepath.Join(bundle, updateHelperName(game)), helper, 0755)
	// Background child would mutate after its parent times out unless the whole group is stopped.
	os.WriteFile(filepath.Join(bundle, "install.sh"), []byte("#!/bin/bash\n(sleep 1; printf late > \"$2/late-write\") &\nwait\n"), 0600)
	ctx, cancel := context.WithTimeout(context.Background(), 100*time.Millisecond)
	defer cancel()
	if e := updateInstallBundled(ctx, game, bundle); e == nil {
		t.Fatal("timed-out installer succeeded")
	}
	time.Sleep(1200 * time.Millisecond)
	if _, e := os.Stat(filepath.Join(game, "late-write")); !os.IsNotExist(e) {
		t.Fatal("installer child outlived cancellation", e)
	}
}
