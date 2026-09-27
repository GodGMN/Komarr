#!/usr/bin/env python3
"""Check a populated alpha database, upgrade, and real backup restore."""

import argparse
import io
import json
import os
import pathlib
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid
import xml.etree.ElementTree as ET
import zipfile


def docker(*args):
    return subprocess.check_output(["docker", *args], text=True).strip()


def api(url, key, path, method="GET", body=None):
    data = None if body is None else json.dumps(body).encode()
    headers = {"X-Api-Key": key}
    if data is not None:
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        raise RuntimeError(f"{method} {path}: HTTP {error.code}: "
                           f"{error.read().decode(errors='replace')}") from error


def key_from_config(config):
    key = ET.parse(config).getroot().findtext("ApiKey")
    assert key, "Komarr did not create an API key"
    return key


def start(name, image, config, library):
    docker(
        "run", "-d", "--rm", "--name", name, "--user", f"{os.getuid()}:{os.getgid()}",
        "-e", "HOME=/tmp", "-p", "127.0.0.1::8787",
        "-v", f"{config}:/config", "-v", f"{library}:/manga", image,
    )
    port = docker("port", name, "8787/tcp").rsplit(":", 1)[-1]
    url = f"http://127.0.0.1:{port}"
    for _ in range(120):
        try:
            with urllib.request.urlopen(url + "/ping", timeout=2) as response:
                if response.status == 200:
                    return url
        except (urllib.error.URLError, TimeoutError, OSError):
            pass
        if subprocess.run(["docker", "inspect", name], stdout=subprocess.DEVNULL,
                          stderr=subprocess.DEVNULL).returncode:
            raise RuntimeError(f"{name} exited during startup")
        time.sleep(1)
    raise RuntimeError(f"{name} did not start: {docker('logs', '--tail', '50', name)}")


def stop(name):
    subprocess.run(["docker", "stop", name], stdout=subprocess.DEVNULL,
                   stderr=subprocess.DEVNULL, check=True)
    for _ in range(40):
        if subprocess.run(["docker", "inspect", name], stdout=subprocess.DEVNULL,
                          stderr=subprocess.DEVNULL).returncode:
            return
        time.sleep(0.25)
    raise RuntimeError(f"{name} was not removed after stopping")


def seed(config, library):
    """Add synthetic records offline to the released alpha's migrated schema."""
    archive = library / "BLAME!" / "BLAME! - Vol 001.cbz"
    archive.parent.mkdir()
    with zipfile.ZipFile(archive, "w") as output:
        output.writestr("page-01.txt", "Synthetic manga upgrade fixture\n")
    now = "2026-09-27 12:00:00"
    with sqlite3.connect(config / "komarr.db") as db:
        db.execute("PRAGMA foreign_keys=ON")
        db.execute(
            "INSERT INTO Manga (AniListId, TitleRomaji, PreferredTitle, CleanTitle, "
            "Synonyms, UserAliases, QualityPolicy, TrackingMode, Monitored, "
            "MonitorFutureItems, RootFolderPath, Path, Tags, Added) "
            "VALUES (?, ?, ?, ?, ?, ?, ?, 0, 1, 1, ?, ?, '[]', ?)",
            (30149, "BLAME!", "BLAME!", "blame", '["Blame"]', '["BLAME manga"]',
             '{"AllowedLanguages":["English"],"MinimumSeeders":2}',
             "/manga", "/manga/BLAME!", now),
        )
        manga_id = db.execute("SELECT Id FROM Manga WHERE AniListId=30149").fetchone()[0]
        db.execute(
            "INSERT INTO MangaItems (MangaId, Type, NumberDecimal, NumberText, "
            "Monitored, DiscoveredFrom, Added) VALUES (?, 0, 1, '1', 1, 2, ?)",
            (manga_id, now),
        )
        item_id = db.execute("SELECT Id FROM MangaItems WHERE MangaId=?", (manga_id,)).fetchone()[0]
        db.execute(
            "INSERT INTO MangaFiles (MangaId, Path, Size, Modified, DateAdded, "
            "Language, Source) VALUES (?, ?, ?, ?, ?, 'English', 'Digital')",
            (manga_id, str(archive).replace(str(library), "/manga", 1),
             archive.stat().st_size, now, now),
        )
        file_id = db.execute("SELECT Id FROM MangaFiles WHERE MangaId=?", (manga_id,)).fetchone()[0]
        db.execute("INSERT INTO MangaFileItems (MangaFileId, MangaItemId) VALUES (?, ?)",
                   (file_id, item_id))
        db.execute(
            "INSERT INTO MangaDownloads (MangaId, CoveredItemIds, IndexerId, Indexer, "
            "ReleaseGuid, ReleaseTitle, Protocol, DownloadClientId, DownloadClient, "
            "DownloadId, Status, Added) VALUES (?, ?, 7, 'Fixture', ?, ?, 1, 8, "
            "'qBittorrent', 'synthetic-hash', 2, ?)",
            (manga_id, json.dumps([item_id]), "fixture-release", "BLAME! v01", now),
        )
        download_id = db.execute("SELECT Id FROM MangaDownloads WHERE MangaId=?",
                                 (manga_id,)).fetchone()[0]
        db.execute(
            "INSERT INTO MangaHistory (MangaId, MangaDownloadId, EventType, Date, "
            "Message, ReleaseTitle, ReleaseGuid, IndexerId, CoveredItemIds) "
            "VALUES (?, ?, 4, ?, 'Synthetic import', 'BLAME! v01', "
            "'fixture-release', 7, ?)",
            (manga_id, download_id, now, json.dumps([item_id])),
        )
        assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"


def verify(url, config, library):
    key = key_from_config(config / "config.xml")
    assert any(folder["path"] == "/manga" for folder in api(url, key, "/api/v1/rootfolder"))
    assert any(client["name"] == "Fixture qBittorrent"
               for client in api(url, key, "/api/v1/downloadclient"))
    manga = next(item for item in api(url, key, "/api/v1/manga")
                 if item["aniListId"] == 30149)
    assert manga["preferredTitle"] == "BLAME!"
    assert "BLAME manga" in manga["userAliases"]
    assert manga["qualityPolicy"]["minimumSeeders"] == 2
    assert "English" in manga["qualityPolicy"]["allowedLanguages"]
    manga_id = manga["id"]
    items = api(url, key, f"/api/v1/manga/{manga_id}/items")
    files = api(url, key, f"/api/v1/manga/{manga_id}/files")
    downloads = api(url, key, f"/api/v1/manga/{manga_id}/downloads")
    history = api(url, key, f"/api/v1/manga/{manga_id}/history")
    wanted = api(url, key, f"/api/v1/manga/{manga_id}/wanted")
    assert len(items) == len(files) == len(downloads) == len(history) == 1
    assert items[0]["numberText"] == "1"
    assert files[0]["path"] == "/manga/BLAME!/BLAME! - Vol 001.cbz"
    assert downloads[0]["releaseGuid"] == "fixture-release"
    assert history[0]["message"] == "Synthetic import"
    assert wanted[0]["owned"] is True
    assert (library / "BLAME!" / "BLAME! - Vol 001.cbz").is_file()
    with sqlite3.connect(config / "komarr.db") as db:
        assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
    host = api(url, key, "/api/v1/config/host")
    assert host["instanceName"] == "Komarr populated upgrade fixture"
    return key


def wait_command(url, key, command):
    result = api(url, key, "/api/v1/command", "POST", {"name": command})
    for _ in range(90):
        status = api(url, key, f"/api/v1/command/{result['id']}")
        if status["status"].lower() == "completed":
            return
        if status["status"].lower() == "failed":
            raise RuntimeError(f"{command} failed: {status}")
        time.sleep(1)
    raise RuntimeError(f"{command} timed out")


def upload(url, key, archive):
    boundary = "komarr-upgrade-fixture"
    payload = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; "
               f"filename=\"{archive.name}\"\r\nContent-Type: application/zip\r\n\r\n").encode()
    payload += archive.read_bytes() + f"\r\n--{boundary}--\r\n".encode()
    request = urllib.request.Request(
        url + "/api/v1/system/backup/restore/upload", data=payload,
        headers={"X-Api-Key": key, "Content-Type": f"multipart/form-data; boundary={boundary}"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode(errors="replace")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--old-image", default="ghcr.io/godgmn/komarr@sha256:1700f0a5fa7b201e3817689e4add7490645b86dcc2289cf110870d353e02d7db")
    parser.add_argument("--new-image", default="komarr:local")
    args = parser.parse_args()
    name = f"komarr-populated-upgrade-{uuid.uuid4().hex[:8]}"
    with tempfile.TemporaryDirectory(prefix="komarr-populated-upgrade-") as temporary:
        root = pathlib.Path(temporary)
        config, restored, library = (root / part for part in ("config", "restored", "manga"))
        for path in (config, restored, library):
            path.mkdir()
        try:
            old_url = start(name, args.old_image, config, library)
            old_key = key_from_config(config / "config.xml")
            host = api(old_url, old_key, "/api/v1/config/host")
            host["instanceName"] = "Komarr populated upgrade fixture"
            api(old_url, old_key, "/api/v1/config/host/1", "PUT", host)
            quality = api(old_url, old_key, "/api/v1/qualityprofile")[0]["id"]
            metadata = api(old_url, old_key, "/api/v1/metadataprofile")[0]["id"]
            api(old_url, old_key, "/api/v1/rootfolder", "POST", {
                "name": "Manga", "path": "/manga", "defaultQualityProfileId": quality,
                "defaultMetadataProfileId": metadata,
            })
            client = next(item for item in api(old_url, old_key, "/api/v1/downloadclient/schema")
                          if item["implementation"] == "QBittorrent")
            client["name"] = "Fixture qBittorrent"
            client["enable"] = False
            for field in client["fields"]:
                if field["name"] == "host":
                    field["value"] = "fixture-qbit"
                elif field["name"] == "port":
                    field["value"] = 8080
            api(old_url, old_key, "/api/v1/downloadclient", "POST", client)
            stop(name)
            seed(config, library)
            old_url = start(name, args.old_image, config, library)
            verify(old_url, config, library)
            stop(name)
            new_url = start(name, args.new_image, config, library)
            new_key = verify(new_url, config, library)
            print(f"Upgrade passed: {args.old_image} -> {args.new_image}")

            wait_command(new_url, new_key, "Backup")
            backups = list((config / "Backups").rglob("*.zip")) + list((config / "backups").rglob("*.zip"))
            assert backups, "Backup command produced no ZIP"
            backup = max(backups, key=lambda path: path.stat().st_mtime)
            with zipfile.ZipFile(backup) as bundle:
                names = {path.lower() for path in bundle.namelist()}
                assert {"config.xml", "komarr.db"} <= names
            stop(name)

            restore_url = start(name, args.new_image, restored, library)
            restore_key = key_from_config(restored / "config.xml")
            original_config = (restored / "config.xml").read_bytes()
            with sqlite3.connect(restored / "komarr.db") as db:
                assert db.execute("SELECT count(*) FROM Manga").fetchone()[0] == 0
            invalid = root / "incomplete.zip"
            with zipfile.ZipFile(invalid, "w") as bundle:
                bundle.writestr("config.xml", original_config)
            code, _ = upload(restore_url, restore_key, invalid)
            assert code >= 400, f"Incomplete backup was accepted: HTTP {code}"
            assert (restored / "config.xml").read_bytes() == original_config
            assert not (restored / "komarr.db.restore").exists()
            with sqlite3.connect(restored / "komarr.db") as db:
                assert db.execute("SELECT count(*) FROM Manga").fetchone()[0] == 0
            code, result = upload(restore_url, restore_key, backup)
            assert code == 200 and result["restartRequired"] is True, result
            stop(name)
            restore_url = start(name, args.new_image, restored, library)
            verify(restore_url, restored, library)
            print("Backup restore and incomplete-backup rejection passed.")
        finally:
            subprocess.run(["docker", "rm", "-f", name], stdout=subprocess.DEVNULL,
                           stderr=subprocess.DEVNULL)


if __name__ == "__main__":
    main()
