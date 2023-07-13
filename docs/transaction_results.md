# Transaction Results Publishing 

cWeed will publish the results of the transactions it runs as `.csv` files in 
either a configured location or the `results` directory of the application 
folder by default. 


## File naming convention 

Results files are named after the transaction being run. The convention is: 

> `{filename}_results.csv`

## File rolling 

Results files are rolled by default at `10MB`. If an alternate rolling size in 
logging is set, cWeed will currently honor this size for results files as well.

## Results `.csv` Fields 

Field | value | Description
|--|--|--|
timestamp| date | timestamp of the result entry 
result | passed/failed | the outcome of the transaction 
resultReason | string msg | this is a description of the reason given by cWeed for the result obtained 
targetUrl | url | the most recent url the transaction navigated to
application | string | this is a data field that can be specified by the user to describe the application the transaction is for 
useCaseDescription | string | a description of what the use case the transaction covers 
totalDuration | int | how long the transaction took to run 
totalDurationThreshold | int | this is set by the user, and signifies the maximum time alotted for a transaction to run before failing out
lastTestRun | date | the last time the trancaction was run 
lastTestDuration | int | totalDuration for the last run 
lastTestDurationThreshold | int | totalDurationThreshold value for the last run 
appendedMessages | string | any other messages the user would like to include 


### timestamp 

The time of publishing for the result entry, formatted to seconds precision as 
follows: 

`YYYYMMDD_HHmmss`

### Result 

Currently contains either `passed` or `failed` values, which represent whether 
a transaction passed or failed. 

### Result Reason 

Describes the reason for the result.

> An example of this would be something like: `'transaction completed successfully'` or in the case of a failure something like `'totalDurationThreshold reached'`

### Target Url

The last url cWeed navigated to before completing the transaction. 

### Application 

This option is more for the user to be able to give more context to the 
transaction being run. For example, if the user was running a transaction to 
test whether a core application of their business was running they could give 
the application name in this field of the results as a datapoint for analytics. 

This option is set in the transaction by using the `useCase` helper function. 

> See Transaction Setup for more details on how to write transactions and a 
> list of helper functions 

### Use Case Description

This is another option set by the user through `useCase` and describes the use
case for the transactions. 

> An example could be something like `'can reach endpoint at someimportantdomain.yourcompany.com'`

### Total Duration 

The amount of time in seconds that the transaction took to run 

### Total Duration Threshold 

The amount of time in seconds allowed for a specific transaction to run before 
it is considered a failure case. This is useful to catch network related errors. 

### Last Test Run 

Results for the previous run of this transaction, describes as `passed` or `failed` 

### Last Test Duration 

The amount of time in seconds that the transaction took to run on its previous 
run. 

### Last Test Duration Threshold 

The value of totalDurationThreshold for the previous run of the transaction.

## Results Processing 

cWeed can be configured to process the results at the end of a transaction via 
use of a results processing script of your choice. an simple example is included
with the application that just does a word count of the result. 

In order to use this functionality, set the `resultsProcessingScript` option in 
`config.json` to specify the runner and path of the results processing script.
The output and error streams are logged to the generated logfile for the
transaction.

*configuration example*
```json
// include in config.json
"resultsProcessingScript": {
    "resultsScriptPath": "path/to/script",
    "resultsRunnerPath": "path/to/runner"
}
```

