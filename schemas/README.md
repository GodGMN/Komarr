# schemas/

XSD schemas referenced by the backend.

## Contents

- **`torznab.xsd`** — Torznab indexer feed schema. Torznab is an RSS dialect
  used by indexers in the Servarr ecosystem (Sonarr/Radarr/Lidarr/Readarr) to
  return capabilities and search results in a consistent shape.

## Consumers

- `src/NzbDrone.Core/Indexers/Newznab/` — base Newznab/Torznab indexer.
- `src/NzbDrone.Core/Indexers/Torznab/` — Torznab specialisation.

The XSD is not directly deserialised at runtime; it is the contract definition
that the parser classes follow. See `../ARCHITECTURE.md` §4.3.5 (provider
plugin model).
