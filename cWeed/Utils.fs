module Utils

open System
open System.IO
open System.Reflection

/// formatter for DateTime obj that is specified to the milisecond 
/// for use with timestamps in LogEntry
let TimeStampDateFormat = @"yyyyMMdd_HHmmss.fff"

/// formatter for DateTime obj that is specified to the second 
/// for use with log files being rolled
let FileNameDateFormat = @"yyyyMMdd_HHmmss"

/// returns the name of the exe as a string
let ApplicationName = Assembly.GetExecutingAssembly().GetName().Name

/// this method creates a timestamp string formatted for LogEntry records
let CreateTimeStampNow (datetime: DateTime) (fmt: string) = (datetime).ToString(fmt)


/// creates a string that describes where a LogEntry is being created from 
/// format is "FileModuleName.DeclaringModuleName.MethodName". this fn covers nesting 
/// of one module deep at this time. further nested modules are not tested 
let CreateCallerMethodString (currentMethod: MethodBase) : string = 
    let name = currentMethod.Name
    let declaringType = currentMethod.DeclaringType.ToString() |> String.map  (fun x -> if x = '+' then '.' else x)
    declaringType + "." + name