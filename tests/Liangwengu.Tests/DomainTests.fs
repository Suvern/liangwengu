module Liangwengu.Tests.DomainTests

open System
open Liangwengu
open Xunit

let bj (h: int) (m: int) : DateTime =
    DateTime(2026, 8, 18, h, m, 0, DateTimeKind.Unspecified)

// ------ beijingTime ------

[<Fact>]
let ``beijingTime 加 8 小时且跨日`` () =
    let utc = DateTime(2026, 8, 18, 16, 0, 0, DateTimeKind.Utc)
    let result = Domain.beijingTime utc
    Assert.Equal(DateTime(2026, 8, 19, 0, 0, 0, DateTimeKind.Unspecified), result)

[<Fact>]
let ``beijingTime Kind 被清除`` () =
    let utc = DateTime(2026, 8, 18, 1, 2, 3, DateTimeKind.Utc)
    Assert.Equal(DateTimeKind.Unspecified, (Domain.beijingTime utc).Kind)

// ------ periodOf ------

[<Theory>]
[<InlineData(8, 59)>]   // 峰前 1 分钟
[<InlineData(12, 0)>]   // 午间峰结束
[<InlineData(13, 59)>]  // 午间空闲尾
[<InlineData(18, 0)>]   // 傍晚峰结束
[<InlineData(23, 30)>]
[<InlineData(0, 0)>]
let ``periodOf 空闲时段`` h m =
    Assert.Equal(OffPeak, Domain.periodOf (bj h m))

[<Theory>]
[<InlineData(9, 0)>]    // 上午峰开始
[<InlineData(11, 59)>]  // 上午峰尾
[<InlineData(14, 0)>]   // 下午峰开始
[<InlineData(17, 59)>]  // 下午峰尾
let ``periodOf 高峰时段`` h m =
    Assert.Equal(Peak, Domain.periodOf (bj h m))

// ------ nextSwitch ------

[<Fact>]
let ``nextSwitch 8:59 后 9:00 转峰`` () =
    let p, t = Domain.nextSwitch (bj 8 59)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 18, 9, 0, 0), t)

[<Fact>]
let ``nextSwitch 9:00 后 12:00 转谷`` () =
    let p, t = Domain.nextSwitch (bj 9 0)
    Assert.Equal(OffPeak, p)
    Assert.Equal(DateTime(2026, 8, 18, 12, 0, 0), t)

[<Fact>]
let ``nextSwitch 11:59 后 12:00 转谷`` () =
    let p, t = Domain.nextSwitch (bj 11 59)
    Assert.Equal(OffPeak, p)
    Assert.Equal(DateTime(2026, 8, 18, 12, 0, 0), t)

[<Fact>]
let ``nextSwitch 12:00 后 14:00 转峰`` () =
    let p, t = Domain.nextSwitch (bj 12 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 18, 14, 0, 0), t)

[<Fact>]
let ``nextSwitch 14:00 后 18:00 转谷`` () =
    let p, t = Domain.nextSwitch (bj 14 0)
    Assert.Equal(OffPeak, p)
    Assert.Equal(DateTime(2026, 8, 18, 18, 0, 0), t)

[<Fact>]
let ``nextSwitch 18:00 后次日 9:00 转峰`` () =
    let p, t = Domain.nextSwitch (bj 18 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 19, 9, 0, 0), t)

[<Fact>]
let ``nextSwitch 23:00 后次日 9:00 转峰`` () =
    let p, t = Domain.nextSwitch (bj 23 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 19, 9, 0, 0), t)

// ------ formatCountdown ------

[<Theory>]
[<InlineData(0, 0, "0m")>]
[<InlineData(0, 45, "45m")>]
[<InlineData(1, 23, "1h23m")>]
[<InlineData(14, 2, "14h02m")>]
let ``formatCountdown 格式化`` h m expected =
    Assert.Equal(expected, Domain.formatCountdown (TimeSpan(h, m, 0)))

// ------ 文案 ------

[<Fact>]
let ``statusLine 峰含表情与距谷`` () =
    Assert.Equal("⛰️ 峰 · 距谷 1h23m", Domain.statusLine Peak (TimeSpan(1, 23, 0)))

[<Fact>]
let ``statusLine 谷含表情与距峰`` () =
    Assert.Equal("🌾 谷 · 距峰 45m", Domain.statusLine OffPeak (TimeSpan(0, 45, 0)))

[<Fact>]
let ``inputLine 峰时输入价格`` () =
    let flash = Prices.all |> List.find (fun m -> m.ModelId = "deepseek-v4-flash")
    Assert.Equal("Flash 输入 未命中¥3.00 命中¥0.10", Domain.inputLine Peak flash)

[<Fact>]
let ``tooltip 含各模型输出价`` () =
    let s = Domain.tooltip OffPeak (TimeSpan(14, 2, 0)) Prices.all
    Assert.Contains("Flash输出¥4.50", s)
    Assert.Contains("Pro输出¥13.50", s)
    Assert.Contains("距峰", s)
