#!/usr/bin/env python3
"""Disposable alpha flow with synthetic CBZ files and a local Torznab feed.

This starts fresh Komarr, Prowlarr, and qBittorrent containers. It never uses
copyrighted content. The local Torznab server is reachable only on the
temporary Docker bridge gateway.
"""

import argparse
import base64
import http.cookiejar
import hashlib
import importlib.util
import io
import json
import os
import pathlib
import re
import subprocess
import tempfile
import threading
import time
import urllib.parse
import urllib.request
import uuid
import xml.etree.ElementTree as ET
import zipfile
from email.utils import formatdate
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


PROOF_SPEC = importlib.util.spec_from_file_location(
    "prowlarr_proof", pathlib.Path(__file__).with_name("prowlarr-nyaa-smoke.py")
)
proof = importlib.util.module_from_spec(PROOF_SPEC)
PROOF_SPEC.loader.exec_module(proof)
NEWZNAB = "http://torznab.com/schemas/2015/feed"
ET.register_namespace("torznab", NEWZNAB)
QBIT_IMAGE = (
    "lscr.io/linuxserver/qbittorrent@sha256:"
    "caab2ebce30799ab342c374ea268fef4ec063a8ec3733a4e5d4a8e856ee32ce8"
)


def bencode(value):
    if isinstance(value, int):
        return b"i" + str(value).encode() + b"e"
    if isinstance(value, str):
        value = value.encode()
    if isinstance(value, bytes):
        return str(len(value)).encode() + b":" + value
    if isinstance(value, list):
        return b"l" + b"".join(bencode(item) for item in value) + b"e"
    return b"d" + b"".join(
        bencode(key) + bencode(value[key]) for key in sorted(value)
    ) + b"e"


def cbz_bytes(number):
    out = io.BytesIO()
    png = base64.b64decode(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jR1sAAAAASUVORK5CYII="
    )
    with zipfile.ZipFile(out, "w") as archive:
        archive.writestr(f"page-{number:02d}.png", png)
    return out.getvalue()


def torrent_bytes(filename, content):
    pieces = b"".join(
        hashlib.sha1(content[start:start + 16384]).digest()
        for start in range(0, len(content), 16384)
    )
    info = {"name": filename, "length": len(content), "piece length": 16384, "pieces": pieces}
    return bencode({"announce": "http://127.0.0.1:1/announce", "info": info})


class FixtureFeed:
    def __init__(self, gateway, downloads):
        self.visible = 1
        self.downloads = downloads
        self.releases = {}
        for number in (1, 2):
            filename = f"BLAME! v{number:02d}.cbz"
            content = cbz_bytes(number)
            (downloads / filename).write_bytes(content)
            self.releases[number] = {
                "filename": filename,
                "content": content,
                "torrent": torrent_bytes(filename, content),
            }
        self.server = ThreadingHTTPServer((gateway, 0), self.handler())
        self.url = f"http://{gateway}:{self.server.server_port}"
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)

    def handler(self):
        feed = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *args):
                pass

            def do_GET(self):
                path = urllib.parse.urlsplit(self.path)
                params = urllib.parse.parse_qs(path.query)
                if path.path.startswith("/torrent/"):
                    number = int(path.path.rsplit("/", 1)[-1])
                    data = feed.releases[number]["torrent"]
                    content_type = "application/x-bittorrent"
                elif params.get("t") == ["caps"]:
                    root = ET.Element("caps")
                    ET.SubElement(root, "server", {"version": "1.0", "title": "Komarr Fixture"})
                    searching = ET.SubElement(root, "searching")
                    ET.SubElement(searching, "search", {"available": "yes", "supportedParams": "q"})
                    categories = ET.SubElement(root, "categories")
                    books = ET.SubElement(categories, "category", {"id": "7000", "name": "Books"})
                    ET.SubElement(books, "subcat", {"id": "7030", "name": "Comics"})
                    data = ET.tostring(root, encoding="utf-8", xml_declaration=True)
                    content_type = "application/xml"
                elif path.path == "/api":
                    rss = ET.Element("rss", {"version": "2.0"})
                    channel = ET.SubElement(rss, "channel")
                    ET.SubElement(channel, "title").text = "Komarr Fixture"
                    numbers = range(1, feed.visible + 1)
                    for number in numbers:
                        release = feed.releases[number]
                        item = ET.SubElement(channel, "item")
                        title = f"BLAME! v{number:02d} (Digital) [English]"
                        url = f"{feed.url}/torrent/{number}"
                        ET.SubElement(item, "title").text = title
                        ET.SubElement(item, "guid", {"isPermaLink": "false"}).text = url
                        ET.SubElement(item, "link").text = url
                        ET.SubElement(item, "pubDate").text = formatdate(usegmt=True)
                        ET.SubElement(item, "size").text = str(len(release["content"]))
                        ET.SubElement(item, "enclosure", {
                            "url": url, "length": str(len(release["content"])),
                            "type": "application/x-bittorrent",
                        })
                        for name, value in (("category", "7000"), ("seeders", "8"), ("peers", "8")):
                            ET.SubElement(item, f"{{{NEWZNAB}}}attr", {"name": name, "value": value})
                    data = ET.tostring(rss, encoding="utf-8", xml_declaration=True)
                    content_type = "application/xml"
                else:
                    self.send_error(404)
                    return
                self.send_response(200)
                self.send_header("Content-Type", content_type)
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)

        return Handler

    def start(self):
        self.thread.start()

    def close(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()


def set_field(schema, name, value):
    next(field for field in schema["fields"] if field["name"] == name)["value"] = value


def qbit_password(container):
    for _ in range(60):
        logs = subprocess.run(["docker", "logs", container], text=True, capture_output=True)
        found = re.findall(r"temporary password.*?:\s*(\S+)", logs.stdout + logs.stderr, re.I)
        if found:
            return found[-1]
        time.sleep(1)
    raise RuntimeError("qBittorrent did not print its temporary admin password")


def wait_for(check, description, attempts=90):
    for _ in range(attempts):
        value = check()
        if value:
            return value
        time.sleep(2)
    raise RuntimeError(f"Timed out waiting for {description}")


def qbit_post(opener, url, path, fields):
    request = urllib.request.Request(
        url + path, data=urllib.parse.urlencode(fields).encode()
    )
    with opener.open(request, timeout=10) as response:
        return response.read()


def qbit_load_completed(opener, url, torrent_hash, release, downloads):
    # The tiny fixture is preloaded locally; no network transfer takes place.
    # Re-add with qBittorrent's documented skip_checking switch so the client
    # reports the payload as complete while retaining its copy for seeding.
    qbit_post(opener, url, "/api/v2/torrents/delete", {
        "hashes": torrent_hash.lower(), "deleteFiles": "false"
    })
    (downloads / release["filename"]).write_bytes(release["content"])
    boundary = "komarr-alpha-boundary"
    parts = []
    for name, value in (("skip_checking", "true"), ("category", "komarr"),
                        ("savepath", "/downloads")):
        parts.append(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n".encode())
    parts.append(
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"torrents\"; filename=\"fixture.torrent\"\r\n"
        "Content-Type: application/x-bittorrent\r\n\r\n".encode()
        + release["torrent"] + b"\r\n"
    )
    parts.append(f"--{boundary}--\r\n".encode())
    request = urllib.request.Request(
        url + "/api/v2/torrents/add", data=b"".join(parts),
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
    )
    with opener.open(request, timeout=10) as response:
        response.read()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", default="komarr:local")
    args = parser.parse_args()
    suffix = uuid.uuid4().hex[:8]
    network = f"komarr-alpha-{suffix}"
    names = {kind: f"{network}-{kind}" for kind in ("komarr", "prowlarr", "qbit")}
    feed = None
    with tempfile.TemporaryDirectory(prefix="komarr-alpha-") as temporary:
        data = pathlib.Path(temporary)
        for name in ("komarr", "prowlarr", "qbit", "downloads", "manga"):
            (data / name).mkdir()
        qbit_settings = data / "qbit/qBittorrent"
        qbit_settings.mkdir()
        (qbit_settings / "qBittorrent.conf").write_text(
            "[BitTorrent]\nSession\\DefaultSavePath=/downloads\n"
            "[Preferences]\nWebUI\\HostHeaderValidation=false\n"
            "WebUI\\CSRFProtection=false\nWebUI\\Port=8080\n"
        )
        try:
            proof.docker("network", "create", network)
            gateway = json.loads(proof.docker("network", "inspect", network))[0]["IPAM"]["Config"][0]["Gateway"]
            feed = FixtureFeed(gateway, data / "downloads")
            feed.start()
            proof.docker(
                "run", "-d", "--name", names["komarr"], "--network", network,
                "--user", f"{os.getuid()}:{os.getgid()}", "-e", "HOME=/tmp",
                "-p", "127.0.0.1::8787",
                "-v", f"{data / 'komarr'}:/config",
                "-v", f"{data / 'manga'}:/manga",
                "-v", f"{data / 'downloads'}:/downloads", args.image,
            )
            proof.docker(
                "run", "-d", "--name", names["prowlarr"], "--network", network,
                "-e", f"PUID={os.getuid()}", "-e", f"PGID={os.getgid()}",
                "-p", "127.0.0.1::9696", "-v", f"{data / 'prowlarr'}:/config",
                proof.PROWLARR_IMAGE,
            )
            proof.docker(
                "run", "-d", "--name", names["qbit"], "--network", network,
                "-e", f"PUID={os.getuid()}", "-e", f"PGID={os.getgid()}",
                "-e", "WEBUI_PORT=8080", "-p", "127.0.0.1::8080",
                "-v", f"{data / 'qbit'}:/config",
                "-v", f"{data / 'downloads'}:/downloads",
                QBIT_IMAGE,
            )
            komarr_url = f"http://127.0.0.1:{proof.port(names['komarr'], 8787)}"
            prowlarr_url = f"http://127.0.0.1:{proof.port(names['prowlarr'], 9696)}"
            proof.wait_ready(komarr_url, names["komarr"])
            proof.wait_ready(prowlarr_url, names["prowlarr"])
            komarr_key = proof.key_from_config(data / "komarr/config.xml")
            prowlarr_key = proof.key_from_config(data / "prowlarr/config.xml")
            password = qbit_password(names["qbit"])
            qbit_url = f"http://127.0.0.1:{proof.port(names['qbit'], 8080)}"
            login = urllib.request.Request(
                qbit_url + "/api/v2/auth/login",
                data=urllib.parse.urlencode({"username": "admin", "password": password}).encode(),
                headers={"Origin": qbit_url, "Referer": qbit_url + "/"},
            )
            qbit_opener = urllib.request.build_opener(
                urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar())
            )
            with qbit_opener.open(login, timeout=10) as response:
                if response.status not in (200, 204) or response.read().strip() not in (b"", b"Ok."):
                    raise RuntimeError("qBittorrent did not accept its temporary password")
            print("qBittorrent WebUI authentication passed.")

            _, profiles = proof.api(prowlarr_url, prowlarr_key, "/api/v1/appprofile")
            profile = next(item for item in profiles if item["name"] == "Standard")
            _, schemas = proof.api(prowlarr_url, prowlarr_key, "/api/v1/indexer/schema")
            indexer = next(item for item in schemas if item["name"] == "Generic Torznab")
            indexer["appProfileId"] = profile["id"]
            indexer["name"] = "Synthetic Manga"
            set_field(indexer, "baseUrl", feed.url)
            set_field(indexer, "apiPath", "/api")
            proof.api(prowlarr_url, prowlarr_key, "/api/v1/indexer/test", "POST", indexer)
            proof.api(prowlarr_url, prowlarr_key, "/api/v1/indexer", "POST", indexer)

            _, app_schemas = proof.api(prowlarr_url, prowlarr_key, "/api/v1/applications/schema")
            app = next(item for item in app_schemas if item["implementation"] == "Readarr")
            app["name"] = "Komarr"
            set_field(app, "prowlarrUrl", f"http://{names['prowlarr']}:9696")
            set_field(app, "baseUrl", f"http://{names['komarr']}:8787")
            set_field(app, "apiKey", komarr_key)
            proof.api(prowlarr_url, prowlarr_key, "/api/v1/applications/test", "POST", app)
            proof.api(prowlarr_url, prowlarr_key, "/api/v1/applications", "POST", app)
            wait_for(
                lambda: any(
                    "Synthetic Manga" in item["name"]
                    for item in proof.api(komarr_url, komarr_key, "/api/v1/indexer")[1]
                ), "Prowlarr indexer sync",
            )
            print("Prowlarr synced the synthetic Torznab indexer.")

            _, clients = proof.api(komarr_url, komarr_key, "/api/v1/downloadclient/schema")
            client = next(item for item in clients if item["implementation"] == "QBittorrent")
            client["name"] = "qBittorrent"
            client["enable"] = True
            set_field(client, "host", names["qbit"])
            set_field(client, "port", 8080)
            set_field(client, "username", "admin")
            set_field(client, "password", password)
            proof.api(komarr_url, komarr_key, "/api/v1/downloadclient/test", "POST", client)
            _, created_client = proof.api(komarr_url, komarr_key, "/api/v1/downloadclient", "POST", client)
            print("Download client:", {key: created_client.get(key) for key in ("name", "enable", "protocol", "implementation")})
            _, quality_profiles = proof.api(komarr_url, komarr_key, "/api/v1/qualityprofile")
            _, metadata_profiles = proof.api(komarr_url, komarr_key, "/api/v1/metadataprofile")
            proof.api(komarr_url, komarr_key, "/api/v1/rootfolder", "POST", {
                "name": "Manga", "path": "/manga",
                "defaultQualityProfileId": quality_profiles[0]["id"],
                "defaultMetadataProfileId": metadata_profiles[0]["id"],
            })
            _, manga = proof.api(
                komarr_url, komarr_key, "/api/v1/manga", "POST",
                {"aniListId": 30149, "rootFolderPath": "/manga"},
            )
            _, decisions = proof.api(
                komarr_url, komarr_key, f"/api/v1/manga/{manga['id']}/search/decisions"
            )
            for release in decisions["releases"]:
                print("Search:", release["title"], release.get("protocol"), release["decision"])
            release = next(
                item for item in decisions["releases"]
                if "v01" in item["title"] and item["decision"]["canGrabManually"]
            )
            _, items = proof.api(komarr_url, komarr_key, f"/api/v1/manga/{manga['id']}/items")
            first = next(item for item in items if item["numberText"] == "1")
            _, grabbed = proof.api(
                komarr_url, komarr_key, f"/api/v1/manga/{manga['id']}/grab", "POST",
                {"guid": release["guid"], "title": release["title"],
                 "indexerId": release["indexerId"], "itemId": first["id"],
                 "confirmManualReview": not release["decision"]["canGrabAutomatically"]},
            )
            print("qBittorrent accepted synthetic release:", grabbed["downloadId"])
            def torrent_state():
                with qbit_opener.open(qbit_url + "/api/v2/torrents/info", timeout=10) as response:
                    torrents = json.load(response)
                return next((item for item in torrents if item["hash"].upper() == grabbed["downloadId"].upper()), None)

            torrent = wait_for(torrent_state, "torrent appearing in qBittorrent", attempts=20)
            print("Torrent state:", torrent["state"], torrent["progress"], torrent["content_path"])
            if torrent["progress"] < 1:
                qbit_load_completed(
                    qbit_opener, qbit_url, grabbed["downloadId"],
                    feed.releases[1], data / "downloads",
                )
            wait_for(lambda: (state if (state := torrent_state()) and state["progress"] == 1 else None),
                     "synthetic torrent completion", attempts=30)
            print("Synthetic torrent is complete; source payload remains under /downloads.")
            for _ in range(20):
                proof.api(komarr_url, komarr_key, "/api/v1/command", "POST", {"name": "RefreshMonitoredDownloads"})
                time.sleep(2)
                _, files = proof.api(komarr_url, komarr_key, f"/api/v1/manga/{manga['id']}/files")
                if files:
                    break
            else:
                raise RuntimeError("Komarr did not import the completed synthetic torrent")
            print("Imported files:", [item["path"] for item in files])
            _, wanted = proof.api(komarr_url, komarr_key, f"/api/v1/manga/{manga['id']}/wanted")
            if not any(item["itemId"] == first["id"] and item["owned"] for item in wanted):
                raise RuntimeError("Volume 1 was imported without owned coverage")
            if (data / "downloads" / feed.releases[1]["filename"]).read_bytes() != feed.releases[1]["content"]:
                raise RuntimeError("Torrent source payload was changed or removed during import")
            if (retained := torrent_state()) is None or retained["progress"] != 1:
                raise RuntimeError("qBittorrent stopped retaining the completed torrent after import")
            print("Volume 1 is owned; qBittorrent's source payload remains intact.")

            feed.visible = 2
            proof.api(komarr_url, komarr_key, "/api/v1/command", "POST", {"name": "RssSync"})
            def second_download():
                _, downloads = proof.api(komarr_url, komarr_key, f"/api/v1/manga/{manga['id']}/downloads")
                return next((item for item in downloads if "v02" in item["releaseTitle"]
                             and item.get("downloadId")), None)

            second = wait_for(second_download, "automatic RSS grab of volume 2", attempts=45)
            print("RSS automatically grabbed volume 2:", second["downloadId"])
            def second_state():
                with qbit_opener.open(qbit_url + "/api/v2/torrents/info", timeout=10) as response:
                    torrents = json.load(response)
                return next((item for item in torrents if item["hash"].upper() == second["downloadId"].upper()), None)

            wait_for(second_state, "second torrent appearing in qBittorrent", attempts=20)
            qbit_load_completed(qbit_opener, qbit_url, second["downloadId"], feed.releases[2], data / "downloads")
            wait_for(lambda: (state if (state := second_state()) and state["progress"] == 1 else None),
                     "second synthetic torrent completion", attempts=30)
            for _ in range(20):
                proof.api(komarr_url, komarr_key, "/api/v1/command", "POST", {"name": "RefreshMonitoredDownloads"})
                time.sleep(2)
                _, wanted = proof.api(komarr_url, komarr_key, f"/api/v1/manga/{manga['id']}/wanted")
                if sum(item["owned"] for item in wanted) >= 2:
                    break
            else:
                raise RuntimeError("RSS volume 2 was not imported with owned coverage")
            if (data / "downloads" / feed.releases[2]["filename"]).read_bytes() != feed.releases[2]["content"]:
                raise RuntimeError("Second torrent source payload was changed or removed")
            if (retained := second_state()) is None or retained["progress"] != 1:
                raise RuntimeError("qBittorrent stopped retaining the second completed torrent")
            print("Volume 2 is owned; both torrent source payloads remain intact.")
        finally:
            if feed:
                feed.close()
            for container in names.values():
                subprocess.run(["docker", "rm", "-f", container], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            subprocess.run(["docker", "network", "rm", network], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


if __name__ == "__main__":
    main()
