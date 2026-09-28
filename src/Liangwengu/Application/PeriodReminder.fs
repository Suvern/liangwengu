namespace Liangwengu.Application

open System
open Liangwengu

type ReminderBaseline =
    { Instant: DateTimeOffset
      Period: Period
      Boundary: DateTimeOffset option }

module PeriodReminder =
    let baseline policy instant =
        let period = Domain.periodOf policy instant

        { Instant = instant
          Period = period
          Boundary =
            if period = Unknown then
                None
            else
                Domain.nextSwitch policy instant |> Option.map snd }

    /// Re-baseline explicitly on startup, settings changes and new snapshots.
    let advance policy enabled instant previous =
        let next = baseline policy instant

        let notify =
            enabled
            && previous.Period <> Unknown
            && next.Period <> Unknown
            && previous.Period <> next.Period
            && instant > previous.Instant
            && (previous.Boundary |> Option.exists (fun boundary -> boundary <= instant))

        next, (if notify then Some next.Period else None)
