module ConfigTypes
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
    static member Default () = {
        browser = "chrome"
        browserOpts = []
        driverLocation = "./drivers"
    }

/// this is the logging configuration settings and 
/// are nested within the AppConfiguration record 
type LoggingConfiguration = {
    location: string 
    rollSize: int 
    format: string 
    verbosity: int 
} with 
    static member Default () = {
        location = "./logs"
        rollSize = 10
        format = "unstructured"
        verbosity = 1
    }


/// this is directory specific configuration intended 
/// for the transaction runner to use. this configuration 
/// layers between what is given as default and any 
/// configuration that is given at the top of a file  
type DirectoryConfiguration = {
    pollingInterval: int 
    browser: string 
    browserOptions: string array 
    browserDriverDir: string
    nugetPackages: string array
}

/// this is the configuration record for a specific transaction 
/// intended for use by the transaction runner. this configuration 
/// is composed together via defaults, directory specific, and 
/// configurations provided at the top of the transaction in a config
/// tag. in order of precedence, file, directory, default is considered
/// with file being the foremost important.
type TransactionConfiguration = {
    scriptPath: string
    stagedScriptPath: string
    pollingInterval: int
    browser: string
    browserOptions: string array
    browserDriverDir: string
    nugetPackages: string array
}


/// this is the overall application configuration. Any
/// settings for cweed itself are listed here. this 
/// "parent" configuration also holds the settings 
/// for the logs and browsers configured
type AppConfiguration = {
    scriptDirs: string list 
    maxThreadCount: int 
    pollingInterval: int 
    logs: LoggingConfiguration
    browsers: BrowserConfiguration list 
} with 
    static member Default () = {
        scriptDirs = ["./scripts"]
        maxThreadCount = 4
        pollingInterval = 5
        logs = LoggingConfiguration.Default()
        browsers = [ BrowserConfiguration.Default() ]
    }
