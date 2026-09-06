//go:build !windows

package main

import (
	"os/exec"
	"syscall"
)

func updateBootstrapProcess(cmd *exec.Cmd) {
	cmd.SysProcAttr = &syscall.SysProcAttr{Setpgid: true}
	// Cancel shell children too, so a timed-out installer cannot keep writing
	// after the helper releases the installation/update lock.
	cmd.Cancel = func() error { return syscall.Kill(-cmd.Process.Pid, syscall.SIGKILL) }
}
