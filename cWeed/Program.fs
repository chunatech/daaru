module cWeed
// cWeed is a good source of Selenium, powered by Canopy
// https://lefthandedgoat.github.io/canopy/index.html
// https://www.selenium.dev/about/

// Project was initially adapted from example projects in these articles:
// http://kcieslak.io/Dynamically-extending-F-applications
// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html

open System.IO
open System.Diagnostics
open System.Threading.Tasks

// Open modules internal to the project
//open Register
//open Watcher
let fsiSaLocation: string = "../../../fsiStandalone/TestMultiple/fsiStandalone/fsiStandalone"
// letBaseConfigLocation: string ""

[<EntryPoint>]
let main (argv: string[]) =
    // Find base/default config file
    // Read config from config file into record


    // Recursively search each script directory defined in config
    // For each directory and subdirectory:


    // -- Build .cwt and .fsx watchers for that directory
    // -- Add watchers to list for tracking and cleanup later
    let watcherList: FileSystemWatcher list = Watcher.createForDirs ["scripts"] []


    // -- Find any local config file for that directory


    // -- Find any .cwt or .fsx files in that directory
    // -- For each file found:


    // -- -- Read head of file to check for config override - push to 0.2


    // -- -- Build running config record for file, layer configs like so:
    // -- -- base/default config <- local dir config <- override from file


    // -- -- Build record for file, containing running config, path,
    // -- -- and timestamps for last run, last failure, and last success


    // -- -- Add record for file to Register


    let curDirInfo: DirectoryInfo = DirectoryInfo(".")
    printfn "cWeed has started and is running from:\n%s" curDirInfo.FullName


    // Iterate over each file record in Register once a minute.
    // For any without a thread running, start thread on polling cycle
    while true do
        let input: string = System.Console.ReadLine ()
        let lst: string list = Register.get ()

        let res: string = (lst |> List.filter (fun (x: string) -> x.EndsWith input)) |> List.head
        let fi: FileInfo = FileInfo(fsiSaLocation)
        let psi: ProcessStartInfo = new ProcessStartInfo(fi.FullName, $"%s{res}")
        psi.UseShellExecute <- false
        let testTask (psi: ProcessStartInfo) =
            task {
                Process.Start(psi) |> ignore
            }
        let task1: Task<unit> = testTask psi
        let task2: Task<unit> = testTask psi
        let task3: Task<unit> = testTask psi
        let task4: Task<unit> = testTask psi

        Task.WaitAll(task1, task2, task3, task4)
        printfn "Result: %A" res

    0 // return an integer exit code