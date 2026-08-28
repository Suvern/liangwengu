namespace Liangwengu.Mac

open System.Diagnostics

module Notify =
    let private escapeAppleScriptString (value: string) =
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")

    let show (title: string) (message: string) =
        let psi = ProcessStartInfo()
        psi.FileName <- "/usr/bin/osascript"
        psi.ArgumentList.Add("-e")

        psi.ArgumentList.Add(
            $"display notification \"{escapeAppleScriptString message}\" with title \"{escapeAppleScriptString title}\""
        )

        psi.UseShellExecute <- false
        psi.RedirectStandardError <- true

        use notificationProcess = Process.Start(psi)
        notificationProcess.WaitForExit()

        if notificationProcess.ExitCode <> 0 then
            let stderr = notificationProcess.StandardError.ReadToEnd().Trim()
            failwith $"osascript notification failed: {stderr}"
