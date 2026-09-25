import process from "node:process";
import { defineRailway, github, preserve, project, service, volume } from "railway/iac";

export default defineRailway((ctx) => {
  if (!ctx.projectName) throw new Error("Link the dedicated reporting project before planning.");
  const additionalVariables = Object.fromEntries(
    (process.env.FTK_REPORTING_PRESERVE_VARIABLES || "").split(",").filter(Boolean).map((name) => {
      if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(name)) throw new Error("Invalid preserved variable name.");
      return [name, preserve()];
    }),
  );

  const data = volume("reporting-api-volume", {
    region: "us-west2",
    sizeMB: 5000,
    allowOnlineResize: true,
    alerts: { usage: { "80": {}, "95": {}, "100": {} } },
  });
  const api = service("reporting-api", {
    source: github("jarlbrak/ftk-mod-framework", {
      branch: process.env.FTK_REPORTING_SOURCE_BRANCH || "master",
      rootDirectory: "/reporting-service",
    }),
    build: { builder: "DOCKERFILE", dockerfilePath: "Dockerfile" },
    replicas: { "us-west2": 1 },
    healthcheck: "/healthz",
    healthcheckTimeout: 30,
    deploy: {
      sleepApplication: false,
      restartPolicyType: "ON_FAILURE",
      restartPolicyMaxRetries: 3,
    },
    volumeMounts: { "/data": data },
    env: {
      ...additionalVariables,
      DATA_DIR: "/data",
      PORT: "8080",
      GITHUB_REPOSITORY: "jarlbrak/ftk-mod-framework",
      GITHUB_TOKEN: preserve(),
      PUBLIC_BASE_URL: preserve(),
      TRUSTED_PROXY_HOPS: preserve(),
      MAX_REPORTS: preserve(),
      REPORTS_PER_IP_HOUR: preserve(),
      REPORTS_PER_HOUR: preserve(),
      REPORTS_PER_DAY: preserve(),
    },
  });

  return project(ctx.projectName, { resources: [api, data] });
});
