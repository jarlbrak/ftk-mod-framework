//go:build windows

package main

import "os/exec"

// The PowerShell installer performs copies/downloads in its own process.
// CommandContext terminates and waits for that process before the lock returns.
func updateBootstrapProcess(cmd *exec.Cmd) {}
