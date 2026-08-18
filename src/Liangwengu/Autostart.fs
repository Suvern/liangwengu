namespace Liangwengu

module Autostart =

    // 实现方式: HKCU\Software\Microsoft\Windows\CurrentVersion\Run
    // 值名 Liangwengu，数据为 exe 完整路径（带引号）。HKCU 无需管理员权限。
    // 注意: Windows 的 NativeMenuItem.IsChecked 有 bug（Avalonia #8751），
    // 菜单开关状态用 Header 文字表达，不依赖 IsChecked。

    /// 当前是否已设置开机启动
    let isEnabled () : bool =
        failwith "TODO: M2"

    /// 写入注册表，启用开机启动
    let enable () : unit =
        failwith "TODO: M2"

    /// 删除注册表值，关闭开机启动
    let disable () : unit =
        failwith "TODO: M2"
