namespace dtTransactions

module dtTransactions =
    open System
    open System.IO

    /// this is the configuration record for a specific transaction
    /// intended for use by the transaction runner. this configuration
    /// is composed together via defaults, directory specific, and
    /// configurations provided at the top of the transaction in a config
    /// tag. in order of precedence, file, directory, default is considered
    /// with file being the foremost important.
    type TransactionConfiguration = { 
        scriptPath: string
        stagedScriptPath: string
        logPath: string
        resultsPath: string
        screenshotPath: string
        pollingInterval: int
        browser: string
        browserOptions: string array
        browserDriverDir: string
        canopyConfig: string array
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
        canopyConfig: string array
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
        mutable BrowserDriverVersion: string
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
                RunDetails.BrowserDriverVersion = ""
                BrowserDriverPort = ""
                BrowserStarted = DateTime()
                Passed = 0
                Skipped = 0
                Failed = 0
                UnhandledOutput = 0
                UnhandledErrors = 0
                DriverVersionMismatch = (false, "")
            }


