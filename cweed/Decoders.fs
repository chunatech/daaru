/// this module holds json decoders for the configurations types
module ConfigurationDecoders 

    open System.IO
    open Thoth.Json.Net

    open ConfigTypes

    /// decodes the formatted date time from json. Caller needs to provide 
    /// a fmt string that represents the DateTime valid string fmt used to 
    /// create the encoded DateTime
    let formattedDateTimeDecoder (fmt: string) : Decoder<System.DateTime> = 
        fun path value -> 
        if Decode.Helpers.isString value then
            let v: Newtonsoft.Json.Linq.JValue = unbox value 
            let tryDate = System.DateTime.TryParseExact(
                v.ToString(),
                fmt,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None
            )
            match tryDate with 
            | (true, dt) -> Ok dt
            | (false, _) -> (path, BadField("DateTime parsing error", value)) |> Error
        else 
            (path, BadField("Datetime parsing error", value)) |> Error
