namespace Liangwengu.Platform

open System
open Avalonia
open Liangwengu

type TrayActivation =
    { Anchor: PixelRect option
      Pointer: PixelPoint option }

type ITrayHost =
    inherit IDisposable
    abstract Activated: IEvent<TrayActivation>
    abstract Update: Period * string -> unit
    abstract Anchor: unit -> PixelRect option
    abstract Pointer: unit -> PixelPoint option

/// Used by the portable target; native application targets supply their own host.
type NullTrayHost() =
    let activated = Event<TrayActivation>()

    interface ITrayHost with
        member _.Activated = activated.Publish
        member _.Update(_, _) = ()
        member _.Anchor() = None
        member _.Pointer() = None
        member _.Dispose() = ()
