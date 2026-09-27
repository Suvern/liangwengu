# pricing-snapshot

独立同步流水线：抓取 DeepSeek 定价页和中国节假日日历 → 解析并校验 → 更新根目录 `pricing.json`。桌面应用直接读取快照中内嵌的节假日数据，不依赖运行时在线访问 `holiday-cn`。

## 流程

```
fetch-holidays ───────────────────────────────────┐
fetch-html → parse-with-llm → validate → sync ────┴→ pricing.json
```

1. **fetch-html**：抓中文版 HTML，cheerio 去掉 script/nav/footer 等噪声，取 `<article>` 内容算 sha256。
2. **fetch-holidays**：抓取当前年、相邻年 `holiday-cn` JSON；选取覆盖范围内 `isOffDay: true` 的日期。只在相邻年安排已有来源文件和日期时扩展覆盖范围。
3. **parse-with-llm**：把清理后的 HTML 交给 DeepSeek API（JSON mode，最多重试 3 次）；模型输出北京时间窗口，不输出假期日期。
4. **validate**：把同步器生成的 holiday calendar 注入快照；Ajv 校验 v2 Schema，代码另校验时间窗口顺序/重叠、假期覆盖范围和模型 ID 唯一性，并剥离 LLM 输出层字段。
5. **sync**：比较定价 HTML hash 和假日数据；二者均未变化时跳过。仅假日变更时复用旧的 v2 价格数据，不调用 LLM。v1 快照会在下次同步时重新解析并升级为 v2。校验失败或 LLM 请求 schema bump 时，不覆盖 `pricing.json`。

假日数据来自 [NateScarlet/holiday-cn](https://github.com/NateScarlet/holiday-cn)，其 JSON 中的 `papers` 用于上游数据核对，不复制到应用快照。同步遇到当前年度数据缺失、响应无效或网络错误时失败，不会把空列表当成“没有节假日”。

PR 策略在 GitHub Actions yml 里根据 sync 输出的 `dataChanged` / `bumpNeeded` 字段决定。

## 环境

- Node 22+（内置 fetch）
- `npm install`（装 cheerio / ajv / tsx）
- `npm test` / `npm run typecheck` 验证日历处理、v2 快照和 TypeScript 类型

## env

| key | 必需 | 说明 |
|-|-|-|
| `DEEPSEEK_API_KEY` | 是 | DeepSeek API key |
| `DEEPSEEK_MODEL` | 否 | 默认 `deepseek-flash` |

## 本地跑

```bash
cd scripts/pricing-snapshot
npm install
export DEEPSEEK_API_KEY=sk-...
npx tsx src/sync.ts
```
