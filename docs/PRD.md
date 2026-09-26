# Komarr
## Manga Collection Manager & Automation for Usenet and BitTorrent

- **Status:** Initial architecture / PRD
- **Target:** v0.1 MVP → community alpha
- **License:** GNU GPL v3
- **Primary metadata provider:** AniList
- **Base project:** Librarr 1.2.2-beta, forked and pinned to a specific upstream commit/tag
- **Primary ecosystem compatibility:** Prowlarr, Torznab, Newznab, qBittorrent, Transmission, Deluge, SABnzbd, NZBGet and existing Servarr-style download clients.

---

# 1. Product vision

Komarr is a self-hosted manga collection manager and acquisition automation application inspired by Sonarr, Radarr and Readarr.

The desired experience is:

1. Search for a manga.
2. Select the correct AniList entry.
3. Add it to Komarr.
4. Choose how it should be monitored.
5. Komarr searches configured indexers for matching releases.
6. Komarr evaluates and selects acceptable releases.
7. Komarr sends the selected release to the configured download client.
8. Once complete, Komarr identifies the downloaded files.
9. Komarr imports, renames and organizes them.
10. Komarr continues monitoring indexer feeds for newly released volumes or chapters.

Example:

```text
User searches:
    "Chainsaw Man"

            ↓

AniList:
    Chainsaw Man
    チェンソーマン
    Chainsaw-Man
    AniList ID: ...
    Status: RELEASING

            ↓

Komarr library:
    Chainsaw Man
    Tracking: Volumes
    Existing: 01-18
    Monitoring future releases: Yes

            ↓

Prowlarr / Torznab:
    [Digital] Chainsaw Man v19 (2026) (Digital) (...)

            ↓

Komarr parser:
    Manga: Chainsaw Man
    Unit: Volume 19
    Source: Digital
    Language: English
    Confidence: High

            ↓

qBittorrent

            ↓

Completed Download Handling

            ↓

/manga/Chainsaw Man/
    Chainsaw Man - Vol 019.cbz
```

Komarr is not a manga reader.

Applications such as Kavita, Komga, calibre-web or custom readers such as Hyperion should consume the resulting library.

Komarr's responsibility ends at maintaining a correct, organized manga collection.

---

# 2. Architectural decision

## 2.1 Base Komarr on Librarr, not Sonarr

Komarr SHALL begin as a fork of:

```text
Rorqualx/Librarr
tag: 1.2.2-beta
```

or an explicitly recorded commit corresponding to that release.

Librarr is itself descended from Readarr, which descended from the Servarr/Sonarr architecture.

It therefore contains the subsystems Komarr actually needs:

```text
Authentication
Configuration
Datastore
DecisionEngine
Download
HealthCheck
History
IndexerSearch
Indexers
Jobs
Messaging
Notifications
Organizer
Profiles
Queue
RemotePathMappings
RootFolders
Security
Tags
REST API
SignalR
React UI
Docker / packaging
```

The Readarr/Librarr codebase is already semantically much closer to manga than Sonarr because it operates on document-like media rather than video episodes.

Readarr already supports automated monitoring, missing-item searches, release selection, failed-download handling, ebook-like file formats, importing, renaming, multiple download clients and Torznab/Newznab.

Librarr additionally modernizes the archived Readarr base and currently targets .NET 10, React 18 and current packaging infrastructure.

## 2.2 Do not maintain upstream Librarr compatibility

Komarr is a real fork, not a Librarr plugin.

After the initial fork:

```text
Librarr upstream
       X
       │
       └── no expectation of routine merges
```

The codebase should progressively replace Readarr/Librarr terminology and concepts.

Useful upstream fixes MAY be manually cherry-picked.

We must avoid preserving inappropriate abstractions purely to make upstream merging easier.

---

# 3. Important domain difference

Sonarr has authoritative episode metadata:

```text
Series
 └── Season
      └── Episode
```

Its metadata source can tell it that an episode exists before Sonarr encounters a release.

Komarr does not have equivalent volume metadata from AniList.

AniList provides fields including:

```text
id
idMal

title {
    romaji
    english
    native
}

synonyms
description
status
format
countryOfOrigin
startDate
endDate
chapters
volumes
coverImage
bannerImage
genres
tags
relations
updatedAt
```

AniList describes `chapters` and `volumes` as the counts when a manga is complete.

Therefore:

```text
AniList != TVDB-for-manga-volumes
```

Komarr MUST support two ways of discovering wanted content.

### Metadata-derived desired state

For titles where an authoritative usable count is available:

```text
volumes = 10

Wanted:
1
2
3
...
10
```

### Release-derived desired state

For ongoing or incompletely-described titles:

```text
Known library max:
18

RSS sees:
Chainsaw Man v19

→ discover Volume 19
→ evaluate
→ grab
```

This distinction is fundamental to the product architecture.

---

# 4. Product terminology

The product domain SHALL use the following terms.

## Manga

An AniList work.

Examples:

```text
BLAME!
Chainsaw Man
Berserk
Claymore
```

## Manga Item

A logical collectible unit belonging to a Manga.

An item is initially one of:

```text
Volume
Chapter
```

Future types may include:

```text
Omnibus
Special
Pack
```

but these SHALL NOT be required for v0.1.

## Manga File

A physical file stored in the library.

Examples:

```text
Chainsaw Man - Vol 001.cbz
Chainsaw Man - Vol 002.cbz
Berserk - Vol 042.epub
```

One file MAY cover more than one Manga Item.

## Release

An indexer result representing downloadable content.

Example:

```text
[SomeGroup] Chainsaw Man v01-18 (Digital) (English)
```

## Release Coverage

The items that a release appears to contain.

Example:

```text
type: Volume
start: 1
end: 18
```

---

# 5. Initial domain model

Do NOT directly transform:

```text
Author → Mangaka
Book → Manga
Edition → Volume
```

Those concepts are not equivalent and doing this mechanically will leave the Readarr domain encoded throughout the application.

Instead implement a new manga domain.

## Manga

```text
Manga
-----
Id
AniListId
MalId?

TitleRomaji
TitleEnglish?
TitleNative?

PreferredTitle
CleanTitle

Synonyms[]

Description?
Status
Format
CountryOfOrigin?

StartDate?
EndDate?

AniListChapterCount?
AniListVolumeCount?

CoverUrl?
BannerUrl?

TrackingMode
Monitored

MonitorFutureItems

RootFolderPath
Path

QualityProfileId
MetadataProfileId? [likely remove later]
Tags[]

LastInfoSync
LastSearchTime
Added
AniListUpdatedAt?
```

`AniListId` SHALL be the canonical external identity.

## TrackingMode

```text
Volume
Chapter
```

Default:

```text
Volume
```

Chapter tracking exists because Nyaa and other sources may distribute scanlations per chapter.

## MangaItem

```text
MangaItem
---------
Id
MangaId

Type
NumberDecimal?
NumberText

Title?
Monitored

DiscoveredFrom
    Metadata
    Release
    Manual

ReleaseDate?
Added
```

`NumberText` exists so the model is not permanently limited to integers.

Examples:

```text
1
1.5
12
12.5
0
EXTRA-1
```

When numeric representation is possible, also populate `NumberDecimal` for sorting.

## MangaFile

```text
MangaFile
---------
Id
MangaId

Path
Size
Modified
DateAdded

OriginalFilePath?
SceneName?
ReleaseGroup?

Language?
Source?
Quality?

IndexerFlags

EditionLabel?
```

## MangaFileItem

Many-to-many relation:

```text
MangaFileItem
-------------
MangaFileId
MangaItemId
```

This allows:

```text
Omnibus.cbz

covers:
Volume 1
Volume 2
Volume 3
```

without redesigning the file model later.

---

# 6. Editions

Manga editions are a genuine domain problem.

Example:

```text
BLAME! original edition
10 volumes

BLAME! Master Edition
6 volumes
```

AniList does not provide enough structured information to reliably map every physical/digital edition.

Therefore v0.1 SHALL NOT attempt to create a global normalized edition database.

This is intentional.

Instead:

```text
MangaFile.EditionLabel
```

and parsed release metadata MAY contain:

```text
EditionHint
```

Examples:

```text
Master Edition
Deluxe Edition
Omnibus
Perfect Edition
Digital
Unknown
```

These values are informational in v0.1.

A future release may introduce:

```text
MangaEdition
EditionVolumeMapping
```

without breaking the core `MangaItem ↔ MangaFile` model.

The system MUST NOT assume:

```text
AniList volume count == every available edition's volume count
```

---

# 7. Metadata subsystem

## 7.1 AniList provider

Create:

```text
MetadataSource/AniList/
```

Suggested components:

```text
AniListClient
AniListSearchService
AniListMetadataService
AniListMapper
AniListRequestLimiter
AniListCache
```

The API is GraphQL and public manga metadata does not require authentication.

## 7.2 Search

Search:

```graphql
Page(page: $page, perPage: $perPage) {
  media(search: $query, type: MANGA) {
    ...
  }
}
```

Results should include enough data to distinguish similarly named manga:

```text
cover
preferred title
native title
romaji title
year
format
status
author/staff if practical
description preview
```

## 7.3 Caching

AniList explicitly prohibits using the API as a backup/storage service and mass collection of its dataset. Komarr SHALL only request and cache data necessary for titles that the user searches for or has added.

Do not crawl AniList.

Recommended metadata refresh:

```text
releasing manga:
    every 12-24h, staggered

finished manga:
    every 7d or longer

manual refresh:
    immediate
```

Search result cache:

```text
15-60 minutes
```

AniList currently documents a temporary degraded limit of 30 requests/minute and a normal limit of 90 requests/minute. The client MUST obey the response rate-limit headers and `Retry-After`.

Implement one centralized limiter.

Never scatter raw AniList HTTP calls throughout the codebase.

## 7.4 Failure mode

If AniList is unavailable:

- existing library MUST continue functioning;
- indexer RSS processing MUST continue;
- downloading MUST continue;
- importing MUST continue;
- cached metadata MUST remain usable;
- only operations requiring fresh metadata should fail.

Metadata availability must never become equivalent to application availability.

This is explicitly intended to avoid Readarr's historical failure mode where its metadata infrastructure became unusable and contributed to retirement of the project.

---

# 8. Alias system

Manga releases are extremely inconsistent in naming.

A Manga SHALL maintain generated/searchable aliases.

Sources:

```text
title.romaji
title.english
title.native
AniList synonyms
user-defined aliases
```

Example:

```text
Manga:
Sousou no Frieren

Aliases:
Frieren
Frieren: Beyond Journey's End
Sousou no Frieren
葬送のフリーレン
```

Normalize aliases for matching:

```text
lowercase
Unicode normalization
remove punctuation where appropriate
collapse whitespace
normalize apostrophes
normalize dash variants
optionally roman-number handling
```

Do NOT permanently discard the original strings.

---

# 9. Indexer architecture

Retain Readarr/Librarr indexer infrastructure.

Required protocols:

```text
Torznab
Newznab
```

Native individual tracker implementations are secondary.

Prowlarr should be the expected configuration for most users.

Prowlarr supports hundreds of trackers and generic Torznab/Newznab connections.

## 9.1 Nyaa

Prowlarr's current Nyaa definition describes the site as covering anime, manga, literature and music.

Its Literature categories are normalized to:

```text
Books
```

and the definition supports:

```text
book-search: [q]
```

Therefore Komarr should use Books categories by default for manga indexers.

## 9.2 Prowlarr compatibility phase 1

Retain API compatibility with the relevant Readarr endpoints:

```text
GET    /api/v1/indexer
GET    /api/v1/indexer/schema
POST   /api/v1/indexer
PUT    /api/v1/indexer/{id}
DELETE /api/v1/indexer/{id}
POST   /api/v1/indexer/test
GET    /api/v1/system/status
```

Prowlarr's current Readarr integration uses these endpoints and does not require Readarr-specific book-domain APIs for indexer synchronization.

Acceptance test:

> A current Prowlarr instance configured with application type `Readarr`, pointed at Komarr, can synchronize a Nyaa indexer to Komarr.

If successful, document this as temporary compatibility.

## 9.3 Prowlarr compatibility phase 2

Submit a Prowlarr contribution adding:

```text
Applications/Komarr/
```

based on its existing Readarr implementation.

Desired end-state:

```text
Settings
 → Apps
   → Komarr
```

No compatibility masquerading required.

---

# 10. Release parser

This is one of the most important Komarr-specific components.

Create a dedicated model:

```text
ParsedMangaReleaseInfo
```

Suggested fields:

```text
RawTitle

ParsedTitle
MatchedAlias?

UnitType
StartNumber
EndNumber

IsPack
IsComplete

EditionHint?

Language?
Source?
ReleaseGroup?

Year?

Confidence
Warnings[]
```

## 10.1 Examples

Input:

```text
Chainsaw Man v12 (2023) (Digital) (LuCaZ)
```

Output:

```text
title: Chainsaw Man
unitType: Volume
start: 12
end: 12
source: Digital
confidence: High
```

Input:

```text
Chainsaw Man Vol. 01-11
```

Output:

```text
unitType: Volume
start: 1
end: 11
isPack: true
confidence: High
```

Input:

```text
Chainsaw Man ch. 182
```

Output:

```text
unitType: Chapter
start: 182
end: 182
confidence: High
```

Input:

```text
BLAME! Master Edition 01-06
```

Output:

```text
title: BLAME!
start: 1
end: 6
editionHint: Master Edition
confidence: Medium/High
```

## 10.2 Explicit tokens

Recognize at minimum:

### Volume

```text
v01
v1
v001
vol01
vol 01
vol. 01
volume 1
volumes 1-5
v01-05
v01~05
```

### Chapter

```text
c123
ch123
ch.123
ch 123
chapter 123
chapters 120-130
```

### Packs

```text
complete
completed
collection
batch
pack
vol 1-10
v01-v10
```

### Edition hints

```text
omnibus
master edition
deluxe
perfect edition
collector's edition
digital
official
scan
raw
```

## 10.3 Ambiguous bare numbers

Example:

```text
Some Manga 01-20
```

This MUST NOT silently be assumed to mean volumes.

Return:

```text
confidence: Low
```

and allow the Decision Engine to reject or require interactive/manual handling.

## 10.4 Parser philosophy

Komarr's core parser MUST remain deterministic.

Do not depend on an LLM.

Machine-learning/LLM parsing could later exist as an optional provider, but basic operation must remain offline, predictable and testable.

---

# 11. Parser fixture corpus

Before implementing automatic grabbing, build:

```text
tests/Parser/Fixtures/
```

containing a large real-world corpus of manga release names.

Target before enabling auto-grab:

```text
>= 500 release titles
```

Preferably:

```text
1,000+
```

Sources should cover:

```text
Nyaa
private trackers where contributors can legally provide titles
Usenet naming
digital manga
scanlations
packs
single volumes
single chapters
omnibus releases
different languages
weird punctuation
Japanese titles
romaji titles
English titles
```

Each fixture:

```json
{
  "input": "...",
  "expected": {
    "title": "...",
    "unitType": "Volume",
    "start": 4,
    "end": 4,
    "editionHint": null
  }
}
```

The parser should be developed fixture-first.

False positives are more dangerous than false negatives.

---

# 12. Search pipeline

Interactive search:

```text
Manga
  ↓
Generate search aliases
  ↓
IndexerSearch
  ↓
Torznab/Newznab
  ↓
Release parser
  ↓
Map release → Manga
  ↓
Determine coverage
  ↓
DecisionEngine
  ↓
Display candidates
```

For a particular volume:

```text
Search aliases:
"Chainsaw Man v12"
"Chainsaw Man vol 12"
"Chainsaw Man volume 12"
"Chainsaw-Man v12"
...
```

Do not hammer indexers with every synonym.

Choose a bounded search strategy.

Suggested initial search preference:

```text
preferred title
english title
romaji title
```

Stop or merge after useful results.

---

# 13. Matching releases to Manga

Matching should use several signals.

Suggested score:

```text
exact normalized preferred title
exact normalized alias
prefix/structured title match
fuzzy title similarity
requested item contained in parsed coverage
release category
language
edition hints
```

A release MUST NOT auto-grab solely because of a fuzzy string match.

Example:

```text
requested:
BLAME!

result:
BLAME! Academy and So On
```

must not accidentally map to the parent work.

Mappings should be explainable.

Interactive Search should expose:

```text
Matched title: BLAME!
Matched alias: "Blame"
Coverage: Volumes 1-10
Match confidence: 0.96

Accepted/Rejected:
✓ Title match
✓ Contains requested volume
✓ Language allowed
✗ None
```

---

# 14. Monitoring model

Komarr SHALL implement two complementary monitoring systems.

## 14.1 Known desired items

When Komarr knows a count:

```text
Finished manga
AniListVolumeCount = 10
TrackingMode = Volume
```

generate:

```text
Volume 1
...
Volume 10
```

These behave similarly to missing episodes/books.

## 14.2 Future-item discovery

When:

```text
Status = RELEASING
MonitorFutureItems = true
```

Komarr listens to new indexer feed results.

Pseudo-flow:

```text
RSS result arrives
    ↓
Parse release
    ↓
Match Manga alias
    ↓
Coverage = Volume 19
    ↓
Does Manga already know Volume 19?
    ├─ yes → normal evaluation
    └─ no
         ↓
      create discovered MangaItem
         ↓
      evaluate/grab
```

This allows ongoing manga monitoring without requiring AniList to know every volume in advance.

---

# 15. RSS / recent release sync

Reuse the existing *arr RSS sync architecture wherever practical.

Komarr should periodically query enabled indexers for recent releases.

Do NOT perform one HTTP request per manga.

Correct architecture:

```text
Indexer recent feed
       ↓
many releases
       ↓
parser
       ↓
match against monitored Manga aliases
       ↓
evaluate candidates
```

This is necessary for installations containing hundreds or thousands of manga.

---

# 16. Decision Engine

Retain the Servarr DecisionEngine/specification pattern but replace book-specific checks.

Suggested specifications:

```text
MangaTitleSpecification
MangaItemSpecification
MonitoringSpecification
LanguageSpecification
FormatSpecification
AlreadyImportedSpecification
ExistingFileSpecification
BlocklistSpecification
ReleaseSizeSpecification
SeedersSpecification
EditionHintSpecification
QualityProfileSpecification
CustomFormatSpecification
```

Every rejection MUST provide a user-visible reason.

Example:

```text
Rejected:
- Release appears to contain Volume 6, requested Volume 8.
```

Never silently discard candidates.

---

# 17. Quality system

Do not preserve ebook/audio quality semantics unchanged.

For manga, useful quality dimensions include:

## Container

```text
CBZ
CBR
EPUB
PDF
ZIP
```

## Source

```text
Official Digital
Digital
Scan
Raw
Unknown
```

## Language

```text
English
Japanese
Spanish
Spanish-LATAM
French
German
...
```

## Release properties

```text
Complete
Pack
Single volume
Single chapter
Upscaled
Watermarked
Official translation
Fan translation
```

Reuse the existing Custom Formats machinery where appropriate rather than hard-coding every preference.

Example profile:

```text
Preferred:
+100 Official Digital
+50  CBZ
+20  English
-100 PDF
-500 Raw
```

---

# 18. Download clients

Keep the existing download client subsystem as intact as possible.

Required community-alpha targets:

```text
qBittorrent
Transmission
SABnzbd
NZBGet
```

Retaining all inherited supported clients is preferable if they continue compiling and passing tests.

Komarr sends the selected download and uses a category such as:

```text
komarr
```

---

# 19. Completed Download Handling

Flow:

```text
Download client reports complete
       ↓
Komarr inspects output
       ↓
enumerate candidate files
       ↓
parse individual filenames
       ↓
match Manga
       ↓
map coverage
       ↓
import
```

Files supported initially:

```text
.cbz
.cbr
.epub
.pdf
.zip
```

Do not assume that the torrent itself contains only one file.

Example:

```text
Chainsaw Man v01-11/
    Chainsaw Man v01.cbz
    Chainsaw Man v02.cbz
    ...
    Chainsaw Man v11.cbz
```

Each file should be parsed independently and imported.

---

# 20. Hardlinks and seeding

Preserve existing Servarr semantics.

When source and library are on the same filesystem and hardlinks are enabled:

```text
/downloads/komarr/file.cbz
        ↕ hardlink
/manga/Series/file.cbz
```

Do not move a torrent payload in a way that breaks seeding.

Fallback:

```text
copy
```

where hardlinking is impossible.

---

# 21. Naming and organization

Default library structure:

```text
{Root Folder}/
  {Manga Title}/
    {Manga Title} - Vol {volume:000}.{ext}
```

Example:

```text
/manga/
  Chainsaw Man/
    Chainsaw Man - Vol 001.cbz
    Chainsaw Man - Vol 002.cbz
```

Chapter mode:

```text
{Manga Title} - Ch {chapter}.{ext}
```

Users should eventually be able to configure naming tokens.

Potential tokens:

```text
{Manga Title}
{Original Title}
{Romaji Title}
{English Title}
{Native Title}
{Volume}
{Chapter}
{Edition}
{Language}
{Release Group}
{Source}
{Year}
```

---

# 22. Existing library import

Komarr MUST be useful to users who already have manga.

Flow:

```text
Add Root Folder
      ↓
Scan folders
      ↓
Detect unmapped manga directories
      ↓
User maps folder to AniList Manga
      ↓
Parse contained files
      ↓
Map files to MangaItems
```

Example:

```text
/manga/BLAME!/
    BLAME v01.cbz
    ...
```

User selects:

```text
AniList → BLAME!
```

Komarr identifies existing coverage.

Ambiguous files should be surfaced for interactive mapping.

Never silently invent mappings.

---

# 23. Wanted pages

Provide:

```text
Wanted → Missing
Wanted → Cutoff Unmet
```

Missing example:

```text
BLAME!            Volume 07
Chainsaw Man      Volume 19
Claymore          Volume 14
```

Cutoff unmet example:

```text
Chainsaw Man v12

Current:
PDF Scan

Wanted:
CBZ Digital
```

---

# 24. Interactive Search UX

This is a core *arr feature and MUST be preserved.

Columns should include:

```text
Release
Indexer
Age
Size
Seeders
Parsed coverage
Language
Source
Custom Format score
Status
```

Hover/details:

```text
✓ Manga matched
✓ Volume 12
✓ English
✓ Digital
✓ Meets profile
✓ 38 seeders

Score: +170
```

Rejected releases:

```text
⚠ Volume range does not contain requested item
⚠ Raw release not allowed
⚠ Existing file is preferred
```

---

# 25. UI identity

Komarr should visibly feel like a member of the *arr ecosystem.

Do not radically redesign the UI for v0.1.

Users should immediately understand:

```text
Library
Add New
Wanted
Activity
Settings
System
```

Suggested main navigation:

```text
Manga
Add New
Wanted
Activity
Calendar [later]
Settings
System
```

Manga overview cards should show:

```text
cover
title
status
monitoring state
owned items / known items
```

Example:

```text
Chainsaw Man

18 / ?
Volumes

RELEASING
MONITORED
```

For complete manga:

```text
BLAME!

10 / 10
Volumes

FINISHED
MONITORED
```

---

# 26. What to remove from Librarr

Remove or replace book-specific functionality.

Candidate removal areas include:

```text
Books/Author*
Books/Calibre/*
Books/Narrator*
MetadataSource/OpenLibrary/*
MetadataSource/Goodreads/*
book-specific tagging
audiobook media analysis
Calibre integration
ISBN/ASIN-specific search
author bibliography monitoring
book series semantics
audiobook quality types
```

Do not immediately delete generic infrastructure merely because it lives near book code.

Audit dependencies first.

---

# 27. What to retain from Librarr

Prefer retaining:

```text
Authentication
Backup
Blocklisting
Configuration
CustomFormats
Datastore
DecisionEngine framework
DiskSpace
Download
HealthCheck
History
Housekeeping
Http
IndexerSearch framework
Indexers
Jobs
Messaging
Notifications
Organizer framework
Profiles framework
Queue
RemotePathMappings
RootFolders
Security
Tags
Update mechanisms where legally/operationally appropriate
REST framework
SignalR
generic React component library
Docker build
GitHub Actions
SQLite/Postgres support
```

---

# 28. Rename strategy

Do not attempt a gigantic blind global rename in one commit.

Recommended stages:

## Stage A

Product-facing rename:

```text
Librarr → Komarr
```

including:

```text
branding
binary names
Docker image
ports if desired
UI text
app data folder
environment variables
documentation
```

## Stage B

Domain rename:

```text
Books → Manga
Book → Manga
BookFile → MangaFile
```

only where semantics actually match.

## Stage C

Internal historical namespaces.

The inherited code may still use:

```text
NzbDrone.*
```

This is common in Servarr descendants.

Do not make namespace purity a blocker for v0.1.

A namespace migration can occur later.

---

# 29. Database

Use inherited SQLite/PostgreSQL infrastructure.

Fresh Komarr installations SHOULD start with a new Komarr schema rather than pretending to be Readarr/Librarr databases.

No requirement exists to migrate Readarr or Librarr databases.

Core tables:

```text
Manga
MangaItems
MangaFiles
MangaFileItems

QualityProfiles
CustomFormats
Indexers
DownloadClients
History
Blocklist
RootFolders
Tags
Commands
Config
```

Avoid carrying unused legacy book tables into a clean installation.

---

# 30. API

Maintain Servarr-style REST patterns.

Examples:

```text
GET    /api/v1/manga
GET    /api/v1/manga/{id}
POST   /api/v1/manga
PUT    /api/v1/manga/{id}
DELETE /api/v1/manga/{id}

GET    /api/v1/manga/lookup?term=...
POST   /api/v1/manga/{id}/refresh

GET    /api/v1/item
GET    /api/v1/item/{id}

GET    /api/v1/release
POST   /api/v1/release

GET    /api/v1/wanted/missing

GET    /api/v1/indexer
GET    /api/v1/indexer/schema
...
```

Generate OpenAPI documentation if inherited infrastructure supports it.

---

# 31. Security

Inherited authentication must remain operational.

Do not expose download client credentials or indexer API keys in logs.

Redact:

```text
API keys
cookies
auth headers
passwords
torrent passkeys
AniList OAuth tokens if OAuth is later added
```

Path traversal defenses are required in import/naming code.

A downloaded release must never be able to make Komarr write outside configured root folders.

---

# 32. Out of scope for v0.1

Explicitly out of scope:

```text
Manga reader
AniList reading-progress synchronization
MAL progress synchronization
automatic metadata database mirroring
OCR
CBZ page manipulation
conversion between CBZ/EPUB/PDF
automatic archive extraction beyond inherited safe capabilities
perfect physical-edition modelling
ISBN catalogue
retail price tracking
manga recommendations
AI release parsing
mobile app
multi-user libraries
```

Do not allow scope creep here.

---

# 33. Milestone plan

## M0 — Fork boots as Komarr

Goal:

```text
docker run Komarr
```

and UI loads.

Tasks:

- fork Librarr;
- pin base commit;
- GPL attribution;
- product rename;
- Komarr app-data directory;
- remove Librarr update endpoints;
- Docker image;
- GitHub Actions;
- basic smoke test.

Acceptance:

```text
fresh container
→ database created
→ UI reachable
→ settings persisted
→ restart works
```

---

## M1 — Manga domain + AniList

Tasks:

- new Manga entities;
- MangaItem;
- MangaFile;
- migrations;
- AniList GraphQL client;
- rate limiter;
- caching;
- lookup endpoint;
- Add Manga UI;
- Manga details page;
- metadata refresh.

Acceptance:

```text
Search "BLAME!"
→ see AniList results
→ add correct entry
→ survives restart
→ cover/title/status visible
```

---

## M2 — Parser

Tasks:

- `ParsedMangaReleaseInfo`;
- title cleaning;
- alias matcher;
- volume parser;
- chapter parser;
- range parser;
- pack detection;
- source/language hints;
- confidence;
- fixture harness.

Acceptance:

```text
500+ fixture corpus

critical false-positive rate effectively zero
```

Automatic grabs remain disabled until confidence is sufficient.

---

## M3 — Interactive indexer search

Tasks:

- adapt IndexerSearch;
- retain Torznab/Newznab;
- Books categories;
- release mapping;
- DecisionEngine basics;
- Interactive Search UI.

Acceptance:

```text
Komarr + Prowlarr + Nyaa
→ search BLAME!
→ real releases appear
→ volume ranges parsed
→ rejection reasons visible
```

---

## M4 — Download + import

Tasks:

- qBittorrent path;
- SABnzbd path;
- completed download handling;
- file parsing;
- hardlink/copy;
- naming;
- history;
- blocklist;
- failed import reporting.

Acceptance:

```text
Interactive Search
→ Grab
→ qBittorrent
→ download completes
→ Komarr imports CBZ
→ Manga item becomes owned
```

This is the first true end-to-end Komarr milestone.

---

## M5 — Automation

Tasks:

- missing items;
- automatic search;
- RSS/recent release sync;
- release-derived item discovery;
- future monitoring;
- cutoff upgrades;
- failed download retry.

Acceptance:

No human interaction required for:

```text
new monitored volume appears on indexer
→ Komarr sees release
→ evaluates it
→ grabs it
→ imports it
```

---

## M6 — Existing library import

Tasks:

- root folder scan;
- unmapped folders;
- AniList matching;
- interactive correction;
- file coverage detection.

Acceptance:

A user with an existing structured manga library can adopt it without downloading anything again.

---

## M7 — Community alpha

Tasks:

- configuration wizard;
- health checks;
- backup/restore;
- proper Docker tags;
- x86_64 + arm64;
- documentation;
- screenshots;
- parser contribution guide;
- security documentation;
- issue templates;
- release notes.

Target:

```text
v0.1.0-alpha.1
```

---

## M8 — Native Prowlarr integration

Create upstream Prowlarr PR.

Copy/adapt existing Readarr application connector:

```text
Readarr
   ↓
Komarr
```

Sync categories:

```text
Books
Books/EBook
Books/Comics
Books/Foreign
```

where appropriate.

---

# 34. Agent execution strategy

Agents SHOULD NOT all modify the same domain simultaneously.

Recommended parallel worktrees:

```text
agent/bootstrap
agent/anilist
agent/domain
agent/parser
agent/indexers
agent/importer
agent/frontend
agent/tests
```

Dependency graph:

```text
bootstrap
   ↓
domain ─────→ frontend
   │
   ├────→ anilist
   │
   ├────→ parser
   │        ↓
   └────→ indexers
             ↓
          importer
             ↓
          automation
```

Merge infrastructure before dependent agents rebase.

---

# 35. Agent coding rules

1. Preserve useful inherited code before rewriting it.
2. Remove book concepts deliberately, not with blind substitutions.
3. Every new domain behavior gets tests.
4. Release parsing is fixture-driven.
5. No LLM is required at runtime.
6. Never silently accept an ambiguous match.
7. Every rejection should be explainable.
8. External APIs must sit behind interfaces.
9. AniList outages must not break local library operation.
10. Avoid giant God services.
11. Do not introduce a separate microservice unless genuinely necessary.
12. Prefer existing Servarr patterns for consistency.
13. Maintain Prowlarr/Readarr indexer API compatibility until native Komarr support exists.
14. New DB schema must use Komarr naming.
15. User data migrations must be deterministic and tested.

---

# 36. Test matrix

Minimum automated test categories:

## Unit

```text
AniList mapping
title normalization
alias matching
volume parsing
chapter parsing
range parsing
quality parsing
naming
coverage matching
DecisionEngine specs
```

## Integration

```text
SQLite migrations
Postgres migrations
Torznab responses
Newznab responses
download client mocks
completed download import
hardlink behavior
copy behavior
root folder scanning
```

## Contract

```text
AniList GraphQL response fixtures
Prowlarr → Komarr indexer sync
Servarr-style API schemas
```

## End-to-end

Docker Compose fixture:

```text
Komarr
Prowlarr
qBittorrent
```

Mock indexer where necessary.

Scenario:

```text
Add manga
→ discover release
→ grab
→ fake completed download
→ import
→ owned state
```

---

# 37. Performance requirements

Komarr should comfortably handle:

```text
10,000 manga
100,000 manga items
100,000+ files
```

without loading entire collections into memory for routine operations.

Alias matching for RSS must be indexed/precomputed.

Do not perform:

```text
for each RSS release:
    for each manga:
        expensive fuzzy match
```

Preferred architecture:

```text
normalized alias index
alias token lookup
candidate narrowing
then fuzzy/structured scoring
```

---

# 38. Observability

Maintain:

```text
Trace
Debug
Info
Warn
Error
```

Important operations should expose structured context:

```text
MangaId
AniListId
Indexer
Release
DownloadId
MangaItemId
ImportPath
```

Example:

```text
Matched release 'Chainsaw Man v19' to Manga 42
Alias='Chainsaw Man'
Coverage=Volume:19
Confidence=0.98
```

Debug logs should make parser failures diagnosable.

---

# 39. Health checks

Required:

```text
AniList reachable
Indexer failures
Download client failures
Root folder unavailable
Root folder not writable
Remote path mapping failures
Database health
Disk space
```

AniList outage:

```text
Warning
```

not fatal.

---

# 40. Backup

Retain inherited backup system.

Backup must include:

```text
database
configuration
```

Credentials may therefore be included.

Documentation must warn users that backups contain secrets.

---

# 41. AniList future integration

OAuth is NOT required for v0.1 metadata use. Public metadata can be queried without authentication.

Future optional functionality could include:

```text
import AniList manga list
add Planning entries to Komarr
mark owned manga based on AniList status
sync reading progress
```

This is explicitly separate from core metadata acquisition.

Komarr is primarily a collection manager, not an AniList client.

---

# 42. Future edition system

Only after sufficient real-world data has been collected should Komarr consider:

```text
MangaEdition
------------
Id
MangaId
Name
Language
Publisher
VolumeCount
Type
ExternalIds

EditionItemMapping
------------------
EditionId
EditionVolume
CanonicalChapterStart
CanonicalChapterEnd
```

This could model:

```text
BLAME!
├ Original
│  └ 10 volumes
└ Master Edition
   └ 6 volumes
```

Do not build this before users demonstrate that simpler release coverage cannot solve their needs.

---

# 43. Success definition for v0.1

Komarr v0.1 is successful if a new user can:

```text
1. Install Docker image.
2. Add manga root folder.
3. Configure Prowlarr.
4. Configure qBittorrent.
5. Search AniList for a manga.
6. Add it.
7. Perform interactive search.
8. See correctly parsed manga releases.
9. Grab one.
10. Have the completed CBZ automatically imported and renamed.
11. See the acquired volume represented in the UI.
12. Have a later volume automatically detected and grabbed from RSS.
```

Nothing beyond that is required to prove the product.

---

# 44. Architectural non-negotiables

The following decisions are deliberate and should not be casually changed by implementation agents.

### Base

Use Librarr/Readarr infrastructure rather than rebuilding the *arr stack.

### Metadata

AniList is the canonical identity provider for v0.1.

### Metadata resilience

Komarr never depends on AniList being online to operate an existing library.

### Units

Volume and Chapter are first-class collectible units.

### Editions

Full edition normalization is postponed.

### Monitoring

Support both metadata-derived known items and release-derived future items.

### Acquisition

Use Torznab/Newznab and Prowlarr rather than bespoke tracker scraping.

### Parsing

Deterministic parser with extensive fixtures.

### Runtime AI

None required.

### Storage

Self-hosted SQLite/Postgres using inherited Servarr database infrastructure.

### UI

Retain *arr UX conventions.

### License

GPLv3, preserving required attribution and source availability.

---

# 45. Initial technical spike

Before the full implementation begins, one agent should perform a 1-2 commit spike proving the riskiest inheritance assumptions.

The spike should:

1. Fork Librarr.
2. Rename the running binary/UI to Komarr minimally.
3. Leave indexer subsystem intact.
4. Remove/disable the existing metadata startup dependencies.
5. Start a fresh database.
6. Configure Prowlarr as a `Readarr` application pointing at Komarr.
7. Sync Nyaa.
8. Call Komarr's indexer search with a raw manga query.
9. Display the returned Nyaa Literature results in logs or a temporary endpoint.

Required proof:

```text
Prowlarr
   ↓
Komarr inherited Readarr API
   ↓
Nyaa Literature
   ↓
real manga search results
```

If this works, the highest-risk infrastructure question is resolved before large-scale refactoring begins.

---

# 46. Recommended first implementation order

The lead agent should execute exactly this sequence:

```text
A. Fork Librarr 1.2.2-beta and pin SHA.
B. Make clean Komarr build/package.
C. Prove Prowlarr + Nyaa round trip.
D. Introduce Manga domain schema.
E. Introduce AniList provider.
F. Build Add Manga flow.
G. Build parser + fixture corpus.
H. Connect parser to interactive search.
I. Adapt DecisionEngine.
J. Adapt completed download/import.
K. Add Wanted.
L. Enable RSS automation.
M. Build existing-library import.
N. Harden, document, release alpha.
```

Do not start with a visual rewrite.

Do not start with edition modeling.

Do not start by adding twenty metadata providers.

Do not start by implementing Nyaa directly.

Get the complete vertical acquisition path working first.

---

# 47. Product summary

Komarr is conceptually:

```text
             AniList
                │
         identity / metadata
                │
                ▼
         ┌─────────────┐
         │   Komarr   │
         │             │
         │ Manga model │
         │ Parser      │
         │ Wanted      │
         │ Decisions   │
         └──────┬──────┘
                │
         Torznab/Newznab
                │
                ▼
           ┌─────────┐
           │Prowlarr │
           └────┬────┘
                │
       Nyaa / trackers / NZB
                │
                ▼
         ┌─────────────┐
         │ qBit / SAB  │
         └──────┬──────┘
                │
                ▼
       Completed Download
                │
                ▼
         Komarr Import
                │
                ▼
       /media/manga/...
                │
                ▼
       Kavita / Komga /
       Hyperion / etc.
```

The product is not “Sonarr with AniList swapped in.”

It is:

> **the Servarr acquisition pipeline adapted properly to the much messier identity, numbering and edition semantics of manga.**

That distinction should guide every architectural decision.
