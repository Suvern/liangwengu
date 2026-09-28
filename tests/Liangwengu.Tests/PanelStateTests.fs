module Liangwengu.Tests.PanelStateTests

open System
open Liangwengu
open Liangwengu.Presentation
open Xunit

let snapshot =
    { SchemaVersion = 2
      SourceHash = "test"
      Currency = "CNY"
      PeakPolicy = DomainTests.policyChina
      Models = DomainTests.testModels }

let at value = DateTimeOffset.Parse(value)

let state time =
    PanelState.create snapshot (at time) Bundled PanelState.idle

[<Theory>]
[<InlineData("2026-08-18T08:59:30+08:00", false, "不足 1 分钟")>]
[<InlineData("2026-08-18T09:00:00+08:00", true, "3 小时 0 分钟")>]
[<InlineData("2026-08-18T13:02:00+08:00", false, "58 分钟")>]
let ``current prices and countdown share the same instant`` time peak countdown =
    let model = state time
    Assert.Equal((if peak then Peak else OffPeak), model.Period)
    Assert.Equal(countdown, model.Countdown)
    Assert.Equal((if peak then "9.00" else "4.50"), model.Prices.Head.Output)

[<Fact>]
let ``expired calendar hides prices and countdown`` () =
    let model = state "2027-01-01T10:00:00+08:00"
    Assert.Equal(Unknown, model.Period)
    Assert.Empty model.Prices
    Assert.Equal("状态未知", model.Countdown)

[<Fact>]
let ``weekends holidays and cross day boundaries`` () =
    let weekend = state "2026-08-22T10:00:00+08:00"
    Assert.Equal(OffPeak, weekend.Period)
    Assert.Contains("08月24日", weekend.SwitchAt)
    let holiday = state "2026-10-01T10:00:00+08:00"
    Assert.Equal(OffPeak, holiday.Period)
    Assert.Contains("10月08日", holiday.SwitchAt)

[<Fact>]
let ``last covered evening keeps prices without predicting a boundary`` () =
    let model = state "2026-12-31T20:00:00+08:00"
    Assert.NotEmpty model.Prices
    Assert.Equal("下次切换待更新", model.CountdownLabel)

[<Fact>]
let ``sources currency and model rows are dynamic`` () =
    let custom =
        { snapshot with
            Currency = "USD"
            Models = snapshot.Models @ snapshot.Models }

    let now = at "2026-08-18T13:02:00+08:00"
    let model = PanelState.create custom now (Remote now) PanelState.idle
    Assert.Equal(4, model.Prices.Length)
    Assert.Contains("USD", model.Unit)
    Assert.Contains("13:02", model.Source)
    Assert.Equal("本地缓存", (PanelState.create custom now Cache PanelState.idle).Source)
