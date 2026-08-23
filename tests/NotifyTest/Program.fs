open Windows.Data.Xml.Dom
open Windows.UI.Notifications

[<EntryPoint>]
let main _ =
    let xml = XmlDocument()
    xml.LoadXml(
        "<toast><visual><binding template=\"ToastText02\">"
        + "<text id=\"1\">liangwengu</text>"
        + "<text id=\"2\">这是一条测试通知</text>"
        + "</binding></visual></toast>")
    let toast = ToastNotification(xml)
    ToastNotificationManager.CreateToastNotifier("liangwengu").Show(toast)
    0
