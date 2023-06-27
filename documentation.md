# Documentation 

## Table of Contents 
1. Installation 
2. App Configuraiton 
3. Transactions

## Installation 

## App Configuration 

Application configuraiton is handled via a file named `config.json`. if this file is not located, defaults are used for all of the settings so while it is not required for testing purposes, it is recommended in the case of production use.

### Location 

The default location cweed looks for this file is in the `config` folder located in the directory where the `cweed` executable is located.

Future releases may use argument passing for custom configurations, but this is not yet developed. 

### Configuration Settings File

this is an example `config.json`

```json
{
	"scriptDirectories": ["something"],
	"maxThreadCount": 5,
	"pollingInterval": 1,
	"browsers": [{
		"browser": "chrome",
		"browserOpts": ["someOpt"],
		"driverLocation": ""
	}],
	"logs": {
		"location": "",
		"rollSize": 10,
		"format": "unformatted",
		"verbosity": 0
	}
}
```

option | requirement | description 
|--|--|--|
scriptDirectories | optional | An array directories which hold transactions to be run (`.cwt` files). The user can configure multiple directories to be watched by cweed. If the user adds transactions to any of these directories cweed will monitor for changes and run those transactions according to their polling settings. Defaults to the `scripts` folder located in the cweed executable directory
maxThreadCount | optional | Handles how many threads will be used specifically for processing transactions. This does not count the actual cweed process. Defaults to `4` if not configured.
pollingInterval | optional | This setting governs how frequently transactions will be run in minutes. Defaults to `5` 
browsers | optional | This is an array representing the configurations for each browser the user intends to use with cweed. At this time only chrome is supported. see [Browser Configuration Options](#browser-configuration-options) for more details. The default value for this option is currently a default setup of chrome with the driver location being in the `drivers` folder of the cweed executable directory
logs | optional | This is the settings for the logger, such as location of the logs directory, format of the log entries, verbosity logged and size before rolling the log file. 

## Browser Configuration Options

```json
{
    "browser": "chrome",
    "browserOpts": ["someOpt"],
    "driverLocation": "/path/to/driver/directory"
}
```
<br />

Browser configuraiton objects contain the browser specific information cweed needs for running its transactions. At this time only chrome is supported, however future releases intend to support multiple browsers. 

option | requirement | description 
|--|--|--|
browser | required | browser short name 
browserOpts | optional | flags to be run with the transactions (i.e --no-sandbox). include the full flag with hyphens. 
driverLocation | required | full path of the driver for this browser. While cweed will detect out of version matches, it does not manage keeping the driver updated at this time. 

### Browser Support

at this time only chrome is supported but future releases intend to support multiple browsers. 

<br />

## Logger Configuration Options

```json
"logs": {
    "location": "/path/to/log/directory",
    "rollSize": 10,
    "format": "unformatted",
    "verbosity": 0
}
```
option | requirement | description 
|--|--|--|
location | optional | full path to the log directory. Defaults to to `logs` in the cweed executable directory 
rollSize | optional | the filesize max before rolling the log file, in MB. defaults to `10`
format | optional | the format of the logs. json and unstructured logs are supported. Default is `unstructured`
verbosity | optional | the verbosity to log to file, inclusive, as in integer starting with `0` as DEBUG and `3` as ERROR. the selected level and any above it are included. Default value is `1` 

### Logger verbosity

The cweed logger has two formats for logging, and 4 levels of verbosity (DEBUG, INFO, WARN, ERROR).

level | description 
|--|--|
DEBUG | these are developer related debugging information 
INFO | informational logging such as transactions being registered to the watcher or configurations being located. None of these logs should indicate an issue, however they may be useful in finding incorrect configurations, etc.
WARN | warnings that will not cause cweed to crash, but should be considered as they may change the output from what the user desires or expects *(i.e. user specified filepaths being set to default locations, if not found)*
ERROR | These are critical and will cause program failure

<br />

### Log formatting

**unstructured format**
```txt
timestamp provider [level] msg
```

**json format**
```json
[{
    "timestamp": "",
    "provider":"",
    "level":"",
    "msg":""
}]
```
format | description
|--|--|
json | an array of json objects representing each entry in log.json files 
unstructured | single line entries in text format, where each entry is on a new line, and each parameter of the entry is seperated via a space character.

### does cweed logger handle individual transaction logging?

transaction results are recorded, but not by cweed logger, rather they are recorded in `.csv` format in the `results` folder, in the cweed executable directory. future releases will consider adding user specified results locations but it is currently not supported. results csvs are rolled at the same rolling size as the logs. future releases will consider a configuration object dedicated to these kinds of settings for the user. 

cweed logger is pulling in unhandled logs for transactions currently. these are errors that cause the transaction not to be run at all and tend to come from the packaged fsi cweed uses. 

<!-- ## Transactions -->

