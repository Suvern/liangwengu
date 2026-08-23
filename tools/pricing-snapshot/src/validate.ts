#!/usr/bin/env tsx
import Ajv from "ajv";
import addFormats from "ajv-formats";
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { SCHEMA_V1, type LlmRawOutput, type PricingSnapshot } from "./common.js";

const isMain = import.meta.url === pathToFileURL(process.argv[1]).href;

const ajv = new Ajv({ allErrors: true, strict: false });
addFormats(ajv);
const validateFn = ajv.compile(JSON.parse(readFileSync(SCHEMA_V1, "utf8")));

export interface ValidationResult {
  ok: boolean;
  errors: string[];
  snapshot: PricingSnapshot | null;
  bumpNeeded: boolean;
  bumpReason: string;
}

export function validate(raw: LlmRawOutput): ValidationResult {
  const stripped = { ...raw, sourceHash: "sha256:" + "0".repeat(64) } as Record<string, unknown>;
  delete stripped.schemaBumpNeeded;
  delete stripped.schemaBumpReason;

  const ok = validateFn(stripped) as boolean;
  const errors = ok ? [] : (validateFn.errors ?? []).map(
    (e) => `${e.instancePath || "/"} ${e.message ?? ""}`
  );

  const snapshot: PricingSnapshot | null = ok
    ? {
        schemaVersion: raw.schemaVersion,
        sourceHash: "",
        currency: raw.currency,
        peakPolicy: raw.peakPolicy,
        models: raw.models,
      }
    : null;

  return {
    ok,
    errors,
    snapshot,
    bumpNeeded: raw.schemaBumpNeeded === true,
    bumpReason: raw.schemaBumpReason ?? "",
  };
}

if (isMain) {
  const raw = JSON.parse(readFileSync(0, "utf8")) as LlmRawOutput;
  const r = validate(raw);
  if (!r.ok) {
    r.errors.forEach((e) => console.error(`  ${e}`));
    console.error("validate: FAIL");
    process.exit(1);
  }
  console.error("validate: OK");
  if (r.bumpNeeded) console.error(`schemaBumpNeeded: ${r.bumpReason}`);
  console.log(JSON.stringify(r.snapshot, null, 2));
}
