module Liangwengu.Program

open System
open System.Diagnostics
open System.Threading
open Avalonia
open Avalonia.Controls

let private logException context (ex: exn) =
    Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] {context}: {ex}")

let private runNotificationSmokeTest () =
    try
        match
            Liangwengu.Platform.Notification.show "liangwengu test" "Native notification smoke test"
            |> fun task -> task.GetAwaiter().GetResult()
        with
        | Ok() ->
            Console.WriteLine("Notification submitted successfully.")
            0
        | Error message ->
            Console.Error.WriteLine($"Notification failed: {message}")
            1
    with ex ->
        logException "Notification smoke test failed" ex
        1

[<EntryPoint>]
[<STAThread>]
let main (args: string[]) =
#if WIN32
    Liangwengu.Native.Windows.AttachConsole(0xFFFFFFFFu) |> ignore // Windows: attach to parent console
#endif

    Trace.Listeners.Add(new TextWriterTraceListener(Console.Error)) |> ignore
    Trace.AutoFlush <- true

    if args |> Array.contains "--notification-smoke-test" then
        runNotificationSmokeTest ()
    else
        AppDomain.CurrentDomain.UnhandledException.Add(fun eventArgs ->
            logException "Unhandled exception" (eventArgs.ExceptionObject :?> exn))

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException.Add(fun eventArgs ->
            logException "Unobserved task exception" eventArgs.Exception
            eventArgs.SetObserved())

        try
            match Liangwengu.Platform.SingleInstance.tryAcquire () with
            | None ->
                Console.Error.WriteLine("Liangwengu is already running.")
                0
            | Some instanceLease ->
                use _instanceLease = instanceLease

                AppBuilder
                    .Configure<App>()
                    .UsePlatformDetect()
                    .WithInterFont()
                    .LogToTrace()
                    .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown)
        with ex ->
            logException "Application startup failed" ex
            1
