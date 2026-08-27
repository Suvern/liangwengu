# Liangwengu - 梁文峰？梁文谷！

这是一个 **实时展示 DeepSeek 峰谷价格的系统托盘小程序**，用于随时查看峰谷情况

全部使用 `F#` + `Avalonia.FuncUI` 进行开发，支持 Windows 和 macOS

**效果展示**

| 平台    | 高峰时段                                  | 空闲时段                                    | 右键菜单                                          |
|---------|-------------------------------------------|---------------------------------------------|---------------------------------------------------|
| Windows | ![](docs/assets/windows_preview_peak.png) | ![](docs/assets/windows_preview_valley.png) | ![](docs/assets/windows_preview_context_menu.png) |
| macOS   | ![](docs/assets/macos_preview_preak.png)  | ![](docs/assets/macos_preview_valley.png)  | ![](docs/assets/macos_preview_context_menu.png)   |

---

众所周知，自从 DeepSeek V4 Pro 采用峰谷定价后，“梁圣”的口碑会随着峰谷价格呈现一个离散、可归纳的二值化状态：
> 参考 [滑动变祖器](https://liang.itsuyo.top/) 中“小难梁” -> “梁祖”的演化过程

| 定价       | 状态        | 图示                        |
|------------|-------------|-----------------------------|
| 高峰时段   | 😈“梁文峰”  | ![](docs/assets/peak.png)   |
| 空闲时段   | 😊“梁文谷”  | ![](docs/assets/valley.png) |

* 定价和峰谷数据来自 https://api-docs.deepseek.com/zh-cn/quick_start/pricing
  * 通过 LLM 解析官网 HTML 生成 `pricing.json`，程序每 30 min 从 GitHub 拉取最新版本，离线时使用编译期内嵌的兜底数据
* 图片来自 [Abyss-Seeker/liang-intensity-calibrator](https://github.com/Abyss-Seeker/liang-intensity-calibrator/tree/main) 的初始帧和结束帧

## Feature
* 系统托盘根据峰/谷状态自动切换图标"梁文峰"/"梁文谷"（一念神魔）
* 鼠标 hover 托盘图标展示当前峰谷状态 + 价格
* 右键托盘图标查看详细状态、设置开机启动
* 定价数据动态拉取：LLM 解析官网 → GitHub 托管 JSON → 程序每 30 min 自动更新，离线用 bundled 兜底

## TODO
- [x] 开机启动
- [x] 从官网定价动态拉取数据而非写死
- [x] macOS 托盘支持
- [ ] 峰谷跳变系统通知提醒
- [ ] 支持更多平台峰谷定价

## Develop
需要前置安装 [.NET 10](https://dotnet.microsoft.com/zh-cn/download) 环境

### 启动项目

**macOS:**
```bash
dotnet run --project ./src/Liangwengu -f net10.0
```

**Windows:**
```bash
dotnet run --project ./src/Liangwengu -f net10.0-windows10.0.17763.0
```

### 发布

**macOS (ARM64):**
```bash
./scripts/build-mac.sh arm64 0.1.0
# 产物: artifacts/macos-arm64/liangwengu-0.1.0-macos-arm64.dmg
```

**macOS (x64):**
```bash
./scripts/build-mac.sh x64 0.1.0
# 产物: artifacts/macos-x64/liangwengu-0.1.0-macos-x64.dmg
```

**Windows (x64):**
```powershell
.\scripts\build-win.ps1 x64
# 产物位于: artifacts\windows-x64\liangwengu.exe
```

### 手动发布命令

**macOS:**
```bash
./scripts/build-mac.sh arm64 0.1.0
```

**Windows:**
```bash
dotnet publish src/Liangwengu/Liangwengu.fsproj -c Release -f net10.0-windows10.0.17763.0 -r win-x64 --self-contained true -o artifacts/windows-x64
```

## License
[MIT](LICENSE)
