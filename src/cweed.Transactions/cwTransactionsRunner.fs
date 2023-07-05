namespace cwTransactions

/// this module handles running individual transactions, updating the 
/// register, and output streams of the transactions
module cwTransactionRunner = 
    open System
    open System.Timers
    open System.Diagnostics
    open System.Collections.Concurrent
    open System.Text.RegularExpressions

    open cweed.Utils

    open cwTransactions

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
            let transactions: Transaction list = cwTransactionRegister.getAll ()
            let needToRun: Transaction list = transactions|> List.filter (fun (x: Transaction) -> x.LastRunTime.AddMinutes(x.Configuration.pollingInterval) <= DateTime.Now)
            
            // Add any jobs set to run to the run queue
            for t: Transaction in needToRun do
                this.queue.Enqueue t
            ()


        member private this.handleTransactionProcessExit (t: Transaction) (threadId : int32) (e: EventArgs) =
            // TODO: Build out logic here to handle result state (Pass/Fail, etc..)
            this.threadTracker.Enqueue threadId
            printfn $"%s{t.Configuration.scriptPath} process exited: %s{DateTime.Now.ToString()}"
            
            let latest: option<Transaction> = cwTransactionRegister.get t.Configuration.scriptPath
            match latest with
            | Some (lt: Transaction) ->
                if lt.LastRunDetails.Failed > 0 then
                    lt.LastFailure <- DateTime.Now

                    if lt.ConsecutiveRunCount > 0 then
                        lt.ConsecutiveRunCount <- -1
                    else
                        lt.ConsecutiveRunCount <- lt.ConsecutiveRunCount - 1
                elif lt.LastRunDetails.Passed > 0 then
                    lt.LastSuccess <- DateTime.Now

                    if lt.ConsecutiveRunCount > 0 then
                        lt.ConsecutiveRunCount <- lt.ConsecutiveRunCount + 1
                    else
                        lt.ConsecutiveRunCount <- 1
                cwTransactionRegister.update lt
                printfn "%A" lt
            | None ->
                ()


        member private this.handleTransactionOutput (t: Transaction) (e: DataReceivedEventArgs) =
            if String.IsNullOrEmpty(e.Data) |> not then
                let latest: option<Transaction> = cwTransactionRegister.get t.Configuration.scriptPath
                match latest with
                | Some (lt: Transaction) ->
                    match e.Data with
                    // STARTING (Extract driver port and version):
                    | (text: string) when text.StartsWith("Starting ") ->
                        let port: string = text.Split(" on port ")[1]
                        lt.LastRunDetails.BrowserDriverPort <- port

                        let pattern: Regex = Regex(@"\b\d+\.[0-9\.]+\b")
                        let found: Match = pattern.Match(text)

                        if found.Success then
                            lt.LastRunDetails.BrowserDriverVersion <- found.Value

                        cwTransactionRegister.update lt
                    // STARTED:
                    | (text: string) when text.EndsWith("started successfully.") ->
                        lt.LastRunDetails.BrowserStarted <- DateTime.Now
                        cwTransactionRegister.update lt
                    // PASSED:
                    | (text: string) when text.EndsWith(" passed") ->
                        let numString: string = text.Split(" ") |> Array.head
                        let mutable num: int = 0
                        if Int32.TryParse(numString, &num) then
                            lt.LastRunDetails.Passed <- num
                            cwTransactionRegister.update lt
                    // FAILED:
                    | (text: string) when text.EndsWith(" failed") ->
                        let numString: string = text.Split(" ") |> Array.head
                        let mutable num: int = 0
                        if Int32.TryParse(numString, &num) then
                            lt.LastRunDetails.Failed <- num
                            cwTransactionRegister.update lt
                    // SKIPPED:
                    | (text: string) when text.EndsWith(" skipped") ->
                        let numString: string = text.Split(" ") |> Array.head
                        let mutable num: int = 0
                        if Int32.TryParse(numString, &num) then
                            lt.LastRunDetails.Skipped <- num
                            cwTransactionRegister.update lt
                    // RESULTS:
                    | (text: string) when text.StartsWith("[[RESULT_HEADER]]") ->
                        let header: string = (e.Data.Split("[[RESULT]]")[0]).Replace("[[RESULT_HEADER]]","")
                        let results: string = e.Data.Split("[[RESULT]]")[1]
                        CsvTools.appendStringToCSVFile lt.Configuration.resultsPath header results
                    // LOG:
                    | (text: string) when text.StartsWith("[[LOG]]") ->
                        printfn "%s" (e.Data.Replace("[[LOG]]", ""))
                        printfn "%s" lt.Configuration.logPath
                        //TODO: actually write out the log lines to the logPath of the transaction
                        //      (pending Tina's adjustment of the logger)
                    // Change this to a log:
                    | _ ->
                        lt.LastRunDetails.UnhandledOutput <- lt.LastRunDetails.UnhandledOutput + 1
                        cwTransactionRegister.update lt
                        // TODO: Maybe add setting to enable/disable this:
                        // consider as debug setting in future when debug settings are programmed. for now keep as .unhandled file -Tina
                        lt.WriteOutUnhandled STDOUT e.Data

                | None ->
                    ()  // TODO: Add logging here.  We should never reach this.
        

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