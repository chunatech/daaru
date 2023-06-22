module Configuration

open System.IO
open Thoth.Json.Net

open ConfigTypes
// open ConfigurationDecoders 
open Logger

(*
    TODO: 
    [ ] handle logging in this file
*)


/// default location of the user provided configuration file
let DefaultConfigurationFileLocation = Path.Join(System.AppContext.BaseDirectory, "config")
QueueLog (LogLevel.DEBUG) (System.Reflection.MethodBase.GetCurrentMethod()) ($"the value of DefaultConfigurationFileLocation is %s{DefaultConfigurationFileLocation}")

/// default config file name
let DefaultConfigurationFileName = "config.json"
QueueLog (LogLevel.DEBUG) (System.Reflection.MethodBase.GetCurrentMethod()) ($"the value of DefaultConfigurationFileName is %s{DefaultConfigurationFileName}")

/// load a configuration file from a filepath. If the file does not exist, then 
/// load the default application configuration
let ConfigurationFromFileOrDefault filepath =
    // if the configfile isn't there just return the defaults 
    if (File.Exists(filepath) |> not) then 
        QueueLog (LogLevel.WARN) (System.Reflection.MethodBase.GetCurrentMethod()) ($"a configuration file was not found at %s{filepath}. using defaults")
        AppConfiguration.Default()
    else 
        QueueLog (LogLevel.DEBUG) (System.Reflection.MethodBase.GetCurrentMethod()) ($"configuration file at %s{filepath} found. reading contents")
        
        let contents: string = 
            File.ReadAllTextAsync(filepath) 
            |> Async.AwaitTask 
            |> Async.RunSynchronously

        QueueLog (LogLevel.DEBUG) (System.Reflection.MethodBase.GetCurrentMethod()) "configuration file contents read. decoding contents..."
        match contents |> Decode.fromString AppConfiguration.Decoder with 
            | Ok config -> 
                QueueLog (LogLevel.DEBUG) (System.Reflection.MethodBase.GetCurrentMethod()) ($"app configuration was decoded successfully")
                config
            | Error errstr -> 
                QueueLog (LogLevel.WARN) (System.Reflection.MethodBase.GetCurrentMethod()) ($"error decoding configuration file at %s{filepath}. %s{errstr}. using defaults")
                AppConfiguration.Default()
                