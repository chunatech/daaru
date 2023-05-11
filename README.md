# cWeed

simple web ui automation and testing tool that allows templating and configuration, as well as running of tests in parallel

## Build cWeed 

linux example using `dotnet publish`, that will create a release build

```bash 
dotnet publish -c Release -r linux-x64 --self-contained true -o ./build/release/linux-x64
```

**Important:** you'll still need to create a staging and scripts directory at this time or the app will crash. From the release directory you can run the app by calling the cweed executable 


```bash 
# linux example
./cweed
```

