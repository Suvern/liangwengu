namespace Liangwengu

open System

/// 计费时段
type Period =
    | Peak
    | OffPeak
    | Unknown

module Domain =

    let private policyTimeZone (policy: PeakPolicy) =
        match policy.Timezone with
        | "UTC" -> TimeZoneInfo.Utc
        | "Asia/Shanghai" ->
            try
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai")
            with :? TimeZoneNotFoundException ->
                TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")
        | timezone -> invalidArg "policy" $"unsupported policy timezone: %s{timezone}"

    let private isWeekend (date: DateTime) =
        date.DayOfWeek = DayOfWeek.Saturday || date.DayOfWeek = DayOfWeek.Sunday

    let private isHoliday (policy: PeakPolicy) (localDate: DateTime) =
        match policy.HolidayCalendar with
        | None -> false
        | Some calendar ->
            let date =
                localDate.ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)

            calendar.ExcludedDates |> List.contains date

    let private calendarCovers (policy: PeakPolicy) (localDate: DateTime) =
        match policy.HolidayCalendar with
        | None -> true
        | Some calendar ->
            let date =
                localDate.ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)

            date >= calendar.CoveredFrom && date <= calendar.CoveredThrough

    let private isPeakDay (policy: PeakPolicy) (localDate: DateTime) =
        (not policy.WeekdaysOnly || not (isWeekend localDate))
        && not (isHoliday policy localDate)

    let private toPolicyTime (policy: PeakPolicy) (instant: DateTimeOffset) =
        TimeZoneInfo.ConvertTime(instant, policyTimeZone policy)

    /// 绝对时刻 + 峰谷策略 -> 当前计费时段。窗口和星期按策略时区解释。
    let periodOf (policy: PeakPolicy) (instant: DateTimeOffset) : Period =
        let local = toPolicyTime policy instant

        if not (calendarCovers policy local.DateTime) then
            Unknown
        elif not (isPeakDay policy local.DateTime) then
            OffPeak
        else
            let mins = local.Hour * 60 + local.Minute

            policy.Windows
            |> List.exists (fun w -> mins >= PricingSchema.parseHHmm w.Start && mins < PricingSchema.parseHHmm w.End)
            |> function
                | true -> Peak
                | false -> OffPeak

    let private localBoundaryToUtc (timezone: TimeZoneInfo) (date: DateTime) (minute: int) =
        let local =
            DateTime.SpecifyKind(date.Date.AddMinutes(float minute), DateTimeKind.Unspecified)

        DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timezone), TimeSpan.Zero)

    /// 绝对时刻 + 峰谷策略 -> 覆盖范围内下一次切换的 UTC 绝对时刻与切换后的时段。
    /// 若当前日历已过期或覆盖范围内没有下一次切换，则返回 None。
    let nextSwitch (policy: PeakPolicy) (instant: DateTimeOffset) : (Period * DateTimeOffset) option =
        let timezone = policyTimeZone policy
        let localNow = TimeZoneInfo.ConvertTime(instant, timezone)

        let firstBoundaryAfter (date: DateTime) =
            policy.Windows
            |> List.collect (fun w ->
                [ localBoundaryToUtc timezone date (PricingSchema.parseHHmm w.Start), Peak
                  localBoundaryToUtc timezone date (PricingSchema.parseHHmm w.End), OffPeak ])
            |> List.sortBy fst
            |> List.tryFind (fun (t, _) -> t > instant.ToUniversalTime())

        let rec loop (date: DateTime) =
            if not (calendarCovers policy date) then
                None
            elif isPeakDay policy date then
                match firstBoundaryAfter date with
                | Some(t, p) -> Some(p, t)
                | None -> loop (date.AddDays 1.0)
            else
                loop (date.AddDays 1.0)

        loop localNow.Date

    let periodEmoji (p: Period) =
        match p with
        | Peak -> "😈"
        | OffPeak -> "😊"
        | Unknown -> "⚠️"

    let periodLabel (p: Period) =
        match p with
        | Peak -> "峰"
        | OffPeak -> "谷"
        | Unknown -> "未知"

    let private pricesOf (p: Period) (m: ModelPrices) =
        match p with
        | Peak -> m.Peak
        | OffPeak -> m.OffPeak
        | Unknown -> invalidArg "p" "prices are unavailable when the holiday calendar is out of coverage"

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
            | Unknown -> "切换"

        $"距%s{nextLabel}还有 %s{formatCountdown remaining}"

    /// 状态行: "😈 峰 · 距谷还有 1h23m"
    let statusLine (p: Period) (remaining: TimeSpan) : string =
        match p with
        | Unknown -> "⚠️ 峰谷未知 · 假日日历待更新"
        | _ -> $"%s{periodEmoji p} %s{periodLabel p} · %s{countdownPart p remaining}"

    let statusLineWithoutSwitch (p: Period) : string =
        match p with
        | Unknown -> "⚠️ 峰谷未知 · 假日日历待更新"
        | _ -> $"%s{periodEmoji p} %s{periodLabel p} · 下次切换待更新"

    /// 模型输入价格行: "Flash 输入 未命中¥3.00 命中¥0.10"
    let inputLine (p: Period) (m: ModelPrices) : string =
        match p with
        | Unknown -> $"%s{m.DisplayName} 输入 价格未知"
        | _ ->
            let pr = pricesOf p m
            $"%s{m.DisplayName} 输入 未命中¥%s{fmtPrice pr.InputCacheMiss} 命中¥%s{fmtPrice pr.InputCacheHit}"

    /// Tooltip 单行: 梁文"峰"😈 |  距谷还有 1h23m | Flash输出¥9.00 Pro输出¥27.00 /M
    /// 峰谷随时段切换名字玩梗（梁文峰/梁文谷）
    let private tooltipWithCountdown (p: Period) (remaining: TimeSpan) (models: ModelPrices list) : string =
        let pricePart =
            models
            |> List.map (fun m -> $"%s{m.DisplayName}输出¥%s{fmtPrice (pricesOf p m).Output}")
            |> String.concat " "

        $"梁文\u201C{periodLabel p}\u201D{periodEmoji p} |  {countdownPart p remaining} | {pricePart} /M"

    let tooltip (p: Period) (remaining: TimeSpan) (models: ModelPrices list) : string =
        if p = Unknown then
            "假日日历已超出覆盖范围，当前峰谷状态和价格未知"
        else
            tooltipWithCountdown p remaining models

    let tooltipWithoutSwitch (p: Period) (models: ModelPrices list) : string =
        if p = Unknown then
            "假日日历已超出覆盖范围，当前峰谷状态和价格未知"
        else
            let pricePart =
                models
                |> List.map (fun m -> $"%s{m.DisplayName}输出¥%s{fmtPrice (pricesOf p m).Output}")
                |> String.concat " "

            $"梁文\u201C{periodLabel p}\u201D{periodEmoji p} |  下次切换待更新 | {pricePart} /M"
