namespace Liangwengu.Windows

open System
open Windows.Data.Xml.Dom
open Windows.UI.Notifications

module Notify =
    let private xmlEscape (s: string) =
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;")

    let buildXml (title: string) (msg: string) =
        let title = xmlEscape title
        let msg = xmlEscape msg

        "<toast><visual><binding template=\"ToastText02\">"
        + $"<text id=\"1\">{title}</text><text id=\"2\">{msg}</text>"
        + "</binding></visual></toast>"

    let show (title: string) (msg: string) =
        try
            let xml = XmlDocument()
            xml.LoadXml(buildXml title msg)
            let toast = ToastNotification(xml)
            ToastNotificationManager.CreateToastNotifier("liangwengu").Show(toast)
            Ok()
        with ex ->
            Error ex.Message
