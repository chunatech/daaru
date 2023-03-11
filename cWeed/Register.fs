module Register

open System
open Configuration
open CWeedTransactions

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


let get () =
    register.PostAndReply Get


let update (t: Transaction) =
    t |> Update |> register.Post