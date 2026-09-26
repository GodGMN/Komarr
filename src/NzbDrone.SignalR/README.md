# NzbDrone.SignalR/ — real-time push (Readarr.SignalR.csproj)

Single SignalR hub that pushes server-side events to the React UI.

## Key types

- `MessageHub` — the only hub. Clients (the SPA) connect once via
  `/signalr/messages`.
- `IBroadcastSignalRMessage` — interface for components that want to push.
- `*WithSignalR` controller base classes in `Readarr.Api.V1` use the
  broadcaster to emit CRUD events for the entity they manage.

## What's pushed

- Command progress (`CommandUpdated`).
- Queue / activity changes.
- Health-check updates.
- Entity CRUD (author added, book deleted, etc.).
- Version / update info.

## Client side

The SPA consumes via `@microsoft/signalr 6.0.25`. The single connector is
`frontend/src/Components/SignalRConnector.js`, which dispatches Redux actions
from incoming events.

## Dependencies

`Microsoft.AspNetCore.SignalR` (6.x — implicit framework reference) and
`NzbDrone.Core`.
