open System.Globalization
open System.Text
open System.IO
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
            let argv: string[] = [| "fsi.exe"; "--noninteractive" |]
            FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream)
    with
    | (ex: exn) ->
        printfn "Error: %A" ex
        printfn "Inner: %A" ex.InnerException
        printfn "ErrorStream: %s" (errStream.ToString())
        raise ex


let getOpen (path: string) =
    let path: string = Path.GetFullPath path
    let filename: string = Path.GetFileNameWithoutExtension path
    let textInfo: TextInfo = (CultureInfo("en-US", false)).TextInfo
    textInfo.ToTitleCase filename


let getLoad (path: string) =
        let path: string = Path.GetFullPath path
        path.Replace("\\", "\\\\")


let evaluate (path: string) =
    let filename: string = getOpen path
    let load: string = getLoad path

    let _, (errs: FSharp.Compiler.Diagnostics.FSharpDiagnostic[]) = fsi.EvalInteractionNonThrowing(sprintf "#load \"%s\";;" load)
    if errs.Length > 0 then printfn "Load Errors : %A" errs

    let _, (errs: FSharp.Compiler.Diagnostics.FSharpDiagnostic[]) = fsi.EvalInteractionNonThrowing(sprintf "open %s;;" filename)
    if errs.Length > 0 then printfn "Open Errors : %A" errs

    let _, (errs: FSharp.Compiler.Diagnostics.FSharpDiagnostic[]) = fsi.EvalInteractionNonThrowing(sprintf "#quit;;")
    if errs.Length > 0 then printfn "Quit Errors : %A" errs

    // match res with
    // | Choice1Of2 (Some (f: FsiValue)) ->
    //     f.ReflectionValue :?> Transformation |> Some
    // | _ -> None
    ()

[<EntryPoint>]
let main argv =
    let fi = FileInfo(argv.[0])
    printfn "%s" fi.FullName
    evaluate fi.FullName
    0