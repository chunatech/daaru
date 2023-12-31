# Transactions 


## Example 

this is an example of a transaction file using the default `.dt` extension. 
these are just the canopy tests without the boilerplate parts. 

daaru composes these files with a template, watches them for changes, and runs 
them in an automated way on a polling cycle. 

> Canopy provides a flexible, easy to learn api for its UI automation. See the [canopy documentation](https://lefthandedgoat.github.io/canopy/) for more information on how 
to write canopy tests. 

```fsharp 
useCase "daaru" "Test daaru transactions" 0

dt "testOne" 0 (fun _ -> 
    goto "http://lefthandedgoat.github.io/canopy/testpages/"
    printfn "[[LOG]][INFO] User generated log test."

    "#welcome" == "Welcome"

    "#firstName" == "John"

    "#firstName" << "Something Else"

    "#button_clicked" == "button not clicked"
    click "#button"
    "#button_clicked" == "button clicked"
)
```

## Transaction Config


## Helper Functions 

### `goto`

```fsharp
let goto (destinationUrl: string)
```

navigates to the url specified and sets it as the targetUrl for results processing

### `useCase`

```fsharp
let useCase (applicationName: string) (testDescription: string) (totalDurationThreshold: int64)
```

sets the application name, use case description, and total duration threshold for 
the transaction

### `appendMessage`

```fsharp
let appendMessage (text: string)
```

### `successMessageScrape`

```fsharp
let successMessageScrape (selector: string)
```

### `failureMessageScrape`

```fsharp
let failureMessageScrape (selector: string)
```

### `__WHITE_LABEL__`

```fsharp
let __WHITE_LABEL__ (name: string) (maxDurationMs: int64) (testFn: unit -> unit)
```

this is the function that sets ups the test. the `__WHITE_LABEL__` template string
is replaced with either dt by default or a moniker of your choice. To use a 
custom template name and extension configure the `testWhiteLabel` setting in `config.json`. whatever moniker is chosen is also the extension the program will 
look for when running tests.

### `requestCredentials`

```fsharp
let requestCredentials (commandArgs: string list): Net.NetworkCredential
```
this function runs the credentials request via the script passed to it through 
configuration. It reutrns a [NetworkCredential](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkcredential?view=net-6.0) class to work with. 
