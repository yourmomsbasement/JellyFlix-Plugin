#!/usr/bin/env python3
"""Adds a release to manifest.json, the Jellyfin plugin repository (catalog) file for JellyFlix.

Usage: update_manifest.py <manifest.json> <plugin.zip> <version> <source-url> <target-abi> [changelog]
"""
import datetime
import hashlib
import json
import sys

GUID = "c1a2981e-3ba2-44c5-9360-83cf293c2a13"
IMAGE_URL = "https://raw.githubusercontent.com/yourmomsbasement/JellyFlix-Plugin/main/images/jellyflix.png"


def main(manifest_path, zip_path, version, source_url, target_abi, changelog=""):
    try:
        with open(manifest_path, encoding="utf-8") as f:
            manifest = json.load(f)
    except FileNotFoundError:
        manifest = []

    plugin = next((p for p in manifest if p["guid"] == GUID), None)
    if plugin is None:
        plugin = {
            "guid": GUID,
            "name": "JellyFlix",
            "description": "Netflix-style household profiles, recommendations and automatic JellyFlix web client updates.",
            "overview": "One login, several profiles with their own watch history; personalized Top Picks and Because you watched rows; keeps the JellyFlix web client up to date.",
            "owner": "yourmomsbasement",
            "category": "General",
            "versions": [],
        }
        manifest.append(plugin)

    plugin["imageUrl"] = IMAGE_URL

    with open(zip_path, "rb") as f:
        checksum = hashlib.md5(f.read()).hexdigest()  # Jellyfin verifies plugin downloads with MD5

    plugin["versions"] = [v for v in plugin["versions"] if v["version"] != version]
    plugin["versions"].insert(0, {
        "version": version,
        "changelog": changelog,
        "targetAbi": target_abi,
        "sourceUrl": source_url,
        "checksum": checksum,
        "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    })

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    if len(sys.argv) < 6:
        sys.exit(__doc__)
    main(*sys.argv[1:7])
