namespace Liangwengu.Native

#if WIN32
#nowarn "9"

open System
open System.IO
open System.Runtime.InteropServices
open System.Runtime.InteropServices.ComTypes
open System.Text

module Windows =
    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool AttachConsole(uint32 dwProcessId)

    [<Literal>]
    let AppUserModelId = "Liangwengu"

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type PropertyKey = { FormatId: Guid; PropertyId: uint32 }

    [<Struct; StructLayout(LayoutKind.Explicit, Size = 16)>]
    type PropVariant =
        [<FieldOffset(0)>]
        val VariantType: uint16

        [<FieldOffset(8)>]
        val Value: IntPtr

        new(variantType, value) =
            { VariantType = variantType
              Value = value }

    [<ComImport; Guid("000214F9-0000-0000-C000-000000000046"); InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
    type IShellLinkW =
        abstract GetPath: path: StringBuilder * bufferLength: int * fileAttributes: IntPtr * reserved: uint32 -> int
        abstract GetIDList: IntPtr -> int
        abstract SetIDList: IntPtr -> int
        abstract GetDescription: description: StringBuilder * bufferLength: int -> int
        abstract SetDescription: description: string -> int
        abstract GetWorkingDirectory: directory: StringBuilder * bufferLength: int -> int
        abstract SetWorkingDirectory: directory: string -> int
        abstract GetArguments: arguments: StringBuilder * bufferLength: int -> int
        abstract SetArguments: arguments: string -> int
        abstract GetHotkey: hotkey: uint16 ref -> int
        abstract SetHotkey: uint16 -> int
        abstract GetShowCmd: showCommand: int ref -> int
        abstract SetShowCmd: int -> int
        abstract GetIconLocation: iconPath: StringBuilder * bufferLength: int * iconIndex: int ref -> int
        abstract SetIconLocation: iconPath: string * iconIndex: int -> int
        abstract SetRelativePath: path: string * reserved: uint32 -> int
        abstract Resolve: IntPtr * uint32 -> int
        abstract SetPath: path: string -> int

    [<ComImport; Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"); InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
    type IPropertyStore =
        abstract GetCount: count: uint32 ref -> int
        abstract GetAt: index: uint32 * key: PropertyKey ref -> int
        abstract GetValue: key: PropertyKey ref * value: PropVariant ref -> int
        abstract SetValue: key: inref<PropertyKey> * value: byref<PropVariant> -> int
        abstract Commit: unit -> int

    [<Literal>]
    let private ClsIdShellLink = "00021401-0000-0000-C000-000000000046"

    [<Literal>]
    let private PropertyIdAppUserModelId = "9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"

    let ensureToastShortcut () =
        let shortcutPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "Windows",
                "Start Menu",
                "Programs",
                "Liangwengu.lnk"
            )

        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)) |> ignore

        match Environment.ProcessPath with
        | null -> Error "无法获取当前进程路径"
        | processPath ->
            let shellLinkType = Type.GetTypeFromCLSID(Guid(ClsIdShellLink))
            let shellLinkObject = Activator.CreateInstance(shellLinkType)

            try
                let shellLink = shellLinkObject :?> IShellLinkW
                let propertyStore = shellLinkObject :?> IPropertyStore
                let persistFile = shellLinkObject :?> IPersistFile

                let setPathResult = shellLink.SetPath(processPath)

                if setPathResult < 0 then
                    Error $"无法设置 Windows shortcut 路径 (HRESULT 0x{setPathResult:X8})"
                else
                    let value = Marshal.StringToCoTaskMemUni(AppUserModelId)

                    try
                        let mutable key =
                            { FormatId = Guid(PropertyIdAppUserModelId)
                              PropertyId = 5u }

                        let mutable propVariant = PropVariant(31us, value)
                        let setValueResult = propertyStore.SetValue(&key, &propVariant)

                        let commitResult =
                            if setValueResult >= 0 then
                                propertyStore.Commit()
                            else
                                setValueResult

                        if commitResult < 0 then
                            Error $"无法注册 Windows Toast AUMID (HRESULT 0x{commitResult:X8})"
                        else
                            persistFile.Save(shortcutPath, true)
                            Ok()
                    finally
                        Marshal.FreeCoTaskMem(value)
            finally
                Marshal.FinalReleaseComObject(shellLinkObject) |> ignore
#endif
