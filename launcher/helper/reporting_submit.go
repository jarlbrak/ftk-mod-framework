package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"flag"
	"io"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"regexp"
	"strconv"
	"time"
)

const reportingRequestLimit = 2 * 1024 * 1024
const reportingResponseLimit = 64 * 1024

var reportingID = regexp.MustCompile(`^[a-f0-9]{32}$`)

type reportingResult struct {
	SchemaVersion int    `json:"schemaVersion"`
	Status        string `json:"status"`
	ReportID      string `json:"reportId,omitempty"`
	IssueNumber   int    `json:"issueNumber,omitempty"`
	IssueURL      string `json:"issueUrl,omitempty"`
	Error         string `json:"error,omitempty"`
	HTTPStatus    int    `json:"httpStatus,omitempty"`
}

func reportingSubmitMain(args []string) error {
	flags := flag.NewFlagSet("report-submit", flag.ContinueOnError)
	flags.SetOutput(io.Discard)
	request := flags.String("request", "", "local JSON request")
	result := flags.String("result", "", "local JSON result")
	endpoint := flags.String("endpoint", "", "HTTPS reporting endpoint")
	if flags.Parse(args) != nil || flags.NArg() != 0 {
		return errors.New("invalid report-submit arguments")
	}
	client := &http.Client{Timeout: 30 * time.Second, CheckRedirect: func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }}
	return reportingSubmit(*request, *result, *endpoint, client)
}

func reportingPaths(request, result string) error {
	if !filepath.IsAbs(request) || !filepath.IsAbs(result) || filepath.Clean(request) != request || filepath.Clean(result) != result || request == result || filepath.Dir(request) != filepath.Dir(result) {
		return errors.New("report paths must be distinct absolute files in one private directory")
	}
	dir := filepath.Dir(request)
	root := ""
	for current := dir; current != filepath.Dir(current); current = filepath.Dir(current) {
		if filepath.Base(current) == "ReportingDelivery" && filepath.Base(filepath.Dir(current)) == "BepInEx" {
			root = current
			break
		}
	}
	if root == "" {
		return errors.New("report paths must be under BepInEx/ReportingDelivery")
	}
	for current := dir; ; current = filepath.Dir(current) {
		info, err := os.Lstat(current)
		if err != nil || !info.IsDir() || info.Mode()&os.ModeSymlink != 0 {
			return errors.New("report directory must exist and cannot be a symlink")
		}
		if current == filepath.Dir(root) {
			break
		}
	}
	for _, path := range []string{request, result} {
		info, err := os.Lstat(path)
		if os.IsNotExist(err) && path == result {
			continue
		}
		if err != nil || !info.Mode().IsRegular() || info.Mode()&os.ModeSymlink != 0 {
			return errors.New("report file must be regular and cannot be a symlink")
		}
	}
	return nil
}

func reportingEndpoint(endpoint string) bool {
	u, err := url.Parse(endpoint)
	return err == nil && u.Scheme == "https" && u.Host != "" && u.User == nil && u.RawQuery == "" && u.Fragment == "" && u.Path == "/v1/reports" && u.RawPath == ""
}

func reportingSubmit(request, result, endpoint string, client *http.Client) error {
	if err := reportingPaths(request, result); err != nil {
		return err
	}
	if !reportingEndpoint(endpoint) {
		return errors.New("report endpoint must be an HTTPS /v1/reports URL")
	}
	f, err := os.Open(request)
	if err != nil {
		return errors.New("cannot open report request")
	}
	data, err := io.ReadAll(io.LimitReader(f, reportingRequestLimit+1))
	_ = f.Close()
	if err != nil || len(data) > reportingRequestLimit {
		return errors.New("report request exceeds limit or is unreadable")
	}
	var envelope struct {
		SchemaVersion int    `json:"schemaVersion"`
		ReportID      string `json:"reportId"`
	}
	if marketUniqueJSON(data) != nil || json.Unmarshal(data, &envelope) != nil || envelope.SchemaVersion != 1 || !reportingID.MatchString(envelope.ReportID) {
		return errors.New("invalid report request")
	}
	out := reportingResult{SchemaVersion: 1, Status: "error", ReportID: envelope.ReportID, Error: "transport_error"}
	req, err := http.NewRequest("POST", endpoint, bytes.NewReader(data))
	if err == nil {
		req.Header.Set("Content-Type", "application/json")
		req.Header.Set("User-Agent", "FTKModFramework-Reporting")
		// Retain the caller's trusted transport for tests while preventing redirects
		// even if a future caller supplies a more permissive HTTP client.
		bounded := *client
		bounded.Timeout = 30 * time.Second
		bounded.CheckRedirect = func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }
		response, sendErr := bounded.Do(req)
		if sendErr == nil {
			out.HTTPStatus = response.StatusCode
			raw, readErr := io.ReadAll(io.LimitReader(response.Body, reportingResponseLimit+1))
			_ = response.Body.Close()
			var remote reportingResult
			if readErr == nil && len(raw) <= reportingResponseLimit && marketUniqueJSON(raw) == nil && json.Unmarshal(raw, &remote) == nil && remote.SchemaVersion == 1 {
				if (response.StatusCode == 200 || response.StatusCode == 201) && remote.Status == "submitted" && remote.ReportID == envelope.ReportID && remote.IssueNumber > 0 && remote.IssueURL == "https://github.com/jarlbrak/ftk-mod-framework/issues/"+strconv.Itoa(remote.IssueNumber) {
					out = remote
					out.HTTPStatus = response.StatusCode
					out.Error = ""
				} else if response.StatusCode >= 400 && remote.Status == "error" && reportingServerError(remote.Error) {
					out.Error = remote.Error
				} else {
					out.Error = "invalid_response"
				}
			} else {
				out.Error = "invalid_response"
			}
		}
	}
	if err := reportingPaths(request, result); err != nil {
		return err
	}
	if err := reportingWrite(result, out); err != nil {
		return errors.New("cannot write report result")
	}
	if out.Status != "submitted" {
		return errors.New("report submission did not complete; see result")
	}
	return nil
}

func reportingServerError(code string) bool {
	switch code {
	case "invalid_report", "report_conflict", "payload_too_large", "unsupported_media_type", "rate_limited", "submission_pending", "service_unavailable", "capacity_reached":
		return true
	}
	return false
}

func reportingWrite(path string, value reportingResult) error {
	data, err := json.Marshal(value)
	if err != nil {
		return err
	}
	f, err := os.CreateTemp(filepath.Dir(path), ".report-result-")
	if err != nil {
		return err
	}
	defer os.Remove(f.Name())
	if err = f.Chmod(0600); err == nil {
		_, err = f.Write(data)
	}
	if err == nil {
		err = f.Sync()
	}
	closeErr := f.Close()
	if err == nil {
		err = closeErr
	}
	if err != nil {
		return err
	}
	return marketReplace(f.Name(), path)
}
