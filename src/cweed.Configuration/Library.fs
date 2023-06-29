namespace cweed


module AppConfiguration =
    open System.IO
    open Thoth.Json.Net
    open cweed.Logger

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
    

    /// this is the overall application configuration. Any
    /// settings for cweed itself are listed here. this
    /// "parent" configuration also holds the settings
    /// for the logs and browsers configured
    type AppConfiguration = { 
        /// list of directories that hold scripts to be run 
        scriptDirs: string list
        /// base directory of screenshots
        screenshotDirPath: string
        /// max threads that cweed transaction runner will use 
        /// to run transactions in parallel 
        maxThreadCount: int
        /// how frequently cweed will poll for transactions to 
        /// run 
        pollingInterval: int
        /// configuration options for the logger. see logging module for 
        /// details  
        logs: LoggingConfiguration
        /// list of browsers to use with their respective browser options and 
        /// driver locations 
        browsers: BrowserConfiguration list 

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
            maxThreadCount = 4
            pollingInterval = 5
            logs = LoggingConfiguration.Default
            browsers = [ BrowserConfiguration.Default ] 
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
                    maxThreadCount = 
                        get.Optional.Field "maxThreadCount" Decode.int |> Option.defaultValue 4
                    pollingInterval = 
                        get.Optional.Field "pollingInterval" Decode.int |> Option.defaultValue 5
                    logs =
                        get.Optional.Field "logs" LoggingConfiguration.Decoder
                        |> Option.defaultValue AppConfiguration.Default.logs
                    browsers =
                        get.Optional.Field "browsers" (Decode.list BrowserConfiguration.Decoder)
                        |> Option.defaultValue AppConfiguration.Default.browsers 
                    credentialsRequestScript = 
                        get.Optional.Field "credentialsRequestScript" (CredentialsRequestScriptConfiguration.Decoder)
                }
            )



    module ConfigFileHandler = 
        
        /// default name of the config file
        let configFileName = "config.json"

        /// default directory to look for config file 
        let configFileLocation = (Path.Join(System.AppContext.BaseDirectory, "config"))

        /// decode json into an AppConfiguration or in the case of an error
        /// provide the defaut configuration
        let private _decodeConfigContentsJsonOrDefault (contents: string) = 
            match contents |> Decode.fromString AppConfiguration.Decoder with 
                | Ok config -> 
                    let infoLog = LogWriter.writeLog (System.Reflection.MethodBase.GetCurrentMethod()) LogLevel.INFO
                    infoLog $"configuration decoded from successfully: {config}"
                    config
                | Error errstr -> 
                    let warnLog = LogWriter.writeLog (System.Reflection.MethodBase.GetCurrentMethod()) LogLevel.WARN 
                    warnLog errstr
                    warnLog "using default configuration"
                    // log warn here and provide the error string 
                    AppConfiguration.Default


        let readConfigFileOrDefault () = 
            let log = LogWriter.writeLog (System.Reflection.MethodBase.GetCurrentMethod())

            let cFile = Path.Join(configFileLocation, configFileName)
            log LogLevel.DEBUG $"the value of cFile is %s{cFile}"

            if (File.Exists(cFile) |> not) then 
                log LogLevel.WARN $"%s{cFile} does not exist. using default configuration"
                AppConfiguration.Default
            else
                try File.ReadAllText(cFile)
                with 
                    | exn -> 
                        log LogLevel.WARN $"error reading %s{cFile}. default configuration will be applied"
                        log LogLevel.DEBUG $"exception raised while reading %s{cFile}. %s{exn.Message}"
                        "" 
                |> _decodeConfigContentsJsonOrDefault