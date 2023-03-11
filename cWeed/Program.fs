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
let maxThreadCount: int32 = 4
let fsiSaLocation: string = "../../../fsiStandalone/TestMultiple/fsiStandalone/fsiStandalone"
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
    
    Watcher.Init(config)

    // call the loggers initialization method
    InitLogger(loggerSettings)

    WriteLog LogLevel.INFO this "logger Initialized"
    WriteLog LogLevel.INFO this $"base configuration was set to {config}"

    // find all existing scripts in configured directories and register them
    let existingScripts: string array = Watcher.registerExistingScripts config.scriptDirectories
    // TODO: Log out existingScripts here

    // Build .cwt and .fsx watchers for that directory
    // Add watchers to list for tracking and cleanup later
    let watcherList: FileSystemWatcher array = Watcher.createForDirs config.scriptDirectories
    // TODO: Log out watcherList here
    
    // print running directory to console and log
    let curDirInfo: DirectoryInfo = DirectoryInfo(".")
    // TODO: Log out current directory path

    WriteLog LogLevel.INFO this $"cWeed has started and is running from {curDirInfo.FullName}"

    // Initialize thread tracker and populate it
    let fsiFi: FileInfo = FileInfo(fsiSaLocation)
    let runner: TransactionRunner = TransactionRunner.init fsiFi.FullName maxThreadCount 
    
    while true do
        Threading.Thread.Sleep(5000)
        
        runner.runTransactions ()

    0