module Liangwengu.Program

open System
open System.Runtime.InteropServices
open Avalonia
open Avalonia.Controls

module Native =
    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool AttachConsole(uint32 dwProcessId)
    let ATTACH_PARENT_PROCESS = 0xFFFFFFFFu

[<EntryPoint>]
[<STAThread>]
let main (args: string[]) =
    if System.OperatingSystem.IsWindows () then
        Native.AttachConsole(Native.ATTACH_PARENT_PROCESS) |> ignore

    AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown)
