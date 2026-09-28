namespace Liangwengu.Presentation

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.Builder
open Avalonia.Automation
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Platform
open Avalonia.Styling
open Avalonia.Input
open Liangwengu

type PanelActions =
    { Refresh: unit -> unit
      Autostart: bool -> unit
      Reminders: bool -> unit
      OpenPricing: unit -> unit
      Exit: unit -> unit }

type PanelWindow(initial: PanelState, initialMac: bool, actions: PanelActions) as this =
    inherit HostWindow()

    let state =
        new State<PanelState * bool * bool * float>(initial, initialMac, false, 195.) :> IWritable<_>

    let load name =
        use stream = AssetLoader.Open(Uri("avares://liangwengu/Assets/" + name))
        new Bitmap(stream)

    let peak, valley, appIcon = load "peak.png", load "valley.png", load "app-icon.png"

    let brush (value: string) =
        SolidColorBrush(Color.Parse value) :> IBrush

    let mutable allowClose = false

    let render (model: PanelState) mac dark tip =
        let compact = this.Height < 520.
        let fg = if dark then "#F3F4F6" else "#202530"
        let muted = if dark then "#ABB4C1" else "#606C7D"
        let line = if dark then "#3C4553" else "#DFE4EB"

        let blurred =
            this.ActualTransparencyLevel = WindowTransparencyLevel.AcrylicBlur
            || this.ActualTransparencyLevel = WindowTransparencyLevel.Blur

        let surface =
            if dark then
                (if blurred then "#F2242A33" else "#242A33")
            else
                (if blurred then "#F2F7F9FC" else "#F7F9FC")

        let inset = if dark then "#2D3540" else "#FFFFFF"

        let accent, tint =
            match model.Period, dark with
            | OffPeak, false -> "#16724C", "#E8F4ED"
            | OffPeak, true -> "#8DE0B6", "#233F34"
            | Peak, false -> "#996000", "#FFF3DE"
            | Peak, true -> "#F6CC84", "#493922"
            | Unknown, _ -> muted, inset

        let text value size color =
            TextBlock.create
                [ TextBlock.text value
                  TextBlock.fontSize size
                  TextBlock.foreground (brush color) ]
            :> IView

        let section row (child: IView) =
            Border.create
                [ Border.row row
                  Border.child child
                  Border.margin (0., if compact then 2. else 4.) ]
            :> IView

        let button (label: string) enabled action =
            Button.create
                [ Button.content label
                  Button.isEnabled enabled
                  Button.onClick (fun _ -> action ())
                  Button.background Brushes.Transparent
                  Button.foreground (brush (if dark then "#8EC5FF" else "#1265C4"))
                  Button.padding (8., 5.)
                  Button.fontSize 13. ]
            :> IView

        let setting (label: string) (enabled: bool) busy action =
            Grid.create
                [ Grid.columnDefinitions "*,Auto"
                  Grid.height (if compact then 30. else 38.)
                  Grid.children
                      [ TextBlock.create
                            [ TextBlock.text label
                              TextBlock.verticalAlignment VerticalAlignment.Center
                              TextBlock.fontSize 14.
                              TextBlock.foreground (brush fg) ]
                        ToggleSwitch.create
                            [ ToggleSwitch.column 1
                              ToggleSwitch.isChecked enabled
                              AttrBuilder<ToggleSwitch>
                                  .CreateProperty<string>(AutomationProperties.NameProperty, label, ValueNone)
                              ToggleSwitch.isEnabled (not busy)
                              ToggleSwitch.onContent ""
                              ToggleSwitch.offContent ""
                              ToggleSwitch.onClick (fun _ -> action (not enabled))
                              ToggleSwitch.verticalAlignment VerticalAlignment.Center
                              ToggleSwitch.tip label ] ] ]
            :> IView

        let priceRow header (values: string list) =
            Grid.create
                [ Grid.columnDefinitions "*,88,72,66"
                  Grid.height (if header then 32. else 36.)
                  Grid.children
                      [ for i, value in List.indexed values do
                            TextBlock.create
                                [ TextBlock.column i
                                  TextBlock.text value
                                  TextBlock.fontSize (if header then 11. else 14.)
                                  TextBlock.foreground (brush (if header then muted else fg))
                                  TextBlock.fontWeight (
                                      if i = 3 && not header then
                                          FontWeight.SemiBold
                                      else
                                          FontWeight.Normal
                                  )
                                  TextBlock.verticalAlignment VerticalAlignment.Center
                                  TextBlock.horizontalAlignment (
                                      if i = 0 then
                                          HorizontalAlignment.Left
                                      else
                                          HorizontalAlignment.Right
                                  )
                                  TextBlock.textTrimming TextTrimming.CharacterEllipsis
                                  TextBlock.tip value
                                  TextBlock.margin (4., 0.) ] ] ]
            :> IView

        let refreshContent: IView =
            if model.Operations.Refreshing then
                text "同步中…" 11. muted
            else
                PathIcon.create
                    [ PathIcon.width 16.
                      PathIcon.height 16.
                      PathIcon.data (
                          Geometry.Parse "M20,7 L20,2 L18,4 A9,9 0 1 0 21,14 L19,14 A7,7 0 1 1 16.5,5.5 L14,8 L20,8 Z"
                      ) ]
                :> IView

        Grid.create
            [ Grid.rowDefinitions (if mac then "10,*" else "0,*")
              Grid.children
                  [ Border.create
                        [ Border.isVisible mac
                          Border.width 14.
                          Border.height 14.
                          Border.background (brush surface)
                          Border.renderTransform (RotateTransform 45.)
                          Border.horizontalAlignment HorizontalAlignment.Left
                          Border.margin (tip - 7., 3., 0., 0.) ]
                    Border.create
                        [ Border.row 1
                          Border.background (brush surface)
                          Border.borderBrush (brush line)
                          Border.borderThickness 1.
                          Border.cornerRadius (if mac then 12. else 8.)
                          Border.padding (if compact then 10. else 16.)
                          Border.child (
                              Grid.create
                                  [ Grid.rowDefinitions (
                                        if compact then
                                            "28,48,84,*,64,Auto,52"
                                        else
                                            "40,84,100,*,84,Auto,56"
                                    )
                                    Grid.children
                                        [ section
                                              0
                                              (Grid.create
                                                  [ Grid.columnDefinitions "28,*,Auto"
                                                    Grid.children
                                                        [ Image.create
                                                              [ Image.source appIcon
                                                                Image.width 22.
                                                                Image.height 22. ]
                                                          TextBlock.create
                                                              [ TextBlock.column 1
                                                                TextBlock.text "梁文谷"
                                                                TextBlock.fontWeight FontWeight.SemiBold
                                                                TextBlock.verticalAlignment VerticalAlignment.Center
                                                                TextBlock.foreground (brush fg) ]
                                                          Button.create
                                                              [ Button.column 2
                                                                Button.isEnabled (not model.Operations.Refreshing)
                                                                AttrBuilder<Button>
                                                                    .CreateProperty<string>(
                                                                        AutomationProperties.NameProperty,
                                                                        "刷新价格",
                                                                        ValueNone
                                                                    )
                                                                Button.background Brushes.Transparent
                                                                Button.minHeight 24.
                                                                Button.height 24.
                                                                Button.padding (6., 0.)
                                                                Button.tip "刷新价格"
                                                                Button.onClick (fun _ -> actions.Refresh())
                                                                Button.content refreshContent ] ] ])
                                          section
                                              1
                                              (Grid.create
                                                  [ Grid.columnDefinitions "68,*"
                                                    Grid.children
                                                        [ Image.create
                                                              [ Image.source (
                                                                    match model.Period with
                                                                    | Peak -> peak
                                                                    | OffPeak -> valley
                                                                    | _ -> appIcon
                                                                )
                                                                Image.width (if compact then 40. else 56.)
                                                                Image.height (if compact then 40. else 56.)
                                                                Image.horizontalAlignment HorizontalAlignment.Left ]
                                                          StackPanel.create
                                                              [ StackPanel.column 1
                                                                StackPanel.verticalAlignment VerticalAlignment.Center
                                                                StackPanel.spacing (if compact then 2. else 5.)
                                                                StackPanel.children
                                                                    [ TextBlock.create
                                                                          [ TextBlock.text model.Title
                                                                            TextBlock.fontSize (
                                                                                if compact then 18. else 24.
                                                                            )
                                                                            TextBlock.fontWeight FontWeight.Bold
                                                                            TextBlock.foreground (brush fg) ]
                                                                      text
                                                                          model.Status
                                                                          (if compact then 11. else 13.)
                                                                          accent ] ] ] ])
                                          section
                                              2
                                              (Border.create
                                                  [ Border.background (brush tint)
                                                    Border.cornerRadius 8.
                                                    Border.padding (12., 8.)
                                                    Border.child (
                                                        StackPanel.create
                                                            [ StackPanel.spacing 2.
                                                              StackPanel.children
                                                                  [ text model.CountdownLabel 12. accent
                                                                    TextBlock.create
                                                                        [ TextBlock.text model.Countdown
                                                                          TextBlock.fontSize (
                                                                              if
                                                                                  compact || model.Countdown.Length > 12
                                                                              then
                                                                                  22.
                                                                              else
                                                                                  30.
                                                                          )
                                                                          TextBlock.fontWeight FontWeight.Bold
                                                                          TextBlock.foreground (brush accent) ]
                                                                    text model.SwitchAt 12. muted ] ]
                                                    ) ])
                                          section
                                              3
                                              (Grid.create
                                                  [ Grid.rowDefinitions "28,32,*"
                                                    Grid.children
                                                        [ Grid.create
                                                              [ Grid.columnDefinitions "*,Auto"
                                                                Grid.children
                                                                    [ text "模型价格" 14. fg
                                                                      TextBlock.create
                                                                          [ TextBlock.column 1
                                                                            TextBlock.text model.Unit
                                                                            TextBlock.fontSize 11.
                                                                            TextBlock.foreground (brush muted)
                                                                            TextBlock.verticalAlignment
                                                                                VerticalAlignment.Center ] ] ]
                                                          Border.create
                                                              [ Border.row 1
                                                                Border.background (brush inset)
                                                                Border.child (
                                                                    priceRow true [ "模型"; "输入未命中"; "输入命中"; "输出" ]
                                                                ) ]
                                                          ScrollViewer.create
                                                              [ ScrollViewer.row 2
                                                                ScrollViewer.horizontalScrollBarVisibility
                                                                    ScrollBarVisibility.Disabled
                                                                ScrollViewer.content (
                                                                    StackPanel.create
                                                                        [ StackPanel.children
                                                                              [ if model.Prices.IsEmpty then
                                                                                    text "当前价格未知，请更新数据" 13. muted
                                                                                for row in model.Prices do
                                                                                    priceRow
                                                                                        false
                                                                                        [ row.Name
                                                                                          row.Miss
                                                                                          row.Hit
                                                                                          row.Output ] ] ]
                                                                ) ] ] ])
                                          section
                                              4
                                              (Border.create
                                                  [ Border.borderBrush (brush line)
                                                    Border.borderThickness (
                                                        if mac then Thickness 1. else Thickness(0., 1., 0., 0.)
                                                    )
                                                    Border.cornerRadius (if mac then 8. else 0.)
                                                    Border.padding (6., 0.)
                                                    Border.child (
                                                        StackPanel.create
                                                            [ StackPanel.children
                                                                  [ setting
                                                                        "开机启动"
                                                                        model.Operations.Autostart
                                                                        model.Operations.AutostartBusy
                                                                        actions.Autostart
                                                                    setting
                                                                        "切换时提醒"
                                                                        model.Operations.Reminders
                                                                        model.Operations.RemindersBusy
                                                                        actions.Reminders ] ]
                                                    ) ])
                                          section
                                              5
                                              (TextBlock.create
                                                  [ TextBlock.text (defaultArg model.Operations.Error "")
                                                    TextBlock.isVisible model.Operations.Error.IsSome
                                                    TextBlock.textWrapping TextWrapping.Wrap
                                                    TextBlock.maxHeight 42.
                                                    TextBlock.fontSize 12.
                                                    TextBlock.foreground (brush accent) ])
                                          section
                                              6
                                              (StackPanel.create
                                                  [ StackPanel.spacing 4.
                                                    StackPanel.children
                                                        [ text model.Source 11. muted
                                                          Grid.create
                                                              [ Grid.columnDefinitions "*,Auto"
                                                                Grid.children
                                                                    [ button "查看官方定价 ↗" true actions.OpenPricing
                                                                      Border.create
                                                                          [ Border.column 1
                                                                            Border.child (button "退出" true actions.Exit) ] ] ] ] ]) ] ]
                          ) ] ] ]
        :> IView

    do
        this.Title <- "梁文谷"
        this.Width <- 390.
        this.Height <- 560.
        this.MinHeight <- 430.
        this.WindowDecorations <- WindowDecorations.None
        this.CanResize <- false
        this.ShowInTaskbar <- false
        this.Topmost <- true
        this.Background <- Brushes.Transparent

        this.TransparencyLevelHint <-
            [| WindowTransparencyLevel.AcrylicBlur
               WindowTransparencyLevel.Blur
               WindowTransparencyLevel.Transparent
               WindowTransparencyLevel.None |]

        this.FontFamily <-
            FontFamily(
                if initialMac then
                    "-apple-system, PingFang SC"
                else
                    "Segoe UI, Microsoft YaHei UI"
            )

        this.Content <-
            Component(fun ctx ->
                let value = ctx.usePassed state
                let model, mac, dark, tip = value.Current
                render model mac dark tip)

        this.ActualThemeVariantChanged.Add(fun _ ->
            let model, mac, _, tip = state.Current
            state.Set(model, mac, this.ActualThemeVariant = ThemeVariant.Dark, tip))

        this.SizeChanged.Add(fun _ -> state.Set state.Current)

        this.KeyDown.Add(fun e ->
            if e.Key = Key.Escape then
                this.Hide()
                e.Handled <- true)

        this.Closing.Add(fun e ->
            if not allowClose then
                e.Cancel <- true
                this.Hide())

    member _.Update(model) =
        let _, mac, dark, tip = state.Current

        if model <> (let current, _, _, _ = state.Current in current) then
            state.Set(model, mac, dark, tip)

    member _.SetPlatform(mac) =
        this.FontFamily <-
            FontFamily(
                if mac then
                    "-apple-system, PingFang SC"
                else
                    "Segoe UI, Microsoft YaHei UI"
            )

        let model, _, dark, tip = state.Current
        state.Set(model, mac, dark, tip)

    member _.SetTip(offset) =
        let model, mac, dark, _ = state.Current
        state.Set(model, mac, dark, max 20. (min 370. offset))

    member _.Shutdown() =
        allowClose <- true
        this.Close()
        peak.Dispose()
        valley.Dispose()
        appIcon.Dispose()
        state.Dispose()
