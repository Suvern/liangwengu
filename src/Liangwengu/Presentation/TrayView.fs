namespace Liangwengu.Presentation

open System
open Avalonia.Controls
open Avalonia.Platform
open Liangwengu

type TrayView = {
    Icon: TrayIcon
    PeakIcon: WindowIcon
    ValleyIcon: WindowIcon
}

module TrayView =
    let create (menu: TrayMenu) =
        let loadIcon name =
            let stream: IO.Stream = AssetLoader.Open(Uri($"avares://liangwengu/Assets/{name}"))
            WindowIcon(stream)

        let icon = new TrayIcon()
        icon.Menu <- menu.Root
        let view = {
            Icon = icon
            PeakIcon = loadIcon "peak.png"
            ValleyIcon = loadIcon "valley.png"
        }
        view

    let update (view: TrayView) (menu: TrayMenu) (snapshot: PricingSnapshot) (utc: DateTime) (currentPeriod: Period option) =
        let period = Domain.periodOf snapshot.PeakPolicy utc
        let _, switchAt = Domain.nextSwitch snapshot.PeakPolicy utc
        let remaining = switchAt - utc

        view.Icon.ToolTipText <- Domain.tooltip period remaining snapshot.Models
        menu.Status.Header <- Domain.statusLine period remaining

        List.zip menu.Models snapshot.Models
        |> List.iter (fun (item, model) -> item.Header <- Domain.inputLine period model)

        if currentPeriod <> Some period then
            view.Icon.Icon <- (match period with Peak -> view.PeakIcon | OffPeak -> view.ValleyIcon)

        period
