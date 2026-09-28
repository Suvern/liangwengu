module Liangwengu.Tests.PanelPlacementTests

open Avalonia
open Liangwengu.Presentation
open Xunit

[<Theory>]
[<InlineData(1.0)>]
[<InlineData(1.25)>]
[<InlineData(1.5)>]
[<InlineData(2.0)>]
let ``negative monitor and mixed DPI stay in work area`` scale =
    let work = PixelRect(-2560, -400, 2560, 1400)

    let position, _ =
        PanelPlacement.place work (Some(PixelRect(-40, 1000, 24, 24))) None (Size(390., 560.)) scale false

    Assert.True(position.X >= work.X)
    Assert.True(position.Y >= work.Y)
    Assert.True(position.X + int (390. * scale) <= work.Right)
    Assert.True(position.Y + int (560. * scale) <= work.Bottom)

[<Fact>]
let ``top tray flips below and mac bottom anchor flips above`` () =
    let work = PixelRect(0, 30, 1920, 1000)

    let win, _ =
        PanelPlacement.place work (Some(PixelRect(1800, 0, 24, 24))) None (Size(390., 560.)) 1. false

    let mac, _ =
        PanelPlacement.place work (Some(PixelRect(1800, 1000, 24, 24))) None (Size(390., 560.)) 1. true

    Assert.Equal(32, win.Y)
    Assert.True(mac.Y < 1000)

[<Fact>]
let ``missing anchor uses pointer then platform corner`` () =
    let work = PixelRect(0, 0, 1000, 800)

    let point, _ =
        PanelPlacement.place work None (Some(PixelPoint(500, 700))) (Size(390., 560.)) 1. false

    Assert.Equal(111, point.X)
    let fallback, _ = PanelPlacement.place work None None (Size(390., 560.)) 1. true
    Assert.Equal(610, fallback.X)
    Assert.Equal(9, fallback.Y)
