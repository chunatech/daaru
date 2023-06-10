/// this module holds json decoders for the configurations types
module ConfigurationDecoders 

    open System.IO
    open Thoth.Json.Net

    open ConfigTypes

    /// decodes the formatted date time from json. Caller needs to provide 
    /// a fmt string that represents the DateTime valid string fmt used to 
    /// create the encoded DateTime
    let formattedDateTimeDecoder (fmt: string) : Decoder<System.DateTime> = 
        fun path value -> 
        if Decode.Helpers.isString value then
            let v: Newtonsoft.Json.Linq.JValue = unbox value 
            let tryDate = System.DateTime.TryParseExact(
                v.ToString(),
                fmt,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None
            )
            match tryDate with 
            | (true, dt) -> Ok dt
            | (false, _) -> (path, BadField("DateTime parsing error", value)) |> Error
        else 
            (path, BadField("Datetime parsing error", value)) |> Error


    let browserConfigDecoder : Decoder<BrowserConfiguration> = 
        Decode.object (
            fun get -> 
            {
                browser = get.Required.Field "browser" Decode.string
                driverLocation = get.Required.Field "driverLocation" Decode.string
                browserOpts = 
                    get.Optional.Field "browserOpts" (Decode.list Decode.string) 
                    |> Option.defaultValue []
            }
        )


    let loggingConfigurationDecoder: Decoder<LoggingConfiguration> = 
        Decode.object (
            fun get -> 
            {
                location =
                    get.Optional.Field "location" Decode.string 
                    |> Option.defaultValue (LoggingConfiguration.Default()).location
                rollSize = 
                    get.Optional.Field "rollSize" Decode.int 
                    |> Option.defaultValue (LoggingConfiguration.Default()).rollSize
                format = 
                    get.Optional.Field "format" Decode.string 
                    |> Option.defaultValue (LoggingConfiguration.Default()).format
                verbosity = 
                    get.Optional.Field "verbosity" Decode.int 
                    |> Option.defaultValue (LoggingConfiguration.Default()).verbosity
            }
        )

    let AppConfigurationDecoder: Decoder<AppConfiguration> = 
        Decode.object (
            fun get -> 
                {
                    scriptDirs = 
                        get.Optional.Field "scriptDirectories" (Decode.list Decode.string)
                        |> Option.defaultValue ["./scripts"]
                    maxThreadCount = 
                        get.Optional.Field "maxThreadCount" Decode.int
                        |> Option.defaultValue 4
                    pollingInterval = 
                        get.Optional.Field "pollingInterval" Decode.int
                        |> Option.defaultValue 5
                    logs = 
                        get.Optional.Field "logs" loggingConfigurationDecoder
                        |> Option.defaultValue (AppConfiguration.Default().logs)
                    browsers = 
                        get.Optional.Field "browsers" (Decode.list browserConfigDecoder)
                        |> Option.defaultValue (AppConfiguration.Default().browsers)
                }
        )
