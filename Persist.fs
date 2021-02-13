module Persist
open System
open System.IO
open System.Text
open Microsoft.Xna.Framework.Storage

type private Term = LongTerm | ShortTerm

type private Data =
    {
        name: string
        term: Term
        mutable file: Stream
        mutable map: Map<string, int>
    }

let VERSION_1 = 0x10101
//let mutable private file : Stream = null
//let mutable private map = Map.empty<string, int>

let private load data =

    let _open () =

        let result = StorageDevice.BeginShowSelector (null, null)
        result.AsyncWaitHandle.WaitOne () |> ignore
        let device = StorageDevice.EndShowSelector result
        result.AsyncWaitHandle.Close ()

        let result = device.BeginOpenContainer ("Persist", null, null)
        result.AsyncWaitHandle.WaitOne () |> ignore
        let container = device.EndOpenContainer result
        result.AsyncWaitHandle.Close ()

        container.OpenFile (data.name, FileMode.OpenOrCreate)

    let _read () =

        use reader = new BinaryReader (data.file, Encoding.UTF8, true)
        if reader.ReadInt32 () = VERSION_1 then
            for _ in 1 .. reader.ReadInt32 () do
                let key = reader.ReadString ()
                let value = reader.ReadInt32 ()
                data.map <- data.map |> Map.add key value

    try
        data.map <- Map.empty
        data.file <- _open ()
        if data.file.Length > 4L then
            _read ()
            if data.term = ShortTerm then
                data.file.SetLength 0L
                data.file.Flush ()
    with _ -> ()

let private save data =

    data.file.Position <- 0L
    use writer = new BinaryWriter (data.file, Encoding.UTF8, true)
    writer.Write VERSION_1
    writer.Write (data.map |> Map.count)
    data.map |> Map.iter (fun key value ->
        writer.Write key
        writer.Write value
    )

let private longTermData =
    {
        name = "state.long.v1.bin"
        term = LongTerm
        file = Unchecked.defaultof<_>
        map = Unchecked.defaultof<_>
    }

let private shortTermData =
    {
        name = "state.short.v1.bin"
        term = ShortTerm
        file = Unchecked.defaultof<_>
        map = Unchecked.defaultof<_>
    }

let initialize () =
    load longTermData
    load shortTermData

let inline private get key data        = data.map |> Map.tryFind key
let inline private set key value data  = data.map <- data.map |> Map.add key value

let shortGet key = get key shortTermData
let shortSet key value = set key value shortTermData
let shortSave () = save shortTermData

let longGet key = get key longTermData
let longSet key value = if longGet key <> Some value
                        then    set key value longTermData
                                save longTermData
