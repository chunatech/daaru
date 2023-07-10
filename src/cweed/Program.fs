open cweed.AppConfiguration
open cwTransactions
open cwTransactions.cwTransactions
open cwTransactions.cwTransactionRunner
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
    let existingScripts: string array = cwTransactionWatcher.registerExistingScripts (appConfig.scriptDirs |> List.toArray) appConfig.testWhiteLabel
    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"transactions registered %s{existingScripts.ToString()}"

    // set up transaction watchers
    let srcWatcherList: FileSystemWatcher array = cwTransactionWatcher.createForDirs (cwTransactionWatcher.WatcherType.Source) (appConfig.scriptDirs |> List.toArray) appConfig.testWhiteLabel  
    let stagingWatcherList: FileSystemWatcher array = cwTransactionWatcher.createForDirs (cwTransactionWatcher.WatcherType.Staging) ([| stagingDir |]) ""
    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"watchers created for source and staging directories"

    // init TransactionRunner
    let runner: TransactionRunner = TransactionRunner.init fsiSaLocation appConfig logger cweedLogFile
    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"transaction runner initialized"


    while true do 
        Threading.Thread.Sleep(100)
        runner.runTransactions ()
        logger.ProcessQueue()
    exit 0

(*
    [] add ref to transaction lib and start integrating into main
*)