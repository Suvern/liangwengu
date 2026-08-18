namespace Liangwengu

open System

/// 计费时段
type Period =
    | Peak
    | OffPeak

module Domain =

    // 高峰: 北京时间 [9,12) ∪ [14,18)，其余空闲；每天如此
    // 北京时间无夏令时: UTC + 8h 纯算术，不经 TimeZoneInfo

    /// UTC 时间 -> 北京墙时间（Kind 置为 Unspecified，防止误传给关心 Kind 的 API）
    let beijingTime (utc: DateTime) : DateTime =
        DateTime.SpecifyKind(utc.AddHours 8.0, DateTimeKind.Unspecified)

    /// 北京墙时间 -> 当前计费时段
    let periodOf (bj: DateTime) : Period =
        let minutes = bj.Hour * 60 + bj.Minute
        let isPeak = (minutes >= 540 && minutes < 720) || (minutes >= 840 && minutes < 1080)
        if isPeak then Peak else OffPeak

    /// 北京墙时间 -> 下一次切换的时刻（北京墙时间）与切换后的时段
    let nextSwitch (bj: DateTime) : Period * DateTime =
        let date = bj.Date
        let boundaries =
            [ (date.AddHours 9.0, Peak)
              (date.AddHours 12.0, OffPeak)
              (date.AddHours 14.0, Peak)
              (date.AddHours 18.0, OffPeak) ]
        match boundaries |> List.tryFind (fun (t, _) -> t > bj) with
        | Some (t, p) -> p, t
        | None -> Peak, (date.AddDays 1.0).AddHours 9.0

    // ------ 文案格式化（纯函数，可测） ------

    let periodEmoji (p: Period) =
        match p with Peak -> "⛰️" | OffPeak -> "🌾"

    let periodLabel (p: Period) =
        match p with Peak -> "峰" | OffPeak -> "谷"

    let private pricesOf (p: Period) (m: ModelPrices) =
        match p with Peak -> m.Peak | OffPeak -> m.OffPeak

    let fmtPrice (d: decimal) : string = d.ToString("0.00")

    /// 剩余时间 -> "1h23m" / "45m"（分钟粒度，不足 1 小时只显示分钟）
    let formatCountdown (ts: TimeSpan) : string =
        let total = max 0 (int ts.TotalMinutes)
        let h, m = total / 60, total % 60
        if h = 0 then sprintf "%dm" m else sprintf "%dh%02dm" h m

    /// 状态行: "⛰️ 峰 · 距谷 1h23m"
    let statusLine (p: Period) (remaining: TimeSpan) : string =
        let nextLabel = match p with Peak -> "谷" | OffPeak -> "峰"
        sprintf "%s %s · 距%s %s" (periodEmoji p) (periodLabel p) nextLabel (formatCountdown remaining)

    /// 模型输入价格行: "Flash 输入 未命中¥3.00 命中¥0.10"
    let inputLine (p: Period) (m: ModelPrices) : string =
        let pr = pricesOf p m
        sprintf "%s 输入 未命中¥%s 命中¥%s" m.DisplayName (fmtPrice pr.InputCacheMiss) (fmtPrice pr.InputCacheHit)

    /// Tooltip 单行: "梁文谷 | ⛰️ 峰 · 距谷 1h23m | Flash输出¥9.00 Pro输出¥27.00 /M"
    let tooltip (p: Period) (remaining: TimeSpan) (models: ModelPrices list) : string =
        let pricePart =
            models
            |> List.map (fun m -> sprintf "%s输出¥%s" m.DisplayName (fmtPrice (pricesOf p m).Output))
            |> String.concat " "
        sprintf "梁文谷 | %s | %s /M" (statusLine p remaining) pricePart
