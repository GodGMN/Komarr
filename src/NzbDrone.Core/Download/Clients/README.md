# Download/Clients/ — download client implementations

One folder per supported client. All derive from `TorrentClientBase` or
`UsenetClientBase` (themselves in `../`).

## Implementations

| Folder              | Protocol      | Largest file (LoC)              |
|---------------------|---------------|----------------------------------|
| `Sabnzbd/`          | Usenet        | `Sabnzbd.cs` ~539                |
| `NzbGet/`           | Usenet        | `Nzbget.cs` ~345                 |
| `NzbVortex/`        | Usenet        | `NzbVortex.cs` ~257              |
| `QBittorrent/`      | Torrent       | `QBittorrent.cs` ~725            |
| `Transmission/`     | Torrent       | `Transmission.cs` ~305           |
| `Deluge/`           | Torrent       | `Deluge.cs` ~356                 |
| `rTorrent/`         | Torrent       | `RTorrent.cs` ~316               |
| `UTorrent/`         | Torrent       | `UTorrent.cs` ~321               |
| `Aria2/`            | Torrent       | `Aria2.cs` ~264                  |
| `DownloadStation/`  | Both          | `TorrentDownloadStation.cs` ~459 / `UsenetDownloadStation.cs` ~438 |
| `Hadouken/`         | Torrent       | `Hadouken.cs` ~199               |
| `Blackhole/`        | Both          | `TorrentBlackhole.cs` / `UsenetBlackhole.cs` |

## Conventions

Each client folder contains:

```
{Client}/
├── {Client}.cs            DownloadClientBase implementation
├── {Client}Settings.cs    [FieldDefinition]-decorated settings
├── {Client}Proxy.cs       Thin RestSharp wrapper for the client's API
├── {Client}*Models.cs     Request/response DTOs
└── {Client}Exception.cs   Domain exceptions
```

## Smell

The `*Proxy.cs` files duplicate a near-identical RestSharp request →
response → exception-wrap loop. Worth a shared base. See
`../../../../ARCHITECTURE.md` §8.4.
