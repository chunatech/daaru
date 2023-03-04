module CWeedTransactions

open Configuration

type Transaction =
    { Path: string
      Configuration: TransactionConfiguration }