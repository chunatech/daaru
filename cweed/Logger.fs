(* 
    MVP Tasks: 
    [x] data LogLevel (option)
    [x] data LogEntry (record)
    [x] data LoggerSettings (record)
    [x] data LogEntryQueue (Queue)


    [x] create LoggerSettings record
        [x] method ReadSettingsFromFile (string filepath)
        [x] create LoggerSettings record and populate with user 
            settings || defaults if applicable

    [x] create Logger module
        [x] Logger only Writes logs at or above specified logging level in configuration
        [x] hold current LoggerSettings record 
        [x] check if log directory exists in LogDirectoryLocation and 
            create if !Exists using location and name specified in settings
        [x] hold CurrentLogFilePath (string) 
        [x] hold LogEntryQueue
        [x] LogEntryQueue management 
            [x] queue a log entry 
            [x] try dequeuing a log entry #figure out how to fail this..
        [x] method WriteLog level callerMethod msg
        [x] method RollLogFile  
        [x] handle roll the logs when the log file reaches ~10MB
            [x] rename the current log file to specified fmt ^ 
            [x] create a new log file with naming convention ^
    
    NON MVP Tasks: 
        [ ] handling logger queue failure
        [ ] json log files are arrays of json instead of line by line entries with no commas that aren't 
            parsable in a standard way.
        [ ] logging support for zulu time
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
open ConfigTypes


/// this enum represents the level of log verbosity
/// DEBUG - developer level debugging information
/// INFO - this is an informational log and occurs whenever an event of significance happens
/// WARN - these are warnings that something has not happened as stated, but was recoverable
/// ERROR - this is anything that is caught from a result or catch block
/// CRITICAL - these are breaking errors that need attention 
type LogLevel = 
    | DEBUG = 0
    | INFO = 1
    | WARN = 2
    | ERROR = 3
    | CRITICAL = 4


/// this type describes the log structure and has methods to 
/// convert the union to and from strings 
/// 
/// LogEntry formats as follows: 
///
/// in json fmt
/// - {
///     "timestamp": "yyyyMMdd_HHmmss.fff", // example timestamp fmt 
///     "level": "INFO" // TODO: discuss log levels with chase to find out whats needed 
///     "logger": "main" // This is the entity doing the logging  
///     "message": "this is the actual log message we got"
/// }
///
/// in unstructured fmt:
/// - "yyyyMMdd_HHmmss.fff [LOGLEVEL] Module.MethodName 'logged message'"
/// - "20210808_180414.721 [INFO] Module.MethodName 'hello from logger'"
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
        loggingLevel: int
    }
with

    static member Create (logdir: string) (rollsize: int) (fmt: string) (loglevel: int) = {
        logDir = logdir
        logFileRollingSize = rollsize
        logFormat = LogFormat.FromString fmt
        loggingLevel = loglevel
    } 

    static member Default = {
        logDir = Path.Join(DirectoryInfo(".").FullName, "logs")
        logFileRollingSize = 10
        logFormat = LogFormat.Unstructured
        loggingLevel = 3
    }


/// this will contain the stored settings derived from the settings file after 
/// initialization. before initialization (or if this doesn't occur this is set)
/// to the default record for LoggerSettings
let mutable LoggerConfig = LoggingConfiguration.Default()

/// holds the log entries to be written. Only the Logger should ever use this
let LogEntryQueue = ConcurrentQueue<LogEntry>()


/// this is how the current log file name will look
let logFileName: string = 
    if LoggerConfig.format= "json" then 
        $"{ApplicationName}.log.json"
    else 
        $"{ApplicationName}.log"

let logFilePath = Path.Join(LoggerConfig.location, logFileName)

/// initialize the logger with the settings from the user 



/// if true the file is at least of the size specified for roll and should be rolled
/// over using the RollLogFile method below
let IsRollSize () = 
    if File.Exists(logFilePath) then 
        int64(FileInfo(logFilePath).Length / int64(1024 * 1024)) >= LoggerConfig.rollSize
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
            match LoggerConfig.format with 
                | "json" -> $"{ApplicationName}{firstTimeStamp}_{(CreateTimeStampNow (DateTime.Now) FileNameDateFormat)}.log.json"
                | _ -> $"{ApplicationName}_{firstTimeStamp}_{(CreateTimeStampNow (DateTime.Now) FileNameDateFormat)}.log"
        )        
        let newLogFilePath = Path.Join(LoggerConfig.location, filename)
        File.Move(logFileToRename, newLogFilePath)
    ()

/// write a single entry to the log file. this method will manage opening 
/// and closing the filestream
let WriteLogEntryToFile (entry: LogEntry) = 
    File.AppendAllLinesAsync(logFilePath, [$"%s{entry.ToLogString}"]) |> ignore


let QueueLogEntry (entry: LogEntry) = 
    LogEntryQueue.Enqueue entry


let QueueLog (level: LogLevel) (callerMethod: MethodBase) (msg: string) = 
    let entry: LogEntry = LogEntry.Create level callerMethod msg
    QueueLogEntry entry

/// runs after write log and handles processing the queue of logs
let ProcessQueue () = 
    while (LogEntryQueue.TryPeek() |> fst) do
        match LogEntryQueue.TryDequeue() with 
            | (true, entry) -> 
                if IsRollSize () then 
                    RollLogFile ()
                //entry.PrintToConsole LogFormat.Unstructured
                // printfn $"entry at Logger.ProcessQueue: %A{entry}"
                WriteLogEntryToFile entry 
            // TODO: Handle this better 
            | _ -> 
                ()


/// writes a log to the logfile specified by configuraiton and 
/// only if the level specified for the log is >= the loggingLevel
/// field in the configuration (set to Error by default) 
let WriteLog (level:LogLevel) callerMethod msg =  
    if (int level) >= (int LoggerConfig.verbosity) then
        QueueLog level callerMethod msg
    else 
        ()
    // this will process either way as the logger itself queues items
    // directly in certain scenarios
    ProcessQueue ()



/// writes a log to the logfile specified by configuraiton and 
/// only if the level specified for the log is >= the loggingLevel
/// field in the configuration (set to Error by default) 
/// prints the log to the console
let WriteLogAndPrintToConsole (level:LogLevel) callerMethod msg =  
    if (int level) >= (int LoggerConfig.verbosity) then
        let entry = LogEntry.Create level callerMethod msg
        entry.PrintToConsole LogFormat.Unstructured
        QueueLogEntry entry
    else 
        ()
    // this will process either way as the logger itself queues items
    // directly in certain scenarios
    ProcessQueue ()

let InitLogger (config: LoggingConfiguration) = 
    // for logging purposes
    let this = MethodBase.GetCurrentMethod()
    
    // give logger the settings from the configuration file
    LoggerConfig <- config

    // directly add to queue here as logger is not fully initialized yet 
    let log = LogEntry.Create LogLevel.INFO this $"logger settings: %A{LoggerConfig}"
    QueueLogEntry log

    // get full path from settings
    let logDirPath = DirectoryInfo(LoggerConfig.location).FullName

    // pass full path into create directory
    let dir = Directory.CreateDirectory(logDirPath)
    
    // directly add to queue here as logger is not fully initialized yet
    let log = LogEntry.Create LogLevel.INFO this $"creating log directory at {dir} if it does not already exist"
    QueueLogEntry log