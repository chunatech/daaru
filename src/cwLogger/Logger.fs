namespace cwLogger

module Logger = 
    open System
    open System.IO
    open System.Linq
    open System.Reflection
    open System.Text.RegularExpressions
    open System.Collections.Concurrent

    /// represents the severity level of the indiviual logs being generated
    type Severity = 
        | Debug = 0 
        | Info = 1 
        | Warn = 2 
        | Error = 3 
        | Critical = 4 

    /// an individual log
    type private Log = {
        LogFile: string 
        Caller: string 
        TimeStamp: DateTime
        Severity: Severity 
        Msg: string 
    }
    with
        /// the log as a log line. formatted as `TimeStamp [Severity] Caller Msg`
        member x.ToLogString(dateFmt: string) = $"%s{(x.TimeStamp).ToString(dateFmt)} [%A{x.Severity}] %s{x.Caller} '%s{x.Msg}'"

    type private LoggingQueue = ConcurrentQueue<Log>

    /// manages logs in a queue and logs them to the files specified by the user. 
    /// instanciate with a filesize and severity threshold. overloads to create 
    /// a logger with default values are provided. 
    type Logger(fileSizeLimit: int , severityThreshold: Severity) = 
        let queue: LoggingQueue = LoggingQueue()
        let mutable fileSizeLimit: int = fileSizeLimit
        let mutable severityThreshold: Severity = severityThreshold
        let dateFmt: string = @"yyyyMMdd_HHmmss.fff"

        new () = new Logger(10, Severity.Info)

        new (filesizeLimit: int) = new Logger(filesizeLimit, Severity.Info)

        new (severityThreshold: Severity) = new Logger (10, severityThreshold)
            

        member private _.Queue 
            with get() = queue 


        member private _.DateFmt
            with get() = dateFmt
        

        /// the maximum size in `MB` allowed for a log file before the logger rolls the file
        member _.FileSizeLimit 
            with get() = fileSizeLimit
            and set(value: int) = fileSizeLimit <- value
        

        /// the minimum `Severity` level the logger will consider.
        /// 
        /// For example, if the severity is set to `Severity.Info`, all `Severity` levels greater than or equal to `Severity.Info` will be written.
        member _.SeverityThreshold
            with get() = severityThreshold
            and set(value: Severity) = severityThreshold <- value
       

        member private this.RollLogFile(logFile: string) =
            let firstLine: string = File.ReadAllLines(logFile) |> Enumerable.First
            let first_ts: string = (firstLine.Split(".")[0]).TrimEnd()
            let now_ts: string = ((DateTime.Now.ToString(this.DateFmt)).Split(".")[0]).TrimEnd()
            let file_info: FileInfo = FileInfo(logFile)
            let roll_file_name: string = (file_info.FullName).Replace($"{file_info.Name}.log", $"{file_info.Name}_%s{first_ts}_%s{now_ts}.log")
            File.Move(logFile, roll_file_name)
             

        member private this.QueueLog (filePath: string) (callingMethod: MethodBase) (severity: Severity) (msg: string) =
            let createCallerMethodString(caller: MethodBase) = 
                let name: string = caller.Name 
                let declaringType: string = caller.DeclaringType.ToString() |> String.map  (fun x -> if x = '+' then '.' else x)
                (declaringType + "." + name)
            ({
                LogFile = filePath
                Caller = callingMethod |> createCallerMethodString
                TimeStamp = DateTime.Now 
                Severity = severity
                Msg = (Regex.Replace(msg, """[\t|\n|\s]+""", " "))
            })
            |> this.Queue.Enqueue


        member private this.RollLogFileIfIsSizeLimit (logFilePath: string) = 
            let isSizeLimit: bool = File.Exists(logFilePath) && FileInfo(logFilePath).Length / int64(1024 * 1024) >= this.FileSizeLimit
            if isSizeLimit then 
                this.RollLogFile(logFilePath)

        member private this.LogToFile(log: Log) = 
            File.AppendAllLines(log.LogFile, [log.ToLogString(this.DateFmt)])


        /// process the logs stored in the queue
        member this.ProcessQueue () = 
            while this.Queue.TryPeek() |> fst do 
                let log = this.Queue.TryDequeue() |> snd
                this.LogToFile(log)
                this.RollLogFileIfIsSizeLimit(log.LogFile)
                
        /// public method for the caller to create and queue up logs to be written. The queue will 
        /// not be processed until `ProcessQueue()` is called 
        member this.Log(filePath: string) (callingMethod: MethodBase) (severity: Severity) (msg: string) = 
            if (int severity) >= (int this.SeverityThreshold) then 
                this.QueueLog filePath callingMethod severity msg
                