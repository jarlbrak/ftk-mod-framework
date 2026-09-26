//go:build !windows

package main

import "errors"

func thunderstoreOpenLauncher(launcher, game string, parent int) error {
	return errors.New("Thunderstore launcher handoff requires native Windows")
}

func thunderstoreShortcut(launcher string) error {
	return errors.New("bootstrap shortcuts require Windows")
}
func thunderstoreShowError(message string) { println(message) }
