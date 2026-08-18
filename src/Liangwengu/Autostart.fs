namespace Liangwengu

open System
open Microsoft.Win32

module Autostart =

    // HKCU\Software\Microsoft\Windows\CurrentVersion\Run
    // 值名 Liangwengu，数据为带引号的 exe 完整路径。HKCU 无需管理员权限。
    // 注意: Windows 的 NativeMenuItem.IsChecked 有 bug（Avalonia #8751），
    // 菜单开关状态用 Header 文字表达，不依赖 IsChecked。

    let private runKeySubPath = @"Software\Microsoft\Windows\CurrentVersion\Run"
    let private valueName = "Liangwengu"

    /// 当前是否已设置开机启动
    let isEnabled () : bool =
        use key = Registry.CurrentUser.OpenSubKey(runKeySubPath)
        not (isNull key) && not (isNull (key.GetValue(valueName)))

    /// 写入注册表，启用开机启动
    let enable () : unit =
        match Environment.ProcessPath with
        | null -> failwith "无法获取当前进程路径"
        | path ->
            use key = Registry.CurrentUser.CreateSubKey(runKeySubPath, writable = true)
            key.SetValue(valueName, $"\"{path}\"")

    /// 删除注册表值，关闭开机启动
    let disable () : unit =
        use key = Registry.CurrentUser.OpenSubKey(runKeySubPath, writable = true)
        if not (isNull key) then
            key.DeleteValue(valueName, throwOnMissingValue = false)
