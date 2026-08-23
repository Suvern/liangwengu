# pricing-snapshot

Out-of-band pipeline: fetch DeepSeek pricing HTML → parse with LLM → validate → update `pricing.json`（根目录）。

## 流程

```
fetch-html  →  parse-with-llm  →  validate  →  sync（编排 + 写 pricing.json）
```

1. **fetch-html**：抓中文版 HTML，cheerio 去掉 script/nav/footer 等噪声，取 `<article>` 内容算 sha256
2. **parse-with-llm**：把清理后的 HTML 塞给 DeepSeek API（json mode，重试 3 次）
3. **validate**：ajv 校验 JSON Schema，剥离 LLM 输出层字段（schemaBumpNeeded 等）
4. **sync**：编排上述三步；sourceHash 不变则跳过 LLM；变了则重跑并写 `pricing.json`

PR 策略在 GitHub Actions yml 里根据 sync 输出的 `dataChanged` / `bumpNeeded` 字段决定。

## 环境

- Node 22+（内置 fetch）
- `npm install`（装 cheerio / ajv / tsx）

## env

| key | 必需 | 说明 |
|-|-|-|
| `DEEPSEEK_API_KEY` | 是 | DeepSeek API key |
| `DEEPSEEK_MODEL` | 否 | 默认 `deepseek-chat` |

## 本地跑

```bash
cd tools/pricing-snapshot
npm install
export DEEPSEEK_API_KEY=sk-...
npx tsx src/sync.ts
```
