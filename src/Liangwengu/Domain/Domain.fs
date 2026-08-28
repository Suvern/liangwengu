namespace Liangwengu

open System

/// 计费时段
type Period =
    | Peak
    | OffPeak

module Domain =

    // 峰谷策略来自 PricingSnapshot.PeakPolicy（运行时由拉取/bundled 提供）。
    // timezone 固定 UTC：官方英文版直接以 UTC 表述，DateTime.UtcNow 直读，零时区转换。

    /// UTC 时间 + 峰谷策略 -> 当前计费时段
    /// weekdaysOnly=true 时，周六日整天为 OffPeak。
    let periodOf (policy: PeakPolicy) (utc: DateTime) : Period =
        if
            policy.WeekdaysOnly
            && (utc.DayOfWeek = DayOfWeek.Saturday || utc.DayOfWeek = DayOfWeek.Sunday)
        then
            OffPeak
        else
            let mins = utc.Hour * 60 + utc.Minute

            policy.Windows
            |> List.exists (fun w -> mins >= PricingSchema.parseHHmm w.Start && mins < PricingSchema.parseHHmm w.End)
            |> function
                | true -> Peak
                | false -> OffPeak

    /// UTC 时间 + 峰谷策略 -> 下一次切换的时刻（UTC）与切换后的时段
    /// weekdaysOnly=true 时，周五 10:00 后的下一个切换是周一 01:00（跳过整个周末）。
    let nextSwitch (policy: PeakPolicy) (utc: DateTime) : Period * DateTime =
        let isWeekday (d: DateTime) =
            not policy.WeekdaysOnly
            || (d.DayOfWeek <> DayOfWeek.Saturday && d.DayOfWeek <> DayOfWeek.Sunday)

        // 给定一个日期（0:00），按 windows 生成当日所有边界（升序），过滤出 > utc 的第一个
        let firstBoundaryAfter (date: DateTime) =
            policy.Windows
            |> List.collect (fun w ->
                [ date.AddMinutes(float (PricingSchema.parseHHmm w.Start)), Peak
                  date.AddMinutes(float (PricingSchema.parseHHmm w.End)), OffPeak ])
            |> List.sortBy fst
            |> List.tryFind (fun (t, _) -> t > utc)

        let rec loop (date: DateTime) =
            if isWeekday date then
                match firstBoundaryAfter date with
                | Some(t, p) -> p, t
                | None -> loop (date.AddDays 1.0)
            else
                loop (date.AddDays 1.0)

        loop utc.Date

    let periodEmoji (p: Period) =
        match p with
        | Peak -> "😈"
        | OffPeak -> "😊"

    let periodLabel (p: Period) =
        match p with
        | Peak -> "峰"
        | OffPeak -> "谷"

    let private pricesOf (p: Period) (m: ModelPrices) =
        match p with
        | Peak -> m.Peak
        | OffPeak -> m.OffPeak

    let fmtPrice (d: decimal) : string = d.ToString("0.00")

    /// 剩余时间 -> "1h23m" / "45m"（分钟粒度，不足 1 小时只显示分钟）
    let formatCountdown (ts: TimeSpan) : string =
        let total = max 0 (int ts.TotalMinutes)
        let h, m = total / 60, total % 60
        if h = 0 then $"%d{m}m" else $"%d{h}h%02d{m}m"

    /// 倒计时段: "距谷还有 1h23m"
    let private countdownPart (p: Period) (remaining: TimeSpan) : string =
        let nextLabel =
            match p with
            | Peak -> "谷"
            | OffPeak -> "峰"

        $"距%s{nextLabel}还有 %s{formatCountdown remaining}"

    /// 状态行: "😈 峰 · 距谷还有 1h23m"
    let statusLine (p: Period) (remaining: TimeSpan) : string =
        $"%s{periodEmoji p} %s{periodLabel p} · %s{countdownPart p remaining}"

    /// 模型输入价格行: "Flash 输入 未命中¥3.00 命中¥0.10"
    let inputLine (p: Period) (m: ModelPrices) : string =
        let pr = pricesOf p m
        $"%s{m.DisplayName} 输入 未命中¥%s{fmtPrice pr.InputCacheMiss} 命中¥%s{fmtPrice pr.InputCacheHit}"

    /// Tooltip 单行: 梁文"峰"😈 |  距谷还有 1h23m | Flash输出¥9.00 Pro输出¥27.00 /M
    /// 峰谷随时段切换名字玩梗（梁文峰/梁文谷）
    let tooltip (p: Period) (remaining: TimeSpan) (models: ModelPrices list) : string =
        let pricePart =
            models
            |> List.map (fun m -> $"%s{m.DisplayName}输出¥%s{fmtPrice (pricesOf p m).Output}")
            |> String.concat " "

        $"梁文\u201C{periodLabel p}\u201D{periodEmoji p} |  {countdownPart p remaining} | {pricePart} /M"
