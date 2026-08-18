namespace Liangwengu

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Platform
open Avalonia.Themes.Fluent
open Avalonia.Threading

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())

    override this.OnFrameworkInitializationCompleted() =
        base.OnFrameworkInitializationCompleted()

        let loadIcon name =
            let stream: IO.Stream = AssetLoader.Open(Uri($"avares://liangwengu/Assets/{name}"))
            WindowIcon(stream)

        let peakIcon = loadIcon "peak.png"
        let valleyIcon = loadIcon "valley.png"

        // 状态行 + 每模型一行价格（禁用），加菜单项时与 Prices.all 顺序对应
        let statusItem = NativeMenuItem(Header = "初始化…", IsEnabled = false)
        let modelItems =
            Prices.all
            |> List.map (fun _ -> NativeMenuItem(Header = "…", IsEnabled = false))
        let exitItem = NativeMenuItem(Header = "退出")

        let menu = NativeMenu()
        menu.Add statusItem
        modelItems |> List.iter menu.Add
        menu.Add (NativeMenuItemSeparator())

        // 开机启动开关: 状态用 Header 文字表达（规避 Windows IsChecked bug）
        let autostartLabel enabled = if enabled then "开机启动: 开" else "开机启动: 关"
        let autostartItem = NativeMenuItem(Header = autostartLabel (Autostart.isEnabled ()))
        autostartItem.Click.Add(fun _ ->
            if Autostart.isEnabled () then Autostart.disable () else Autostart.enable ()
            autostartItem.Header <- autostartLabel (Autostart.isEnabled ()))
        menu.Add autostartItem

        menu.Add exitItem

        let trayIcon = new TrayIcon()
        trayIcon.Menu <- menu

        // 就地更新文本/图标，不重建菜单（规避平台 bug，见 PLAN.md 第 8 节）
        let mutable currentPeriod: Period option = None

        let refresh () =
            let utc = DateTime.UtcNow
            let period = Domain.periodOf utc
            let _, switchAt = Domain.nextSwitch utc
            let remaining = switchAt - utc
            trayIcon.ToolTipText <- Domain.tooltip period remaining Prices.all
            statusItem.Header <- Domain.statusLine period remaining
            (modelItems, Prices.all)
            ||> List.iter2 (fun item m -> item.Header <- Domain.inputLine period m)
            if currentPeriod <> Some period then
                trayIcon.Icon <- (match period with Peak -> peakIcon | OffPeak -> valleyIcon)
                currentPeriod <- Some period

        let timer = DispatcherTimer()
        timer.Interval <- TimeSpan.FromMinutes 1.0
        timer.Tick.Add(fun _ -> refresh ())
        timer.Start()
        refresh ()

        let icons = TrayIcons()
        icons.Add trayIcon
        TrayIcon.SetIcons(this, icons)

        exitItem.Click.Add(fun _ ->
            match this.ApplicationLifetime with
            | :? IClassicDesktopStyleApplicationLifetime as desktop -> desktop.Shutdown()
            | _ -> ())
