open cweed.AppConfiguration
open dtTransactions
// open dtTransactions.dtTransactions
open dtTransactions.dtTransactionRunner
open dtLogger.Logger

open System
open System.IO
open System.Reflection

/// this is where cweed looks for the standalone fsi portion of the application 
let fsiSaLocation: string = (Path.Join(AppContext.BaseDirectory, "fsi_standalone/fsi_standalone"))

/// the staging directory location. see cweed.Transactions/cwTransactionsBuilder.fs
let stagingDir: string = dtTransactionBuilder.stagingDir

/// initialize the application configuration, logger and transaction builder.  
let InitializeProgram () = 
    let mutable appConfig: AppConfiguration = ConfigFileHandler.readConfigFileOrDefault ()
    let logger: Logger = new Logger(appConfig.logs.FileSizeLimit, enum<Severity>(appConfig.logs.Severity))
    dtTransactionBuilder.init (appConfig) 
    appConfig, logger


[<EntryPoint>]
let main args = 
    // initialize configurations
    let (appConfig: AppConfiguration), (logger: Logger) = InitializeProgram()

    // where the logs are stored, as given by the application configuration
    let logDirectory: string = appConfig.logs.LogDirectory

    // the name of the application log file
    let cweedLogFile: string = Path.Join(logDirectory, "cweed.log")

    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"application configuration received"
    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"transaction builder initialzied"
    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"logger initialized"

    // if the log directory doesnt exist, create it
    if not <| Directory.Exists(logDirectory) then Directory.CreateDirectory(logDirectory) |> ignore
    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"program initialzied with settings: {appConfig}"

    // register existing scripts 
    let existingScripts: string array = dtTransactionWatcher.registerExistingScripts (appConfig.scriptDirs |> List.toArray) appConfig.testWhiteLabel
    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"transactions registered %s{existingScripts.ToString()}"

    // set up transaction watchers
    let srcWatcherList: FileSystemWatcher array = dtTransactionWatcher.createForDirs (dtTransactionWatcher.WatcherType.Source) (appConfig.scriptDirs |> List.toArray) appConfig.testWhiteLabel  
    let stagingWatcherList: FileSystemWatcher array = dtTransactionWatcher.createForDirs (dtTransactionWatcher.WatcherType.Staging) ([| stagingDir |]) ""
    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"watchers created for source and staging directories"

    // init TransactionRunner
    let runner: TransactionRunner = TransactionRunner.init fsiSaLocation appConfig logger cweedLogFile
    logger.Log cweedLogFile (MethodBase.GetCurrentMethod()) Severity.Info $"transaction runner initialized"

    // main loop of the program.. sleeps for 100ms, runs transactions, and then processes logs 
    while true do 
        Threading.Thread.Sleep(100)
        runner.runTransactions ()
        logger.ProcessQueue()
    exit 0