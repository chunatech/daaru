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
open ConfigTypes
open Configuration

//printfn "%s" (Path.Combine(System.AppContext.BaseDirectory, "fsi_standalone"))
//printfn "%b" ((Path.Join(System.AppContext.BaseDirectory, "fsi_standalone")) |> File.Exists)
//printfn  "%b" File.Exists()

// Set internal global variables
let fsiSaLocation: string = (Path.Join(System.AppContext.BaseDirectory, "fsi_standalone/fsi_standalone"))
let stagingDir: string = "./staging"
// letBaseConfigLocation: string ""

(*
    Task List:

    General
    [x] Adjust TransactionComposer to write composed cwt transactions to fsx files in staging dir: TransactionComposer.fs
    [/] Build out logging more fully: Everywhere
    [] finalize directory structure for compiled/installed project 
    [] handle cases for missing directories in dirstructure 

    Script Watching 
    [] Update staging scripts on source script update: Watcher.fs, Register.fs

    Templating tasks
    [] Build out transaction config layering properly: TransactionComposer.fs
    [] Build out event sending logic (call program or api): RunQueue.fs, Configuration.fs, TransactionComposer.fs
    [] Build credential query caller: RunQueue.fs, Configuration.fs

    transactions 
    [x] Handle pass and failure states of transactions: RunQueue.fs
    [ ] transaction events publishing

    pkg mgmt, dependency handling 
    [] handle chrome driver updating 
    [] Build out package management (local, nuget, custom):
*)

[<EntryPoint>]
let main (argv: string[]) =
    let this: Reflection.MethodBase = (System.Reflection.MethodBase.GetCurrentMethod())

    // Find and read base/default config file
    let config:  AppConfiguration = ConfigurationFromFileOrDefault (Path.Combine(DefaultConfigurationFileLocation, DefaultConfigurationFileName))
    
    // initialize the transaction composer
    TransactionComposer.Init config stagingDir

    // call the loggers initialization method
    InitLogger(config.logs)

    WriteLog LogLevel.INFO this "logger Initialized"
    WriteLog LogLevel.INFO this $"base configuration was set to {config}"
    WriteLog LogLevel.INFO this  $"transaction composer initialized. staging directory set to {stagingDir}. configuration set to base configuration"

    // clear out existing staged scripts if they exist
    if (Directory.Exists(stagingDir)) then Directory.Delete(DirectoryInfo(stagingDir).FullName, true)

    // find all existing scripts in configured directories and register them
    let existingScripts: string array = Watcher.registerExistingScripts (config.scriptDirs |> List.toArray)

    // set up watchers for configured script directories
    let sourceWatcherList: FileSystemWatcher array = 
        Watcher.createForDirs Watcher.WatcherType.Source (config.scriptDirs |> List.toArray)

    // set up watcher for staged script directory
    let stagingFsxWatcherList: FileSystemWatcher array = 
        Watcher.createForDirs Watcher.WatcherType.Staging [|stagingDir|]

    // format existing scripts list for logging 
    let formattedExistingScripts = String.Join(",", existingScripts)
    
    WriteLog LogLevel.DEBUG this $"the value of existingScripts is [ {formattedExistingScripts} ]"

    // format the watcher list paths for logging 
    let fmtWatcherList watcherList = ( "," , (sourceWatcherList |> Array.map (fun watcher -> watcher.Path)) ) |> String.Join
    let formattedSrcWatcherList = fmtWatcherList sourceWatcherList
    let formattedStagingFsxWatcherList = fmtWatcherList stagingFsxWatcherList

    WriteLog LogLevel.DEBUG this $"the value of sourceWatcherList is [ {formattedSrcWatcherList} ]"
    WriteLog LogLevel.DEBUG this $"the value of stagingFsxWatcherList is [ {formattedStagingFsxWatcherList} ]"

    // print running directory to console and log
    let curDirInfo: DirectoryInfo = DirectoryInfo(".")
    WriteLogAndPrintToConsole LogLevel.INFO this $"cWeed has started and is running from %s{curDirInfo.FullName}"

    // Initialize thread tracker and populate it
    let fsiFi: FileInfo = FileInfo(fsiSaLocation)
    let runner: TransactionRunner = TransactionRunner.init fsiFi.FullName config.maxThreadCount
    WriteLog LogLevel.INFO this $"TransactionRunner is initialized with fsi location at %s{runner.fsiPath} and max thread count set to %d{config.maxThreadCount}"
    
    while true do
        Threading.Thread.Sleep(100)
        runner.runTransactions ()
    0