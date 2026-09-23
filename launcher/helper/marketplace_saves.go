package main

import (
	"bytes"
	"context"
	"encoding/binary"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"time"
)

// Matches BinaryWriter's length-prefixed UTF-8 format in SaveSetIdentity. The
// format version is part of the digest; generation IDs never decide compatibility.
func marketSaveFingerprint(gameHash, framework string, packages []marketPackage, settings map[string]interface{}) (string, error) {
	if !marketSHA.MatchString(gameHash) || !marketVersion.MatchString(framework) {
		return "", errors.New("invalid saved set build identity")
	}
	var b bytes.Buffer
	str := func(s string) {
		n := len(s)
		for n >= 128 {
			b.WriteByte(byte(n) | 128)
			n >>= 7
		}
		b.WriteByte(byte(n))
		b.WriteString(s)
	}
	str("ftkmf-save-set-v1")
	str(strings.ToLower(gameHash))
	str(framework)
	writeMap := func(m map[string]string) {
		keys := []string{}
		for k := range m {
			keys = append(keys, k)
		}
		sort.Strings(keys)
		binary.Write(&b, binary.LittleEndian, int32(len(keys)))
		for _, k := range keys {
			str(k)
			str(m[k])
		}
	}
	enabled := map[string]string{}
	for _, p := range packages {
		if p.Enabled {
			if !marketSHA.MatchString(p.SHA256) || enabled[p.ModGUID] != "" {
				return "", errors.New("invalid saved package identity")
			}
			enabled[p.ModGUID] = strings.ToLower(p.SHA256)
		}
	}
	writeMap(enabled)
	mapped := map[string]string{}
	for key, native := range map[string]string{"dataContent": "EnableDataContent", "campaignEngine": "EnableCampaignEngine", "behaviorLoading": "EnableBehaviorLoading"} {
		value, ok := settings[native].(bool)
		if !ok {
			return "", fmt.Errorf("saved setting %s is missing", native)
		}
		if value {
			mapped[key] = "True"
		} else {
			mapped[key] = "False"
		}
	}
	writeMap(mapped)
	return marketHash(b.Bytes()), nil
}

// Early immutable generations did not record framework settings. Their save
// fingerprints still contain the compatibility settings for that framework version, so reconstruct
// only missing historical values from the running request and compare that
// result with the pinned fingerprint. A changed current value therefore still
// rejects the restore; it is never silently accepted as a default.
func marketSavedSettings(lock, current map[string]interface{}) map[string]interface{} {
	merged := map[string]interface{}{}
	for key, value := range current {
		merged[key] = value
	}
	for key, value := range lock {
		merged[key] = value
	}
	return merged
}

func marketRestoreSaveSet(r marketRequest) (string, error) {
	if !marketSHA.MatchString(r.SaveFingerprint) {
		return "", errors.New("invalid saved mod set identity")
	}
	pinPath := filepath.Join(r.StateRoot, "save-pins", r.SaveFingerprint+".json")
	if err := marketNoSymlink(r.StateRoot, pinPath); err != nil {
		return "", err
	}
	var pin struct {
		SchemaVersion int    `json:"schemaVersion"`
		Generation    string `json:"generation"`
	}
	if err := marketRead(pinPath, &pin, marketLimit); err != nil {
		return "", err
	}
	if pin.SchemaVersion != 1 || !marketHex.MatchString(pin.Generation) {
		return "", errors.New("invalid saved generation pin")
	}
	var lock marketLock
	path := filepath.Join(r.StateRoot, "generations", pin.Generation, "lock.json")
	if err := marketNoSymlink(r.StateRoot, path); err != nil {
		return "", err
	}
	if err := marketRead(path, &lock, marketLimit); err != nil {
		return "", err
	}
	if lock.FrameworkVersion != r.FrameworkVersion {
		return "", fmt.Errorf("this save library requires framework %s", lock.FrameworkVersion)
	}
	savedSettings := marketSavedSettings(lock.Settings, r.Settings)
	fingerprint, err := marketSaveFingerprint(r.gameFingerprint, lock.FrameworkVersion, lock.Packages, savedSettings)
	if err != nil {
		return "", err
	}
	if fingerprint != r.SaveFingerprint {
		return "", errors.New("saved mod set does not match this game build or its retained artifacts")
	}
	// Validate against today's running settings too; restoring content does not silently
	// change framework behavior configuration.
	activeFingerprint, err := marketSaveFingerprint(r.gameFingerprint, r.FrameworkVersion, lock.Packages, r.Settings)
	if err != nil {
		return "", err
	}
	if activeFingerprint != fingerprint {
		return "", errors.New("restore the original framework settings before selecting this save library")
	}
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	if err = marketValidateGeneration(ctx, r, pin.Generation); err != nil {
		return "", err
	}
	if _, err = os.Stat(filepath.Dir(path)); err != nil {
		return "", err
	}
	return pin.Generation, nil
}
