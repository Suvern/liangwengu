namespace Liangwengu

open System

/// 计费时段
type Period =
    | Peak
    | OffPeak

module Domain =

    // 官方英文版定价页直接以 UTC 表述峰谷:
    //   Peak hours are 01:00-04:00 and 06:00-10:00 UTC (all other hours are off-peak)
    //   （等价于中文版的北京时间 9:00-12:00 / 14:00-18:00，两者 -8h 关系）
    // 因此全部以 UTC 计算，无需任何时区转换。

    /// UTC 时间 -> 当前计费时段
    let periodOf (utc: DateTime) : Period =
        let minutes = utc.Hour * 60 + utc.Minute
        let isPeak = (minutes >= 60 && minutes < 240) || (minutes >= 360 && minutes < 600)
        if isPeak then Peak else OffPeak

    /// UTC 时间 -> 下一次切换的时刻（UTC）与切换后的时段
    let nextSwitch (utc: DateTime) : Period * DateTime =
        let date = utc.Date
        let boundaries =
            [ (date.AddHours 1.0, Peak)
              (date.AddHours 4.0, OffPeak)
              (date.AddHours 6.0, Peak)
              (date.AddHours 10.0, OffPeak) ]
        match boundaries |> List.tryFind (fun (t, _) -> t > utc) with
        | Some (t, p) -> p, t
        | None -> Peak, (date.AddDays 1.0).AddHours 1.0

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
        if h = 0 then $"{m}m" else $"{h}h{m:D2}m"

    /// 状态行: "⛰️ 峰 · 距谷 1h23m"
    let statusLine (p: Period) (remaining: TimeSpan) : string =
        let nextLabel = match p with Peak -> "谷" | OffPeak -> "峰"
        $"{periodEmoji p} {periodLabel p} · 距{nextLabel} {formatCountdown remaining}"

    /// 模型输入价格行: "Flash 输入 未命中¥3.00 命中¥0.10"
    let inputLine (p: Period) (m: ModelPrices) : string =
        let pr = pricesOf p m
        $"{m.DisplayName} 输入 未命中¥{fmtPrice pr.InputCacheMiss} 命中¥{fmtPrice pr.InputCacheHit}"

    /// Tooltip 单行: "梁文谷 | ⛰️ 峰 · 距谷 1h23m | Flash输出¥9.00 Pro输出¥27.00 /M"
    let tooltip (p: Period) (remaining: TimeSpan) (models: ModelPrices list) : string =
        let pricePart =
            models
            |> List.map (fun m -> $"{m.DisplayName}输出¥{fmtPrice (pricesOf p m).Output}")
            |> String.concat " "
        $"梁文谷 | {statusLine p remaining} | {pricePart} /M"
