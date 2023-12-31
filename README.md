# Daaru

## Description

Daaru is a browser automation and testing tool, with a focus on ease of use and extensibility. It functions as a wrapper around canopy that includes file watching, results publishing and logging. 

Daaru is built using:
- [Dotnet](https://dotnet.microsoft.com/en-us/download/dotnet)
- [FSharp](https://fsharp.org/) 
- [Canopy](https://github.com/lefthandedgoat/canopy)
- [Selenium](https://www.selenium.dev/) 
- [Thoth](https://thoth-org.github.io/Thoth.Json/)

 ## Getting Started 

 ### Requirements 

 - [chrome driver](https://chromedriver.storage.googleapis.com/index.html) that is compatible with the version of chrome being used 
 - [chrome browser](https://www.google.com/chrome/)

 ### Installation

<!-- TODO: Link to latest release here -->
1. Obtain a [release](https://github.com/chunatech/daaru/releases/latest) for your operating system
2. Extract the release into the directory where you would like Daaru to be 
installed
<!-- TODO: Link to Configuration page -->
3. Obtain a chrome driver that is compatible with the version of chrome being used and 
either extract it into the `drivers` directory of the application, or upon configuration, 
specify its location in the config file.
4. Configure Daaru if desired by adding a `config.json` file to the `config`
directory in your installation. see [configuration]() for more details
*alternately, you can build Daaru from source, by using either the `build.ps1` or 
`build.sh` files in the root of the project. Obtain the source code from [releases](),
or build from the master branch if desired.*

## Attributions 

For the dlls that are distributed with Daaru, copies of their licenses are included 
in the same directory as the dll. Links to these licenses are also included 
below, as well as Thoth json, since we used the Thoth.Json.Net library in our project 
for decoding/encoding.  

* [canopy](https://github.com/lefthandedgoat/canopy/blob/master/LICENSE.md)
* [Newtonsoft Json](https://github.com/JamesNK/Newtonsoft.Json/blob/master/LICENSE.md) 
* [Selenium Web Driver](https://github.com/SeleniumHQ/selenium/blob/trunk/LICENSE)
* [Thoth Json](https://github.com/thoth-org/Thoth.Json.Net/blob/main/LICENSE.md)


## Daaru's License

MIT License

Copyright (c) 2023 Chase Colvin, Tina Colvin

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.