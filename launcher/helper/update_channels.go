package main

import (
	"context"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"net/url"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"time"
)

const updateReleasesURL = "https://api.github.com/repos/jarlbrak/ftk-mod-framework/releases"

type updateSettings struct {
	SchemaVersion int    `json:"schemaVersion"`
	Mode          string `json:"mode"`
	Tag           string `json:"tag,omitempty"`
	ReleaseID     int64  `json:"releaseId,omitempty"`
	NotesSHA256   string `json:"notesSha256,omitempty"`
}
type updateChannelRequest struct {
	ReleaseID     int64  `json:"releaseId,omitempty"`
	SchemaVersion int    `json:"schemaVersion"`
	OperationID   string `json:"operationId"`
	GameDir       string `json:"gameDir"`
	Mode          string `json:"mode,omitempty"`
	Tag           string `json:"tag,omitempty"`
}
type updateReleaseInfo struct {
	ReleaseID   int64    `json:"releaseId"`
	Version     string   `json:"version"`
	Tag         string   `json:"tag"`
	Prerelease  bool     `json:"prerelease"`
	PublishedAt string   `json:"publishedAt"`
	URL         string   `json:"url"`
	Notes       string   `json:"notes"`
	Available   bool     `json:"available"`
	Reason      string   `json:"reason"`
	Assets      []string `json:"assets,omitempty"`
}
type updateHistory struct {
	SchemaVersion int                 `json:"schemaVersion"`
	FetchedAt     int64               `json:"fetchedAt"`
	StableTag     string              `json:"stableTag"`
	Releases      []updateReleaseInfo `json:"releases"`
	MoreAvailable bool                `json:"moreAvailable"`
}
type updateChannelResult struct {
	SchemaVersion      int                 `json:"schemaVersion"`
	OperationID        string              `json:"operationId"`
	OK                 bool                `json:"ok"`
	Status             string              `json:"status"`
	Message            string              `json:"message"`
	InstalledVersion   string              `json:"installedVersion"`
	SelectedMode       string              `json:"selectedMode"`
	SelectedTag        string              `json:"selectedTag"`
	Stable             *updateReleaseInfo  `json:"stable"`
	Preview            *updateReleaseInfo  `json:"preview"`
	Releases           []updateReleaseInfo `json:"releases"`
	CacheAgeSeconds    int64               `json:"cacheAgeSeconds"`
	MoreAvailable      bool                `json:"moreAvailable"`
	LauncherCompatible bool                `json:"launcherCompatible"`
	LauncherMessage    string              `json:"launcherMessage"`
}
type updateCapabilities struct {
	SchemaVersion    int    `json:"schemaVersion"`
	ReleaseSelection int    `json:"releaseSelection"`
	LauncherVersion  string `json:"launcherVersion"`
}
type updateHTTPError struct{ StatusCode int }

func (e *updateHTTPError) Error() string {
	return fmt.Sprintf("official release endpoint returned HTTP %d", e.StatusCode)
}
func updateMissingRelease(e error) bool {
	var h *updateHTTPError
	return errors.As(e, &h) && h.StatusCode == 404
}
func updateReadSettings(game string) (updateSettings, error) {
	s := updateSettings{SchemaVersion: 1, Mode: "stable"}
	e := marketRead(filepath.Join(updateRoot(game), "update-settings.json"), &s, 65536)
	if os.IsNotExist(e) {
		return s, nil
	}
	if e != nil {
		return s, e
	}
	if s.SchemaVersion != 1 || !contains([]string{"stable", "preview", "pinned"}, s.Mode) {
		return s, errors.New("unsupported update preference schema or mode")
	}
	if s.Mode == "pinned" && (!updateValidTag(s.Tag) || s.ReleaseID <= 0) {
		return s, errors.New("invalid pinned release identity")
	}
	return s, nil
}
func updateWriteSettings(game string, s updateSettings) error {
	return marketWrite(filepath.Join(updateRoot(game), "update-settings.json"), s)
}
func updateAPIAllowed(s string) bool {
	if s == updaterLatestURL {
		return true
	}
	for page := 1; page <= 3; page++ {
		if s == fmt.Sprintf("%s?per_page=30&page=%d", updateReleasesURL, page) {
			return true
		}
	}
	prefix := updateReleasesURL + "/tags/"
	if strings.HasPrefix(s, prefix) {
		tag, e := url.PathUnescape(strings.TrimPrefix(s, prefix))
		return e == nil && updateValidTag(tag)
	}
	return false
}
func updateReleaseVersion(r updateRelease) string { return strings.TrimPrefix(r.TagName, "v") }
func updateInfo(r updateRelease) updateReleaseInfo {
	notes := r.Body
	if len(notes) > 16384 {
		notes = notes[:16384] + "\n[Release notes truncated. Open the official release for the full text.]"
	}
	assets := []string{}
	for _, a := range r.Assets {
		assets = append(assets, a.Name)
	}
	return updateReleaseInfo{ReleaseID: r.ID, Version: updateReleaseVersion(r), Tag: r.TagName, Prerelease: r.Prerelease, PublishedAt: r.PublishedAt, URL: "https://github.com/jarlbrak/ftk-mod-framework/releases/tag/" + url.PathEscape(r.TagName), Notes: notes, Assets: assets}
}
func updateCollectHistory(ctx context.Context) (updateHistory, error) {
	h := updateHistory{SchemaVersion: 1, FetchedAt: time.Now().Unix(), Releases: []updateReleaseInfo{}}
	byTag := map[string]updateReleaseInfo{}
	metaCtx, cancel := context.WithTimeout(ctx, 5*time.Second)
	raw, e := updateFetch(metaCtx, updaterLatestURL, marketLimit)
	cancel()
	if e != nil && !updateMissingRelease(e) {
		return h, e
	}
	if e == nil {
		var stable updateRelease
		if e = json.Unmarshal(raw, &stable); e != nil {
			return h, e
		}
		if stable.Draft || stable.Prerelease || stable.ID <= 0 || !updateValidTag(stable.TagName) {
			return h, errors.New("unsupported stable release metadata")
		}
		h.StableTag = stable.TagName
		byTag[stable.TagName] = updateInfo(stable)
	}
	for page := 1; page <= 3; page++ {
		if e = ctx.Err(); e != nil {
			return h, e
		}
		raw, e = updateFetch(ctx, fmt.Sprintf("%s?per_page=30&page=%d", updateReleasesURL, page), marketLimit)
		if e != nil {
			return h, e
		}
		var releases []updateRelease
		if e = json.Unmarshal(raw, &releases); e != nil {
			return h, e
		}
		if releases == nil || len(releases) > 30 {
			return h, errors.New("invalid release history page")
		}
		for _, r := range releases {
			if r.Draft || r.ID <= 0 || !updateValidTag(r.TagName) {
				continue
			}
			byTag[r.TagName] = updateInfo(r)
		}
		if len(releases) < 30 {
			break
		}
		if page == 3 {
			h.MoreAvailable = true
		}
	}
	for _, r := range byTag {
		h.Releases = append(h.Releases, r)
	}
	sort.Slice(h.Releases, func(i, j int) bool {
		a, b := h.Releases[i], h.Releases[j]
		cmp := marketCompare(a.Version, b.Version)
		if cmp != 0 {
			return cmp > 0
		}
		if a.Prerelease != b.Prerelease {
			return !a.Prerelease
		}
		return a.PublishedAt > b.PublishedAt
	})
	return h, nil
}
func updateHistoryRead(game string) (updateHistory, error) {
	h := updateHistory{SchemaVersion: 1, Releases: []updateReleaseInfo{}}
	e := marketRead(filepath.Join(updateRoot(game), "release-history.json"), &h, marketLimit)
	if os.IsNotExist(e) {
		return h, nil
	}
	if e != nil {
		return h, e
	}
	if h.SchemaVersion != 1 || len(h.Releases) > 91 {
		return h, errors.New("unsupported release history cache")
	}
	for _, r := range h.Releases {
		if r.ReleaseID <= 0 || !marketVersion.MatchString(r.Version) || r.Tag != "v"+r.Version {
			return h, errors.New("invalid cached release identity")
		}
	}
	return h, nil
}
func frameworkUpdatesMain(args []string) error {
	if len(args) == 0 {
		return errors.New("framework-updates operation required")
	}
	fs := flag.NewFlagSet("framework-updates", flag.ContinueOnError)
	request := fs.String("request", "", "request JSON")
	result := fs.String("result", "", "result JSON")
	if e := fs.Parse(args[1:]); e != nil {
		return e
	}
	if *request == "" || *result == "" || fs.NArg() != 0 {
		return errors.New("request and result required")
	}
	var r updateChannelRequest
	if e := marketRead(*request, &r, 65536); e != nil {
		return e
	}
	out, e := updateChannels(args[0], r)
	if e != nil {
		out.OK = false
		out.Status = "error"
		out.Message = e.Error()
	}
	if we := marketWrite(*result, out); we != nil {
		return we
	}
	return e
}
func updateChannels(op string, r updateChannelRequest) (updateChannelResult, error) {
	out := updateChannelResult{SchemaVersion: 1, OperationID: r.OperationID, OK: true, Status: "ready", Releases: []updateReleaseInfo{}, CacheAgeSeconds: -1}
	if r.SchemaVersion != 1 || !marketHex.MatchString(r.OperationID) || !filepath.IsAbs(r.GameDir) || !contains([]string{"status", "refresh", "select"}, op) {
		return out, errors.New("invalid framework update request")
	}
	settings, e := updateReadSettings(r.GameDir)
	if e != nil {
		return out, e
	}
	history, e := updateHistoryRead(r.GameDir)
	if e != nil {
		return out, e
	}
	if op == "refresh" {
		ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
		fresh, fetchErr := updateCollectHistory(ctx)
		cancel()
		if fetchErr != nil {
			out.Status = "offline"
			out.Message = "Release information is unavailable. " + fetchErr.Error()
			if history.FetchedAt == 0 {
				out.Status = "unavailable"
			}
		} else {
			history = fresh
			if e = marketWrite(filepath.Join(updateRoot(r.GameDir), "release-history.json"), history); e != nil {
				return out, e
			}
			out.Status = "online"
		}
	}
	if op == "select" {
		if !contains([]string{"stable", "preview", "pinned"}, r.Mode) {
			return out, errors.New("choose Stable, Preview or an exact listed version")
		}
		settings = updateSettings{SchemaVersion: 1, Mode: r.Mode}
		if r.Mode == "pinned" {
			found := false
			for _, release := range history.Releases {
				if release.Tag == r.Tag {
					if r.ReleaseID <= 0 || r.ReleaseID != release.ReleaseID {
						return out, errors.New("Release identity changed; refresh and review that version again before pinning.")
					}
					if !updateReleaseHasAssets(release, r.GameDir) {
						return out, errors.New("this release does not provide the required verified update assets")
					}
					settings.Tag = release.Tag
					settings.ReleaseID = release.ReleaseID
					h := sha256.Sum256([]byte(release.Notes))
					settings.NotesSHA256 = hex.EncodeToString(h[:])
					found = true
					break
				}
			}
			if !found {
				return out, errors.New("refresh releases and review that exact version before pinning")
			}
		}
		if e = os.MkdirAll(updateRoot(r.GameDir), 0700); e != nil {
			return out, e
		}
		unlock, e := marketAcquire(filepath.Join(updateRoot(r.GameDir), "launch-update.lock"))
		if e != nil {
			return out, errors.New("the launcher is preparing the game; try saving this choice again shortly")
		}
		e = updateWriteSettings(r.GameDir, settings)
		unlock()
		if e != nil {
			return out, e
		}
		out.Message = "Saved for the next Modded launcher start. The running game is unchanged."
	}
	out.SelectedMode = settings.Mode
	out.SelectedTag = settings.Tag
	var receipt updateReceipt
	if e = marketRead(filepath.Join(updateRoot(r.GameDir), "installation.json"), &receipt, 65536); e == nil && receipt.SchemaVersion == 1 && marketVersion.MatchString(receipt.FrameworkVersion) {
		out.InstalledVersion = receipt.FrameworkVersion
	}
	var capabilities updateCapabilities
	if marketRead(filepath.Join(updateRoot(r.GameDir), "launcher-capabilities.json"), &capabilities, 65536) == nil && capabilities.SchemaVersion == 1 && capabilities.ReleaseSelection >= 1 {
		out.LauncherCompatible = true
		out.LauncherMessage = "The last Modded launcher start reported support for release selection."
	} else {
		out.LauncherMessage = "Download the updated Modded launcher once to apply this choice. Older launchers only follow Stable."
	}
	out.MoreAvailable = history.MoreAvailable
	if history.FetchedAt > 0 {
		out.CacheAgeSeconds = time.Now().Unix() - history.FetchedAt
		if out.CacheAgeSeconds < 0 {
			out.CacheAgeSeconds = 0
		}
	}
	if len(history.Releases) == 0 && out.Status == "ready" {
		out.Status = "unavailable"
		out.Message = "Refresh to load official releases and patch notes."
	}
	managedPackages, managedErr := updateVersionPackages(r.GameDir)
	for _, release := range history.Releases {
		release.URL = "https://github.com/jarlbrak/ftk-mod-framework/releases/tag/" + url.PathEscape(release.Tag)
		release.Available = updateReleaseHasAssets(release, r.GameDir)
		release.Reason = "Compatibility and verified assets are checked at the next Modded launcher start."
		if !release.Available {
			release.Reason = "This release lacks the required update manifest or platform assets."
		} else if managedErr != nil {
			release.Available = false
			release.Reason = managedErr.Error()
		} else if e := updatePackagesCompatibility(managedPackages, release.Version); e != nil {
			release.Available = false
			release.Reason = e.Error()
		}
		if out.InstalledVersion != "" && release.Version == out.InstalledVersion {
			release.Reason = "Already installed. Pinning keeps this version."
		}
		if marketCompare(release.Version, "0.1.0") <= 0 {
			release.Reason += " This version has no in-game version picker; use the launcher's Restore bundled version action to return."
		}
		out.Releases = append(out.Releases, release)
		if release.Tag == history.StableTag && !release.Prerelease {
			copy := release
			out.Stable = &copy
		}
		if out.Preview == nil {
			copy := release
			out.Preview = &copy
		}
	}
	if settings.Mode == "stable" && out.Stable != nil && out.InstalledVersion != "" && marketCompare(out.Stable.Version, out.InstalledVersion) < 0 {
		out.Message = "Stable is older than the installed version. Keep this version until Stable catches up; no automatic downgrade."
	}
	if out.Stable == nil && out.Message == "" {
		out.Message = "No stable release is published yet. Preview follows the newest release, including previews."
	}
	return out, nil
}
func updateReleaseHasAssets(r updateReleaseInfo, game string) bool {
	return contains(r.Assets, "update.json") && contains(r.Assets, "FTKModFramework.dll") && contains(r.Assets, updateHelperAsset(game))
}
func updateSelectRelease(ctx context.Context, game string) (updateRelease, updateSettings, error) {
	settings, e := updateReadSettings(game)
	if e != nil {
		return updateRelease{}, settings, e
	}
	if settings.Mode == "preview" {
		history, e := updateCollectHistory(ctx)
		if e != nil {
			return updateRelease{}, settings, e
		}
		if len(history.Releases) == 0 {
			return updateRelease{}, settings, errors.New("no preview or stable releases are published yet")
		}
		selected := history.Releases[0]
		return updateRelease{ID: selected.ReleaseID, TagName: selected.Tag, Prerelease: selected.Prerelease, PublishedAt: selected.PublishedAt, Body: selected.Notes}, settings, nil
	}
	endpoint := updaterLatestURL
	if settings.Mode == "pinned" {
		endpoint = updateReleasesURL + "/tags/" + url.PathEscape(settings.Tag)
	}
	metadataCtx, cancel := context.WithTimeout(ctx, 5*time.Second)
	defer cancel()
	raw, e := updateFetch(metadataCtx, endpoint, marketLimit)
	if e != nil {
		return updateRelease{}, settings, e
	}
	var release updateRelease
	if e = json.Unmarshal(raw, &release); e != nil {
		return release, settings, e
	}
	if settings.Mode == "pinned" && (release.TagName != settings.Tag || release.ID != settings.ReleaseID) {
		return release, settings, errors.New("the pinned release identity changed; refresh and review it again")
	}
	return release, settings, nil
}

func updateValidTag(tag string) bool {
	return strings.HasPrefix(tag, "v") && marketVersion.MatchString(strings.TrimPrefix(tag, "v"))
}
