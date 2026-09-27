namespace Liangwengu

open System
open System.Globalization
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.RegularExpressions

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

type HolidayCalendar =
    { CoveredFrom: string
      CoveredThrough: string
      ExcludedDates: string list }

type PeakPolicy =
    { Timezone: string
      WeekdaysOnly: bool
      Windows: TimeWindow list
      HolidayCalendar: HolidayCalendar option }

type PricingSnapshot =
    { SchemaVersion: int
      SourceHash: string
      Currency: string
      PeakPolicy: PeakPolicy
      Models: ModelPrices list }

type private V1PeakPolicy =
    { WeekdaysOnly: bool
      Windows: TimeWindow list }

type private V1PricingSnapshot =
    { SchemaVersion: int
      SourceHash: string
      Currency: string
      PeakPolicy: V1PeakPolicy
      Models: ModelPrices list }

type private V2PeakPolicy =
    { Timezone: string
      WeekdaysOnly: bool
      Windows: TimeWindow list
      HolidayCalendar: HolidayCalendar }

type private V2PricingSnapshot =
    { SchemaVersion: int
      SourceHash: string
      Currency: string
      PeakPolicy: V2PeakPolicy
      Models: ModelPrices list }

module PricingSchema =

    let MAX_SUPPORTED_SCHEMA_VERSION = 2

    let private supportedSchemaVersions = Set.ofList [ 1; 2 ]

    let private sourceHashPattern =
        Regex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)

    let parseHHmm (s: string) : int =
        if
            String.IsNullOrEmpty(s)
            || s.Length <> 5
            || s[2] <> ':'
            || not (
                Char.IsAsciiDigit(s[0])
                && Char.IsAsciiDigit(s[1])
                && Char.IsAsciiDigit(s[3])
                && Char.IsAsciiDigit(s[4])
            )
        then
            failwith $"invalid HH:mm: %s{s}"

        let hours = Int32.Parse(s.AsSpan(0, 2), CultureInfo.InvariantCulture)
        let minutes = Int32.Parse(s.AsSpan(3, 2), CultureInfo.InvariantCulture)

        if hours > 23 || minutes > 59 then
            failwith $"invalid HH:mm: %s{s}"

        hours * 60 + minutes

    let private options =
        let o =
            JsonSerializerOptions(
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            )

        o.Converters.Add(JsonFSharpConverter())
        o

    let private deserialize<'a> (json: string) =
        let value = JsonSerializer.Deserialize<'a>(json, options)

        if obj.ReferenceEquals(value, null) then
            failwith "snapshot must be a JSON object"

        value

    let private parseDate (field: string) (value: string) =
        match DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, parsed -> parsed
        | _ -> failwith $"%s{field} must use YYYY-MM-DD format"

    let private validateWindows (windows: TimeWindow list) =
        if windows.IsEmpty then
            failwith "windows must be non-empty"

        let ranges =
            windows
            |> List.map (fun window ->
                let start = parseHHmm window.Start
                let finish = parseHHmm window.End

                if finish <= start then
                    failwith "window end must be later than start"

                start, finish)
            |> List.sortBy fst

        ranges
        |> List.pairwise
        |> List.iter (fun ((_, previousEnd), (nextStart, _)) ->
            if nextStart < previousEnd then
                failwith "windows must not overlap")

    let private validateCalendar (calendar: HolidayCalendar) =
        let coveredFrom = parseDate "holidayCalendar.coveredFrom" calendar.CoveredFrom

        let coveredThrough =
            parseDate "holidayCalendar.coveredThrough" calendar.CoveredThrough

        if coveredFrom > coveredThrough then
            failwith "holidayCalendar.coveredFrom must be on or before coveredThrough"

        let dates =
            calendar.ExcludedDates |> List.map (parseDate "holidayCalendar.excludedDates[]")

        if dates <> (dates |> List.distinct |> List.sort) then
            failwith "holidayCalendar.excludedDates must be sorted and unique"

        if dates |> List.exists (fun date -> date < coveredFrom || date > coveredThrough) then
            failwith "holidayCalendar.excludedDates must be within the covered range"

    let private validateCommon
        (schemaVersion: int)
        (sourceHash: string)
        (currency: string)
        (windows: TimeWindow list)
        (models: ModelPrices list)
        =
        if not (supportedSchemaVersions.Contains(schemaVersion)) then
            failwith $"unsupported schemaVersion %d{schemaVersion}"

        if not (sourceHashPattern.IsMatch(sourceHash)) then
            failwith "sourceHash must be sha256 followed by 64 lowercase hexadecimal characters"

        if currency <> "CNY" && (schemaVersion = 2 || currency <> "USD") then
            failwith "v1 currency must be CNY or USD; v2 currency must be CNY"

        validateWindows windows

        if models.IsEmpty then
            failwith "models must be non-empty"

        if
            models
            |> List.exists (fun model ->
                String.IsNullOrWhiteSpace(model.ModelId)
                || String.IsNullOrWhiteSpace(model.DisplayName))
        then
            failwith "modelId and displayName must be non-empty"

        if
            models |> List.map (fun model -> model.ModelId) |> List.distinct |> List.length
            <> models.Length
        then
            failwith "modelId values must be unique"

        if
            models
            |> List.collect (fun model ->
                [ model.Peak.InputCacheHit
                  model.Peak.InputCacheMiss
                  model.Peak.Output
                  model.OffPeak.InputCacheHit
                  model.OffPeak.InputCacheMiss
                  model.OffPeak.Output ])
            |> List.exists (fun price -> price < 0m)
        then
            failwith "prices must be non-negative"

    let parse (json: string) : Result<PricingSnapshot, string> =
        try
            use doc = JsonDocument.Parse(json)

            if doc.RootElement.ValueKind <> JsonValueKind.Object then
                failwith "snapshot must be a JSON object"

            let mutable versionElement = Unchecked.defaultof<JsonElement>

            if not (doc.RootElement.TryGetProperty("schemaVersion", &versionElement)) then
                failwith "schemaVersion is required"

            if versionElement.ValueKind <> JsonValueKind.Number then
                failwith "schemaVersion must be an integer"

            let version = versionElement.GetInt32()

            if not (supportedSchemaVersions.Contains(version)) then
                failwith $"unsupported schemaVersion %d{version}"

            let snapshot: PricingSnapshot =
                match version with
                | 1 ->
                    let wire = deserialize<V1PricingSnapshot> json

                    if wire.SchemaVersion <> 1 then
                        failwith "schemaVersion does not match v1 payload"

                    validateCommon wire.SchemaVersion wire.SourceHash wire.Currency wire.PeakPolicy.Windows wire.Models

                    { SchemaVersion = 1
                      SourceHash = wire.SourceHash
                      Currency = wire.Currency
                      PeakPolicy =
                        { Timezone = "UTC"
                          WeekdaysOnly = wire.PeakPolicy.WeekdaysOnly
                          Windows = wire.PeakPolicy.Windows
                          HolidayCalendar = None }
                      Models = wire.Models }
                | 2 ->
                    let wire = deserialize<V2PricingSnapshot> json

                    if wire.SchemaVersion <> 2 then
                        failwith "schemaVersion does not match v2 payload"

                    if wire.PeakPolicy.Timezone <> "Asia/Shanghai" then
                        failwith "v2 peakPolicy.timezone must be Asia/Shanghai"

                    validateCommon wire.SchemaVersion wire.SourceHash wire.Currency wire.PeakPolicy.Windows wire.Models
                    validateCalendar wire.PeakPolicy.HolidayCalendar

                    { SchemaVersion = 2
                      SourceHash = wire.SourceHash
                      Currency = wire.Currency
                      PeakPolicy =
                        { Timezone = wire.PeakPolicy.Timezone
                          WeekdaysOnly = wire.PeakPolicy.WeekdaysOnly
                          Windows = wire.PeakPolicy.Windows
                          HolidayCalendar = Some wire.PeakPolicy.HolidayCalendar }
                      Models = wire.Models }
                | _ -> failwith "unreachable schema version"

            Ok snapshot
        with ex ->
            Error ex.Message

    let serialize (snapshot: PricingSnapshot) : string =
        match snapshot.SchemaVersion, snapshot.PeakPolicy.HolidayCalendar with
        | 1, _ ->
            let wire: V1PricingSnapshot =
                { SchemaVersion = 1
                  SourceHash = snapshot.SourceHash
                  Currency = snapshot.Currency
                  PeakPolicy =
                    { WeekdaysOnly = snapshot.PeakPolicy.WeekdaysOnly
                      Windows = snapshot.PeakPolicy.Windows }
                  Models = snapshot.Models }

            JsonSerializer.Serialize(wire, options)
        | 2, Some calendar ->
            let wire: V2PricingSnapshot =
                { SchemaVersion = 2
                  SourceHash = snapshot.SourceHash
                  Currency = snapshot.Currency
                  PeakPolicy =
                    { Timezone = "Asia/Shanghai"
                      WeekdaysOnly = snapshot.PeakPolicy.WeekdaysOnly
                      Windows = snapshot.PeakPolicy.Windows
                      HolidayCalendar = calendar }
                  Models = snapshot.Models }

            JsonSerializer.Serialize(wire, options)
        | 2, None -> invalidArg "snapshot" "schema v2 requires a holiday calendar"
        | version, _ -> invalidArg "snapshot" $"unsupported schema version %d{version}"

    let tryParse (json: string) : PricingSnapshot option =
        match parse json with
        | Ok s -> Some s
        | Error _ -> None
