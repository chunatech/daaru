#r "nuget: canopy"
#r "nuget: Selenium.WebDriver.ChromeDriver"
open canopy.runner.classic
open canopy.configuration
open canopy.classic
chromeDir <- "bin/Debug/net6.0/"
let browserOptions: OpenQA.Selenium.Chrome.ChromeOptions = OpenQA.Selenium.Chrome.ChromeOptions()
let browserWO: canopy.types.BrowserStartMode =  canopy.types.BrowserStartMode.ChromeWithOptions(browserOptions)
start browserWO
"cwtestone" &&& fun _ ->
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
run()
quit(browserWO)
