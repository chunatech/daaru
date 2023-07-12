
# Installation and Building from Source

## Installation 

<!-- TODO: set up release hyperlink -->
Installing `cweed` can be done in two ways, either by obtaining a built [release]() and extracting the release to the 
desired installation location on your system, or by building the applicaiton from source.

### Building From Source

<br />

**Requirements**
- Dotnet 6 >= 6.0.406

It is recommended to use the build scripts provided with the source code to build this application, or to review them 
before manually attempting to build this app as the build flags and directory set up for the application is included in 
the build scripts.

For both linux and windows operating systems we recommend the `build.ps1` script as the bash script isn't quite up to 
parity with regards to cli options, and the powershell version contains a fallback for not including the necessary 
arguments. However if you do not have powershell on your system, the bash script will build the application with the 
correct flags and directory structure. 

**Linux**

*(provided you have powershell installed at `/usr/bin/pwsh`)*: 
```shell
./build.ps1
```
otherwise  
```shell
pwsh build.ps1
```
**Windows**

from powershell or pwsh 
```powershell
.\build.ps1
```

from cmd
```powershell
# powershell 7+
pwsh build.ps1
```
```powershell
# powershell 5.1
powershell build.ps1
```

the finished build will be located in `/build/release/{your_os_tag}/cweed` complete with default directory structure.
Move the entire `cweed` directory from this location to wherever you intend to install cweed.  

<br />


