# Logging 

The cWeed logger is configurabe by the user (see [Configuring cWeed]()). It handles all of the applications logging, 
and also can be used to log information from transactions 

## Log Format 

the format is: 

`timestamp [severity] method.call 'msg'`

This is an example log line. each log occupies only one line. New entries are on new lines. each field is seperated
by spaces.  

```
20230705_155055.479 [INFO] Program.main 'transaction runner initialized'
```

Field | Description 
| -- | -- |
timestamp | this is the date ordered 4 digit year, 2 digit month, 2 digit day. then `_` and the time in 24hr format to ms precision which is notated with a `.`
severity | this is the severity level of the log expressed as a string, in all caps 
method call | this is the method which generated the log. It utilizes dot notation  
message |  the acutal message, in between `'` characters


## Log Severity 

cWeed has multiple levels of logging that can be configured ranging from `Debug` to `Critical`

Severity | Int | Description
|--|--|--|
DEBUG | 0 | Debugging logs, mostly for development
INFO | 1 | Informational logs 
WARN | 2 | These considerations will not stop the program from running but may warn of an underlying issue
ERROR | 3 | These can cause the program to halt and signify an issue that needs to be resolved. 
CRITICAL | 4 | These are errors that come from reaching unreachable branches or other highly unexpected behaviors and will halt the program.


## Logging in Transactions 

cWeed allows for use of its logger in your transactions files via prepending logged items with a special string notation

example string format showcasing an informational log 

```fsharp 
printfn "[[LOG]][INFO] this would be the message that gets logged" 
```
cWeed consumes the stdout and stderr streams for running transactions. The program will parse lines tagged like this 
into logs and place them into files named after the transaction which the log comes from. These files are also subject
to log rolling and located in the configured logs directory. 

