open cweed.AppConfiguration
open cweed.Logger
open cweed.CwTransactions
open cweed.CwTransactions.TransactionRunner

open System
open System.IO

let fsiSaLocation: string = (Path.Join(AppContext.BaseDirectory, "fsi_standalone/fsi_standalone"))
let stagingDir: string = TransactionBuilder.stagingDir

let initAppConfigs () = 
    let writeLog = LogWriter.writeLog (Reflection.MethodBase.GetCurrentMethod())
    let appConfig: AppConfiguration = ConfigFileHandler.readConfigFileOrDefault ()

    // initialize the logger
    LogWriter.init(appConfig.logs)
    writeLog LogLevel.INFO $"Logger initialized with settings %A{appConfig.logs}"
    
    // initialize the transaction builder
    TransactionBuilder.init (appConfig)    
    writeLog LogLevel.INFO $"TransactionBuilder initialized"    
    
    writeLog LogLevel.INFO $"app configuration set to %A{appConfig}"
    appConfig

[<EntryPoint>]
let main args = 
    let writeLog = LogWriter.writeLog (Reflection.MethodBase.GetCurrentMethod())
    // initialize configurations
    let mutable appConfig: AppConfiguration = initAppConfigs()

    // register existing scripts 
    let existingScripts: string array = TransactionWatcher.registerExistingScripts (appConfig.scriptDirs |> List.toArray)

    // set up transaction watchers
    let srcWatcherList: FileSystemWatcher array = TransactionWatcher.createForDirs (TransactionWatcher.WatcherType.Source) (appConfig.scriptDirs |> List.toArray)
    let stagingWatcherList: FileSystemWatcher array = TransactionWatcher.createForDirs (TransactionWatcher.WatcherType.Staging) ([| stagingDir |])

    let runner: TransactionRunner = TransactionRunner.init fsiSaLocation appConfig.maxThreadCount

    while true do 
        Threading.Thread.Sleep(100)
        runner.runTransactions ()

    exit 0

(*
    [] add ref to transaction lib and start integrating into main
*)