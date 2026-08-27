#!/usr/bin/env tsx
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { TEMPLATE_V1, PRICING_URL, type LlmRawOutput } from "./common.js";

const isMain = import.meta.url === pathToFileURL(process.argv[1]).href;

const API_BASE = "https://api.deepseek.com";
const MODEL = process.env.DEEPSEEK_MODEL ?? "deepseek-chat";
const MAX_RETRIES = 3;

async function callDeepSeek(system: string, user: string): Promise<string> {
  const key = process.env.DEEPSEEK_API_KEY;
  if (!key) throw new Error("DEEPSEEK_API_KEY is not set");

  const res = await fetch(`${API_BASE}/chat/completions`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${key}` },
    body: JSON.stringify({
      model: MODEL,
      messages: [{ role: "system", content: system }, { role: "user", content: user }],
      response_format: { type: "json_object" },
      temperature: 0,
    }),
  });
  if (!res.ok) throw new Error(`deepseek api ${res.status}: ${await res.text()}`);
  return ((await res.json()) as { choices: { message: { content: string } }[] }).choices[0].message.content;
}

export async function parsePricingHtml(html: string): Promise<LlmRawOutput> {
  const system = readFileSync(TEMPLATE_V1, "utf8");
  const user = `解析以下 DeepSeek 定价页 HTML，按 schema 输出 JSON。\n\nURL: ${PRICING_URL}\n\nHTML:\n${html}\n`;

  let lastErr: unknown;
  for (let i = 1; i <= MAX_RETRIES; i++) {
    try {
      const content = await callDeepSeek(system, user);
      let s = content.trim();
      if (s.startsWith("```")) s = s.replace(/^```(?:json)?\s*/i, "").replace(/\s*```$/i, "");
      console.error(`info: parse succeeded on attempt ${i}`);
      return JSON.parse(s) as LlmRawOutput;
    } catch (e) {
      lastErr = e;
      console.error(`warn: attempt ${i}/${MAX_RETRIES} failed: ${(e as Error).message}`);
    }
  }
  throw new Error(`all ${MAX_RETRIES} attempts failed: ${(lastErr as Error)?.message}`);
}

if (isMain) {
  const html = readFileSync(0, "utf8");
  parsePricingHtml(html)
    .then((out) => console.log(JSON.stringify(out, null, 2)))
    .catch((e) => {
      console.error(e);
      process.exit(1);
    });
}
