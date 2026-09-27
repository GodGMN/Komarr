# Troubleshooting

Start with **Manga → Setup & Health**. Each check links to the relevant settings page. For Docker, inspect `docker logs komarr` and the files in `/config/logs`; remove private URLs, API keys, cookies, and credentials before sharing logs or backup files.

| Symptom | Check |
| --- | --- |
| The UI does not open | Confirm the container is running and port `8787` is published. `docker logs komarr` shows startup failures. Komarr binds `0.0.0.0` inside the container. |
| Manga root is missing or not writable | Use the container path, such as `/manga`, in Media Management. Verify the host volume is mounted and the container user can write to it. |
| AniList is unavailable | Treat this as a metadata warning. Saved manga and local collection management continue to work. Retry lookup later. |
| Prowlarr syncs no indexer | In Prowlarr, temporarily use the Readarr application type, Komarr's reachable URL and API key, and a manga-capable Torznab/Newznab source. Confirm RSS and search are enabled on the synced indexer. The [Nyaa proof](integrations/prowlarr-nyaa.md) shows the tested compatibility path. |
| Download stays in progress | Confirm the download client reports completion and that Komarr can see the finished payload under `/downloads`. Add a remote path mapping when the client reports a different path. |
| Release is skipped or requires review | Open the release decision and history on the manga detail page. Komarr does not automatically acquire an ambiguous title, unknown number, overlapping coverage, or a blocked release. |
| Archive was not imported | Check the file review reason and root folder permissions. A torrent payload is hardlinked or copied; the source is kept for seeding. Existing destination names and unsafe paths are never overwritten. |
| A mapped folder is absent | Library Scan only shows direct children of configured roots and bounds each scan. Linked files or folders outside a root are skipped with an explanation. Use the next scan or map the intended folder explicitly. |
| Backup restore fails | A ZIP restore requires both `config.xml` and `komarr.db`. Keep backups private and restart Komarr after a successful restore. |

The release parser corpus and [fixture contribution guide](contributing-fixtures.md) help report manga naming cases that need review.
