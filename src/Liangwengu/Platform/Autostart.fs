namespace Liangwengu

open System
open System.IO

module Autostart =

#if WIN32
    open Microsoft.Win32

    let private runKeySubPath = @"Software\Microsoft\Windows\CurrentVersion\Run"
    let private valueName = "Liangwengu"

    let isEnabled () : bool =
        use key = Registry.CurrentUser.OpenSubKey(runKeySubPath)
        not (isNull key) && not (isNull (key.GetValue(valueName)))

    let enable () : unit =
        match Environment.ProcessPath with
        | null -> failwith "无法获取当前进程路径"
        | path ->
            use key = Registry.CurrentUser.CreateSubKey(runKeySubPath, writable = true)
            key.SetValue(valueName, $"\"{path}\"")

    let disable () : unit =
        use key = Registry.CurrentUser.OpenSubKey(runKeySubPath, writable = true)

        if not (isNull key) then
            key.DeleteValue(valueName, throwOnMissingValue = false)
#else
    let private plistDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents")

    let private plistPath = Path.Combine(plistDir, "com.liangwengu.plist")

    let private xmlEscape (s: string) =
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;")

    let private ensureMacOS () =
        if not (OperatingSystem.IsMacOS()) then
            failwith "Linux autostart is not implemented"

    let private currentUserId () =
        let psi = System.Diagnostics.ProcessStartInfo()
        psi.FileName <- "/usr/bin/id"
        psi.ArgumentList.Add("-u")
        psi.UseShellExecute <- false
        psi.RedirectStandardOutput <- true
        use proc = System.Diagnostics.Process.Start(psi)
        proc.WaitForExit()

        if proc.ExitCode <> 0 then
            failwith "无法获取当前 macOS 用户 ID"

        let uid = proc.StandardOutput.ReadToEnd().Trim()

        if String.IsNullOrWhiteSpace(uid) then
            failwith "无法获取当前 macOS 用户 ID"

        uid

    let private runLaunchctl (arguments: string list) =
        let psi = System.Diagnostics.ProcessStartInfo()
        psi.FileName <- "/bin/launchctl"
        arguments |> List.iter psi.ArgumentList.Add
        psi.UseShellExecute <- false
        psi.RedirectStandardError <- true
        use proc = System.Diagnostics.Process.Start(psi)
        proc.WaitForExit()

        if proc.ExitCode <> 0 then
            let stderr = proc.StandardError.ReadToEnd().Trim()
            failwith $"launchctl failed: {stderr}"

    let isEnabled () : bool =
        ensureMacOS ()
        File.Exists(plistPath)

    let enable () : unit =
        ensureMacOS ()

        match Environment.ProcessPath with
        | null -> failwith "无法获取当前进程路径"
        | path ->
            Directory.CreateDirectory(plistDir) |> ignore
            let escapedPath = xmlEscape path

            let plist =
                $"""<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.liangwengu</string>
    <key>ProgramArguments</key>
    <array>
        <string>{escapedPath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <false/>
            </dict>
            </plist>"""

            File.WriteAllText(plistPath, plist)
            let uid = currentUserId ()
            runLaunchctl [ "bootstrap"; $"gui/{uid}"; plistPath ]

    let disable () : unit =
        ensureMacOS ()

        if File.Exists(plistPath) then
            let uid = currentUserId ()
            runLaunchctl [ "bootout"; $"gui/{uid}/com.liangwengu" ]
            File.Delete(plistPath)
#endif
