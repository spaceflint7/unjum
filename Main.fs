module com.spaceflint.unjum.Main

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

//
// Background
//

type State =
    {
        Texture: Texture2D
        Pixels: Color[]
        Width: int
        Height: int
        MenuLayer: Xna.Layer
        GameLayer: Xna.Layer
        FpsLayer: Xna.Layer
        Mailbox: Mailbox<string>
    }

let drawBackground state =

    let pingpong t n =
        let n' = n * 2
        let t = t % n'
        if t >= 0 && t <= n then t else n' - t

    let n = int (Xna.totalTime * 0.05)
    System.Array.Fill (state.Pixels, Color (32 + pingpong (n / 3) 32,
                                            32 + pingpong (n / 5) 64,
                                            32 + pingpong (n / 7) 96,
                                            255))

    for i in 3 .. 11 do
        let x = ((1234 + n) / i) % state.Width
        let y = ((1234 + n) / i) % state.Height
        let c = Color (i * 255 / 10, i * 255 / 9, i * 255 / 8)
        state.Pixels.[y * state.Width + x] <- c

    state.Texture.SetData state.Pixels
    Xna.drawTexture state.Texture Xna.screenRect Color.White

    state

let initBackground state =

    //let w = Xna.screenRect.Width / 8
    //let h = Xna.screenRect.Height / 8

    let w = 32
    let h = w * Xna.screenRect.Height / Xna.screenRect.Width

    { state with    Texture = Xna.newTexture w h
                    Pixels = Array.zeroCreate (w * h)
                    Width = w
                    Height = h }

//
// FPS counter
//

module ``FPS Counter`` =

    type State =
        {
            FramesPerSecond: float
            StartTime: float
            StartFrames: int
        }

    let draw state =

        let str = $"{state.FramesPerSecond:N0} FPS"
        let scale = Xna.virtualToRealScale str 50f
        let w, h = Xna.measureString str scale
        let x = Xna.screenRect.Width - w
        let y = Xna.screenRect.Height - h
        Xna.drawString str (Point (x,y)) scale Color.Green

        if Xna.totalTime - state.StartTime < 1000. then state
        else    { state with    FramesPerSecond = float (Xna.frameCount - state.StartFrames)
                                                * 1000. / (Xna.totalTime - state.StartTime)
                                StartTime = Xna.totalTime
                                StartFrames = Xna.frameCount }

    let init (layer: Xna.Layer<State>) =

        layer.draw <- draw

        {
            FramesPerSecond = 30.
            StartTime = 0.
            StartFrames = 0
        }

//
// Main function
//

let updateMode state =

    match (state.Mailbox.TryReceive 0) |> Async.RunSynchronously with

    | Some "GAME" -> Xna.disableLayer state.MenuLayer
                     Xna.enableLayer  state.GameLayer

    | Some "MENU" -> Xna.disableLayer state.GameLayer
                     Xna.enableLayer  state.MenuLayer

    | _ -> ()

    state

let init (layer: Xna.Layer<State>) =

    Persist.initialize ()
    Xna.theGame.onPause <- Persist.shortSave

    layer.update <- updateMode
    layer.draw <- drawBackground

    let mailbox = newMailbox<string> ()

    {
        Texture = null
        Pixels = null
        Width = 0
        Height = 0

        MenuLayer = Xna.newLayer (Menu.init mailbox) true
        GameLayer = Xna.newLayer (Game.init mailbox) false
        FpsLayer = Xna.newLayer (``FPS Counter``.init) true
        Mailbox = mailbox

    } |> initBackground

[<EntryPoint>]
let main argv = Xna.run<_> init
