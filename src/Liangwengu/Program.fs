module Liangwengu.Program

open System
open Avalonia
open Avalonia.Controls

[<EntryPoint>]
[<STAThread>]
let main (args: string[]) =
    AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown)
