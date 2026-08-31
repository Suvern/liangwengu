namespace Liangwengu.Windows

open System
open System.IO
open Windows.Data.Xml.Dom
open Windows.UI.Notifications

module Notify =
    let private xmlEscape (s: string) =
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;")

    let buildXml (title: string) (msg: string) =
        let title = xmlEscape title
        let msg = xmlEscape msg

        let iconPath =
            Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.png")
            |> Uri
            |> fun uri -> xmlEscape uri.AbsoluteUri

        "<toast><visual><binding template=\"ToastGeneric\">"
        + $"<image placement=\"appLogoOverride\" src=\"{iconPath}\" alt=\"Liangwengu\"/>"
        + $"<text>{title}</text><text>{msg}</text>"
        + "</binding></visual></toast>"

    let show (title: string) (msg: string) =
        try
            match Liangwengu.Native.Windows.ensureToastShortcut () with
            | Error message -> Error message
            | Ok() ->
                let xml = XmlDocument()
                xml.LoadXml(buildXml title msg)
                let toast = ToastNotification(xml)
                ToastNotificationManager.CreateToastNotifier(Liangwengu.Native.Windows.AppUserModelId).Show(toast)
                Ok()
        with ex ->
            Error ex.Message
