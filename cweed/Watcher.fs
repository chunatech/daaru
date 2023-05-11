module Watcher
// Filtered watcher of filesystem, to look for any new .cwt or .fsx files, then take action on them.

open System
open System.IO
open System.Text
open System.Globalization
open System.Security.Cryptography


open Logger
open Configuration
open CWeedTransactions
open TransactionComposer


type WatcherType =
    | Source
    | Staging



let create (filter: string) addCb removeCb updateCb (dir: string) =
    Directory.CreateDirectory dir |> ignore
    let watcher: FileSystemWatcher = new FileSystemWatcher()
    watcher.Filter <- filter
    watcher.Path <- dir
    watcher.Created.Add (fun (n: FileSystemEventArgs) -> FileInfo(n.FullPath).FullName |> addCb)
    watcher.Deleted.Add (fun (n: FileSystemEventArgs) -> FileInfo(n.FullPath).FullName |> removeCb)
    watcher.Renamed.Add (fun (n: RenamedEventArgs) -> FileInfo(n.OldFullPath).FullName |> removeCb; FileInfo(n.FullPath).FullName |> addCb)
    watcher.Changed.Add (fun (n: FileSystemEventArgs) -> FileInfo(n.FullPath).FullName |> updateCb)
    watcher.SynchronizingObject <- null
    watcher.EnableRaisingEvents <- true
    watcher.IncludeSubdirectories <- true

    watcher


let remove (path: string) =
    let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
    let mutable msg: string = ""
    match (Register.get path) with
    | Some (t: Transaction) ->
        Register.remove t.Configuration.scriptPath
        msg <- "staged transaction removed " + t.Configuration.stagedScriptPath
        File.Delete t.Configuration.stagedScriptPath
    | None ->
        msg <- "no transaction to remove at " + path
    WriteLogAndPrintToConsole LogLevel.INFO this msg



let add (path: string) =
    let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
    let mutable msg: string = ""

    match ComposeTransaction path with
    | Some (tc: TransactionConfiguration) ->
        Register.add tc
        msg <- $"added %s{path}"
    | None -> 
        msg <- $"unable to add %s{path}"
        ()

    WriteLogAndPrintToConsole LogLevel.INFO this msg 


let update (path: string) =
    // TODO:  Make this method check hash of staged file against source file
    //        Do nothing if they are the same
    let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
    let mutable msg: string = ""

    match (Register.get path) with
    | Some (t: Transaction) ->
        let sourceBytes: byte array = File.ReadAllBytes(path)
        let sourceHash: string = Convert.ToHexString(SHA1.Create().ComputeHash(sourceBytes))

        let stagedBytes: byte array = File.ReadAllBytes(t.Configuration.stagedScriptPath)
        let stagedHash: string = Convert.ToHexString(SHA1.Create().ComputeHash(stagedBytes))

        if sourceHash <> stagedHash then
            remove path
            add path
            msg <- $"Script updated: %s{path}"
        // else
        //     msg <- $"%s{path} touched, but not changed.  Taking no action."

        WriteLogAndPrintToConsole LogLevel.INFO this msg
    | None ->
        ()


let validateFsx (path: string) =
    // TODO: Build out logic to have staged .fsx files validated
    let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
    let msg: string = $"validating {path}" 
    WriteLogAndPrintToConsole LogLevel.INFO this msg


let createForDirs (watcherType: WatcherType) (dirArr: string array) =
    let mutable watcherArr: FileSystemWatcher array = [||]
    match watcherType with
    | Source ->
        for dir: string in dirArr do
            watcherArr <- Array.append [|create "*.cwt" add remove update dir|] watcherArr
            watcherArr <- Array.append [|create "*.fsx" add remove update dir|] watcherArr
            ()
    | Staging ->
        for dir: string in dirArr do
            watcherArr <- Array.append [|create "*.fsx" validateFsx (fun _ -> ()) validateFsx dir|] watcherArr
            ()

    watcherArr


// TODO: Need to enhance this method to check if secure mode is enabled.  If so
//       it needs to also check the .authorized directory.
let registerExistingScripts (confDirs: string array) =
    let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
    let mutable scriptPaths: string array = [||]
    for dir: string in confDirs do
        
        WriteLogAndPrintToConsole LogLevel.INFO this $"registering directory {dir}"
        
        let dirInfo: DirectoryInfo = DirectoryInfo(dir)
        let cwtScripts: FileInfo array = dirInfo.GetFiles("*.cwt", SearchOption.AllDirectories)
        let fsxScripts: FileInfo array = dirInfo.GetFiles("*.fsx", SearchOption.AllDirectories)
        
        scriptPaths <- Array.append (cwtScripts |> Array.map(fun (fi: FileInfo) -> fi.FullName)) scriptPaths
        scriptPaths <- Array.append (fsxScripts |> Array.map(fun (fi: FileInfo) -> fi.FullName)) scriptPaths
    
    for sp: string in scriptPaths do
        add sp

    scriptPaths