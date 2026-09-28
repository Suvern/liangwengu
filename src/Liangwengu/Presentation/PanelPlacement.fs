namespace Liangwengu.Presentation

open Avalonia

module PanelPlacement =
    /// All input geometry is in physical pixels; size and gap originate in DIPs.
    let place (work: PixelRect) (anchor: PixelRect option) (pointer: PixelPoint option) (size: Size) scale mac =
        let scale = if scale > 0. then scale else 1.
        let gap = int (8. * scale)
        let width = min work.Width (int (size.Width * scale))
        let height = min work.Height (int (size.Height * scale))

        let bounds =
            anchor
            |> Option.defaultWith (fun () ->
                let point =
                    pointer
                    |> Option.defaultValue (PixelPoint(work.Right - gap, if mac then work.Y else work.Bottom))

                PixelRect(point.X, point.Y, 1, 1))

        let x =
            if mac then
                bounds.X + bounds.Width / 2 - width / 2
            else
                bounds.Right - width

        let above, below = bounds.Y - gap - height, bounds.Bottom + gap

        let desiredY =
            if mac then
                (if below + height <= work.Bottom then below else above)
            else
                (if above >= work.Y then above else below)

        let clamp low high value = max low (min high value)

        let position =
            PixelPoint(clamp work.X (work.Right - width) x, clamp work.Y (work.Bottom - height) desiredY)

        let tip = float (bounds.X + bounds.Width / 2 - position.X) / scale
        position, tip
