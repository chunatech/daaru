module cWeed
// cWeed is a good source of Selenium, powered by Canopy
// https://lefthandedgoat.github.io/canopy/index.html
// https://www.selenium.dev/about/

// Project was initially adapted from example projects in these articles:
// http://kcieslak.io/Dynamically-extending-F-applications
// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html

open System.IO
open Thoth.Json.Net

// Open modules internal to the project
open Configuration
open Register
open Watcher
open Evaluator


[<EntryPoint>]
let main (argv: string[]) =
    // Recursively search each script directory defined in config
    // For each directory and subdirectory:

    let config = BaseConfiguration.readFromFileOrDefault BaseConfiguration.defaultBaseConfigurationFilePath
    printfn "\nConfig:  %A" config 


    // -- Build .cwt and .fsx watchers for that directory
    // -- Add watchers to list for tracking and cleanup later
    let watcherList: FileSystemWatcher list = Watcher.createForDirs ["scripts"] []


    // -- Find any local config file for that directory


    // -- Find any .cwt or .fsx files in that directory
    // -- For each file found:


    // -- -- Read head of file to check for config override


    // -- -- Build running config record for file, layer configs like so:
    // -- -- base/default config <- local dir config <- override from file


    // -- -- Build record for file, containing running config, path,
    // -- -- and timestamps for last run, last failure, and last success


    // -- -- Add record for file to Register


    let curDirInfo: DirectoryInfo = DirectoryInfo(".")
    printfn "%s" curDirInfo.FullName


    // Iterate over each file record in Register once a minute.
    // For any without a thread running, start thread on polling cycle
    while true do
        let input: string = System.Console.ReadLine ()
        let lst: string list = Register.get ()

        let res: string list = lst |> List.filter (fun (x: string) -> x.EndsWith input)
        printfn "Result: %A" res

    0 // return an integer exit code