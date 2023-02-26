#r "nuget: canopy"
#r "nuget: Selenium.WebDriver.ChromeDriver"

open canopy.runner.classic
open canopy.configuration
open canopy.classic

canopy.configuration.chromeDir <- "./bin/Debug/net6.0/"

//start an instance of chrome
let chromeOptions = OpenQA.Selenium.Chrome.ChromeOptions()
chromeOptions.AddArgument("--no-sandbox")
chromeOptions.AddArgument("--incognito")
chromeOptions.AddArgument("--headless")
let chromeWO =  canopy.types.BrowserStartMode.ChromeWithOptions(chromeOptions)
start chromeWO

//this is how you define a test
"taking canopy for a spin" &&& fun _ ->
    //this is an F# function body, it's whitespace enforced

    //go to url
    url "http://lefthandedgoat.github.io/canopy/testpages/"

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

//run all tests
run()

printfn "tests completed!"

quit()