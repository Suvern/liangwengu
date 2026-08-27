# Changelog

本文件记录 liangwengu 的变更。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/)。

## Unreleased

### Added
- macOS 生成带应用图标的 `.app` 和 `.dmg` 安装包，Release 分别构建 ARM64 与 x64
- 定价数据动态拉取：LLM 解析 DeepSeek 官网 HTML 生成 `pricing.json`，程序每 30 min 从 GitHub 拉取，离线用编译期内嵌的 bundled 数据兜底
- out-of-band 生成管线（`tools/pricing-snapshot/`）：TypeScript + tsx 脚本，cheerio 提取价格区域算 sha256，DeepSeek API（json mode）解析，ajv 校验
- `pricing-sync.yml` GitHub Actions workflow：每日 00:05 UTC 定时执行，HTML hash 不变跳过 LLM，数据变更提 PR，仅 hash 变更直接 push
- `PricingSchema.fs`：JSON 类型定义 + FSharp.SystemTextJson 解析 + schema 版本门控（MAX_SUPPORTED=1）
- `PricingFetcher.fs`：bundled（EmbeddedResource）+ APPDATA 本地缓存 + 远程拉取（HttpClient 单例）
- `PricingService.fs`：30 min 定时拉取，连续 3 次失败（网络可用时）才弹 Windows toast 通知
- `tests/NotifyTest/`：独立的 toast 通知测试小程序
- `PricingSchemaTests.fs`：JSON 解析单元测试

### Changed
- 峰谷规则从"每天"改为"周一至周五"（`weekdaysOnly`），修复周末误判峰时的 bug
- `Domain.periodOf` / `nextSwitch` 接受 `PeakPolicy` 参数，不再依赖硬编码规则
- TFM 从 `net8.0` 改为 `net8.0-windows10.0.17763.0`（toast 需要 WinRT API）
- `pricing.json` 作为 EmbeddedResource 编译期内嵌，既是远程托管源也是 bundled 兜底，单一来源
- CI/release workflow 产物路径更新为 `net8.0-windows10.0.17763.0`

### Removed
- `Prices.fs`：硬编码价格表删除，数据移至 `pricing.json`

## v0.1.0 - 2026-08-19

### Added
- Windows 托盘常驻应用：实时显示 DeepSeek API 峰谷状态与价格
- 托盘图标根据峰/谷自动切换"梁文峰/梁文谷"（😈/😊，一念神魔）
- Tooltip 玩梗文案：梁文"峰/谷" + 倒计时 + Flash/Pro 输出价（元/M tokens）
- 右键菜单：状态行 + 两模型输入价格行（点击跳转官方定价页）+ 开机启动开关 + 退出
- 开机启动：读写 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 注册表
- Domain 纯函数 + xUnit 单元测试（峰谷边界 UTC 用例）
- DispatcherTimer(1min) 动态刷新 Tooltip / 状态行 / 图标
- CI 流水线：push/PR 构建测试（win-x64），PR 上传 exe 预览 artifact
- Release 流水线：tag 触发，单文件 exe + sha256 + Draft Release
- README：截图、Feature、构建说明
- MIT License
