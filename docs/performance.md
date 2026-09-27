# Manga library performance notes

The RSS matcher builds an alias index once per feed batch. It looks up a maximum of 32 candidate manga for each parsed release. Items, files, file coverage, and blocklist entries are fetched only for matched manga, then cached for the rest of that batch. Unmatched releases do not read those tables.

`MangaRssSyncFixture.Matching_one_title_reads_only_its_items_files_and_blocklist` profiles 10,000 manga, a synthetic 100,000-item and 100,000-file collection, and 100 reports for one title. On the local .NET 10 SDK container, `Process` took 49 ms on the final focused run. The test verifies one scoped query each for items, files, coverage, and blocklist, and no whole-table reads for those tables. The number is a direction check, not a production latency guarantee: repositories are mocked, so it excludes database, disk, indexer, and download-client time.

The read-only library scan caps one request at 100 roots, 2,000 folders, 200 files per folder, and 10,000 returned files. It reports truncation instead of silently treating unseen files as absent. Folder mapping is explicit and validates the selected direct child of a configured root before registration.

The Wanted UI checks at most 50 manga per page and requests more only when the user asks. Its regression test models 10,000 manga and 100,000 items; two pages inspected 100 titles in 21 ms with mocked repositories. Scheduled missing-item searches inspect at most 100 manga per run and send searches for at most five; checked titles without missing items move to the back of the search order. The older unpaged `/api/v1/manga/wanted` endpoint remains for clients that already use it, so those clients should move to `/api/v1/manga/wanted/page` for large libraries.

To repeat the RSS profile, run the focused `MangaRssSyncFixture` test with normal console verbosity. Its output includes the measured time and the test fails if a whole-table item/file/coverage/blocklist query is reintroduced.
