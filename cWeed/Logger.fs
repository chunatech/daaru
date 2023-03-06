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
[/] data LogEntryQueue (Queue)


[x] create LoggerSettings record
    [x] method ReadSettingsFromFile (string filepath)
    [x] create LoggerSettings record and populate with user 
        settings || defaults if applicable

[/] create Logger module
    [x] hold current LoggerSettings record 
    [x] check if log directory exists in LogDirectoryLocation and 
        create if !Exists using location and name specified in settings
    [ ] hold CurrentLogFilePath (string) 
    [ ] hold LogEntryQueue
    [_] LogEntryQueue management 
        [_] queue a log entry 
        [_] try dequeuing a log entry #figure out how to fail this..
    [_] method AppendToLogFile (CurrentLogFilePath) (LogEntry)
    [_] method RenameLogFile currentLogFile
    [_] method RollLogFile 
    [_] handle roll the logs when the log file reaches ~10MB
        [_] rename the current log file to specified fmt ^ 
        [_] create a new log file with naming convention ^


NON MVP Tasks: 
[ ] fix settings file to have optional fields and instead compose with defaults for whatever is not 
    present
[_] create log settings file in LogDirectoryLocation. This is up for discussion at this time and non priority

*)
module Log

open System
open System.IO
open System.Reflection
open System.Text
open System.Collections.Concurrent
open System.Text.RegularExpressions

// handles json conversions 
open Thoth.Json.Net

// general utilities 
open Utils


/// creates a string that describes where a LogEntry is being created from 
/// format is "FileModuleName.DeclaringModuleName.MethodName". this fn covers nesting 
/// of one module deep at this time. further nested modules are not tested 
let createCallerMethodString (currentMethod: MethodBase) : string = 
    let name = currentMethod.Name
    let declaringType = currentMethod.DeclaringType.ToString() |> String.map  (fun x -> if x = '+' then '.' else x)
    declaringType + "." + name


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
            logger = caller
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



/// this record contains the information from the settings file describing 
/// where the logger will store the log files, the name of the direcotry it 
/// will use/create and the size of the file it will aim for before rolling 
/// the log file over
type LoggerSettings = {
    logDirName: string;
    logDirPath: string;
    rollingSize: int;
    format: string;
}
with 

    /// these are the defaults for the logger settings
    static member Default = 
        {
            logDirName = "logs"
            logDirPath = DirectoryInfo(".").FullName
            rollingSize = 10
            format = Unstructured.ToString
        }

    /// this handles decoding the settings from json. at this time all the fields are 
    /// required but ultimatley should work such that any ommited settings are 
    /// populated with default values
    /// TODO: accept the partial settings 
    static member decoder = 
        Decode.object (fun get ->
            {
                logDirName = get.Required.Field "logDirName" Decode.string
                logDirPath = get.Required.Field "logDirPath" Decode.string
                rollingSize = get.Required.Field "rollingSize" Decode.int
                format = get.Required.Field "format" Decode.string
            }
        )

    /// encodes logger settings as json string for writing settings to file 
    static member encoder = 
        Encode.Auto.toString

    /// reads in logger settings file and if successful, returns the settings else this 
    /// method returns the default settings
    static member ReadInFromFileOrDefaults filepath = 
        (
            let callerMethod = MethodBase.GetCurrentMethod() |> createCallerMethodString 
            if File.Exists(filepath) then 
                match File.ReadAllText(filepath) |> Decode.fromString (LoggerSettings.decoder) with 
                    | Ok settings -> settings 
                    | Error msg -> 
                        // create logs
                        let l1 = LogEntry.Create ERROR callerMethod $"from Thoth.Json.Net {msg}"
                        let l2 = LogEntry.Create WARN callerMethod $"invalid json at {filepath}... using defaults"
                        
                        // TODO: queue the logs and handle them properly
                        l1.PrintToConsole Unstructured
                        l2.PrintToConsole Unstructured
                        
                        LoggerSettings.Default
            else 
                let l1 = LogEntry.Create WARN callerMethod $"file at {filepath} did not exist... using defaults"
                // TODO: queue the logs and handle them properly
                l1.PrintToConsole Unstructured
                LoggerSettings.Default
        )



let LogEntryQueue = ConcurrentQueue<LogEntry>()



/// holds the current log settings. settings are stored in the executable directory under the name 
/// settings.log.json
let GetLoggerSettings = LoggerSettings.ReadInFromFileOrDefaults (Path.Join(DirectoryInfo(".").FullName, "settings.log.json")) 


/// uses the informatino from GetLoggerSettings param to get the log directory or create it, if it does not 
/// exist already
let GetLogDirectoryOrCreateItFromSettings = 
    let location = GetLoggerSettings.logDirPath
    let dirname = GetLoggerSettings.logDirName

    let caller = createCallerMethodString (MethodBase.GetCurrentMethod())
    let logmsg = $"checking for log directory at {Path.Join(location, dirname)}. If one does not exist, it will be created at this location"
    let log = LogEntry.Create INFO caller logmsg
    log.PrintToConsole Unstructured 

    // @Chase currently not covering exceptions here as ReadInFromFileOrDefaults covers them broadly.. futher consider 
    // if coverage is needed here and how to handle it as defaults are already taken before this poing. if this 
    // method returns an exception then even the defaults would be bad. should we crash if this happens? 
    // would it ever actually happen? 
    Path.Join(location, dirname) |> Directory.CreateDirectory