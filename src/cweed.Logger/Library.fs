namespace cweed


module Logger = 
    open System
    open System.IO
    open System.Reflection
    open System.Collections.Concurrent
    open System.Text.RegularExpressions
    open System.Linq
    open Thoth.Json.Net
    open cweed.Utils.DateTimeHandlers
    
    
    /// creates a string that describes where a LogEntry is being created from 
    /// format is "FileModuleName.DeclaringModuleName.MethodName". this fn covers nesting 
    /// of one module deep at this time. further nested modules are not tested 
    // let CreateCallerMethodString([CallerMemberName]::caller:string) : string =
    let CreateCallerMethodString (caller: MethodBase) : string = 
        let name = caller.Name
        let declaringType = caller.DeclaringType.ToString() |> String.map  (fun x -> if x = '+' then '.' else x)
        declaringType + "." + name


    /// this is the logging configuration settings and 
    /// are nested within the AppConfiguration record 
    type LoggingConfiguration = { 
        location: string
        rollSize: int
        format: string
        verbosity: int 
    } with

        static member Default = { 
            location = (Path.Join(AppContext.BaseDirectory, "logs"))
            rollSize = 10
            format = "unstructured"
            verbosity = 1 
        }

        static member Decoder: Decoder<LoggingConfiguration> =
            Decode.object (fun (get: Decode.IGetters) ->
                { 
                    location =
                        get.Optional.Field "location" Decode.string
                        |> Option.defaultValue LoggingConfiguration.Default.location
                    rollSize =
                        get.Optional.Field "rollSize" Decode.int
                        |> Option.defaultValue LoggingConfiguration.Default.rollSize
                    format =
                        get.Optional.Field "format" Decode.string
                        |> Option.defaultValue LoggingConfiguration.Default.format
                    verbosity =
                        get.Optional.Field "verbosity" Decode.int
                        |> Option.defaultValue LoggingConfiguration.Default.verbosity 
                }
            )


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
    /// {
    ///     "timestamp": "yyyyMMdd_HHmmss.fff", // example timestamp fmt 
    ///     "level": "INFO" // TODO: discuss log levels with chase to find out whats needed 
    ///     "logger": "main" // This is the entity doing the logging  
    ///     "message": "this is the actual log message we got"
    /// }
    ///
    /// in unstructured fmt:
    /// "yyyyMMdd_HHmmss.fff [LOGLEVEL] Module.MethodName 'logged message'"
    /// "20210808_180414.721 [INFO] Module.MethodName 'hello from logger'"
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
        static member Create (level: LogLevel) (caller: MethodBase) (msg: string) = 
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
        member this.PrintToConsole (fmt: LogFormat) : unit = 
            match fmt with 
            | Unstructured -> Console.WriteLine this.ToLogString
            | Json -> Console.WriteLine this.ToJsonString

    
    type LogEntryQueue = ConcurrentQueue<LogEntry>


    module LogWriter = 
        let mutable private _config: LoggingConfiguration = LoggingConfiguration.Default

        let mutable private _logFileName: string = "cweed.log"

        let mutable private _logFileLocation: string = _config.location


        let private _queue: LogEntryQueue = LogEntryQueue()


        let private _createRollFileName (filepath: string) : string =  
            let firstLine: string = File.ReadAllLines(filepath) |> Enumerable.First
            let firstTimeStamp: string = (firstLine.Split('.')[0]).TrimEnd()
            let rName: string = $"cweed_%s{firstTimeStamp}_%s{(CreateTimeStampNow (DateTime.Now) (FileNameDateFormat))}.log"
            
            match _config.format with 
            | "json" -> rName + ".json"
            | _ -> rName 
            

        let private _rollLogFile () = 
            let lfilepath: string = Path.Join(_logFileLocation, _logFileName)

            // check file size against roll size from user config
            if int64(FileInfo(lfilepath).Length / int64(1024 * 1024)) >= _config.rollSize then 
                let newLogFilePath: string = Path.Combine(
                    Path.GetDirectoryName(lfilepath),
                    (lfilepath |> _createRollFileName)
                )
                File.Move(lfilepath, newLogFilePath)

        let private _processLogEntryQueue  () = 
            while (_queue.TryPeek() |> fst) do 
                match _queue.TryDequeue() with 
                | (true, (entry: LogEntry)) -> 

                    File.AppendAllLinesAsync((Path.Join(_logFileLocation, _logFileName)), [$"%s{entry.ToLogString}"])
                    |> Async.AwaitTask
                    // TODO: if there's an exception its being ignored. handle this better
                    |> ignore

                    _rollLogFile ()
                | _ -> ()

        
        let queueLog (callerMethod: MethodBase) (level: LogLevel) (msg: string) = 
            if (int level) >= (int _config.verbosity) then 
                let entry: LogEntry = LogEntry.Create level callerMethod msg
                entry |> _queue.Enqueue
            ()

        let writeLog (callerMethod: MethodBase) (level: LogLevel) (msg: string) = 
            if (int level) >= (int _config.verbosity) then 
                queueLog callerMethod level  msg 
            _processLogEntryQueue ()

        let writeLogAndPrintToConsole (callerMethod: MethodBase)  (level: LogLevel) (msg: string) = 
            if (int level) >= (int _config.verbosity) then
                let entry: LogEntry = LogEntry.Create level callerMethod msg 
                entry |> _queue.Enqueue
                printfn $"%s{entry.ToLogString}"
            ()

        /// set up logging configuration and initialize the queue
        let init (config: LoggingConfiguration) = 
            let queueInitLogs = queueLog (MethodBase.GetCurrentMethod()) LogLevel.INFO 

            _config <- config
            _logFileLocation <-config.location
            _logFileName <- if config.format = "json" then _logFileName + ".json" else _logFileName

            queueInitLogs "logger configuration initialized"

            queueInitLogs $"creating log directory at %s{DirectoryInfo(_config.location).FullName} if it does not exist"

            // make sure the log directory exists
            Directory.CreateDirectory(DirectoryInfo(_config.location).FullName) 
            |> ignore

            // make sure the log file exists here
            if File.Exists(Path.Join(_logFileLocation, _logFileName)) |> not then 
                File.Create(Path.Join(_logFileLocation, _logFileName)) |> ignore

            _processLogEntryQueue()
            ()


(*
    Logger 
    [x] add logwriting methods 
    [ ] start implementing logger in config and main 
    [ ] test logger 
*)