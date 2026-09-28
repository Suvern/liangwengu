namespace Liangwengu

open System
open System.IO
open System.Text.Json

type UserSettings = { Reminders: bool }

module UserSettings =
    let defaults = { Reminders = false }

    let path =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "liangwengu",
            "settings.json"
        )

    let load file =
        try
            use document = JsonDocument.Parse(File.ReadAllText file)
            { Reminders = document.RootElement.GetProperty("reminders").GetBoolean() }
        with _ ->
            defaults

    let save file settings =
        let temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp"

        try
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath file))
            |> ignore

            File.WriteAllText(
                temporary,
                if settings.Reminders then
                    "{\"reminders\":true}"
                else
                    "{\"reminders\":false}"
            )

            File.Move(temporary, file, true)
            Ok()
        with ex ->
            try
                if File.Exists temporary then
                    File.Delete temporary
            with _ ->
                ()

            Error("无法保存提醒设置：" + ex.Message)
