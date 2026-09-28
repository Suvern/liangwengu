namespace Liangwengu.Presentation

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Styling
open Avalonia.Input
open Avalonia.Media.Imaging
open Avalonia.Threading
open Liangwengu

module PanelPreview =
    let start (app: Application) (lifetime: IClassicDesktopStyleApplicationLifetime) (args: string array) =
        let optionValue key fallback =
            args
            |> Array.tryFindIndex ((=) key)
            |> Option.bind (fun i -> Array.tryItem (i + 1) args)
            |> Option.defaultValue fallback

        let mutable scenario = optionValue "--scenario" "valley"
        let mutable mac = optionValue "--platform" "windows" = "macos"
        let mutable operations = PanelState.idle
        let snapshot = PricingFetcher.loadBundled ()

        let getModel () =
            let instant =
                DateTimeOffset.Parse(
                    if scenario = "peak" then "2026-09-28T17:02:00+08:00"
                    elif scenario = "unknown" then "2099-01-01T10:00:00+08:00"
                    else "2026-09-28T13:02:00+08:00"
                )

            let ops =
                { operations with
                    Refreshing = scenario = "refreshing"
                    Error =
                        if scenario = "error" then
                            Some "同步失败，正在使用上次数据"
                        else
                            operations.Error }

            let snap =
                if scenario = "many" then
                    { snapshot with
                        Models =
                            [ for i in 1..12 do
                                  { snapshot.Models.Head with
                                      DisplayName = $"Long model name {i}" } ] }
                else
                    snapshot

            PanelState.create snap instant Bundled ops

        let mutable update = ignore

        let report message =
            operations <- { operations with Error = Some message }
            update ()

        let actions =
            { Refresh = fun () -> report "预览：已触发刷新"
              Autostart =
                fun value ->
                    operations <- { operations with Autostart = value }
                    update ()
              Reminders =
                fun value ->
                    operations <- { operations with Reminders = value }
                    update ()
              OpenPricing = fun () -> report "预览：官方定价入口"
              Exit = lifetime.Shutdown }

        let window = new PanelWindow(getModel (), mac, actions)
        update <- fun () -> window.Update(getModel ())

        window.RequestedThemeVariant <-
            if optionValue "--theme" "light" = "dark" then
                ThemeVariant.Dark
            else
                ThemeVariant.Light

        window.WindowStartupLocation <- WindowStartupLocation.CenterScreen
        window.Height <- float (optionValue "--height" "560")
        window.ShowInTaskbar <- true

        window.KeyDown.Add(fun e ->
            match e.Key with
            | Key.F1 ->
                scenario <- "valley"
                update ()
            | Key.F2 ->
                scenario <- "peak"
                update ()
            | Key.F3 ->
                scenario <- "unknown"
                update ()
            | Key.F4 ->
                scenario <- "refreshing"
                update ()
            | Key.F5 ->
                scenario <- "error"
                update ()
            | Key.F6 ->
                mac <- not mac
                window.SetPlatform mac
            | Key.F7 ->
                window.RequestedThemeVariant <-
                    if window.ActualThemeVariant = ThemeVariant.Dark then
                        ThemeVariant.Light
                    else
                        ThemeVariant.Dark
            | Key.F8 ->
                scenario <- "many"
                update ()
            | Key.Escape -> lifetime.Shutdown()
            | _ -> ())

        lifetime.Exit.Add(fun _ -> window.Shutdown())
        window.Show()
        let capture = optionValue "--capture" ""

        if capture <> "" then
            DispatcherTimer.RunOnce(
                (fun () ->
                    use bitmap =
                        new RenderTargetBitmap(
                            PixelSize(int window.Width * 2, int window.Height * 2),
                            Vector(192., 192.)
                        )

                    bitmap.Render window
                    bitmap.Save(capture, PngBitmapEncoderOptions.Default)
                    lifetime.Shutdown()),
                TimeSpan.FromSeconds 1.
            )
            |> ignore
