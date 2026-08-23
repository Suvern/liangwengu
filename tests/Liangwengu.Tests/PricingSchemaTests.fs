module Liangwengu.Tests.PricingSchemaTests

open Liangwengu
open Xunit

let validV1 = """{
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

let okOrFail r msg =
    match r with
    | Ok v -> v
    | Error e -> Assert.Fail($"%s{msg}: %s{e}"); Unchecked.defaultof<_>

let assertError r =
    match r with
    | Ok _ -> Assert.Fail("expected Error, got Ok")
    | Error _ -> ()

[<Fact>]
let ``parse 合法 v1 JSON 成功`` () =
    let s = okOrFail (PricingSchema.parse validV1) "expected Ok"
    Assert.Equal(1, s.SchemaVersion)
    Assert.Equal("CNY", s.Currency)
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
let ``parse 缺少 schemaVersion 返回 Error`` () =
    let json = validV1.Replace("\"schemaVersion\": 1,\n  ", "")
    assertError (PricingSchema.parse json)

[<Fact>]
let ``parse USD 币种也能解析`` () =
    let json = validV1.Replace("\"CNY\"", "\"USD\"")
    let s = okOrFail (PricingSchema.parse json) "expected Ok"
    Assert.Equal("USD", s.Currency)

[<Fact>]
let ``parse 空的 windows 返回 Error`` () =
    let json = validV1.Replace(
        """[
      { "start": "01:00", "end": "04:00" },
      { "start": "06:00", "end": "10:00" }
    ]""", "[]")
    assertError (PricingSchema.parse json)

[<Fact>]
let ``parse 空的 models 返回 Error`` () =
    let json = validV1.Replace(
        """[
    {
      "modelId": "deepseek-v4-flash",
      "displayName": "Flash",
      "peak":     { "inputCacheHit": 0.10, "inputCacheMiss": 3.0, "output": 9.0 },
      "offPeak":  { "inputCacheHit": 0.05, "inputCacheMiss": 1.5, "output": 4.5 }
    }
  ]""", "[]")
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

[<Fact>]
let ``parse 多模型快照`` () =
    let json = """{
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
