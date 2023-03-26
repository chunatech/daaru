module CWeedTransactions

open System
open Configuration


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
and RunDetails = {
    mutable BrowserStarting: DateTime
    mutable BrowserDriverPort: string
    mutable BrowserStarted: DateTime
    mutable Passed: int
    mutable Skipped: int
    mutable Failed: int
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
            UnhandledErrors = 0
            DriverVersionMismatch = (false, "")
        }