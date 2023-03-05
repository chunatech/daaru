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


[/] create LoggerSettings record
    [ ] 
    [x] method ReadSettingsFromFile (string filepath)
    [x] create LoggerSettings record and populate with user 
        settings || defaults if applicable
    [ ] if user provides partial settings, compose settings 
        record that contains users selected settings with 
        defaults for the remainder. (write to file?)

[/] create Logger module
    [_] hold current LoggerSettings record 
    [_] create log file in LogDirectoryLocation
    [x] check if log directory exists in LogDirectoryLocation and 
        create if !Exists
    [x] hold LogEntryQueue
    [/] hold CurrentLogFilePath (string) 
    [_] LogEntryQueue management 
        [_] queue a log entry 
        [_] try dequeuing a log entry #figure out how to fail this..
    [_] method AppendToLogFile (CurrentLogFilePath) (LogEntry)
    [_] method RenameLogFile currentLogFile
    [_] method RollLogFile 
    [_] handle roll the logs when the log file reaches ~10MB
        [_] rename the current log file to specified fmt ^ 
        [_] create a new log file with naming convention ^

*)
module Log

open System
open System.IO
open System.Collections.Concurrent
open System.Reflection
open Thoth.Json.Net
open System.Text.RegularExpressions

/// formatter for DateTime obj that is specified to the milisecond 
/// for use with timestamps in LogEntry
let TimeStampDateFormat = @"yyyy/MM/dd HH:mm:ss.fff"

/// formatter for DateTime obj that is specified to the second 
/// for use with log files being rolled
let FileNameDateFormat = @"yyyy/MM/dd HH:mm:ss"


/// this method creates a timestamp string formatted for LogEntry records
let createTimeStampNow (datetime: DateTime) = (datetime).ToString(TimeStampDateFormat)


/// creates a string that describes where the LogEntry is being created from 
/// format is "FileModuleName.DeclaringModuleName.MethodName". this fn covers nesting 
/// of one module deep at this time. further nested modules are not tested 
let createLoggerString (currentMethod: MethodBase) : string = 
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
            timestamp = createTimeStampNow DateTime.Now
            level = level 
            logger = caller
            message = Regex.Replace(msg, """[\t|\n|\s]+""", " ")
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
            if File.Exists(filepath) then 
                match File.ReadAllText(filepath) |> Decode.fromString (LoggerSettings.decoder) with 
                    | Ok settings -> settings 
                    | Error msg -> 
                        // todo log the json was bad
                        LoggerSettings.Default
            else 
                // todo: log that file did not exist
                // create a log file for next run 
                File.WriteAllText(filepath, LoggerSettings.encoder LoggerSettings.Default)
                LoggerSettings.Default
        )



/// a queue with concurrency capability that holds LogEntry records
let LogQueue:  ConcurrentQueue<LogEntry> = ConcurrentQueue<LogEntry>()


/// directory has to be named logs
/// handles returning either the directory or creating one for log dir
/// side effects: directory creation on NOTFOUND
/// TODO: handle logging for this method
let GetLogDirectoryOrCreateIt location dirname =
    match DirectoryInfo(location).EnumerateDirectories(dirname) |> Seq.tryExactlyOne with 
        | None ->
            //printfn "did not find existing directory.. creating one" 
            let dirpath = Path.Join(location, dirname)
            
            let logMsg = $"did not find existing directory for {dirpath}.. creating one"
            let caller = MethodBase.GetCurrentMethod() |> createLoggerString
            let level = LogLevel.WARN
            let logEntry = LogEntry.Create level caller logMsg
            
            logEntry.PrintToConsole LogFormat.Unstructured

            Directory.CreateDirectory(dirpath)
        | Some dir ->
            printfn "found %A directory, setting LogDir to located directory" dir
            dir


