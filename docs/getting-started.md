# Getting started with Komarr

Komarr stores manga metadata, monitored volumes or chapters, download history, and collection coverage in `/config/komarr.db`. The archives remain in your manga root. Readers such as Komga and Kavita can read that folder directly; they do not need to connect to Komarr.

## Install

The `edge` image tracks the latest tested `main` build. Alpha release tags such as `v0.1.0-alpha.1` stay fixed. Images are built for Linux amd64 and arm64. GitHub Container Registry must show the package as **Public** for an anonymous pull ([GitHub visibility guide](https://docs.github.com/en/packages/learn-github-packages/configuring-a-packages-access-control-and-visibility)); until the first public image is available, build the checkout with the [Docker instructions](../distribution/docker/README.md).

```bash
docker pull ghcr.io/godgmn/komarr:edge
mkdir -p komarr-config manga downloads
docker run -d --name komarr --restart unless-stopped \
  -p 8787:8787 \
  -v "$PWD/komarr-config:/config" \
  -v "$PWD/manga:/manga" \
  -v "$PWD/downloads:/downloads" \
  ghcr.io/godgmn/komarr:edge
```

The repository also has [docker-compose.release.yml](../docker-compose.release.yml). Set `KOMARR_CONFIG`, `KOMARR_MANGA`, `KOMARR_DOWNLOADS`, `KOMARR_PORT`, `KOMARR_TAG`, and `TZ` in a local `.env` file if needed, then run `docker compose -f docker-compose.release.yml up -d`.

Open `http://localhost:8787` or `http://<host-LAN-IP>:8787`. Komarr listens on all interfaces by default. On a shared network, open **Settings → General → Security** and enable authentication. Keep `/config` and its backups private because they can contain API keys and download-client credentials.

## First-run setup

1. Open **Manga → Setup & Health**. Add a root folder under **Settings → Media Management**, using the container path `/manga` in the example above.
2. Add an indexer under **Settings → Indexers**. Nyaa.si can be added directly: choose **Nyaa**, set **Website URL** to `https://nyaa.si`, and set **Additional Parameters** to `&cats=3_1&filter=1` for Literature → English-translated (`&cats=3_0&filter=1` for all Literature). A Prowlarr Torznab or Newznab feed can also be entered directly for other trackers. Enable both RSS and automatic search for the feed if you want those functions.
3. For Prowlarr application synchronization, temporarily choose **Readarr** as the application type in Prowlarr. Point it to Komarr's URL and API key. Prowlarr currently maps manga sources through its Books category (`7000`). Native Komarr application support in Prowlarr is planned; the [tested Nyaa integration](integrations/prowlarr-nyaa.md) gives the exact proof and limitations.
4. Add qBittorrent, Transmission, SABnzbd, or NZBGet under **Settings → Download Clients**. Give Komarr access to the completed payload at `/downloads`. If the client reports another path, add a remote path mapping that translates it to the mounted local path.
5. Search for a manga under **Add Manga**, choose the correct AniList entry, set the root, and monitor volumes or chapters. Set its container, language, source, edition, size, seeder, and upgrade rules under **Settings → Manga Quality**. Use **Search Releases** on its detail page to review and manually grab a result. Komarr does not automatically grab an ambiguous title or coverage match.
6. For archives you already own, open **Manga → Library Scan**. Preview a folder directly under a configured root, choose the correct saved manga, review the file coverage, and register it in place. Komarr does not move or download those files during mapping.

Return to **Setup & Health** after each step. A failed AniList connection is a warning: saved manga and local collection management remain available. Disk space, database, root, indexer, client, and remote-path results explain what needs attention.

## Files, seeding, and backups

Torrent imports hardlink when the filesystem permits it and the setting is enabled. Otherwise Komarr copies the archive. It does not delete the download client's original payload, so seeding can continue. Conflicting destinations and uncertain coverage stay in manual review.

Use **System → Backup** to create a database and config backup. The backup ZIP must contain both `komarr.db` and `config.xml` for restore. Backups can contain credentials; keep them outside public shares. Restart Komarr after a restore so the staged database is loaded. Make a separate copy of `/config` before trying a restore or upgrade.

See [troubleshooting](troubleshooting.md) for startup, indexer, download, and import problems.
