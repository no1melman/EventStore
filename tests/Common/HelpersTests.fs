
module Melman.EventStore.Common.Tests.HelpersTests

open System
open System.Text.Json
open EventStore.Client
open NUnit.Framework

open Melman.EventStore.Common.Helpers

[<SetUp>]
let Setup () = ()
       
   
type TempData = { Name: string }

let createDummyRecord (data: EventData) =
    let meta = dict ["type", "Jeff"; "content-type", "application/json"; "created", DateTime.UtcNow.Ticks.ToString()]
    let record = EventRecord("", Uuid.NewUuid(), StreamPosition.FromInt64(0L), Position.Start, meta, data.Data, ReadOnlyMemory([||]))
    ResolvedEvent(record, record, 0UL)

[<Test>]
let ``Given some data it should be able to serialise and deserialise it`` () =
    let opts =
        let opts = JsonSerializerOptions()
        opts.PropertyNameCaseInsensitive <- true
        opts
    
    let data = createJsonEvent opts "Jeff" { Name = "jeff" }
    
    let resEv = createDummyRecord data
    
    let result = readEvent<TempData> opts resEv
    
    Assert.That("jeff" = result.Name)