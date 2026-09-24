package main

import (
	"context"
	"errors"
	"log"
	"net/http"
	"net/url"
	"os"
	"os/signal"
	"regexp"
	"strconv"
	"strings"
	"syscall"
	"time"
)

type config struct {
	dataDir, repository, token, publicURL, apiURL      string
	maxReports, perIP, global, daily, trustedProxyHops int
}

func positiveEnv(name string, fallback int) int {
	value := os.Getenv(name)
	if value == "" {
		return fallback
	}
	n, err := strconv.Atoi(value)
	if err != nil || n < 1 {
		log.Fatalf("%s must be a positive integer", name)
	}
	return n
}

func environmentConfig() (config, error) {
	c := config{dataDir: os.Getenv("DATA_DIR"), repository: os.Getenv("GITHUB_REPOSITORY"), token: os.Getenv("GITHUB_TOKEN"), publicURL: strings.TrimRight(os.Getenv("PUBLIC_BASE_URL"), "/"), apiURL: "https://api.github.com", maxReports: positiveEnv("MAX_REPORTS", 1000), perIP: positiveEnv("REPORTS_PER_IP_HOUR", 5), global: positiveEnv("REPORTS_PER_HOUR", 100), daily: positiveEnv("REPORTS_PER_DAY", 500)}
	if c.dataDir == "" {
		c.dataDir = "/data"
	}
	if !regexp.MustCompile(`^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$`).MatchString(c.repository) {
		return c, errors.New("GITHUB_REPOSITORY is required")
	}
	u, err := url.Parse(c.publicURL)
	if err != nil || u.Scheme != "https" || u.Host == "" || u.User != nil || u.RawQuery != "" || u.Fragment != "" || u.Path != "" {
		return c, errors.New("PUBLIC_BASE_URL must be an HTTPS origin")
	}
	if s := os.Getenv("TRUSTED_PROXY_HOPS"); s != "" {
		c.trustedProxyHops, err = strconv.Atoi(s)
		if err != nil || c.trustedProxyHops < 0 || c.trustedProxyHops > 8 {
			return c, errors.New("TRUSTED_PROXY_HOPS must be 0 through 8")
		}
	}
	return c, nil
}

func main() {
	cfg, err := environmentConfig()
	if err != nil {
		log.Fatal(err)
	}
	svc, err := newService(cfg, &http.Client{Timeout: 25 * time.Second, CheckRedirect: func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }})
	if err != nil {
		log.Fatal("cannot initialize reporting storage")
	}
	port := positiveEnv("PORT", 8080)
	server := &http.Server{Addr: ":" + strconv.Itoa(port), Handler: svc, ReadHeaderTimeout: 5 * time.Second, ReadTimeout: 15 * time.Second, WriteTimeout: 35 * time.Second, IdleTimeout: 30 * time.Second, MaxHeaderBytes: 8192}
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()
	go func() {
		ticker := time.NewTicker(time.Hour)
		defer ticker.Stop()
		for {
			select {
			case <-ctx.Done():
				shutdown, cancel := context.WithTimeout(context.Background(), 30*time.Second)
				defer cancel()
				_ = server.Shutdown(shutdown)
				return
			case <-ticker.C:
				if err := svc.expire(); err != nil {
					log.Print("diagnostic retention cleanup failed")
				}
			}
		}
	}()
	log.Print("reporting service ready")
	if err := server.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatal("reporting server stopped unexpectedly")
	}
}
