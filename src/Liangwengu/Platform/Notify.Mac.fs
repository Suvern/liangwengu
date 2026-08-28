namespace Liangwengu.Mac

open System
open UserNotifications

module Notify =
    let show (title: string) (message: string) =
        let center = UNUserNotificationCenter.Current

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
                            if not (isNull addError) then
                                Console.Error.WriteLine($"macOS notification failed: {addError.LocalizedDescription}")
                    )
                elif not (isNull error) then
                    Console.Error.WriteLine($"macOS notification permission denied: {error.LocalizedDescription}")
        )
