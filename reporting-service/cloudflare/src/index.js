import { DurableObject, WorkerEntrypoint } from "cloudflare:workers";
import { handleReceiptRequest, expireReceiptBatch } from "./receipt-bridge.mjs";

const enabled = env => env.FTK_RECEIPT_BRIDGE_ENABLED === "true";
const options = env => ({ enabled: enabled(env), retentionEnabled: env.FTK_RETENTION_ENABLED === "true", maximum: Number(env.MAX_REPORTS || "1000") });

export class ReceiptAuthority extends DurableObject {
  canonical() {
    return this.ctx.id.equals(this.env.RECEIPT_AUTHORITY.idFromName("canonical"));
  }
  async fetch(request) {
    if (!this.canonical()) return new Response(null, { status: 404 });
    return this.ctx.blockConcurrencyWhile(() => handleReceiptRequest(request, this.env.RECEIPTS, this.ctx.storage, options(this.env)));
  }
  async alarm() {
    if (!this.canonical()) return;
    return this.ctx.blockConcurrencyWhile(async () => {
      try { await expireReceiptBatch(this.env.RECEIPTS, this.ctx.storage, options(this.env)); }
      catch { await this.ctx.storage.setAlarm(Date.now() + 60_000); }
    });
  }
}

// Private service-binding preparation only. This entrypoint cannot start a
// container, forward a report, or acquire GitHub authorization.
export class ReportingController extends WorkerEntrypoint {
  async fetch() {
    return new Response("Reporting activation integration is pending", { status: 503, headers: { "Cache-Control": "no-store" } });
  }
}

// Accidental public routing must never expose either private entrypoint.
export default { fetch() { return new Response(null, { status: 404 }); } };
