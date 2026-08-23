module Liangwengu.Tests.DomainTests

open System
open Liangwengu
open Xunit

// 官方英文版: Peak = 01:00-04:00 / 06:00-10:00 UTC，周一至周五；其余空闲
let policy: PeakPolicy = {
    WeekdaysOnly = true
    Windows = [ { Start = "01:00"; End = "04:00" }; { Start = "06:00"; End = "10:00" } ]
}

// weekdaysOnly=false 的对照策略（旧规则：每天峰谷）
let policyEveryDay: PeakPolicy = { policy with WeekdaysOnly = false }

// 测试用模型表（与 bundled 解耦，保证期望稳定）
let testModels: ModelPrices list = [
    { ModelId = "deepseek-v4-flash"; DisplayName = "Flash";
      Peak =     { InputCacheHit = 0.10m; InputCacheMiss = 3.0m; Output = 9.0m };
      OffPeak =  { InputCacheHit = 0.05m; InputCacheMiss = 1.5m; Output = 4.5m } }
    { ModelId = "deepseek-v4-pro"; DisplayName = "Pro";
      Peak =     { InputCacheHit = 0.30m; InputCacheMiss = 9.0m; Output = 27.0m };
      OffPeak =  { InputCacheHit = 0.15m; InputCacheMiss = 4.5m; Output = 13.5m } }
]

// 2026-08-18 是周二（工作日）；周末用 08-22(六)/08-23(日)/08-24(一)
let utc (h: int) (m: int) : DateTime =
    DateTime(2026, 8, 18, h, m, 0, DateTimeKind.Utc)

let utcOn (month: int) (day: int) (h: int) (m: int) : DateTime =
    DateTime(2026, month, day, h, m, 0, DateTimeKind.Utc)

// ------ periodOf ------

[<Theory>]
[<InlineData(0, 59)>]   // 首峰前 1 分钟
[<InlineData(4, 0)>]    // 晨峰结束
[<InlineData(5, 59)>]   // 午间空闲尾
[<InlineData(10, 0)>]   // 晚峰结束
[<InlineData(23, 30)>]
[<InlineData(0, 0)>]
let ``periodOf 空闲时段`` h m =
    Assert.Equal(OffPeak, Domain.periodOf policy (utc h m))

[<Theory>]
[<InlineData(1, 0)>]    // 首峰开始
[<InlineData(3, 59)>]   // 首峰尾
[<InlineData(6, 0)>]    // 次峰开始
[<InlineData(9, 59)>]   // 次峰尾
let ``periodOf 高峰时段`` h m =
    Assert.Equal(Peak, Domain.periodOf policy (utc h m))

[<Theory>]
[<InlineData(8, 22, 0, 0)>]   // 周六 00:00 UTC
[<InlineData(8, 22, 1, 0)>]   // 周六 01:00 UTC（若非 weekdaysOnly 应为 Peak）
[<InlineData(8, 22, 9, 0)>]   // 周六 09:00 UTC（若非 weekdaysOnly 应为 Peak）
[<InlineData(8, 23, 0, 0)>]   // 周日 00:00 UTC
[<InlineData(8, 23, 6, 30)>]  // 周日 06:30 UTC
let ``periodOf 周末全天空闲`` month day h m =
    Assert.Equal(OffPeak, Domain.periodOf policy (utcOn month day h m))

[<Fact>]
let ``periodOf weekdaysOnly=false 时周末仍判峰`` () =
    // 周六 09:00 UTC 落在 06:00-10:00 窗口内
    Assert.Equal(Peak, Domain.periodOf policyEveryDay (utcOn 8 22 9 0))

// ------ nextSwitch ------

[<Fact>]
let ``nextSwitch 0:59 后 1:00 转峰`` () =
    let p, t = Domain.nextSwitch policy (utc 0 59)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 18, 1, 0, 0), t)

[<Fact>]
let ``nextSwitch 1:00 后 4:00 转谷`` () =
    let p, t = Domain.nextSwitch policy (utc 1 0)
    Assert.Equal(OffPeak, p)
    Assert.Equal(DateTime(2026, 8, 18, 4, 0, 0), t)

[<Fact>]
let ``nextSwitch 3:59 后 4:00 转谷`` () =
    let p, t = Domain.nextSwitch policy (utc 3 59)
    Assert.Equal(OffPeak, p)
    Assert.Equal(DateTime(2026, 8, 18, 4, 0, 0), t)

[<Fact>]
let ``nextSwitch 4:00 后 6:00 转峰`` () =
    let p, t = Domain.nextSwitch policy (utc 4 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 18, 6, 0, 0), t)

[<Fact>]
let ``nextSwitch 6:00 后 10:00 转谷`` () =
    let p, t = Domain.nextSwitch policy (utc 6 0)
    Assert.Equal(OffPeak, p)
    Assert.Equal(DateTime(2026, 8, 18, 10, 0, 0), t)

[<Fact>]
let ``nextSwitch 10:00 后次日 1:00 转峰`` () =
    let p, t = Domain.nextSwitch policy (utc 10 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 19, 1, 0, 0), t)

[<Fact>]
let ``nextSwitch 23:00 后次日 1:00 转峰`` () =
    let p, t = Domain.nextSwitch policy (utc 23 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 19, 1, 0, 0), t)

[<Fact>]
let ``nextSwitch 周五 10:00 后跳到周一 01:00 转峰`` () =
    // 2026-08-21 周五 10:00 UTC → 下一切换是周一 08-24 01:00
    let p, t = Domain.nextSwitch policy (utcOn 8 21 10 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 24, 1, 0, 0), t)

[<Fact>]
let ``nextSwitch 周六任意时刻跳到周一 01:00 转峰`` () =
    let p, t = Domain.nextSwitch policy (utcOn 8 22 12 0)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 24, 1, 0, 0), t)

[<Fact>]
let ``nextSwitch 周日任意时刻跳到周一 01:00 转峰`` () =
    let p, t = Domain.nextSwitch policy (utcOn 8 23 23 30)
    Assert.Equal(Peak, p)
    Assert.Equal(DateTime(2026, 8, 24, 1, 0, 0), t)

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
    let flash = testModels |> List.find (fun m -> m.ModelId = "deepseek-v4-flash")
    Assert.Equal("Flash 输入 未命中¥3.00 命中¥0.10", Domain.inputLine Peak flash)

[<Fact>]
let ``tooltip 峰时完整格式`` () =
    let s = Domain.tooltip Peak (TimeSpan(1, 23, 0)) testModels
    Assert.Equal("梁文\u201C峰\u201D😈 |  距谷还有 1h23m | Flash输出¥9.00 Pro输出¥27.00 /M", s)

[<Fact>]
let ``tooltip 谷时完整格式`` () =
    let s = Domain.tooltip OffPeak (TimeSpan(14, 2, 0)) testModels
    Assert.Equal("梁文\u201C谷\u201D😊 |  距峰还有 14h02m | Flash输出¥4.50 Pro输出¥13.50 /M", s)
