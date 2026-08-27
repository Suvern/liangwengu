module Liangwengu.Program

open System
open System.Diagnostics
open System.Threading
open Avalonia
open Avalonia.Controls

let private logException context (ex: exn) =
    Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] {context}: {ex}")

[<EntryPoint>]
[<STAThread>]
let main (args: string[]) =
#if WIN32
    Liangwengu.Native.Windows.AttachConsole(0xFFFFFFFFu) |> ignore // Windows: attach to parent console
#endif

    Trace.Listeners.Add(new TextWriterTraceListener(Console.Error)) |> ignore
    Trace.AutoFlush <- true

    AppDomain.CurrentDomain.UnhandledException.Add(fun eventArgs ->
        logException "Unhandled exception" (eventArgs.ExceptionObject :?> exn))

    System.Threading.Tasks.TaskScheduler.UnobservedTaskException.Add(fun eventArgs ->
        logException "Unobserved task exception" eventArgs.Exception
        eventArgs.SetObserved())

    try
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown)
    with ex ->
        logException "Application startup failed" ex
        1
