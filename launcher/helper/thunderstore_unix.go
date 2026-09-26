//go:build !windows

package main

import "errors"

func thunderstoreOpenLauncher(launcher, game string, parent int) error {
	return errors.New("Thunderstore launcher handoff requires native Windows")
}
