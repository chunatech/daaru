module Watcher
// Filtered watcher of filesystem, to look for any new .cwt or .fsx files, then take action on them.

open System.IO

open Register

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