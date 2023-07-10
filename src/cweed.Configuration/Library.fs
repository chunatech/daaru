namespace cweed


module AppConfiguration =
    open System
    open System.IO
    open Thoth.Json.Net


    /// this is the logging configuration settings and 
    /// are nested within the AppConfiguration record 
    type LoggingConfiguration = { 
        LogDirectory: string
        FileSizeLimit: int
        Severity: int 
    } with

        static member Default = { 
            LogDirectory = (Path.Join(AppContext.BaseDirectory, "logs"))
            FileSizeLimit = 10
            Severity = 1 
        }

        static member Decoder: Decoder<LoggingConfiguration> =
            Decode.object (fun (get: Decode.IGetters) ->
                { 
                    LogDirectory =
                        get.Optional.Field "logDirectory" Decode.string
                        |> Option.defaultValue LoggingConfiguration.Default.LogDirectory
                    FileSizeLimit =
                        get.Optional.Field "fileSizeLimit" Decode.int
                        |> Option.defaultValue LoggingConfiguration.Default.FileSizeLimit
                    Severity =
                        get.Optional.Field "severity" Decode.int
                        |> Option.defaultValue LoggingConfiguration.Default.Severity 
                }
            )



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
        static member Default = { 
            browser = "chrome"
            browserOpts = []
            driverLocation = (Path.Join(System.AppContext.BaseDirectory, "drivers"))
        }
        static member Decoder: Decoder<BrowserConfiguration> =
            Decode.object (fun get ->
                { 
                    browser = 
                        get.Required.Field "browser" Decode.string
                    driverLocation = 
                        get.Optional.Field "driverLocation" Decode.string
                        |> Option.defaultValue BrowserConfiguration.Default.driverLocation
                    browserOpts =
                        get.Optional.Field "browserOpts" (Decode.list Decode.string)
                        |> Option.defaultValue [] 
                }
            )


    /// user defined results processing script 
    type ResultsProcessingScriptConfiguration = {
        resultsScriptPath: string
        resultsRunnerPath: string
    } 
    with 
        static member Decoder: Decoder<ResultsProcessingScriptConfiguration> = 
            Decode.object(fun get -> 
                {
                    resultsScriptPath = get.Required.Field "resultsScriptPath" Decode.string
                    resultsRunnerPath = get.Required.Field "resultsRunnerPath" Decode.string
                }
            )


    /// user defined credentials script 
    type CredentialsRequestScriptConfiguration = {
        credScriptPath: string
        credRunnerPath: string
    } 
    with 
        static member Decoder: Decoder<CredentialsRequestScriptConfiguration> = 
            Decode.object(fun get -> 
                {
                    credScriptPath = get.Required.Field "credScriptPath" Decode.string
                    credRunnerPath = get.Required.Field "credRunnerPath" Decode.string
                }
            )
    

    type CanopyConfiguration = {
        elementTimeout: float; 
        compareTimeout: float; 
        pageTimeout: float;
    }
    with 
        static member Decoder: Decoder<CanopyConfiguration> = 
            Decode.object (
                fun get -> 
                    {
                        elementTimeout = get.Optional.Field "elementTimeout" Decode.float
                        |> Option.defaultValue 10.0 
                        compareTimeout = get.Optional.Field "compareTimeout" Decode.float
                        |> Option.defaultValue 10.0
                        pageTimeout = get.Optional.Field "pageTimeout" Decode.float
                        |> Option.defaultValue 10.0
                    }
            ) 


    /// this is the overall application configuration. Any
    /// settings for cweed itself are listed here. this
    /// "parent" configuration also holds the settings
    /// for the logs and browsers configured
    type AppConfiguration = { 
        /// list of directories that hold scripts to be run 
        scriptDirs: string list

        /// base directory of screenshots
        screenshotDirPath: string

        /// directory where results from transactions are published 
        resultsDirPath: string

        /// max threads that cweed transaction runner will use 
        /// to run transactions in parallel 
        maxThreadCount: int

        // max failure screenshots to keep
        maxScreenshots: int

        /// how frequently cweed will poll for transactions to 
        /// run 
        pollingInterval: int

        /// configuration options for the logger. see logging module for 
        /// details  
        logs: LoggingConfiguration

        /// list of browsers to use with their respective browser options and 
        /// driver locations 
        browsers: BrowserConfiguration list 

        // list of canopy configuration options
        canopyConfig: string list

        /// user defined results processing script, to take user defined actions on
        /// the results published to the _results.csv files for each transaction
        resultsProcessingScript: ResultsProcessingScriptConfiguration option

        /// user defined credentials request script for use in transactions
        /// with the default template. If this is not specified by the user, it 
        /// will be ignored with the assumption that the user isn't going to use that 
        /// functionality. However, if included, the paths for the script and runner 
        /// are both required and will be validated prior to configuration acceptance
        credentialsRequestScript: CredentialsRequestScriptConfiguration option 
    } with
        /// default implementation of the application configuration. This configuration 
        /// will be used in the event of cweed being unable to locate the users configs

        static member Default = { 
            scriptDirs = [ (Path.Join(System.AppContext.BaseDirectory, "scripts")) ]
            screenshotDirPath =  (Path.Join(System.AppContext.BaseDirectory, "screenshots")) 
            resultsDirPath = (Path.Join(System.AppContext.BaseDirectory, "results"))
            maxThreadCount = 4
            maxScreenshots = 3
            pollingInterval = 5
            logs = LoggingConfiguration.Default
            browsers = [ BrowserConfiguration.Default ]
            canopyConfig = []
            resultsProcessingScript = None
            credentialsRequestScript = None
        }

        /// decodes the json configuration from the user 
        static member Decoder: Decoder<AppConfiguration> =
            Decode.object (fun get ->
                { 
                    scriptDirs =
                        get.Optional.Field "scriptDirectories" (Decode.list Decode.string)
                        |> Option.defaultValue AppConfiguration.Default.scriptDirs
                    screenshotDirPath = 
                        get.Optional.Field "screenshotDirectory" (Decode.string)
                        |> Option.defaultValue AppConfiguration.Default.screenshotDirPath
                    resultsDirPath = 
                        get.Optional.Field "resultsDirectory" (Decode.string)
                        |> Option.defaultValue AppConfiguration.Default.resultsDirPath
                    maxThreadCount = 
                        get.Optional.Field "maxThreadCount" Decode.int |> Option.defaultValue 4
                    maxScreenshots = 
                        get.Optional.Field "maxScreenshots" Decode.int |> Option.defaultValue 3
                    pollingInterval = 
                        get.Optional.Field "pollingInterval" Decode.int |> Option.defaultValue 5
                    logs =
                        get.Optional.Field "logs" LoggingConfiguration.Decoder
                        |> Option.defaultValue AppConfiguration.Default.logs
                    browsers =
                        get.Optional.Field "browsers" (Decode.list BrowserConfiguration.Decoder)
                        |> Option.defaultValue AppConfiguration.Default.browsers
                    canopyConfig =
                        get.Optional.Field "canopyConfig" (Decode.list Decode.string)
                        |> Option.defaultValue AppConfiguration.Default.canopyConfig
                    resultsProcessingScript = 
                        get.Optional.Field "resultsProcessingScript" (ResultsProcessingScriptConfiguration.Decoder)
                    credentialsRequestScript = 
                        get.Optional.Field "credentialsRequestScript" (CredentialsRequestScriptConfiguration.Decoder)
                }
            )



    module ConfigFileHandler = 
        
        /// default name of the config file
        let configFileName = "config.json"

        /// default directory to look for config file 
        let configFileLocation = (Path.Join(System.AppContext.BaseDirectory, "config"))

        let private _decodeConfigContentsJsonOrDefault (contents: string) = 
            match contents |> Decode.fromString AppConfiguration.Decoder with 
                | Ok config -> 
                    let mutable cfg: AppConfiguration = config
                    
                    // handle severity threshold out of bounds 
                    if config.logs.Severity > 4 then 
                        let logCfg: LoggingConfiguration = { config.logs with Severity = 4 }
                        let appCfg: AppConfiguration = { config with logs = logCfg }
                        cfg <- appCfg
                    else if config.logs.Severity < 0 then 
                        let logCfg: LoggingConfiguration = { config.logs with Severity = 0 }
                        let appCfg: AppConfiguration = { config with logs = logCfg }
                        cfg <- appCfg
                    
                    // check credentials request script if exists
                    match cfg.credentialsRequestScript with 
                    // validate credential runner path 
                    | Some creds -> 
                        if (not <| File.Exists(creds.credScriptPath)) || (not <| File.Exists(creds.credRunnerPath)) then 
                            exit 1
                        else cfg
                    | None -> cfg 

                // configuration was not decoded on this branch. return the default configuration
                | Error errstr -> 
                    AppConfiguration.Default


        let readConfigFileOrDefault () = 
            let cFile = Path.Join(configFileLocation, configFileName)
            if (File.Exists(cFile) |> not) then 
                AppConfiguration.Default
            else
                try File.ReadAllText(cFile)
                with 
                    | exn -> "" 
                |> _decodeConfigContentsJsonOrDefault