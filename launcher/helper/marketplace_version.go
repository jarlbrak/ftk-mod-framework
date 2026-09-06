package main

import (
	"fmt"
	"strconv"
	"strings"
)

// Matches System.Version's three int32 components, with canonical decimal text.
// Major zero follows the same rule as every other major version.
func marketFrameworkVersion(s string) ([3]int64, bool) {
	var result [3]int64
	if !marketVersion.MatchString(s) {
		return result, false
	}
	parts := strings.Split(s, ".")
	for i, p := range parts {
		n, e := strconv.ParseInt(p, 10, 32)
		if e != nil {
			return result, false
		}
		result[i] = n
	}
	return result, true
}
func marketFrameworkRange(confirmed string) (string, bool) {
	v, ok := marketFrameworkVersion(confirmed)
	if !ok {
		return "", false
	}
	return fmt.Sprintf(">=%s <%d.0.0", confirmed, v[0]+1), true
}
func marketConfirmedCompatibility(confirmed, running string) string {
	author, valid := marketFrameworkVersion(confirmed)
	if !valid {
		return "The mod author must declare a valid frameworkVersion (X.Y.Z)."
	}
	client, valid := marketFrameworkVersion(running)
	if !valid {
		return "The running framework version is invalid."
	}
	if author[0] != client[0] {
		return "The mod author must confirm compatibility with framework major " + strconv.FormatInt(client[0], 10) + " in an updated manifest."
	}
	if marketCompare(running, confirmed) < 0 {
		return "This mod requires framework " + confirmed + " or newer within the same major version."
	}
	return ""
}
func marketPackageFrameworkCompatibility(p marketPackage, running string) string {
	if p.FrameworkVersion != "" {
		canonical, valid := marketFrameworkRange(p.FrameworkVersion)
		if !valid {
			return "The mod author must declare a valid frameworkVersion (X.Y.Z)."
		}
		if p.FrameworkRange != "" && p.FrameworkRange != canonical {
			return "frameworkRange conflicts with the author's confirmed frameworkVersion."
		}
		return marketConfirmedCompatibility(p.FrameworkVersion, running)
	}
	// A generation's old framework version is a migration boundary, never an
	// inferred author declaration. Empty generations have no packages to gate.
	anchor, anchorValid := marketFrameworkVersion(p.legacyFrameworkAnchor)
	client, clientValid := marketFrameworkVersion(running)
	if !anchorValid || !clientValid {
		return "Legacy mod compatibility has no valid recorded framework anchor; an updated author manifest is required."
	}
	if anchor[0] != client[0] {
		return "Legacy mod metadata cannot authorize a new framework major; an updated author manifest is required."
	}
	if !marketRange(p.FrameworkRange, running) {
		return "Legacy mod framework range " + p.FrameworkRange + " does not support this version."
	}
	return ""
}

func marketStrictVersion(s string) bool { _, valid := marketFrameworkVersion(s); return valid }
