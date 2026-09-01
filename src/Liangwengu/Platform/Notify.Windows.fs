namespace Liangwengu.Windows

open System
open System.IO
open System.Reflection
open Windows.Data.Xml.Dom
open Windows.UI.Notifications

module Notify =
    [<Literal>]
    let private IconResourceName = "liangwengu.app-icon.png"

    let private iconFilePath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Liangwengu",
            "app-icon.png"
        )

    let private ensureIconFile () =
        if not (File.Exists(iconFilePath)) then
            let directory = Path.GetDirectoryName(iconFilePath)
            Directory.CreateDirectory(directory) |> ignore

            use resource =
                Assembly.GetExecutingAssembly().GetManifestResourceStream(IconResourceName)

            if isNull resource then
                invalidOp $"Embedded notification icon not found: {IconResourceName}"

            use output = File.Create(iconFilePath)
            resource.CopyTo(output)

        iconFilePath

    let private xmlEscape (s: string) =
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;")

    let buildXml (title: string) (msg: string) =
        let title = xmlEscape title
        let msg = xmlEscape msg

        let iconUri = ensureIconFile () |> Uri |> (fun uri -> xmlEscape uri.AbsoluteUri)

        "<toast><visual><binding template=\"ToastGeneric\">"
        + $"<image placement=\"appLogoOverride\" src=\"{iconUri}\" alt=\"Liangwengu\"/>"
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
