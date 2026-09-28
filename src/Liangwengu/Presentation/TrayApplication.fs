namespace Liangwengu.Presentation

open System
open System.Diagnostics
open System.Threading.Tasks
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Threading
open Liangwengu
open Liangwengu.Application
open Liangwengu.Platform

module TrayApplication =
    let start (_app: Application) (lifetime: IClassicDesktopStyleApplicationLifetime) =
        let cached = PricingFetcher.loadLocalCache ()
        let mutable snapshot = cached |> Option.defaultWith PricingFetcher.loadBundled
        let mutable source = if cached.IsSome then Cache else Bundled

        let mutable operations =
            { PanelState.idle with
                AutostartBusy = true
                Reminders = (UserSettings.load UserSettings.path).Reminders }

        let mutable baseline =
            PeriodReminder.baseline snapshot.PeakPolicy DateTimeOffset.UtcNow

        let mutable disposed = false
        let mutable redraw = ignore
        let mutable lastTrayDismiss = -1000L
        let mac = OperatingSystem.IsMacOS()

        let post action =
            Dispatcher.UIThread.Post(fun () ->
                if not disposed then
                    action ())

        let service =
            new PricingRefreshService(
                PricingFetcher.tryFetchRemote,
                PricingFetcher.saveLocalCache,
                (fun () -> DateTimeOffset.UtcNow),
                Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable
            )

        let host: ITrayHost =
#if WIN32
            new WindowsTrayHost()
#else
#if MACOS
            new MacTrayHost()
#else
            new NullTrayHost()
#endif
#endif
        let report message =
            operations <- { operations with Error = Some message }
            redraw ()

        let sendNotification title message =
            task {
                let! result = Notification.show title message

                match result with
                | Error error -> post (fun () -> report ("通知未提交：" + error))
                | Ok() -> ()
            }
            |> ignore

        let actions =
            { Refresh = fun () -> service.Refresh(true) |> ignore
              Autostart =
                fun enabled ->
                    if not operations.AutostartBusy then
                        operations <-
                            { operations with
                                AutostartBusy = true
                                Error = None }

                        redraw ()

                        task {
                            let! result =
                                Task.Run(fun () ->
                                    try
                                        if enabled then
                                            Autostart.enable ()
                                        else
                                            Autostart.disable ()

                                        Ok(Autostart.isEnabled ())
                                    with ex ->
                                        let actual =
                                            try
                                                Some(Autostart.isEnabled ())
                                            with _ ->
                                                None

                                        Error(ex.Message, actual))

                            post (fun () ->
                                match result with
                                | Ok actual ->
                                    operations <-
                                        { operations with
                                            Autostart = actual
                                            AutostartBusy = false }
                                | Error(error, actual) ->
                                    operations <-
                                        { operations with
                                            Autostart = defaultArg actual operations.Autostart
                                            AutostartBusy = false
                                            Error = Some("开机启动设置失败：" + error) }

                                redraw ())
                        }
                        |> ignore
              Reminders =
                fun enabled ->
                    if not operations.RemindersBusy then
                        operations <-
                            { operations with
                                RemindersBusy = true
                                Error = None }

                        redraw ()

                        task {
                            let! result =
                                ReminderSettings.change
                                    Notification.requestPermission
                                    (fun value -> UserSettings.save UserSettings.path { Reminders = value })
                                    enabled

                            post (fun () ->
                                match result with
                                | Ok value ->
                                    operations <-
                                        { operations with
                                            Reminders = value
                                            RemindersBusy = false }

                                    baseline <- PeriodReminder.baseline snapshot.PeakPolicy DateTimeOffset.UtcNow
                                | Error error ->
                                    operations <-
                                        { operations with
                                            RemindersBusy = false
                                            Error = Some error }

                                redraw ())
                        }
                        |> ignore
              OpenPricing =
                fun () ->
                    try
                        Process.Start(
                            ProcessStartInfo(
                                "https://api-docs.deepseek.com/zh-cn/quick_start/pricing/",
                                UseShellExecute = true
                            )
                        )
                        |> ignore
                    with ex ->
                        report ("无法打开浏览器：" + ex.Message)
              Exit = lifetime.Shutdown }

        let model () =
            PanelState.create snapshot DateTimeOffset.UtcNow source operations

        let window = new PanelWindow(model (), mac, actions)

        redraw <-
            fun () ->
                let next = model ()
                window.Update next
                host.Update(next.Period, next.Tooltip)

        let tick () =
            let now = DateTimeOffset.UtcNow

            let next, notification =
                PeriodReminder.advance snapshot.PeakPolicy operations.Reminders now baseline

            baseline <- next

            notification
            |> Option.iter (fun period ->
                sendNotification
                    "梁文谷 · 峰谷切换"
                    (if period = Peak then
                         "已进入高峰时段，当前使用峰价。"
                     else
                         "已进入空闲时段，当前使用谷价。"))

            redraw ()

        let toggle activation =
            tick ()

            if window.IsVisible then
                window.Hide()
            elif Environment.TickCount64 - lastTrayDismiss >= 250L then
                let screen =
                    activation.Anchor
                    |> Option.map (fun rect -> rect.Center)
                    |> Option.orElse activation.Pointer
                    |> Option.bind (window.Screens.ScreenFromPoint >> Option.ofObj)
                    |> Option.orElse (Option.ofObj window.Screens.Primary)

                match screen with
                | None -> window.WindowStartupLocation <- WindowStartupLocation.CenterScreen
                | Some screen ->
                    // macOS exposes window/screen coordinates in Cocoa points.
                    let scale = if mac then 1. else screen.Scaling
                    window.Height <- min 560. (float screen.WorkingArea.Height / scale - 16.)
                    window.MinHeight <- min 430. window.Height
                    window.Width <- min 390. (float screen.WorkingArea.Width / scale)

                    let position, tip =
                        PanelPlacement.place
                            screen.WorkingArea
                            activation.Anchor
                            activation.Pointer
                            (Size(window.Width, window.Height))
                            scale
                            mac

                    window.Position <- position
                    window.SetTip tip

                window.Show()
                window.Activate()

        let traySubscription = host.Activated.Subscribe toggle

        window.Deactivated.Add(fun _ ->
            if window.IsVisible then
                match host.Anchor(), host.Pointer() with
                | Some rect, Some pointer when rect.Contains pointer -> lastTrayDismiss <- Environment.TickCount64
                | _ -> ()

                window.Hide())

        let refreshSubscription =
            service.Changed.Subscribe(fun change ->
                post (fun () ->
                    match change with
                    | RefreshStarted ->
                        operations <-
                            { operations with
                                Refreshing = true
                                Error = None }
                    | RefreshSucceeded(next, at) ->
                        snapshot <- next
                        source <- Remote at
                        baseline <- PeriodReminder.baseline snapshot.PeakPolicy DateTimeOffset.UtcNow

                        operations <-
                            { operations with
                                Refreshing = false
                                Error = None }
                    | RefreshFailed(message, notify) ->
                        operations <-
                            { operations with
                                Refreshing = false
                                Error = Some message }

                        if notify then
                            sendNotification "liangwengu" "pricing.json拉取失败，请确认您可以正常访问GitHub"

                    redraw ()))

        let timer = DispatcherTimer(Interval = TimeSpan.FromSeconds 1.)
        timer.Tick.Add(fun _ -> tick ())

        lifetime.Exit.Add(fun _ ->
            disposed <- true
            timer.Stop()
            refreshSubscription.Dispose()
            traySubscription.Dispose()
            (service :> IDisposable).Dispose()
            host.Dispose()
            window.Shutdown())

        task {
            let! actual =
                Task.Run(fun () ->
                    try
                        Ok(Autostart.isEnabled ())
                    with ex ->
                        Error ex.Message)

            post (fun () ->
                match actual with
                | Ok enabled ->
                    operations <-
                        { operations with
                            Autostart = enabled
                            AutostartBusy = false }
                | Error error ->
                    operations <-
                        { operations with
                            Error = Some error
                            AutostartBusy = false }

                redraw ())
        }
        |> ignore

        redraw ()
        timer.Start()
        service.StartPeriodic(TimeSpan.FromMinutes 30.)
        service.Refresh(false) |> ignore
