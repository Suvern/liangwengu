namespace Liangwengu

open System
open System.Net.NetworkInformation
open Avalonia.Threading
open Windows.Data.Xml.Dom
open Windows.UI.Notifications

module PricingService =

    let notify (title: string) (msg: string) =
        try
            let xml = XmlDocument()
            xml.LoadXml(
                "<toast><visual><binding template=\"ToastText02\">"
                + $"<text id=\"1\">{title}</text><text id=\"2\">{msg}</text>"
                + "</binding></visual></toast>")
            let toast = ToastNotification(xml)
            ToastNotificationManager.CreateToastNotifier("liangwengu").Show(toast)
        with _ -> ()

    let start (onUpdate: PricingSnapshot -> unit) : unit =
        let consecutiveFailures = ref 0

        let fetch () =
            async {
                let! remote = PricingFetcher.tryFetchRemote ()
                match remote with
                | Some snap ->
                    consecutiveFailures.Value <- 0
                    Dispatcher.UIThread.Post(fun () ->
                        PricingFetcher.saveLocalCache snap
                        onUpdate snap)
                | None ->
                    if NetworkInterface.GetIsNetworkAvailable () then
                        incr consecutiveFailures
                        if consecutiveFailures.Value >= 3 then
                            consecutiveFailures.Value <- 0
                            notify "liangwengu" "pricing.json拉取失败，请确认您可以正常访问GitHub"
            } |> Async.Start

        let timer = DispatcherTimer()
        timer.Interval <- TimeSpan.FromMinutes 30.0
        timer.Tick.Add(fun _ -> fetch ())
        timer.Start()
        fetch ()
