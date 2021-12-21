module Melman.EventStore.Common.EventToUnionCase

open System
open System.Reflection
open Microsoft.FSharp.Reflection

let private (|IsUnionType|IsNotUnionType|) (``type``: Type) = if FSharpType.IsUnion ``type`` then IsUnionType else IsNotUnionType

type UnionCaseTree =
    | Branch of UnionCaseInfo * UnionCaseTree
    | Leaf of UnionCaseInfo
    | NotFound

let rec getUnionCases (result: UnionCaseInfo list) (uniontree: UnionCaseTree) =
    match uniontree with
    | NotFound -> result
    | Leaf u -> [yield! result; u]
    | Branch (u, t) -> getUnionCases [yield! result; u] t

let private isBranchOrLeaf = function Branch _ | Leaf _ -> true | _ -> false

let rec findType (``type``: Type) typeName =
    match ``type`` with
    | IsUnionType ->
        let unionCases = ``type`` |> FSharpType.GetUnionCases
        match (unionCases |> Array.tryFind (fun f -> f.Name = typeName)) with
        | None ->
                unionCases
                |> Array.map (fun f -> f, f.GetFields())
                |> Array.map (fun (union, fields) ->
                    fields
                    |> Array.map (fun pinfo -> findType pinfo.PropertyType typeName )
                    |> Array.tryFind isBranchOrLeaf
                    |> Option.map (fun mu -> Branch (union, mu))
                    )
                |> Array.tryFind (fun a -> a.IsSome)
                |> Option.bind id
                |> Option.defaultValue NotFound
        | _ ->
            unionCases
            |> Array.tryFind (fun f -> f.Name = typeName) // find a matching case via name
            |> Option.map Leaf
            |> Option.defaultValue NotFound
    | IsNotUnionType -> NotFound

let private invokeMethod (m: MethodInfo) (union: UnionCaseInfo) =
    function
    | Some value ->
        try
            m.Invoke(null, [| value |])
        with
        | :? TargetParameterCountException -> invalidOp $"Union case %s{union.Name}, declaring type %s{union.DeclaringType.Name}"
    | None ->
        try
            m.Invoke(null, [||])
        with
        | :? TargetParameterCountException -> invalidOp $"Union case %s{union.Name}, declaring type %s{union.DeclaringType.Name} :: NO VALUE WAS GIVEN"

let private createUnionType<'a> (typeToCreate: string) (valueOpt: obj option) (union: UnionCaseInfo) =
    let invoke m = invokeMethod m union valueOpt 
    union.DeclaringType.GetMethods()
    |> Array.filter(fun m -> m.Name.Contains(typeToCreate))
    |> Array.head
    |> invoke
    :?> 'a

let private createUnionTypeAsObj (typeToCreate: string) (valueOpt: obj option) (union: UnionCaseInfo) =
    let invoke m = invokeMethod m union valueOpt 
    union.DeclaringType.GetMethods()
    |> Array.filter(fun m -> m.Name.Contains(typeToCreate))
    |> Array.head
    |> invoke

let private createUnionTypeAsObjFromPropType (valueOpt: obj option) (union: UnionCaseInfo) =
    let invoke m = invokeMethod m union valueOpt
    union.DeclaringType.GetMethods()
    |> Array.filter(fun m -> m.Name.Contains(union.Name))
    |> Array.head
    |> invoke

// This just creates a union case from somewhere in a full tree
let getUnionCaseType<'full> typeToCreate : Type option =
    findType typeof<'full> typeToCreate
    |> getUnionCases []
    |> List.tryLast
    |> Option.bind (fun  u ->
            u.GetFields()
            |> Array.tryHead
        )
    |> Option.map (fun p -> p.PropertyType)

// This just creates a union case from somewhere in a full tree
let createUnionFromFullUnionTree<'full, 'final> typeToCreate param =
    findType typeof<'full> typeToCreate
    |> getUnionCases []
    |> List.tryLast
    |> Option.map (createUnionType<'final> typeToCreate param)

// This creates the whole the union, with the instantiated case somewhere inside
let createFullUnionTree<'a> typeToCreate (param: obj option) =
    findType typeof<'a> typeToCreate
    |> getUnionCases []
    |> List.rev
    |> function
        | head :: tail ->
            let init = createUnionTypeAsObj typeToCreate param head
            let final = tail |> List.fold (fun v -> createUnionTypeAsObjFromPropType (Some v)) init
            Some (final :?> 'a)
        | _ -> None

