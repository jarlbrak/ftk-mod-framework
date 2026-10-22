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
  const runtime = new Miniflare(convertV4MiniflareOptions({
    modules: true,
    scriptPath: join(output, 'index.js'),
    compatibilityDate: config.compatibility_date,
    durableObjects: { RECEIPT_AUTHORITY: 'ReceiptAuthority' },
    bindings: config.vars,
  }));
  try {
    const response = await runtime.dispatchFetch('https://example.invalid/report');
    assert.equal(response.status, 404);
    assert.equal(await response.text(), '');
  } finally {
    await runtime.dispose();
  }
});
