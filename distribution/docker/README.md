# Komarr Docker image

Build from a clean checkout at the repository root:

```bash
docker build -f distribution/docker/Dockerfile -t komarr:local .
```

The image builds the .NET backend and React frontend in separate stages. The Dockerfile maps Docker Buildx architectures for Linux amd64, arm64, and arm/v7; this task verifies amd64. No prebuilt image is published yet.

Run it with a persistent config directory and shared paths for manga and completed downloads:

```bash
docker run -d --name komarr --restart unless-stopped \
  -p 8787:8787 \
  -v "$PWD/docker-config:/config" \
  -v /path/to/manga:/manga \
  -v /path/to/downloads:/downloads \
  komarr:local
```

Open `http://localhost:8787` on the host, or `http://<host-LAN-IP>:8787` from another device on the same network. Komarr listens on `0.0.0.0:8787` by default, and the Docker and Compose examples publish that port on all host interfaces. The first run creates `config.xml` and `komarr.db` under `/config`. Point Komarr at an empty config directory; Readarr and Librarr databases are not migrated. The `/manga` and `/downloads` paths should match what you configure in the UI and your download client.

Komarr currently starts with UI authentication disabled. On a shared network, open **Settings → General → Security** and enable authentication. **Manga → Setup & Health** shows a warning until it is enabled. Keep `/config` private: backups include the database and config, which can contain API keys and download-client credentials.

`docker compose up -d --build` uses the tracked Compose file and defaults to local `docker-config/`, `docker-manga/`, and `docker-downloads/` directories. Set `KOMARR_CONFIG`, `KOMARR_MANGA`, `KOMARR_DOWNLOADS`, `KOMARR_PORT`, or `TZ` in a local `.env` file to override them. These local directories and `.env` are gitignored.

To validate a built image, run `./scripts/smoke-docker.sh komarr:local`. It checks fresh database creation, the UI and API, saves a host setting, restarts the container, verifies persistence, then enables Basic authentication and checks that anonymous requests are blocked while the API key still works.
