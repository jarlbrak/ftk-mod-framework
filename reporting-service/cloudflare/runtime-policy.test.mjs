import test from 'node:test';
import assert from 'node:assert/strict';
import { activated, freshConfiguration, clientAddress, ingressRequest, receiptEgress, githubEgress, runtimeForward } from './src/runtime-policy.mjs';
const flags = ['FTK_REPORTING_ACTIVATION_ENABLED', 'FTK_RECEIPTS_IMPORTED', 'FTK_RECEIPT_BRIDGE_ENABLED', 'FTK_RETENTION_ENABLED', 'FTK_ENDPOINT_CONTINUITY_VERIFIED', 'FTK_TRUSTED_INGRESS_VERIFIED', 'FTK_FRESH_CONFIG_VERIFIED'];
const values = { version: 'fresh-v1', githubToken: 'synthetic_token_only_for_local_tests', publicOrigin: 'https://reports.example', maxReports: 1000, perIPHour: 5, perHour: 100, perDay: 500 };
const environment = () => ({ ...Object.fromEntries(flags.map(name => [name, 'true'])), MAX_REPORTS: '1000', FTK_FRESH_RUNTIME_CONFIG: JSON.stringify(values), REPORTING_RUNTIME: { idFromName: name => name } });
const issueURL = 'https://api.github.com/repos/jarlbrak/ftk-mod-framework/issues';
function context() {
  const events = [], data = new Map();
  const container = {
    running: false, images: { reporting: `registry.example/reporting@sha256:${'a'.repeat(64)}` },
    async interceptOutboundHttp(host) { events.push(['http', host]); },
    async interceptOutboundHttps(host) { events.push(['https', host]); },
    start(options) { events.push(['start', options]); this.running = true; },
    async setInactivityTimeout(value) { events.push(['timeout', value]); },
    getTcpPort(value) { events.push(['port', value]); return { fetch: async request => {
      const url = new URL(typeof request === 'string' ? request : request.url);
      events.push([url.pathname === '/healthz' ? 'probe' : 'forward', request]);
      return new Response(null, { status: 200 });
    } }; },
  };
  return { events, container, id: { equals: name => name === 'canonical' }, storage: { get: async key => data.get(key), put: async (key, value) => { data.set(key, value); } }, exports: { ReceiptEgress: () => 'receipt-proxy', GithubEgress: () => 'github-proxy' }, blockConcurrencyWhile: run => run() };
}
const report = () => new Request('https://reports.example/v1/reports', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'untrusted', Cookie: 'untrusted', 'X-Forwarded-For': '198.51.100.99', Forwarded: 'for=198.51.100.99', 'CF-Connecting-IP': '198.51.100.99' }, body: '{}' });

test('every activation attestation fails closed before runtime access', async () => {
  for (const flag of flags) {
    const env = environment(); env[flag] = 'false';
    const ctx = context();
    assert.equal(activated(env), false);
    assert.equal((await runtimeForward(ctx, env, report(), '203.0.113.7')).status, 503);
    assert.deepEqual(ctx.events, []);
  }
});
test('fresh envelope rejects unknown variables and cannot override protected process values', () => {
  const env = environment();
  for (const bad of [{ ...values, HTTP_PROXY: 'https://example.invalid' }, { ...values, version: 'copied' }, { ...values, maxReports: 999 }, { ...values, githubToken: 'bad\r\nheader' }, { ...values, publicOrigin: 'https://reports.example/path' }]) {
    assert.throws(() => freshConfiguration({ ...env, FTK_FRESH_RUNTIME_CONFIG: JSON.stringify(bad) }));
  }
  const config = freshConfiguration(env);
  assert.equal(config.containerEnv.TRUSTED_PROXY_HOPS, '1');
  assert.equal(config.containerEnv.GITHUB_REPOSITORY, 'jarlbrak/ftk-mod-framework');
  assert.equal(config.containerEnv.SSL_CERT_FILE, '/etc/cloudflare/certs/cloudflare-containers-ca.crt');
  assert.equal(Object.values(config.containerEnv).includes(values.githubToken), false);
});
test('trusted RPC address replaces every forwarded header and credentials', async () => {
  const req = ingressRequest(report(), '2001:db8::7');
  assert.deepEqual([...req.headers.keys()].sort(), ['content-type', 'x-forwarded-for']);
  assert.equal(req.headers.get('X-Forwarded-For'), '2001:db8::7');
  assert.equal(req.url, 'http://reporting.internal/v1/reports');
  assert.equal(await req.text(), '{}');
  for (const bad of ['203.0.113.1, 198.51.100.1', '1.2.3.256', '1.02.3.4', '[::1]', '::1%eth0', 'example.com', '', '::1\n']) assert.throws(() => clientAddress(bad));
});
test('canonical runtime starts only a pinned image after exact intercepts and forwards once', async () => {
  const ctx = context(), env = environment();
  assert.equal((await runtimeForward(ctx, env, report(), '203.0.113.7')).status, 200);
  const kinds = ctx.events.map(item => item[0]);
  assert.deepEqual(kinds.slice(0, 3), ['http', 'https', 'start']);
  assert.deepEqual(ctx.events[0], ['http', 'receipts.internal']);
  assert.deepEqual(ctx.events[1], ['https', 'api.github.com']);
  const start = ctx.events.find(([name]) => name === 'start')[1];
  assert.equal(start.enableInternet, false);
  assert.equal(start.env.GITHUB_TOKEN, 'outbound-proxy-injected');
  assert.equal(ctx.events.filter(([name]) => name === 'forward').length, 1);
  ctx.events.length = 0;
  assert.equal((await runtimeForward(ctx, env, report(), '203.0.113.7')).status, 200);
  assert.equal(ctx.events.some(([name]) => name === 'start'), false);
  assert.deepEqual(ctx.events.slice(0, 2).map(item => item[0]), ['http', 'https']);
});
test('alternate identity, missing native binding, unpinned image and unknown warm state never boot', async () => {
  const env = environment();
  const alternate = context(); alternate.id.equals = () => false;
  assert.equal((await runtimeForward(alternate, env, report(), '203.0.113.7')).status, 404);
  assert.deepEqual(alternate.events, []);
  for (const mutate of [ctx => { ctx.container = undefined; }, ctx => { ctx.container.images.reporting = 'registry.example/reporting:latest'; }, ctx => { ctx.container.running = true; }, ctx => { ctx.container.interceptOutboundHttps = undefined; }]) {
    const ctx = context(); mutate(ctx);
    assert.equal((await runtimeForward(ctx, env, report(), '203.0.113.7')).status, 503);
    assert.deepEqual(ctx.events, []);
  }
});
test('proxy exceptions never replay a report and readiness precedes the only forward', async () => {
  const ctx = context(), env = environment(); let reports = 0;
  ctx.container.getTcpPort = () => ({ fetch: async request => {
    if (typeof request === 'string') return new Response(null, { status: 200 });
    reports++; throw new Error('synthetic ambiguous transport');
  } });
  assert.equal((await runtimeForward(ctx, env, report(), '203.0.113.7')).status, 503);
  assert.equal(reports, 1);
});
test('receipts interception cannot escape canonical authority or access another host', async () => {
  const env = environment(); let calls = 0;
  env.RECEIPT_AUTHORITY = { getByName(name) { assert.equal(name, 'canonical'); return { fetch: async () => { calls++; return new Response(null, { status: 200 }); } }; } };
  assert.equal((await receiptEgress(new Request('http://receipts.internal/receipts'), env)).status, 200);
  for (const url of ['https://receipts.internal/receipts', 'http://receipts.internal:8080/receipts', 'http://example.invalid/receipts', 'http://receipts.internal/receipts?x=y']) assert.equal((await receiptEgress(new Request(url), env)).status, 403);
  assert.equal(calls, 1);
});
test('GitHub egress restricts repository and methods, replaces authorization, and never follows redirects', async () => {
  const env = environment(); let calls = 0;
  const outbound = async req => {
    calls++;
    assert.equal(req.redirect, 'manual');
    assert.equal(req.headers.get('Authorization'), `Bearer ${values.githubToken}`);
    assert.equal(req.headers.get('Cookie'), null);
    assert.equal(req.headers.get('X-Forwarded-For'), null);
    return new Response(null, { status: 302, headers: { Location: 'https://example.invalid' } });
  };
  assert.equal((await githubEgress(new Request(issueURL, { method: 'POST', headers: { Authorization: 'untrusted', Cookie: 'untrusted', 'X-Forwarded-For': '203.0.113.7' }, body: '{}' }), env, outbound)).status, 302);
  const query = '?state=all&sort=created&direction=asc&since=2026-10-22T00%3A00%3A00Z&per_page=100&page=1';
  assert.equal((await githubEgress(new Request(issueURL + query), env, outbound)).status, 302);
  for (const [url, method] of [[issueURL, 'DELETE'], [issueURL + '?x=y', 'POST'], [issueURL.replace('jarlbrak', 'other'), 'POST'], [issueURL.replace('https:', 'http:'), 'POST'], [issueURL + query + '&page=2', 'GET']]) assert.equal((await githubEgress(new Request(url, { method }), env, outbound)).status, 403);
  assert.equal(calls, 2);
});
test('definite GitHub errors pass through and unknown creation outcomes remain single attempts', async () => {
  const env = environment();
  for (const status of [400, 401, 403, 404, 415, 422, 429, 500]) {
    assert.equal((await githubEgress(new Request(issueURL, { method: 'POST', body: '{}' }), env, async () => new Response(null, { status }))).status, status);
  }
  let calls = 0;
  await assert.rejects(githubEgress(new Request(issueURL, { method: 'POST', body: '{}' }), env, async () => { calls++; throw new Error('synthetic unknown outcome'); }));
  assert.equal(calls, 1);
});
