#!/usr/bin/env python3
"""Live, read-only Komarr -> Prowlarr -> Nyaa Books search proof.

Run after building a Komarr image. Requires Docker and internet access to Nyaa.
The Prowlarr image is pinned to the digest tested with this script.
"""

import argparse
import json
import os
import pathlib
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
import xml.etree.ElementTree as ET

PROWLARR_IMAGE = (
    "lscr.io/linuxserver/prowlarr@sha256:"
    "f2b26429893d4c4cb71941b7ee50b1bdecd9d5f9f9e02d5410615e9f4f7c8d95"
)


def docker(*args):
    return subprocess.check_output(["docker", *args], text=True).strip()


def port(container, container_port):
    return docker("port", container, f"{container_port}/tcp").splitlines()[0].rsplit(":", 1)[1]


def wait_ready(url, container):
    for _ in range(120):
        try:
            with urllib.request.urlopen(url + "/ping", timeout=2) as response:
                if response.status == 200:
                    return
        except (urllib.error.URLError, OSError):
            pass
        if docker("inspect", "--format", "{{.State.Running}}", container) != "true":
            raise RuntimeError(f"{container} exited during startup")
        time.sleep(1)
    raise RuntimeError(f"{container} did not become ready within 120 seconds")


def api(url, key, path, method="GET", body=None):
    payload = None if body is None else json.dumps(body).encode()
    headers = {"X-Api-Key": key}
    if payload is not None:
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(
        url + path, data=payload, headers=headers, method=method
    )
    try:
        with urllib.request.urlopen(request, timeout=90) as response:
            data = response.read()
            return response.status, json.loads(data) if data else None
    except urllib.error.HTTPError as error:
        detail = error.read().decode(errors="replace").replace(key, "[masked]")
        raise RuntimeError(f"{method} {path}: HTTP {error.code}: {detail[:500]}") from error


def key_from_config(path):
    key = ET.parse(path).getroot().findtext("ApiKey")
    if not key:
        raise RuntimeError(f"Missing API key in {path}")
    return key


def set_field(schema, name, value):
    field = next(field for field in schema["fields"] if field["name"] == name)
    field["value"] = value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", default="komarr:local", help="Built Komarr image")
    parser.add_argument("--query", default="One Piece", help="Live manga query")
    parser.add_argument("--manga-id", type=int, help="AniList ID for manga title search")
    args = parser.parse_args()
    suffix = uuid.uuid4().hex[:8]
    network = f"komarr-proof-{suffix}"
    komarr = f"komarr-proof-{suffix}"
    prowlarr = f"prowlarr-proof-{suffix}"

    with tempfile.TemporaryDirectory(prefix="komarr-prowlarr-") as temporary:
        data = pathlib.Path(temporary)
        (data / "komarr").mkdir()
        (data / "prowlarr").mkdir()
        try:
            docker("network", "create", network)
            docker(
                "run", "-d", "--name", komarr, "--network", network,
                "--user", f"{os.getuid()}:{os.getgid()}", "-e", "HOME=/tmp",
                "-p", "127.0.0.1::8787", "-v", f"{data / 'komarr'}:/config",
                args.image,
            )
            docker(
                "run", "-d", "--name", prowlarr, "--network", network,
                "-e", f"PUID={os.getuid()}", "-e", f"PGID={os.getgid()}",
                "-p", "127.0.0.1::9696", "-v", f"{data / 'prowlarr'}:/config",
                PROWLARR_IMAGE,
            )
            komarr_url = f"http://127.0.0.1:{port(komarr, 8787)}"
            prowlarr_url = f"http://127.0.0.1:{port(prowlarr, 9696)}"
            wait_ready(komarr_url, komarr)
            wait_ready(prowlarr_url, prowlarr)
            komarr_key = key_from_config(data / "komarr/config.xml")
            prowlarr_key = key_from_config(data / "prowlarr/config.xml")

            _, profiles = api(prowlarr_url, prowlarr_key, "/api/v1/appprofile")
            profile = next(item for item in profiles if item["name"] == "Standard")
            _, indexer_schemas = api(
                prowlarr_url, prowlarr_key, "/api/v1/indexer/schema"
            )
            nyaa = next(item for item in indexer_schemas if item["name"] == "Nyaa.si")
            nyaa["appProfileId"] = profile["id"]
            api(prowlarr_url, prowlarr_key, "/api/v1/indexer/test", "POST", nyaa)
            api(prowlarr_url, prowlarr_key, "/api/v1/indexer", "POST", nyaa)

            _, app_schemas = api(
                prowlarr_url, prowlarr_key, "/api/v1/applications/schema"
            )
            app = next(item for item in app_schemas if item["implementation"] == "Readarr")
            app["name"] = "Komarr"
            set_field(app, "prowlarrUrl", f"http://{prowlarr}:9696")
            set_field(app, "baseUrl", f"http://{komarr}:8787")
            set_field(app, "apiKey", komarr_key)
            api(prowlarr_url, prowlarr_key, "/api/v1/applications/test", "POST", app)
            api(prowlarr_url, prowlarr_key, "/api/v1/applications", "POST", app)

            for _ in range(60):
                _, indexers = api(komarr_url, komarr_key, "/api/v1/indexer")
                if any("Nyaa.si" in item["name"] for item in indexers):
                    break
                time.sleep(1)
            else:
                raise RuntimeError("Prowlarr did not sync Nyaa to Komarr")

            path = "/api/v1/indexer/rawsearch?" + urllib.parse.urlencode(
                {"query": args.query}
            )
            _, result = api(komarr_url, komarr_key, path)
            books = [
                release for release in result["releases"]
                if any(7000 <= category < 8000 for category in release["categories"])
            ]
            if not books:
                raise RuntimeError(f"No Books results for {args.query!r}: {result['indexers']}")
            print(f"Prowlarr synced Nyaa to Komarr; {result['total']} raw releases for {args.query!r}.")
            for release in books[:3]:
                print(f"  {release['title']} | {release['indexer']} | {release['categories']}")

            if args.manga_id:
                _, manga = api(
                    komarr_url, komarr_key, "/api/v1/manga", "POST",
                    {"aniListId": args.manga_id},
                )
                _, known_items = api(
                    komarr_url, komarr_key, f"/api/v1/manga/{manga['id']}/items"
                )
                if manga.get("aniListVolumeCount") and len(known_items) != manga["aniListVolumeCount"]:
                    raise RuntimeError("Known AniList volumes were not created as manga items")
                print(f"Saved {len(known_items)} known items for {manga['preferredTitle']!r}.")
                _, search = api(
                    komarr_url, komarr_key, f"/api/v1/manga/{manga['id']}/search"
                )
                if not search["queries"] or len(search["queries"]) > 3:
                    raise RuntimeError(f"Unbounded manga queries: {search['queries']}")
                manga_books = [
                    release for release in search["releases"]
                    if any(7000 <= category < 8000 for category in release["categories"])
                ]
                if not manga_books:
                    raise RuntimeError(
                        f"No Books results for {manga['preferredTitle']!r}: "
                        f"{search['indexerErrors']}"
                    )
                print(
                    f"Manga search for {manga['preferredTitle']!r}: "
                    f"{len(search['queries'])} queries, {len(manga_books)} Books releases."
                )
                for release in manga_books[:3]:
                    print(f"  {release['title']} | {release['indexer']} | {release['categories']}")

                _, reviewed = api(
                    komarr_url, komarr_key,
                    f"/api/v1/manga/{manga['id']}/search/decisions",
                )
                if not reviewed["releases"]:
                    raise RuntimeError("Manga decision search returned no releases")
                if not any(
                    item["decision"]["rejections"] or
                    item["decision"]["reviewReasons"]
                    for item in reviewed["releases"]
                ):
                    raise RuntimeError("Manga decision search omitted rejection reasons")
                accepted = sum(
                    item["decision"]["canGrabAutomatically"]
                    for item in reviewed["releases"]
                )
                print(
                    f"Decision search: {reviewed['total']} releases, "
                    f"{accepted} automatic candidates, reasons on other results."
                )
                for item in reviewed["releases"][:3]:
                    decision = item["decision"]
                    reasons = decision["rejections"] or decision["reviewReasons"]
                    print(f"  {item['title']}: {reasons[0] if reasons else 'eligible'}")
        finally:
            for container in (prowlarr, komarr):
                subprocess.run(["docker", "rm", "-f", container],
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            subprocess.run(["docker", "network", "rm", network],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


if __name__ == "__main__":
    main()
