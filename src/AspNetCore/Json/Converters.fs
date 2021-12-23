namespace Melman.EventStore.AspNetCore.Json

open System.Text.Json
open System.Text.Json.Serialization

type OptionConverter<'a> () =
    inherit JsonConverter<Option<'a>>()

    override this.Read(reader, typeToConvert, options) = 
      try
        let data = JsonSerializer.Deserialize(reader.ValueSpan, typeToConvert.GenericTypeArguments.[0], options)
        if not (isNull data) then Some (data :?> 'a)
        else None
      with
       | :? System.ArgumentNullException as ane -> raise ane
       | :? JsonException as je -> raise (JsonException("Error whilst converting supposed Option, see inner exn for issue", je))
       | :? System.InvalidCastException as ice -> raise (JsonException("Got an invalid cast when converting deserialised data into the Option, see inner exn for issue", ice))

    override this.Write(writer, value, options) =
        match value with
        | Some a -> JsonSerializer.Serialize(writer, a, options)
        | None -> writer.WriteNullValue()
    
type OptionConverterFactory () =
    inherit JsonConverterFactory()

    let ot = typedefof<Option<_>>
    let parameterlessCtor a =
        let jc = typedefof<OptionConverter<_>>.MakeGenericType([|a|])
        jc.GetConstructors()
        |> Array.find (fun c -> c.GetParameters().Length = 0)
    
    override this.CreateConverter (typeToConvert, options) =
        let a = typeToConvert.GenericTypeArguments.[0]
        let ctor = parameterlessCtor a
        ctor.Invoke([||]) :?> JsonConverter
        
    override this.CanConvert (typeToConvert) =
        match typeToConvert.IsGenericType with
        | false -> false
        | _ -> typeToConvert.GetGenericTypeDefinition() = ot