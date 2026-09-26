# Readarr.Http/ — HTTP middleware

Cross-cutting ASP.NET Core middleware shared by `Readarr.Api.V1` and the
SignalR endpoints.

## Responsibilities

- **API-key authentication** — `AuthenticationBuilderExtensions.cs` wires the
  API-key scheme into ASP.NET Core's auth pipeline. The key is loaded from
  `config.xml` and may also be supplied via the `Authorization` header,
  the `apikey` query string, or the `X-Api-Key` header.
- **Forms authentication** — for the optional UI login page.
- **CORS** — permissive in development, locked-down in production.
- **Error handling** — `IExceptionFilter` converts domain exceptions into
  HTTP status codes with a consistent `ErrorResource` body.
- **Request logging** — request/response correlation IDs and slow-request
  warnings.
- **Static-file serving** — serves the compiled SPA from `_output/UI/`.
- **Web-API frame** — `RestController<TResource>` base classes that handle
  HEAD/GET-by-id/POST/PUT/DELETE patterns uniformly.

## Authentication modes

- **None** (open).
- **Basic** (HTTP basic auth).
- **Forms** (cookie-based form login).

Set via Settings → General → Security.

## Dependencies

`Microsoft.AspNetCore.*` 6.x; `NzbDrone.Core` for the config service.
