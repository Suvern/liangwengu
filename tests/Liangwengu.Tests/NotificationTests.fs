namespace Liangwengu.Tests

open Xunit
open Liangwengu.Platform

module NotificationTests =
    [<Fact>]
    let ``unsupported platforms return a useful result`` () =
#if NET10_0 && !WIN32 && !MACOS
        let result =
            Notification.show "title" "message" |> Async.AwaitTask |> Async.RunSynchronously

        match result with
        | Error message -> Assert.Contains("not implemented", message)
        | Ok() -> Assert.Fail("An unsupported platform must not report success")
#else
        ()
#endif

#if WIN32
    [<Fact>]
    let ``Windows toast XML escapes notification content`` () =
        let xml =
            Liangwengu.Windows.Notify.buildXml "a <title> & \"quote\"" "line 1 & line 2"

        Assert.Contains("a &lt;title&gt; &amp; &quot;quote&quot;", xml)
        Assert.Contains("line 1 &amp; line 2", xml)
        Assert.Contains("template=\"ToastGeneric\"", xml)
        Assert.Contains("placement=\"appLogoOverride\"", xml)
        Assert.Contains("app-icon.png", xml)

#endif
