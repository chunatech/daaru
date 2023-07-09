open System
open System.IO
open System.Text
open FSharp.Compiler.Interactive.Shell

// Init string builders to be used for output and error streams
let sbOut: StringBuilder = StringBuilder()
let sbErr: StringBuilder = StringBuilder()


let fsi: FsiEvaluationSession =
    let inStream: StringReader = new StringReader("")
    let outStream: StringWriter = new StringWriter(sbOut)
    let errStream: StringWriter = new StringWriter(sbErr)

    try
        // Create FSI evaluation session
        // First arg in argv below may not be evaluated,
        // however subsequent args will be.
        // https://github.com/fsharp/fsharp-compiler-docs/issues/877
        let fsiConfig: FsiEvaluationSessionHostConfig = FsiEvaluationSession.GetDefaultConfiguration()
        let argv: string[] = [| "fsi.exe"; "--noninteractive"; "--multiemit-" |]
        FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream)
    with
    | (ex: exn) ->
        printfn "Error: %A" ex
        printfn "Inner: %A" ex.InnerException
        printfn "ErrorStream: %s" (errStream.ToString())
        raise ex


let evaluate (path: string) =
    let _, (errs: FSharp.Compiler.Diagnostics.FSharpDiagnostic[]) = fsi.EvalScriptNonThrowing(path)
    if errs.Length > 0 then printfn "Script Errors : %s" (String.Join("; ", errs).Replace("\n", ", ").Replace("\r", ""))
    ()


[<EntryPoint>]
let main argv =
    let fi = FileInfo(argv.[0])
    printfn "%s" fi.FullName
    evaluate fi.FullName
    0
