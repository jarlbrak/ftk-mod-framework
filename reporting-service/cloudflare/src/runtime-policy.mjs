const repository = 'jarlbrak/ftk-mod-framework';
const gates = ['FTK_REPORTING_ACTIVATION_ENABLED', 'FTK_RECEIPTS_IMPORTED', 'FTK_RECEIPT_BRIDGE_ENABLED', 'FTK_RETENTION_ENABLED', 'FTK_ENDPOINT_CONTINUITY_VERIFIED', 'FTK_TRUSTED_INGRESS_VERIFIED', 'FTK_FRESH_CONFIG_VERIFIED'];
export const activated = env => gates.every(key => env[key] === 'true');
export const unavailable = () => new Response(null, { status: 503, headers: { 'Cache-Control': 'no-store' } });
export function canonical(ctx, namespace) {
  return Boolean(namespace && ctx.id.equals(namespace.idFromName('canonical')));
}

// This is a new configuration envelope, never a copied Railway environment.
// Only these values can enter the Go process; GitHub authorization stays outside.
export function freshConfiguration(env) {
  const value = JSON.parse(env.FTK_FRESH_RUNTIME_CONFIG);
  const keys = ['version', 'githubToken', 'publicOrigin', 'maxReports', 'perIPHour', 'perHour', 'perDay'];
  if (!value || Array.isArray(value) || Object.keys(value).length !== keys.length || keys.some(key => !Object.hasOwn(value, key)) || value.version !== 'fresh-v1') throw new Error('invalid fresh configuration');
  if (typeof value.githubToken !== 'string' || !/^[A-Za-z0-9_]{20,512}$/.test(value.githubToken)) throw new Error('invalid authorization');
  const origin = new URL(value.publicOrigin);
  if (origin.protocol !== 'https:' || origin.origin !== value.publicOrigin || origin.username || origin.password) throw new Error('invalid public origin');
  for (const key of ['maxReports', 'perIPHour', 'perHour', 'perDay']) {
    if (!Number.isSafeInteger(value[key]) || value[key] < 1 || value[key] > 100000) throw new Error('invalid bound');
  }
  if (value.maxReports > 2000 || String(value.maxReports) !== env.MAX_REPORTS) throw new Error('receipt capacity mismatch');
  return { githubToken: value.githubToken, publicOrigin: value.publicOrigin, containerEnv: {
    PORT: '8080', DATA_DIR: '/data', GITHUB_REPOSITORY: repository,
    GITHUB_TOKEN: 'outbound-proxy-injected', PUBLIC_BASE_URL: value.publicOrigin,
    MAX_REPORTS: String(value.maxReports), REPORTS_PER_IP_HOUR: String(value.perIPHour),
    REPORTS_PER_HOUR: String(value.perHour), REPORTS_PER_DAY: String(value.perDay),
    TRUSTED_PROXY_HOPS: '1', RECEIPT_STORE_BRIDGE_URL: 'http://receipts.internal',
    FTK_RECEIPT_BRIDGE_ENABLED: 'true',
    SSL_CERT_FILE: '/etc/cloudflare/certs/cloudflare-containers-ca.crt',
  } };
}
export function clientAddress(value) {
  if (typeof value !== 'string' || value.length > 45 || /[\s%\[\],/]/.test(value)) throw new Error('invalid client address');
  if (/^(0|[1-9][0-9]{0,2})(\.(0|[1-9][0-9]{0,2})){3}$/.test(value) && value.split('.').every(part => Number(part) <= 255)) return value;
  if (value.includes(':')) {
    const url = new URL(`http://[${value}]/`);
    return url.hostname.slice(1, -1);
  }
  throw new Error('invalid client address');
}
export function ingressRequest(request, address) {
  const ip = clientAddress(address);
  const url = new URL(request.url);
  const get = request.method === 'GET' && (['/', '/privacy', '/healthz'].includes(url.pathname) || /^\/diagnostics\/[a-f0-9]{32}\.(json|log)$/.test(url.pathname));
  const post = request.method === 'POST' && url.pathname === '/v1/reports';
  if ((!get && !post) || url.search || url.hash || request.headers.has('Upgrade')) throw new Error('unsupported ingress');
  const headers = new Headers({ 'X-Forwarded-For': ip });
  if (post && request.headers.has('Content-Type')) headers.set('Content-Type', request.headers.get('Content-Type'));
  return new Request(`http://reporting.internal${url.pathname}`, { method: request.method, headers, body: post ? request.body : null, redirect: 'manual', duplex: 'half' });
}

export async function receiptEgress(request, env) {
  if (!activated(env)) return unavailable();
  const url = new URL(request.url);
  if (url.origin !== 'http://receipts.internal' || url.username || url.password || url.hash || url.search || !/^\/receipts(?:\/[a-f0-9]{32})?$/.test(url.pathname) || !['GET', 'PUT'].includes(request.method)) return new Response(null, { status: 403 });
  return env.RECEIPT_AUTHORITY.getByName('canonical').fetch(request);
}
export async function githubEgress(request, env, outbound = fetch) {
  if (!activated(env)) return unavailable();
  const url = new URL(request.url);
  if (url.origin !== 'https://api.github.com' || url.username || url.password || url.hash || url.pathname !== `/repos/${repository}/issues`) return new Response(null, { status: 403 });
  if (request.method === 'POST') {
    if (url.search) return new Response(null, { status: 403 });
  } else if (request.method === 'GET') {
    const expected = { state: 'all', sort: 'created', direction: 'asc', per_page: '100' };
    const entries = [...url.searchParams];
    if (entries.length !== 6 || new Set(entries.map(([key]) => key)).size !== 6 || Object.entries(expected).some(([key, value]) => url.searchParams.get(key) !== value) || !/^(?:[1-9]|1[0-9]|20)$/.test(url.searchParams.get('page') || '') || !/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$/.test(url.searchParams.get('since') || '') || !Number.isFinite(Date.parse(url.searchParams.get('since')))) return new Response(null, { status: 403 });
  } else return new Response(null, { status: 403 });
  const config = freshConfiguration(env);
  const headers = new Headers({ Authorization: `Bearer ${config.githubToken}`, Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28', 'Content-Type': 'application/json', 'User-Agent': 'FTK-Reporting-Service' });
  // Never replay issue creation, including a redirect or an unknown transport
  // outcome. Return definitive GitHub errors intact for the Go receipt protocol.
  return outbound(new Request(url, { method: request.method, headers, body: request.method === 'POST' ? request.body : null, redirect: 'manual', duplex: 'half' }));
}

// Native API only: no inherited start/exec RPC can bypass the canonical guard.
export async function runtimeForward(ctx, env, request, address) {
  if (!canonical(ctx, env.REPORTING_RUNTIME)) return new Response(null, { status: 404 });
  if (!activated(env)) return unavailable();
  try {
    const config = freshConfiguration(env);
    const incoming = ingressRequest(request, address);
    const container = ctx.container;
    const image = container?.images?.reporting;
    if (!container || typeof image !== 'string' || !/@sha256:[a-f0-9]{64}$/.test(image) || ['interceptOutboundHttp', 'interceptOutboundHttps', 'setInactivityTimeout', 'start', 'getTcpPort'].some(key => typeof container[key] !== 'function')) return unavailable();
    await ctx.blockConcurrencyWhile(async () => {
      const bytes = new TextEncoder().encode(JSON.stringify({ image, env: config.containerEnv }));
      const fingerprint = [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))].map(value => value.toString(16).padStart(2, '0')).join('');
      if (container.running) {
        if (await ctx.storage.get('runtime-configuration') !== fingerprint) throw new Error('running configuration requires reviewed restart');
      }
      // Refresh intercepts even for a warm container after a Worker deployment.
      // Registration precedes boot and network access is off on every start.
      await container.interceptOutboundHttp('receipts.internal', ctx.exports.ReceiptEgress());
      await container.interceptOutboundHttps('api.github.com', ctx.exports.GithubEgress());
      if (!container.running) {
        container.start({ image, enableInternet: false, env: config.containerEnv });
        await ctx.storage.put('runtime-configuration', fingerprint);
      }
      await container.setInactivityTimeout(10 * 60 * 1000);
    });
    const port = container.getTcpPort(8080);
    // Readiness probes are safe to repeat; the actual report is forwarded once.
    let ready = false;
    for (let attempt = 0; attempt < 20; attempt++) {
      try {
        const probe = await port.fetch('http://reporting.internal/healthz', { signal: AbortSignal.timeout(500) });
        await probe.body?.cancel();
        if (probe.status === 200) { ready = true; break; }
      } catch { /* The process may still be starting. */ }
      await new Promise(resolve => setTimeout(resolve, 100));
    }
    if (!ready) return unavailable();
    return await port.fetch(incoming);
  } catch { return unavailable(); }
}
