# 托盘 Panel 设计概念 v1

本轮使用内置 image_gen 生成两张效果图，每个平台均展示空闲、高峰两种状态。参考项目已有系统截图与头像素材；属于视觉方案，尚未实现 UI。

## 效果图

### Windows

本地根目录参考图：`windows_tray_panel_concept_v1.png`（不随 Git 分发）。

### macOS

本地根目录参考图：`macos_tray_panel_concept_v1.png`（不随 Git 分发）。

## 设计方向

共享信息顺序：当前峰谷状态、下次切换倒计时、模型价格、常用设置、数据更新时间与官方链接。

| 项目 | Windows | macOS |
| --- | --- | --- |
| 弹出位置 | 任务栏托盘上方，贴近图标 | 顶部菜单栏图标下方 |
| 表面 | Fluent 浅灰底、细描边、轻阴影 | 珍珠白轻透浮层、柔和阴影 |
| 建议外圆角 | 8 DIP | 12 DIP |
| 设置区 | 紧凑平铺行 | 圆角分组行 |
| 字体方向 | Segoe UI + 中文系统回退 | SF Pro + 苹方系统回退 |

建议初始宽度 390 DIP，高度约 520–560 DIP，根据系统字体和缩放自适应。绿色表示谷、琥珀色表示峰，同时保留文字与头像，不仅依赖颜色传达状态。

价格来自本仓库当前 pricing.json，单位人民币元 / 百万 tokens；两幅场景分别设定为普通工作日北京时间 13:02、17:02，对应距 14:00、18:00 切换均为 58 分钟。图片中的桌面日期、头像细节和系统图标属于生成示意，不作为实现资产或业务规则来源；实际开发复用项目原始头像。

“切换时提醒”和手动刷新入口是下一步拟议交互；不表示本轮已经实现。未知状态应隐藏价格和倒计时并说明原因；离线缓存应显示数据来源状态。这两类异常状态未在本轮图片中展开。

## FuncUI 落地建议

项目已引用 Avalonia 12.1.1、Avalonia.FuncUI 2.0.0 和 Avalonia.Themes.Fluent，App.fs 已加载 FluentTheme。FuncUI 提供 F# 视图与状态表达，外观可由 Avalonia 主题及自定义样式实现。

| 视觉区域 | 建议控件 |
| --- | --- |
| 浮层主体 | 无系统装饰的 Window / FuncUI HostWindow，Border 包裹 Grid |
| 标题、状态与倒计时 | Image、TextBlock、Border、StackPanel |
| 两行价格表 | Grid + ItemsControl，避免为简单展示额外引入 DataGrid |
| 设置开关 | ToggleSwitch |
| 刷新、官方链接、退出 | Button + 图标 / 文本样式 |

两平台共享内容组件，平台样式控制圆角、间距、字体、表面和弹出定位。macOS 的原生材质、Windows 的模糊效果支持降级；本图表达视觉目标。左右键统一切换 Panel，取消原生菜单；点击外部及 Esc 收起面板。

参考：[FuncUI 官方项目](https://github.com/fsprojects/Avalonia.FuncUI)、[Avalonia Themes](https://docs.avaloniaui.net/docs/styling/themes)、[FluentAvalonia 主题资料](https://amwx.github.io/FluentAvaloniaDocs/pages/FATheme)。本方案以项目现有 FluentTheme 为基础，无需为效果图引入第三方主题依赖。

完整生成提示词见 [tray-panel-prompts.md](tray-panel-prompts.md)。
