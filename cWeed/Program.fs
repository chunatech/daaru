module cWeed
// cWeed is a good source of Selenium, powered by Canopy
// https://lefthandedgoat.github.io/canopy/index.html
// https://www.selenium.dev/about/

// Project was initially adapted from example projects in these articles:
// http://kcieslak.io/Dynamically-extending-F-applications
// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html

open System.IO
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


[<EntryPoint>]
let main (argv: string[]) =
    let this = (System.Reflection.MethodBase.GetCurrentMethod())
    // Find base/default config file
    // Read config from config file into record 
    let config: BaseConfiguration = 
        BaseConfiguration.readFromFileOrDefault BaseConfiguration.defaultBaseConfigurationFilePath


    // create settings to pass into the Logger
    let loggerSettings = 
        LoggerSettings.Create (Path.Join(config.logDirPath, config.logDirName)) config.rollingSize config.logFormat
    

    // call the loggers initialization method
    InitLogger(loggerSettings)

    WriteLog INFO this "logger Initialized"
    WriteLog INFO this $"base configuration was set to {config}"


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

    WriteLog INFO this $"cWeed has started and is running from {curDirInfo.FullName}"

    // Iterate over each file record in Register once a minute.
    // For any without a thread running, start thread on polling cycle
    let lastLoopTime: int32 = 0
    while true do
        let input: string = System.Console.ReadLine ()
        let lst: Transaction list = Register.get ()

        let res: Transaction = (lst |> List.filter (fun (x: Transaction) -> 
            x.Configuration.scriptPath.EndsWith input)) |> List.head
        let fsiFi: FileInfo = FileInfo(fsiSaLocation)

        // once a minute:
        //  check the run queue
        //  if the run queue still has unprocessed items, log a warning or error
        //  check the transaction register for transactions that need to start
        //    (meaning that  last run time + poll time is the greater or equal to the current time)
        //  enqueue any transactions that need to start to the run queue
        //  wait 60 seconds

        let testTask (transaction: Transaction) =
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

        let task1: Task<unit> = testTask res
        task1.Wait()
        printfn "Result: %A" res

    0 // return an integer exit code