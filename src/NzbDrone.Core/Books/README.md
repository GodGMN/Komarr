# Books/ — Author / Book / Edition / Series domain

The core domain. Everything else (download, import, notification) hangs off
these models.

## Domain model

```
Author  ─── 1 ───★── n ─── Book ─── 1 ─★─ n ─── Edition
   │                                            (one Edition is preferred)
   └── n ─── Series ─── n ─── Book
                  (collections / sagas / arcs)
```

- `Author` — primary library entity. Has a metadata profile, quality profile,
  metadata source (Goodreads / others).
- `Book` — a work by an author. Many editions per book.
- `Edition` — a specific publication (format, ISBN, language).
- `Series` — author-collected groupings (e.g., a saga); a book may belong
  to multiple series.
- `AuthorMetadata` — denormalised metadata snapshot.

## Key services

- `AuthorService` — CRUD + lookup.
- `RefreshAuthorService` (~418 LoC) — periodic metadata refresh from external
  sources.
- `BookService` (~318 LoC) — book CRUD.
- `EditionService` — edition selection + monitoring rules.
- `SeriesBookLinkService` — many-to-many book↔series links.

## Repositories

`AuthorRepository`, `BookRepository`, `EditionRepository`,
`SeriesRepository`. All inherit `BasicRepository<T>` from `Datastore/`.

## Events

`AuthorAdded`, `AuthorUpdated`, `AuthorDeleted`, `BookAdded`, `BookUpdated`,
`BookDeleted`, plus refresh-pipeline events. Handlers across the codebase
react (`Indexers/`, `Notifications/`, `MediaCover/`, `MediaFiles/`).

## Commands

`RefreshAuthorCommand`, `BulkRefreshAuthorCommand`, `RescanFolderCommand`,
etc.
