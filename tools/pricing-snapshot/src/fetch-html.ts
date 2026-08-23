#!/usr/bin/env tsx
import * as cheerio from "cheerio";
import { pathToFileURL } from "node:url";
import { PRICING_URL, sha256Hex } from "./common.js";

const isMain = import.meta.url === pathToFileURL(process.argv[1]).href;

const UA =
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

async function fetchHtml(url: string): Promise<string> {
  const res = await fetch(url, {
    headers: { "User-Agent": UA, "Accept-Language": "zh-CN,zh;q=0.9" },
  });
  if (!res.ok) throw new Error(`fetch ${url} -> ${res.status}`);
  return Buffer.from(await res.arrayBuffer()).toString("utf8");
}

function cleanHtml(html: string): string {
  const $ = cheerio.load(html);
  $("script, style, nav, footer, aside, .pagination-nav, .toc").remove();
  const scope = $("article").first();
  if (scope.length === 0) return $("body").html() ?? html;
  return scope.html() ?? "";
}

export async function fetchAndHash(): Promise<{ hash: string; html: string }> {
  const raw = await fetchHtml(PRICING_URL);
  const cleaned = cleanHtml(raw);
  const text = cheerio.load(cleaned).text().replace(/\s+/g, " ").trim().toLowerCase();
  return { hash: sha256Hex(text), html: cleaned };
}

if (isMain) {
  fetchAndHash()
    .then(({ hash }) => console.log(hash))
    .catch((e) => {
      console.error(e);
      process.exit(1);
    });
}
