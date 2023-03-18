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
let maxThreadCount: int32 = 2
let fsiSaLocation: string = "../../../fsiStandalone/TestMultiple/fsiStandalone/fsiStandalone"
let stagingDir: string = "./staging"
// letBaseConfigLocation: string ""


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
    // TODO: Log out existingScripts here

    // Build .cwt and .fsx watchers for that directory
    // Add watchers to list for tracking and cleanup later
    let cwtWatcherList: FileSystemWatcher array = 
        Watcher.createForDirs Watcher.WatcherType.Source config.scriptDirectories

    let stagingFsxWatcherList: FileSystemWatcher array = 
        Watcher.createForDirs Watcher.WatcherType.Staging [|stagingDir|]
    // TODO: Log out watcherList here
    
    // print running directory to console and log
    let curDirInfo: DirectoryInfo = DirectoryInfo(".")
    WriteLogAndPrintToConsole LogLevel.INFO this $"cWeed has started and is running from %s{curDirInfo.FullName}"

    // Initialize thread tracker and populate it
    let fsiFi: FileInfo = FileInfo(fsiSaLocation)
    let runner: TransactionRunner = TransactionRunner.init fsiFi.FullName maxThreadCount
    
    while true do
        Threading.Thread.Sleep(100)
        
        runner.runTransactions ()

    0