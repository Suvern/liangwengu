namespace Liangwengu

open System
open Avalonia.Threading

module PricingService =

    let private consecutiveFailures = ref 0

    let notify (title: string) (msg: string) =
#if WIN32
        try Windows.Notify.show title msg with _ -> ()
#else
        if OperatingSystem.IsMacOS() then
            // macOS: 暂时用 log 输出，后续可添加原生通知方案
            printfn "[Notification] %s: %s" title msg
        else
            failwith "Linux notification is not implemented"
#endif

    let start (onUpdate: PricingSnapshot -> unit) : unit =
        let fetch () =
            async {
                try
                    let! remote = PricingFetcher.tryFetchRemote ()
                    match remote with
                    | Some snap ->
                        consecutiveFailures.Value <- 0
                        Dispatcher.UIThread.Post(fun () ->
                            try
                                PricingFetcher.saveLocalCache snap
                                onUpdate snap
                            with ex ->
                                Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] Pricing update failed: {ex}"))
                    | None ->
                        if System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable () then
                            incr consecutiveFailures
                            if consecutiveFailures.Value >= 3 then
                                consecutiveFailures.Value <- 0
                                notify "liangwengu" "pricing.json拉取失败，请确认您可以正常访问GitHub"
                with ex ->
                    Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] Pricing refresh failed: {ex}")
            } |> Async.Start

        let timer = DispatcherTimer()
        timer.Interval <- TimeSpan.FromMinutes 30.0
        timer.Tick.Add(fun _ -> fetch ())
        timer.Start()
        fetch ()
