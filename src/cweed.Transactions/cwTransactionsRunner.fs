namespace cwTransactions

/// this module handles running individual transactions, updating the 
/// register, and output streams of the transactions
module cwTransactionRunner = 
    open System
    open System.IO
    open System.Timers
    open System.Diagnostics
    open System.Collections.Concurrent
    open System.Text.RegularExpressions

    open cweed.Utils

    open cwTransactions
    open cwLogger.Logger
    open System.Reflection

    type TransactionRunner = {
        fsiPath: string
        queue: ConcurrentQueue<Transaction>
        runTimer: Timer
        threadTracker: ConcurrentQueue<int32>
        logger: Logger
        logFile: String
    }
    with 
        static member init (fsiPath: string) (threadCount: int32) (logger: Logger) (logFile: string) = 
            let tr: TransactionRunner = {
                fsiPath = fsiPath
                queue = ConcurrentQueue<Transaction>()
                runTimer = new Timer(60_000)
                threadTracker = threadCount |> fun (tc: int32) ->
                    let cq: ConcurrentQueue<int32> = ConcurrentQueue<int32>()
                    for t=1 to tc do  // for x to y range is inclusive
                        cq.Enqueue t
                    cq
                logger = logger
                logFile = logFile 
            }

            // set up run timer 
            tr.runTimer.AutoReset <- true
            logger.Log tr.logFile (MethodBase.GetCurrentMethod()) Severity.Debug $"TransactionRunner runTimer.AutoReset set to %A{(tr.runTimer.AutoReset)}"

            tr.runTimer.Elapsed.AddHandler tr.checkTransactionQueue
            logger.Log tr.logFile (MethodBase.GetCurrentMethod()) Severity.Debug $"TransactionRunner runTimer.Elapsed handler set to checkTransactionQueue"

            tr.runTimer.Start()
            logger.Log tr.logFile (MethodBase.GetCurrentMethod()) Severity.Debug $"TransactionRunner runTimer started"
            
            tr
    

        member this.checkTransactionQueue source (e: ElapsedEventArgs) =
            let transactions: Transaction list = cwTransactionRegister.getAll ()
            this.logger.Log this.logFile (MethodBase.GetCurrentMethod()) Severity.Debug $"transactions %s{transactions.ToString()}"
            let needToRun: Transaction list = transactions|> List.filter (fun (x: Transaction) -> x.LastRunTime.AddMinutes(x.Configuration.pollingInterval) <= DateTime.Now)
            
            // Add any jobs set to run to the run queue
            for t: Transaction in needToRun do
                this.queue.Enqueue t
                this.logger.Log this.logFile (MethodBase.GetCurrentMethod()) Severity.Info $"%s{t.Configuration.scriptPath} is queued to run"
            ()


        member private this.handleTransactionProcessExit (t: Transaction) (threadId : int32) (e: EventArgs) =
            let log = this.logger.Log this.logFile (MethodBase.GetCurrentMethod())
            log Severity.Debug $"running handleTransactionProcessExit for %s{t.Configuration.scriptPath}"
            // TODO: Build out logic here to handle result state (Pass/Fail, etc..)
            this.threadTracker.Enqueue threadId
            log Severity.Debug $"returning thread %d{threadId}"

            log Severity.Info $"%s{t.Configuration.scriptPath} process exited at %s{DateTime.Now.ToString()}"

            let latest: option<Transaction> = cwTransactionRegister.get t.Configuration.scriptPath

            match latest with
            | Some (lt: Transaction) ->
                log Severity.Debug $"matching on latest transaction %s{lt.Configuration.scriptPath}"

                if lt.LastRunDetails.Failed > 0 then
                    lt.LastFailure <- DateTime.Now
                    log Severity.Debug $"failure detected at %s{lt.LastFailure.ToString()}"

                    if lt.ConsecutiveRunCount > 0 then
                        lt.ConsecutiveRunCount <- -1
                        log Severity.Debug $"setting ConsecutiveRunCount to %d{lt.ConsecutiveRunCount}"

                    else
                        lt.ConsecutiveRunCount <- lt.ConsecutiveRunCount - 1
                        log Severity.Debug $"decrementing transaction ConsecutiveRunCount %d{lt.ConsecutiveRunCount}"
                
                elif lt.LastRunDetails.Passed > 0 then
                    lt.LastSuccess <- DateTime.Now
                    log Severity.Debug $"success detected at %s{lt.LastSuccess.ToString()}"

                    if lt.ConsecutiveRunCount > 0 then
                        lt.ConsecutiveRunCount <- lt.ConsecutiveRunCount + 1
                        log Severity.Debug $"incrementing ConsecutiveRunCount to %d{lt.ConsecutiveRunCount}"

                    else
                        lt.ConsecutiveRunCount <- 1
                        log Severity.Debug $"setting ConsecutiveRunCount to %d{lt.ConsecutiveRunCount}"

                log Severity.Debug $"running cwTransactionRegister update handle"
                cwTransactionRegister.update lt
                printfn "%A" lt
            | None ->
                log Severity.Debug "no latest transaction detected this cycle"


        member private this.handleTransactionOutput (t: Transaction) (e: DataReceivedEventArgs) =
            let log = this.logger.Log this.logFile (MethodBase.GetCurrentMethod())
            log Severity.Debug $"running handleTransactionOutput for %s{t.Configuration.stagedScriptPath}"
            
            if String.IsNullOrEmpty(e.Data) |> not then
                log Severity.Debug $"e.Data is not empty or null. parsing e.Data for latest transaction"
                let latest: option<Transaction> = cwTransactionRegister.get t.Configuration.scriptPath
                
                match latest with
                | Some (lt: Transaction) ->
                    log Severity.Debug $"latest transaction %s{lt.Configuration.scriptPath}"

                    match e.Data with
                    // STARTING (Extract driver port and version):
                    | (text: string) when text.StartsWith("Starting ") ->
                        log Severity.Debug $"extracting driver port and version information"
                        let port: string = text.Split(" on port ")[1]
                        lt.LastRunDetails.BrowserDriverPort <- port
                        log Severity.Debug $"browser port set to %s{port}" 
                        let pattern: Regex = Regex(@"\b\d+\.[0-9\.]+\b")
                        let found: Match = pattern.Match(text)

                        if found.Success then
                            lt.LastRunDetails.BrowserDriverVersion <- found.Value
                            log Severity.Debug $"driver version detected. %s{lt.LastRunDetails.BrowserDriverVersion}"
                        
                        log Severity.Debug $"running cwTransactionRegsiter.update hook for %s{lt.Configuration.scriptPath}"
                        cwTransactionRegister.update lt

                    // STARTED:
                    | (text: string) when text.EndsWith("started successfully.") ->
                        lt.LastRunDetails.BrowserStarted <- DateTime.Now
                        log Severity.Debug $"browser started for %s{lt.Configuration.scriptPath} at %s{lt.LastRunDetails.BrowserStarted.ToString()}"

                        log Severity.Debug $"running cwTransactionRegsiter.update hook for %s{lt.Configuration.scriptPath}"
                        cwTransactionRegister.update lt

                    // PASSED:
                    | (text: string) when text.EndsWith(" passed") ->
                        log Severity.Debug $"transaction passed output detected"
                        let numString: string = text.Split(" ") |> Array.head
                        let mutable num: int = 0
                        if Int32.TryParse(numString, &num) then
                            lt.LastRunDetails.Passed <- num
                            log Severity.Debug $"setting passed value for %s{lt.Configuration.scriptPath} to %d{lt.LastRunDetails.Passed}"

                            log Severity.Debug $"running cwTransactionRegsiter.update hook for %s{lt.Configuration.scriptPath}"
                            cwTransactionRegister.update lt

                    // FAILED:
                    | (text: string) when text.EndsWith(" failed") ->
                        log Severity.Debug $"transaction failed output detected"
                        let numString: string = text.Split(" ") |> Array.head
                        let mutable num: int = 0
                        if Int32.TryParse(numString, &num) then
                            lt.LastRunDetails.Failed <- num
                            log Severity.Debug $"setting failure value for %s{lt.Configuration.scriptPath} to %d{lt.LastRunDetails.Failed}"

                            log Severity.Debug $"running cwTransactionRegsiter.update hook for %s{lt.Configuration.scriptPath}"
                            cwTransactionRegister.update lt
                    // SKIPPED:
                    | (text: string) when text.EndsWith(" skipped") ->
                        log Severity.Debug $"transaction skipped output detected"
                        let numString: string = text.Split(" ") |> Array.head
                        let mutable num: int = 0
                        if Int32.TryParse(numString, &num) then
                            lt.LastRunDetails.Skipped <- num
                            log Severity.Debug $"setting skipped value for %s{lt.Configuration.scriptPath} to %d{lt.LastRunDetails.Skipped}"

                            log Severity.Debug $"running cwTransactionRegsiter.update hook for %s{lt.Configuration.scriptPath}"
                            cwTransactionRegister.update lt

                    // RESULTS:
                    | (text: string) when text.StartsWith("[[RESULT_HEADER]]") ->
                        log Severity.Debug $"detected results output"
                        let header: string = (e.Data.Split("[[RESULT]]")[0]).Replace("[[RESULT_HEADER]]","")
                        let results: string = e.Data.Split("[[RESULT]]")[1]

                        log Severity.Debug $"appending results to csv at %s{lt.Configuration.resultsPath}"
                        CsvTools.appendStringToCSVFile lt.Configuration.resultsPath header results
                    // LOG:
                    | (text: string) when text.StartsWith("[[LOG]]") ->
                        log Severity.Debug $"log output detected"
                        // fn to get the log message and severity from the parsed output
                        let logFromParsedOutput (parsedLog: string) =  
                            let (sev: Severity) , (msg: string) = 
                                match parsedLog with 
                                | (parsedLog: string) when parsedLog.StartsWith("[DEBUG]") -> 
                                    Severity.Debug, parsedLog.Replace("[DEBUG]", "")
                                | (parsedLog: string) when parsedLog.StartsWith("[INFO]") -> 
                                    Severity.Info, parsedLog.Replace("[INFO]", "")
                                | (parsedLog: string) when parsedLog.StartsWith("[WARN]") -> 
                                    Severity.Warn, parsedLog.Replace("[WARN]", "")
                                | (parsedLog: string) when parsedLog.StartsWith("[ERROR]") -> 
                                    Severity.Error, parsedLog.Replace("[ERROR]", "")
                                | (parsedLog: string) when parsedLog.StartsWith("[CRITICAL]") -> 
                                    Severity.Critical, parsedLog.Replace("[CRITICAL]", "") 
                                | _ -> Severity.Info, parsedLog 
                            sev, (msg.TrimStart())

                        
                        let sev, msg = logFromParsedOutput (e.Data.Replace("[[LOG]]", ""))

                        log Severity.Debug $"generating log at %s{lt.Configuration.logPath} for %s{lt.Configuration.scriptPath}"
                        this.logger.Log lt.Configuration.logPath (MethodBase.GetCurrentMethod()) sev msg

                    // Change this to a log:
                    | _ ->
                        lt.LastRunDetails.UnhandledOutput <- lt.LastRunDetails.UnhandledOutput + 1
                        cwTransactionRegister.update lt
                        // TODO: Maybe add setting to enable/disable this:
                        // consider as debug setting in future when debug settings are programmed. for now keep as .unhandled file -Tina
                        lt.WriteOutUnhandled STDOUT e.Data

                | None ->
                    log Severity.Critical $"reached unreachable branch"
                    // usually don't want to process logs outside of main, but the logs need 
                    // to be cleaned up before exit call
                    this.logger.ProcessQueue() 
                    exit -1
        

        member private this.handleTransactionError (t: Transaction) (e: DataReceivedEventArgs) =
            // TODO: Build out logic here for error/failure parsing and handling
            if String.IsNullOrEmpty(e.Data) |> not then
                let latest: option<Transaction> = cwTransactionRegister.get t.Configuration.scriptPath
                match latest with
                | Some (lt: Transaction) ->
                    match e.Data with
                    // Version mismatch:
                    | (text: string) when text.Contains("[WARNING]: This version of ChromeDriver has not been tested with Chrome version") ->
                        let ver: string = text.Substring(text.LastIndexOf("version ")).Replace("version ", "").Replace(".", "")
                        // Change this to a log:
                        lt.LastRunDetails.DriverVersionMismatch <- (true, ver)
                        cwTransactionRegister.update lt
                    // | (text: string) when text.EndsWith("subscribing a listener to the already connected DevToolsClient. Connection notification will not arrive.") ->
                    //     ignore text
                    | _ ->
                        lt.LastRunDetails.UnhandledErrors <- lt.LastRunDetails.UnhandledErrors + 1
                        cwTransactionRegister.update lt
                        lt.WriteOutUnhandled STDERR e.Data
                | None ->
                   ()


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
                    cwTransactionRegister.update t
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