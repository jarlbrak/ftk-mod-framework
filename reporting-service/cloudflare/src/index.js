import { DurableObject, WorkerEntrypoint } from "cloudflare:workers";
import { handleReceiptRequest, expireReceiptBatch } from "./receipt-bridge.mjs";
import { activated, unavailable, ingressRequest, runtimeForward, receiptEgress, githubEgress } from "./runtime-policy.mjs";

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

// A trusted gateway supplies its verified edge address as an RPC argument.
// No HTTP header can opt into this private ingress capability.
export class ReportingController extends WorkerEntrypoint {
  async fetch() { return unavailable(); }
  async forward(request, address) {
    if (!activated(this.env)) return unavailable();
    try {
      // Validate before resolving a runtime instance. The runtime validates and
      // reconstructs the request again at its own capability boundary.
      const incoming = ingressRequest(request, address);
      return await this.env.REPORTING_RUNTIME.getByName("canonical").forward(incoming, address);
    } catch { return unavailable(); }
  }
}

export class ReportingRuntime extends DurableObject {
  async fetch() { return new Response(null, { status: 404 }); }
  async forward(request, address) { return runtimeForward(this.ctx, this.env, request, address); }
}

export class ReceiptEgress extends WorkerEntrypoint {
  async fetch(request) {
    try { return await receiptEgress(request, this.env); }
    catch { return unavailable(); }
  }
}
export class GithubEgress extends WorkerEntrypoint {
  async fetch(request) {
    try { return await githubEgress(request, this.env); }
    catch { return unavailable(); }
  }
}

// Accidental public routing must never expose either private entrypoint.
export default { fetch() { return new Response(null, { status: 404 }); } };
