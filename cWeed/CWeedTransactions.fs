module CWeedTransactions

open System
open Configuration


type Transaction = {
    Configuration: TransactionConfiguration
    mutable LastRunTime: DateTime
    mutable LastSuccess: DateTime
    mutable LastFailure: DateTime
    mutable ConsecutiveRunCount: Int32
}
with
    static member Create (tc: TransactionConfiguration) =
        {
            Transaction.Configuration = tc
            LastRunTime = DateTime()
            LastSuccess = DateTime()
            LastFailure = DateTime()
            ConsecutiveRunCount = 0
        }