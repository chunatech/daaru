namespace cweed

module Utils = 
    open Thoth.Json.Net
    open System
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
            let formatStr: string = @"yyyyMMdd_HHmmss.fff"
            type T = TimeStamp of string
            let value (TimeStamp (ts: string)) = ts 
            let create (dt: DateTime) : T = ((dt).ToString(formatStr)) |> TimeStamp
            let tryParse (TimeStamp (ts: string)) : Result<DateTime, string> = 

                let tryDate: bool * DateTime = DateTime.TryParseExact(
                    ts, 
                    formatStr, 
                    Globalization.CultureInfo.InvariantCulture,
                    Globalization.DateTimeStyles.None
                )

                match tryDate with 
                | (true, dt: DateTime) -> dt |> Ok 
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
        let appendRecordToCSVFile<'T> (csv: string) (record: 'T) : Result<unit, string> = 
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

        let appendStringToCSVFile (filepath: string) (header: string) (line: string) =
            let fullPath: string = FileInfo(filepath).FullName

            if (not <| File.Exists(fullPath)) then
                try
                    File.AppendAllLines(fullPath, [header])
                with (exn: exn) ->
                    printfn "%A" exn.Message
                    //TODO: Write error to log
            
            try
                File.AppendAllLines(fullPath, [line])
            with (exn: exn) ->
                printfn "%A" exn.Message
                //TODO: Write error to log


    module FileTools =
        /// takes in a file path and a specified size in megabytes.  if the target file is over the
        /// specified size, the file is renamed to have a timestamp appended.  file extension is
        /// maintained.
        let RollFileBySize (filepath: string) (sizeLimitMB: int64) =
            if File.Exists(filepath) then
                if FileInfo(filepath).Length >= sizeLimitMB * int64(1024*1024) then
                    let dtString: string = DateTime.Now.ToString("yyyyMMdd_hhmmssfff")
                    let fileExt: string = FileInfo(filepath).Extension
                    let newName: string = 
                        if filepath.Contains('.') then
                            filepath.Replace($"%s{fileExt}", $"_%s{dtString}%s{fileExt}")
                        else
                            $"filepath_%s{dtString}"
                    File.Move(filepath, newName)
        
        /// takes in a file path, a specified size in megabytes, a count of lines to leave behind in
        /// the original file, and a bool indicating whether the file has a header or not.  if the
        /// target file is over the specified size, all of the file's content, except specified number
        /// of lines to leave behind.  If header is true, then a copy of the first line of the file
        /// will also be left behind, at the top of the file.  all other content is written to a new
        /// file with the same name as the original file, but with a timestamp appended to it. file
        /// extension is maintained. 
        let PartialRollFileBySize (filepath: string) (sizeLimitMB: int64) (leaveLines: int) (header: bool) =
            if File.Exists(filepath) then
                if FileInfo(filepath).Length >= sizeLimitMB * int64(1024*1024) then
                    let dtString: string = DateTime.Now.ToString("yyyyMMdd_hhmmssff")
                    let fileExt: string = FileInfo(filepath).Extension
                    let newName: string = 
                        if filepath.Contains('.') then
                            filepath.Replace($"%s{fileExt}", $"_%s{dtString}%s{fileExt}")
                        else
                            $"filepath_%s{dtString}"
                    let fileContent: string array = File.ReadAllLines(filepath)
                    let rolledFileContent: string array = fileContent[0..(fileContent.Length - leaveLines - 1)]
                    let mutable remainingFileContent: string array = fileContent[(fileContent.Length - leaveLines)..(fileContent.Length-1)]
                    
                    if header then
                        remainingFileContent <- Array.append [|fileContent[0]|] remainingFileContent
                    
                    File.WriteAllLines(newName, rolledFileContent)
                    File.WriteAllLines(filepath, remainingFileContent)


    module FilePathDecoder =
        let Decoder: Decoder<string> = 
            fun (path: string) (value: JsonValue) -> 
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
            fun (path: string) (value: JsonValue) -> 
                let v: Newtonsoft.Json.Linq.JValue = unbox value  
                let dirpath: string = v.ToString()
                // if the filepath given is malformed, return an error 
                if  (not <| (System.Uri.IsWellFormedUriString(dirpath, System.UriKind.RelativeOrAbsolute))) || 
                    (not <| Directory.Exists(dirpath)) 
                then
                    Error <| (path, BadType("valid relative or absolute path", value))
                // if the neither file nor directory specified in this path exist, return an error
                else dirpath |> Ok