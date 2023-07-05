namespace cwTransactions

/// handles the templating, building, and creation of fsx files from cwt and 
/// eventually custom extensions
module TransactionBuilder = 
    open System
    open System.IO
    open cweed.AppConfiguration
    open cwTransactions

    // instance of the app configuration being used. initialized with the 
    // default. init fn provides the user config at runtime
    let mutable private _config: AppConfiguration = cweed.AppConfiguration.AppConfiguration.Default

    /// internal staging directory. this is where cweed stages the built  
    /// scripts that are actually run by the transaction runner
    let mutable stagingDir: string = Path.Join(System.AppContext.BaseDirectory, "staging")
    
    /// this directory is where the templates are located. a default template is provided at this time. 
    let templatesDir: string = Path.Join(AppContext.BaseDirectory, "templates")

    // these are required dlls for use in the transaction file
    let libDirDLLs: FileInfo array = DirectoryInfo("libs").GetFiles("*.dll", SearchOption.AllDirectories)
    let private _defaultImports = 
        libDirDLLs
        |> Array.map (fun p -> $"#r @\"%s{p.FullName}\"")

    // array containing the default open statements in the "header" section
    let private _defaultOpenStmts: string array = [|
        "open canopy.runner.classic"
        "open canopy.configuration"
        "open canopy.classic"
    |]

    // this composes together all the lines that make up the browser configuration portion of the testfile 
    // and returns them as an array of strings to be further composed into a testfile
    let private _buildHeader (config: TransactionConfiguration) : string array = 

        // chrome configuration
        let chromeDirConfig: string array = [| $"chromeDir <- \"{DirectoryInfo(config.browserDriverDir).FullName}\"" |]
        let browserOptsObj: string array = [| "let browserOptions: OpenQA.Selenium.Chrome.ChromeOptions = OpenQA.Selenium.Chrome.ChromeOptions()" |]
        let opts: string array = 
            config.browserOptions
            |> Array.map (fun (opt: string) -> $"browserOptions.AddArgument(\"--{opt}\")") 

        // startmode
        let startMode: string = "let browserWO: canopy.types.BrowserStartMode =  canopy.types.BrowserStartMode.ChromeWithOptions(browserOptions)"
        let startCmdString: string = "start browserWO"
        
        let startModeSettings: string array = [|
            startMode
            startCmdString
        |]

        Array.distinct (Array.concat [
            _defaultImports;
            _defaultOpenStmts;
            chromeDirConfig;
            browserOptsObj;
            opts;
            startModeSettings;
        ])



    let private _buildTransactionFileContents (tConfig: TransactionConfiguration) : string array = 
        let header = _buildHeader tConfig
        let testName: string array = [| $"\"{Path.GetFileNameWithoutExtension(tConfig.scriptPath)}\" &&& fun _ ->" |]
        let testContent: string array = File.ReadAllLines(tConfig.scriptPath)
        let footer = [|
            "run()";
            "quit(browserWO)";
        |]

        // put all the pieces together and return
        // one string array to be written to file
        Array.distinct (Array.concat [
            header;
            testName;
            testContent;
            footer;
        ])

    let rec private _buildFromTemplate (tConfig: TransactionConfiguration) (template: string list) (result: string list) = 
                    match template with 
                    | [] -> result
                    | line::lines -> 
                        match line with 
                        // add the #r statements here
                        | "__DEPENDENCIES__" ->
                            _buildFromTemplate tConfig lines result @ (_defaultImports |> Array.toList)

                        | (line: string) when line.Contains("__CREDENTIAL_REQUEST_SCRIPT_RUNNER__") -> 
                            match _config.credentialsRequestScript with 
                            | Some (cfg: CredentialsRequestScriptConfiguration) -> 
                                let line' = line.Replace("__CREDENTIAL_REQUEST_SCRIPT_RUNNER__", cfg.credRunnerPath)
                                _buildFromTemplate tConfig lines result @ ([line'])
                            | None -> 
                                let line' = line.Replace("__CREDENTIAL_REQUEST_SCRIPT_RUNNER__", "")
                                _buildFromTemplate tConfig lines result @ ([line'])

                        | (line: string) when line.Contains("__CREDENTIAL_REQUEST_SCRIPT__") ->
                            match _config.credentialsRequestScript with 
                            | Some (cfg: CredentialsRequestScriptConfiguration) -> 
                                let line' = line.Replace("__CREDENTIAL_REQUEST_SCRIPT__", cfg.credScriptPath)
                                _buildFromTemplate tConfig lines result @ ([line'])
                            | None -> 
                                let line' = line.Replace("__CREDENTIAL_REQUEST_SCRIPT__", "")
                                _buildFromTemplate tConfig lines result @ [line']                                
                        
                        | (line: string) when line.Contains("__SCREENSHOT_DIR__") -> 
                            let line': string = line.Replace("__SCREENSHOT_DIR__", _config.screenshotDirPath)
                            _buildFromTemplate tConfig lines result @ ([line'])


                        | (line: string) when line.Contains("__CHROME_DRIVER_DIR__") -> 
                            let line': string = line.Replace("__CHROME_DRIVER_DIR__", tConfig.browserDriverDir)
                            _buildFromTemplate tConfig lines result @ ([line'])


                        | (line: string) when line.Contains("__BROWSER_OPTIONS__") -> 
                            let mutable opts: string list = []
                            for opt: string in (tConfig.browserOptions) do 
                                opts <- $"browserOptions.AddArgument(\"--%s{opt}\")"::opts

                            _buildFromTemplate tConfig lines result @ opts
                        
                        // TODO: add functionality for this configuration. talk to chase
                        | (line: string) when line.Contains("__TRANSACTION_CONFIG__") -> 
                            _buildFromTemplate tConfig lines result


                        | (line: string) when line.Contains("__TRANSACTION_TESTS__") -> 
                            let cwt: string list = File.ReadAllLines(tConfig.scriptPath) |> Array.toList
                            _buildFromTemplate tConfig lines result @ cwt

                        // TODO: 
                        // ADD TRANSACTION CONFIG 
                        // ADD TRANSACTION TESTS 
                        // ADD LOGGER LINES PARSING 
                        // ADD RESULTS PARSING 

                        // an oridinary line 
                        | _ -> _buildFromTemplate tConfig lines result @ [line]

    let private _processCwtFromTemplate (tConfig) : option<TransactionConfiguration> = 
        let sourcePath: string = FileInfo(tConfig.scriptPath).FullName
        let sourceDir: string = Path.GetDirectoryName(sourcePath)
        
        // create the path for the staging file
        let stagingFilePath = sourcePath.Replace(sourceDir, stagingDir).Replace(".cwt", ".fsx")
        // first read in template. currenltly only supporting default template. if this can't be read in then exit the program we 
        // wont be able to construct any scripts.
        let templateContents = 
            try File.ReadAllLines(Path.Join(templatesDir, "default.template")) 
            with exn -> 
                exit 0
        let templateContents' = templateContents
        templateContents' |> Array.Reverse

        let result = 
            _buildFromTemplate tConfig (templateContents' |> Array.toList) []

        let logPath: string = 
            stagingFilePath.Replace(@"/staging/",@"/logs/")
                            .Replace(@"\\staging\\", @"\logs\")
                            .Replace(@".fsx",@".log")

        let resultsPath: string = 
            stagingFilePath.Replace(@"/staging/",@"/results/")
                            .Replace(@"\staging\", @"\results\")
                            .Replace(@".fsx",@"_results.csv")

        for fp in [ stagingFilePath; logPath; resultsPath ] do
            // create staging directory mirror of target script
            Directory.CreateDirectory (Path.GetDirectoryName(fp)) 
            |> ignore

        // create the fsx
        File.WriteAllLines(stagingFilePath, result)

        // TODO add some config with staged filepath
        { tConfig with 
            stagedScriptPath = stagingFilePath
            logPath = logPath
            resultsPath = resultsPath
        }
        |> Some



    // set up and copy fsx files into staging. return updated transaction configuration
    let private _processFsx (tConfig: TransactionConfiguration) : option<TransactionConfiguration> = 
        let sourcePath: string = FileInfo(tConfig.scriptPath).FullName
        printfn $"sourcePath: %s{sourcePath}"
        let sourceDir: string = Path.GetDirectoryName(sourcePath)
        // create the path for the staging file
        let stagingFilePath: string = sourcePath.Replace(sourceDir, stagingDir)
        printfn $"stagingFilePath: %s{stagingFilePath}"

        // create path for transaction log file
        let logPath: string =
            stagingFilePath.Replace(@"/staging/",@"/logs/")
                            .Replace(@"\\staging\\", @"\logs\")
                            .Replace(@".fsx",@".log")
        
        // create path for transaction _results.csv file
        let resultsPath: string =
            stagingFilePath.Replace(@"/staging/",@"/results/")
                            .Replace(@"\\staging\\", @"\results\")
                            .Replace(@".fsx",@"_results.csv")

        for fp in [ stagingFilePath; logPath; resultsPath ] do
            // create staging directory mirror
            Directory.CreateDirectory (Path.GetDirectoryName(fp))
            |> ignore
        
        // copy the fsx
        File.Copy(sourcePath, stagingFilePath, true)

        // return the config
        { tConfig with
            stagedScriptPath = stagingFilePath
            logPath = logPath
            resultsPath = resultsPath

        }
        |> Some



    /// initialize the transaction processer with a copy of the app configuration
    let init (config: AppConfiguration) = 
        _config <- config


    /// process a transaction based on its extension
    let buildTransaction (path: string): option<TransactionConfiguration> = 
        let browserConfigs: BrowserConfiguration list = _config.browsers

        // build the transactionConfig here 
        let tConfig: TransactionConfiguration = {
            scriptPath = path
            stagedScriptPath = ""
            logPath = ""
            resultsPath = ""
            pollingInterval = _config.pollingInterval
            browser = browserConfigs[0].browser
            browserOptions = browserConfigs[0].browserOpts 
                |> List.toArray
            browserDriverDir = browserConfigs[0].driverLocation
            nugetPackages = [||]
        }

        // run the processing fn based on ext type
        match Path.GetExtension(tConfig.scriptPath) with 
        | ".fsx" -> _processFsx tConfig 
        | ".cwt" -> _processCwtFromTemplate tConfig
        | _ -> None