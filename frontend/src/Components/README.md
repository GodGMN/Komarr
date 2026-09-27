# Components/ — shared UI primitives

Reusable, feature-agnostic components consumed by everything else.

## Subfolders

| Folder       | What                                                            |
|--------------|-----------------------------------------------------------------|
| `Form/`      | Inputs (`TagInput`, `TextInput`, `SelectInput`, etc.)            |
| `Link/`      | `IconButton`, `Link`, `MenuItem`                                 |
| `Loading/`   | `LoadingIndicator`, `LoadingMessage`                             |
| `Menu/`      | `Menu`, `MenuButton`, dropdowns                                  |
| `Modal/`     | `Modal`, `ModalBody/Header/Footer`, `ConfirmModal`               |
| `Page/`      | `Page`, `PageHeader`, `PageSidebar`, `PageToolbar`               |
| `Router/`    | Custom `Switch` wrapper                                          |
| `Swipe/`     | Touch-swipe page header (mobile)                                 |
| `Table/`     | `VirtualTable` for large lists                                   |
| `SignalRConnector.js` | The single SignalR ↔ Redux bridge                       |

## SignalR connector

`SignalRConnector.js` is mounted once near the root and lives for the
SPA's lifetime. It:

1. Establishes the `@microsoft/signalr` connection to the backend hub.
2. Subscribes to entity channels.
3. Dispatches Redux actions when events arrive — directly, without going
   through thunks.
4. Handles reconnection / connection-lost UI.

## Conventions

- Class components. Hooks have started to appear but most primitives are
  still class-based.
- PropTypes for every prop on `.js` files (enforced by ESLint).
- CSS Modules sibling file per component (`Foo.js` + `Foo.css`).
- Forwarded refs use the legacy ref-callback style, not `forwardRef`.
