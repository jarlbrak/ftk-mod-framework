// Receipt bodies use the existing Go JSON format. This module has no public route.
const PREFIX = "receipts/";
const MAX_BYTES = 2 * 1024 * 1024 + 4096;
const ID = /^[a-f0-9]{32}$/;
const statuses = new Set(["pending", "submitted", "rejected"]);
const response = (status, body = null, headers = {}) => new Response(body, { status, headers: { "Cache-Control": "no-store", ...headers } });
const tag = object => object.httpEtag || `"${object.etag}"`;
const stable = value => JSON.stringify(value, (_key, item) => item && typeof item === "object" && !Array.isArray(item) ? Object.fromEntries(Object.keys(item).sort().map(key => [key, item[key]])) : item);
function valid(rec, id) {
  return rec && rec.reportId === id && typeof rec.hash === "string" && rec.hash.length > 0 && Number.isFinite(Date.parse(rec.created)) && statuses.has(rec.status);
}
async function readObject(object) {
  if (object.size > MAX_BYTES) throw new Error("receipt exceeds bound");
  const text = await object.text();
  if (new TextEncoder().encode(text).length > MAX_BYTES) throw new Error("receipt exceeds bound");
  return JSON.parse(text);
}
async function listIDs(bucket, maximum) {
  const ids = [];
  let cursor;
  do {
    const page = await bucket.list({ prefix: PREFIX, cursor, limit: 1000 });
    for (const object of page.objects) {
      const id = object.key.slice(PREFIX.length).replace(/\.json$/, "");
      if (!ID.test(id) || object.key !== `${PREFIX}${id}.json`) throw new Error("unexpected receipt object");
      ids.push(id);
      if (ids.length > maximum) throw new Error("receipt import exceeds configured capacity");
    }
    cursor = page.truncated ? page.cursor : undefined;
    if (page.truncated && !cursor) throw new Error("invalid list cursor");
  } while (cursor);
  return ids;
}
async function catalog(bucket, storage, maximum) {
  const saved = await storage.get("reserved-ids") || [];
  const ids = [...new Set([...saved, ...await listIDs(bucket, maximum)])];
  if (ids.length > maximum) throw new Error("receipt catalog exceeds configured capacity");
  await storage.put("reserved-ids", ids);
  return ids;
}
function transitionAllowed(before, after) {
  if (["reportId", "hash", "created", "schemaVersion"].some(key => before[key] !== after[key])) return false;
  if (before.report == null && after.report != null) return false;
  if (after.report != null && stable(before.report) !== stable(after.report)) return false;
  if (before.status === "submitted") return after.status === "submitted" && before.issueNumber === after.issueNumber && before.issueUrl === after.issueUrl;
  return (before.status === "pending" && ["pending", "submitted", "rejected"].includes(after.status)) || (before.status === "rejected" && ["rejected", "pending"].includes(after.status));
}

// The caller serializes requests through one canonical Durable Object. Durable
// reservations are retained even after uncertain R2 writes, so restart cannot
// overrun capacity or turn a missing prior receipt into permission to POST again.
export async function handleReceiptRequest(request, bucket, storage, options) {
  if (!options.enabled) return response(503);
  const maximum = options.maximum || 1000;
  if (!Number.isInteger(maximum) || maximum < 1 || maximum > 2000) return response(503);
  try {
    const url = new URL(request.url);
    if (url.hostname !== "receipts.internal" || url.search) return response(400);
    if (url.pathname === "/receipts" && request.method === "GET") {
      const ids = await listIDs(bucket, maximum);
      await catalog(bucket, storage, maximum);
      if (options.retentionEnabled && !await storage.getAlarm()) await storage.setAlarm(Date.now() + 1000);
      return response(200, JSON.stringify(ids), { "Content-Type": "application/json" });
    }
    const match = /^\/receipts\/([a-f0-9]{32})$/.exec(url.pathname);
    if (!match) return response(404);
    const id = match[1], key = `${PREFIX}${id}.json`;
    const object = await bucket.get(key);
    if (request.method === "GET") {
      if (!object) {
        const reserved = await storage.get("reserved-ids") || [];
        return response(reserved.includes(id) ? 503 : 404);
      }
      const rec = await readObject(object);
      if (!valid(rec, id)) return response(503);
      return response(200, JSON.stringify(rec), { ETag: tag(object), "Content-Type": "application/json" });
    }
    if (request.method !== "PUT") return response(405);
    const expected = request.headers.get("If-Match"), create = request.headers.get("If-None-Match") === "*";
    if ((create && expected) || (!create && !expected) || (expected && !/^"[a-fA-F0-9]+"$/.test(expected))) return response(428);
    if (Number(request.headers.get("Content-Length")) > MAX_BYTES) return response(413);
    const reader = request.body?.getReader();
    const chunks = [];
    let length = 0;
    if (reader) {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        length += value.byteLength;
        if (length > MAX_BYTES) { await reader.cancel(); return response(413); }
        chunks.push(value);
      }
    }
    const bytes = new Uint8Array(length);
    let offset = 0;
    for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
    let rec;
    try { rec = JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(bytes)); } catch { return response(400); }
    if (!valid(rec, id)) return response(400);
    if (create) {
      if (object) return response(412);
      const ids = await catalog(bucket, storage, maximum);
      if (ids.includes(id)) return response(503);
      if (ids.length >= maximum) return response(507);
      // Persist the reservation before starting the R2 write. Never roll it back
      // after failure: the R2 write may have committed without a response.
      await storage.put("reserved-ids", [...ids, id]);
    } else {
      if (!object || tag(object) !== expected) return response(412);
      const before = await readObject(object);
      if (!valid(before, id) || !transitionAllowed(before, rec)) return response(409);
    }
    const conditions = new Headers(create ? { "If-None-Match": "*" } : { "If-Match": expected });
    const saved = await bucket.put(key, bytes, { onlyIf: conditions, httpMetadata: { contentType: "application/json" } });
    if (!saved) return response(412);
    if (options.retentionEnabled && !await storage.getAlarm()) await storage.setAlarm(Date.now() + 1000);
    return response(200, null, { ETag: tag(saved) });
  } catch {
    // Do not echo a provider error: it may include a URL or diagnostic payload.
    return response(503);
  }
}

// A Durable Object alarm runs independently of the sleeping Go container. The
// index retains every ID, including conservative reservations. CAS prevents a
// cleanup read from overwriting a concurrently finalized issue reference.
export async function expireReceiptBatch(bucket, storage, options) {
  if (!options.enabled || !options.retentionEnabled) return;
  const maximum = options.maximum || 1000;
  const ids = await catalog(bucket, storage, maximum);
  let cursor = await storage.get("expiry-cursor") || 0;
  const end = Math.min(cursor + 10, ids.length);
  for (; cursor < end; cursor++) {
    const id = ids[cursor], key = `${PREFIX}${id}.json`;
    const object = await bucket.get(key);
    if (!object) continue;
    const rec = await readObject(object);
    if (!valid(rec, id)) throw new Error("invalid receipt");
    if (rec.report != null && (options.now || Date.now()) - Date.parse(rec.created) >= 30 * 24 * 60 * 60 * 1000) {
      delete rec.report;
      const conditions = new Headers({ "If-Match": tag(object) });
      await bucket.put(key, JSON.stringify(rec), { onlyIf: conditions, httpMetadata: { contentType: "application/json" } });
    }
  }
  await storage.put("expiry-cursor", cursor >= ids.length ? 0 : cursor);
  await storage.setAlarm(Date.now() + (cursor >= ids.length ? 60 * 60 * 1000 : 1000));
}
