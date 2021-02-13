[<AutoOpen>]
module Utils

let inline dprint s = System.Diagnostics.Debug.WriteLine s

let inline dprintfn fmt = Printf.ksprintf dprint fmt

type Mailbox<'T> = MailboxProcessor<'T>
let inline newMailbox<'T> () = new Mailbox<'T> (Unchecked.defaultof<_>, Unchecked.defaultof<_>)

type XorShiftRandom (seed: uint64) =

    let mutable x = seed <<< 1
    let mutable y = seed >>> 1

    member _.Next () =

        let x' = y;
        x <- x ^^^ (x <<< 23)
        let y' = x ^^^ y ^^^ (x >>> 17) ^^^ (y >>> 26)
        let r = y' + y
        x <- x'
        y <- y'
        (int) r
