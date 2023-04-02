module cWeed
// cWeed is a good source of Selenium, powered by Canopy
// https://lefthandedgoat.github.io/canopy/index.html
// https://www.selenium.dev/about/

open System
open System.IO

// Open modules internal to the project
open Utils
open Logger
open RunQueue
open Configuration

// Set internal global variables
let fsiSaLocation: string = "../../../fsiStandalone/TestMultiple/fsiStandalone/fsiStandalone"
let stagingDir: string = "./staging"
// letBaseConfigLocation: string ""

(*
    Task List:
    [x] Adjust TransactionComposer to write composed cwt transactions to fsx files in staging dir: TransactionComposer.fs
    [/] Handle pass and failure states of transactions: RunQueue.fs
    [] Update staging scripts on source script update: Watcher.fs, Register.fs
    [] Build out transaction config layering properly: TransactionComposer.fs
    [] Build out secure mode logic: RunQueue.fs, Watcher.fs
    [] Build out event sending logic (call program or api): RunQueue.fs, Configuration.fs, TransactionComposer.fs
    [] Build credential query caller: RunQueue.fs, Configuration.fs
    [] Build out logging more fully: Everywhere
    [] Build out package management (local, nuget, custom): ?
*)

[<EntryPoint>]
let main (argv: string[]) =
    let this: Reflection.MethodBase = (System.Reflection.MethodBase.GetCurrentMethod())

    // Find and read base/default config file
    let config: BaseConfiguration = 
        BaseConfiguration.readFromFileOrDefault BaseConfiguration.defaultBaseConfigurationFilePath

    // create settings to pass into the Logger
    let loggerSettings: LoggerSettings = 
        LoggerSettings.Create (Path.Join(config.logDirPath, config.logDirName)) config.rollingSize config.logFormat config.loggingLevel
    
    TransactionComposer.Init config stagingDir

    // call the loggers initialization method
    InitLogger(loggerSettings)

    WriteLog LogLevel.INFO this "logger Initialized"
    // WriteLog LogLevel.INFO this $"base configuration was set to {config}"

    // clear out existing staged scripts if they exist
    Directory.Delete(DirectoryInfo(stagingDir).FullName, true)

    // find all existing scripts in configured directories and register them
    let existingScripts: string array = Watcher.registerExistingScripts config.scriptDirectories
    // set up watchers for configured script directories
    let sourceWatcherList: FileSystemWatcher array = 
        Watcher.createForDirs Watcher.WatcherType.Source config.scriptDirectories
    // set up watcher for staged script directory
    let stagingFsxWatcherList: FileSystemWatcher array = 
        Watcher.createForDirs Watcher.WatcherType.Staging [|stagingDir|]
    // TODO: Log out details from above three variables here
    
    // print running directory to console and log
    let curDirInfo: DirectoryInfo = DirectoryInfo(".")
    WriteLogAndPrintToConsole LogLevel.INFO this $"cWeed has started and is running from %s{curDirInfo.FullName}"

    // Initialize thread tracker and populate it
    let fsiFi: FileInfo = FileInfo(fsiSaLocation)
    let runner: TransactionRunner = TransactionRunner.init fsiFi.FullName config.maxThreadCount
    
    while true do
        Threading.Thread.Sleep(100)
        runner.runTransactions ()

    0