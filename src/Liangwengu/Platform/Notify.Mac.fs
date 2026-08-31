namespace Liangwengu.Mac

open System
open System.Threading.Tasks
open UserNotifications

module Notify =
    let show (title: string) (message: string) =
        try
            let center = UNUserNotificationCenter.Current

            let result =
                TaskCompletionSource<Result<unit, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

            center.RequestAuthorization(
                UNAuthorizationOptions.Alert,
                fun granted error ->
                    if granted then
                        let content = new UNMutableNotificationContent()
                        content.Title <- title
                        content.Body <- message

                        let request =
                            UNNotificationRequest.FromIdentifier(Guid.NewGuid().ToString(), content, null)

                        center.AddNotificationRequest(
                            request,
                            fun addError ->
                                if isNull addError then
                                    result.TrySetResult(Ok()) |> ignore
                                else
                                    result.TrySetResult(Error addError.LocalizedDescription) |> ignore
                        )
                    elif isNull error then
                        result.TrySetResult(Error "macOS notification permission denied") |> ignore
                    else
                        result.TrySetResult(Error error.LocalizedDescription) |> ignore
            )

            result.Task
        with ex ->
            Task.FromResult(Error ex.Message)
