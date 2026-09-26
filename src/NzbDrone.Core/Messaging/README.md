# Messaging/ — events + commands

Two in-process channels that drive almost every cross-cutting behaviour.

## Events (`Events/`)

In-memory pub/sub.

- `EventAggregator` — single instance, owns subscriptions.
- `IEvent` — marker interface; everything published implements it.
- `IHandle<TEvent>` — synchronous handler. Runs on the publisher's thread.
- `IHandleAsync<TEvent>` — async handler. Runs on the thread pool.

Handlers are auto-registered by the DryIoc scan and discovered per event
type at publish time.

## Commands (`Commands/`)

Background queued work.

- `ICommand` — marker for command messages (e.g., `RefreshAuthorCommand`).
- `CommandQueueManager` (~282 LoC) — durable queue stored in the database
  (`Commands` table) so commands survive process restarts.
- `CommandExecutor` (~138 LoC) — pulls from the queue and dispatches.
- `IExecute<TCommand>` — command-handler interface.
- `CommandUpdated` event — published when a command's state changes;
  forwarded to the SignalR hub so the UI sees live progress.

## Conventions

- Commands are JSON-serialisable POCOs — no behaviour, just data.
- One handler per command (a command has a single "execute me" semantics).
- Many handlers per event (events are broadcast notifications).

## Related

- `../Jobs/` — schedules recurring command enqueues.
- `../ProgressMessaging/` — SignalR wrapping for command progress.
