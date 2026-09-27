module Liangwengu.Tests.PricingSchemaTests

open System
open Liangwengu
open Xunit

let validV1 =
    """{
  "schemaVersion": 1,
  "sourceHash": "sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
  "currency": "CNY",
  "peakPolicy": {
    "weekdaysOnly": true,
    "windows": [
      { "start": "01:00", "end": "04:00" },
      { "start": "06:00", "end": "10:00" }
    ]
  },
  "models": [
    {
      "modelId": "deepseek-v4-flash",
      "displayName": "Flash",
      "peak":     { "inputCacheHit": 0.10, "inputCacheMiss": 3.0, "output": 9.0 },
      "offPeak":  { "inputCacheHit": 0.05, "inputCacheMiss": 1.5, "output": 4.5 }
    }
  ]
}"""

let validV2 =
    """{
  "schemaVersion": 2,
  "sourceHash": "sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
  "currency": "CNY",
  "peakPolicy": {
    "timezone": "Asia/Shanghai",
    "weekdaysOnly": true,
    "windows": [
      { "start": "09:00", "end": "12:00" },
      { "start": "14:00", "end": "18:00" }
    ],
    "holidayCalendar": {
      "coveredFrom": "2026-01-01",
      "coveredThrough": "2026-12-31",
      "excludedDates": ["2026-10-01", "2026-10-02"]
    }
  },
  "models": [
    { "modelId": "deepseek-flash", "displayName": "Flash", "peak": { "inputCacheHit": 0.04, "inputCacheMiss": 2, "output": 8 }, "offPeak": { "inputCacheHit": 0.02, "inputCacheMiss": 1, "output": 4 } }
  ]
}"""

let okOrFail r msg =
    match r with
    | Ok v -> v
    | Error e ->
        Assert.Fail($"%s{msg}: %s{e}")
        Unchecked.defaultof<_>

let assertError r =
    match r with
    | Ok _ -> Assert.Fail("expected Error, got Ok")
    | Error _ -> ()

[<Fact>]
let ``parse 合法 v1 JSON 成功`` () =
    let s = okOrFail (PricingSchema.parse validV1) "expected Ok"
    Assert.Equal(1, s.SchemaVersion)
    Assert.Equal("CNY", s.Currency)
    Assert.Equal("UTC", s.PeakPolicy.Timezone)
    Assert.True(s.PeakPolicy.HolidayCalendar.IsNone)
    Assert.True(s.PeakPolicy.WeekdaysOnly)
    Assert.Equal(2, s.PeakPolicy.Windows.Length)
    Assert.Equal("01:00", s.PeakPolicy.Windows.[0].Start)
    Assert.Equal("04:00", s.PeakPolicy.Windows.[0].End)
    Assert.Single(s.Models) |> ignore
    Assert.Equal("deepseek-v4-flash", s.Models.[0].ModelId)
    Assert.Equal("Flash", s.Models.[0].DisplayName)
    Assert.Equal(0.10m, s.Models.[0].Peak.InputCacheHit)
    Assert.Equal(9.0m, s.Models.[0].Peak.Output)
    Assert.Equal(4.5m, s.Models.[0].OffPeak.Output)

[<Fact>]
let ``parse 不支持的 schemaVersion 返回 Error`` () =
    let json = validV1.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99")
    assertError (PricingSchema.parse json)

[<Fact>]
let ``parse 合法 v2 JSON 成功并保留北京时间假日日历`` () =
    let s = okOrFail (PricingSchema.parse validV2) "expected Ok"
    Assert.Equal(2, s.SchemaVersion)
    Assert.Equal("Asia/Shanghai", s.PeakPolicy.Timezone)
    let calendar = s.PeakPolicy.HolidayCalendar.Value
    Assert.Equal("2026-01-01", calendar.CoveredFrom)
    Assert.Equal("2026-12-31", calendar.CoveredThrough)
    Assert.Equal<string list>([ "2026-10-01"; "2026-10-02" ], calendar.ExcludedDates)

[<Fact>]
let ``parse 拒绝 v2 使用非北京时间`` () =
    let json = validV2.Replace("Asia/Shanghai", "UTC")
    assertError (PricingSchema.parse json)

[<Theory>]
[<InlineData("[\"2026-10-02\",\"2026-10-01\"]")>]
[<InlineData("[\"2026-10-01\",\"2026-10-01\"]")>]
[<InlineData("[\"2027-01-01\"]")>]
let ``parse 拒绝无序重复或超出覆盖范围的假期`` dates =
    let json = validV2.Replace("[\"2026-10-01\", \"2026-10-02\"]", dates)
    assertError (PricingSchema.parse json)

[<Fact>]
let ``serialize v1 快照仍输出旧字段布局`` () =
    let snap = okOrFail (PricingSchema.parse validV1) "expected Ok"
    let serialized = PricingSchema.serialize snap
    Assert.Contains("\"schemaVersion\":1", serialized)
    Assert.DoesNotContain("timezone", serialized)
    Assert.True(PricingSchema.tryParse serialized |> Option.isSome)

[<Fact>]
let ``serialize v2 快照往返保留 holidayCalendar`` () =
    let snap = okOrFail (PricingSchema.parse validV2) "expected Ok"
    let serialized = PricingSchema.serialize snap
    Assert.Contains("\"timezone\":\"Asia/Shanghai\"", serialized)
    Assert.Contains("\"holidayCalendar\"", serialized)
    let reparsed = okOrFail (PricingSchema.parse serialized) "round trip expected Ok"
    Assert.Equal(2, reparsed.SchemaVersion)

[<Fact>]
let ``parse 缺少 schemaVersion 返回 Error`` () =
    let json =
        """{
  "sourceHash": "sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
  "currency": "CNY",
  "peakPolicy": { "weekdaysOnly": true, "windows": [{ "start": "01:00", "end": "04:00" }] },
  "models": [{ "modelId": "a", "displayName": "A", "peak": { "inputCacheHit": 1, "inputCacheMiss": 2, "output": 3 }, "offPeak": { "inputCacheHit": 0.5, "inputCacheMiss": 1, "output": 1.5 } }]
}"""

    assertError (PricingSchema.parse json)

[<Fact>]
let ``parse USD 币种也能解析`` () =
    let json = validV1.Replace("\"CNY\"", "\"USD\"")
    let s = okOrFail (PricingSchema.parse json) "expected Ok"
    Assert.Equal("USD", s.Currency)

[<Fact>]
let ``parse 空的 windows 返回 Error`` () =
    let json =
        validV1.Replace(
            """[
      { "start": "01:00", "end": "04:00" },
      { "start": "06:00", "end": "10:00" }
    ]""",
            "[]"
        )

    assertError (PricingSchema.parse json)

[<Fact>]
let ``parse 空的 models 返回 Error`` () =
    let json =
        validV1.Replace(
            """[
    {
      "modelId": "deepseek-v4-flash",
      "displayName": "Flash",
      "peak":     { "inputCacheHit": 0.10, "inputCacheMiss": 3.0, "output": 9.0 },
      "offPeak":  { "inputCacheHit": 0.05, "inputCacheMiss": 1.5, "output": 4.5 }
    }
  ]""",
            "[]"
        )

    assertError (PricingSchema.parse json)

[<Fact>]
let ``parse 非法 JSON 返回 Error`` () =
    assertError (PricingSchema.parse "{ not valid json")

[<Fact>]
let ``tryParse 成功返回 Some`` () =
    Assert.True(PricingSchema.tryParse validV1 |> Option.isSome)

[<Fact>]
let ``tryParse 失败返回 None`` () =
    Assert.True(PricingSchema.tryParse "garbage" |> Option.isNone)

[<Fact>]
let ``parseHHmm 解析小时分钟`` () =
    Assert.Equal(60, PricingSchema.parseHHmm "01:00")
    Assert.Equal(0, PricingSchema.parseHHmm "00:00")
    Assert.Equal(600, PricingSchema.parseHHmm "10:00")
    Assert.Equal(1439, PricingSchema.parseHHmm "23:59")

[<Theory>]
[<InlineData("24:00")>]
[<InlineData("12:60")>]
[<InlineData("1:00")>]
let ``parseHHmm 拒绝越界或格式错误时间`` value =
    Assert.ThrowsAny<Exception>(fun () -> PricingSchema.parseHHmm value |> ignore)
    |> ignore

[<Fact>]
let ``parse 多模型快照`` () =
    let json =
        """{
  "schemaVersion": 1,
  "sourceHash": "sha256:0000000000000000000000000000000000000000000000000000000000000000",
  "currency": "CNY",
  "peakPolicy": { "weekdaysOnly": false, "windows": [{ "start": "01:00", "end": "04:00" }] },
  "models": [
    { "modelId": "a", "displayName": "A", "peak": { "inputCacheHit": 1, "inputCacheMiss": 2, "output": 3 }, "offPeak": { "inputCacheHit": 0.5, "inputCacheMiss": 1, "output": 1.5 } },
    { "modelId": "b", "displayName": "B", "peak": { "inputCacheHit": 4, "inputCacheMiss": 5, "output": 6 }, "offPeak": { "inputCacheHit": 2, "inputCacheMiss": 2.5, "output": 3 } }
  ]
}"""

    let s = okOrFail (PricingSchema.parse json) "expected Ok"
    Assert.Equal(2, s.Models.Length)
    Assert.False(s.PeakPolicy.WeekdaysOnly)
    Assert.Equal("a", s.Models.[0].ModelId)
    Assert.Equal(1m, s.Models.[0].Peak.InputCacheHit)
    Assert.Equal(3m, s.Models.[0].Peak.Output)

[<Fact>]
let ``bundled pricing json 是可解析的 v2 快照`` () =
    let snapshot = PricingFetcher.loadBundled ()
    Assert.Equal(2, snapshot.SchemaVersion)
    Assert.Equal("Asia/Shanghai", snapshot.PeakPolicy.Timezone)
    let calendar = snapshot.PeakPolicy.HolidayCalendar.Value
    Assert.Equal("2026-01-01", calendar.CoveredFrom)
    Assert.Equal("2026-12-31", calendar.CoveredThrough)
    Assert.Contains("2026-10-01", calendar.ExcludedDates)
