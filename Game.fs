module Game

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Microsoft.Xna.Framework.Audio

type PressedState =
    | NotPressed
    | InvalidPress
    | ValidPress of {| Where: Point ; When: float |}
    | CheckmarkPress
    | CheckmarkRelease

type State =
    {
        CurrentLevel: int
        HighestLevel: int
        SwitchToLevel: int
        ReservedHeight: int
        OriginalImage: Texture2D
        ScrambledImage: Texture2D
        ScrambledPixels: Color[]
        SquareSize: int
        PressedState: PressedState
        DragOffset: Point
        TimeOfWin: float
        WinSoundEffect: SoundEffectInstance
        Mailbox: Mailbox<string>
    }

let FONT_SCALE = 75f
let SCREENS_COUNT = 20

let createInitialState level mailbox =

    Persist.longSet "currentLevel" level
    if level > (Persist.longGet "highestLevel" |> Option.defaultValue 0)
    then Persist.longSet "highestLevel" level

    {
        CurrentLevel = level
        HighestLevel = Persist.longGet "highestLevel" |> Option.defaultValue 0
        SwitchToLevel = 0
        ReservedHeight = 0
        OriginalImage = null
        ScrambledImage = null
        ScrambledPixels = null
        SquareSize = 0
        PressedState = NotPressed
        DragOffset = Point.Zero
        TimeOfWin = 0.
        WinSoundEffect = null
        Mailbox = mailbox
    }

let startWinSound state timeOfWin =

    if timeOfWin = 0. then null
    elif not <| Menu.isSoundOn () then null else
    match state.WinSoundEffect with
    | null ->   Xna.pauseMusic ()
                let ef = (Xna.loadSound "THX").CreateInstance ()
                ef.Pitch <- 0f
                ef.Volume <- 0.15f
                ef.Play ()
                ef
    | ef ->     ef

let stopWinSound state =

    if not <| isNull state.WinSoundEffect then
        state.WinSoundEffect.Stop ()
        if Menu.isSoundOn () then Xna.resumeMusic ()

(*let resumeMusicWhenSoundStops state =

    if    not <| isNull state.WinSoundEffect
       && state.WinSoundEffect.State = SoundState.Stopped
       && Menu.isSoundOn () then Xna.resumeMusic () *)

let scramblePixels state =

    let rnd = XorShiftRandom (uint64 state.CurrentLevel)
    let pixels = state.ScrambledPixels
    let len = pixels.Length
    let width = state.OriginalImage.Width

    let isEdgeSquare index =
        pixels.[index] <> Color.Transparent && (
               (index >= width      && pixels.[index - width] = Color.Transparent)
            || (index >= 1          && pixels.[index - 1] = Color.Transparent)
            || (index < len - 1     && pixels.[index + 1] = Color.Transparent)
            || (index < len - width && pixels.[index + width] = Color.Transparent)
        )

    let rec findEdge list index =
        if   index >= len then list
        elif isEdgeSquare index then findEdge (index :: list) (index + 1)
        else findEdge list (index + 1)

    let rec selectEdge index =
        let index2 = match ((abs <| rnd.Next () % 4) + 1) * 2 with
                        | 8 -> index - width
                        | 4 -> index - 1
                        | 6 -> index + 1
                        | 2 -> index + width
                        | _ -> -1 // not reached
        if index2 >= 0 && index2 < len && pixels.[index2] = Color.Transparent
        then index2 else selectEdge index

    let swap1 () =
        let edges_array = findEdge List.empty<int> 0 |> List.toArray
        let edges_index = abs <| rnd.Next () % edges_array.Length
        let index = edges_array.[edges_index]
        let index2 = selectEdge index
        if index2 >= 0 && index2 < len && pixels.[index2] = Color.Transparent then
            pixels.[index2] <- pixels.[index]
            pixels.[index]  <- Color.Transparent
            true
        else false

    let rec swapN n =
        if n > 0 then (if swap1 () then (n - 1) else n) |> swapN

    swapN (state.CurrentLevel * 10)
    state.ScrambledImage.SetData pixels
    state

let saveGameState state =

    Persist.shortSet "gameInProgress" state.CurrentLevel
    Persist.shortSet "scrambledCount" state.ScrambledPixels.Length
    state.ScrambledPixels |> Array.iteri (fun index color ->
        let baseName = $"scrambled{index:D9}"
        Persist.shortSet $"{baseName}R" (int color.R)
        Persist.shortSet $"{baseName}G" (int color.G)
        Persist.shortSet $"{baseName}B" (int color.B)
        Persist.shortSet $"{baseName}A" (int color.A)
    )

    state

let loadGameState state =

    let n = Persist.shortGet "scrambledCount" |> Option.defaultValue 0
    if n <= 1 then state |> scramblePixels
    else    for index in 0 .. (n - 1) do
                let baseName = $"scrambled{index:D9}"
                let r = Persist.shortGet $"{baseName}R" |> Option.defaultValue 0
                let g = Persist.shortGet $"{baseName}G" |> Option.defaultValue 0
                let b = Persist.shortGet $"{baseName}B" |> Option.defaultValue 0
                let a = Persist.shortGet $"{baseName}A" |> Option.defaultValue 0
                state.ScrambledPixels.[index] <- Color (r, g, b, a)
            state.ScrambledImage.SetData state.ScrambledPixels
            state

let loadImage state =

    let scale = Xna.virtualToRealScale "X" FONT_SCALE

    let fileNumber = ((state.CurrentLevel - 1) % SCREENS_COUNT) + 1
    let tex = Xna.loadTexture $"screen{fileNumber:D3}"
    let _, reservedHeight = Xna.measureString "9" scale
    let sqsz = min (Xna.screenRect.Width / tex.Width)
                   ((Xna.screenRect.Height - reservedHeight) / tex.Height)

    let pixels = Xna.getTexturePixels tex
    let tex2 = Xna.newTexture tex.Width tex.Height

    let state = { state with OriginalImage = tex
                             ScrambledImage = tex2
                             ScrambledPixels = pixels
                             ReservedHeight = reservedHeight
                             SquareSize = sqsz }

    if Persist.shortGet "gameInProgress" = Persist.longGet "currentLevel"
    then state |> loadGameState
    else state |> scramblePixels

let getImageRect state =

    let width = state.SquareSize * state.OriginalImage.Width
    let height = state.SquareSize * state.OriginalImage.Height
    let left = (Xna.screenRect.Width - width) / 2
    let top = max state.ReservedHeight ((Xna.screenRect.Height - height) / 2)
    Rectangle (left, top, width, height)

let getPixel x y state =

    let index = y * state.ScrambledImage.Width + x
    let pixels = state.ScrambledPixels
    if index >= 0 && index < pixels.Length then pixels.[index] else Color.Black

let isEmpty x y state = (getPixel x y state) = Color.Transparent

let checkInput state =

    let mouse = Input.Mouse.GetState ()

    let isValidPress (rect: Rectangle) =

        let x = (mouse.X - rect.Left) / state.SquareSize
        let y = (mouse.Y - rect.Top) / state.SquareSize

        let inline isEmpty x' y' = isEmpty (x + x') (y + y') state

        if    (not <| isEmpty 0 0)
           && (isEmpty -1 0 || isEmpty 1 0 || isEmpty 0 -1 || isEmpty 0 1)
        then Some (Point (x, y)) else None

    let doInitialPress () =

        let rect = getImageRect state
        if   state.TimeOfWin > 0. then { state with PressedState = CheckmarkPress }
        elif not (rect.Contains (mouse.X, mouse.Y)) then state
        else match isValidPress rect with
             | Some point -> { state with PressedState = ValidPress
                                      {| Where = point ; When = Xna.totalTime + 800. |} }
             | None       -> { state with PressedState = InvalidPress }

    let releasedState timeOfWin =

        match state.PressedState with
        | NotPressed -> state
        | CheckmarkPress -> { state with PressedState = CheckmarkRelease }
        | _ ->  let timeOfWin = if   timeOfWin = 0. then state.TimeOfWin
                                elif state.TimeOfWin = 0. then timeOfWin
                                else min timeOfWin state.TimeOfWin
                { state with PressedState = NotPressed
                             TimeOfWin = timeOfWin
                             WinSoundEffect = startWinSound state timeOfWin }

    let doRelease (where: Point) =

        // let offset = getPressDirection (getImageRect state) where
        let offset = state.DragOffset
        if offset <> Point.Zero then

            let index1 = where.Y * state.ScrambledImage.Width + where.X
            let index2 = index1 + offset.Y * state.ScrambledImage.Width + offset.X
            state.ScrambledPixels.[index2] <- state.ScrambledPixels.[index1]
            state.ScrambledPixels.[index1] <- Color.Transparent
            state.ScrambledImage.SetData state.ScrambledPixels

            saveGameState state |> ignore

            let originalPixels = Xna.getTexturePixels state.OriginalImage
            if Array.compareWith (fun (a: Color) b -> a.PackedValue - b.PackedValue |> int)
                                 originalPixels state.ScrambledPixels = 0
            then Xna.totalTime
            else 0.
        else 0.

    let getDragOffset (where: Point) =

        let rect = getImageRect state
        let mouseX = (mouse.X - rect.Left) / state.SquareSize - where.X |> sign
        let mouseY = (mouse.Y - rect.Top) / state.SquareSize - where.Y |> sign
        if mouseX = 0 && mouseY = 0
        then { state with DragOffset = Point.Zero }
        elif abs mouseX <> abs mouseY
           && isEmpty (where.X + mouseX) (where.Y + mouseY) state
        then { state with DragOffset = Point (mouseX, mouseY) }
        else state

    match mouse.LeftButton with
    | ButtonState.Pressed -> match state.PressedState with
                             | NotPressed       -> doInitialPress ()
                             | ValidPress info  -> getDragOffset info.Where
                             | _                -> state
    | _                   -> match state.PressedState with
                             | ValidPress info  -> doRelease info.Where
                             | _                -> 0.
                             |> releasedState

let rec update state =

    match state with
    | { OriginalImage = null } -> loadImage state
    | x when x.SwitchToLevel <> 0 ->
                    stopWinSound state
                    createInitialState state.SwitchToLevel state.Mailbox
                        |> update |> saveGameState
    | _ -> match checkInput state with
           | { PressedState = CheckmarkRelease } ->
                    update { state with SwitchToLevel = state.CurrentLevel + 1 }
           | state' -> Persist.shortSet "gameInProgress" state.CurrentLevel
                       state'

let drawButtons state =

    let scale = Xna.virtualToRealScale "X" FONT_SCALE

    let drawOneButton align str func =
        let width, _ = Xna.measureString str scale
        let left = float32 (Xna.screenRect.Width - width) * align
        let pos = Point (int left, 0)
        match func with
        | None      -> Xna.drawString str pos scale Color.Red
        | Some f    -> Xna.drawStringButton str pos scale Color.Red f

    let mutable state' = state

    if state.CurrentLevel > 1 then
        drawOneButton 0f "<<<" (Some (fun () ->
            state' <- { state with SwitchToLevel = state.CurrentLevel - 1 }))

    if state.CurrentLevel < state.HighestLevel then
        drawOneButton 1f ">>>" (Some (fun () ->
            state' <- { state with SwitchToLevel = state.CurrentLevel + 1 }))

    drawOneButton 0.5f $"#{state.CurrentLevel:D3}" None

    state'

let draw state =

    let rect = getImageRect state
    Xna.drawTexture state.OriginalImage rect (Color (96, 96, 96, 96))
    Xna.drawTexture state.ScrambledImage rect Color.White

    for y in rect.Top .. state.SquareSize .. rect.Bottom do
        let rect = Rectangle (rect.Left, y, rect.Width, 1)
        Xna.drawTexture Xna.whitePixel rect Color.Black

    for x in rect.Left .. state.SquareSize .. rect.Right do
        let rect = Rectangle (x, rect.Top, 1, rect.Height)
        Xna.drawTexture Xna.whitePixel rect Color.Black

    match state.PressedState with
    | ValidPress info ->

        let color = 64 + (sin ((info.When - Xna.totalTime) / 200.) * 63. |> int)
        let color = Color (0, 0, 0, color)
        let left = rect.Left + info.Where.X * state.SquareSize
        let top = rect.Top + info.Where.Y * state.SquareSize
        let rectSquare = Rectangle (left, top, state.SquareSize, state.SquareSize)
        Xna.drawTexture Xna.whitePixel rectSquare color

        //let offset = getPressDirection (Input.Mouse.GetState ()) rect info.Where state
        let offset = state.DragOffset
        if offset <> Point.Zero then
            rectSquare.Offset (offset.X * state.SquareSize, offset.Y * state.SquareSize)
            Xna.drawTexture Xna.whitePixel rectSquare color

    | _ -> ()

    if state.TimeOfWin > 0. then
        let t = max (Xna.totalTime - state.TimeOfWin |> int) 0 |> min 500
        let w = Xna.screenRect.Width * t / 500
        let h = Xna.screenRect.Height * t / 500
        let cR = sin (Xna.totalTime * 0.01) * 0.5 + 0.5 |> float32
        let cG = cos (Xna.totalTime * 0.01) * 0.5 + 0.5 |> float32
        let c = Color (cR, cG, 0f, 1f)
        Xna.drawTexture (Xna.loadTexture "checkmark") (Rectangle (0, 0, w, h)) c

    if Xna.escapePressed () then
        stopWinSound state
        Persist.shortSet "gameInProgress" -1
        state.Mailbox.Post "MENU"
        state
    else
        drawButtons state
    (*else
        resumeMusicWhenSoundStops state*)

let init mailbox (layer: Xna.Layer<State>) =

    layer.update <- update
    layer.draw <- draw

    createInitialState (Persist.longGet "currentLevel" |> Option.defaultValue 1) mailbox
