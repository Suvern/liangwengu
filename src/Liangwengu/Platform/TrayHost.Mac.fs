namespace Liangwengu.Platform

open System
open AppKit
open Foundation
open Avalonia
open Avalonia.Platform
open Liangwengu

type MacTrayHost() =
    let activated = Event<TrayActivation>()

    let item =
        NSStatusBar.SystemStatusBar.CreateStatusItem(System.Runtime.InteropServices.NFloat(-1.))

    let button = item.Button
    let mutable disposed = false

    let load name =
        use stream = AssetLoader.Open(Uri("avares://liangwengu/Assets/" + name))
        use memory = new IO.MemoryStream()
        stream.CopyTo memory
        use data = NSData.FromArray(memory.ToArray())
        let image = new NSImage(data)
        image.Size <- CoreGraphics.CGSize(20., 20.)
        image.Template <- false
        image

    let icons =
        [ Peak, load "peak.png"
          OffPeak, load "valley.png"
          Unknown, load "app-icon.png" ]
        |> Map.ofList

    // Avalonia's macOS window positions are Cocoa screen points with a top-left origin.
    let fromCocoa (rect: CoreGraphics.CGRect) =
        let top = float NSScreen.Screens[0].Frame.Height
        PixelRect(int rect.X, int (top - float rect.Y - float rect.Height), int rect.Width, int rect.Height)

    let anchor () =
        if isNull button.Window then
            None
        else
            Some(fromCocoa (button.Window.ConvertRectToScreen(button.ConvertRectToView(button.Bounds, null))))

    let pointer () =
        let point = NSEvent.CurrentMouseLocation
        let top = float NSScreen.Screens[0].Frame.Height
        Some(PixelPoint(int point.X, int (top - float point.Y)))

    let onActivated =
        EventHandler(fun _ _ ->
            activated.Trigger
                { Anchor = anchor ()
                  Pointer = pointer () })

    do
        NSApplication.SharedApplication.ActivationPolicy <- NSApplicationActivationPolicy.Accessory
        button.Image <- icons[Unknown]
        button.ToolTip <- "梁文谷"

        button.SendActionOn(LanguagePrimitives.EnumOfValue<uint64, NSEventType>((1UL <<< 2) ||| (1UL <<< 4)))
        |> ignore

        button.Activated.AddHandler onActivated

    interface ITrayHost with
        member _.Activated = activated.Publish
        member _.Anchor() = anchor ()
        member _.Pointer() = pointer ()

        member _.Update(period, tooltip) =
            if not disposed then
                button.Image <- icons[period]
                button.ToolTip <- tooltip

        member _.Dispose() =
            if not disposed then
                disposed <- true
                button.Activated.RemoveHandler onActivated
                NSStatusBar.SystemStatusBar.RemoveStatusItem item
                icons |> Map.iter (fun _ image -> image.Dispose())
                item.Dispose()
