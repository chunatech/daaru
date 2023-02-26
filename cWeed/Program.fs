module cWeed
// cWeed is a good source of Selenium, built on top of Canopy
// https://lefthandedgoat.github.io/canopy/index.html
// https://www.selenium.dev/about/

// Project was initially adapted from an example projects in these articles:
// http://kcieslak.io/Dynamically-extending-F-applications
// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html

open System.IO
open System.Collections.Generic
open System.Collections.ObjectModel


// type defined in original example project
// type Transformation = string -> string


// module Register =
//     type private Msg =
//         | Add of string * Transformation
//         | Remove of string
//         | Get of AsyncReplyChannel<Transformation list>

//     let private register =
//         MailboxProcessor.Start (fun inbox ->
//             // Define processing loop
//             let rec loop lst = async {

//                 // Receive message
//                 let! msg = inbox.Receive()

//                 // Process message and kick off next iteration
//                 match msg with
//                 | Add (n,f) ->
//                     return! loop ((n,f)::lst)
//                 | Remove n ->
//                     return! loop (lst |> List.filter(fun (f,_) -> f <> n))
//                 | Get rc ->
//                     let l = lst |> List.map snd
//                     rc.Reply l
//                     return! loop lst
//             }
//             loop [] )

//     let add name fnc =
//         (name, fnc) |> Add |> register.Post

//     let remove name =
//         name |> Remove |> register.Post

//     let get () =
//         register.PostAndReply Get


// Evauluator of the constructed/loaded .fsx files with a non-interactive FSI session
// module Evaluator =
//     open System.Globalization
//     open System.Text
//     open Microsoft.FSharp.Compiler.Interactive.Shell

//     // Init string builders to be used for output and error streams
//     let sbOut = StringBuilder()
//     let sbErr = StringBuilder()

//     let fsi =
//         let inStream = new StringReader("")
//         let outStream = new StringWriter(sbOut)
//         let errStream = new StringWriter(sbErr)

//         try
//             // Create FSI evaluation session
//             // First arg in argv below may not be evaluated,
//             // however subsequent args will be.
//             // https://github.com/fsharp/fsharp-compiler-docs/issues/877
//             let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
//             let argv = [| "fsi.exe"; "--noninteractive" |]
//             FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream)
//         with
//         | ex ->
//             printfn "Error: %A" ex
//             printfn "Inner: %A" ex.InnerException
//             printfn "ErrorStream: %s" (errStream.ToString())
//             raise ex


//     let getOpen path =
//         let path = Path.GetFullPath path
//         let filename = Path.GetFileNameWithoutExtension path
//         let textInfo = (CultureInfo("en-US", false)).TextInfo
//         textInfo.ToTitleCase filename


//     let getLoad path =
//          let path = Path.GetFullPath path
//          path.Replace("\\", "\\\\")


//     let evaluate path =
//         let filename = getOpen path
//         let load = getLoad path

//         let _, errs = fsi.EvalInteractionNonThrowing(sprintf "#load \"%s\";;" load)
//         if errs.Length > 0 then printfn "Load Errors : %A" errs

//         let _, errs = fsi.EvalInteractionNonThrowing(sprintf "open %s;;" filename)
//         if errs.Length > 0 then printfn "Open Errors : %A" errs

//         let res,errs = fsi.EvalExpressionNonThrowing "map"
//         if errs.Length > 0 then printfn "Get map Errors : %A" errs

//         match res with
//         | Choice1Of2 (Some f) ->
//             f.ReflectionValue :?> Transformation |> Some
//         | _ -> None


// Filtered watcher of filesystem, to look for any new .cwt or .fsx files, then take action on them.
module Watcher =
    let create filter addCb rmCb updateCb dir =
        if Directory.Exists dir |> not then Directory.CreateDirectory dir |> ignore
        let watcher = new FileSystemWatcher()
        watcher.Filter <- filter
        watcher.Path <- dir
        watcher.Created.Add (fun n -> n.FullPath |> addCb)
        watcher.Deleted.Add (fun n -> n.FullPath |> rmCb)
        watcher.Renamed.Add (fun n -> n.OldFullPath |> rmCb; n.FullPath |> addCb)
        watcher.Changed.Add (fun n -> n.FullPath |> updateCb)
        watcher.SynchronizingObject <- null
        watcher.EnableRaisingEvents <- true

        watcher
    
    let rec createForDirs addCb rmCb updateCb dirList watcherList =
        match dirList with
        | [] -> watcherList
        | dir :: dirs -> 
            let watcherList = [create "*.cwt" addCb rmCb updateCb dir] @ watcherList
            let watcherList = [create "*.fsx" addCb rmCb updateCb dir] @ watcherList
            createForDirs addCb rmCb updateCb dirs watcherList



[<EntryPoint>]
let main argv =

    let remove path =
        printfn "%s removed" path
        // let fn = Path.GetFileNameWithoutExtension path
        // Register.remove fn

    let add path =
        printfn "%s added" path
        // let fn = Path.GetFileNameWithoutExtension path
        // match Evaluator.evaluate path |> Option.map (fun ev -> Register.add fn ev ) with
        // | Some _ -> ()
        // | None -> printfn "File `%s` couldn't be parsed" path

    let update path =
        printfn "%s added or updated" path

    let watcherList = Watcher.createForDirs add remove update ["scripts"] []
    printfn "%A" watcherList

    // while true do
    //     let input = System.Console.ReadLine ()
    //     let lst = Register.get ()
    //     let res = lst |> List.fold (fun s e -> e s ) input
    //     printfn "Result: %s" res

    let curDirInfo = DirectoryInfo(".")
    printfn "%s" curDirInfo.FullName

    while true do
        let unused = System.Console.ReadLine ()
        printfn "%s" unused

    0 // return an integer exit code
