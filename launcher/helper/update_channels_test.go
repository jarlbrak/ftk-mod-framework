package main

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

func channelRelease(game, tag string, id int64, preview bool) updateRelease {
	r := updateRelease{ID: id, TagName: tag, Prerelease: preview, PublishedAt: "2026-09-05T12:00:00Z", Body: "Verified release notes for " + tag}
	for _, name := range []string{"update.json", "FTKModFramework.dll", updateHelperAsset(game)} {
		r.Assets = append(r.Assets, struct {
			Name string `json:"name"`
		}{name})
	}
	return r
}
func channelFeed(stable *updateRelease, releases []updateRelease) func(context.Context, string, int64) ([]byte, error) {
	return func(ctx context.Context, s string, limit int64) ([]byte, error) {
		if e := ctx.Err(); e != nil {
			return nil, e
		}
		if s == updaterLatestURL {
			if stable == nil {
				return nil, &updateHTTPError{404}
			}
			return json.Marshal(stable)
		}
		if strings.Contains(s, "?per_page=30&page=1") {
			return json.Marshal(releases)
		}
		if strings.Contains(s, "/tags/") {
			tag := s[strings.LastIndex(s, "/")+1:]
			for _, r := range releases {
				if r.TagName == tag {
					return json.Marshal(r)
				}
			}
			return nil, &updateHTTPError{404}
		}
		return []byte(`[]`), nil
	}
}
func channelRequest(game string) updateChannelRequest {
	return updateChannelRequest{SchemaVersion: 1, OperationID: strings.Repeat("b", 32), GameDir: game}
}
func TestUpdateChannelsNumericHistoryAndPreviewStable(t *testing.T) {
	game, bundle, _, _, _ := updateFixture(t)
	updateVerifyBaseline(game, bundle)
	stable := channelRelease(game, "v0.10.0", 10, false)
	preview := channelRelease(game, "v0.9.0", 9, true)
	draft := channelRelease(game, "v9.0.0", 90, true)
	draft.Draft = true
	invalid := channelRelease(game, "1.0.0", 100, false)
	updateFetch = channelFeed(&stable, []updateRelease{preview, draft, invalid, stable})
	out, e := updateChannels("refresh", channelRequest(game))
	if e != nil || len(out.Releases) != 2 || out.Preview.Tag != stable.TagName || out.Stable.Tag != stable.TagName {
		t.Fatal("preview must follow newest stable too; strict valid tags only", out, e)
	}
	if out.SelectedMode != "stable" || out.CacheAgeSeconds < 0 {
		t.Fatal(out)
	}
}
func TestUpdateChannelsNoStableAndOfflineCache(t *testing.T) {
	game, _, _, _, _ := updateFixture(t)
	preview := channelRelease(game, "v0.1.1", 11, true)
	updateFetch = channelFeed(nil, []updateRelease{preview})
	out, e := updateChannels("refresh", channelRequest(game))
	if e != nil || out.Stable != nil || out.Preview == nil || out.Status != "online" {
		t.Fatal(out, e)
	}
	updateFetch = func(context.Context, string, int64) ([]byte, error) { return nil, errors.New("offline") }
	out, e = updateChannels("refresh", channelRequest(game))
	if e != nil || out.Status != "offline" || len(out.Releases) != 1 || out.Releases[0].Notes != preview.Body {
		t.Fatal("offline notes cache lost", out, e)
	}
}
func TestUpdateChannelsMalformedResponseKeepsCache(t *testing.T) {
	game, _, _, _, _ := updateFixture(t)
	r := channelRelease(game, "v0.1.1", 11, true)
	updateFetch = channelFeed(nil, []updateRelease{r})
	updateChannels("refresh", channelRequest(game))
	before, _ := os.ReadFile(filepath.Join(updateRoot(game), "release-history.json"))
	updateFetch = func(context.Context, string, int64) ([]byte, error) { return []byte(`{"bad":`), nil }
	out, e := updateChannels("refresh", channelRequest(game))
	after, _ := os.ReadFile(filepath.Join(updateRoot(game), "release-history.json"))
	if e != nil || out.Status != "offline" || marketHash(before) != marketHash(after) {
		t.Fatal("malformed refresh replaced cache", out, e)
	}
}
func TestUpdateChannelsSelectOnlyChangesPreferences(t *testing.T) {
	game, bundle, _, _, _ := updateFixture(t)
	updateVerifyBaseline(game, bundle)
	release := channelRelease(game, "v0.1.1", 11, true)
	updateFetch = channelFeed(nil, []updateRelease{release})
	request := channelRequest(game)
	updateChannels("refresh", request)
	before := map[string]string{}
	for _, p := range updatePaths(game) {
		before[p], _ = marketHashFile(filepath.Join(game, p))
	}
	updateRunning = func(context.Context) (bool, error) { return true, nil }
	request.Mode = "pinned"
	request.Tag = release.TagName
	request.ReleaseID = release.ID
	out, e := updateChannels("select", request)
	if e != nil || out.SelectedTag != release.TagName || out.SelectedMode != "pinned" || len(out.Releases) != 1 {
		t.Fatal(out, e)
	}
	for _, p := range updatePaths(game) {
		h, _ := marketHashFile(filepath.Join(game, p))
		if h != before[p] {
			t.Fatal("selection mutated running framework", p)
		}
	}
	settings, e := updateReadSettings(game)
	if e != nil || settings.ReleaseID != release.ID || settings.NotesSHA256 == "" {
		t.Fatal(settings, e)
	}
	if out.LauncherCompatible || !strings.Contains(out.LauncherMessage, "updated Modded launcher") {
		t.Fatal("missing legacy launcher warning", out)
	}
}
func TestUpdateChannelsStableBehindPreviewHolds(t *testing.T) {
	game, bundle, _, _, _ := updateFixture(t)
	receipt, _, _ := updateVerifyBaseline(game, bundle)
	receipt.FrameworkVersion = "0.1.1"
	marketWrite(filepath.Join(updateRoot(game), "installation.json"), receipt)
	stable := channelRelease(game, "v0.1.0", 10, false)
	updateFetch = channelFeed(&stable, []updateRelease{stable})
	out, e := updateChannels("refresh", channelRequest(game))
	if e != nil || !strings.Contains(out.Message, "no automatic downgrade") {
		t.Fatal(out, e)
	}
	candidate, _, e := updateCandidate(context.Background(), game, receipt)
	if candidate != nil || e == nil || !strings.Contains(e.Error(), "keeping the current version") {
		t.Fatal("channel silently downgraded", candidate, e)
	}
}
func TestUpdateChannelsExplicitPinDowngradeAndIdentity(t *testing.T) {
	for _, mode := range []string{"valid", "changed-id", "wrong-game", "managed"} {
		t.Run(mode, func(t *testing.T) {
			game, bundle, m, assets, _ := updateFixture(t)
			receipt, _, _ := updateVerifyBaseline(game, bundle)
			receipt.FrameworkVersion = "0.1.1"
			marketWrite(filepath.Join(updateRoot(game), "installation.json"), receipt)
			m.FrameworkVersion = "0.1.0"
			m.AutoUpdateFrom = "0.1.0"
			release := channelRelease(game, "v0.1.0", 10, true)
			marketWrite(filepath.Join(updateRoot(game), "release-history.json"), updateHistory{SchemaVersion: 1, FetchedAt: time.Now().Unix(), Releases: []updateReleaseInfo{updateInfo(release)}})
			req := channelRequest(game)
			req.Mode = "pinned"
			req.Tag = release.TagName
			req.ReleaseID = release.ID
			if _, e := updateChannels("select", req); e != nil {
				t.Fatal(e)
			}
			if mode == "changed-id" {
				release.ID = 12
			}
			if mode == "wrong-game" {
				m.GameAssemblySHA256 = []string{strings.Repeat("a", 64)}
			}
			if mode == "managed" {
				id := strings.Repeat("c", 32)
				root := filepath.Join(updateRoot(game), "marketplace")
				marketWrite(filepath.Join(root, "state.json"), marketState{SchemaVersion: 1, Current: id})
				marketWrite(filepath.Join(root, "generations", id, "lock.json"), marketLock{SchemaVersion: 1, Packages: []marketPackage{{Name: "New mod", Version: "1.0.0", FrameworkRange: "0.1.1"}}})
			}
			fallback := updateFixtureFetch(m, assets)
			updateFetch = func(ctx context.Context, s string, limit int64) ([]byte, error) {
				if strings.Contains(s, "/tags/") {
					return json.Marshal(release)
				}
				return fallback(ctx, s, limit)
			}
			candidate, _, e := updateCandidate(context.Background(), game, receipt)
			if mode == "valid" {
				if e != nil || candidate == nil || candidate.FrameworkVersion != "0.1.0" {
					t.Fatal("explicit compatible downgrade blocked by automatic-only policy", candidate, e)
				}
			} else if e == nil || candidate != nil {
				t.Fatal("pin bypassed a required gate", mode, candidate, e)
			}
		})
	}
}
func TestUpdateChannelsSelectionLockAndRestore(t *testing.T) {
	game, bundle, _, _, _ := updateFixture(t)
	request := channelRequest(game)
	request.Mode = "preview"
	unlock, e := marketAcquire(filepath.Join(updateRoot(game), "launch-update.lock"))
	if e != nil {
		t.Fatal(e)
	}
	if _, e = updateChannels("select", request); e == nil {
		unlock()
		t.Fatal("selection raced active launch")
	}
	unlock()
	if _, e = updateChannels("select", request); e != nil {
		t.Fatal(e)
	}
	oldInstaller := updateInstaller
	defer func() { updateInstaller = oldInstaller }()
	updateInstaller = func(context.Context, string, string) error { return errors.New("repair failed") }
	if _, e = prepareLaunchModeReset(game, bundle, false, false, true, true); e == nil {
		t.Fatal("expected repair failure")
	}
	settings, _ := updateReadSettings(game)
	if settings.Mode != "preview" {
		t.Fatal("failed repair cleared preference")
	}
	updateInstaller = func(context.Context, string, string) error { return nil }
	if _, e = prepareLaunchModeReset(game, bundle, false, false, true, true); e != nil {
		t.Fatal(e)
	}
	settings, _ = updateReadSettings(game)
	if settings.Mode != "stable" {
		t.Fatal("successful restore did not return to Stable")
	}
}
func TestUpdateChannelsEndpointAllowlistAndCacheSchema(t *testing.T) {
	for _, s := range []string{updateReleasesURL + "?per_page=1000&page=1", updateReleasesURL + "/tags/1.0.0", updateReleasesURL + "/tags/v1.0.0/evil", "https://api.github.com/repos/evil/repo/releases/latest"} {
		if updateAPIAllowed(s) {
			t.Fatal("unapproved API path", s)
		}
	}
	game, _, _, _, _ := updateFixture(t)
	marketWrite(filepath.Join(updateRoot(game), "update-settings.json"), map[string]interface{}{"schemaVersion": 99, "mode": "preview"})
	if _, e := updateChannels("status", channelRequest(game)); e == nil {
		t.Fatal("unsupported settings silently reset to Stable")
	}
}
func TestUpdateChannelsUnsupportedManagedLockBlocksPinAndFollow(t *testing.T) {
	for _, mode := range []string{"stable", "pinned"} {
		t.Run(mode, func(t *testing.T) {
			game, bundle, m, assets, _ := updateFixture(t)
			receipt, _, _ := updateVerifyBaseline(game, bundle)
			id := strings.Repeat("a", 32)
			root := filepath.Join(updateRoot(game), "marketplace")
			marketWrite(filepath.Join(root, "state.json"), marketState{SchemaVersion: 1, Pending: id})
			marketWrite(filepath.Join(root, "generations", id, "lock.json"), marketLock{SchemaVersion: 2, Packages: []marketPackage{{Name: "Future mod", Version: "1.0.0", FrameworkRange: ">=0.1.0 <0.2.0"}}})
			release := channelRelease(game, "v0.1.1", 11, false)
			fallback := updateFixtureFetch(m, assets)
			if mode == "pinned" {
				updateWriteSettings(game, updateSettings{SchemaVersion: 1, Mode: "pinned", Tag: release.TagName, ReleaseID: release.ID})
			}
			updateFetch = func(ctx context.Context, s string, limit int64) ([]byte, error) {
				if strings.Contains(s, "/tags/") {
					return json.Marshal(release)
				}
				return fallback(ctx, s, limit)
			}
			if candidate, _, e := updateCandidate(context.Background(), game, receipt); e == nil || candidate != nil || !strings.Contains(e.Error(), "lock schema") {
				t.Fatal("unsupported lock permitted update", mode, candidate, e)
			}
		})
	}
}
func TestUpdateChannelsRejectsRecreatedReviewedTag(t *testing.T) {
	game, _, _, _, _ := updateFixture(t)
	release := channelRelease(game, "v0.1.1", 12, true)
	marketWrite(filepath.Join(updateRoot(game), "release-history.json"), updateHistory{SchemaVersion: 1, FetchedAt: time.Now().Unix(), Releases: []updateReleaseInfo{updateInfo(release)}})
	r := channelRequest(game)
	r.Mode = "pinned"
	r.Tag = release.TagName
	r.ReleaseID = 11
	if _, e := updateChannels("select", r); e == nil {
		t.Fatal("recreated tag silently replaced reviewed release")
	}
	s, _ := updateReadSettings(game)
	if s.Mode != "stable" {
		t.Fatal("rejected pin changed preference")
	}
}
func TestUpdateChannelsPinnedDowngradeCommitsVerifiedPair(t *testing.T) {
	game, bundle, _, _, _ := updateFixture(t)
	if _, e := prepareLaunch(game, bundle, false); e != nil {
		t.Fatal(e)
	}
	current, known, e := updateVerifyBaseline(game, "")
	if e != nil || !known || current.FrameworkVersion != "0.1.1" {
		t.Fatal(current, e)
	}
	oldDLL, oldHelper := updateTestPE(1), []byte("known original helper fixture")
	gameHash, _ := marketHashFile(updateManagedAssembly(game))
	m := updateManifest{SchemaVersion: 1, FrameworkVersion: "0.1.0", HelperProtocol: 1, AutoUpdateFrom: "0.1.0", GameAssemblySHA256: []string{gameHash}, Assets: map[string]updateAsset{"FTKModFramework.dll": {marketHash(oldDLL), int64(len(oldDLL))}, updateHelperAsset(game): {marketHash(oldHelper), int64(len(oldHelper))}}}
	release := channelRelease(game, "v0.1.0", 100, true)
	updateWriteSettings(game, updateSettings{SchemaVersion: 1, Mode: "pinned", Tag: release.TagName, ReleaseID: release.ID})
	fallback := updateFixtureFetch(m, map[string][]byte{"FTKModFramework.dll": oldDLL, updateHelperAsset(game): oldHelper})
	updateFetch = func(ctx context.Context, s string, limit int64) ([]byte, error) {
		if strings.Contains(s, "/tags/") {
			return json.Marshal(release)
		}
		return fallback(ctx, s, limit)
	}
	msg, e := prepareLaunch(game, "", false)
	if e != nil || !strings.Contains(msg, "Updated FTK Mod Framework to 0.1.0") {
		t.Fatal(msg, e)
	}
	restored, known, e := updateVerifyBaseline(game, "")
	if e != nil || !known || restored.FrameworkVersion != "0.1.0" || restored.DLLSHA256 != marketHash(oldDLL) || restored.HelperSHA256 != marketHash(oldHelper) {
		t.Fatal(restored, e)
	}
	s, _ := updateReadSettings(game)
	if s.Mode != "pinned" || s.Tag != "v0.1.0" {
		t.Fatal("downgrade changed pin")
	}
}
func TestUpdateChannelsHistoryHasClearBound(t *testing.T) {
	game, _, _, _, _ := updateFixture(t)
	pages := 0
	updateFetch = func(ctx context.Context, s string, limit int64) ([]byte, error) {
		if s == updaterLatestURL {
			return nil, &updateHTTPError{404}
		}
		pages++
		releases := []updateRelease{}
		for i := 0; i < 30; i++ {
			n := (pages-1)*30 + i
			releases = append(releases, channelRelease(game, fmt.Sprintf("v0.1.%d", n), int64(n+1), true))
		}
		return json.Marshal(releases)
	}
	out, e := updateChannels("refresh", channelRequest(game))
	if e != nil || pages != 3 || len(out.Releases) != 90 || !out.MoreAvailable || out.Preview.Tag != "v0.1.89" {
		t.Fatal("history limit/order incorrect", pages, len(out.Releases), out, e)
	}
}
