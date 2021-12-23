module Melman.EventStore.AspNetCore.Tests.Json.OptionConverterTests

open System.Text.Json

open Melman.EventStore.AspNetCore.Json.Common
open NUnit.Framework


[<SetUp>]
let Setup () =
    ()

type TestRecord = 
    {
        Name: string
        Age: int option
    }

[<Test>]
let ``Given an option it should serialise it`` () =
    let opts = jsonOpts None []

    let result = JsonSerializer.Serialize({ Name = "Callum"; Age = None }, opts)

    Assert.AreEqual("""{"name":"Callum","age":null}""", result)


[<Test>]
let ``Given a value in an option it should serialise it`` () =
    let opts = jsonOpts None []

    let result = JsonSerializer.Serialize({ Name = "Callum"; Age = Some 100 }, opts)

    Assert.AreEqual("""{"name":"Callum","age":100}""", result)


[<Test>]
let ``Given some json with null it should deserialise it`` () =
    let opts = jsonOpts None []

    let result = JsonSerializer.Deserialize<TestRecord>("""{"name":"Callum","age":null}""", opts)

    Assert.AreEqual({ Name = "Callum"; Age = None }, result)


[<Test>]
let ``Given some json with a value it should deserialise it`` () =
    let opts = jsonOpts None []

    let result = JsonSerializer.Deserialize<TestRecord>("""{"name":"Callum","age":100}""", opts)

    Assert.AreEqual({ Name = "Callum"; Age = Some 100 }, result)