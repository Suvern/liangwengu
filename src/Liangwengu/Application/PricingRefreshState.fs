namespace Liangwengu.Application

type RefreshState = {
    ConsecutiveFailures: int
}

module PricingRefreshState =
    let initial = { ConsecutiveFailures = 0 }

    let succeeded () = initial

    let failed state =
        let failures = state.ConsecutiveFailures + 1
        if failures >= 3 then
            initial, true
        else
            { ConsecutiveFailures = failures }, false
