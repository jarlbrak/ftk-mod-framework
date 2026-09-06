package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"strings"
)

// Preflight only the JSON/behavior manifests the framework discovers. Unrelated
// BepInEx DLLs have no reliable author-version contract and are not certified here.
func updateManualPackages(game string) ([]marketPackage, error) {
	root := filepath.Join(game, "BepInEx", "plugins")
	enabled, development := true, false
	configPath := filepath.Join(game, "BepInEx", "config", "com.ftkmf.framework.cfg")
	config, e := marketReadBytes(configPath, marketLimit)
	if e != nil && !os.IsNotExist(e) {
		return nil, e
	}
	section := ""
	configured := ""
	for _, line := range strings.Split(strings.TrimPrefix(string(config), "\ufeff"), "\n") {
		line = strings.TrimSpace(line)
		if line == "" || strings.HasPrefix(line, "#") || strings.HasPrefix(line, ";") {
			continue
		}
		if strings.HasPrefix(line, "[") && strings.HasSuffix(line, "]") {
			section = strings.TrimSpace(line[1 : len(line)-1])
			continue
		}
		kv := strings.SplitN(line, "=", 2)
		if len(kv) != 2 {
			continue
		}
		key, value := strings.TrimSpace(kv[0]), strings.TrimSpace(kv[1])
		if section == "Data" && key == "DataContentRoot" {
			configured = value
		}
		if (section == "Data" && key == "EnableDataContent") || (section == "Diagnostics" && key == "RunSelfTests") {
			v := strings.ToLower(value)
			if v != "true" && v != "false" {
				return nil, fmt.Errorf("cannot verify manual mods: invalid %s/%s in %s", section, key, configPath)
			}
			if key == "EnableDataContent" {
				enabled = v == "true"
			} else {
				development = v == "true"
			}
		}
	}
	if !enabled {
		return nil, nil
	}
	if configured != "" {
		configured, e = updateValidateManualPath(configured, runtime.GOOS, updateWindowsGame(game))
		if e != nil {
			return nil, e
		}
		if filepath.IsAbs(configured) {
			root = configured
		} else {
			root = filepath.Join(game, configured)
		}
	}
	folders, e := os.ReadDir(root)
	if os.IsNotExist(e) {
		return nil, nil
	}
	if e != nil {
		return nil, e
	}
	if len(folders) > 5000 {
		return nil, errors.New("manual mod discovery exceeds the folder limit")
	}
	packages := []marketPackage{}
	for _, folder := range folders {
		dir := filepath.Join(root, folder.Name())
		info, e := os.Stat(dir)
		if e != nil || !info.IsDir() {
			continue
		}
		manifestPath := filepath.Join(dir, "manifest.json")
		raw, e := marketReadBytes(manifestPath, marketLimit)
		if os.IsNotExist(e) {
			continue
		}
		if e != nil {
			return nil, e
		}
		var m struct {
			ModGUID          string `json:"modGuid"`
			Name             string `json:"name"`
			Version          string `json:"version"`
			FrameworkVersion string `json:"frameworkVersion"`
			DevelopmentOnly  bool   `json:"developmentOnly"`
		}
		if e = marketUniqueJSON(raw); e != nil {
			return nil, fmt.Errorf("manual mod manifest %s is malformed: %w", manifestPath, e)
		}
		if e = json.Unmarshal(raw, &m); e != nil {
			return nil, fmt.Errorf("manual mod manifest %s is malformed: %w", manifestPath, e)
		}
		if !development && (m.DevelopmentOnly || marketDeveloperGUID(m.ModGUID)) {
			continue
		}
		if m.ModGUID == "com.ftkmf.synthetic" && folder.Name() != "__ftkmf_synthetic__" {
			continue
		}
		if strings.TrimSpace(m.ModGUID) == "" || strings.TrimSpace(m.Name) == "" || !marketStrictVersion(m.Version) {
			return nil, fmt.Errorf("manual mod manifest %s has incomplete identity; update deferred", manifestPath)
		}
		if _, valid := marketFrameworkVersion(m.FrameworkVersion); !valid {
			return nil, fmt.Errorf("manual mod %s must declare a valid author-confirmed frameworkVersion in %s before framework updates", m.Name, manifestPath)
		}
		packages = append(packages, marketPackage{ModGUID: m.ModGUID, Name: m.Name, Version: m.Version, FrameworkVersion: m.FrameworkVersion})
	}
	return packages, nil
}
func updateVersionPackages(game string) ([]marketPackage, error) {
	managed, e := updateManagedPackages(game)
	if e != nil {
		return nil, e
	}
	manual, e := updateManualPackages(game)
	if e != nil {
		return nil, e
	}
	return append(managed, manual...), nil
}

func updateValidateManualPath(configured, host string, windowsGame bool) (string, error) {
	if host != "windows" && !(host == "linux" && windowsGame) {
		return configured, nil
	}
	normalized := strings.ReplaceAll(configured, `\`, "/")
	drive := len(normalized) >= 3 && ((normalized[0] >= 'A' && normalized[0] <= 'Z') || (normalized[0] >= 'a' && normalized[0] <= 'z')) && normalized[1] == ':' && normalized[2] == '/'
	unc := strings.HasPrefix(normalized, "//") && !strings.HasPrefix(normalized, "//?/") && !strings.HasPrefix(normalized, "//./")
	if unc {
		parts := strings.Split(strings.TrimPrefix(normalized, "//"), "/")
		unc = len(parts) >= 2 && parts[0] != "" && parts[1] != ""
	}
	rooted := strings.HasPrefix(normalized, "/") || strings.Contains(normalized, ":")
	if host == "linux" {
		if rooted {
			return "", errors.New("the Linux launcher cannot verify a rooted Windows DataContentRoot; use a game-relative content root before updating")
		}
		return normalized, nil
	}
	if rooted && !drive && !unc {
		return "", errors.New("DataContentRoot is an ambiguous Windows rooted path; use a fully qualified drive/UNC path or a game-relative content root")
	}
	return configured, nil
}
