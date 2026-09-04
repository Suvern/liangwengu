# Changelog

本文件记录 liangwengu 的变更。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/)。

## v0.2.0 - 2026-09-04

### Added
- Windows 和 macOS 系统托盘支持；Release 分别构建 Windows x64、macOS ARM64 与 x64 安装包
- macOS 生成带应用图标的 `.app` 和 `.dmg` 安装包
- macOS LaunchAgent 开机启动支持，与 Windows 注册表开机启动保持一致
- 原生系统通知适配：Windows Toast 和 macOS UserNotifications；连续三次定价拉取失败时提示用户
- 单实例运行保护，避免重复启动托盘应用
- Windows 单文件 exe 嵌入应用图标并注册 Toast 应用身份
- 定价数据动态拉取：LLM 解析 DeepSeek 官网 HTML 生成 `pricing.json`，程序每 30 min 从 GitHub 拉取，离线用编译期内嵌的 bundled 数据兜底
- out-of-band 生成管线（`scripts/pricing-snapshot/`）：TypeScript + tsx 脚本，cheerio 提取价格区域算 sha256，DeepSeek API（json mode）解析，ajv 校验
- `pricing-sync.yml` GitHub Actions workflow：每日 00:05 UTC 定时执行，HTML hash 不变跳过 LLM，数据变更提 PR，仅 hash 变更直接 push
- 定价 JSON schema 版本门控、远程拉取、本地缓存和编译期 bundled 数据兜底
- 跨平台原生通知、定价 JSON 解析和刷新状态的测试覆盖

### Changed
- 峰谷规则从"每天"改为"周一至周五"（`weekdaysOnly`），修复周末误判峰时的 bug
- `Domain.periodOf` / `nextSwitch` 接受 `PeakPolicy` 参数，不再依赖硬编码规则
- 目标框架升级为 `.NET 10`，支持 `net10.0`、`net10.0-windows10.0.17763.0` 和 `net10.0-macos`
- `pricing.json` 作为 EmbeddedResource 编译期内嵌，既是远程托管源也是 bundled 兜底，单一来源
- CI 和 Release 流水线覆盖 Windows x64、macOS ARM64 与 x64，并固定 SDK、依赖和 just 版本
- 开发、测试、格式化、通知 smoke test 与打包命令统一收敛至 `justfile`

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
