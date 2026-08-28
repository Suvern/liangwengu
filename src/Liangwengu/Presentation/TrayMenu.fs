namespace Liangwengu.Presentation

open System
open System.Diagnostics
open Avalonia.Controls
open Liangwengu

type TrayMenu = {
    Root: NativeMenu
    Status: NativeMenuItem
    Models: NativeMenuItem list
    Autostart: NativeMenuItem
    Exit: NativeMenuItem
}

module TrayMenu =
    let create (modelPrices: ModelPrices list) (onExit: unit -> unit) =
        let openPricing _ =
            Process.Start(ProcessStartInfo(
                FileName = "https://api-docs.deepseek.com/zh-cn/quick_start/pricing/",
                UseShellExecute = true)) |> ignore

        let status = NativeMenuItem(Header = "初始化…")
        status.Click.Add openPricing

        let models =
            modelPrices
            |> List.map (fun _ ->
                let item = NativeMenuItem(Header = "…")
                item.Click.Add openPricing
                item)

        let menu = NativeMenu()
        menu.Add status
        models |> List.iter menu.Add
        menu.Add (NativeMenuItemSeparator())

        let autostartLabel enabled = if enabled then "关闭开机启动" else "启用开机启动"
        let autostart = NativeMenuItem(Header = autostartLabel (Autostart.isEnabled ()))
        autostart.Click.Add(fun _ ->
            try
                if Autostart.isEnabled () then Autostart.disable () else Autostart.enable ()
                autostart.Header <- autostartLabel (Autostart.isEnabled ())
            with ex ->
                Console.Error.WriteLine($"Autostart operation failed: {ex.Message}"))
        menu.Add autostart

        let exit = NativeMenuItem(Header = "退出")
        exit.Click.Add(fun _ -> onExit ())
        menu.Add exit

        { Root = menu; Status = status; Models = models; Autostart = autostart; Exit = exit }
