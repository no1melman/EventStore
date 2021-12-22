namespace Melman.EventStore.AspNetCore

open System.Threading
open System.Threading.Tasks

open Microsoft.FSharp.Core
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open EventStore.Client

open Melman.EventStore.Common

type IEventStoreFactory =
    abstract CreateClient : unit -> EventStoreClient

[<AbstractClass>]
type StreamSubscriber (logger: ILogger<StreamSubscriber>, eventStoreFactory: IEventStoreFactory) =
    inherit BackgroundService()

    let mutable latestEventPosition  = 0UL
    
    abstract HandleNewEvent : resolvedEvent: ResolvedEvent -> cancellationToken: CancellationToken -> Task
    abstract HandleSubscriptionDropped : reason: SubscriptionDroppedReason -> error: exn -> unit
    abstract StoreLatestEventPosition : latestEventPosition: uint64 -> Task
    abstract Stream : string
    
    member private this.EventAppeared _ (resolvedEvent: ResolvedEvent) (cancellationToken: CancellationToken) : Task = task {
            latestEventPosition <- resolvedEvent.OriginalEventNumber.ToUInt64()
            
            // should think about adding an abstract method for storing that latest event position into a cache of some sort (letting the implementor decide)
            do! this.StoreLatestEventPosition latestEventPosition
            do! this.HandleNewEvent resolvedEvent cancellationToken
        }
    
    member private this.SubscriptionDropped _ (reason: SubscriptionDroppedReason) (error: exn) =
        this.HandleSubscriptionDropped reason error 
    
    override this.ExecuteAsync stoppingToken =
        logger.LogInformation("Initialising Stream Subscriber Service...")
        Task.Factory.StartNew(fun () -> task {
            logger.LogInformation("Running Stream Subscriber
                                  Service...")
            
            let client = eventStoreFactory.CreateClient ()            
        
            let subscriber = Helpers.subscribe client stoppingToken
            
            let! _ = subscriber this.Stream None this.EventAppeared this.SubscriptionDropped
            
            while not stoppingToken.IsCancellationRequested do
                do! Task.Delay(30000, stoppingToken)
        
            logger.LogInformation("Stream Subscriber Background Service Shutting down...")
            return! Task.CompletedTask
        }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Current) |> ignore
        Task.CompletedTask
    