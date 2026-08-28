module Liangwengu.Tests.PricingRefreshStateTests

open Liangwengu.Application
open Xunit

[<Fact>]
let ``成功后重置失败状态`` () =
    Assert.Equal(0, (PricingRefreshState.succeeded ()).ConsecutiveFailures)

[<Fact>]
let ``前两次失败不通知`` () =
    let state, notify = PricingRefreshState.failed PricingRefreshState.initial
    let state, notify = PricingRefreshState.failed state
    Assert.Equal(2, state.ConsecutiveFailures)
    Assert.False(notify)

[<Fact>]
let ``第三次失败通知并重置`` () =
    let state, _ = PricingRefreshState.failed PricingRefreshState.initial
    let state, _ = PricingRefreshState.failed state
    let state, notify = PricingRefreshState.failed state
    Assert.Equal(0, state.ConsecutiveFailures)
    Assert.True(notify)
