//go:build windows

package main

import (
	"context"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"syscall"
	"time"
	"unsafe"
)

// A named event confirms the launcher has initialized its waiting UI. Process
// creation alone is insufficient: .NET startup can fail before a window exists.
func thunderstoreOpenLauncher(launcher, game string, parent int) error {
	kernel := syscall.NewLazyDLL("kernel32.dll")
	createEvent := kernel.NewProc("CreateEventW")
	wait := kernel.NewProc("WaitForSingleObject")
	eventName := `Local\FTKMFBootstrap-` + marketToken()
	name, err := syscall.UTF16PtrFromString(eventName)
	if err != nil {
		return err
	}
	handle, _, callErr := createEvent.Call(0, 1, 0, uintptr(unsafe.Pointer(name)))
	if handle == 0 {
		return fmt.Errorf("create launcher readiness event: %w", callErr)
	}
	defer syscall.CloseHandle(syscall.Handle(handle))
	if err = thunderstoreShortcut(launcher); err != nil {
		return err
	}
	command := exec.Command(launcher, "--game-dir", game, "--wait-for-process", strconv.Itoa(parent), "--ready-event", eventName)
	command.Dir = filepath.Dir(launcher)
	if err = command.Start(); err != nil {
		return fmt.Errorf("open installed launcher: %w", err)
	}
	outcome, _, _ := wait.Call(handle, 30000)
	if outcome != 0 {
		_ = command.Process.Kill()
		_ = command.Wait()
		return errors.New("the launcher did not become ready; the game was left running. Check that .NET Framework 4.8 is installed, then retry setup")
	}
	return command.Process.Release()
}

func thunderstoreShortcut(launcher string) error {
	// The game association is stored alongside the verified launcher, so both this
	// shortcut and a later Steam shortcut retain the exact installation target.
	script := `$dir = Join-Path ([Environment]::GetFolderPath('Programs')) 'FTK Mod Framework'; New-Item -ItemType Directory -Force -Path $dir | Out-Null; $link = Join-Path $dir ('For The King Modded (' + $env:FTK_BOOTSTRAP_ID + ').lnk'); $shell = New-Object -ComObject WScript.Shell; $shortcut = $shell.CreateShortcut($link); $shortcut.TargetPath = $env:FTK_BOOTSTRAP_LAUNCHER; $shortcut.WorkingDirectory = [IO.Path]::GetDirectoryName($env:FTK_BOOTSTRAP_LAUNCHER); $shortcut.Save()`
	ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancel()
	powershell := filepath.Join(os.Getenv("SystemRoot"), "System32", "WindowsPowerShell", "v1.0", "powershell.exe")
	if !filepath.IsAbs(powershell) {
		return errors.New("system PowerShell path unavailable")
	}
	command := exec.CommandContext(ctx, powershell, "-NoProfile", "-NonInteractive", "-Command", "$ErrorActionPreference = 'Stop'; "+script)
	folder := filepath.Base(filepath.Dir(launcher))
	// Include the archive and game identities so another installed launcher entry
	// is never overwritten when a different release or game is bootstrapped.
	command.Env = append(os.Environ(), "FTK_BOOTSTRAP_LAUNCHER="+launcher, "FTK_BOOTSTRAP_ID="+folder[:8]+"-"+folder[len(folder)-16:])
	command.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	if output, err := command.CombinedOutput(); err != nil {
		return fmt.Errorf("create Start menu launcher entry: %w: %s", err, output)
	}
	return nil
}
