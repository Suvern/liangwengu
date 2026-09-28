namespace Liangwengu

open System
open System.Threading
open System.Threading.Tasks
open Liangwengu.Application

type RefreshEvent =
    | RefreshStarted
    | RefreshSucceeded of PricingSnapshot * DateTimeOffset
    | RefreshFailed of message: string * shouldNotify: bool

/// Network and time dependencies are injected; only one request can be active.
type PricingRefreshService
    (
        fetch: unit -> Async<PricingSnapshot option>,
        persist: PricingSnapshot -> unit,
        clock: unit -> DateTimeOffset,
        online: unit -> bool
    ) as this =
    let gate = obj ()
    let changed = Event<RefreshEvent>()
    let cancellation = new CancellationTokenSource()
    let mutable disposed = false
    let mutable running: Task option = None
    let mutable failures = PricingRefreshState.initial
    let mutable timer: Timer option = None

    member _.Changed = changed.Publish

    member _.Refresh(manual: bool) : Task =
        lock gate (fun () ->
            if disposed then
                Task.CompletedTask
            else
                match running with
                | Some task -> task
                | None ->
                    let completion =
                        TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)

                    running <- Some completion.Task
                    changed.Trigger RefreshStarted

                    let work =
                        task {
                            try
                                let! result = Async.StartAsTask(fetch (), cancellationToken = cancellation.Token)

                                lock gate (fun () ->
                                    if not disposed then
                                        match result with
                                        | Some snapshot ->
                                            failures <- PricingRefreshState.succeeded ()

                                            try
                                                persist snapshot
                                            with ex ->
                                                Console.Error.WriteLine("Pricing cache: " + ex.Message)

                                            changed.Trigger(RefreshSucceeded(snapshot, clock ()))
                                        | None ->
                                            let notify =
                                                if manual || not (online ()) then
                                                    false
                                                else
                                                    let next, notify = PricingRefreshState.failed failures
                                                    failures <- next
                                                    notify

                                            changed.Trigger(RefreshFailed("同步失败，正在使用上次数据", notify)))
                            with
                            | :? OperationCanceledException -> ()
                            | ex ->
                                lock gate (fun () ->
                                    if not disposed then
                                        changed.Trigger(RefreshFailed("同步失败：" + ex.Message, false)))

                            lock gate (fun () -> running <- None)
                            completion.TrySetResult() |> ignore
                        }

                    work |> ignore
                    completion.Task)

    member _.StartPeriodic(interval: TimeSpan) =
        lock gate (fun () ->
            if not disposed && timer.IsNone then
                timer <- Some(new Timer((fun _ -> this.Refresh(false) |> ignore), null, interval, interval)))

    interface IDisposable with
        member _.Dispose() =
            lock gate (fun () ->
                if not disposed then
                    disposed <- true
                    timer |> Option.iter (fun value -> value.Dispose())
                    cancellation.Cancel())

            cancellation.Dispose()
