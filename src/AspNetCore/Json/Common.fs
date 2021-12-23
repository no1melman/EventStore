module Melman.EventStore.AspNetCore.Json.Common

open System.Text.Json

let jsonOpts opts converters =
    // this line say, if there are pre-existing options, then pass them to the constructor
    // otherwise just go with the default
    let opts = 
        opts 
        |> Option.map (fun o -> JsonSerializerOptions(options = o)) 
        |> Option.defaultValue (JsonSerializerOptions())

    opts.PropertyNameCaseInsensitive <- false
    opts.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase

    // add custom converters
    converters |> List.iter (fun c -> opts.Converters.Add(c))

    // ensure that the option converter is in there
    opts.Converters.Add(OptionConverterFactory())

    opts