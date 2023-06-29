namespace cweed

module Utils = 
    open Thoth.Json.Net
    open System.IO

    module DateTimeHandlers = 
        open System

        /// formatter for DateTime obj that is specified to the milisecond 
        /// for use with timestamps in LogEntry
        let TimeStampDateFormat = @"yyyyMMdd_HHmmss.fff"

        /// formatter for DateTime obj that is specified to the second 
        /// for use with log files being rolled
        let FileNameDateFormat = @"yyyyMMdd_HHmmss"

        /// create a formatted timestamp string
        let CreateTimeStampNow (datetime: DateTime) (fmt: string) = (datetime).ToString(fmt)


        /// decodes the formatted date time from json. Caller needs to provide 
        /// a fmt string that represents the DateTime valid string fmt used to 
        /// create the encoded DateTime
        let fmtStringDateTimeJsonDecoder (fmt: string) : Decoder<System.DateTime> = 
            fun (path: string) (value: JsonValue) -> 
            if Decode.Helpers.isString value then
                let v: Newtonsoft.Json.Linq.JValue = unbox value 
                let tryDate: bool * DateTime = System.DateTime.TryParseExact(
                    v.ToString(),
                    fmt,
                    Globalization.CultureInfo.InvariantCulture,
                    Globalization.DateTimeStyles.None
                )
                match tryDate with 
                | (true, dt: DateTime) -> Ok dt
                | (false, _) -> (path, BadField("DateTime parsing error", value)) |> Error
            else 
                (path, BadField("Datetime parsing error", value)) |> Error
  
        /// module for timestamp creation that can parse a ts from the format str
        module TimeStamp = 
            open System 
            let formatStr = @"yyyyMMdd_HHmmss.fff"
            type T = TimeStamp of string
            let value (TimeStamp ts) = ts 
            let create (dt: DateTime) : T = ((dt).ToString(formatStr)) |> TimeStamp
            let tryParse (TimeStamp ts) : Result<DateTime, string> = 

                let tryDate = DateTime.TryParseExact(
                    ts, 
                    formatStr, 
                    Globalization.CultureInfo.InvariantCulture,
                    Globalization.DateTimeStyles.None
                )

                match tryDate with 
                | (true, dt) -> dt |> Ok 
                | _ -> $"TimeStamp.tryParse() error parsing value %s{ts}" |> Error


    module CsvTools = 
        open DateTimeHandlers

        /// create a string that represents the header portion of a csv for a given type where 
        /// each property of the type is represented as a field
        let createCSVHeaderStr<'T> () =
            let props: System.Reflection.PropertyInfo array = typeof<'T>.GetProperties()
            let mutable header: string = ""
            for prop: System.Reflection.PropertyInfo in props do
                if ((prop = props[props.Length-1]) |> not) then 
                    header <- header + $"%s{prop.Name},"
                else header <- header + $"%s{prop.Name}"
            header 


        /// create a string that represents one record in a csv given a type
        let createCSVRecordStr<'T> (record: 'T) =
            let props: System.Reflection.PropertyInfo array = typeof<'T>.GetProperties()
            let mutable recordStr: string = ""
            for prop: System.Reflection.PropertyInfo in props do 
                // get the value of the property we're iterating through 
                let propertyValue = prop.GetValue(record)

                // match on type to decide how to format the data 
                let strValue: string = 
                    match propertyValue with 
                    | :? System.DateTime as (dt: System.DateTime) -> 
                        CreateTimeStampNow dt TimeStampDateFormat
                    | :? string as (str: string) -> str
                    | :? int as (num: int) -> $"%d{num}"
                    | :? bool as (b: bool) -> $"%b{b}"
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
            let fullPath: string = System.IO.DirectoryInfo(csv).FullName 
            let header: string = createCSVHeaderStr<'T> ()
            let r: string = createCSVRecordStr<'T> (record) 

            // if the file exists, we don't need to create the header in the file
            if (System.IO.File.Exists(csv) |> not) then
                try 
                    System.IO.File.AppendAllLines(fullPath, [header; r]) |> Ok
                with (exn: exn) -> 
                    Error exn.Message
            else 
                try 
                    System.IO.File.AppendAllLines(fullPath, [r]) |> Ok
                with (exn: exn) -> 
                    Error exn.Message


    module FilePathDecoder =
        let Decoder: Decoder<string> = 
            fun (path) (value: JsonValue) -> 
                let v: Newtonsoft.Json.Linq.JValue = unbox value  
                // if the filepath given is malformed, return an error 
                if not <| (System.Uri.IsWellFormedUriString(v.ToString(), System.UriKind.RelativeOrAbsolute)) then
                    Error <| (path, BadPrimitive("malformed filepath", value))

                // if the neither file nor directory specified in this path exist, return an error 
                else if (not <| File.Exists(v.ToString())) then 
                    Error <| (path, BadType("file not found", value))
                // this is should be a located, valid path
                else v.ToString() |> Ok

    module DirPathDecoder = 
        let Decoder: Decoder<string> = 
            fun (path) (value: JsonValue) -> 
                let v: Newtonsoft.Json.Linq.JValue = unbox value  
                let dirpath: string = v.ToString()
                // if the filepath given is malformed, return an error 
                if  (not <| (System.Uri.IsWellFormedUriString(dirpath, System.UriKind.RelativeOrAbsolute))) || 
                    (not <| Directory.Exists(dirpath)) 
                then
                    Error <| (path, BadType("valid relative or absolute path", value))
                // if the neither file nor directory specified in this path exist, return an error
                else dirpath |> Ok