module cWeed
// cWeed is a good source of Selenium, powered by Canopy
// https://lefthandedgoat.github.io/canopy/index.html
// https://www.selenium.dev/about/

// Project was initially adapted from example projects in these articles:
// http://kcieslak.io/Dynamically-extending-F-applications
// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html

open System.IO

// Open modules internal to the project
open Register
open Watcher
open Evaluator


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