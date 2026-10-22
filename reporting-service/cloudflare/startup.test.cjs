const test = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { spawnSync } = require('node:child_process');
const { Miniflare, convertV4MiniflareOptions } = require('miniflare');

// Exercise the emitted artifact in workerd: a syntax check and dry-run alone do
// not catch class-name rewriting that breaks native DurableObject subclasses.
test('disabled emitted Worker starts in workerd and exposes no public handler', { timeout: 30000 }, async () => {
  const output = join(__dirname, 'node_modules/.cache/startup-test-build');
  const build = spawnSync(process.execPath, [join(__dirname, 'node_modules/wrangler/bin/wrangler.js'), 'deploy', '--dry-run', '--outdir', output], { cwd: __dirname, encoding: 'utf8', timeout: 20000 });
  assert.equal(build.status, 0, build.stderr || build.stdout);
  const config = JSON.parse(readFileSync(join(__dirname, 'wrangler.jsonc'), 'utf8'));
  assert.equal(config.keep_names, false);
  assert.equal(config.workers_dev, false);
  assert.deepEqual(config.routes, []);
  for (const [name, value] of Object.entries(config.vars)) {
    if (name.startsWith('FTK_')) assert.equal(value, 'false');
  }
  const fresh = { version: 'fresh-v1', githubToken: 'synthetic_token_only_for_local_tests', publicOrigin: 'https://reports.example', maxReports: 1000, perIPHour: 5, perHour: 100, perDay: 500 };
  const enabled = { ...config.vars, FTK_FRESH_RUNTIME_CONFIG: JSON.stringify(fresh) };
  for (const name of Object.keys(enabled)) if (name.startsWith('FTK_') && name !== 'FTK_FRESH_RUNTIME_CONFIG') enabled[name] = 'true';
  const outbound = [];
  const worker = (name, bindings) => ({ name, modules: true, scriptPath: join(output, 'index.js'), compatibilityDate: config.compatibility_date,
    durableObjects: { RECEIPT_AUTHORITY: 'ReceiptAuthority', REPORTING_RUNTIME: 'ReportingRuntime' }, bindings,
    outboundService: async request => { outbound.push({ url: request.url, headers: Object.fromEntries(request.headers), redirect: request.redirect }); return new Response(null, { status: 422 }); },
  });
  const probe = {
    name: 'probe', modules: true, compatibilityDate: config.compatibility_date,
    serviceBindings: {
      DISABLED: { name: 'disabled', entrypoint: 'ReportingController' },
      ENABLED: { name: 'enabled', entrypoint: 'ReportingController' },
      GH: { name: 'enabled', entrypoint: 'GithubEgress' },
      RECEIPTS: { name: 'disabled', entrypoint: 'ReceiptEgress' },
    },
    durableObjects: { RUNTIME: { className: 'ReportingRuntime', scriptName: 'enabled' } },
    script: `export default { async fetch(request, env) {
      const action = new URL(request.url).pathname;
      const req = () => new Request('https://reports.example/v1/reports', { method: 'POST', headers: { 'X-Forwarded-For': '198.51.100.9' }, body: '{}' });
      if (action === '/private-http') return env.ENABLED.fetch(request);
      if (action === '/disabled-rpc') return env.DISABLED.forward(req(), '203.0.113.7');
      if (action === '/enabled-rpc-no-container') return env.ENABLED.forward(req(), '203.0.113.7');
      if (action === '/invalid-ip') return env.ENABLED.forward(req(), '203.0.113.7,198.51.100.9');
      if (action === '/alternate') return env.RUNTIME.getByName('alternate').forward(req(), '203.0.113.7');
      if (action === '/runtime-http') return env.RUNTIME.getByName('canonical').fetch(request);
      if (action === '/no-start-rpc') {
        try { await env.RUNTIME.getByName('canonical').start(); return new Response(null, {status:500}); }
        catch { return new Response(null, {status:404}); }
      }
      if (action === '/receipts-disabled') return env.RECEIPTS.fetch(new Request('http://receipts.internal/receipts'));
      if (action === '/github-deny') return env.GH.fetch(new Request('https://api.github.com/user'));
      if (action === '/github-create') return env.GH.fetch(new Request('https://api.github.com/repos/jarlbrak/ftk-mod-framework/issues', { method:'POST', headers:{Authorization:'untrusted',Cookie:'untrusted','X-Forwarded-For':'198.51.100.9'},body:'{}' }));
      return new Response(null, {status:404});
    } }`,
  };
  const runtime = new Miniflare(convertV4MiniflareOptions({ workers: [worker('disabled', config.vars), worker('enabled', enabled), probe] }));
  try {
    for (const name of ['disabled', 'enabled']) {
      const publicWorker = await runtime.getWorker(name);
      for (const path of ['/', '/healthz', '/receipts', '/v1/reports']) {
        const response = await publicWorker.fetch('https://example.invalid' + path);
        assert.equal(response.status, 404);
        assert.equal(await response.text(), '');
      }
    }
    const client = await runtime.getWorker('probe');
    for (const [path, status] of Object.entries({ '/private-http':503, '/disabled-rpc':503, '/enabled-rpc-no-container':503, '/invalid-ip':503, '/alternate':404, '/runtime-http':404, '/no-start-rpc':404, '/receipts-disabled':503, '/github-deny':403, '/github-create':422 })) {
      const response = await client.fetch('https://example.invalid' + path);
      assert.equal(response.status, status, path);
      await response.body?.cancel();
    }
    assert.equal(outbound.length, 1);
    assert.equal(outbound[0].url, 'https://api.github.com/repos/jarlbrak/ftk-mod-framework/issues');
    assert.equal(outbound[0].headers.authorization, 'Bearer ' + fresh.githubToken);
    assert.equal(outbound[0].headers.cookie, undefined);
    assert.equal(outbound[0].headers['x-forwarded-for'], undefined);
  } finally {
    await runtime.dispose();
  }
});
