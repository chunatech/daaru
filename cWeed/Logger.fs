(* Notes for me

LogEntry formats as follows: 

in json fmt
{
     "timestamp": "2021-08-08 18:04:14.721", // example timestamp 
     "level": "INFO" // TODO: discuss log levels with chase to find out whats needed 
     "logger": "main" // This is the entity doing the logging  
     "message": "this is the actual log message we got"
}

in unstructured fmt
"2021-08-08 18:04:14.721 [INFO] Module.MethodName 'hello from logger'"

----------------------------------------------------------

Logger responsibilities:

- read in log settings file and override defaults with 
  settings provided
    - read in settings from json

- settings 
    - Manage Log Format (unstructured || json)
        - default is unstructured
    - Manage time format (system || zulu)
        - default is system time
    - LogDirectoryLocation (string filepath)
    - rollAt: (int size <in MB>) default ~10MB

- create LogEntry records that express logged events 

- Manage a Queue of LogEntry
    - hold LogEntry records that have not yet been logged to 
      a file 
    - remove records that have been logged out from the queue
    - should be FIFO priority

- Create Log Directory if !Exists else use existing dir

- Create Log Files that hold logged events in format specified
    - format for unstructured:
        - each line represents one entry 
    - format for json: 
        - each file is array of LogEntry json objs [{LogEntry}, {LogEntry} ...]

- Roll Logs with timestamps for easy querying by end user 
    - roll timestamps precision to the second
        - formatting
            - the current log file looks like 
                ApplicationName.log
                ApplicationName.log.json
            - after a roll it will look like
                ApplicationName_{firsttimestamp}_{lasttimestamp}.log
                ApplicationName_{firsttimestamp}_{lasttimestamp}.log.json

------------------------------------------------------------

TASKS:

[x] data LogLevel (option)
[x] data LogEntry (record)
[x] data LoggerSettings (record)
[x] data LogEntryQueue (Queue)


[x] create LoggerSettings record
    [x] method ReadSettingsFromFile (string filepath)
    [x] create LoggerSettings record and populate with user 
        settings || defaults if applicable

[/] create Logger module
    [x] hold current LoggerSettings record 
    [x] check if log directory exists in LogDirectoryLocation and 
        create if !Exists using location and name specified in settings
    [x] hold CurrentLogFilePath (string) 
    [x] hold LogEntryQueue
    [/] LogEntryQueue management 
        [x] queue a log entry 
        [_] try dequeuing a log entry #figure out how to fail this..
    [x] method WriteLog level callerMethod msg

    These are built but need to be tested 
    [/] method RollLogFile  
    [/] handle roll the logs when the log file reaches ~10MB
        [/] rename the current log file to specified fmt ^ 
        [/] create a new log file with naming convention ^


NON MVP Tasks: 
[ ] fix settings file to have optional fields and instead compose with defaults for whatever is not 
    present
*)
module Logger

open System
open System.IO
open System.Reflection
open System.Text
open System.Collections.Concurrent
open System.Text.RegularExpressions
open System.Linq

// handles json conversions 
open Thoth.Json.Net

// general utilities 
open Utils


/// this union represents the level of log verbosity
/// INFO - this is an informational log and occurs whenever an event of significance happens
/// WARN - these are warnings that something has not happened as stated, but was recoverable
/// ERROR - this is anything that is caught from a result or catch block
/// CRITICAL - these are breaking errors that need attention 
/// DEBUG - developer level debugging information
type LogLevel = 
    | INFO
    | WARN
    | ERROR 
    | CRITICAL
    | DEBUG
with 

    /// method that gives a string value representing a LogLevel
    member this.ToString : string = 
        match this with 
            | INFO -> "INFO"
            | WARN -> "WARN"
            | ERROR -> "ERROR"
            | CRITICAL -> "CRITICAL"
            | DEBUG -> "DEBUG"



/// this type describes the log structure and has methods to 
/// convert the union to and from strings 
type LogFormat = 
    | Json
    | Unstructured 
with 

    /// conversion to lower case string for use in record and 
    /// json.  This is done for easy json decoding/encoding
    member this.ToString = 
        match this with 
            | Unstructured -> "unstructured"
            | Json -> "json"

    /// although the record itself that refers to this setting 
    /// uses the string version of it, this conversion exists 
    /// to make it easier to match on for programmic reason while 
    /// creating logging methods. any invalid string will be treated
    /// as unstructured
    static member FromString (str: string) = 
        let lowerString = str.ToLower()
        match lowerString with 
            | "json" -> Json
            | "unstructured" -> Unstructured
            | _ -> Unstructured


/// represents a single log.  All logs written are first created
/// as this type and the methods on this type format the Log entry 
/// accordingly
type LogEntry = {
        //TODO: use actual DateTime here
        timestamp: string; //2021-08-08 18:04:14.721 example fmt
        level: LogLevel; 
        logger: string;
        message: string;
    }
with 

    /// creates a single log entry as record of LogEntry type
    static member Create level caller msg = 
        {
            timestamp = CreateTimeStampNow DateTime.Now TimeStampDateFormat
            level = level 
            logger = CreateCallerMethodString caller
            message = Regex.Replace(msg, """[\t|\n|\s]+""", " ") //formats the string to replace tabs/newlines/spaces with single space
        }

    /// formats the LogEntry to a json string using thoth library
    member this.ToJsonString = 
        Encode.Auto.toString this

    /// formats the LogEntry to an unstructured log string that spans one line 
    member this.ToLogString = 
        $"{this.timestamp} [{this.level}] {this.logger} '{this.message}'"

    /// print a LogEntry to the console (standard stream)
    member this.PrintToConsole fmt : unit = 
        match fmt with 
        | Unstructured -> Console.WriteLine this.ToLogString
        | Json -> Console.WriteLine this.ToJsonString

    

type LoggerSettings = {
        logDir: string;
        logFileRollingSize: int
        logFormat: LogFormat
    }
with

    static member Create (logdir: string) (rollsize: int) (fmt: string) = {
        logDir = logdir
        logFileRollingSize = rollsize
        logFormat = LogFormat.FromString fmt
    } 

    static member Default = {
        logDir = Path.Join(DirectoryInfo(".").FullName, "logs")
        logFileRollingSize = 10
        logFormat = LogFormat.Unstructured
    }


/// this will contain the stored settings derived from the settings file after 
/// initialization. before initialization (or if this doesn't occur this is set)
/// to the default record for LoggerSettings
let mutable settings = LoggerSettings.Default 

/// holds the log entries to be written. Only the Logger should ever use this
let LogEntryQueue = ConcurrentQueue<LogEntry>()


/// this is how the current log file name will look
let logFileName: string = 
    if settings.logFormat = LogFormat.Json then 
        $"{ApplicationName}.log.json"
    else 
        $"{ApplicationName}.log"

let logFilePath = Path.Join(settings.logDir, logFileName)


/// initialize the logger with the settings from the user 
let InitLogger (settingsFromConfig: LoggerSettings) = 
    // for logging purposes
    let this = MethodBase.GetCurrentMethod()
    
    // give logger the settings from the configuration file
    settings <- settingsFromConfig

    // directly add to queue here as logger is not fully initialized yet 
    let log = LogEntry.Create INFO this $"logger settings: {settings}"
    LogEntryQueue.Enqueue log
    
    // create or locate the log directory
    let dir = Directory.CreateDirectory(settings.logDir)
    
    // directly add to queue here as logger is not fully initialized yet
    let log = LogEntry.Create INFO this $"creating log directory at {dir} if it does not already exist"
    LogEntryQueue.Enqueue log



/// if true the file is at least of the size specified for roll and should be rolled
/// over using the RollLogFile method below
let IsRollSize () = 
    if File.Exists(logFilePath) then 
        int64(FileInfo(logFilePath).Length / int64(1024 * 1024)) >= settings.logFileRollingSize
    else 
        false 


/// rolls the log file such that the first entry timestamp (to seconds) is the first 
/// timestamp in the name and the second timestamp is created on roll 
let RollLogFile () = 
    if IsRollSize () then 
        let logFileToRename = logFilePath
        let firstLine = File.ReadLines(logFileToRename) |> Enumerable.First 
        let firstTimeStamp = (firstLine.Split('.')[0]).TrimEnd()
        let filename = (
            match settings.logFormat with 
                | Json -> $"{ApplicationName}{firstTimeStamp}_{(CreateTimeStampNow (DateTime.Now) FileNameDateFormat)}.log.json"
                | _ -> $"{ApplicationName}_{firstTimeStamp}_{(CreateTimeStampNow (DateTime.Now) FileNameDateFormat)}.log"
        )        
        let newLogFilePath = Path.Join(settings.logDir, filename)
        File.Move(logFileToRename, newLogFilePath)
    ()

/// write a single entry to the log file. this method will manage opening 
/// and closing the filestream
let WriteLogEntryToFile (entry: LogEntry) = 
    let logFile = File.OpenWrite(logFilePath)
    logFile.Position <- logFile.Length
    let bytes = Encoding.UTF8.GetBytes $"%s{entry.ToLogString}\n"
    logFile.Write(bytes)
    logFile.Flush() 
    logFile.Close()
    

let ProcessQueue () = 
    while (LogEntryQueue.TryPeek() |> fst) do
        match LogEntryQueue.TryDequeue() with 
            | (true, entry) -> 
                if IsRollSize () then 
                    RollLogFile ()
                entry.PrintToConsole LogFormat.Unstructured
                WriteLogEntryToFile entry 
            // TODO: Handle this better 
            | _ -> 
                ()


let WriteLog level callerMethod msg =  
    let entry = LogEntry.Create level callerMethod msg
    LogEntryQueue.Enqueue entry
    ProcessQueue ()