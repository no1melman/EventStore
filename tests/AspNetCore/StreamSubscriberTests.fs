module Melman.EventStore.AspNetCore.Tests.StreamSubscriberTests

open System
open System.Text.Json
open System.Threading
open System.Threading.Tasks

open Melman.EventStore.Common
open Melman.EventStore.AspNetCore
open Microsoft.Extensions.Hosting
open NUnit.Framework
open Moq

open EventStore.Client

open Microsoft.Extensions.Logging.Abstractions
open Microsoft.Extensions.Logging

[<SetUp>]
let Setup () =
    ()

type TestableStreamSubscriber(logger: ILogger<StreamSubscriber>, eventStoreFactory: IEventStoreFactory, called: unit -> unit) =
    inherit StreamSubscriber(logger, eventStoreFactory)

    override this.HandleNewEvent resolvedEvent cancellationToken =
        called()
        Task.CompletedTask
    override this.HandleSubscriptionDropped reason error = ()
    override this.StoreLatestEventPosition latestEventPosition = Task.CompletedTask
    override this.Stream = "Test Stream"


type FakeEventStoreFactory(mock: Mock<EventStoreClient>) =
    interface IEventStoreFactory with
        member this.CreateClient() = mock.Object

let releasingTask (trigger: SemaphoreSlim) =
    Task.CompletedTask.ContinueWith<StreamSubscription> (fun t ->
       trigger.Release() |> ignore
       null :> StreamSubscription)

type TempData = { Name: string }

let createDummyRecord (data: EventData) =
    let meta = dict ["type", "Jeff"; "content-type", "application/json"; "created", DateTime.UtcNow.Ticks.ToString()]
    let record = EventRecord("", Uuid.NewUuid(), StreamPosition.FromInt64(0L), Position.Start, meta, data.Data, ReadOnlyMemory([||]))
    ResolvedEvent(record, record, 0UL)

let ``Given a stream it should be subscribed against and then cancelled... or something`` () =
    let nullLogger = NullLogger<StreamSubscriber>.Instance
    
    let mockClient = Mock<EventStoreClient>()
    
    let fakeFactory = FakeEventStoreFactory(mockClient)
    
    let cts = new CancellationTokenSource()
    
    let called () = cts.Cancel()
    
    let sut = new TestableStreamSubscriber(nullLogger, fakeFactory, called) : IHostedService
    
    let releaseTrigger = new SemaphoreSlim(0, 1)
    
    let opts = JsonSerializerOptions()
    let reco = createDummyRecord (Helpers.createJsonEvent opts "" { Name = "jeff" })
    
    // Get error here... bugger
    // Non-overridable members (here: EventStoreClient.SubscribeToStreamAsync) may not be used in setup / verification expr
    mockClient.Setup(fun x -> x.SubscribeToStreamAsync(
                                                        "Test Stream",
                                                        It.IsAny<Func<StreamSubscription, ResolvedEvent, CancellationToken, Task>>(),
                                                        resolveLinkTos = false,
                                                        subscriptionDropped = It.IsAny<Action<StreamSubscription, SubscriptionDroppedReason, exn>>(),
                                                        configureOperationOptions = It.IsAny<Action<EventStoreClientOperationOptions>>(),
                                                        userCredentials = It.IsAny<UserCredentials>(),
                                                        cancellationToken = It.IsAny<CancellationToken>()))
                .Callback<string, Func<StreamSubscription, ResolvedEvent, CancellationToken, Task>>((fun n evAppeared ->
                    let toFire = task {
                        do! releaseTrigger.WaitAsync() // this will wait for the subscription to be created below
                        evAppeared.Invoke(null, reco, cts.Token) |> ignore
                    }
                    ()))
                .Returns(releasingTask releaseTrigger) |> ignore
    
    let _ = sut.StartAsync(cts.Token) // kicks off the background service
    
    
    mockClient.VerifyAll()
    
    Assert.Pass()
