module Liangwengu.Program

open System
open Avalonia
open Avalonia.Controls

[<EntryPoint>]
[<STAThread>]
let main (args: string[]) =
#if WIN32
    Liangwengu.Native.Windows.AttachConsole(0xFFFFFFFFu) |> ignore // Windows: attach to parent console
#endif

    AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown)
