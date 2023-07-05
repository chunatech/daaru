namespace cwTransactions

/// holds a list of located transactions and handles the transaction registration 
/// process 
module cwTransactionRegister = 
    open cwTransactions

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
