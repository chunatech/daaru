(*
    Goals of this module: 
    - Compose a Transaction from a .cwt into a canopy friendly 
      format to be executed with canopy, preserving the end users 
      settings and package additions



    [discussion needed]
    - how the pipeline up until a transaction is sent to the composer will look
    - methods for receiving new transactions 
        hook into Watcher add method directly for this
    - validation parameters of a cwt pre processing? 
        (can validate post fsx composition since fsi can validate)
    - how will the composer recieve items to queue and process? 
        - ideas for this: 
            - as a tuple (TransactionSettings, FilePath) or (TransactionSettings, FileContent)
                - if verification of permissions / authority ends up requiring 
                  the content to be opened already, maybe its better to just 
                  send the content instead of the filepath to reduce IO calls 

    ================================================================================
    Implementation

    inputs needed: 
    - .cwt contents/filepath
    - configuration items related to transaction (TransactionSettings)
    
    output expected: 
    - for each .cwt processed 
        - an .fsx file that represents a single transaction, which can be 
          run by the standalone fsi included in this package

    =================================================================================
    notes: 
    - currently working for cwts in a naive fashion. need to do further research and refactoring
    - no validation is done in this step. consider validation and whether or not it is necessary as 
      the fsi will validate fsx scripts 
    - any authorizations to add/update files to be composed is assumed valid at this point
*)
module TransactionComposer

open System.Reflection
open System.IO

open Utils
open ConfigTypes
open Configuration
open Logger


/// in order to create a properly templated #r import string we need to know what kind of 
/// package this is.  For now, defaulting to nuget. unable to locate example of local import 
/// string though it is possible to do.  needs further research and discussion. 
type PkgLocation = 
    | Nuget 
    | Internal


let mutable bConfig:  AppConfiguration = ConfigurationFromFileOrDefault (Path.Combine(DefaultConfigurationFileLocation, DefaultConfigurationFileName))
let mutable stagingDir: string = "./staging"

let Init (conf: AppConfiguration) (stagDir: string) = 
    bConfig <- conf
    stagingDir <- stagDir


/// required imports at this time
/// TODO: fix this logic because its crashing the fsi standalone
let private _defaultImports: string array = [|
    "#r \"nuget: canopy\""
    "#r \"nuget: Selenium.WebDriver.ChromeDriver\""
|]


/// array containing the default open statements in the "header" section
let private _defaultOpenStmts: string array = [|
    "open canopy.runner.classic"
    "open canopy.configuration"
    "open canopy.classic"
|]

/// this builds the #r import string. Incomplete at present as it does not cover locally sourced 
/// packages. meant to be used for custom that are used in the test by the end user
let createNugetImportString pkgname=
    $"#r \"nuget: {pkgname}\""

/// creates an array of browser options that are added to the browserOptions in selenium/canopy. at 
/// this time only chrome is supported. 
let createBrowserOptionsArray (browserOpts: string array) = 
    browserOpts |> Array.map (fun (opt: string) -> $"browserOptions.AddArgument(\"--{opt}\")") 


/// this returns the composed string that tells canopy/selenium where the chrome driver is located 
/// only chrome is supported at this time. chromium is not tested
let browserConfiguraitonString browserDir = 
    $"canopy.configuration.chromeDir <- {browserDir}"

/// this composes together all the lines that make up the browser configuration portion of the testfile 
/// and returns them as an array of strings to be further composed into a testfile
let headerBrowserConfigurations (config: TransactionConfiguration) = 
    let chromeDirConfig: string array = [| $"chromeDir <- \"{DirectoryInfo(config.browserDriverDir).FullName}\"" |]
    // browseropts 
    let browserOptsObj: string array = [| "let browserOptions: OpenQA.Selenium.Chrome.ChromeOptions = OpenQA.Selenium.Chrome.ChromeOptions()" |]
    let opts: string array = createBrowserOptionsArray config.browserOptions
    // startmode
    let startMode: string = "let browserWO: canopy.types.BrowserStartMode =  canopy.types.BrowserStartMode.ChromeWithOptions(browserOptions)"
    let startCmdString: string = "start browserWO"
    
    let startModeSettings: string array = [|
        startMode
        startCmdString
    |]

    Array.distinct (Array.concat [
        chromeDirConfig;
        browserOptsObj;
        opts;
        startModeSettings;
    ])


/// this method composes all the lines before the test name and test itself, considered "header" for the purposes 
/// of this module. returns a string array of those lines
let buildHeader (config: TransactionConfiguration) = 
       // imports 
    let importsFromConfig: string array = config.nugetPackages |> Array.map (fun pkg -> createNugetImportString pkg)
    let imports: string array = Array.distinct (Array.concat [ importsFromConfig; _defaultImports; ])
    // openstmts
    let openStmts: string array = _defaultOpenStmts
    // chromedir 
    let browserConfigs: string array = headerBrowserConfigurations config 

    Array.distinct (Array.concat [
        imports;
        openStmts;
        browserConfigs;
    ])


/// retrieves the content from the cwt file and stitches together an fsx file of the same name in the format  
/// of a canopy test, then writes the file to the configured script location
let buildTransactionFile (config: TransactionConfiguration) =
    let this: MethodBase = MethodBase.GetCurrentMethod()

    // compose all the header information using the config given
    let header: string array = buildHeader config

    // name the test after the cwt file
    let testName: string array = [| $"\"{Path.GetFileNameWithoutExtension(config.scriptPath)}\" &&& fun _ ->" |]
    
    // test content 
    let content: string array = File.ReadAllLines(config.scriptPath)

    // put together the footer here
    let runStmt: string = "run()"
    let quitStmt: string = "quit(browserWO)"
    let footer: string array = [|
        runStmt
        quitStmt
    |]
    
    // put all the pieces together and return
    // one string array to be written to file
    Array.distinct (Array.concat [
        header;
        testName;
        content;
        footer;
    ])


/// process one cwt file into one fsx file 
let ProcessCwt (config: TransactionConfiguration) = 
    let this: MethodBase = MethodBase.GetCurrentMethod()
    // assume authorization has been handled at this point 
    // if the script is a cwt, we need compose it into an fsx
    WriteLog LogLevel.DEBUG this $"method not implemented yet.. doing nothing with this value {config}"
    let testFileContent: string array = buildTransactionFile config

    let stagingDirFullPath: string = DirectoryInfo(stagingDir).FullName
    let sourcePath: string = FileInfo(config.scriptPath).FullName
    let sourceScriptDir: option<string> = (bConfig.scriptDirs
        |> List.toArray
        |> Array.filter (fun (csd: string) -> sourcePath.StartsWith(DirectoryInfo(csd).FullName))
        |> Array.tryExactlyOne)

    match sourceScriptDir with
    | Some (csdPath: string) ->
        let stagingFilePath: string = sourcePath.Replace(DirectoryInfo(csdPath).FullName,stagingDirFullPath).Replace(".cwt", ".fsx")
        let targetStagingDir: string = Path.GetDirectoryName(stagingFilePath)
        Directory.CreateDirectory targetStagingDir |> ignore
        // write the file and close it
        File.WriteAllLines(stagingFilePath, testFileContent)
        // File.Copy(sourcePath, stagingFilePath, true)
        
        WriteLog LogLevel.DEBUG this $"writing fsx script to staging location: %s{stagingFilePath}"
        Some({ config with stagedScriptPath = stagingFilePath })
    | None ->
        WriteLog LogLevel.DEBUG this "something has gone terribly wrong, you should not be here"
        None


/// copy fsx to staging dir, and update transaction config with staged path
let ProcessFsx (config: TransactionConfiguration) = 
    let this: MethodBase = MethodBase.GetCurrentMethod()

    let stagingDirFullPath: string = DirectoryInfo(stagingDir).FullName
    let sourcePath: string = FileInfo(config.scriptPath).FullName
    let sourceScriptDir: option<string> = (bConfig.scriptDirs 
        |> List.toArray
        |> Array.filter (fun (csd: string) -> sourcePath.StartsWith(DirectoryInfo(csd).FullName))
        |> Array.tryExactlyOne)

    match sourceScriptDir with
    | Some (csdPath: string) ->
        let stagingFilePath: string = sourcePath.Replace(DirectoryInfo(csdPath).FullName,stagingDirFullPath)
        let targetStagingDir: string = Path.GetDirectoryName(stagingFilePath)
        Directory.CreateDirectory targetStagingDir |> ignore
        File.Copy(sourcePath, stagingFilePath, true)
        
        WriteLog LogLevel.DEBUG this $"copying fsx script to staging location: %s{stagingFilePath}"
        Some({ config with stagedScriptPath = stagingFilePath })
    | None ->
        WriteLog LogLevel.DEBUG this "something has gone terribly wrong, you should not be here"
        None



/// this runs from the place where the transaction is received and starts the process for 
/// handing it off to the fsi
let ComposeTransaction (path: string) = 
    let this: MethodBase = MethodBase.GetCurrentMethod()

    // TODO: Build out logic to apply actual directory config, using only default right now
    //let dirConfig: AppConfiguration = bConfig
    let browserConfigs: BrowserConfiguration list = bConfig.browsers
    
    let tConfig: TransactionConfiguration = {
        scriptPath = path
        stagedScriptPath = ""  // script not staged yet
        pollingInterval = bConfig.pollingInterval
        browser = browserConfigs[0].browser
        browserOptions = browserConfigs[0].browserOpts |> List.toArray
        browserDriverDir = browserConfigs[0].driverLocation
        // TODO: either deprecate or figure out how to use this with the 
        // standalone fsi 
        nugetPackages = [||]
    }

    match Path.GetExtension(tConfig.scriptPath) with 
        | ".fsx" -> ProcessFsx tConfig
        | ".cwt" -> ProcessCwt tConfig
        | _ -> 
            let msg = $"{tConfig.scriptPath} has an unrecognized extenion.. this file will not process"
            WriteLog LogLevel.WARN this msg
            None


