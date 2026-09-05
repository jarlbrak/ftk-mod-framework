// Steam shortcut artwork setup. The binary VDF format is private to Steam;
// preserve unknown fields and reject unsupported types instead of guessing.
package main

import (
	"bytes"
	"encoding/binary"
	"errors"
	"flag"
	"fmt"
	"hash/crc32"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"time"
)

const appName = "For The King Modded"

type field struct {
	kind     byte
	name     string
	value    []byte
	children []field
}

func cstring(b []byte, p *int) (string, error) {
	start := *p
	for *p < len(b) && b[*p] != 0 {
		*p++
	}
	if *p >= len(b) {
		return "", errors.New("unterminated VDF string")
	}
	s := string(b[start:*p])
	*p++
	return s, nil
}
func parse(b []byte, p *int, depth int) ([]field, error) {
	if depth > 32 {
		return nil, errors.New("VDF nesting too deep")
	}
	var out []field
	for *p < len(b) {
		kind := b[*p]
		*p++
		if kind == 8 {
			return out, nil
		}
		name, e := cstring(b, p)
		if e != nil {
			return nil, e
		}
		f := field{kind: kind, name: name}
		start := *p
		switch kind {
		case 0:
			f.children, e = parse(b, p, depth+1)
		case 1:
			_, e = cstring(b, p)
		case 2, 3, 4, 6:
			*p += 4
		case 7, 10:
			*p += 8
		default:
			return nil, fmt.Errorf("unsupported VDF field type %d", kind)
		}
		if e != nil {
			return nil, e
		}
		if *p > len(b) {
			return nil, errors.New("truncated VDF value")
		}
		if kind != 0 {
			f.value = append([]byte(nil), b[start:*p]...)
		}
		out = append(out, f)
	}
	return nil, errors.New("missing VDF terminator")
}
func decode(b []byte) ([]field, error) {
	p := 0
	f, e := parse(b, &p, 0)
	if e == nil && p != len(b) {
		e = errors.New("trailing VDF bytes")
	}
	return f, e
}
func encode(f []field) []byte {
	var b bytes.Buffer
	for _, v := range f {
		b.WriteByte(v.kind)
		b.WriteString(v.name)
		b.WriteByte(0)
		if v.kind == 0 {
			b.Write(encode(v.children))
		} else {
			b.Write(v.value)
		}
	}
	b.WriteByte(8)
	return b.Bytes()
}
func textField(n, v string) field { return field{kind: 1, name: n, value: append([]byte(v), 0)} }
func intField(n string, v uint32) field {
	b := make([]byte, 4)
	binary.LittleEndian.PutUint32(b, v)
	return field{kind: 2, name: n, value: b}
}
func get(f []field, n string) *field {
	for i := range f {
		if strings.EqualFold(f[i].name, n) {
			return &f[i]
		}
	}
	return nil
}
func str(f []field, n string) string {
	v := get(f, n)
	if v == nil || v.kind != 1 || len(v.value) == 0 {
		return ""
	}
	return string(v.value[:len(v.value)-1])
}
func idFor(exe, name string) uint32 { return crc32.ChecksumIEEE([]byte(exe+name)) | 0x80000000 }
func idOf(f []field) uint32 {
	v := get(f, "appid")
	if v != nil && v.kind == 2 && len(v.value) == 4 {
		return binary.LittleEndian.Uint32(v.value)
	}
	return idFor(str(f, "Exe"), str(f, "AppName"))
}
func sameExe(a, b string) bool {
	a = filepath.Clean(strings.Trim(a, "\""))
	b = filepath.Clean(strings.Trim(b, "\""))
	if runtime.GOOS == "windows" {
		return strings.EqualFold(a, b)
	}
	return a == b
}
func update(b []byte, launcher, icon string, create bool) ([]byte, uint32, bool, error) {
	var f []field
	var err error
	if len(b) == 0 {
		f = []field{{kind: 0, name: "shortcuts"}}
	} else {
		f, err = decode(b)
		if err != nil {
			return nil, 0, false, err
		}
	}
	root := get(f, "shortcuts")
	if root == nil || root.kind != 0 {
		return nil, 0, false, errors.New("missing shortcuts object")
	}
	quoted := "\"" + launcher + "\""
	for _, entry := range root.children {
		if entry.kind == 0 && sameExe(str(entry.children, "Exe"), launcher) {
			return b, idOf(entry.children), true, nil
		}
	}
	id := idFor(quoted, appName)
	used := map[string]bool{}
	for _, entry := range root.children {
		used[entry.name] = true
		if entry.kind == 0 && idOf(entry.children) == id {
			return nil, 0, false, errors.New("shortcut ID collision")
		}
	}
	if !create {
		return b, id, false, nil
	}
	index := 0
	for used[strconv.Itoa(index)] {
		index++
	}
	values := []field{intField("appid", id), textField("AppName", appName), textField("Exe", quoted), textField("StartDir", "\""+filepath.Dir(launcher)+"\""), textField("icon", icon), textField("ShortcutPath", ""), textField("LaunchOptions", ""), intField("IsHidden", 0), intField("AllowDesktopConfig", 1), intField("AllowOverlay", 1), intField("OpenVR", 0), intField("Devkit", 0), textField("DevkitGameID", ""), intField("LastPlayTime", 0), {kind: 0, name: "tags", children: []field{textField("0", "Modded")}}}
	root.children = append(root.children, field{kind: 0, name: strconv.Itoa(index), children: values})
	out := encode(f)
	if _, err = decode(out); err != nil {
		return nil, 0, false, err
	}
	return out, id, true, nil
}
func roots(explicit string) []string {
	if explicit != "" {
		return []string{explicit}
	}
	h, _ := os.UserHomeDir()
	r := []string{filepath.Join(h, "Library/Application Support/Steam"), filepath.Join(h, ".local/share/Steam"), filepath.Join(h, ".steam/steam"), filepath.Join(h, ".var/app/com.valvesoftware.Steam/.local/share/Steam"), filepath.Join(h, "snap/steam/common/.local/share/Steam")}
	if runtime.GOOS == "windows" {
		for _, env := range []string{"ProgramFiles(x86)", "ProgramFiles"} {
			if v := os.Getenv(env); v != "" {
				r = append(r, filepath.Join(v, "Steam"))
			}
		}
		out, err := exec.Command("reg", "query", `HKCU\Software\Valve\Steam`, "/v", "SteamPath").Output()
		if err == nil {
			for _, line := range strings.Split(string(out), "\n") {
				if i := strings.Index(line, "REG_SZ"); i >= 0 {
					r = append(r, strings.TrimSpace(line[i+6:]))
				}
			}
		}
	}
	return r
}
func steamRunning() (bool, error) {
	if runtime.GOOS == "windows" {
		b, e := exec.Command("tasklist", "/FI", "IMAGENAME eq steam.exe", "/NH").Output()
		return strings.Contains(strings.ToLower(string(b)), "steam.exe"), e
	}
	name := "steam"
	if runtime.GOOS == "darwin" {
		name = "steam_osx"
	}
	e := exec.Command("pgrep", "-x", name).Run()
	if e == nil {
		return true, nil
	}
	var ee *exec.ExitError
	if errors.As(e, &ee) && ee.ExitCode() == 1 {
		return false, nil
	}
	return false, e
}
func atomicWrite(path string, b []byte, mode os.FileMode) error {
	f, e := os.CreateTemp(filepath.Dir(path), ".ftkmf-*")
	if e != nil {
		return e
	}
	// Keep a failed temporary file recoverable for diagnosis.
	if _, e = f.Write(b); e != nil {
		f.Close()
		return e
	}
	if e = f.Chmod(mode); e != nil {
		f.Close()
		return e
	}
	if e = f.Close(); e != nil {
		return e
	}
	return os.Rename(f.Name(), path)
}
func copyArt(config, art string, id uint32) error {
	grid := filepath.Join(config, "grid")
	if e := os.MkdirAll(grid, 0755); e != nil {
		return e
	}
	suffixes := map[string]string{"header.png": ".png", "capsule.png": "p.png", "hero.png": "_hero.png", "logo.png": "_logo.png", "icon.png": "_icon.png"}
	for src, suffix := range suffixes {
		dest := filepath.Join(grid, strconv.FormatUint(uint64(id), 10)+suffix)
		occupied := false
		matches, e := filepath.Glob(strings.TrimSuffix(dest, ".png") + ".*")
		if e != nil {
			return e
		}
		for _, existing := range matches {
			switch strings.ToLower(filepath.Ext(existing)) {
			case ".png", ".jpg", ".jpeg", ".webp", ".gif":
				occupied = true
			}
		}
		if occupied {
			continue
		}
		data, e := os.ReadFile(filepath.Join(art, src))
		if e != nil {
			return e
		}
		if e = atomicWrite(dest, data, 0644); e != nil {
			return e
		}
	}
	return nil
}
func run(args []string) error {
	if len(args) == 0 {
		return errors.New("usage: helper artwork|register --launcher PATH --art DIR [--steam-root DIR]")
	}
	create := args[0] == "register"
	if !create && args[0] != "artwork" {
		return errors.New("unknown command")
	}
	fs := flag.NewFlagSet(args[0], flag.ContinueOnError)
	launcher := fs.String("launcher", "", "launcher executable or .app")
	art := fs.String("art", "", "art directory")
	rootArg := fs.String("steam-root", "", "override Steam root")
	if e := fs.Parse(args[1:]); e != nil {
		return e
	}
	if *launcher == "" || *art == "" {
		return errors.New("--launcher and --art are required")
	}
	launcherAbs, e := filepath.Abs(*launcher)
	if e != nil {
		return e
	}
	if _, e = os.Stat(launcherAbs); e != nil {
		return e
	}
	for _, name := range []string{"header.png", "capsule.png", "hero.png", "logo.png", "icon.png"} {
		if _, e = os.Stat(filepath.Join(*art, name)); e != nil {
			return e
		}
	}
	if create {
		running, e := steamRunning()
		if e != nil {
			return fmt.Errorf("cannot confirm Steam is closed: %w", e)
		}
		if running {
			return errors.New("close Steam, then run Add to Steam again; ordinary first-launch artwork can be applied while Steam runs")
		}
	}
	found := 0
	seen := map[string]bool{}
	for _, root := range roots(*rootArg) {
		configs, _ := filepath.Glob(filepath.Join(root, "userdata", "*", "config"))
		for _, config := range configs {
			resolved, e := filepath.EvalSymlinks(config)
			if e != nil {
				return e
			}
			if seen[resolved] {
				continue
			}
			seen[resolved] = true
			path := filepath.Join(config, "shortcuts.vdf")
			b, e := os.ReadFile(path)
			if e != nil && !os.IsNotExist(e) {
				return e
			}
			out, id, matched, e := update(b, launcherAbs, filepath.Join(*art, "icon.png"), create)
			if e != nil {
				return fmt.Errorf("%s: %w", path, e)
			}
			if !matched && !create {
				fmt.Printf("Shortcut not saved yet. Preparing default artwork; relaunch after restarting Steam if needed.\n")
			}
			if create && !bytes.Equal(b, out) {
				running, e := steamRunning()
				if e != nil || running {
					return errors.New("Steam started during setup; no shortcut changes written")
				}
				if len(b) > 0 {
					backup := path + ".ftkmf-backup-" + time.Now().Format("20060102-150405.000000000")
					if e = os.WriteFile(backup, b, 0600); e != nil {
						return e
					}
				}
				if e = atomicWrite(path, out, 0600); e != nil {
					return e
				}
			}
			if e = copyArt(config, *art, id); e != nil {
				return e
			}
			found++
			fmt.Printf("Steam artwork ready for %s (shortcut %d).\n", appName, id)
		}
	}
	if found == 0 {
		return errors.New("no Steam account found; sign in to Steam once, then relaunch")
	}
	return nil
}
func main() {
	if len(os.Args) > 1 && (os.Args[1] == "marketplace-validate" || os.Args[1] == "marketplace-fixture" || os.Args[1] == "marketplace-catalog-validate") {
		if e := marketDeveloper(os.Args[1:]); e != nil {
			fmt.Fprintln(os.Stderr, e)
			os.Exit(1)
		}
		return
	}
	if len(os.Args) > 1 && os.Args[1] == "marketplace" {
		if e := marketplaceMain(os.Args[2:]); e != nil {
			fmt.Fprintln(os.Stderr, e)
			os.Exit(1)
		}
		return
	}
	if e := run(os.Args[1:]); e != nil {
		fmt.Fprintln(os.Stderr, e)
		os.Exit(1)
	}
}
