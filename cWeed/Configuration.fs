module Configuration

open System.IO
open Thoth.Json.Net

/// record type to hold the applications base configuration. this contains information regarding 
/// which directories to watch, packages to use, browser to use, options to set and is the default 
/// setup for everything within watched directories unless otherwise specified with a directory 
/// specific configuration setup 
/// TODO: Chase and Tina discuss adding 'localPacakges' member/field.
type BaseConfiguration = {
    scriptDirectories: string array 
    pollingInterval: int 
    browser: string 
    browserOptions: string array 
    browserDriverDir: string
    nugetPackages: string array
}

type DirectoryConfiguration = {
    directory: string
    pollingInterval: int 
    browser: string 
    browserOptions: string array 
    browserDriverDir: string
    nugetPackages: string array
}

type TransactionConfiguration = {
    scriptPath: string
    pollingInterval: int
    browser: string
    browserOptions: string array
    browserDriverDir: string
    nugetPackages: string array
}

type Configuration =
    | BaseConfig of BaseConfiguration
    | DirectoryConfig of DirectoryConfiguration
    | TransactionConfig of TransactionConfiguration

module BaseConfiguration = 
    /// default configuration location information. Still subject to location/naming change at this time
    let defaultBaseConfigurationDir = __SOURCE_DIRECTORY__
    let defaultBaseConfigurationFilePath = defaultBaseConfigurationDir + "/conf.json"
    
    /// this decodes configuration file json to the BaseConfiguration record type. returns a Decoder which 
    /// when used with Decode.fromString and a string of json, will return a Result of either BaseConfiguration or 
    /// String Error 
    let decoder : Decoder<BaseConfiguration> = 
        Decode.object (fun get -> 
            {
                scriptDirectories = get.Required.Field "scriptDirectories" (Decode.array Decode.string)
                pollingInterval = get.Required.Field "pollingInterval" Decode.int
                browser = get.Required.Field "browser" Decode.string
                browserOptions = get.Required.Field "browserOptions" (Decode.array Decode.string)
                browserDriverDir = get.Required.Field "browserDriverDir" Decode.string
                nugetPackages = get.Required.Field "nugetPackages" (Decode.array Decode.string)
            }
        )    
    
    /// default record to use base configuration in case there is no specified configuration or the specified 
    /// is not found or improperly formatted 
    let defaultConfig: BaseConfiguration = 
        ({
            scriptDirectories = [| "/scripts" |]
            pollingInterval = 5
            browser = "chrome"
            browserOptions = [||]
            browserDriverDir = "/drivers"
            nugetPackages = [||]
        })

    /// takes in a filepath to a base conf file as filepath param. at this time all fields are required 
    /// returns either the configuration read from file or default configuration defined in Default method ^
    let readFromFileOrDefault (filepath:string) = 
        (
            match File.ReadAllText(filepath) |> Decode.fromString decoder with 
                | Ok r -> r
                | Error err -> 
                    printfn "[Configurator.ReadInFileConf]:  %s" err
                    defaultConfig
        )