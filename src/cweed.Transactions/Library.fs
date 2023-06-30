namespace cweed

module CwTransactions =
    open System
    open System.IO
    open System.Reflection

    open AppConfiguration
    open Logger

    /// this is the configuration record for a specific transaction
    /// intended for use by the transaction runner. this configuration
    /// is composed together via defaults, directory specific, and
    /// configurations provided at the top of the transaction in a config
    /// tag. in order of precedence, file, directory, default is considered
    /// with file being the foremost important.
    type TransactionConfiguration = { 
        scriptPath: string
        stagedScriptPath: string
        pollingInterval: int
        browser: string
        browserOptions: string array
        browserDriverDir: string
        nugetPackages: string array 
    }


    /// this is directory specific configuration intended
    /// for the transaction runner to use. this configuration
    /// layers between what is given as default and any
    /// configuration that is given at the top of a file
    type DirectoryConfiguration = { 
        pollingInterval: int
        browser: string
        browserOptions: string array
        browserDriverDir: string
        nugetPackages: string array 
    }

    

    type UnhandledOutput = 
    | STDERR
    | STDOUT

    type Transaction = {
        Configuration: TransactionConfiguration
        mutable LastRunTime: DateTime
        mutable LastSuccess: DateTime
        mutable LastFailure: DateTime
        mutable ConsecutiveRunCount: Int32
        mutable LastRunDetails: RunDetails
        
    }
    with
        static member Create (tc: TransactionConfiguration) =
            {
                Transaction.Configuration = tc
                LastRunTime = DateTime()
                LastSuccess = DateTime()
                LastFailure = DateTime()
                ConsecutiveRunCount = 0
                LastRunDetails = RunDetails.Create()
            }


        member this.WriteOutUnhandled (stream: UnhandledOutput) (content: string) =
            let stagedScriptPath: string = this.Configuration.stagedScriptPath
            let outFilePath: string = stagedScriptPath.Replace(".fsx", ".unhandled")
            let prefix: string =
                match stream with
                | STDERR -> "[STDERR]"
                | STDOUT -> "[STDOUT]"

            File.AppendAllLinesAsync(outFilePath, [$"%s{prefix} %s{content}"]) |> ignore


    and RunDetails = {
        mutable BrowserStarting: DateTime
        mutable BrowserDriverPort: string
        mutable BrowserStarted: DateTime
        mutable Passed: int
        mutable Skipped: int
        mutable Failed: int
        mutable UnhandledOutput: int
        mutable UnhandledErrors: int
        mutable DriverVersionMismatch: (bool * string)
    }
    with
        static member Create () =
            {
                RunDetails.BrowserStarting = DateTime()
                BrowserDriverPort = ""
                BrowserStarted = DateTime()
                Passed = 0
                Skipped = 0
                Failed = 0
                UnhandledOutput = 0
                UnhandledErrors = 0
                DriverVersionMismatch = (false, "")
            }


    /// handles the templating, building, and creation of fsx files from cwt and 
    /// eventually custom extensions
    module TransactionBuilder = 

        // instance of the app configuration being used. initialized with the 
        // default. init fn provides the user config at runtime
        let mutable private _config: AppConfiguration = AppConfiguration.Default

        /// internal staging directory. this is where cweed stages the built  
        /// scripts that are actually run by the transaction runner
        let mutable stagingDir: string = Path.Join(System.AppContext.BaseDirectory, "staging")
        
        /// this directory is where the templates are located. a default template is provided at this time. 
        let templatesDir: string = Path.Join(AppContext.BaseDirectory, "templates")

        // these are required dlls for use in the transaction file
        let private _defaultImports = 
            [|
                "libs/canopy.dll";
                "libs/Newtonsoft.Json.dll";
                "libs/WebDriver.dll";
            |] 
            |> Array.map (fun p -> $"#r @\"%s{(Path.Join(System.AppContext.BaseDirectory,p))}\"")
            


        // array containing the default open statements in the "header" section
        let private _defaultOpenStmts: string array = [|
            "open canopy.runner.classic"
            "open canopy.configuration"
            "open canopy.classic"
        |]

        // this composes together all the lines that make up the browser configuration portion of the testfile 
        // and returns them as an array of strings to be further composed into a testfile
        let private _buildHeader (config: TransactionConfiguration) : string array = 

            // chrome configuration
            let chromeDirConfig: string array = [| $"chromeDir <- \"{DirectoryInfo(config.browserDriverDir).FullName}\"" |]
            let browserOptsObj: string array = [| "let browserOptions: OpenQA.Selenium.Chrome.ChromeOptions = OpenQA.Selenium.Chrome.ChromeOptions()" |]
            let opts: string array = 
                config.browserOptions
                |> Array.map (fun (opt: string) -> $"browserOptions.AddArgument(\"--{opt}\")") 

            // startmode
            let startMode: string = "let browserWO: canopy.types.BrowserStartMode =  canopy.types.BrowserStartMode.ChromeWithOptions(browserOptions)"
            let startCmdString: string = "start browserWO"
            
            let startModeSettings: string array = [|
                startMode
                startCmdString
            |]

            Array.distinct (Array.concat [
                _defaultImports;
                _defaultOpenStmts;
                chromeDirConfig;
                browserOptsObj;
                opts;
                startModeSettings;
            ])



        let private _buildTransactionFileContents (tConfig: TransactionConfiguration) : string array = 
            let header = _buildHeader tConfig
            let testName: string array = [| $"\"{Path.GetFileNameWithoutExtension(tConfig.scriptPath)}\" &&& fun _ ->" |]
            let testContent: string array = File.ReadAllLines(tConfig.scriptPath)
            let footer = [|
                "run()";
                "quit(browserWO)";
            |]

            // put all the pieces together and return
            // one string array to be written to file
            Array.distinct (Array.concat [
                header;
                testName;
                testContent;
                footer;
            ])

        let rec private _buildFromTemplate (tConfig: TransactionConfiguration) (template: string list) (result: string list) = 
                        match template with 
                        | [] -> result
                        | line::lines -> 
                            match line with 
                            // add the #r statements here
                            | "__DEPENDENCIES__" ->
                                _buildFromTemplate tConfig lines result @ (_defaultImports |> Array.toList)

                            | (line: string) when line.Contains("__CREDENTIAL_REQUEST_SCRIPT_RUNNER__") -> 
                                match _config.credentialsRequestScript with 
                                | Some (cfg: CredentialsRequestScriptConfiguration) -> 
                                    let line' = line.Replace("__CREDENTIAL_REQUEST_SCRIPT_RUNNER__", cfg.credRunnerPath)
                                    _buildFromTemplate tConfig lines result @ ([line'])
                                | None -> 
                                    let line' = line.Replace("__CREDENTIAL_REQUEST_SCRIPT_RUNNER__", "")
                                    _buildFromTemplate tConfig lines result @ ([line'])

                            | (line: string) when line.Contains("__CREDENTIAL_REQUEST_SCRIPT__") ->
                                match _config.credentialsRequestScript with 
                                | Some (cfg: CredentialsRequestScriptConfiguration) -> 
                                    let line' = line.Replace("__CREDENTIAL_REQUEST_SCRIPT__", cfg.credScriptPath)
                                    _buildFromTemplate tConfig lines result @ ([line'])
                                | None -> 
                                    let line' = line.Replace("__CREDENTIAL_REQUEST_SCRIPT__", "")
                                    _buildFromTemplate tConfig lines result @ [line']                                
                            
                            | (line: string) when line.Contains("__SCREENSHOT_DIR__") -> 
                                let line': string = line.Replace("__SCREENSHOT_DIR__", _config.screenshotDirPath)
                                _buildFromTemplate tConfig lines result @ ([line'])


                            | (line: string) when line.Contains("__CHROME_DRIVER_DIR__") -> 
                                let line': string = line.Replace("__CHROME_DRIVER_DIR__", tConfig.browserDriverDir)
                                _buildFromTemplate tConfig lines result @ ([line'])


                            | (line: string) when line.Contains("__BROWSER_OPTIONS__") -> 
                                let mutable opts: string list = []
                                for opt: string in (tConfig.browserOptions) do 
                                    opts <- $"browserOptions.AddArgument(\"--%s{opt}\")"::opts

                                _buildFromTemplate tConfig lines result @ opts
                            
                            // TODO: add functionality for this configuration. talk to chase
                            | (line: string) when line.Contains("__TRANSACTION_CONFIG__") -> 
                                _buildFromTemplate tConfig lines result


                            | (line: string) when line.Contains("__TRANSACTION_TESTS__") -> 
                                let cwt: string list = File.ReadAllLines(tConfig.scriptPath) |> Array.toList
                                _buildFromTemplate tConfig lines result @ cwt

                            // TODO: 
                            // ADD TRANSACTION CONFIG 
                            // ADD TRANSACTION TESTS 
                            // ADD LOGGER LINES PARSING 
                            // ADD RESULTS PARSING 

                            // an oridinary line 
                            | _ -> _buildFromTemplate tConfig lines result @ [line]

        let private _processCwtFromTemplate (tConfig) : option<TransactionConfiguration> = 
            let sourcePath: string = FileInfo(tConfig.scriptPath).FullName
            let sourceDir: string = Path.GetDirectoryName(sourcePath)
            
            // create the path for the staging file
            let stagingFilePath = sourcePath.Replace(sourceDir, stagingDir).Replace(".cwt", ".fsx")
            let targetStagingDir = Path.GetDirectoryName(stagingFilePath)
            // first read in template. currenltly only supporting default template. if this can't be read in then exit the program we 
            // wont be able to construct any scripts.
            let templateContents = 
                try File.ReadAllLines(Path.Join(templatesDir, "default.template")) 
                with exn -> 
                    LogWriter.writeLogAndPrintToConsole (MethodBase.GetCurrentMethod()) LogLevel.ERROR $"%s{exn.Message}"
                    exit 0
            let templateContents' = templateContents
            templateContents' |> Array.Reverse


            let result = 
                _buildFromTemplate tConfig (templateContents' |> Array.toList) []

            // create staging directory mirror of target script
            Directory.CreateDirectory targetStagingDir 
            |> ignore

            // create the fsx
            File.WriteAllLines(stagingFilePath, result)

            // TODO add some config with staged filepath
            ({ tConfig with stagedScriptPath = stagingFilePath })
            |> Some



        // set up and copy fsx files into staging. return updated transaction configuration
        let private _processFsx (tConfig: TransactionConfiguration) : option<TransactionConfiguration> = 
            let sourcePath: string = FileInfo(tConfig.scriptPath).FullName
            printfn $"sourcePath: %s{sourcePath}"
            let sourceDir: string = Path.GetDirectoryName(sourcePath)
            // create the path for the staging file
            let stagingFilePath: string = sourcePath.Replace(sourceDir, stagingDir)
            printfn $"stagingFilePath: %s{stagingFilePath}"
            let targetStagingDir: string = Path.GetDirectoryName(stagingFilePath)

            // create staging directory mirror of target script
            Directory.CreateDirectory targetStagingDir 
            |> ignore

            // create the fsx
            File.Copy(sourcePath, stagingFilePath, true)

            // return the config
            { tConfig with stagedScriptPath = stagingFilePath }
            |> Some



        /// initialize the transaction processer with a copy of the app configuration
        let init (config: AppConfiguration) = 
            _config <- config


        /// process a transaction based on its extension
        let buildTransaction (path: string): option<TransactionConfiguration> = 
            let browserConfigs: BrowserConfiguration list = _config.browsers

            // build the transactionConfig here 
            let tConfig: TransactionConfiguration = {
                scriptPath = path
                stagedScriptPath = ""
                pollingInterval = _config.pollingInterval
                browser = browserConfigs[0].browser
                browserOptions = browserConfigs[0].browserOpts 
                    |> List.toArray
                browserDriverDir = browserConfigs[0].driverLocation
                nugetPackages = [||]
            }

            // run the processing fn based on ext type
            match Path.GetExtension(tConfig.scriptPath) with 
            | ".fsx" -> _processFsx tConfig 
            | ".cwt" -> _processCwtFromTemplate tConfig
            | _ -> 
                LogWriter.writeLog 
                    (MethodBase.GetCurrentMethod())
                    LogLevel.INFO
                    $"{tConfig.scriptPath} has an unrecognized extension"
                None

    /// holds a list of located transactions and handles the transaction registration 
    /// process 
    module TransactionRegister = 
        type private RegisteredTransaction =
            | Add of transaction: Transaction
            | Remove of string // Transaction.Configuration.scriptPath
            | Get of AsyncReplyChannel<Transaction list>
            | Update of transaction: Transaction


        let private register: MailboxProcessor<RegisteredTransaction> =
            MailboxProcessor.Start (fun (inbox: MailboxProcessor<RegisteredTransaction>) ->
                // Define processing loop
                let rec loop (lst: Transaction list) = async {

                    // Receive message
                    let! (rt: RegisteredTransaction) = inbox.Receive()

                    // Process message and kick off next iteration
                    match rt with
                    | Add (t: Transaction) ->
                        return! loop (t::lst)
                    | Remove (tsp: string) ->
                        return! loop (lst |> List.filter(fun (t: Transaction) -> 
                            t.Configuration.scriptPath <> tsp))
                    | Get (rc: AsyncReplyChannel<Transaction list>) ->
                        rc.Reply lst
                        return! loop lst
                    | Update (ut: Transaction) ->
                        // TODO:  Build out update logic
                        // Update transaction details
                        // below is just placeholder logic
                        return! loop (lst |> List.map (fun (t: Transaction) -> 
                            if t.Configuration.scriptPath = ut.Configuration.scriptPath then ut else t))
                }
                loop [] )


        let add (transactionConfig: TransactionConfiguration) =
            let transaction: Transaction = Transaction.Create(transactionConfig)
            transaction |> Add |> register.Post


        let remove (scriptPath: string) =
            scriptPath |> Remove |> register.Post


        let getAll () =
            register.PostAndReply Get


        // TODO: move the below logic into the Register processing logic itself
        let get (scriptPath: string) =
            let all: Transaction list = getAll()
            
            // here this is the list returned from get all 
            all |> List.filter (fun (t: Transaction) -> 
                t.Configuration.scriptPath = scriptPath || t.Configuration.stagedScriptPath = scriptPath
            ) |> List.tryExactlyOne


        let update (t: Transaction) =
            t |> Update |> register.Post        


    /// this module handles running individual transactions, updating the 
    /// register, and output streams of the transactions
    module TransactionRunner = 
        open System.Timers
        open System.Diagnostics
        open System.Collections.Concurrent

        type TransactionRunner = {
            fsiPath: string
            queue: ConcurrentQueue<Transaction>
            runTimer: Timer
            threadTracker: ConcurrentQueue<int32>
        }
        with 
            static member init (fsiPath: string) (threadCount: int32) = 
                let tr: TransactionRunner = {
                    fsiPath = fsiPath
                    queue = ConcurrentQueue<Transaction>()
                    runTimer = new Timer(60_000)
                    threadTracker = threadCount |> fun (tc: int32) ->
                        let cq: ConcurrentQueue<int32> = ConcurrentQueue<int32>()
                        for t=1 to tc do  // for x to y range is inclusive
                            cq.Enqueue t
                        cq
                }
                tr.runTimer.AutoReset <- true
                tr.runTimer.Elapsed.AddHandler tr.checkTransactionQueue
                tr.runTimer.Start()
                tr
        

            member this.checkTransactionQueue source (e: ElapsedEventArgs) =
                let transactions: Transaction list = TransactionRegister.getAll ()
                let needToRun: Transaction list = transactions|> List.filter (fun (x: Transaction) -> x.LastRunTime.AddMinutes(x.Configuration.pollingInterval) <= DateTime.Now)
                
                // Add any jobs set to run to the run queue
                for t: Transaction in needToRun do
                    this.queue.Enqueue t
                ()


            member private this.handleTransactionProcessExit (t: Transaction) (threadId : int32) (e: EventArgs) =
                // TODO: Build out logic here to handle result state (Pass/Fail, etc..)
                this.threadTracker.Enqueue threadId
                printfn $"%s{t.Configuration.scriptPath} process exited: %s{DateTime.Now.ToString()}"
                
                let latest: option<Transaction> = TransactionRegister.get t.Configuration.scriptPath
                match latest with
                | Some (lt: Transaction) ->
                    printfn "%A" lt.LastRunDetails
                | None ->
                    ()


            member private this.handleTransactionOutput (t: Transaction) (e: DataReceivedEventArgs) =
                // TODO: Build out logic here to handle successful results parsing
                if String.IsNullOrEmpty(e.Data) |> not then
                    let latest: option<Transaction> = TransactionRegister.get t.Configuration.scriptPath
                    match latest with
                    | Some (lt: Transaction) ->
                        match e.Data with
                        // PASSED:
                        | (text: string) when text.EndsWith(" passed") ->
                            let numString: string = text.Split(" ") |> Array.head
                            let mutable num: int = 0
                            if Int32.TryParse(numString, &num) then
                                lt.LastRunDetails.Passed <- num
                                TransactionRegister.update lt
                        // FAILED:
                        | (text: string) when text.EndsWith(" failed") ->
                            let numString: string = text.Split(" ") |> Array.head
                            let mutable num: int = 0
                            if Int32.TryParse(numString, &num) then
                                lt.LastRunDetails.Failed <- num
                                TransactionRegister.update lt
                        // SKIPPED:
                        | (text: string) when text.EndsWith(" skipped") ->
                            let numString: string = text.Split(" ") |> Array.head
                            let mutable num: int = 0
                            if Int32.TryParse(numString, &num) then
                                lt.LastRunDetails.Failed <- num
                                TransactionRegister.update lt
                        // Change this to a log:
                        | _ ->
                            lt.LastRunDetails.UnhandledOutput <- lt.LastRunDetails.UnhandledOutput + 1
                            TransactionRegister.update lt
                            // TODO: Maybe add setting to enable/disable this:
                            // consider as debug setting in future when debug settings are programmed. for now keep as .unhandled file -Tina
                            lt.WriteOutUnhandled STDOUT e.Data

                            LogWriter.writeLog  (MethodBase.GetCurrentMethod()) LogLevel.DEBUG $"STDOUT: Unhandled output received from %s{t.Configuration.scriptPath}: %s{e.Data}"
                            
                    | None ->
                        ()  // TODO: Add logging here.  We should never reach this.
          

            member private this.handleTransactionError (t: Transaction) (e: DataReceivedEventArgs) =
                // TODO: Build out logic here for error/failure parsing and handling
                if String.IsNullOrEmpty(e.Data) |> not then
                    let latest: option<Transaction> = TransactionRegister.get t.Configuration.scriptPath
                    match latest with
                    | Some (lt: Transaction) ->
                        match e.Data with
                        // Version mismatch:
                        | (text: string) when text.Contains("[WARNING]: This version of ChromeDriver has not been tested with Chrome version") ->
                            let ver: string = text.Substring(text.LastIndexOf("version ")).Replace("version ", "").Replace(".", "")
                            // Change this to a log:
                            LogWriter.writeLog  (MethodBase.GetCurrentMethod()) LogLevel.WARN $"ChromeDriver update necessary.  Expecting version %s{ver}."
                            //printfn $"ChromeDriver update necessary.  Expecting version %s{ver}."
                            lt.LastRunDetails.DriverVersionMismatch <- (true, ver)
                            TransactionRegister.update lt
                        // | (text: string) when text.EndsWith("subscribing a listener to the already connected DevToolsClient. Connection notification will not arrive.") ->
                        //     ignore text
                        | _ ->
                            lt.LastRunDetails.UnhandledErrors <- lt.LastRunDetails.UnhandledErrors + 1
                            TransactionRegister.update lt
                            lt.WriteOutUnhandled STDERR e.Data
                            // printfn $"Unhandled error received from %s{t.Configuration.scriptPath}:\n%s{e.Data}"
                            LogWriter.writeLog  (MethodBase.GetCurrentMethod()) LogLevel.DEBUG $"STDERROR: Unhandled output received from %s{t.Configuration.scriptPath}: %s{e.Data}"
                    | None ->
                        ()  // TODO: Add logging here.  We should never reach this.


            member this.runTransactions () =
                if this.queue.TryPeek() |> fst |> not then
                    //printfn "No transactions to run..."
                    ()
                else
                    if this.threadTracker.TryPeek() |> fst |> not then
                        printfn "No threads available to run transaction..."
                        ()
                    else
                        // TODO: This is probably unsafe, come back and fix it, if needed.
                        let t: Transaction = this.queue.TryDequeue() |> snd
                        let threadId: int32 = this.threadTracker.TryDequeue() |> snd

                        printfn $"%s{t.Configuration.scriptPath} last ran at: %s{t.LastRunTime.ToString()}"
                        t.LastRunTime <- DateTime.Now
                        TransactionRegister.update t
                        printfn $"%s{t.Configuration.scriptPath} starting at: %s{t.LastRunTime.ToString()}"

                        let psi: ProcessStartInfo = new ProcessStartInfo(this.fsiPath, $"%s{t.Configuration.stagedScriptPath}")
                        psi.UseShellExecute <- false
                        psi.RedirectStandardOutput <- true
                        psi.RedirectStandardError <- true
                        
                        let p: Process = new Process()
                        p.StartInfo <- psi
                        p.EnableRaisingEvents <- true
                        p.Exited.Add (this.handleTransactionProcessExit t threadId)
                        p.OutputDataReceived.Add (this.handleTransactionOutput t)
                        p.ErrorDataReceived.Add (this.handleTransactionError t)
                        p.Start() |> ignore
                        p.BeginOutputReadLine()
                        p.BeginErrorReadLine()
                        

    /// handles a file system watcher designed to watch transaction files for changes
    /// and handle adding or removing them to the transaction register
    module TransactionWatcher = 
        open System.Security.Cryptography


        type WatcherType =
            | Source
            | Staging



        let create (filter: string) addCb removeCb updateCb (dir: string) =
            Directory.CreateDirectory dir |> ignore
            let watcher: FileSystemWatcher = new FileSystemWatcher()
            watcher.Filter <- filter
            watcher.Path <- dir
            watcher.Created.Add (fun (n: FileSystemEventArgs) -> FileInfo(n.FullPath).FullName |> addCb)
            watcher.Deleted.Add (fun (n: FileSystemEventArgs) -> FileInfo(n.FullPath).FullName |> removeCb)
            watcher.Renamed.Add (fun (n: RenamedEventArgs) -> FileInfo(n.OldFullPath).FullName |> removeCb; FileInfo(n.FullPath).FullName |> addCb)
            watcher.Changed.Add (fun (n: FileSystemEventArgs) -> FileInfo(n.FullPath).FullName |> updateCb)
            watcher.SynchronizingObject <- null
            watcher.EnableRaisingEvents <- true
            watcher.IncludeSubdirectories <- true

            watcher


        let remove (path: string) =
            let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
            let mutable msg: string = ""
            match (TransactionRegister.get path) with
            | Some (t: Transaction) ->
                TransactionRegister.remove t.Configuration.scriptPath
                msg <- "staged transaction removed " + t.Configuration.stagedScriptPath
                File.Delete t.Configuration.stagedScriptPath
            | None ->
                msg <- "no transaction to remove at " + path

            LogWriter.writeLogAndPrintToConsole this LogLevel.INFO msg




        let add (path: string) =
            let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
            let mutable msg: string = ""

            match TransactionBuilder.buildTransaction path with
            | Some (tc: TransactionConfiguration) ->
                TransactionRegister.add tc
                msg <- $"added %s{path}"
            | None -> 
                msg <- $"unable to add %s{path}"
                ()

            LogWriter.writeLogAndPrintToConsole this LogLevel.INFO msg

        let update (path: string) =
            // TODO:  Make this method check hash of staged file against source file
            //        Do nothing if they are the same
            let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
            let mutable msg: string = ""

            match (TransactionRegister.get path) with
            | Some (t: Transaction) ->
                let sourceBytes: byte array = File.ReadAllBytes(path)
                let sourceHash: string = Convert.ToHexString(SHA1.Create().ComputeHash(sourceBytes))

                let stagedBytes: byte array = File.ReadAllBytes(t.Configuration.stagedScriptPath)
                let stagedHash: string = Convert.ToHexString(SHA1.Create().ComputeHash(stagedBytes))

                if sourceHash <> stagedHash then
                    remove path
                    add path
                    msg <- $"Script updated: %s{path}"
                // else
                //     msg <- $"%s{path} touched, but not changed.  Taking no action."

                LogWriter.writeLogAndPrintToConsole this LogLevel.INFO msg

            | None ->
                ()


        let validateFsx (path: string) =
            // TODO: Build out logic to have staged .fsx files validated
            let this: System.Reflection.MethodBase = System.Reflection.MethodBase.GetCurrentMethod()
            let msg: string = $"validating {path}" 
            
            LogWriter.writeLogAndPrintToConsole this LogLevel.INFO msg



        let createForDirs (watcherType: WatcherType) (dirArr: string array) =
            let mutable watcherArr: FileSystemWatcher array = [||]
            match watcherType with
            | Source ->
                for dir: string in dirArr do
                    watcherArr <- Array.append [|create "*.cwt" add remove update dir|] watcherArr
                    watcherArr <- Array.append [|create "*.fsx" add remove update dir|] watcherArr
                    ()
            | Staging ->
                for dir: string in dirArr do
                    watcherArr <- Array.append [|create "*.fsx" validateFsx (fun _ -> ()) validateFsx dir|] watcherArr
                    ()

            watcherArr


        // TODO: Need to enhance this method to check if secure mode is enabled.  If so
        //       it needs to also check the .authorized directory.
        let registerExistingScripts (confDirs: string array) =
            let this: Reflection.MethodBase = MethodBase.GetCurrentMethod()
            let mutable scriptPaths: string array = [||]
            for dir: string in confDirs do
                
                LogWriter.writeLogAndPrintToConsole this LogLevel.INFO $"registering directory {dir}"
                
                let dirInfo: DirectoryInfo = DirectoryInfo(dir)
                let cwtScripts: FileInfo array = dirInfo.GetFiles("*.cwt", SearchOption.AllDirectories)
                let fsxScripts: FileInfo array = dirInfo.GetFiles("*.fsx", SearchOption.AllDirectories)
                
                scriptPaths <- Array.append (cwtScripts |> Array.map(fun (fi: FileInfo) -> fi.FullName)) scriptPaths
                scriptPaths <- Array.append (fsxScripts |> Array.map(fun (fi: FileInfo) -> fi.FullName)) scriptPaths
            
            for sp: string in scriptPaths do
                add sp

            scriptPaths