module Liangwengu.Tests.PanelServiceTests

open System
open System.IO
open System.Threading.Tasks
open Liangwengu
open Liangwengu.Application
open Xunit

let now () =
    DateTimeOffset.Parse("2026-08-18T13:02:00+08:00")

[<Fact>]
let ``concurrent refresh requests share one operation and commit once`` () =
    task {
        let response =
            TaskCompletionSource<PricingSnapshot option>(TaskCreationOptions.RunContinuationsAsynchronously)

        let mutable saves = 0
        let mutable fetches = 0

        let fetch () =
            async {
                fetches <- fetches + 1
                return! response.Task |> Async.AwaitTask
            }

        use service =
            new PricingRefreshService(fetch, (fun _ -> saves <- saves + 1), now, (fun () -> true))

        let events = ResizeArray<RefreshEvent>()
        use subscription = service.Changed.Subscribe events.Add
        let first = service.Refresh false
        let second = service.Refresh true
        Assert.Same(first, second)
        response.SetResult(Some PanelStateTests.snapshot)
        do! first.WaitAsync(TimeSpan.FromSeconds 5.)
        Assert.Equal(1, fetches)
        Assert.Equal(1, saves)
        Assert.Equal(2, events.Count)

        match events[1] with
        | RefreshSucceeded(snapshot, at) ->
            Assert.Equal(now (), at)
            Assert.Equal(PanelStateTests.snapshot, snapshot)
        | other -> failwithf "Unexpected %A" other
    }

[<Fact>]
let ``manual failures do not count toward background failure notifications`` () =
    task {
        use service =
            new PricingRefreshService((fun () -> async { return None }), ignore, now, (fun () -> true))

        let notices = ResizeArray<bool>()

        use subscription =
            service.Changed.Subscribe (function
                | RefreshFailed(_, flag) -> notices.Add flag
                | _ -> ())

        do! service.Refresh false
        do! service.Refresh true
        do! service.Refresh true
        do! service.Refresh false
        do! service.Refresh false
        Assert.Equal<bool list>([ false; false; false; false; true ], List.ofSeq notices)
    }

[<Fact>]
let ``offline failures preserve data and never notify`` () =
    task {
        let mutable persisted = false

        use service =
            new PricingRefreshService(
                (fun () -> async { return None }),
                (fun _ -> persisted <- true),
                now,
                (fun () -> false)
            )

        let notices = ResizeArray<bool>()

        use subscription =
            service.Changed.Subscribe (function
                | RefreshFailed(_, flag) -> notices.Add flag
                | _ -> ())

        for _ in 1..4 do
            do! service.Refresh false

        Assert.False persisted
        Assert.All(notices, fun flag -> Assert.False flag)
    }

[<Fact>]
let ``disposing suppresses late results and future work`` () =
    task {
        let response =
            TaskCompletionSource<PricingSnapshot option>(TaskCreationOptions.RunContinuationsAsynchronously)

        let mutable persisted = false

        let service =
            new PricingRefreshService(
                (fun () -> async { return! response.Task |> Async.AwaitTask }),
                (fun _ -> persisted <- true),
                now,
                (fun () -> true)
            )

        let events = ResizeArray<RefreshEvent>()
        use subscription = service.Changed.Subscribe events.Add
        let pending = service.Refresh false
        (service :> IDisposable).Dispose()
        response.TrySetResult(Some PanelStateTests.snapshot) |> ignore
        do! pending.WaitAsync(TimeSpan.FromSeconds 5.)
        do! service.Refresh true
        Assert.False persisted
        Assert.Single(events) |> ignore
    }

[<Fact>]
let ``exceptions finish busy state and allow retry`` () =
    task {
        let mutable attempts = 0

        use service =
            new PricingRefreshService(
                (fun () ->
                    async {
                        attempts <- attempts + 1

                        if attempts = 1 then
                            return raise (TimeoutException "timeout")
                        else
                            return Some PanelStateTests.snapshot
                    }),
                ignore,
                now,
                (fun () -> true)
            )

        let events = ResizeArray<RefreshEvent>()
        use subscription = service.Changed.Subscribe events.Add
        do! service.Refresh true
        do! service.Refresh true
        Assert.Equal(4, events.Count)

        match events[3] with
        | RefreshSucceeded _ -> ()
        | _ -> failwith "retry did not succeed"
    }

[<Fact>]
let ``settings recover from missing corrupt and unwritable files`` () =
    let directory =
        Path.Combine(Path.GetTempPath(), "liangwengu-tests-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory directory |> ignore
    let file = Path.Combine(directory, "settings.json")

    try
        Assert.False((UserSettings.load file).Reminders)
        File.WriteAllText(file, "broken")
        Assert.False((UserSettings.load file).Reminders)
        Assert.Equal(Ok(), UserSettings.save file { Reminders = true })
        Assert.True((UserSettings.load file).Reminders)
        Assert.True((UserSettings.save (Path.Combine(file, "impossible.json")) { Reminders = true }).IsError)
    finally
        if File.Exists file then
            File.Delete file

        Directory.Delete directory

[<Theory>]
[<InlineData("2026-08-18T08:59:00+08:00", "2026-08-18T09:00:00+08:00", true)>]
[<InlineData("2026-08-18T11:59:00+08:00", "2026-08-18T12:00:00+08:00", true)>]
[<InlineData("2026-08-18T11:59:00+08:00", "2026-08-18T20:00:00+08:00", true)>]
[<InlineData("2026-08-18T08:59:00+08:00", "2026-08-18T20:00:00+08:00", false)>]
[<InlineData("2026-08-18T09:00:00+08:00", "2026-08-18T08:59:00+08:00", false)>]
[<InlineData("2026-12-31T17:59:00+08:00", "2027-01-01T10:00:00+08:00", false)>]
let ``time transitions notify once without replaying history`` first second expected =
    let policy = PanelStateTests.snapshot.PeakPolicy
    let baseline = PeriodReminder.baseline policy (DateTimeOffset.Parse first)

    let next, notice =
        PeriodReminder.advance policy true (DateTimeOffset.Parse second) baseline

    Assert.Equal(expected, notice.IsSome)

    let _, repeated =
        PeriodReminder.advance policy true (DateTimeOffset.Parse second) next

    Assert.True repeated.IsNone

    let _, disabled =
        PeriodReminder.advance policy false (DateTimeOffset.Parse second) baseline

    Assert.True disabled.IsNone

[<Fact>]
let ``rebasing on startup settings or snapshot changes never emits a notice`` () =
    let policy = PanelStateTests.snapshot.PeakPolicy
    let at = DateTimeOffset.Parse "2026-08-18T10:00:00+08:00"
    let baseline = PeriodReminder.baseline policy at
    let _, notice = PeriodReminder.advance policy true at baseline
    Assert.True notice.IsNone

[<Fact>]
let ``permission rejection never persists an enabled setting`` () =
    task {
        let mutable saved = false

        let! result =
            ReminderSettings.change
                (fun () -> Task.FromResult(Error "denied"))
                (fun _ ->
                    saved <- true
                    Ok())
                true

        Assert.Equal(Error "denied", result)
        Assert.False saved
    }

[<Fact>]
let ``disabling bypasses permission and persistence failure rolls back`` () =
    task {
        let mutable requested = false

        let permission () =
            requested <- true
            Task.FromResult(Ok())

        let! disabled =
            ReminderSettings.change
                permission
                (fun value ->
                    Assert.False value
                    Ok())
                false

        Assert.Equal(Ok false, disabled)
        Assert.False requested
        let! failed = ReminderSettings.change permission (fun _ -> Error "disk full") true
        Assert.Equal(Error "disk full", failed)
        Assert.True requested
    }
