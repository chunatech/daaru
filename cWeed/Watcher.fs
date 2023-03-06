module Watcher
// Filtered watcher of filesystem, to look for any new .cwt or .fsx files, then take action on them.

open System.IO
open Configuration

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
    watcher.IncludeSubdirectories <- true

    watcher


let remove (path: string) =
    printfn "%s removed" path
    // let fn = Path.GetFileNameWithoutExtension path
    Register.remove path


let add (path: string) =
    printfn "%s added" path
    // let ext = FileInfo(path).Extension
    let dirConfig: BaseConfiguration = BaseConfiguration.defaultConfig
    let config: TransactionConfiguration = {
        scriptPath = path
        pollingInterval = dirConfig.pollingInterval
        browser = dirConfig.browser
        browserOptions = dirConfig.browserOptions
        browserDriverDir = dirConfig.browserDriverDir
        nugetPackages = dirConfig.nugetPackages
    }

    Register.add (config)


let update (path: string) =
    printfn "%s added or updated" path


let createForDirs (dirArr: string array) =
    let mutable watcherArr: FileSystemWatcher array = [||]
    for dir: string in dirArr do
        watcherArr <- Array.append [|create "*.cwt" add remove update dir|] watcherArr
        watcherArr <- Array.append [|create "*.fsx" add remove update dir|] watcherArr
        ()
    watcherArr


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
        add sp

    scriptPaths