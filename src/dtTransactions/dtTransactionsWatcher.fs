namespace dtTransactions

/// handles a file system watcher designed to watch transaction files for changes
/// and handle adding or removing them to the transaction register
module dtTransactionWatcher = 
    open System
    open System.IO
    open System.Reflection
    open System.Security.Cryptography
    open dtTransactions


    type WatcherType =
        | Source
        | Staging


    ///<summary>create a transaction watcher</summary>
    /// <param name="filter">a string filter that tells the watcher what files to watch</param>
    /// <param name="addCb">a callback for adding a file to the watcher. the fn takes in a `string` filepath and returns `unit`</param>
    /// <param name="removeCb">a callback for removing a file from being watched. the fn takes in a string filepath and returns unit</param>
    /// <param name="updateCb">a callback for updates to a watched file</param>
    /// <param name="dir">the directory that the watcher will look in</param>
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


    /// <summary>
    /// handler to remove a transaction from the transaction register. used as a callback 
    /// for the transaction watcher.
    /// </summary> 
    /// <param name="path">the path to the transaction to remove</param>
    let remove (path: string) =
        let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
        let mutable msg: string = ""
        match (dtTransactionRegister.get path) with
        | Some (t: Transaction) ->
            dtTransactionRegister.remove t.Configuration.scriptPath
            msg <- "staged transaction removed " + t.Configuration.stagedScriptPath
            File.Delete t.Configuration.stagedScriptPath
        | None ->
            msg <- "no transaction to remove at " + path


    /// <summary>
    /// handler for adding a transaction to the transaction register. used as the addCb for 
    /// the transaction watcher.
    /// </summary> 
    /// <param name="path">the path to the transaction to add</param>
    let add (path: string) =
        let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
        let mutable msg: string = ""

        match dtTransactionBuilder.buildTransaction path with
        | Some (tc: TransactionConfiguration) ->
            dtTransactionRegister.add tc
            msg <- $"added %s{path}"
        | None -> 
            msg <- $"unable to add %s{path}"
            ()

    /// <summary>
    /// handler for re-registering a transaction when a change to that transaction has 
    /// been made. used as the updateCB fn for the transaction watcher. 
    /// </summary> 
    /// <param name="path">the path to the transaction to update</param>
    let update (path: string) =
        // TODO:  Make this method check hash of staged file against source file
        //        Do nothing if they are the same
        let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
        let mutable msg: string = ""

        match (dtTransactionRegister.get path) with
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

    //TODO: set up fsx validation 
    let validateFsx (path: string) =
        // TODO: Build out logic to have staged .fsx files validated
        let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
        let msg: string = $"validating {path}" 
        ()
        

    ///<summary>
    /// create watchers for an array of directories. used in the main program to set up all of 
    /// the script and staging directories before the main loop. 
    /// </summary>
    /// <param name="watcherType">the type of watcher being created. options are source and staging at this time</param>
    /// <param name="dirArr">the array of directories to set up watchers for</param>
    /// <param name="whiteLabel">the extension and calling method name for the transactions as defined by the user via configuration</param>
    /// <returns>an array of transaction watchers configured for the watcher type specified</returns>
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


    /// <summary>register the scripts that already exist in the scripts locations specified by the user</summary>
    /// <param name="confDirs">array of directories configured to hold scripts</param>
    /// <param name="whiteLabel">the white label setting provided via configuration</param>
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