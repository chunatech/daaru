module RunQueue

open System
open System.Timers
open System.Diagnostics

open CWeedTransactions
open System.Collections.Concurrent


// concurrent queue to store transactions that need to run this minute
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
        // and update the LastRunTime
        for t: Transaction in needToRun do
            t.LastRunTime <- DateTime.Now
            Register.update t
            this.queue.Enqueue t
        ()

    member this.runTransactions () =
        if this.queue.TryPeek() |> fst |> not then
            printfn "No transactions to run..."
        else
            // TODO: This is probably unsafe, come back and fix it.
            let t: Transaction = this.queue.TryDequeue() |> snd
            printfn "%s" (t.LastRunTime.ToString())
            let tTask (fsiPath: string) (transaction: Transaction) =
                // TODO: Figure out how to make a process run a callback when exited
                let scriptPath: string = transaction.Configuration.scriptPath
                let psi: ProcessStartInfo = new ProcessStartInfo(fsiPath, $"%s{scriptPath}")
                psi.UseShellExecute <- false

                task {
                    let p: Process = new Process()
                    p.StartInfo <- psi
                    p.EnableRaisingEvents <- true
                    // p.Exited += EventHandler
                    p.Start() |> ignore
                }

            tTask this.fsiPath t |> ignore
