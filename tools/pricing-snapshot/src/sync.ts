#!/usr/bin/env tsx
import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { PRICING_JSON, type PricingSnapshot, type LlmRawOutput, snapshotData } from "./common.js";
import { fetchAndHash } from "./fetch-html.js";
import { parsePricingHtml } from "./parse-with-llm.js";
import { validate } from "./validate.js";

async function main() {
  // 1. 抓 HTML + 算 hash
  console.error("== fetch html ==");
  const { hash: newHash, html } = await fetchAndHash();
  console.error(`   hash = ${newHash}`);

  // 2. 读旧文件对比 hash
  let oldSnap: PricingSnapshot | null = null;
  try {
    oldSnap = JSON.parse(readFileSync(PRICING_JSON, "utf8")) as PricingSnapshot;
  } catch {
    console.error("   (no existing pricing.json)");
  }

  if (oldSnap && oldSnap.sourceHash === newHash) {
    console.error("== sourceHash unchanged; skip ==");
    output({ hashChanged: false, dataChanged: false, bumpNeeded: false, bumpReason: "" });
    return;
  }

  // 3. LLM 解析
  console.error("== parse with LLM ==");
  const raw = await parsePricingHtml(html);
  console.error(`   ${raw.models.length} models, bumpNeeded=${raw.schemaBumpNeeded}`);

  // 4. 校验
  console.error("== validate ==");
  const vr = validate(raw);
  if (!vr.ok) {
    vr.errors.forEach((e) => console.error(`   ${e}`));
    console.error("   validate FAILED");
    process.exit(1);
  }
  if (vr.bumpNeeded) console.error(`   schemaBumpNeeded: ${vr.bumpReason}`);

  // 5. 注入 hash，对比数据，写文件
  const newSnap: PricingSnapshot = { ...vr.snapshot!, sourceHash: newHash };
  const dataChanged = oldSnap ? snapshotData(oldSnap) !== snapshotData(newSnap) : true;
  writeFileSync(PRICING_JSON, JSON.stringify(newSnap, null, 2) + "\n");
  console.error(`   wrote ${PRICING_JSON} (dataChanged=${dataChanged})`);

  output({
    hashChanged: true,
    dataChanged,
    bumpNeeded: vr.bumpNeeded,
    bumpReason: vr.bumpReason,
  });
}

function output(r: {
  hashChanged: boolean;
  dataChanged: boolean;
  bumpNeeded: boolean;
  bumpReason: string;
}) {
  console.log(JSON.stringify(r));
  if (process.env.GITHUB_OUTPUT) {
    appendFileSync(
      process.env.GITHUB_OUTPUT,
      `hashChanged=${r.hashChanged}\ndataChanged=${r.dataChanged}\nbumpNeeded=${r.bumpNeeded}\nbumpReason=${r.bumpReason}\n`
    );
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
