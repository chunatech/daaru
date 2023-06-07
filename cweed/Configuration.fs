module Configuration

open System.IO
open Thoth.Json.Net

(*
    TODO: 
    [ ] handle logging in this file
*)

/// this configuration type describes the 
/// configuration for a browser that is going 
/// to be used to run transactions. It is nested 
/// into the AppConfiguration type and there can 
/// be more than one of them. at this time, however
/// only one browser, chrome, is supported. 
type BrowserConfiguration = {
    browser: string 
    browserOpts: string list 
    driverLocation: string
} with 
    static member Default () = {
        browser = "chrome"
        browserOpts = []
        driverLocation = "./drivers"
    }

/// this is the logging configuration settings and 
/// are nested within the AppConfiguration record 
type LoggingConfiguration = {
    location: string 
    rollSize: int 
    format: string 
    verbosity: int 
} with 
    static member Default () = {
        location = "/logs"
        rollSize = 10
        format = "unstructured"
        verbosity = 1
    }


/// this is directory specific configuration intended 
/// for the transaction runner to use. this configuration 
/// layers between what is given as default and any 
/// configuration that is given at the top of a file  
type DirectoryConfiguration = {
    pollingInterval: int 
    browser: string 
    browserOptions: string array 
    browserDriverDir: string
    nugetPackages: string array
}

/// this is the configuration record for a specific transaction 
/// intended for use by the transaction runner. this configuration 
/// is composed together via defaults, directory specific, and 
/// configurations provided at the top of the transaction in a config
/// tag. in order of precedence, file, directory, default is considered
/// with file being the foremost important.
type TransactionConfiguration = {
    scriptPath: string
    stagedScriptPath: string
    pollingInterval: int
    browser: string
    browserOptions: string array
    browserDriverDir: string
    nugetPackages: string array
}


/// this is the overall application configuration. Any
/// settings for cweed itself are listed here. this 
/// "parent" configuration also holds the settings 
/// for the logs and browsers configured
type AppConfiguration = {
    scriptDirs: string list 
    maxThreadCount: int 
    pollingInterval: int 
    logs: LoggingConfiguration
    browsers: BrowserConfiguration list 
} with 
    static member Default () = {
        scriptDirs = ["./scripts"]
        maxThreadCount = 4
        pollingInterval = 5
        logs = LoggingConfiguration.Default()
        browsers = [ BrowserConfiguration.Default() ]
    }


/// this module holds json decoders for the configurations types
module ConfigurationDecoders = 
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
                    |> Option.defaultValue "/logs"
                rollSize = 
                    get.Optional.Field "rollSize" Decode.int 
                    |> Option.defaultValue 10
                format = 
                    get.Optional.Field "format" Decode.string 
                    |> Option.defaultValue "unstructured"
                verbosity = 
                    get.Optional.Field "verbosity" Decode.int 
                    |> Option.defaultValue 1
            }
        )

    let AppConfigurationDecoder = 
        Decode.object (
            fun get -> 
                {
                    scriptDirs = 
                        get.Optional.Field "scriptDirectories" (Decode.list Decode.string)
                        |> Option.defaultValue ["/scripts"]
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

/// default location of the user provided configuration file
let DefaultConfigurationFileLocation = Path.Combine(System.AppContext.BaseDirectory, "/config")

/// default config file name
let DefaultConfigurationFileName = "config.json"

/// load a configuration file from a filepath. If the file does not exist, then 
/// load the default application configuration
let ConfigurationFromFile filepath =
    // if the configfile isn't there just return the defaults 
    if (File.Exists(filepath) |> not) then 
        AppConfiguration.Default()
    else 
        let contents: string = 
            File.ReadAllTextAsync(filepath) 
            |> Async.AwaitTask 
            |> Async.RunSynchronously

        match contents |> Decode.fromString ConfigurationDecoders.AppConfigurationDecoder with 
            | Ok config -> config
            | Error errstr -> 
                printfn $"error loading configfile %s{errstr}"
                AppConfiguration.Default()
                