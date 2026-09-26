# Metadata removal plan (Phase 1 → Phase 5)

Per-file disposition for every Goodreads-tinged file under
`src/NzbDrone.Core/MetadataSource/` and
`src/NzbDrone.Core/ImportLists/Goodreads/`. This document is the
authoritative checklist for what disappears at the end of Phase 5 of
[`MASTER-PLAN.md`](../MASTER-PLAN.md).

The plan is split by phase because the deletion order matters:

* **Phase 2** refactors the interface seam so the `IProvide*` and
  `ISearchForNew*` contracts no longer mention Goodreads types.
* **Phase 3** introduces `OpenLibraryProxy` alongside the existing
  `BookInfoProxy`, with a config-driven selector.
* **Phase 5** ships the reidentify wizard. Only once Phase 5 is on
  `develop` and the default flips to `"OpenLibrary"` do we delete the
  Goodreads paths.

Until then, everything Goodreads stays compilable so the legacy proxy
remains the fallback during development.

## Interfaces (Phase 2 — refactor in place, do not delete)

| File | Current shape | Phase 2 action |
|---|---|---|
| `src/NzbDrone.Core/MetadataSource/IProvideSeriesInfo.cs` | `using NzbDrone.Core.MetadataSource.Goodreads;` line 1 — returns `Goodreads.SeriesResource` | Drop the `using`. Return new neutral DTO `Books.Model.SeriesInfo`. Change the `int id` parameter to `string foreignId`. |
| `src/NzbDrone.Core/MetadataSource/IProvideListInfo.cs` | Same pattern with `Goodreads.ListResource` | Drop `using`, return `Books.Model.ListInfo`, parameter `string foreignId`. |
| `src/NzbDrone.Core/MetadataSource/ISearchForNewBook.cs` | Line 11: `SearchByGoodreadsBookId(int goodreadsId, bool getAllEditions)` — name and primitive type leak Goodreads | Rename to `SearchByForeignBookId(string foreignBookId, bool getAllEditions)`. Update every call site (7 consumer services, see MASTER-PLAN.md Phase 2). |
| `src/NzbDrone.Core/MetadataSource/IProvideAuthorInfo.cs` | Clean (uses `string readarrId`) — no Goodreads leak | No change. |
| `src/NzbDrone.Core/MetadataSource/IProvideBookInfo.cs` | Clean (uses `string id`) | No change. |
| `src/NzbDrone.Core/MetadataSource/ISearchForNewAuthor.cs` | Not inspected in inventory yet — re-verify clean | If clean, no change. |
| `src/NzbDrone.Core/MetadataSource/ISearchForNewEntity.cs` | Not inspected in inventory yet — re-verify clean | If clean, no change. |
| `src/NzbDrone.Core/MetadataSource/MetadataRequestBuilder.cs` | Generic HTTP builder, presumed clean | Re-verify when refactoring `BookInfoProxy` to absorb any Goodreads-specific URL building. |

## `MetadataSource/BookInfo/` (Phase 3 — keep through development, delete in Phase 5)

The active production proxy. Reachable from every consumer of the
`IProvide*` interfaces. Stays as the fallback metadata source while
`OpenLibraryProxy` matures.

| File | Verdict | Phase to delete |
|---|---|---|
| `BookInfoProxy.cs` (~993 lines) | DELETE | End of Phase 5 |
| `BookInfoException.cs` | DELETE | End of Phase 5 |
| `BookInfoResource/AuthorResource.cs` | DELETE | End of Phase 5 |
| `BookInfoResource/BookResource.cs` | DELETE | End of Phase 5 |
| `BookInfoResource/BulkBookResource.cs` | DELETE | End of Phase 5 |
| `BookInfoResource/ContributorResource.cs` | DELETE | End of Phase 5 |
| `BookInfoResource/RecentUpdatesResource.cs` | DELETE | End of Phase 5 |
| `BookInfoResource/SeriesResource.cs` | DELETE | End of Phase 5 |
| `BookInfoResource/WorkResource.cs` | DELETE | End of Phase 5 |

Hand-off rule for Phase 2: when the interface refactor lands, mark
`BookInfoProxy` with `[Obsolete("Use OpenLibraryProxy via MetadataSourceFactory. Slated for deletion at the end of Phase 5.")]`
so anyone touching the seam during development gets a clear warning.

## `MetadataSource/Goodreads/` (delete in Phase 5)

Only reached transitively from `BookInfoProxy.cs:21` (`using
NzbDrone.Core.MetadataSource.Goodreads;`) and from the two interface
type leaks above. Once Phase 2 removes the interface leak and Phase 5
removes `BookInfoProxy`, this folder is unreachable.

| File | Reachable from | Verdict |
|---|---|---|
| `GoodreadsProxy.cs` | `BookInfoProxy` (transitive), also direct from a few ImportLists/Goodreads call sites | DELETE at end of Phase 5 — after the ImportLists/Goodreads providers are also gone (Phase 6) |
| `GoodreadsException.cs` | Same | DELETE end of Phase 5 |
| `Extensions/HttpResponseExtensions.cs` | `GoodreadsProxy` only | DELETE end of Phase 5 |
| `Extensions/XmlExtensions.cs` | `GoodreadsProxy` only | DELETE end of Phase 5 |
| `Resources/AuthorBookListResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/AuthorResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/AuthorSeriesListResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/AuthorSummaryResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/BestBookResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/BookLinkResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/BookResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/BookSearchResultResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/BookSummaryResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/GoodreadsResource.cs` | Base type for the rest | DELETE end of Phase 5 |
| `Resources/ListResource.cs` | `IProvideListInfo` (after Phase 2 the interface no longer references it) | DELETE end of Phase 5 |
| `Resources/OwnedBookResource.cs` | `GoodreadsProxy` + `ImportLists/Goodreads/OwnedBooks` | DELETE end of Phase 5 |
| `Resources/PaginatedList.cs` | Multiple Resources | DELETE end of Phase 5 |
| `Resources/PaginationModel.cs` | Multiple Resources | DELETE end of Phase 5 |
| `Resources/ReviewResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/SeriesResource.cs` | `IProvideSeriesInfo` (after Phase 2 the interface no longer references it) | DELETE end of Phase 5 |
| `Resources/ShowSeriesResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |
| `Resources/UserShelfResource.cs` | `GoodreadsProxy` + `ImportLists/Goodreads/Bookshelf` | DELETE end of Phase 5 |
| `Resources/WorkResource.cs` | `GoodreadsProxy` | DELETE end of Phase 5 |

## `MetadataSource/GoodreadsSearchProxy/` (delete in Phase 5)

Separate search-specific proxy. Mirror disposition of the main
`Goodreads/` folder.

| File | Verdict |
|---|---|
| `GoodreadsSearchProxy.cs` | DELETE end of Phase 5 |
| `Resources/SearchJsonResource.cs` | DELETE end of Phase 5 |

## `ImportLists/Goodreads/` (delete in Phase 6)

Goodreads-shaped import-list providers. Replaced by the new
`ImportLists/OpenLibrary/` providers in Phase 6
(`OpenLibrarySubjectImportList`, `OpenLibraryAuthorImportList`,
`OpenLibraryTrendingImportList`).

| Subfolder | Verdict | Notes |
|---|---|---|
| `ImportLists/Goodreads/Bookshelf/` | DELETE in Phase 6 | Depends on `Goodreads.UserShelfResource`. |
| `ImportLists/Goodreads/Lists/` | DELETE in Phase 6 | Depends on `Goodreads.ListResource`. |
| `ImportLists/Goodreads/OwnedBooks/` | DELETE in Phase 6 | Depends on `Goodreads.OwnedBookResource`. |
| `ImportLists/Goodreads/Series/` | DELETE in Phase 6 | Depends on `Goodreads.SeriesResource` and Wikidata fallback in the new `OpenLibrarySeriesProxy`. |

Important: Phase 6 must ship the OL import list providers *first*, then
this folder gets removed in the same PR or the one immediately after.
Do not leave users without an import-list flow.

## Non-Goodreads `ImportLists/` providers (no action)

| Subfolder | Action |
|---|---|
| `ImportLists/LazyLibrarian/` | Keep. Unaffected by metadata source. |
| `ImportLists/Readarr/` | Keep. Sources lists from another Readarr instance — unchanged. |
| `ImportLists/Exceptions/`, `ImportLists/Exclusions/` | Keep. Framework-level. |

## Ordering summary

```
Phase 2  →  Refactor interfaces, drop Goodreads using-directives.
            Mark BookInfoProxy [Obsolete]. Nothing deleted yet.
Phase 3  →  Add OpenLibraryProxy + MetadataSourceFactory.
            Both proxies live in parallel.
Phase 5  →  Reidentify wizard ships. Default flips to OpenLibrary.
            Delete: MetadataSource/BookInfo/, MetadataSource/Goodreads/,
                    MetadataSource/GoodreadsSearchProxy/.
Phase 6  →  OpenLibrary import lists ship.
            Delete: ImportLists/Goodreads/.
```

## Verification

After Phase 5 cleanup, run:

```bash
# Confirm no remaining references to the deleted folders/types
ast-grep --pattern 'using NzbDrone.Core.MetadataSource.Goodreads' src/
ast-grep --pattern 'using NzbDrone.Core.MetadataSource.BookInfo' src/
ast-grep --pattern 'using NzbDrone.Core.MetadataSource.GoodreadsSearchProxy' src/
ast-grep --pattern 'BookInfoProxy' src/
```

All four should return empty. The build should be green on
`./build.sh --backend && yarn build`.
