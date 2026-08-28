namespace Liangwengu.Presentation

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Threading
open Liangwengu

module TrayApplication =
    let start (app: Application) (lifetime: IClassicDesktopStyleApplicationLifetime) =
        let mutable activeSnapshot = PricingFetcher.loadInitial ()
        let mutable currentPeriod: Period option = None
        let menu = TrayMenu.create activeSnapshot.Models lifetime.Shutdown
        let view = TrayView.create menu

        let refresh () =
            let period = TrayView.update view menu activeSnapshot DateTime.UtcNow currentPeriod
            currentPeriod <- Some period

        let timer = DispatcherTimer()
        timer.Interval <- TimeSpan.FromMinutes 1.0
        timer.Tick.Add(fun _ -> refresh ())
        timer.Start()
        refresh ()

        PricingService.start (fun snapshot ->
            activeSnapshot <- snapshot
            refresh ())

        let icons = TrayIcons()
        icons.Add view.Icon
        TrayIcon.SetIcons(app, icons)

        let platform =
            if OperatingSystem.IsMacOS() then
                "macOS"
            else
                Environment.OSVersion.Platform.ToString()

        Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] Tray icon initialized on {platform}")
