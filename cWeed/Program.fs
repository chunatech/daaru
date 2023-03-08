module cWeed
// cWeed is a good source of Selenium, powered by Canopy
// https://lefthandedgoat.github.io/canopy/index.html
// https://www.selenium.dev/about/

// Project was initially adapted from example projects in these articles:
// http://kcieslak.io/Dynamically-extending-F-applications
// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html

open System
open System.IO
open System.Timers
open System.Diagnostics
open System.Threading.Tasks

// Open modules internal to the project
open Utils
open Logger
open Configuration
open CWeedTransactions

// Set internal global variables
let maxThreadCount: int32 = 1
let fsiSaLocation: string = "../../../fsiStandalone/TestMultiple/fsiStandalone/fsiStandalone"
// letBaseConfigLocation: string ""

let checkTransactionQueue source (e: ElapsedEventArgs) =
    let transactions: Transaction list = Register.get ()
    let needToRun: Transaction list = transactions |> List.filter (fun (x: Transaction) -> x.LastRunTime.AddMinutes(x.Configuration.pollingInterval) <= DateTime.Now)
    
    // TODO: Add transactions from needToRun to the RunQueue here
    

    // TODO: Move below logic to RunQueue processing function after building RunQueue
    let fsiFi: FileInfo = FileInfo(fsiSaLocation)

    let tTask (transaction: Transaction) =
        let scriptPath: string = transaction.Configuration.scriptPath
        let psi: ProcessStartInfo = new ProcessStartInfo(fsiFi.FullName, $"%s{scriptPath}")
        psi.UseShellExecute <- false

        task {
            let p: Process = new Process()
            p.StartInfo <- psi
            p.EnableRaisingEvents <- true
            // p.Exited += EventHandler
            p.Start() |> ignore
        }

    for t: Transaction in needToRun do
        tTask t |> ignore
    ()


[<EntryPoint>]
let main (argv: string[]) =
    let this = (System.Reflection.MethodBase.GetCurrentMethod())
    // Find base/default config file
    // Read config from config file into record 
    let config: BaseConfiguration = 
        BaseConfiguration.readFromFileOrDefault BaseConfiguration.defaultBaseConfigurationFilePath


    // create settings to pass into the Logger
    let loggerSettings = 
        LoggerSettings.Create (Path.Join(config.logDirPath, config.logDirName)) config.rollingSize config.logFormat config.loggingLevel
    
    
    Watcher.Init(config)


    // call the loggers initialization method
    InitLogger(loggerSettings)

    WriteLog LogLevel.INFO this "logger Initialized"
    WriteLog LogLevel.INFO this $"base configuration was set to {config}"


    // find all existing scripts in configured directories and register them
    let existingScripts = Watcher.registerExistingScripts config.scriptDirectories
    // TODO: Log out existingScripts here

    // Build .cwt and .fsx watchers for that directory
    // Add watchers to list for tracking and cleanup later
    let watcherList: FileSystemWatcher array = Watcher.createForDirs config.scriptDirectories
    // TODO: Log out watcherList here
    
    // print running directory to console and log
    let curDirInfo: DirectoryInfo = DirectoryInfo(".")
    // TODO: Log out current directory path

    WriteLog LogLevel.INFO this $"cWeed has started and is running from {curDirInfo.FullName}"

    // Start Timer instance, on elapsed iterate over the process
    // queue and start any transactions that need to run
    let processTimer: Timer = new Timer()
    processTimer.Interval <- 60_000  // 60 seconds
    processTimer.AutoReset <- true
    processTimer.Elapsed.AddHandler checkTransactionQueue
    processTimer.Start()

    // Iterate over each file record in Register once a minute.
    // For any without a thread running, start thread on polling cycle
    while true do
        ()
        

        

        // Start a timer that once a minute:
        //  checks the run queue
        //  if the run queue still has unprocessed items, log a warning or error
        //  check the transaction register for transactions that need to start
        //    (meaning that  last run time + poll time is the greater or equal to the current time)
        //  enqueue any transactions that need to start to the run queue
        //  wait 60 seconds

    0 // return an integer exit code