import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { handleReceiptRequest, expireReceiptBatch } from './src/receipt-bridge.mjs';
const id = 'a'.repeat(32), other = 'b'.repeat(32);
const options = { enabled: true, retentionEnabled: true, maximum: 1 };
const receipt = (reportId = id) => ({ schemaVersion: 1, status: 'pending', reportId, hash: 'original-hash', created: '2026-01-01T00:00:00Z', report: { message: 'synthetic payload' } });
class Storage {
  data = new Map(); alarm = null;
  async get(key) { return structuredClone(this.data.get(key)); }
  async put(key, value) { this.data.set(key, structuredClone(value)); }
  async getAlarm() { return this.alarm; }
  async setAlarm(value) { this.alarm = value; }
}
class Bucket {
  data = new Map(); fail = false; commitThenFail = false; race = null;
  async list() { return { objects: [...this.data.keys()].map(key => ({ key })), truncated: false }; }
  async get(key) {
    const value = this.data.get(key);
    return value ? { ...value, text: async () => value.body } : null;
  }
  async put(key, body, { onlyIf }) {
    if (this.race) { const run = this.race; this.race = null; await run(); }
    if (this.fail && !this.commitThenFail) throw new Error('synthetic provider failure');
    const prior = this.data.get(key);
    if (onlyIf.get('If-None-Match') === '*' && prior) return null;
    if (onlyIf.get('If-Match') && prior?.httpEtag !== onlyIf.get('If-Match')) return null;
    body = typeof body === 'string' ? body : new TextDecoder().decode(body);
    const etag = createHash('sha256').update(body).digest('hex');
    const value = { body, size: Buffer.byteLength(body), etag, httpEtag: `"${etag}"` };
    this.data.set(key, value);
    if (this.fail) throw new Error('synthetic uncertain write');
    return value;
  }
}
function request(rec, etag) {
  return new Request(`http://receipts.internal/receipts/${rec.reportId}`, { method: 'PUT', headers: etag ? { 'If-Match': etag } : { 'If-None-Match': '*' }, body: JSON.stringify(rec) });
}
const get = reportId => new Request(`http://receipts.internal/receipts/${reportId}`);
test('disabled preparation performs no bucket operations', async () => {
  assert.equal((await handleReceiptRequest(get(id), null, null, { enabled: false })).status, 503);
});
test('conditional creation is unique and durable capacity survives restart', async () => {
  const bucket = new Bucket(), storage = new Storage();
  assert.equal((await handleReceiptRequest(request(receipt()), bucket, storage, options)).status, 200);
  assert.equal((await handleReceiptRequest(request(receipt()), bucket, storage, options)).status, 412);
  assert.equal((await handleReceiptRequest(request(receipt(other)), bucket, storage, options)).status, 507);
  assert.equal(bucket.data.size, 1);
});
for (const committed of [false, true]) test(`ambiguous create retains reservation when committed=${committed}`, async () => {
  const bucket = new Bucket(), storage = new Storage(); bucket.fail = true; bucket.commitThenFail = committed;
  assert.equal((await handleReceiptRequest(request(receipt()), bucket, storage, options)).status, 503);
  bucket.fail = false;
  // Restart uses the same durable state, with no process-local authorization.
  assert.equal((await handleReceiptRequest(get(id), bucket, storage, options)).status, committed ? 200 : 503);
  assert.equal((await handleReceiptRequest(request(receipt()), bucket, storage, options)).status, committed ? 412 : 503);
  assert.equal((await handleReceiptRequest(request(receipt(other)), bucket, storage, options)).status, 507);
});
test('stale replacement and immutable receipt changes cannot overwrite a final issue', async () => {
  const bucket = new Bucket(), storage = new Storage();
  const pending = receipt();
  const initial = await handleReceiptRequest(request(pending), bucket, storage, options);
  const originalTag = initial.headers.get('ETag');
  const submitted = { ...pending, status: 'submitted', issueNumber: 42, issueUrl: 'https://github.com/jarlbrak/ftk-mod-framework/issues/42' };
  const saved = await handleReceiptRequest(request(submitted, originalTag), bucket, storage, options);
  assert.equal(saved.status, 200);
  assert.equal((await handleReceiptRequest(request(pending, originalTag), bucket, storage, options)).status, 412);
  for (const changed of [{ ...submitted, hash: 'changed' }, { ...submitted, issueNumber: 99 }, { ...submitted, status: 'pending' }]) {
    assert.equal((await handleReceiptRequest(request(changed, saved.headers.get('ETag')), bucket, storage, options)).status, 409);
  }
});
test('independent alarm after restart expires payload only, for all statuses', async () => {
  for (const status of ['pending', 'submitted', 'rejected']) {
    const bucket = new Bucket(), storage = new Storage();
    const original = { ...receipt(), status, issueNumber: 42, issueUrl: 'https://github.com/jarlbrak/ftk-mod-framework/issues/42' };
    await bucket.put(`receipts/${id}.json`, JSON.stringify(original), { onlyIf: new Headers({ 'If-None-Match': '*' }) });
    // No Go process or container participates in expiry.
    await expireReceiptBatch(bucket, storage, { ...options, now: Date.parse('2026-10-21T00:00:00Z') });
    const after = JSON.parse((await bucket.get(`receipts/${id}.json`)).body);
    const { report, ...metadata } = original;
    assert.deepEqual(after, metadata);
    assert.ok(storage.alarm);
  }
});
test('expiry CAS cannot overwrite a concurrently finalized issue', async () => {
  const bucket = new Bucket(), storage = new Storage();
  await handleReceiptRequest(request(receipt()), bucket, storage, options);
  const finalized = { ...receipt(), status: 'submitted', issueNumber: 42, issueUrl: 'https://github.com/jarlbrak/ftk-mod-framework/issues/42' };
  bucket.race = async () => bucket.put(`receipts/${id}.json`, JSON.stringify(finalized), { onlyIf: new Headers() });
  await expireReceiptBatch(bucket, storage, options);
  assert.deepEqual(JSON.parse((await bucket.get(`receipts/${id}.json`)).body), finalized);
  await expireReceiptBatch(bucket, storage, options);
  assert.equal(JSON.parse((await bucket.get(`receipts/${id}.json`)).body).report, undefined);
});
test('no deletion API and provider errors return no payload', async () => {
  const bucket = new Bucket(), storage = new Storage();
  assert.equal((await handleReceiptRequest(new Request(`http://receipts.internal/receipts/${id}`, { method: 'DELETE' }), bucket, storage, options)).status, 405);
  bucket.get = async () => { throw new Error('synthetic secret diagnostic'); };
  const failed = await handleReceiptRequest(get(id), bucket, storage, options);
  assert.equal(failed.status, 503);
  assert.equal(await failed.text(), '');
});
test('chunked payload bound cancels before persistence', async () => {
  const bucket = new Bucket(), storage = new Storage();
  let cancelled = false;
  const body = new ReadableStream({ pull(controller) { controller.enqueue(new Uint8Array(1024 * 1024)); }, cancel() { cancelled = true; } });
  const req = new Request(`http://receipts.internal/receipts/${id}`, { method: 'PUT', headers: { 'If-None-Match': '*' }, body, duplex: 'half' });
  assert.equal((await handleReceiptRequest(req, bucket, storage, options)).status, 413);
  assert.ok(cancelled);
  assert.equal(bucket.data.size, 0);
  assert.equal(await storage.get('reserved-ids'), undefined);
});
