namespace Liangwengu.Platform

open System
open System.Threading.Tasks

module Notification =
    type NotificationResult = Result<unit, string>

    let show (title: string) (message: string) : Task<NotificationResult> =
#if WIN32
        try
            Task.FromResult(Liangwengu.Windows.Notify.show title message)
        with ex ->
            Task.FromResult(Error ex.Message)
#else
#if MACOS
        Liangwengu.Mac.Notify.show title message
#else
        Task.FromResult(Error "Notifications are not implemented on this platform")
#endif
#endif
