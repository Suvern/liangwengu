import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export const ROOT = path.resolve(__dirname, "..", "..", "..");
export const PRICING_JSON = path.join(ROOT, "pricing.json");
export const SCHEMA_V1 = path.join(__dirname, "..", "schema-v1.json");
export const TEMPLATE_V1 = path.join(__dirname, "..", "schema-v1-template.md");

export const PRICING_URL =
  "https://api-docs.deepseek.com/zh-cn/quick_start/pricing/";

export interface PeriodPrices {
  inputCacheHit: number;
  inputCacheMiss: number;
  output: number;
}

export interface ModelPrices {
  modelId: string;
  displayName: string;
  peak: PeriodPrices;
  offPeak: PeriodPrices;
}

export interface TimeWindow {
  start: string;
  end: string;
}

export interface PeakPolicy {
  weekdaysOnly: boolean;
  windows: TimeWindow[];
}

export interface PricingSnapshot {
  schemaVersion: number;
  sourceHash: string;
  currency: string;
  peakPolicy: PeakPolicy;
  models: ModelPrices[];
}

export interface LlmRawOutput extends Omit<PricingSnapshot, "sourceHash"> {
  schemaBumpNeeded?: boolean;
  schemaBumpReason?: string;
}

export function sha256Hex(text: string): string {
  return "sha256:" + createHash("sha256").update(text, "utf8").digest("hex");
}

export function readJson<T>(p: string): T {
  return JSON.parse(readFileSync(p, "utf8")) as T;
}

export function snapshotData(s: Pick<PricingSnapshot, "schemaVersion" | "currency" | "peakPolicy" | "models">) {
  const { schemaVersion, currency, peakPolicy, models } = s;
  return JSON.stringify({ schemaVersion, currency, peakPolicy, models });
}
