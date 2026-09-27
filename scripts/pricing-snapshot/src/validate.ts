#!/usr/bin/env tsx
import Ajv from "ajv";
import addFormats from "ajv-formats";
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { SCHEMA_V2, type HolidayCalendar, type LlmRawOutput, type PricingSnapshot } from "./common.js";
import { fetchHolidayCalendar } from "./fetch-holidays.js";

const isMain = import.meta.url === pathToFileURL(process.argv[1]).href;

const ajv = new Ajv({ allErrors: true, strict: false });
addFormats(ajv);
const validateFn = ajv.compile(JSON.parse(readFileSync(SCHEMA_V2, "utf8")));

export interface ValidationResult {
  ok: boolean;
  errors: string[];
  snapshot: PricingSnapshot | null;
  bumpNeeded: boolean;
  bumpReason: string;
}

function validateSemantics(snapshot: PricingSnapshot): string[] {
  const errors: string[] = [];
  const windows = snapshot.peakPolicy.windows.map(({ start, end }) => {
    const toMinutes = (value: string) => Number(value.slice(0, 2)) * 60 + Number(value.slice(3, 5));
    return { start: toMinutes(start), end: toMinutes(end) };
  });

  for (let i = 0; i < windows.length; i++) {
    if (windows[i].end <= windows[i].start) errors.push(`/peakPolicy/windows/${i} end must be later than start`);
    if (i > 0 && windows[i].start < windows[i - 1].end) errors.push(`/peakPolicy/windows/${i} overlaps or precedes the previous window`);
  }

  const calendar = snapshot.peakPolicy.holidayCalendar;
  if (calendar.coveredFrom > calendar.coveredThrough) {
    errors.push("/peakPolicy/holidayCalendar coveredFrom must be on or before coveredThrough");
  }
  if (calendar.excludedDates.some((date) => date < calendar.coveredFrom || date > calendar.coveredThrough)) {
    errors.push("/peakPolicy/holidayCalendar/excludedDates must fall within the covered range");
  }
  if (calendar.excludedDates.some((date, index) => index > 0 && date <= calendar.excludedDates[index - 1])) {
    errors.push("/peakPolicy/holidayCalendar/excludedDates must be sorted and unique");
  }
  if (new Set(snapshot.models.map((model) => model.modelId)).size !== snapshot.models.length) {
    errors.push("/models modelId values must be unique");
  }
  return errors;
}

export function validate(raw: LlmRawOutput, holidayCalendar: HolidayCalendar): ValidationResult {
  const stripped = {
    ...raw,
    sourceHash: "sha256:" + "0".repeat(64),
    peakPolicy: { ...raw.peakPolicy, holidayCalendar },
  } as Record<string, unknown>;
  delete stripped.schemaBumpNeeded;
  delete stripped.schemaBumpReason;

  const schemaOk = validateFn(stripped) as boolean;
  const errors = schemaOk ? [] : (validateFn.errors ?? []).map(
    (e) => `${e.instancePath || "/"} ${e.message ?? ""}`
  );

  const snapshot: PricingSnapshot | null = schemaOk
    ? {
        schemaVersion: raw.schemaVersion,
        sourceHash: "",
        currency: raw.currency,
        peakPolicy: { ...raw.peakPolicy, holidayCalendar },
        models: raw.models,
      }
    : null;
  if (snapshot) errors.push(...validateSemantics(snapshot));

  return {
    ok: errors.length === 0,
    errors,
    snapshot,
    bumpNeeded: raw.schemaBumpNeeded === true,
    bumpReason: raw.schemaBumpReason ?? "",
  };
}

if (isMain) {
  const raw = JSON.parse(readFileSync(0, "utf8")) as LlmRawOutput;
  fetchHolidayCalendar()
    .then((calendar) => {
      const r = validate(raw, calendar);
      if (!r.ok) {
        r.errors.forEach((e) => console.error(`  ${e}`));
        console.error("validate: FAIL");
        process.exit(1);
      }
      console.log("validate: OK");
      if (r.bumpNeeded) console.warn(`schemaBumpNeeded: ${r.bumpReason}`);
      console.log(JSON.stringify(r.snapshot, null, 2));
    })
    .catch((error) => {
      console.error(error);
      process.exit(1);
    });
}
