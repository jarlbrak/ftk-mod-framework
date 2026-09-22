package main

import (
	"errors"
	"os"
	"path/filepath"
	"strings"
)

// Called only while holding runtime.lock followed by transaction.lock. Save pins
// are durable ownership records, not inferred from a generation's age.
func marketCollect(root string, state marketState) (int, error) {
	if err := marketNoSymlink(root, root); err != nil {
		return 0, err
	}
	for _, name := range []string{"generations", "save-pins"} {
		path := filepath.Join(root, name)
		if _, err := os.Lstat(path); err == nil {
			if err := marketNoSymlink(root, path); err != nil {
				return 0, err
			}
		} else if !os.IsNotExist(err) {
			return 0, err
		}
	}
	keep := map[string]bool{}
	add := func(id string) error {
		if id == "" {
			return nil
		}
		if !marketHex.MatchString(id) {
			return errors.New("invalid generation retention record")
		}
		keep[id] = true
		return nil
	}
	for _, id := range []string{state.Current, state.Previous, state.Pending} {
		if err := add(id); err != nil {
			return 0, err
		}
	}
	var intent marketHotIntent
	if err := marketRead(filepath.Join(root, "hot-activation.json"), &intent, marketLimit); err == nil {
		if intent.SchemaVersion != 1 {
			return 0, errors.New("unsupported activation retention record")
		}
		// Retain both sides even after commit until recovery explicitly retires the intent.
		for _, id := range []string{intent.Current, intent.Target} {
			if err := add(id); err != nil {
				return 0, err
			}
		}
	} else if !os.IsNotExist(err) {
		return 0, err
	}
	pins, err := os.ReadDir(filepath.Join(root, "save-pins"))
	if err != nil && !os.IsNotExist(err) {
		return 0, err
	}
	for _, pin := range pins {
		if strings.HasSuffix(pin.Name(), ".tmp") {
			continue
		}
		if pin.IsDir() || pin.Type()&os.ModeSymlink != 0 {
			return 0, errors.New("unsafe save pin")
		}
		var record struct {
			SchemaVersion int    `json:"schemaVersion"`
			Generation    string `json:"generation"`
		}
		if err := marketRead(filepath.Join(root, "save-pins", pin.Name()), &record, marketLimit); err != nil {
			return 0, err
		}
		if record.SchemaVersion != 1 || record.Generation == "" {
			return 0, errors.New("unsupported save pin")
		}
		if err := add(record.Generation); err != nil {
			return 0, err
		}
	}
	base := filepath.Join(root, "generations")
	entries, err := os.ReadDir(base)
	if os.IsNotExist(err) {
		return 0, nil
	}
	if err != nil {
		return 0, err
	}
	// Validate the complete candidate set before deleting anything. Reject symlink
	// entries instead of following them; unknown files belong to other tooling.
	for _, entry := range entries {
		if !marketHex.MatchString(entry.Name()) {
			continue
		}
		if !entry.IsDir() || entry.Type()&os.ModeSymlink != 0 {
			return 0, errors.New("unsafe generation directory")
		}
	}
	removed := 0
	for _, entry := range entries {
		if !marketHex.MatchString(entry.Name()) || keep[entry.Name()] {
			continue
		}
		if err := os.RemoveAll(filepath.Join(base, entry.Name())); err != nil {
			return removed, err
		}
		removed++
	}
	return removed, nil
}

func marketGenerationBudget(root string, packages []marketPackage, limit int64) error {
	if limit < 1 || limit > 1<<40 {
		return errors.New("invalid marketplace storage budget")
	}
	used := int64(marketLimit)
	for _, p := range packages {
		if p.ExpandedSize < 0 || p.ExpandedSize > expandedLimit {
			return errors.New("invalid package expansion estimate")
		}
		used += p.ExpandedSize
	}
	err := filepath.Walk(filepath.Join(root, "generations"), func(path string, info os.FileInfo, err error) error {
		if os.IsNotExist(err) {
			return nil
		}
		if err != nil {
			return err
		}
		if info.Mode()&os.ModeSymlink != 0 {
			return errors.New("unsafe generation path")
		}
		if !info.IsDir() {
			used += info.Size()
		}
		if used > limit {
			return errors.New("marketplace generation storage budget reached; close the game and use the Modded launcher to clean unused generations")
		}
		return nil
	})
	if err != nil {
		return err
	}
	if used > limit {
		return errors.New("prepared generation exceeds marketplace storage budget")
	}
	return nil
}
