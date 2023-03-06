module Utils

open System
open System.IO


/// formatter for DateTime obj that is specified to the milisecond 
/// for use with timestamps in LogEntry
let TimeStampDateFormat = @"yyyy/MM/dd HH:mm:ss.fff"

/// formatter for DateTime obj that is specified to the second 
/// for use with log files being rolled
let FileNameDateFormat = @"yyyy/MM/dd HH:mm:ss"

/// returns the name of the exe as a string
let Applicationname = Reflection.Assembly.GetExecutingAssembly().GetName().Name

/// this method creates a timestamp string formatted for LogEntry records
let createTimeStampNow (datetime: DateTime) (fmt: string) = (datetime).ToString(fmt)
