namespace Liangwengu.Platform

open System

module Notification =
    let show (title: string) (message: string) =
#if WIN32
        try
            Liangwengu.Windows.Notify.show title message
        with _ ->
            ()
#else
        if OperatingSystem.IsMacOS() then
            Liangwengu.Mac.Notify.show title message
        else
            failwith "Linux notification is not implemented"
#endif
