namespace Liangwengu.Platform

open System
open System.IO
open System.Runtime.InteropServices
open Avalonia
open Avalonia.Controls
open Avalonia.Platform
open Liangwengu

module private TrayWin32 =
    [<Struct; StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)>]
    type IconData =
        val mutable Size: uint32
        val mutable Window: nativeint
        val mutable Id: uint32
        val mutable Flags: uint32
        val mutable Callback: uint32
        val mutable Icon: nativeint

        [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)>]
        val mutable Tip: string

        val mutable State: uint32
        val mutable StateMask: uint32

        [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)>]
        val mutable Info: string

        val mutable Version: uint32

        [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)>]
        val mutable InfoTitle: string

        val mutable InfoFlags: uint32
        val mutable Guid: Guid
        val mutable BalloonIcon: nativeint

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type IconIdentifier =
        val mutable Size: uint32
        val mutable Window: nativeint
        val mutable Id: uint32
        val mutable Guid: Guid

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type Rect =
        val mutable Left: int
        val mutable Top: int
        val mutable Right: int
        val mutable Bottom: int

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type Point =
        val mutable X: int
        val mutable Y: int

    [<DllImport("shell32.dll", CharSet = CharSet.Unicode)>]
    extern bool Shell_NotifyIconW(uint32 action, IconData& data)

    [<DllImport("shell32.dll")>]
    extern int Shell_NotifyIconGetRect(IconIdentifier& identifier, Rect& rect)

    [<DllImport("user32.dll", CharSet = CharSet.Unicode)>]
    extern uint32 RegisterWindowMessageW(string name)

    [<DllImport("user32.dll")>]
    extern bool GetCursorPos(Point& point)

    [<DllImport("user32.dll")>]
    extern nativeint CreateIconFromResourceEx(
        byte[] bits,
        uint32 size,
        bool icon,
        uint32 version,
        int width,
        int height,
        uint32 flags
    )

    [<DllImport("user32.dll")>]
    extern bool DestroyIcon(nativeint icon)

type WindowsTrayHost() =
    let activated = Event<TrayActivation>()

    let window =
        Window(Width = 1., Height = 1., Opacity = 0., ShowInTaskbar = false, WindowDecorations = WindowDecorations.None)

    let mutable data = Unchecked.defaultof<TrayWin32.IconData>
    let mutable disposed = false
    let mutable current: (Period * string) option = None
    let taskbarCreated = TrayWin32.RegisterWindowMessageW "TaskbarCreated"
    let callbackMessage = 0x8001u

    let icon name =
        use source = AssetLoader.Open(Uri("avares://liangwengu/Assets/" + name))
        use stream = new MemoryStream()
        source.CopyTo stream
        let bytes = stream.ToArray()
        // Vista+ accepts PNG-encoded icon resources directly.
        let handle =
            TrayWin32.CreateIconFromResourceEx(bytes, uint32 bytes.Length, true, 0x30000u, 32, 32, 0u)

        if handle = 0n then
            failwith "无法创建托盘图标"

        handle

    let icons =
        [ Peak, icon "peak.png"
          OffPeak, icon "valley.png"
          Unknown, icon "app-icon.png" ]
        |> Map.ofList

    let anchor () =
        let mutable id = Unchecked.defaultof<TrayWin32.IconIdentifier>
        id.Size <- uint32 (Marshal.SizeOf<TrayWin32.IconIdentifier>())
        id.Window <- data.Window
        id.Id <- data.Id
        let mutable rect = Unchecked.defaultof<TrayWin32.Rect>

        if TrayWin32.Shell_NotifyIconGetRect(&id, &rect) = 0 then
            Some(PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top))
        else
            None

    let pointer () =
        let mutable point = Unchecked.defaultof<TrayWin32.Point>

        if TrayWin32.GetCursorPos(&point) then
            Some(PixelPoint(point.X, point.Y))
        else
            None

    let register () =
        if not disposed then
            if not (TrayWin32.Shell_NotifyIconW(0u, &data)) then
                failwith "无法注册 Windows 托盘图标"

            data.Version <- 4u
            TrayWin32.Shell_NotifyIconW(4u, &data) |> ignore

    let hook =
        Win32Properties.CustomWndProcHookCallback(fun _ msg _ lParam handled ->
            if msg = taskbarCreated then
                try
                    register ()
                with ex ->
                    Console.Error.WriteLine ex.Message
            elif msg = callbackMessage then
                let eventId = int (lParam.ToInt64() &&& 0xFFFFL)
                // Version 4 emits NIN_SELECT/NIN_KEYSELECT and WM_CONTEXTMENU.
                if eventId = 0x400 || eventId = 0x401 || eventId = 0x7B then
                    activated.Trigger
                        { Anchor = anchor ()
                          Pointer = pointer () }

                handled <- true

            0n)

    do
        window.Position <- PixelPoint(-32000, -32000)
        window.Show()
        window.Hide()
        data.Size <- uint32 (Marshal.SizeOf<TrayWin32.IconData>())
        data.Window <- window.TryGetPlatformHandle().Handle
        data.Id <- 1u
        data.Flags <- 1u ||| 2u ||| 4u ||| 0x80u
        data.Callback <- callbackMessage
        data.Icon <- icons[Unknown]
        data.Tip <- "梁文谷"
        data.Info <- ""
        data.InfoTitle <- ""
        Win32Properties.AddWndProcHookCallback(window, hook)
        register ()

    interface ITrayHost with
        member _.Activated = activated.Publish
        member _.Anchor() = anchor ()
        member _.Pointer() = pointer ()

        member _.Update(period, tooltip) =
            if not disposed && current <> Some(period, tooltip) then
                current <- Some(period, tooltip)
                data.Icon <- icons[period]

                data.Tip <-
                    if tooltip.Length > 127 then
                        tooltip.Substring(0, 127)
                    else
                        tooltip

                TrayWin32.Shell_NotifyIconW(1u, &data) |> ignore

        member _.Dispose() =
            if not disposed then
                disposed <- true
                TrayWin32.Shell_NotifyIconW(2u, &data) |> ignore
                Win32Properties.RemoveWndProcHookCallback(window, hook)
                window.Close()
                icons |> Map.iter (fun _ handle -> TrayWin32.DestroyIcon(handle) |> ignore)
