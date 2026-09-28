# 托盘 Panel 效果图生成提示词

使用内置 image_gen，参考现有截图与 peak.png / valley.png。以下为完整提示词；图片为设计概念，数值取自项目 pricing.json，切换提醒为拟议功能。

## Windows

```text
Use case: ui-mockup.
Create a polished, highly legible high-fidelity desktop tray panel UI concept sheet for existing Chinese app "梁文谷" (Liangwengu), a DeepSeek peak/off-peak pricing tray utility built in F# Avalonia.FuncUI with Avalonia FluentTheme. This is an implementable compact desktop utility, no dashboard, no webpage.
Reference images supplied: first screenshot is visual/context reference for the existing OS tray and app, NOT a pixel-preservation edit target; second image is the existing peak portrait asset (normal man in suit); third image is existing valley portrait asset (golden crowned man). Preserve these portrait identities as small square thumbnails; no redesigned logo.
Landscape 1536x1024 or larger equivalent, carefully typeset crisp Simplified Chinese. Two equal-width columns each containing a realistic enlarged desktop crop with ONE complete tray panel, all content readable, no scrolling, no cutoffs. Left panel VALLEY at 13:02; right panel PEAK at 17:02. Both same size, same information architecture, same controls, aligned across board. Board heading "梁文谷 / 托盘面板" plus platform subtitle; modest outside labels "空闲时段" and "高峰时段". Neutral tasteful desktop backgrounds leave UI most prominent. Very little decorative copy. Render attractive production-quality UI with careful spacing and alignment, fine borders, subtle shadows, restrained green valley and muted amber peak. No fake charts.
Each panel ~390 logical px wide and ~550 tall, visually scaled large.
Panel content in order:
1. Header small app icon and "梁文谷" left, refresh arrow icon right.
2. Status block: 48px existing portrait; left panel gold portrait, large "梁文谷", green badge "空闲时段", small "当前使用谷价". Right panel normal portrait, large "梁文峰", amber badge "高峰时段", small "当前使用峰价".
3. Prominent countdown: left small "距高峰还有", bold tabular numerals "58 分钟", supporting "14:00 切换 · 北京时间". Right small "距空闲还有", bold "58 分钟", supporting "18:00 切换 · 北京时间". Use restrained tinted background.
4. Section heading "模型价格", trailing small unit "元 / 百万 tokens". Compact elegant four-column table with EXACT column titles "模型", "输入未命中", "输入命中", "输出". Left rows EXACT: "Flash" "1.00" "0.02" "4.00"; "Pro" "4.50" "0.15" "13.50". Right rows EXACT: "Flash" "2.00" "0.04" "8.00"; "Pro" "9.00" "0.30" "27.00". Align numbers right, emphasize output with semibold. Thin row separators, ample readability. Prices are repository sample data, not fetched live.
5. Subtle divider. Two settings rows: "开机启动" with toggle on, "切换时提醒" with toggle off (proposed feature).
6. Footnote "数据更新于 12:50" left panel, "数据更新于 16:50" right panel. Footer text button "查看官方定价 ↗" left and quiet "退出" right.
No traffic-light controls, no window titlebar, no sidebar, no navigation tabs, no extra services, no invented models. No large promotional headings. Prioritize precise text and realistic OS tray anchoring.
Platform: WINDOWS 11 / FLUENT.
Both panels LIGHT neutral Fluent surfaces over desaturated cool blue Windows wallpaper. 8px corner radius, near-white #F5F5F5 acrylic-like outer panel, white subtle bordered inset content, 1px #E2E2E2 separators, dark #1D1D1F text, Segoe UI Variable and Microsoft YaHei-like typography. Compact Fluent ToggleSwitch with blue on state, ~4px inner corner radius, restrained 8/12/16px spacing. Each desktop crop has Windows taskbar at BOTTOM, tray portrait highlighted toward bottom right, adjacent chevron/network/volume/time, times 13:02 and 17:02 respectively. Panel rises immediately ABOVE its corresponding tray portrait with small 8px gap, aligned to bottom-right of desktop crop. NO popover triangle for Windows. Show a believable small portion of taskbar, with the panel filling most of the crop. Subtitle outside panels "Windows · Fluent".
```

## macOS

```text
Use case: ui-mockup.
Create a polished, highly legible high-fidelity desktop tray panel UI concept sheet for existing Chinese app "梁文谷" (Liangwengu), a DeepSeek peak/off-peak pricing tray utility built in F# Avalonia.FuncUI with Avalonia FluentTheme. This is an implementable compact desktop utility, no dashboard, no webpage.
Reference images supplied: first screenshot is visual/context reference for the existing OS tray and app, NOT a pixel-preservation edit target; second image is the existing peak portrait asset (normal man in suit); third image is existing valley portrait asset (golden crowned man). Preserve these portrait identities as small square thumbnails; no redesigned logo.
Landscape 1536x1024 or larger equivalent, carefully typeset crisp Simplified Chinese. Two equal-width columns each containing a realistic enlarged desktop crop with ONE complete tray panel, all content readable, no scrolling, no cutoffs. Left panel VALLEY at 13:02; right panel PEAK at 17:02. Both same size, same information architecture, same controls, aligned across board. Board heading "梁文谷 / 托盘面板" plus platform subtitle; modest outside labels "空闲时段" and "高峰时段". Neutral tasteful desktop backgrounds leave UI most prominent. Very little decorative copy. Render attractive production-quality UI with careful spacing and alignment, fine borders, subtle shadows, restrained green valley and muted amber peak. No fake charts.
Each panel ~390 logical px wide and ~550 tall, visually scaled large.
Panel content in order:
1. Header small app icon and "梁文谷" left, refresh arrow icon right.
2. Status block: 48px existing portrait; left panel gold portrait, large "梁文谷", green badge "空闲时段", small "当前使用谷价". Right panel normal portrait, large "梁文峰", amber badge "高峰时段", small "当前使用峰价".
3. Prominent countdown: left small "距高峰还有", bold tabular numerals "58 分钟", supporting "14:00 切换 · 北京时间". Right small "距空闲还有", bold "58 分钟", supporting "18:00 切换 · 北京时间". Use restrained tinted background.
4. Section heading "模型价格", trailing small unit "元 / 百万 tokens". Compact elegant four-column table with EXACT column titles "模型", "输入未命中", "输入命中", "输出". Left rows EXACT: "Flash" "1.00" "0.02" "4.00"; "Pro" "4.50" "0.15" "13.50". Right rows EXACT: "Flash" "2.00" "0.04" "8.00"; "Pro" "9.00" "0.30" "27.00". Align numbers right, emphasize output with semibold. Thin row separators, ample readability. Prices are repository sample data, not fetched live.
5. Subtle divider. Two settings rows: "开机启动" with toggle on, "切换时提醒" with toggle off (proposed feature).
6. Footnote "数据更新于 12:50" left panel, "数据更新于 16:50" right panel. Footer text button "查看官方定价 ↗" left and quiet "退出" right.
No traffic-light controls, no window titlebar, no sidebar, no navigation tabs, no extra services, no invented models. No large promotional headings. Prioritize precise text and realistic OS tray anchoring.
Platform: macOS / MENU BAR POPOVER.
Both panels LIGHT pearly translucent surfaces over muted lavender-blue macOS wallpaper. SF Pro / PingFang SC-like typography, 12px continuous corners, fine white rim and delicate dark drop shadow, subdued vibrancy-like peripheral translucency with clean readable near-opaque content. Rows feel like polished native menu bar utility; 8px gently rounded grouped settings and small native-looking pill toggles, blue on state. Each desktop crop has macOS MENU BAR at TOP with highlighted little existing portrait status item, Wi-Fi, battery and small time 13:02 or 17:02. Panel hangs immediately BELOW its matching status icon with a small centered upward attachment notch pointing to portrait. Menu bar spans full crop width. No bottom taskbar, no Dock, no window traffic lights. Layout intentionally mac-native in shape and spacing while keeping all common content and Avalonia-realizable control shapes. Subtitle outside panels "macOS · Menu Bar".
```
