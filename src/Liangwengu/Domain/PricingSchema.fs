namespace Liangwengu

open System.Text.Json
open System.Text.Json.Serialization

type PeriodPrices =
    { InputCacheHit: decimal
      InputCacheMiss: decimal
      Output: decimal }

type ModelPrices =
    { ModelId: string
      DisplayName: string
      Peak: PeriodPrices
      OffPeak: PeriodPrices }

type TimeWindow = { Start: string; End: string }

type PeakPolicy =
    { WeekdaysOnly: bool
      Windows: TimeWindow list }

type PricingSnapshot =
    { SchemaVersion: int
      SourceHash: string
      Currency: string
      PeakPolicy: PeakPolicy
      Models: ModelPrices list }

module PricingSchema =

    let MAX_SUPPORTED_SCHEMA_VERSION = 1

    let parseHHmm (s: string) : int =
        let parts = s.Split(':')

        if parts.Length <> 2 then
            failwith $"invalid HH:mm: %s{s}"

        int parts[0] * 60 + int parts[1]

    let private options =
        let o = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
        o.Converters.Add(JsonFSharpConverter())
        o

    let parse (json: string) : Result<PricingSnapshot, string> =
        try
            use doc = JsonDocument.Parse(json)
            let ver = doc.RootElement.GetProperty("schemaVersion").GetInt32()

            if ver > MAX_SUPPORTED_SCHEMA_VERSION then
                Error $"unsupported schemaVersion %d{ver} (max supported: %d{MAX_SUPPORTED_SCHEMA_VERSION})"
            else
                let snap = JsonSerializer.Deserialize<PricingSnapshot>(json, options)

                if snap.Models.IsEmpty then
                    Error "models must be non-empty"
                elif snap.PeakPolicy.Windows.IsEmpty then
                    Error "windows must be non-empty"
                else
                    Ok snap
        with ex ->
            Error ex.Message

    let tryParse (json: string) : PricingSnapshot option =
        match parse json with
        | Ok s -> Some s
        | Error _ -> None
