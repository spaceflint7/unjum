module Menu

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

type State =
    {
        Sound: bool
        Mailbox: Mailbox<string>
    }

let drawCenter str row color func =

    let scale = Xna.virtualToRealScale str 250f
    let w, _ = Xna.measureString str scale
    let _, y = Xna.virtualToRealPoint 0 row
    let pos = Point ((Xna.screenRect.Width - w) / 2, y)
    match func with
    | None      -> Xna.drawString str pos scale color
    | Some f    -> Xna.drawStringButton str pos scale color f

let drawFace str =

    let scale = Xna.virtualToRealScale str 250f
    let _, h = Xna.measureString str scale
    let x, y = Xna.virtualToRealPoint 325 0
    let w, _ = Xna.virtualToRealPoint 250 1
    let x = x + (sin (Xna.totalTime / 200.) * 50. |> int)
    let y = y + h / 4
    Xna.drawTexture (Xna.loadTexture "face") (Rectangle (x, y, w, w)) Color.White

let drawMenu state =

    let title = "UN JUM"
    drawFace   title
    drawCenter title 23 Color.DarkSlateGray None
    drawCenter title 10 Color.LightGoldenrodYellow None

    drawCenter " PLAY " 250 Color.Yellow (Some <| fun () ->
        let n = Persist.longGet "highestLevel" |> Option.defaultValue 0
        Persist.longSet "currentLevel" (if n <= 0 then 1 else n)
        state.Mailbox.Post "GAME"
    )

    let mutable sound = state.Sound
    let label = if sound then " ON" else "OFF"
    drawCenter $"SOUND {label}" 500 Color.Yellow (Some <| fun () ->
        if sound then sound <- false ; Xna.pauseMusic ()
        else          sound <- true  ; Xna.resumeMusic ()
        Persist.longSet "soundEnabled" (if sound then 1 else 0)
    )

    drawCenter "INFO (WEB)" 700 Color.Yellow (Some <| fun () ->
        match (Xna.theGame.Window :> obj) with
        | :? System.IServiceProvider as serviceProvider ->
                match serviceProvider.GetService typeof<System.Collections.IDictionary> with
                | :? System.Collections.IDictionary as dict ->
                    match dict.Item "openUri" with
                        | :? System.Action<string> as f ->
                                f.Invoke "https://github.com/spaceflint7/unjum"
                        | _ -> ()
                | _ -> ()
        | _ -> ()
    )

    if Xna.escapePressed () then Xna.theGame.Exit ()

    { state with Sound = sound }

let mutable isSoundOn : unit -> bool = Unchecked.defaultof<_>

let init (mailbox: Mailbox<string>) (layer: Xna.Layer<State>) =

    isSoundOn <- fun () -> layer.Call (fun state -> state.Sound)
    let isSoundOn = (Persist.longGet "soundEnabled" |> Option.defaultValue 1) <> 0

    Xna.playMusic "RubixCube" 0.1f
    if not isSoundOn then Xna.pauseMusic ()

    layer.draw <- drawMenu

    let n = Persist.longGet "currentLevel" |> Option.defaultValue 0
    if n <= 0 then Persist.longSet "currentLevel" 1
    elif Persist.shortGet "gameInProgress" = Persist.longGet "currentLevel"
    then Xna.disableLayer layer ; mailbox.Post "GAME"

    {
        Sound = isSoundOn
        Mailbox = mailbox
    }
