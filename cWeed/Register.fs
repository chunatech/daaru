module Register


type private Msg =
    | Add of path: string
    | Remove of string
    | Get of AsyncReplyChannel<string list>


let private register: MailboxProcessor<Msg> =
    MailboxProcessor.Start (fun (inbox: MailboxProcessor<Msg>) ->
        // Define processing loop
        let rec loop (lst: string list) = async {

            // Receive message
            let! (msg: Msg) = inbox.Receive()

            // Process message and kick off next iteration
            match msg with
            | Add (n: string) ->
                return! loop (n::lst)
            | Remove (n: string) ->
                return! loop (lst |> List.filter(fun (f: string) -> f <> n))
            | Get (rc: AsyncReplyChannel<string list>) ->
                rc.Reply lst
                return! loop lst
        }
        loop [] )


let add (name: string) =
    name |> Add |> register.Post


let remove (name: string) =
    name |> Remove |> register.Post


let get () =
    register.PostAndReply Get