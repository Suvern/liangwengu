import assert from "node:assert/strict";
import test from "node:test";
import { join } from "node:path";
import { PRICING_JSON, ROOT, readJson, type HolidayCalendar, type LlmRawOutput, type PricingSnapshot } from "../src/common.js";
import { validate } from "../src/validate.js";

const calendar: HolidayCalendar = {
  coveredFrom: "2026-01-01",
  coveredThrough: "2027-12-31",
  excludedDates: ["2026-10-01", "2026-10-02"],
};

const raw: LlmRawOutput = {
  schemaVersion: 2,
  currency: "CNY",
  peakPolicy: {
    timezone: "Asia/Shanghai",
    weekdaysOnly: true,
    windows: [
      { start: "09:00", end: "12:00" },
      { start: "14:00", end: "18:00" },
    ],
  },
  models: [{
    modelId: "deepseek-flash",
    displayName: "Flash",
    peak: { inputCacheHit: 0.04, inputCacheMiss: 2, output: 8 },
    offPeak: { inputCacheHit: 0.02, inputCacheMiss: 1, output: 4 },
  }],
};

test("validate injects the holiday calendar into a v2 snapshot", () => {
  const result = validate(raw, calendar);
  assert.equal(result.ok, true, result.errors.join("; "));
  assert.deepEqual(result.snapshot?.peakPolicy.holidayCalendar, calendar);
});

test("repository pricing.json validates against v2 schema", () => {
  for (const file of [PRICING_JSON, join(ROOT, "scripts", "pricing-snapshot", "examples", "sample-v2.json")]) {
    const snapshot = readJson<PricingSnapshot>(file);
    const { sourceHash: _sourceHash, ...rawSnapshot } = snapshot;
    const result = validate(rawSnapshot as LlmRawOutput, snapshot.peakPolicy.holidayCalendar);
    assert.equal(result.ok, true, `${file}: ${result.errors.join("; ")}`);
  }
});

test("validate rejects overlapping windows, duplicate model ids, and bad coverage", () => {
  const overlapping = {
    ...raw,
    peakPolicy: { ...raw.peakPolicy, windows: [
      { start: "09:00", end: "12:00" },
      { start: "11:00", end: "14:00" },
    ] },
  };
  assert.match(validate(overlapping, calendar).errors.join("; "), /overlaps/);

  const duplicateModels = { ...raw, models: [raw.models[0], raw.models[0]] };
  assert.match(validate(duplicateModels, calendar).errors.join("; "), /modelId values must be unique/);

  const outOfRange = { ...calendar, excludedDates: ["2028-01-01"] };
  assert.match(validate(raw, outOfRange).errors.join("; "), /within the covered range/);
});

test("validate reports an unrepresentable source change without treating it as success", () => {
  const result = validate({ ...raw, schemaBumpNeeded: true, schemaBumpReason: "Different rate schedule" }, calendar);
  assert.equal(result.ok, true);
  assert.equal(result.bumpNeeded, true);
  assert.equal(result.snapshot?.schemaVersion, 2);
});
