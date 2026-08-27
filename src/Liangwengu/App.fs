namespace Liangwengu

open System
open System.Diagnostics
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

        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            Console.CancelKeyPress.Add(fun e ->
                e.Cancel <- true
                desktop.Shutdown())
        | _ -> ()

        let loadIcon name =
            let stream: IO.Stream = AssetLoader.Open(Uri($"avares://liangwengu/Assets/{name}"))
            WindowIcon(stream)

        let peakIcon = loadIcon "peak.png"
        let valleyIcon = loadIcon "valley.png"

        let openPricing _ =
            Process.Start(ProcessStartInfo(
                FileName = "https://api-docs.deepseek.com/zh-cn/quick_start/pricing/",
                UseShellExecute = true)) |> ignore

        let mutable activeSnapshot = PricingFetcher.loadInitial ()

        let statusItem = NativeMenuItem(Header = "初始化…")
        statusItem.Click.Add openPricing
        let modelItems =
            activeSnapshot.Models
            |> List.map (fun _ ->
                let item = NativeMenuItem(Header = "…")
                item.Click.Add openPricing
                item)

        let menu = NativeMenu()
        menu.Add statusItem
        modelItems |> List.iter menu.Add
        menu.Add (NativeMenuItemSeparator())

        let autostartLabel enabled = if enabled then "关闭开机启动" else "启用开机启动"
        let autostartItem = NativeMenuItem(Header = autostartLabel (Autostart.isEnabled ()))
        autostartItem.Click.Add(fun _ ->
            try
                if Autostart.isEnabled () then Autostart.disable () else Autostart.enable ()
                autostartItem.Header <- autostartLabel (Autostart.isEnabled ())
            with ex ->
                Console.Error.WriteLine($"Autostart operation failed: {ex.Message}"))
        menu.Add autostartItem

        let exitItem = NativeMenuItem(Header = "退出")
        menu.Add exitItem

        let trayIcon = new TrayIcon()
        trayIcon.Menu <- menu

        let mutable currentPeriod: Period option = None

        let refresh () =
            let utc = DateTime.UtcNow
            // let utc = DateTime(2026, 8, 27, 9, 1, 1, DateTimeKind.Local) // mock峰
            // let utc = DateTime(2026, 8, 27, 0, 1, 1, DateTimeKind.Local) // mock谷
            
            let policy = activeSnapshot.PeakPolicy
            let period = Domain.periodOf policy utc
            let _, switchAt = Domain.nextSwitch policy utc
            let remaining = switchAt - utc

            trayIcon.ToolTipText <- Domain.tooltip period remaining activeSnapshot.Models
            statusItem.Header <- Domain.statusLine period remaining

            List.zip modelItems activeSnapshot.Models
            |> List.iter (fun (item, m) -> item.Header <- Domain.inputLine period m)

            if currentPeriod <> Some period then
                trayIcon.Icon <- (match period with Peak -> peakIcon | OffPeak -> valleyIcon)
                currentPeriod <- Some period

        let timer = DispatcherTimer()
        timer.Interval <- TimeSpan.FromMinutes 1.0
        timer.Tick.Add(fun _ -> refresh ())
        timer.Start()
        refresh ()

        PricingService.start (fun snap ->
            activeSnapshot <- snap
            refresh ())

        let icons = TrayIcons()
        icons.Add trayIcon
        TrayIcon.SetIcons(this, icons)
        let platform = if OperatingSystem.IsMacOS() then "macOS" else Environment.OSVersion.Platform.ToString()
        Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] Tray icon initialized on {platform}")

        exitItem.Click.Add(fun _ ->
            match this.ApplicationLifetime with
            | :? IClassicDesktopStyleApplicationLifetime as desktop -> desktop.Shutdown()
            | _ -> ())
