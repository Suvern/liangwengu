namespace Liangwengu.Platform

open System
open System.IO

type private InstanceLease(stream: FileStream) =
    member _.Dispose() = stream.Dispose()

    interface IDisposable with
        member this.Dispose() = this.Dispose()

module SingleInstance =
    let private lockPath = Path.Combine(Path.GetTempPath(), "liangwengu.instance.lock")

    let tryAcquire () =
        try
            let stream =
                new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)

            Some(new InstanceLease(stream) :> IDisposable)
        with
        | :? IOException -> None
        | :? UnauthorizedAccessException -> None
