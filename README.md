[![CircleCI](https://circleci.com/gh/no1melman/EventStore/tree/main.svg?style=shield)](https://circleci.com/gh/no1melman/EventStore/tree/main)

![Nuget](https://img.shields.io/nuget/v/Melman.EventStore.Common?label=nuget%3A%20EventStore.Common&?style=for-the-badge)<br />
![Nuget](https://img.shields.io/nuget/v/Melman.EventStore.AspNetCore?label=nuget%3A%20EventStore.AspNetCore&?style=for-the-badge)

# EventStore FSharp Helpers


## Melman.EventStore.Common

Install

```bash
dotnet package add Melman.EventStore.Common
```

### Usage:

Generally it's going to start with creating a client:

```fsharp

// typical client creation
let eventStoreClient = Helpers.createClient "esdb://localhost:2120?tls=false"

// in a aspnet world, I'll generally put a wrapper around it

type IEventStoreFactory =
  abstract CreateClient : unit -> EventStoreClient

type EventStoreFactory(config: IConfiguration) =
  interface IEventStoreFactory with
    member _.CreateClient () = Helpers.createClient (config.GetValue<string>("EventStoreConnectionString"))
```

Creating an event:

```fsharp
// with fsharp you're probably going to be creating a few custom types which need
// conversion to json, hence why I have a param for JsonSerializerOptions

let opts =
  let opts = JsonSerializerOptions()
  opts.Converters.Add(MyCustomConverter())
  opts

let eventData = Helpers.createJsonEvent opts "UserAdded" {| UserName = "Callum" |}
```

Publishing events:

```fsharp
// client that we created earlier
do! Helpers.appendEvents client cancellationToken "User-012324" [ eventData ] // -- Task
```

Read all events:

```fsharp
let! events = Helpers.readAllEvents client cancellationToken "User-012324" // -- Task<ResolvedEvent list>
```

Deserialise a ResolvedEvent (aka, once you've done the above):

```fsharp
let userAdded = Helpers.readEvent<UserAdded> opts event // -- UserAdded

let eventType = typedefof<UserAdded>

let objThatIsUserAdded = Helpers.readEventWithType opts event eventType
```

This final one is sort of my way of finding the last event of a type, say if I were pumping price/hr of crypto 
and wanted the last event pushed into the stream:

```fsharp
let! maybeEvent = Helpers.readBackToFirstEventOfType client cancellationToken "BTC" "SpotPriceAdded" // Task<ResolvedEvent option>
```

### Design

These are all designed with the most rigid values at the beginning of the functions to optimise partial application:


```fsharp

let createEvent = Helpers.createJsonEvent opts

let createUserAddedEvent = createEvent "UserAdded"


// ========================


let appendToUserStream = Helpers.appendEvents client cancellationToken "User-012324"

appendToUserStream [ anEvent; anAnotherEvent ]

```


### Union Helpers

This is really helpful if you create this kind of scenario:

```fsharp
type TransactionType =
    | Buy | Sell | Send | Convert | RewardsIncome | Receive
type Asset = Asset of string
type Transaction =
    {
        Timestamp: DateTime
        TransactionType: TransactionType
        Asset: Asset
        QuantityTransacted: decimal
        SpotPriceCurrency: string
        SpotPriceAtTransaction: decimal
        SubTotal: decimal
        Total: decimal //  (inclusive of fees)
        Fees: decimal
        Notes: string
    }

type SpotPriceAdded = { Price: decimal }
type Transaction

type CryptoStream 
    | AssetBought of Transaction
    | AssetSold of Transaction
    | AssetConverted of Transaction
    | AssetRewardsReceived of Transaction
    | AssetSent of Transaction
    | AssetReceived of Transaction
    | AssetPrice of SpotPrice
```

So the idea is that your whole stream of events is represented by that single Union case, so what you can do is this:

```fsharp
// you want to create an AssetSold
let typeToCreate = "AssetSold" // or you can do nameof(AssetSold) which is more "type" safe


EventToUnionCase.createUnionFromFullUnionTree<CryptoStream, CryptoStream> typeToCreate (Some { Asset = Asset "BTC" }) // CryptoStream option

// See the tests for more extensive usage.

```

The idea is to be used in the deserialisation process when reading - to make things more automagic:

```fsharp
// the 'eventType being the CryptoStream for example
let deserialise<'eventType> (r: ResolvedEvent) (options: JsonSerializerOptions) =
    let eventType =
        UnionEventCreator.getUnionCaseType<'eventType> r.Event.EventType
        |> Option.defaultWith (fun () -> invalidOp $"No type found for %s{r.Event.EventType}")

    let data = Helpers.readEventUsingType opts r eventType
    
    data
    |> Some
    |> UnionEventCreator.createFullUnionTree<'eventType> r.Event.EventType
```

## Melman.EventStore.AspNetCore

Install

```bash
dotnet package add Melman.EventStore.AspNetCore
```

_this relies on the above package anyway, so either just this, or just the other one._

### Usage

This is more of those implementation specifics for reading events, as you can see above, those helpers are quite arbitrary.

When it comes to reading it's very specific on your use case. For me, I wanted a background worker listening to a stream so that
I could hook up signalR and push events to the UI or GraphQL.

```fsharp
type StreamSubscriber(logger: ILogger<StreamSubscriber>, eventStoreFactory: IEventStoreFactory) = // see the impl above for the EventStoreFactory
    inherit StreamSubscriber(logger, eventStoreFactory)

    override this.HandleNewEvent resolvedEvent cancellationToken = Task.CompletedTask // for every event that is published, this will be called in that order... if the stream starts at the beginning, then this will fire for every single event in the stream
    override this.HandleSubscriptionDropped reason error = () // here you can handle what happens when a sub is dropped
    override this.StoreLatestEventPosition latestEventPosition = Task.CompletedTask // this is for if you want to store the last position in Redis or something
    override this.Stream = "Test Stream" // this will be the stream you're subscribing to...
```
