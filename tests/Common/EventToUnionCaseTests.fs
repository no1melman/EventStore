module Melman.EventStore.Common.Tests.EventToUnionCase

open System
open NUnit.Framework

open Melman.EventStore.Common.EventToUnionCase

[<SetUp>]
let Setup () = ()
       
type InnerMostInner =
    | WhatEvs
       
type InnerEvent =
    | Something of string
    | Thing of int
    | Dur of InnerMostInner
type OuterEvent =
    | Dude of InnerEvent
    | Diff of InnerEvent

[<TestCase("WhatEvs", "InnerMostInner")>]
[<TestCase("Dude", "OuterEvent")>]
[<TestCase("Diff", "OuterEvent")>]
[<TestCase("Dur", "InnerEvent")>]
[<TestCase("Thing", "InnerEvent")>]
[<TestCase("Something", "InnerEvent")>]
let ``Given union type should return all inner types names`` typeToFind outerType =
    findType typeof<OuterEvent> typeToFind
    |> getUnionCases []
    |> List.tryLast
    |> function
        | Some a -> Assert.AreEqual(outerType, a.DeclaringType.Name)
        | _ -> Assert.Fail()
    
[<Test>]
let ``Given a search for the Something case should be able create it`` () =
    let typeToCreate = "Something"
    
    createUnionFromFullUnionTree<OuterEvent, InnerEvent> typeToCreate (Some "what")
    |> Option.get
    |> function
        | Something a -> Assert.AreEqual(a, "what")
        | _ -> Assert.Fail()
    
[<Test>]
let ``Given a search for the Whatevs case should be able create it`` () =
    let typeToCreate = "WhatEvs"
    try
        createUnionFromFullUnionTree<OuterEvent, InnerMostInner> typeToCreate None
        |> Option.get
        |> function
           | WhatEvs -> Assert.Pass()
    with 
    | :? InvalidCastException -> Assert.Fail("Unable to cast to InnerMostInner")
    
[<Test>]
let ``Given a search for the Whatevs it should construct the top most union`` () =
    let typeToCreate = "WhatEvs"
    
    createFullUnionTree<OuterEvent> typeToCreate None
    |> function
        | Some a ->
            match a with
            | Diff _ -> Assert.Pass("Passed as Diff")
            | Dude _ -> Assert.Pass("Passed as Dude")
        | None -> Assert.Fail("No items in the union list")
        
[<Test>]
let ``Given a search for the Something it should construct the top most union`` () =
    let typeToCreate = "Something"
    
    createFullUnionTree<OuterEvent> typeToCreate (Some "thing")
    |> function
        | Some a ->
            match a with
            | Diff _ -> Assert.Pass("Passed as Diff")
            | Dude _ -> Assert.Pass("Passed as Dude")
        | None -> Assert.Fail("No items in the union list")

[<Test>]
let ``Given a search for the Something event it should give back the string type`` () =
    let typeToCreate = "Something"
    
    getUnionCaseType<OuterEvent> typeToCreate
    |> function
        | Some t -> Assert.AreEqual(t.Name, "String")
        | None -> Assert.Fail("No type found")
