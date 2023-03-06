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
open Utils
open Log
open Configuration
open CWeedTransactions

// Set internal global variables
let maxThreadCount: int32 = 1
let fsiSaLocation: string = "../../../fsiStandalone/TestMultiple/fsiStandalone/fsiStandalone"
// letBaseConfigLocation: string ""

let registerExistingScripts (confDirs: string array) =
    let mutable scriptPaths: string array = [||]
    for dir: string in confDirs do
        printfn "%s" dir
        let dirInfo: DirectoryInfo = DirectoryInfo(dir)
        let cwtScripts: FileInfo array = dirInfo.GetFiles("*.cwt", SearchOption.AllDirectories)
        let fsxScripts: FileInfo array = dirInfo.GetFiles("*.fsx", SearchOption.AllDirectories)
        scriptPaths <- Array.append (cwtScripts |> Array.map(fun (fi: FileInfo) -> fi.FullName)) scriptPaths
        scriptPaths <- Array.append (fsxScripts |> Array.map(fun (fi: FileInfo) -> fi.FullName)) scriptPaths
    
    for sp: string in scriptPaths do
        Watcher.add sp

    scriptPaths

[<EntryPoint>]
let main (argv: string[]) =
    // Find base/default config file
    // Read config from config file into record
    let config: BaseConfiguration = BaseConfiguration.readFromFileOrDefault BaseConfiguration.defaultBaseConfigurationFilePath
    printfn "\nConfig:  %A" config 

    // find all existing scripts in configured directories and register them
    let existingScripts = registerExistingScripts config.scriptDirectories
    // TODO: Log out existingScripts here

    // Build .cwt and .fsx watchers for that directory
    // Add watchers to list for tracking and cleanup later
    let watcherList: FileSystemWatcher array = Watcher.createForDirs config.scriptDirectories
    // TODO: Log out watcherList here
    
    // print running directory to console and log
    let curDirInfo: DirectoryInfo = DirectoryInfo(".")
    // TODO: Log out current directory path
    printfn "cWeed has started and is running from:\n%s" curDirInfo.FullName


    // Iterate over each file record in Register once a minute.
    // For any without a thread running, start thread on polling cycle
    let lastLoopTime: int32 = 0
    while true do
        let input: string = System.Console.ReadLine ()
        let lst: Transaction list = Register.get ()

        let res: Transaction = (lst |> List.filter (fun (x: Transaction) -> x.Configuration.scriptPath.EndsWith input)) |> List.head
        let fsiFi: FileInfo = FileInfo(fsiSaLocation)
        let psi: ProcessStartInfo = new ProcessStartInfo(fsiFi.FullName, $"%s{res.Configuration.scriptPath}")
        psi.UseShellExecute <- false
        let testTask (psi: ProcessStartInfo) =
            task {
                let p: Process = new Process()
                p.StartInfo <- psi
                p.EnableRaisingEvents <- true
                // p.Exited += EventHandler
                p.Start() |> ignore
            }
        let task1: Task<unit> = testTask psi

        task1.Wait()
        printfn "Result: %A" res

    0 // return an integer exit code