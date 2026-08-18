module Liangwengu.Tests.DomainTests

open System
open Liangwengu
open Xunit

// 官方英文版: Peak = 01:00-04:00 / 06:00-10:00 UTC，其余空闲
let utc (h: int) (m: int) : DateTime =
    DateTime(2026, 8, 18, h, m, 0, DateTimeKind.Utc)

// ------ periodOf ------

[<Theory>]
[<InlineData(0, 59)>]   // 首峰前 1 分钟
[<InlineData(4, 0)>]    // 晨峰结束
[<InlineData(5, 59)>]   // 午间空闲尾
[<InlineData(10, 0)>]   // 晚峰结束
[<InlineData(23, 30)>]
[<InlineData(0, 0)>]
let ``periodOf 空闲时段`` h m =
    Assert.Equal(OffPeak, Domain.periodOf (utc h m))

[<Theory>]
[<InlineData(1, 0)>]    // 首峰开始
[<InlineData(3, 59)>]   // 首峰尾
[<InlineData(6, 0)>]    // 次峰开始
[<InlineData(9, 59)>]   // 次峰尾
let ``periodOf 高峰时段`` h m =
    Assert.Equal(Peak, Domain.periodOf (utc h m))

// ------ nextSwitch ------

[<Fact>]
let ``nextSwitch 0:59 后 1:00 转峰`` () =
    let p, t = Domain.nextSwitch (utc 0 59)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 18, 1, 0, 0), t)

[<Fact>]
let ``nextSwitch 1:00 后 4:00 转谷`` () =
    let p, t = Domain.nextSwitch (utc 1 0)
    Assert.Equal(OffPeak, p)
    Assert.Equal(DateTime(2026, 8, 18, 4, 0, 0), t)

[<Fact>]
let ``nextSwitch 3:59 后 4:00 转谷`` () =
    let p, t = Domain.nextSwitch (utc 3 59)
    Assert.Equal(OffPeak, p)
    Assert.Equal(DateTime(2026, 8, 18, 4, 0, 0), t)

[<Fact>]
let ``nextSwitch 4:00 后 6:00 转峰`` () =
    let p, t = Domain.nextSwitch (utc 4 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 18, 6, 0, 0), t)

[<Fact>]
let ``nextSwitch 6:00 后 10:00 转谷`` () =
    let p, t = Domain.nextSwitch (utc 6 0)
    Assert.Equal(OffPeak, p)
    Assert.Equal(DateTime(2026, 8, 18, 10, 0, 0), t)

[<Fact>]
let ``nextSwitch 10:00 后次日 1:00 转峰`` () =
    let p, t = Domain.nextSwitch (utc 10 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 19, 1, 0, 0), t)

[<Fact>]
let ``nextSwitch 23:00 后次日 1:00 转峰`` () =
    let p, t = Domain.nextSwitch (utc 23 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 19, 1, 0, 0), t)

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
    Assert.Equal("😈 峰 · 距谷还有 1h23m", Domain.statusLine Peak (TimeSpan(1, 23, 0)))

[<Fact>]
let ``statusLine 谷含表情与距峰`` () =
    Assert.Equal("😊 谷 · 距峰还有 45m", Domain.statusLine OffPeak (TimeSpan(0, 45, 0)))

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
