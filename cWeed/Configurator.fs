// Configurator will handle configuration for program and scripts 
// looks for and loads in config.json files. sets defaults where 
// there is no custom configuration. 

// todo: documentation 
// todo: some of these types may need to move to a different 
// module in the future. consider this 
// todo: might break into two modules to govern types and behavior 
// seperately. consider this

module Configurator

open System.IO
open FSharp.Data
open FSharp.Data.JsonExtensions

type DirPath = | DirPath of string
type BrowserOpts = | BrowserOpts of string
type PollingInterval = | PollingInterval of int // format: minutes
type NugetPackage = NugetPackage of string // format: packagename+ver

type Browser = 
    | Chrome 
    | Chromium 
    | Firefox
    | Edge
    | IE


type BaseConfig = {
    scriptDirectories: list<DirPath>
    pollingInterval: PollingInterval
    browser: Browser
    browserOptions: list<BrowserOpts>
    browserDriverDir: DirPath
    nugetPackages: list<NugetPackage>
} 

type DirConfig = {
    pollingInterval: PollingInterval
    browser: Browser
    browserOptions: list<BrowserOpts>
    browserDriverDir: DirPath
    nugetPackages: list<NugetPackage>
} 

type ScriptConfig = {
    scriptPath: DirPath
    pollingInterval: PollingInterval
    browser: Browser
    browserOptions: list<BrowserOpts>
    browserDriverDir: DirPath
    nugetPackages: list<NugetPackage>
} 


type Config = 
    | BaseConfig
    | DirConfig 
    | ScriptConfig


let defaultBaseConfigurationDir = (Path.GetDirectoryName(System.Environment.GetCommandLineArgs()[0]))


// NOTES:
// Find base/default config file
// Read config from config file into record
(*
    Base/Default Config
    {
        scriptDirectories: [ dirpath, dirpath, ... ]  // recursive
        pollingInterval: integer // in minutes
        browser: Chrome|Chromium|Firefox|Edge|IE|Safari
        browserOptions: [ browseroption, browseroption, ... ]
        browserDriverDir: dirpath
        nugetPackages: [ packagename+ver, packagename+ver, ... ]
    }

    Directory Config
    {
        pollingInterval: integer // in minutes
        browser: Chrome|Chromium|Firefox|Edge|IE|Safari
        browserOptions: [ browseroption, browseroption, ... ]
        browserDriverDir: dirpath
        nugetPackages: [ packagename+ver, packagename+ver, ... ]
    }

    Script Config
    {
        scriptPath: path
        pollingInterval: integer // in minutes
        browser: Chrome|Chromium|Firefox|Edge|IE|Safari
        browserOptions: [ browseroption, browseroption, ... ]
        browserDriverDir: dirpath
        nugetPackages: [ packagename+ver, packagename+ver, ... ]
    }
*)
