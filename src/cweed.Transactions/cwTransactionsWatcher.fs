namespace cwTransactions

/// handles a file system watcher designed to watch transaction files for changes
/// and handle adding or removing them to the transaction register
module cwTransactionWatcher = 
    open System
    open System.IO
    open System.Reflection
    open System.Security.Cryptography
    open cwTransactions


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
        match (cwTransactionRegister.get path) with
        | Some (t: Transaction) ->
            cwTransactionRegister.remove t.Configuration.scriptPath
            msg <- "staged transaction removed " + t.Configuration.stagedScriptPath
            File.Delete t.Configuration.stagedScriptPath
        | None ->
            msg <- "no transaction to remove at " + path



    let add (path: string) =
        let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
        let mutable msg: string = ""

        match TransactionBuilder.buildTransaction path with
        | Some (tc: TransactionConfiguration) ->
            cwTransactionRegister.add tc
            msg <- $"added %s{path}"
        | None -> 
            msg <- $"unable to add %s{path}"
            ()


    let update (path: string) =
        // TODO:  Make this method check hash of staged file against source file
        //        Do nothing if they are the same
        let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
        let mutable msg: string = ""

        match (cwTransactionRegister.get path) with
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

        | None ->
            ()


    let validateFsx (path: string) =
        // TODO: Build out logic to have staged .fsx files validated
        let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
        let msg: string = $"validating {path}" 
        ()
        


    let createForDirs (watcherType: WatcherType) (dirArr: string array) (whiteLabel: string) =
        let mutable watcherArr: FileSystemWatcher array = [||]
        match watcherType with
        | Source ->
            for dir: string in dirArr do
                watcherArr <- Array.append [|create $"*.%s{whiteLabel}" add remove update dir|] watcherArr
                watcherArr <- Array.append [|create "*.fsx" add remove update dir|] watcherArr
                ()
        | Staging ->
            for dir: string in dirArr do
                watcherArr <- Array.append [|create "*.fsx" validateFsx (fun _ -> ()) validateFsx dir|] watcherArr
                ()

        watcherArr


    // TODO: Need to enhance this method to check if secure mode is enabled.  If so
    //       it needs to also check the .authorized directory.
    let registerExistingScripts (confDirs: string array) (whiteLabel: string) =
        let this: Reflection.MethodBase = MethodBase.GetCurrentMethod()
        let mutable scriptPaths: string array = [||]
        for dir: string in confDirs do
            
            let dirInfo: DirectoryInfo = DirectoryInfo(dir)
            let cwtScripts: FileInfo array = dirInfo.GetFiles($"*.%s{whiteLabel}", SearchOption.AllDirectories)
            let fsxScripts: FileInfo array = dirInfo.GetFiles("*.fsx", SearchOption.AllDirectories)
            
            scriptPaths <- Array.append (cwtScripts |> Array.map(fun (fi: FileInfo) -> fi.FullName)) scriptPaths
            scriptPaths <- Array.append (fsxScripts |> Array.map(fun (fi: FileInfo) -> fi.FullName)) scriptPaths
        
        for sp: string in scriptPaths do
            add sp

        scriptPaths