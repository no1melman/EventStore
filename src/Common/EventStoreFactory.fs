namespace Melman.EventStore.Common

open EventStore.Client

type IEventStoreFactory =
    abstract CreateClient : unit -> EventStoreClient