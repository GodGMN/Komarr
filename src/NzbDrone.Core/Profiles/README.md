# Profiles/ — user-defined preference profiles

Captures user preferences that drive the decision engine.

## Profile types

- **Quality Profile** (`Qualities/`) — ordered list of allowed qualities
  with a cut-off. Determines upgrade behaviour.
- **Metadata Profile** (`Metadata/`) — which works of an author to monitor
  (main books, novellas, short stories, anthologies, etc.).
- **Release Profile** (`Releases/`) — preferred / banned release groups,
  required / banned terms.
- **Delay Profile** (`Delay/`) — wait N hours before grabbing torrents
  (e.g., to allow Usenet to be preferred first).

## Where they're consumed

- `DecisionEngine/Specifications/` checks against the active profile to
  accept / reject candidate releases.
- `MediaFiles/BookImport/Specifications/` applies the same profiles at
  import time.
- `Indexers/IndexerSearch/` filters search results by profile.

## Persistence

All profiles live in the database via standard
`BasicRepository<T>`-derived repositories. The UI is in
`frontend/src/Settings/Profiles/`.
