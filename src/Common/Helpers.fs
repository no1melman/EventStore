module Melman.EventStore.Common.Helpers

open System
open System.Text.Json

open System.Threading
open EventStore.Client


let createClient url = new EventStoreClient(EventStoreClientSettings.Create(url))


let private deserialise<'a> opts (data: ReadOnlyMemory<byte>) = JsonSerializer.Deserialize<'a>(data.Span, options = opts)
let private serialise opts data = ReadOnlyMemory(JsonSerializer.SerializeToUtf8Bytes(data, options = opts))

let createJsonEvent (options: JsonSerializerOptions) eventType data =
    let serialisedData = serialise options data
    EventData(
        Uuid.NewUuid(),
        eventType,
        serialisedData)
    
let appendEvents (client: EventStoreClient) cancellationToken streamName events = task {
    let! writeResult = client.AppendToStreamAsync(streamName, StreamState.Any, events, cancellationToken = cancellationToken)
    
    return writeResult.LogPosition.CommitPosition, writeResult.NextExpectedStreamRevision.ToUInt64()
}

let private readLoop (readResult: EventStoreClient.ReadStreamResult) = task {
    let mutable evs = []
    let! firstMove = readResult.MoveNextAsync()
    let mutable canLoop = firstMove
    
    while canLoop do
        evs <- [ yield! evs; readResult.Current ]
        let! nextMove = readResult.MoveNextAsync()
        canLoop <- nextMove
        
    return evs
}

let readAllEvents (client: EventStoreClient) cancellationToken streamName = task {
    let readResult = client.ReadStreamAsync(Direction.Forwards, streamName, StreamPosition.Start, cancellationToken = cancellationToken)
    
    return! readLoop readResult
}

let private isType eventType (resolvedEv: ResolvedEvent)  = resolvedEv.Event.EventType = eventType

let readBackToFirstEventOfType (client: EventStoreClient) cancellationToken streamName eventType = task {
    let isEventType = isType eventType
    let readResult = client.ReadStreamAsync(Direction.Backwards, streamName, StreamPosition.End, cancellationToken = cancellationToken)
    
    let! firstMove = readResult.MoveNextAsync()
    let mutable canLoop = firstMove
    let mutable isEvent = readResult.Current |> isEventType
    let mutable resolveEv = if isEvent then (Some readResult.Current) else None
    
    while not isEvent && canLoop  do
        isEvent <- readResult.Current |> isEventType
        if isEvent then resolveEv <- Some readResult.Current
        let! nextMove = readResult.MoveNextAsync()
        canLoop <- nextMove
    
    return resolveEv
}

let readEvent<'a> opts (evnt: ResolvedEvent) = deserialise<'a> opts evnt.Event.Data


let subscribe (client: EventStoreClient) ct streamName start eventAppeared subscriptionDropped =
    start
    |> Option.map (fun s -> client.SubscribeToStreamAsync(streamName, StreamPosition(s), eventAppeared, subscriptionDropped = subscriptionDropped, cancellationToken = ct))
    |> Option.defaultValue (client.SubscribeToStreamAsync(streamName, eventAppeared, subscriptionDropped = subscriptionDropped, cancellationToken = ct)) 
