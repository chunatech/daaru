(*
    Tasks: 

    [_] optional fields allowed on configs 
    [_] readFromFile callback for logging/error handling in main

    considerations on optional fields [discussion needed]
    - which fields should be optional? 
    - handle optionial fields pre mvp?
    - adding 'localPacakges' member/fields. 
*)
module Configuration

open System.IO
open Thoth.Json.Net

/// record type to hold the applications base configuration. this contains information regarding 
/// which directories to watch, packages to use, browser to use, options to set and is the default 
/// setup for everything within watched directories unless otherwise specified with a directory 
/// specific configuration setup 
type BaseConfiguration = {
    scriptDirectories: string array
    maxThreadCount: int
    pollingInterval: int 
    browser: string 
    browserOptions: string array 
    browserDriverDir: string
    nugetPackages: string array
    logDirName: string;
    logDirPath: string;
    rollingSize: int;
    logFormat: string;
    loggingLevel: int;
}
with
    static member Default = {
        BaseConfiguration.scriptDirectories = [| "./scripts" |]
        maxThreadCount = 1
        pollingInterval = 5
        browser = "chrome"
        browserOptions = [||]
        browserDriverDir = "/drivers"
        nugetPackages = [||]
        logDirName = "logs"
        logDirPath = DirectoryInfo(".").FullName
        rollingSize = 10
        logFormat = "unstructured"
        loggingLevel = 1
    }

type DirectoryConfiguration = {
    pollingInterval: int 
    browser: string 
    browserOptions: string array 
    browserDriverDir: string
    nugetPackages: string array
}

type TransactionConfiguration = {
    scriptPath: string
    stagedScriptPath: string
    pollingInterval: int
    browser: string
    browserOptions: string array
    browserDriverDir: string
    nugetPackages: string array
}


module BaseConfiguration = 
    /// default configuration location information. Still subject to location/naming change at this time
    // let defaultBaseConfigurationDir = DirectoryInfo(".").FullName
    let defaultBaseConfigurationDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)
    let defaultBaseConfigurationFilePath = Path.Join(defaultBaseConfigurationDir, "settings.cweed.json")
    
    /// this decodes configuration file json to the BaseConfiguration record type. returns a Decoder which 
    /// when used with Decode.fromString and a string of json, will return a Result of either BaseConfiguration or 
    /// String Error 
    let decoder : Decoder<BaseConfiguration> = 
        Decode.object (fun get -> 
            {
                scriptDirectories = get.Required.Field "scriptDirectories" (Decode.array Decode.string)
                maxThreadCount = get.Required.Field "maxThreadCount" Decode.int
                pollingInterval = get.Required.Field "pollingInterval" Decode.int
                browser = get.Required.Field "browser" Decode.string
                browserOptions = get.Required.Field "browserOptions" (Decode.array Decode.string)
                browserDriverDir = get.Required.Field "browserDriverDir" Decode.string
                nugetPackages = get.Required.Field "nugetPackages" (Decode.array Decode.string)
                logDirName = get.Required.Field "logDirName" Decode.string
                logDirPath = get.Required.Field "logDirPath" Decode.string
                rollingSize = get.Required.Field "rollingSize" Decode.int
                logFormat = get.Required.Field "logFormat" Decode.string
                loggingLevel = get.Required.Field "loggingLevel" Decode.int
            }
        )    
    
    /// default record to use base configuration in case there is no specified configuration or the specified 
    /// is not found or improperly formatted 
    let defaultConfig: BaseConfiguration = BaseConfiguration.Default


    /// takes in a filepath to a base conf file as filepath param. at this time all fields are required 
    /// returns either the configuration read from file or default configuration defined in Default method ^
    let readFromFileOrDefault (filepath:string) =
        printfn "default base conf dir: %A" defaultBaseConfigurationDir 
        if File.Exists(filepath) then 
            (
                match File.ReadAllText(filepath) |> Decode.fromString decoder with
                    | Ok config -> config
                    | Error msg -> 
                        // TODO: this needs to be logged out at ERROR level by a handler since
                        // it is in a step pre-logger initilization
                        printfn "%s" msg
                        BaseConfiguration.Default
            )
        else 
            BaseConfiguration.Default
                