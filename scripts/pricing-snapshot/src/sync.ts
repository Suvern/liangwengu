#!/usr/bin/env tsx
import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { PRICING_JSON, type PricingSnapshot, type LlmRawOutput, snapshotData } from "./common.js";
import { fetchAndHash } from "./fetch-html.js";
import { fetchHolidayCalendar } from "./fetch-holidays.js";
import { parsePricingHtml } from "./parse-with-llm.js";
import { validate } from "./validate.js";

async function main() {
  console.log("== fetch holiday calendar ==");
  const holidayCalendar = await fetchHolidayCalendar();
  console.log(
    `   ${holidayCalendar.excludedDates.length} off-days, covered ${holidayCalendar.coveredFrom} through ${holidayCalendar.coveredThrough}`,
  );

  console.log("== fetch pricing html ==");
  const { hash: newHash, html } = await fetchAndHash();
  console.log(`   hash = ${newHash}`);

  let oldSnap: PricingSnapshot | null = null;
  try {
    oldSnap = JSON.parse(readFileSync(PRICING_JSON, "utf8")) as PricingSnapshot;
  } catch {
    console.log("   (no existing pricing.json)");
  }

  const calendarChanged =
    !oldSnap ||
    oldSnap.schemaVersion !== 2 ||
    JSON.stringify(oldSnap.peakPolicy.holidayCalendar) !== JSON.stringify(holidayCalendar);
  const hashChanged = !oldSnap || oldSnap.sourceHash !== newHash;

  if (!hashChanged && !calendarChanged && oldSnap?.schemaVersion === 2) {
    console.log("== pricing and holiday calendar unchanged; skip ==");
    output({ hashChanged: false, calendarChanged: false, dataChanged: false, bumpNeeded: false, bumpReason: "" });
    return;
  }

  let candidate: PricingSnapshot;
  let bumpNeeded = false;
  let bumpReason = "";

  if (!hashChanged && oldSnap?.schemaVersion === 2) {
    console.log("== update holiday calendar only ==");
    candidate = {
      ...oldSnap,
      peakPolicy: { ...oldSnap.peakPolicy, holidayCalendar },
    };
  } else {
    console.log("== parse pricing html with LLM ==");
    const raw: LlmRawOutput = await parsePricingHtml(html);
    console.log(`   ${Array.isArray(raw.models) ? raw.models.length : 0} models, bumpNeeded=${raw.schemaBumpNeeded === true}`);

    if (raw.schemaBumpNeeded === true) {
      bumpNeeded = true;
      bumpReason = raw.schemaBumpReason ?? "The pricing page contains information the current schema cannot represent.";
      console.log(`   schemaBumpNeeded: ${bumpReason}`);
      output({ hashChanged, calendarChanged, dataChanged: false, bumpNeeded, bumpReason });
      return;
    }

    console.log("== validate v2 snapshot ==");
    const validation = validate(raw, holidayCalendar);
    if (!validation.ok) {
      validation.errors.forEach((error) => console.error(`   ${error}`));
      throw new Error("pricing snapshot validation failed; existing pricing.json was not changed");
    }

    candidate = validation.snapshot!;
  }

  const newSnap: PricingSnapshot = { ...candidate, sourceHash: newHash };
  const dataChanged = !oldSnap || oldSnap.schemaVersion !== 2 || snapshotData(oldSnap) !== snapshotData(newSnap);
  writeFileSync(PRICING_JSON, JSON.stringify(newSnap, null, 2) + "\n");
  console.log(`   wrote ${PRICING_JSON} (dataChanged=${dataChanged})`);

  output({ hashChanged, calendarChanged, dataChanged, bumpNeeded, bumpReason });
}

function output(r: {
  hashChanged: boolean;
  calendarChanged: boolean;
  dataChanged: boolean;
  bumpNeeded: boolean;
  bumpReason: string;
}) {
  const safeBumpReason = r.bumpReason.replace(/[\r\n]+/g, " ").trim();
  console.log(JSON.stringify(r));
  if (process.env.GITHUB_OUTPUT) {
    appendFileSync(
      process.env.GITHUB_OUTPUT,
      `hashChanged=${r.hashChanged}\ncalendarChanged=${r.calendarChanged}\ndataChanged=${r.dataChanged}\nbumpNeeded=${r.bumpNeeded}\nbumpReason=${safeBumpReason}\n`,
    );
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
