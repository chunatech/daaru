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
                for t=0 to tc do
                    cq.Enqueue t
                cq
        }
        tr.runTimer.AutoReset <- true
        tr.runTimer.Elapsed.AddHandler tr.checkTransactionQueue
        tr.runTimer.Start()
        tr

    member this.checkTransactionQueue source (e: ElapsedEventArgs) =
        let transactions: Transaction list = Register.get ()
        let needToRun: Transaction list = transactions|> List.filter (fun (x: Transaction) -> x.LastRunTime.AddMinutes(x.Configuration.pollingInterval) <= DateTime.Now)
        
        // Add any jobs set to run to the run queue
        for t: Transaction in needToRun do
            this.queue.Enqueue t
        ()

    member private this.handleTransactionProcessExit (t: Transaction) (e: EventArgs) =
        
        printfn $"%s{t.Configuration.scriptPath} process exited: %s{DateTime.Now.ToString()}"

    member private this.handleTransactionOutput (t: Transaction) (e: DataReceivedEventArgs) =
        if String.IsNullOrEmpty(e.Data) |> not then
            printfn $"Output received from %s{t.Configuration.scriptPath}:\n%s{e.Data}"
        ()

    member private this.handleTransactionError (t: Transaction) (e: DataReceivedEventArgs) =
        if String.IsNullOrEmpty(e.Data) |> not then
            printfn $"Error received from %s{t.Configuration.scriptPath}:\n%s{e.Data}"
        ()

    member this.runTransactions () =
        if this.queue.TryPeek() |> fst |> not then
            //printfn "No transactions to run..."
            ()
        else
            // TODO: This is probably unsafe, come back and fix it.
            let t: Transaction = this.queue.TryDequeue() |> snd

            printfn $"%s{t.Configuration.scriptPath} last ran at: %s{t.LastRunTime.ToString()}"
            t.LastRunTime <- DateTime.Now
            Register.update t
            printfn $"%s{t.Configuration.scriptPath} starting at: %s{t.LastRunTime.ToString()}"

            let psi: ProcessStartInfo = new ProcessStartInfo(this.fsiPath, $"%s{t.Configuration.scriptPath}")
            psi.UseShellExecute <- false
            psi.RedirectStandardOutput <- true
            psi.RedirectStandardError <- true
            
            let p: Process = new Process()
            p.StartInfo <- psi
            p.EnableRaisingEvents <- true
            p.Exited.Add (this.handleTransactionProcessExit t)
            p.OutputDataReceived.Add (this.handleTransactionOutput t)
            p.ErrorDataReceived.Add (this.handleTransactionError t)
            p.Start() |> ignore
            p.BeginOutputReadLine()
            p.BeginErrorReadLine()
            // p.WaitForExit()
                

