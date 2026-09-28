namespace Liangwengu

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

type App() =
    inherit Application()

    override this.Initialize() = this.Styles.Add(FluentTheme())

    override this.OnFrameworkInitializationCompleted() =
        base.OnFrameworkInitializationCompleted()

        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            Console.CancelKeyPress.Add(fun e ->
                e.Cancel <- true
                desktop.Shutdown())

            let args = Environment.GetCommandLineArgs()

            if args |> Array.contains "--panel-preview" then
                Liangwengu.Presentation.PanelPreview.start this desktop args
            else
                Liangwengu.Presentation.TrayApplication.start this desktop
        | _ -> ()
