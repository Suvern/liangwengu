# Liangwengu 项目信息

## 项目概览

Liangwengu 是一个使用 F# 和 Avalonia.FuncUI 编写的跨平台系统托盘应用，用于展示 DeepSeek API 的峰谷定价状态。

支持的运行目标：

- Windows：`net10.0-windows10.0.17763.0`
- macOS：`net10.0-macos`
- Runtime：`win-x64`、`osx-x64`、`osx-arm64`

## 主要功能

- 根据 UTC 峰谷规则切换托盘图标
- 在 Tooltip 和右键菜单中展示当前状态与价格
- 从 GitHub 远程拉取 `pricing.json`
- 使用本地缓存和 EmbeddedResource 数据离线兜底
- 支持 Windows 注册表和 macOS LaunchAgent 开机启动
- Windows 支持 Toast 通知，macOS 支持 UserNotifications 系统通知

## 代码边界

- `src/Liangwengu/Domain/`：峰谷规则、展示文案和定价 JSON 类型、解析、校验等纯领域逻辑
- `src/Liangwengu/Infrastructure/PricingFetcher.fs`：远程、本地缓存和 bundled 定价数据
- `src/Liangwengu/Application/`：定价刷新协调和失败状态管理
- `src/Liangwengu/Platform/`：单实例、开机启动、原生通知和平台 API 声明
- `src/Liangwengu/Presentation/`：托盘菜单、视图和应用编排
- `src/Liangwengu/App.fs` / `Program.fs`：Avalonia 生命周期和程序入口
- `tests/Liangwengu.Tests/`：领域、定价 schema、刷新状态和通知测试
- `scripts/pricing-snapshot/`：独立的定价数据生成工具，不属于运行时业务代码

## 构建与测试

所有开发命令封装在项目根目录 `justfile` 中（just，同名 recipe 通过 `[macos]`/`[windows]` 注解按平台自动选择，`just --list` 查看全部命令）：

```bash
just run              # 启动 app（按平台自动选 TFM）
just clean            # 清理构建输出
just test             # 通用测试
just test-platform    # 平台相关测试（mac RID 按 CPU 自动推导, 与 CI 一致）
just format           # Fantomas 格式化
just format-check     # 与 CI 一致的格式化检查
just smoke-test       # 系统通知 smoke test
just publish-macos    # 打 dmg（arch 默认按当前 CPU 推导）
just publish-windows  # 打单文件 exe（仅 Windows）
```

发布逻辑（mac DMG、win 单文件 exe）已内联到 justfile 的 shebang 脚本块中，`scripts/` 目录仅保留 git hooks 与 pricing-snapshot 工具，不再存在独立的 build 脚本。

## 架构约束

- 保持一个正式应用项目，不为单个通知实现或小型功能新建类库项目
- 优先通过文件和目录拆分职责
- 纯领域逻辑不依赖 Avalonia、文件系统、网络或平台 API
- 平台 API 通过条件编译隔离
- 不改变已有用户可见行为，除非任务明确要求
- `pricing.json` 继续同时作为远程托管源和编译期 bundled 兜底
- F# 文件新增或移动后必须同步维护 `.fsproj` 中的编译顺序
