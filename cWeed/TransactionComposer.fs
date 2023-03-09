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
open Configuration
open Logger


/// in order to create a properly templated #r import string we need to know what kind of 
/// package this is.  For now, defaulting to nuget. unable to locate example of local import 
/// string though it is possible to do.  needs further research and discussion. 
type PkgLocation = 
    | Nuget 
    | Internal


/// required imports at this time
let _defaultImports = [|
    "#r \"nuget: canopy\""
    "#r \"nuget: Selenium.WebDriver.ChromeDriver\""
|]


/// array containing the default open statements in the "header" section
let _defaultOpenStmts = [|
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
    browserOpts |> Array.map (fun opt -> $"browserOptions.AddArgument(\"--{opt}\")") 


/// this returns the composed string that tells canopy/selenium where the chrome driver is located 
/// only chrome is supported at this time. chromium is not tested
let browserConfiguraitonString browserDir = 
    $"canopy.configuration.chromeDir <- {browserDir}"

/// this composes together all the lines that make up the browser configuration portion of the testfile 
/// and returns them as an array of strings to be further composed into a testfile
let headerBrowserConfigurations config = 
    let chromeDirConfig = [| $"chromeDir <- \"{DirectoryInfo(config.browserDriverDir).FullName}\"" |]
    // browseropts 
    let browserOptsObj = [| "let browserOptions: OpenQA.Selenium.Chrome.ChromeOptions = OpenQA.Selenium.Chrome.ChromeOptions()" |]
    let opts = createBrowserOptionsArray config.browserOptions
    // startmode
    let startMode = "let browserWO: canopy.types.BrowserStartMode =  canopy.types.BrowserStartMode.ChromeWithOptions(browserOptions)"
    let startCmdString = "start browserWO"
    
    let startModeSettings = [|
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
let buildHeader config = 
       // imports 
    let importsFromConfig: string array = config.nugetPackages |> Array.map (fun pkg -> createNugetImportString pkg)
    let imports: string array = Array.distinct (Array.concat [ importsFromConfig; _defaultImports; ])
    // openstmts
    let openStmts: string array = _defaultOpenStmts
    // chromedir 
    let browserConfigs = headerBrowserConfigurations config 

    Array.distinct (Array.concat [
        imports;
        openStmts;
        browserConfigs;
    ])


// @tina, this function below needs to be adjusted to write the file to a 
// staging dir, instead of the script dir.

/// retrieves the content from the cwt file and stitches together an fsx file of the same name in the format  
/// of a canopy test, then writes the file to the configured script location
let buildTestFile (config: TransactionConfiguration) = 
    // compose all the header information using the config given
    let header = buildHeader config

    // name the test after the cwt file
    let testName = [| $"\"{Path.GetFileNameWithoutExtension(config.scriptPath)}\" &&& fun _ ->" |]
    
    // test content 
    let content = File.ReadAllLines(config.scriptPath)

    // put together the footer here
    let runStmt = "run()"
    let quitStmt = "quit(browserWO)"
    let footer = [|
        runStmt
        quitStmt
    |]
    
    // put all the pieces together into one string array to be written to file
    let testFileContent = Array.distinct (Array.concat [
        header;
        testName;
        content;
        footer;
    ])

    // set up the file name to be the same as the cwt given
    let dir = Path.GetDirectoryName(config.scriptPath)
    let testFileName = $"{Path.GetFileNameWithoutExtension(config.scriptPath)}.fsx" 
    
    // write the file and close it
    File.WriteAllLines(Path.Join(dir, testFileName), testFileContent)
    

/// process one cwt file into one fsx file 
let ProcessCwt config = 
    let this = MethodBase.GetCurrentMethod()
    // assume authorization has been handled at this point 
    // if the script is a cwt, we need compose it into an fsx
    WriteLog LogLevel.DEBUG this $"method not implemented yet.. doing nothing with this value {config}"
    buildTestFile config
    ()


// @tina, this function below needs to copy the .fsx file to the staging dir

/// validate fsx file with fsi and pass it along 
let ProcessFsx config = 
    let this = MethodBase.GetCurrentMethod()
    
    // assume authorization has been handled at this point 
    // if the script is an fsx, we can start the process from the fsi fwd 
    WriteLog LogLevel.DEBUG this $"method not implemented yet.. doing nothing with this value {config}"
    ()



/// this runs from the place where the transaction is received and starts the process for 
/// handing it off to the fsi
let ComposeTransaction (config: TransactionConfiguration) = 
    let this = MethodBase.GetCurrentMethod()
    if (Path.HasExtension(config.scriptPath) |> not) then 
        WriteLog LogLevel.WARN this $"{config.scriptPath} has no extension.. this file will not process"
    else 
        match Path.GetExtension(config.scriptPath) with 
            | ".fsx" -> ProcessFsx config 
            | ".cwt" -> ProcessCwt config 
            | _ -> 
                let msg = $"{config.scriptPath} has an unrecognized extenion.. this file will not process"
                WriteLog LogLevel.WARN this msg


