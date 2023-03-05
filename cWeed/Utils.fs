module Utils

open System.IO


let rec recurseListOfDirectories (topDirs: string list) (outDirList: DirectoryInfo list) =
    match topDirs with
        | [] -> outDirList
        | (dir: string) :: (dirs: string list) -> 
            printfn "%s" dir
            let dirInfo: DirectoryInfo = DirectoryInfo(dir)
            let outDirList: DirectoryInfo list = [dirInfo] @ outDirList
            let subDirs: DirectoryInfo list = dirInfo.GetDirectories("*.*", SearchOption.AllDirectories) |> Array.toList
            let outDirList: DirectoryInfo list = subDirs @ outDirList
            recurseListOfDirectories dirs outDirList