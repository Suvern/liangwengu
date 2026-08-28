namespace Liangwengu.Native

#if WIN32
open System.Runtime.InteropServices

module Windows =
    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool AttachConsole(uint32 dwProcessId)
#endif
