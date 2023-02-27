module cWeed
// cWeed is a good source of Selenium, built on top of Canopy
// https://lefthandedgoat.github.io/canopy/index.html
// https://www.selenium.dev/about/

// Project was initially adapted from example projects in these articles:
// http://kcieslak.io/Dynamically-extending-F-applications
// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html

open System.IO
open System.Collections.Generic
open System.Collections.ObjectModel



module Register =
    type private Msg =
        | Add of path: string
        | Remove of string
        | Get of AsyncReplyChannel<string list>

    let private register: MailboxProcessor<Msg> =
        MailboxProcessor.Start (fun (inbox: MailboxProcessor<Msg>) ->
            // Define processing loop
            let rec loop (lst: string list) = async {

                // Receive message
                let! (msg: Msg) = inbox.Receive()

                // Process message and kick off next iteration
                match msg with
                | Add (n: string) ->
                    return! loop (n::lst)
                | Remove (n: string) ->
                    return! loop (lst |> List.filter(fun (f: string) -> f <> n))
                | Get (rc: AsyncReplyChannel<string list>) ->
                    rc.Reply lst
                    return! loop lst
            }
            loop [] )

    let add (name: string) =
        name |> Add |> register.Post

    let remove (name: string) =
        name |> Remove |> register.Post

    let get () =
        register.PostAndReply Get



// Evauluator of the constructed/loaded .fsx files with a non-interactive FSI session
module Evaluator =
    open System.Globalization
    open System.Text
    open FSharp.Compiler.Interactive.Shell

    // Init string builders to be used for output and error streams
    let sbOut: StringBuilder = StringBuilder()
    let sbErr: StringBuilder = StringBuilder()

    let fsi: FsiEvaluationSession =
        let inStream: StringReader = new StringReader("")
        let outStream: StringWriter = new StringWriter(sbOut)
        let errStream: StringWriter = new StringWriter(sbErr)

        try
            // Create FSI evaluation session
            // First arg in argv below may not be evaluated,
            // however subsequent args will be.
            // https://github.com/fsharp/fsharp-compiler-docs/issues/877
                let fsiConfig: FsiEvaluationSessionHostConfig = FsiEvaluationSession.GetDefaultConfiguration()
                let argv: string[] = [| "fsi.exe"; "--noninteractive" |]
                FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream)
        with
        | (ex: exn) ->
            printfn "Error: %A" ex
            printfn "Inner: %A" ex.InnerException
            printfn "ErrorStream: %s" (errStream.ToString())
            raise ex


    let getOpen (path: string) =
        let path: string = Path.GetFullPath path
        let filename: string = Path.GetFileNameWithoutExtension path
        let textInfo: TextInfo = (CultureInfo("en-US", false)).TextInfo
        textInfo.ToTitleCase filename


    let getLoad (path: string) =
         let path: string = Path.GetFullPath path
         path.Replace("\\", "\\\\")


    let evaluate (path: string) =
        let filename: string = getOpen path
        let load: string = getLoad path

        let _, (errs: FSharp.Compiler.Diagnostics.FSharpDiagnostic[]) = fsi.EvalInteractionNonThrowing(sprintf "#load \"%s\";;" load)
        if errs.Length > 0 then printfn "Load Errors : %A" errs

        let _, (errs: FSharp.Compiler.Diagnostics.FSharpDiagnostic[]) = fsi.EvalInteractionNonThrowing(sprintf "open %s;;" filename)
        if errs.Length > 0 then printfn "Open Errors : %A" errs

        let (res: Choice<FsiValue option,exn>),(errs: FSharp.Compiler.Diagnostics.FSharpDiagnostic[]) = fsi.EvalExpressionNonThrowing(sprintf "map;;")
        if errs.Length > 0 then printfn "Get map Errors : %A" errs

        // match res with
        // | Choice1Of2 (Some (f: FsiValue)) ->
        //     f.ReflectionValue :?> Transformation |> Some
        // | _ -> None
        ()



// Filtered watcher of filesystem, to look for any new .cwt or .fsx files, then take action on them.
module Watcher =
    let create (filter: string) addCb rmCb updateCb (dir: string) =
        if Directory.Exists dir |> not then Directory.CreateDirectory dir |> ignore
        let watcher: FileSystemWatcher = new FileSystemWatcher()
        watcher.Filter <- filter
        watcher.Path <- dir
        watcher.Created.Add (fun (n: FileSystemEventArgs) -> n.FullPath |> addCb)
        watcher.Deleted.Add (fun (n: FileSystemEventArgs) -> n.FullPath |> rmCb)
        watcher.Renamed.Add (fun (n: RenamedEventArgs) -> n.OldFullPath |> rmCb; n.FullPath |> addCb)
        // watcher.Changed.Add (fun (n: FileSystemEventArgs) -> n.FullPath |> updateCb)
        watcher.SynchronizingObject <- null
        watcher.EnableRaisingEvents <- true

        watcher


    let remove (path: string) =
        printfn "%s removed" path
        // let fn = Path.GetFileNameWithoutExtension path
        // Register.remove fn


    let add (path: string) =
        printfn "%s added" path
        // Evaluator.evaluate path
        Register.add path


    let update (path: string) =
        printfn "%s added or updated" path
    

    let rec createForDirs (dirList: string list) (watcherList: FileSystemWatcher list) =
        match dirList with
        | [] -> watcherList
        | (dir: string) :: (dirs: string list) -> 
            let watcherList: FileSystemWatcher list = [create "*.cwt" add remove update dir] @ watcherList
            let watcherList: FileSystemWatcher list = [create "*.fsx" add remove update dir] @ watcherList
            createForDirs dirs watcherList



[<EntryPoint>]
let main (argv: string[]) =
    // Special space for TunaKr0n:


    let watcherList: FileSystemWatcher list = Watcher.createForDirs ["scripts"] []

    let curDirInfo: DirectoryInfo = DirectoryInfo(".")
    printfn "%s" curDirInfo.FullName

    while true do
        let input = System.Console.ReadLine ()
        let lst = Register.get ()

        let res: string list = lst |> List.filter (fun (x: string) -> x.EndsWith input)
        printfn "Result: %A" res

    0 // return an integer exit code
