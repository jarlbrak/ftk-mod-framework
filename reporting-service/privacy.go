package main

import (
	"regexp"
	"strings"
	"unicode/utf8"
)

var redactPatterns = []*regexp.Regexp{
	regexp.MustCompile(`(?i)(?:authorization\s*[:=]\s*|bearer\s+|(?:password|passwd|token|secret|api[_-]?key)\s*[:=]\s*)[^\s,;"']+`),
	regexp.MustCompile(`\b(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+)\b`),
	regexp.MustCompile(`(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b`),
	regexp.MustCompile(`(?i)(?:/Users/|/home/|[A-Z]:\\Users\\)[^\s/\\"']+`),
	regexp.MustCompile(`\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b`),
	regexp.MustCompile(`\b7656119[0-9]{10}\b`),
	regexp.MustCompile(`(?i)https?://[^\s"<>]+`),
}

func redact(text string) string {
	for _, pattern := range redactPatterns {
		text = pattern.ReplaceAllString(text, "[redacted]")
	}
	return strings.Map(func(r rune) rune {
		if r < 32 && r != '\n' && r != '\t' && r != '\r' {
			return -1
		}
		return r
	}, text)
}

func sanitize(value interface{}) interface{} {
	switch v := value.(type) {
	case string:
		return redact(v)
	case []interface{}:
		for i := range v {
			v[i] = sanitize(v[i])
		}
		return v
	case map[string]interface{}:
		filtered := make(map[string]interface{}, len(v))
		for key, child := range v {
			lower := strings.ToLower(strings.ReplaceAll(strings.ReplaceAll(key, "_", ""), "-", ""))
			blocked := false
			for _, fragment := range []string{"password", "passwd", "token", "secret", "apikey", "authorization", "cookie", "email", "username", "playername", "steamid", "savegame", "savedata", "screenshot", "environmentvariables", "ipaddress"} {
				if strings.Contains(lower, fragment) {
					blocked = true
					break
				}
			}
			if !blocked {
				filtered[redact(key)] = sanitize(child)
			}
		}
		return filtered
	default:
		return value
	}
}

func validPrefix(text string, n int) string {
	if len(text) <= n {
		return text
	}
	for n > 0 && !utf8.RuneStart(text[n]) {
		n--
	}
	return text[:n]
}

const privacyHTML = `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>FTK bug report disclosure</title><body><main><h1>What happens when you send a bug report</h1><p>Nothing is uploaded until you choose Send report. Detecting an error or an unexpected previous exit only offers to report it. You can dismiss the offer.</p><p>Your description is sent to the FTK Mod Framework reporting service hosted on Railway, which creates a public GitHub issue. You do not need a GitHub account. The service uses its own repository credential; no GitHub credential is stored in the game.</p><p>With Include diagnostics selected, the report also sends a filtered recent game/framework process log dump, including informational messages, warnings and errors, plus game and framework versions, installed mods, and diagnostic session context. Log capture begins when reporting initializes and keeps at most 128 KiB of UTF-8 text per session. For a previous unexpected exit, this may include preserved diagnostics from that session; the latest entries can be lost in an abrupt exit. Older reports without recorded logs cannot recover them later. These diagnostics are public: excerpts appear in the issue and download links expose the sanitized diagnostic JSON and readable .log dump to anyone with a link.</p><p>You can turn off Include diagnostics before sending. Save files, screenshots, and account credentials are not intentionally collected. Automatic filtering removes known sensitive fields and patterns such as common credentials, home-folder names, email addresses, and Steam IDs. Filtering cannot guarantee removal of every personal detail a mod writes to a log, or details you type yourself. Review the optional diagnostic preview and avoid personal information in your description.</p><p>The service removes report descriptions and diagnostic bundles from its storage after 30 days (cleanup runs hourly). GitHub issue text and excerpts remain public until removed by a repository maintainer. Other people may retain copies. Minimal submission receipts (random report ID, content hash, submission time, issue number and URL) remain to prevent duplicate issues.</p><p>Network addresses are used transiently for abuse limits and are not written to report bundles. Railway and GitHub may process connection information under their own policies. The service does not log request bodies, credentials, or player network addresses.</p><p>To request removal, contact a repository maintainer through the report's GitHub issue. This community framework reporting service is not operated by the game's original developer.</p></main></body></html>`

func validTail(text string, n int) string {
	if len(text) <= n {
		return text
	}
	start := len(text) - n
	for start < len(text) && !utf8.RuneStart(text[start]) {
		start++
	}
	return "[earlier lines omitted]\n" + text[start:]
}
