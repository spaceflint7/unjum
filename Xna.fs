module Xna

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Microsoft.Xna.Framework.Audio

//
// TheGame object
//

let mutable frameCount: int = 1
let mutable frameTime: float = 0.
let mutable totalTime: float = 0.
let mutable screenRect = Rectangle ()
let mutable pixelsPerInch: int = 100
let mutable whitePixel: Texture2D = null

let mutable private escapePressCount = 0

let mutable private mousePressAt = Point (-1, -1)
let mutable private mouseReleased = false

type TheGame (initFunc) as this =
    inherit Microsoft.Xna.Framework.Game ()

    [<DefaultValue>] val mutable public onPause: unit -> unit

    do

        base.Content.RootDirectory <- "Content"
        this.onPause <- fun () -> ()

        let gm = new GraphicsDeviceManager (this)
        gm.PreparingDeviceSettings.Add (fun args ->
            let pp = args.GraphicsDeviceInformation.PresentationParameters
            let (w, h) = (this.Window.ClientBounds.Width, this.Window.ClientBounds.Height)
            if this.Window.ScreenDeviceName = "Android" then
                // on Android we should always have portrait orientation
                pp.BackBufferWidth <- w
                pp.BackBufferHeight <- h
            else
                // on Windows, pick the larger axis as the height
                pp.BackBufferWidth <- min w h
                pp.BackBufferHeight <- max w h
            pp.DisplayOrientation <- DisplayOrientation.Portrait
            pp.RenderTargetUsage <- RenderTargetUsage.PreserveContents
            pp.PresentationInterval <- PresentInterval.Two // 30 fps
            pp.DepthStencilFormat <- DepthFormat.None
            screenRect <- Rectangle(0, 0, pp.BackBufferWidth, pp.BackBufferHeight)
        )

    member val public SpriteBatch = Unchecked.defaultof<_> with get, set
    member val public SpriteFont = Unchecked.defaultof<_> with get, set
    member val public FontHeight = Unchecked.defaultof<_> with get, set
    //member val public FontScale = Unchecked.defaultof<_> with get, set

    override this.Initialize () =

        // set update timestamp to correspond to 30 fps
        this.TargetElapsedTime <- System.TimeSpan.FromSeconds (1. / 30.)
        this.IsMouseVisible <- true

        this.SpriteBatch <- new SpriteBatch (this.GraphicsDevice)
        this.SpriteFont <- this.Content.Load<SpriteFont> ("myfont")
        this.SpriteFont.LineSpacing <- 0
        let fontSize = this.SpriteFont.MeasureString "I"
        //this.FontScale <- fontSize.Y / float32 screenRect.Height
        this.FontHeight <- fontSize.Y

        whitePixel <- new Texture2D (this.GraphicsDevice, 1, 1)
        whitePixel.SetData [| 0xFFFFFFFF |]

        pixelsPerInch <-
            let wScreen = this.GraphicsDevice.DisplayMode.Width
            let hScreen = this.GraphicsDevice.DisplayMode.Height
            let dScreen = sqrt (float32 (wScreen * wScreen + hScreen * hScreen))
            let wWindow = this.GraphicsDevice.Viewport.Width
            let hWindow = this.GraphicsDevice.Viewport.Height
            let dWindow = sqrt (float32 (wWindow * wWindow + hWindow * hWindow))
            let ppiInWindows = 100f
            (int) (dWindow * ppiInWindows / dScreen)

        base.Initialize ()

        initFunc ()

    override this.Update (gameTime) =

        if Input.Keyboard.GetState().IsKeyDown(Input.Keys.Escape) then
            escapePressCount <- escapePressCount + 1
        elif escapePressCount > 0 then
            escapePressCount <- -escapePressCount

        frameTime <- gameTime.ElapsedGameTime.TotalMilliseconds
        totalTime <- gameTime.TotalGameTime.TotalMilliseconds
        base.Update gameTime

    override this.Draw (gameTime) =

        let mouse = Input.Mouse.GetState ()
        if mouse.LeftButton = ButtonState.Pressed then
            if mousePressAt.X < 0 then
                mousePressAt <- Point (mouse.X, mouse.Y)
        else
            mouseReleased <- true

        this.GraphicsDevice.Textures.[0] <- null
        this.GraphicsDevice.Clear Color.Black
        this.SpriteBatch.Begin (SpriteSortMode.Deferred, BlendState.AlphaBlend,
                                SamplerState.PointClamp, DepthStencilState.None,
                                RasterizerState.CullNone)
        base.Draw (gameTime)
        this.SpriteBatch.End ()
        frameCount <- frameCount + 1

        if mouseReleased then
            mousePressAt <- Point (-1, -1)
            mouseReleased <- false

    override this.OnDeactivated (sender: obj, args: System.EventArgs) =
        this.onPause ()

let mutable theGame: TheGame = Unchecked.defaultof<_>

let escapePressed () =
    if escapePressCount >= 0 then false else escapePressCount <- 0; true

//
// textures
//

let mutable private textureMap = Map.empty<string, Texture2D>

let loadTexture name =

    let create () =
        use stream = TitleContainer.OpenStream $"{theGame.Content.RootDirectory}/{name}.png"
        let tex = Texture2D.FromStream (theGame.GraphicsDevice, stream)
        textureMap <- textureMap |> Map.add name tex
        Some tex

    textureMap |> Map.tryFind name |> Option.orElseWith create |> Option.get

let newTexture width height = new Texture2D (theGame.GraphicsDevice, width, height)

let drawTexture tex (rect: Rectangle) color =

    theGame.SpriteBatch.Draw (tex, rect, color)

let getTexturePixels (tex: Texture2D) =

    let pixels = Array.zeroCreate<Color> (tex.Width * tex.Height)
    tex.GetData pixels
    pixels

//
// string output
//

let virtualToRealPoint x y =
    (x * screenRect.Width / 1000, y * screenRect.Height / 1000)

let virtualToRealScale (str: string) height =

    let v = theGame.SpriteFont.MeasureString str
    let scale = (height * float32 screenRect.Height / 1000f) / theGame.FontHeight
    let w = v.X * scale
    if w <= float32 screenRect.Width then scale
    else scale * float32 screenRect.Width / float32 w

let measureString (str: string) scale =

    let v = theGame.SpriteFont.MeasureString str
    v.X * scale |> int, v.Y * scale |> int

let drawString (str: string) (pos: Point) (scale: float32) (color: Color) =

    let pos = Vector2 (float32 pos.X, float32 pos.Y)
    theGame.SpriteBatch.DrawString (theGame.SpriteFont, str, pos, color,
                                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f)

let drawStringButton (str: string) (pos: Point) (scale: float32) (color: Color) f =

    drawString str pos scale color
    let mouse = Input.Mouse.GetState ()

    let drawAndCheck released =
        let w, h = measureString str scale
        let p = h / 4
        let r = Rectangle (pos.X + (4f * scale |> int), pos.Y + p + (6f * scale |> int), w - 3, h - p * 2)
        if r.Contains mousePressAt && r.Contains (mouse.X, mouse.Y) then
            drawTexture whitePixel r color
            let color = Color (255 - int color.R, 255 - int color.G, 255 - int color.B)
            drawString str pos scale color
            if released then f ()

    if mouse.LeftButton = ButtonState.Pressed then
        drawAndCheck false
    elif mousePressAt.X <> -1 then
        drawAndCheck true

//
// audio
//

let mutable private soundMap = Map.empty<string, SoundEffect>

let loadSound name =

    let create () =
        let snd = theGame.Content.Load<SoundEffect> name
        soundMap <- soundMap |> Map.add name snd
        Some snd

    soundMap |> Map.tryFind name |> Option.orElseWith create |> Option.get

let playMusic (name: string) vol =

    Microsoft.Xna.Framework.Media.MediaPlayer.IsRepeating <- true
    Microsoft.Xna.Framework.Media.MediaPlayer.Volume <- vol

    Microsoft.Xna.Framework.Media.Song.FromUri (name,
            System.Uri ($"{theGame.Content.RootDirectory}/{name}.mp3",
                        System.UriKind.Relative))
        |> Microsoft.Xna.Framework.Media.MediaPlayer.Play

let pauseMusic () = Microsoft.Xna.Framework.Media.MediaPlayer.Pause ()

let resumeMusic () = Microsoft.Xna.Framework.Media.MediaPlayer.Resume ()

//
// component management
//

type Layer = Microsoft.Xna.Framework.DrawableGameComponent

type Layer<'T> (initFunc) as this =

    inherit Layer (theGame)

    let mutable state: 'T = Unchecked.defaultof<'T>

    [<DefaultValue>] val mutable public update: ('T -> 'T)
    [<DefaultValue>] val mutable public draw: ('T -> 'T)

    do

        this.update <- fun x ->
            this.update <- fun x -> x
            state <- initFunc this
            this.update state

        this.draw <- fun x -> x

    override this.Update (gameTime) =

        state <- this.update state

    override this.Draw (gameTime) =

        state <- this.draw state

    member _.Call<'S> f : 'S = f state

let enableOrDisableLayer (layer: Layer) enabled =

    layer.Enabled <- enabled
    layer.Visible <- enabled

let inline enableLayer layer = enableOrDisableLayer layer true

let inline disableLayer layer = enableOrDisableLayer layer false

let newLayer<'T> initFunc enabled =

    let layer = new Layer<'T> (initFunc)
    if not enabled then disableLayer layer
    theGame.Components.Add layer
    layer

//
// run (main function)
//

let run<'T> initFunc =

    let mutable returnCode = 0
    System.AppDomain.CurrentDomain.UnhandledException.Add <| fun args ->
        returnCode <- 1

        match args.ExceptionObject with
        | :? System.PlatformNotSupportedException as ex
                when ex.StackTrace.Contains "WaitForAsyncOperationToFinish"
                    ->  // XNA might call Thread.Abort after we return from main,
                        // and on .NET 5, this triggers PlatformNotSupportedException,
                        // so just exit the process
                        (System.Diagnostics.Process.GetCurrentProcess ()).Kill ()

        | :? System.Reflection.TargetInvocationException as ex
                    ->  ex.InnerException.ToString()
                            |> System.Windows.Forms.MessageBox.Show
                            |> ignore

        | ex        ->  ex.ToString()
                            |> System.Windows.Forms.MessageBox.Show
                            |> ignore

    theGame <- new TheGame (fun () -> newLayer<'T> initFunc true |> ignore)
    theGame.Run()

    returnCode
