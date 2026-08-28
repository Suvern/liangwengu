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

            Liangwengu.Presentation.TrayApplication.start this desktop
        | _ -> ()
