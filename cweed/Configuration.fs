module Configuration

open System.IO
open Thoth.Json.Net

open ConfigurationDecoders 
open ConfigTypes

(*
    TODO: 
    [ ] handle logging in this file
*)


/// default location of the user provided configuration file
let DefaultConfigurationFileLocation = Path.Combine(System.AppContext.BaseDirectory, "/config")

/// default config file name
let DefaultConfigurationFileName = "config.json"

/// load a configuration file from a filepath. If the file does not exist, then 
/// load the default application configuration
let ConfigurationFromFileOrDefault filepath =
    // if the configfile isn't there just return the defaults 
    if (File.Exists(filepath) |> not) then 
        AppConfiguration.Default()
    else 
        let contents: string = 
            File.ReadAllTextAsync(filepath) 
            |> Async.AwaitTask 
            |> Async.RunSynchronously

        match contents |> Decode.fromString AppConfigurationDecoder with 
            | Ok config -> config
            | Error errstr -> 
                printfn $"error loading configfile %s{errstr}"
                AppConfiguration.Default()
                