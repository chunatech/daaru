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


/// create a string that represents the header portion of a csv for a given type where 
/// each property of the type is represented as a field
let createCSVHeaderStr<'T> () =
    let props = typeof<'T>.GetProperties()
    let mutable header = ""
    for prop in props do
        if ((prop = props[props.Length-1]) |> not) then 
            header <- header + $"%s{prop.Name},"
        else header <- header + $"%s{prop.Name}"
    header 


/// create a string that represents one record in a csv given a type
let createCSVRecordStr<'T> (record: 'T) =
    let props = typeof<'T>.GetProperties()
    let mutable recordStr = ""
    for prop in props do 
        // get the value of the property we're iterating through 
        let propertyValue = prop.GetValue(record)

        // match on type to decide how to format the data 
        let strValue = 
            match propertyValue with 
            | :? System.DateTime as dt -> 
                CreateTimeStampNow dt TimeStampDateFormat
            | :? string as str -> str
            | :? int as num -> $"%d{num}"
            | :? bool as b -> $"%b{b}"
            | _ -> "FORMATERROR"

        // append the record string with the formatted value and account for end of 
        // record formatting
        recordStr <- if (prop = props[props.Length-1] |> not) then recordStr + strValue + "," else recordStr + strValue 
    recordStr

/// append a record to a csv file if it exists, if the file does not exist
/// then create the file and add the header details before appending the 
/// record. Returns a result that gives a string error that represents a 
/// short message of the exn thrown to be handled by the caller
let appendCSVFile<'T> (csv: string) (record: 'T) : Result<unit, string> = 
    let fullPath = System.IO.DirectoryInfo(csv).FullName 
    let header = createCSVHeaderStr<'T> ()
    let r = createCSVRecordStr<'T> (record) 

    // if the file exists, we don't need to create the header in the file
    if (System.IO.File.Exists(csv) |> not) then
        try 
            System.IO.File.AppendAllLines(fullPath, [header; r]) |> Ok
        with exn -> 
            Error exn.Message
    else 
        try 
            System.IO.File.AppendAllLines(fullPath, [r]) |> Ok
        with exn -> 
            Error exn.Message
