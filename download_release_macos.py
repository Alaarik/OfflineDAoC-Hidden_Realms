import os
import sys
import urllib.request
import hashlib
import zipfile

MANIFEST_URL = "https://github.com/shadowofze/OfflineDAoC/releases/download/v0.35b/download-manifest.json"
BASE_URL = "https://github.com/shadowofze/OfflineDAoC/releases/download/v0.35b"
DOWNLOAD_DIR = os.path.abspath(".downloads/v0.35b")
TARGET_DIR = os.path.abspath("playable-v0.35b")

os.makedirs(DOWNLOAD_DIR, exist_ok=True)

manifest_path = os.path.join(DOWNLOAD_DIR, "download-manifest.json")
print("Fetching manifest...")
urllib.request.urlretrieve(MANIFEST_URL, manifest_path)

import json
with open(manifest_path, "r") as f:
    manifest = json.load(f)

print(f"Manifest loaded: {manifest['Title']}")
print(f"Total parts: {len(manifest['Parts'])}")

def verify_file(path, expected_bytes, expected_hash):
    if not os.path.exists(path):
        return False
    if os.path.getsize(path) != expected_bytes:
        return False
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while chunk := f.read(1024 * 1024):
            h.update(chunk)
    return h.hexdigest().lower() == expected_hash.lower()

part_paths = []
for part in manifest["Parts"]:
    p_name = part["Name"]
    p_bytes = part["Bytes"]
    p_hash = part["SHA256"]
    p_path = os.path.join(DOWNLOAD_DIR, p_name)
    part_paths.append(p_path)

    if verify_file(p_path, p_bytes, p_hash):
        print(f"Part verified from cache: {p_name}")
    else:
        print(f"Downloading {p_name} ({p_bytes / (1024*1024):.1f} MB)...")
        # Download with progress report
        req = urllib.request.urlopen(f"{BASE_URL}/{p_name}")
        downloaded = 0
        with open(p_path, "wb") as out_f:
            while chunk := req.read(4 * 1024 * 1024):
                out_f.write(chunk)
                downloaded += len(chunk)
                if downloaded % (50 * 1024 * 1024) < len(chunk):
                    print(f"  {downloaded / (1024*1024):.1f} / {p_bytes / (1024*1024):.1f} MB")
        if not verify_file(p_path, p_bytes, p_hash):
            print(f"Error: Verification failed for {p_name}")
            sys.exit(1)
        print(f"Part {p_name} verified successfully.")

zip_path = os.path.join(DOWNLOAD_DIR, "OfflineDAoC-v0.35b.zip")
print(f"Joining parts into {zip_path}...")
with open(zip_path, "wb") as out_zip:
    for p_path in part_paths:
        print(f"  Appending {os.path.basename(p_path)}...")
        with open(p_path, "rb") as in_p:
            while chunk := in_p.read(16 * 1024 * 1024):
                out_zip.write(chunk)

print("Verifying full archive SHA256...")
if not verify_file(zip_path, manifest["ArchiveBytes"], manifest["ArchiveSHA256"]):
    print("Archive SHA256 verification failed!")
    sys.exit(1)
print("Archive SHA256 verified successfully.")

print(f"Extracting archive to {TARGET_DIR}...")
os.makedirs(TARGET_DIR, exist_ok=True)
root_folder = manifest["RootFolder"] + "/"

with zipfile.ZipFile(zip_path, "r") as z:
    infolist = z.infolist()
    total = len(infolist)
    for idx, item in enumerate(infolist):
        if not item.filename.startswith(root_folder):
            continue
        rel_path = item.filename[len(root_folder):]
        if not rel_path:
            continue
        dest_path = os.path.join(TARGET_DIR, rel_path)
        if item.is_dir():
            os.makedirs(dest_path, exist_ok=True)
        else:
            os.makedirs(os.path.dirname(dest_path), exist_ok=True)
            with z.open(item) as src, open(dest_path, "wb") as dst:
                while chunk := src.read(4 * 1024 * 1024):
                    dst.write(chunk)
        if idx % 1000 == 0:
            print(f"  Extracted {idx}/{total} items...")

print("Extraction completed successfully!")
