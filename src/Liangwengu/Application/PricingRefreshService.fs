namespace Liangwengu

open System
open Avalonia.Threading

module PricingService =

    let start (onUpdate: PricingSnapshot -> unit) : unit =
        let mutable refreshState = Application.PricingRefreshState.initial

        let fetch () =
            async {
                try
                    let! remote = PricingFetcher.tryFetchRemote ()

                    match remote with
                    | Some snap ->
                        refreshState <- Application.PricingRefreshState.succeeded ()

                        Dispatcher.UIThread.Post(fun () ->
                            try
                                PricingFetcher.saveLocalCache snap
                                onUpdate snap
                            with ex ->
                                Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] Pricing update failed: {ex}"))
                    | None ->
                        if System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable() then
                            let state, shouldNotify = Application.PricingRefreshState.failed refreshState
                            refreshState <- state

                            if shouldNotify then
                                Platform.Notification.show "liangwengu" "pricing.json拉取失败，请确认您可以正常访问GitHub"
                with ex ->
                    Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] Pricing refresh failed: {ex}")
            }
            |> Async.Start

        let timer = DispatcherTimer()
        timer.Interval <- TimeSpan.FromMinutes 30.0
        timer.Tick.Add(fun _ -> fetch ())
        timer.Start()
        fetch ()
