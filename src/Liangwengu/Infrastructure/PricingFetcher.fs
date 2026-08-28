namespace Liangwengu

open System
open System.IO
open System.Net.Http
open System.Reflection
open System.Threading.Tasks

module PricingFetcher =

    /// 远程 pricing.json 的 raw URL（GitHub raw 直出 JSON）
    let private remoteUrl = "https://raw.githubusercontent.com/Suvern/liangwengu/master/pricing.json"

    /// APPDATA 缓存路径：%APPDATA%\liangwengu\pricing.json
    let private localCachePath =
        let appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Path.Combine(appData, "liangwengu", "pricing.json")

    /// 单例 HttpClient（避免 socket 耗尽）
    let private http = lazy new HttpClient(Timeout = TimeSpan.FromSeconds 10.0)

    /// 内嵌资源名（fsproj 中 LogicalName 固定）
    let private resourceName = "liangwengu.pricing.json"

    // ---- bundled ----

    /// 从编译期内嵌的 EmbeddedResource 加载兜底 snapshot（同步，必有）
    let loadBundled () : PricingSnapshot =
        let asm = Assembly.GetExecutingAssembly()
        use stream = asm.GetManifestResourceStream(resourceName)
        if isNull stream then
            failwith $"embedded resource not found: %s{resourceName}"
        use reader = new StreamReader(stream)
        let json = reader.ReadToEnd()
        match PricingSchema.parse json with
        | Ok s -> s
        | Error e -> failwith $"bundled pricing.json failed to parse: %s{e}"

    // ---- APPDATA 本地缓存 ----

    /// 从 APPDATA 读本地缓存。若文件不存在或解析失败返回 None。
    let loadLocalCache () : PricingSnapshot option =
        try
            if File.Exists(localCachePath) then
                let json = File.ReadAllText(localCachePath)
                PricingSchema.tryParse json
            else None
        with ex ->
            Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] Loading local pricing cache failed: {ex}")
            None

    /// 将 snapshot 写入 APPDATA。失败静默（降级为不缓存）。
    let saveLocalCache (snap: PricingSnapshot) : unit =
        try
            let dir = Path.GetDirectoryName(localCachePath)
            if not (Directory.Exists(dir)) then Directory.CreateDirectory(dir) |> ignore
            File.WriteAllText(localCachePath, System.Text.Json.JsonSerializer.Serialize(snap))
        with ex ->
            Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] Saving local pricing cache failed: {ex}")

    // ---- 远程拉取 ----

    /// 异步拉取远程 pricing.json。失败 / 版本不支持 / 解析失败均返回 None。
    let tryFetchRemote () : PricingSnapshot option Async =
        async {
            try
                let! resp =
                    http.Value.GetAsync(remoteUrl)
                    |> Async.AwaitTask
                if not resp.IsSuccessStatusCode then
                    Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] Remote pricing returned HTTP {int resp.StatusCode}")
                    return None
                else
                    let! json =
                        resp.Content.ReadAsStringAsync()
                        |> Async.AwaitTask
                    return PricingSchema.tryParse json
            with ex ->
                Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] Fetching remote pricing failed: {ex}")
                return None
        }

    // ---- 启动加载（bundled → APPDATA → 异步远程）----

    /// 同步加载最佳可用 snapshot：先 bundled，若 APPDATA 缓存存在则覆盖。
    /// 返回 (snapshot, 是否用了本地缓存)。
    let loadInitial () : PricingSnapshot =
        let bundled = loadBundled ()
        match loadLocalCache () with
        | Some cached -> cached
        | None -> bundled
