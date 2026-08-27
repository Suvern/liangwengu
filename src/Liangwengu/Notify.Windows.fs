namespace Liangwengu.Windows

open System
open Windows.Data.Xml.Dom
open Windows.UI.Notifications

module Notify =
    let show (title: string) (msg: string) =
        let xml = XmlDocument()
        xml.LoadXml(
            "<toast><visual><binding template=\"ToastText02\">"
            + $"<text id=\"1\">{title}</text><text id=\"2\">{msg}</text>"
            + "</binding></visual></toast>")
        let toast = ToastNotification(xml)
        ToastNotificationManager.CreateToastNotifier("liangwengu").Show(toast)
