namespace Liangwengu.Presentation

open System
open System.Globalization
open Liangwengu

type DataSource =
    | Bundled
    | Cache
    | Remote of DateTimeOffset

type PanelOperations =
    { Refreshing: bool
      Autostart: bool
      AutostartBusy: bool
      Reminders: bool
      RemindersBusy: bool
      Error: string option }

type PriceRow =
    { Name: string
      Miss: string
      Hit: string
      Output: string }

type PanelState =
    { Period: Period
      Title: string
      Status: string
      CountdownLabel: string
      Countdown: string
      SwitchAt: string
      Prices: PriceRow list
      Unit: string
      Source: string
      Tooltip: string
      Operations: PanelOperations }

module PanelState =
    let idle =
        { Refreshing = false
          Autostart = false
          AutostartBusy = false
          Reminders = false
          RemindersBusy = false
          Error = None }

    let private china (instant: DateTimeOffset) = instant.ToOffset(TimeSpan.FromHours 8.)

    let countdown (remaining: TimeSpan) =
        if remaining.TotalMinutes < 1. then
            "不足 1 分钟"
        else
            let minutes = int (Math.Ceiling remaining.TotalMinutes)
            let days, hours, mins = minutes / 1440, (minutes / 60) % 24, minutes % 60

            if days > 0 then $"{days} 天 {hours} 小时 {mins} 分钟"
            elif hours > 0 then $"{hours} 小时 {mins} 分钟"
            else $"{mins} 分钟"

    let create (snapshot: PricingSnapshot) now source operations =
        let period = Domain.periodOf snapshot.PeakPolicy now

        let next =
            if period = Unknown then
                None
            else
                Domain.nextSwitch snapshot.PeakPolicy now

        let title, status =
            match period with
            | Peak -> "梁文峰", "高峰时段"
            | OffPeak -> "梁文谷", "空闲时段"
            | Unknown -> "梁文谷", "状态未知"

        let label, remaining, boundary =
            match next with
            | Some(nextPeriod, instant) ->
                let local = china instant

                let format =
                    if local.Date = (china now).Date then
                        "HH:mm"
                    else
                        "MM月dd日 HH:mm"

                (if nextPeriod = Peak then "距高峰还有" else "距空闲还有"),
                countdown (instant - now),
                local.ToString(format, CultureInfo.InvariantCulture) + " 切换 · 北京时间"
            | None when period = Unknown -> "假日日历待更新", "状态未知", "更新数据后重试"
            | None -> "下次切换待更新", "—", "当前价格仍有效"

        let fmt (price: decimal) =
            price.ToString("0.00", CultureInfo.InvariantCulture)

        let rows =
            if period = Unknown then
                []
            else
                snapshot.Models
                |> List.map (fun model ->
                    let prices = if period = Peak then model.Peak else model.OffPeak

                    { Name = model.DisplayName
                      Miss = fmt prices.InputCacheMiss
                      Hit = fmt prices.InputCacheHit
                      Output = fmt prices.Output })

        let sourceText =
            match source with
            | Bundled -> "内置数据"
            | Cache -> "本地缓存"
            | Remote at -> "最近同步于 " + (china at).ToString("MM-dd HH:mm") + " · 北京时间"

        let tooltip =
            if period = Unknown then
                "假日日历已超出覆盖范围，当前峰谷状态和价格未知"
            else
                let outputs =
                    rows
                    |> List.map (fun row -> $"{row.Name} 输出 {row.Output}")
                    |> String.concat " · "

                $"{title} · {status} | {label} {remaining} | {outputs} {snapshot.Currency}/百万 tokens"

        { Period = period
          Title = title
          Status = status
          CountdownLabel = label
          Countdown = remaining
          SwitchAt = boundary
          Prices = rows
          Unit = (if snapshot.Currency = "CNY" then "元" else snapshot.Currency) + " / 百万 tokens"
          Source = sourceText
          Tooltip = tooltip
          Operations = operations }
