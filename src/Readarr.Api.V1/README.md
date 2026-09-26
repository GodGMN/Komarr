# Readarr.Api.V1/ — REST API v1

ASP.NET Core MVC controllers that expose the public REST API.

## Conventions

- One folder per domain (e.g., `Books/`, `Author/`, `Indexers/`, `Queue/`).
- Each folder has `{Entity}Controller.cs` + `{Entity}Resource.cs` (DTO).
- Controllers extend `RestController<TResource>` or
  `{Entity}ControllerWithSignalR<TResource>` if they need to broadcast CRUD
  events on the SignalR hub.
- Manual mapping (no AutoMapper) between domain models and `*Resource`
  DTOs — usually in extension methods next to the resource.
- API-key auth happens in the `Readarr.Http` middleware layer; controllers
  assume the request is authenticated.

## Notable controllers

- `Books/BookController.cs` — book CRUD.
- `Author/AuthorController.cs` — author CRUD + lookup.
- `Author/AuthorLookupController.cs` — metadata search.
- `Author/AuthorEditorController.cs` — bulk operations on authors.
- `Indexers/ReleaseController.cs` — manual release search & grab.
- `Indexers/IndexerController.cs` — indexer CRUD.
- `DownloadClient/DownloadClientController.cs` — download client CRUD.
- `Queue/QueueController.cs` — active queue.
- `History/HistoryController.cs` — grab/import/failed history.
- `System/SystemController.cs` — status, restart, shutdown.
- `Config/*` — runtime configuration endpoints.

## Swagger

OpenAPI generation via `Swashbuckle.AspNetCore.SwaggerGen 6.5.0`. The
generated `openapi.json` file is **excluded** from CI triggers
(`../../azure-pipelines.yml:33,43`).
