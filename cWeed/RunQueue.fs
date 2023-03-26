module RunQueue

open System
open System.Timers
open System.Diagnostics

open CWeedTransactions
open System.Collections.Concurrent


// wrapper around concurrent queue to store transactions
// that need to run in a given minute
[<Struct>]
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
        let transactions: Transaction list = Register.getAll ()
        let needToRun: Transaction list = transactions|> List.filter (fun (x: Transaction) -> x.LastRunTime.AddMinutes(x.Configuration.pollingInterval) <= DateTime.Now)
        
        // Add any jobs set to run to the run queue
        for t: Transaction in needToRun do
            this.queue.Enqueue t
        ()


    member private this.handleTransactionProcessExit (t: Transaction) (threadId : int32) (e: EventArgs) =
        // TODO: Build out logic here to handle result state (Pass/Fail, etc..)
        this.threadTracker.Enqueue threadId
        printfn $"%s{t.Configuration.scriptPath} process exited: %s{DateTime.Now.ToString()}"
        
        let latest: option<Transaction> = Register.get t.Configuration.scriptPath
        match latest with
        | Some (lt: Transaction) ->
            printfn "%A" lt.LastRunDetails
        | None ->
            ()


    member private this.handleTransactionOutput (t: Transaction) (e: DataReceivedEventArgs) =
        // TODO: Build out logic here to handle successful results parsing
        let latest: option<Transaction> = Register.get t.Configuration.scriptPath
        match latest with
        | Some (lt: Transaction) ->
            if String.IsNullOrEmpty(e.Data) |> not then
                match e.Data with
                // PASSED:
                | (text: string) when text.EndsWith(" passed") ->
                    let numString: string = text.Split(" ") |> Array.head
                    let mutable num: int = 0
                    if Int32.TryParse(numString, &num) then
                        lt.LastRunDetails.Passed <- num
                        Register.update lt
                // FAILED:
                | (text: string) when text.EndsWith(" failed") ->
                    let numString: string = text.Split(" ") |> Array.head
                    let mutable num: int = 0
                    if Int32.TryParse(numString, &num) then
                        lt.LastRunDetails.Failed <- num
                        Register.update lt
                // SKIPPED:
                | (text: string) when text.EndsWith(" skipped") ->
                    let numString: string = text.Split(" ") |> Array.head
                    let mutable num: int = 0
                    if Int32.TryParse(numString, &num) then
                        lt.LastRunDetails.Failed <- num
                        Register.update lt
                // Change this to a log:
                | _ ->
                    printfn $"Unhandled output received from %s{t.Configuration.scriptPath}:\n%s{e.Data}"
        | None ->
            ()  // TODO: Add logging here.  We should never reach this.


    member private this.handleTransactionError (t: Transaction) (e: DataReceivedEventArgs) =
        // TODO: Build out logic here for error/failure parsing and handling
        if String.IsNullOrEmpty(e.Data) |> not then
            match e.Data with
            | (text: string) when text.Contains("[WARNING]: This version of ChromeDriver has not been tested with Chrome version") ->
                let ver: string = text.Substring(text.LastIndexOf(" 1"))
                // Change this to a log:
                printfn $"ChromeDriver update necessary.  Expecting version%s{ver}"
            | (text: string) when text.EndsWith("subscribing a listener to the already connected DevToolsClient. Connection notification will not arrive.") ->
                ignore text
            | _ ->
                t.LastRunDetails.UnhandledErrors <- t.LastRunDetails.UnhandledErrors + 1
                Register.update t
                printfn $"Unhandled error received from %s{t.Configuration.scriptPath}:\n%s{e.Data}"
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
                Register.update t
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
                

