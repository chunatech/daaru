# Transactions 


## Example 

this is an example of a transaction file using the default `.cwt` extension. 
these are just the canopy tests without the boilerplate parts. 

cweed composes these files with a template, watches them for changes, and runs 
them in an automated way on a polling cycle. 

> Canopy provides a flexible, easy to learn api for its UI automation. See the [canopy documentation](https://lefthandedgoat.github.io/canopy/) for more information on how 
to write canopy tests. 

```fsharp 
// this is a cweed built in for results publishing
useCase "cweed" "Test cweed transactions" 0

// this is a cweed built in that runs provides the test to the template with a 
// testname and duration threshold. 0 as a threshold means that cweed will not 
// use a threshold in running this test.
cwt "testOne" 0 (fun _ -> 
    // all of this part is the body of canopy test. this example comes from 
    // the canopy documentation with some alterations for cweed builtins. 

    // goto is a cweed helper fn that does a url call and tracks the url as 
    // the target url for results publishing 
    goto "http://lefthandedgoat.github.io/canopy/testpages/"
    
    // this would parsed by the transaction runner and turned into an informational 
    // severity log. The log would go into a file in the logs directory formatted 
    // {transactionname}.log 
    printfn "[[LOG]][INFO] User generated log test."

    //assert that the element with an id of 'welcome' has
    //the text 'Welcome'
    "#welcome" == "Welcome"

    //assert that the element with an id of 'firstName' has the value 'John'
    "#firstName" == "John"

    //change the value of element with
    //an id of 'firstName' to 'Something Else'
    "#firstName" << "Something Else"

    //verify another element's value, click a button,
    //verify the element is updated
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
is replaced with either cwt by default or a moniker of your choice. To use a 
custom template name and extension configure the `testWhiteLabel` setting in `config.json`. whatever moniker is chosen is also the extension the program will 
look for when running tests.

### `requestCredentials`

```fsharp
let requestCredentials (commandArgs: string list): Net.NetworkCredential
```
this function runs the credentials request via the script passed to it through 
configuration. It reutrns a [NetworkCredential](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkcredential?view=net-6.0) class to work with. 
