open cweed.AppConfiguration
open cweed.CwTransactions
open cweed.CwTransactions.TransactionRunner
open cwLogger.Logger

open System
open System.IO
open System.Reflection

let fsiSaLocation: string = (Path.Join(AppContext.BaseDirectory, "fsi_standalone/fsi_standalone"))
let stagingDir: string = TransactionBuilder.stagingDir

let InitializeProgram () = 
    let mutable appConfig: AppConfiguration = ConfigFileHandler.readConfigFileOrDefault ()
    let logger: Logger = new Logger(appConfig.logs.FileSizeLimit, enum<Severity>(appConfig.logs.Severity))
    TransactionBuilder.init (appConfig) 
    appConfig, logger

[<EntryPoint>]
let main args = 
    // initialize configurations
    let (appConfig: AppConfiguration), (logger: Logger) = InitializeProgram()
    let logDirectory: string = appConfig.logs.LogDirectory
    let cweedLogFile: string = Path.Join(logDirectory, "cweed.log")

    if not <| Directory.Exists(logDirectory) then Directory.CreateDirectory(logDirectory) |> ignore
    
    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"program initialzied with settings: {appConfig}"

    // register existing scripts 
    let existingScripts: string array = TransactionWatcher.registerExistingScripts (appConfig.scriptDirs |> List.toArray)

    // set up transaction watchers
    let srcWatcherList: FileSystemWatcher array = TransactionWatcher.createForDirs (TransactionWatcher.WatcherType.Source) (appConfig.scriptDirs |> List.toArray)
    let stagingWatcherList: FileSystemWatcher array = TransactionWatcher.createForDirs (TransactionWatcher.WatcherType.Staging) ([| stagingDir |])

    let runner: TransactionRunner = TransactionRunner.init fsiSaLocation appConfig.maxThreadCount

    while true do 
        Threading.Thread.Sleep(100)
        runner.runTransactions ()
        logger.ProcessQueue()
    exit 0

(*
    [] add ref to transaction lib and start integrating into main
*)