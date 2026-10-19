"""Install private Nevernever & Edinburgh Fae mob models, skins, auras & animations.

  preview : python3 -B tools/pet-art/install_nevernever_mobs_art.py
  install : python3 -B tools/pet-art/install_nevernever_mobs_art.py install
  rollback: python3 -B tools/pet-art/install_nevernever_mobs_art.py rollback <backup-folder>

Non-destructive guarantee:
  - Zero stock monster rows, NIFs, skins, animation sets, or effect sets are modified.
  - Installs 20 private NIFs in figures/WardenFae_*.NIF
  - Packs 31 private DDS skins into figures/skins/skin099.mpk
  - Inserts private catalog rows into gamedata.mpk:
      * monsters.csv      : 1480..1503 (24 rows in unused gap < 2500, strict ascending order)
      * monnifs.csv       : 530..540, 551..559 (20 rows < 1000, Expansion Only = 0)
      * skins.csv         : 7135..7165 (31 rows, Archive = 99, Expansion Only = 0)
      * anims.csv         : 435..440 (6 rows with full spellcast & movement KFA slots)
      * canims.csv        : 435..440 (6 paired combat rows so melee never freezes)
      * moneffects.csv    : 200..205 (6 Bip01/Head/Hand bone-attached particle effects)
      * moneffectsets.csv : 198..209 (12 multi-effect ambient auras for Fae & World Bosses)
"""
from __future__ import annotations

import csv
import hashlib
import io
import json
import shutil
import struct
import sys
import time
import zlib
from pathlib import Path

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from daoc_catalog import CLIENT, archive  # noqa: E402

GAMEDATA = CLIENT / "gamedata.mpk"
SKINS_MPK = CLIENT / "figures" / "skins" / "skin099.mpk"
FIGURES = CLIENT / "figures"
WORK = HERE / "work" / "nevernever-mobs"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def require(ok: bool, message: str) -> None:
    if not ok:
        raise SystemExit(f"ERROR: {message}")


# ---------------------------------------------------------------------------
# Pure-Python Lossless DXT1 / DXT5 Block Recolorer & Decoder
# ---------------------------------------------------------------------------
def rgb565_to_rgb888(c: int) -> tuple[int, int, int]:
    r5 = (c >> 11) & 0x1F
    g6 = (c >> 5) & 0x3F
    b5 = c & 0x1F
    return ((r5 * 255 + 15) // 31, (g6 * 255 + 31) // 63, (b5 * 255 + 15) // 31)


def rgb888_to_rgb565(r: float, g: float, b: float) -> int:
    r5 = max(0, min(31, int(round(r * 31.0 / 255.0))))
    g6 = max(0, min(63, int(round(g * 63.0 / 255.0))))
    b5 = max(0, min(31, int(round(b * 31.0 / 255.0))))
    return (r5 << 11) | (g6 << 5) | b5


def eval_ramp(lum: float, stops: list[tuple[float, int, int, int]]) -> tuple[float, float, float]:
    if lum <= stops[0][0]:
        return float(stops[0][1]), float(stops[0][2]), float(stops[0][3])
    for i in range(len(stops) - 1):
        t0, r0, g0, b0 = stops[i]
        t1, r1, g1, b1 = stops[i + 1]
        if lum <= t1:
            f = 0.0 if t1 <= t0 else (lum - t0) / (t1 - t0)
            return (r0 + f * (r1 - r0), g0 + f * (g1 - g0), b0 + f * (b1 - b0))
    return float(stops[-1][1]), float(stops[-1][2]), float(stops[-1][3])


def transform_rgb(
    r: int, g: int, b: int, stops: list[tuple[float, int, int, int]], mode: str = "ramp"
) -> tuple[float, float, float]:
    lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
    mx = max(r, g, b) / 255.0
    val = 0.55 * lum + 0.45 * mx
    if mode == "redcap_cutthroat":
        # Preserve intense arterial crimson on red-dominant areas or top cap region
        if r > g * 1.25 and r > b * 1.25:
            return (min(255.0, 45.0 + 220.0 * val), 8.0 + 28.0 * (val ** 2), 10.0 + 32.0 * (val ** 2))
    elif mode == "redcap_marauder":
        if r > g * 1.20 and r > b * 1.20:
            return (min(255.0, 30.0 + 195.0 * val), 4.0 + 18.0 * (val ** 2), 8.0 + 22.0 * (val ** 2))
    return eval_ramp(val, stops)


def recolor_dds(
    src_bytes: bytes, stops: list[tuple[float, int, int, int]], mode: str = "ramp"
) -> bytes:
    require(src_bytes[:4] == b"DDS " and len(src_bytes) >= 128, "Invalid DDS header")
    height, width, _, _, mips = struct.unpack_from("<5I", src_bytes, 12)
    mips = max(1, mips)
    fourcc = src_bytes[84:88]
    require(fourcc in (b"DXT1", b"DXT5"), f"Unsupported DDS FourCC {fourcc!r}")
    block_size = 8 if fourcc == b"DXT1" else 16
    color_offset = 0 if fourcc == b"DXT1" else 8

    out = bytearray(src_bytes)
    offset = 128
    w, h = width, height
    for _level in range(mips):
        bw = max(1, (w + 3) // 4)
        bh = max(1, (h + 3) // 4)
        for _by in range(bh):
            for _bx in range(bw):
                coff = offset + color_offset
                c0, c1, lut = struct.unpack_from("<HHI", out, coff)
                r0, g0, b0 = rgb565_to_rgb888(c0)
                r1, g1, b1 = rgb565_to_rgb888(c1)
                nr0, ng0, nb0 = transform_rgb(r0, g0, b0, stops, mode)
                nr1, ng1, nb1 = transform_rgb(r1, g1, b1, stops, mode)
                nc0 = rgb888_to_rgb565(nr0, ng0, nb0)
                nc1 = rgb888_to_rgb565(nr1, ng1, nb1)

                if fourcc == b"DXT1":
                    if c0 > c1:
                        # 4-color opaque block: must maintain nc0 > nc1
                        if nc0 < nc1:
                            nc0, nc1 = nc1, nc0
                            lut ^= 0x55555555
                        elif nc0 == nc1:
                            if nc0 < 0xFFFF:
                                nc0 += 1
                                lut = 0x55555555
                            else:
                                nc1 -= 1
                                lut = 0x00000000
                    else:
                        # 3-color + 1-bit transparent block: must maintain nc0 <= nc1
                        if nc0 > nc1:
                            nc0, nc1 = nc1, nc0
                            # Swap index 0 (00) and index 1 (01), leave 2 (10) and 3 (11) intact
                            mask = (~(lut >> 1)) & 0x55555555
                            lut ^= mask
                else:
                    # DXT5: 8-byte alpha block precedes color block and is untouched
                    if (c0 > c1) != (nc0 > nc1) and nc0 != nc1:
                        nc0, nc1 = nc1, nc0
                        lut ^= 0x55555555

                struct.pack_into("<HHI", out, coff, nc0, nc1, lut)
                offset += block_size
        w = max(1, w // 2)
        h = max(1, h // 2)

    require(offset == len(src_bytes) and len(out) == len(src_bytes), "DDS payload size mismatch")
    return bytes(out)


def decode_dds_thumb(dds_bytes: bytes, thumb_size: int = 96) -> list[list[tuple[int, int, int]]]:
    """Decode top-level mip of a DXT1/DXT5 DDS into a thumb_size x thumb_size RGB grid."""
    height, width = struct.unpack_from("<II", dds_bytes, 12)
    fourcc = dds_bytes[84:88]
    block_size = 8 if fourcc == b"DXT1" else 16
    color_offset = 0 if fourcc == b"DXT1" else 8
    bw = max(1, (width + 3) // 4)
    bh = max(1, (height + 3) // 4)
    grid = [[(0, 0, 0) for _ in range(thumb_size)] for _ in range(thumb_size)]
    for ty in range(thumb_size):
        py = min(height - 1, int(ty * height / thumb_size))
        by, iy = py // 4, py % 4
        for tx in range(thumb_size):
            px = min(width - 1, int(tx * width / thumb_size))
            bx, ix = px // 4, px % 4
            coff = 128 + (by * bw + bx) * block_size + color_offset
            c0, c1, lut = struct.unpack_from("<HHI", dds_bytes, coff)
            r0, g0, b0 = rgb565_to_rgb888(c0)
            r1, g1, b1 = rgb565_to_rgb888(c1)
            idx = (lut >> (2 * (iy * 4 + ix))) & 0x3
            if idx == 0:
                rgb = (r0, g0, b0)
            elif idx == 1:
                rgb = (r1, g1, b1)
            elif idx == 2:
                rgb = ((2 * r0 + r1) // 3, (2 * g0 + g1) // 3, (2 * b0 + b1) // 3)
            else:
                rgb = ((r0 + 2 * r1) // 3, (g0 + 2 * g1) // 3, (b0 + 2 * b1) // 3)
            grid[ty][tx] = rgb
    return grid


def write_png(path: Path, width: int, height: int, rgb_bytes: bytes) -> None:
    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = bytearray()
    stride = width * 3
    for y in range(height):
        raw.append(0)
        raw.extend(rgb_bytes[y * stride : (y + 1) * stride])
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(raw), 6)) + chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


# ---------------------------------------------------------------------------
# Palettes & Specifications
# ---------------------------------------------------------------------------
PALETTES: dict[str, list[tuple[float, int, int, int]]] = {
    # 1. Bramble Creeper: dark blood-thorn bark -> bioluminescent viridian sap veins
    "bramble_creeper": [(0.0, 18, 10, 14), (0.35, 58, 22, 28), (0.68, 42, 112, 54), (1.0, 92, 235, 120)],
    # 2. Thorn Boggart: mottled deep forest moss, lichen & feral amber highlights
    "thorn_boggart": [(0.0, 16, 24, 14), (0.40, 48, 72, 36), (0.75, 112, 142, 62), (1.0, 218, 186, 88)],
    # 3. Briar Sprite: rose-thorn chitin & iridescent viridian/crimson wings
    "briar_sprite": [(0.0, 20, 36, 26), (0.42, 46, 128, 78), (0.75, 196, 52, 92), (1.0, 250, 168, 196)],
    # 4. Bramblewood Lurker: gnarled dark ironwood bark & glowing amber-crimson thorns
    "bramble_lurker": [(0.0, 18, 12, 10), (0.45, 64, 34, 24), (0.78, 148, 58, 28), (1.0, 242, 148, 52)],
    # 5. Redcap Cutthroat: pallid scarred grey-green skin + arterial crimson wool cap
    "redcap_cutthroat": [(0.0, 22, 24, 20), (0.45, 78, 86, 72), (0.78, 156, 42, 44), (1.0, 235, 58, 62)],
    # 6. Redcap Marauder: blackened iron-shod armor & clotted dark blood-crimson
    "redcap_marauder": [(0.0, 14, 12, 16), (0.45, 54, 48, 58), (0.76, 138, 22, 28), (1.0, 220, 42, 48)],
    # 7. Shadow Malk: abyssal obsidian fur with silver-violet eldritch stripes
    "shadow_malk": [(0.0, 8, 6, 14), (0.42, 34, 22, 56), (0.76, 112, 82, 176), (1.0, 196, 228, 255)],
    # 8. Malk Stalker of Arctis Tor: hoarfrost-white & glacial-blue pelt
    "winter_malk": [(0.0, 18, 36, 64), (0.40, 68, 128, 188), (0.75, 168, 218, 248), (1.0, 244, 252, 255)],
    # 9. Twilight Fetch: spectral violet-indigo shroud & starlight eyes
    "twilight_fetch": [(0.0, 14, 8, 28), (0.42, 68, 32, 124), (0.76, 148, 88, 224), (1.0, 228, 198, 255)],
    # 10. Rime-Crusted Fetch: frozen cyan-white shroud encrusted in glacial ice
    "rime_fetch": [(0.0, 10, 28, 52), (0.42, 42, 118, 178), (0.75, 132, 214, 248), (1.0, 238, 252, 255)],
    # 11. Bog Hag of the Sunder: waterlogged peat-green skin & river-weed shawl
    "bog_hag": [(0.0, 14, 24, 18), (0.42, 44, 78, 52), (0.75, 96, 138, 84), (1.0, 178, 208, 148)],
    # 12a. Rawhead & Bloodybones Body: flayed wet arterial crimson muscle & dark sinew
    "rawhead_body": [(0.0, 28, 4, 6), (0.38, 108, 12, 18), (0.72, 198, 28, 36), (1.0, 255, 118, 112)],
    # 12b. Rawhead & Bloodybones Head/Spikes: glistening bleached bone & wet crimson gore
    "rawhead_head": [(0.0, 48, 8, 12), (0.35, 142, 48, 46), (0.68, 218, 206, 192), (1.0, 255, 252, 246)],
    # 13. Glacial Frosthound: rime-white & ice-blue crystalline fur
    "glacial_frosthound": [(0.0, 16, 34, 58), (0.42, 62, 124, 186), (0.76, 158, 214, 248), (1.0, 242, 252, 255)],
    # 14. Froststalker Wendigo: gaunt frostbitten bone-white hide & bloodied ice-claws
    "frost_wendigo": [(0.0, 20, 28, 38), (0.40, 88, 112, 134), (0.74, 178, 198, 216), (1.0, 244, 248, 255)],
    # 15. Unseelie Sentinel: midnight-cobalt & rime-silver fluted Winter Sidhe armor
    "unseelie_sentinel": [(0.0, 12, 20, 38), (0.42, 42, 82, 142), (0.76, 128, 184, 232), (1.0, 224, 244, 255)],
    # 16. Lord Slate: High Sidhe Winter Knight black-ice plate & frost-rune silver
    "lord_slate": [(0.0, 8, 14, 28), (0.38, 28, 68, 128), (0.72, 98, 178, 242), (1.0, 238, 250, 255)],
    # 17. Seelie Scout: golden-skinned Summer Sidhe in burnished bronze & emerald leaf-mail
    "seelie_sentinel": [(0.0, 16, 34, 18), (0.40, 44, 108, 52), (0.74, 188, 148, 52), (1.0, 252, 228, 132)],
    # 18. Lord Taliesin: resplendent solar-gold & viridian High Sidhe regalia
    "lord_taliesin": [(0.0, 22, 36, 16), (0.38, 64, 124, 42), (0.72, 218, 172, 54), (1.0, 255, 244, 168)],
    # 19. Briar Thornsteed: emerald-moss equine coat & woven rose-briar barding
    "briar_thornsteed": [(0.0, 18, 32, 20), (0.42, 52, 98, 48), (0.74, 138, 54, 68), (1.0, 192, 232, 158)],
    # 20. Elder Gruff: horned caprine/satyr elder in carved oak-and-bronze rune armor
    "elder_gruff": [(0.0, 24, 16, 10), (0.42, 84, 56, 32), (0.75, 164, 118, 52), (1.0, 236, 202, 118)],
}

# Custom skin rows (7135..7165)
# (skin_id, label, dds_name, src_skin_id, palette_key, mode)
SKIN_SPECS = [
    ("7135", "WardenFae Bramble Creeper Body", "fae_bramble_body.dds", "1424", "bramble_creeper", "ramp"),
    ("7136", "WardenFae Bramble Creeper Arms", "fae_bramble_arms.dds", "1448", "bramble_creeper", "ramp"),
    ("7137", "WardenFae Bramble Creeper Legs", "fae_bramble_legs.dds", "1449", "bramble_creeper", "ramp"),
    ("7138", "WardenFae Thorn Boggart", "fae_thorn_boggart.dds", "3125", "thorn_boggart", "ramp"),
    ("7139", "WardenFae Briar Sprite", "fae_briar_sprite.dds", "3204", "briar_sprite", "ramp"),
    ("7140", "WardenFae Bramble Lurker", "fae_bramble_lurker.dds", "1382", "bramble_lurker", "ramp"),
    ("7141", "WardenFae Redcap Cutthroat", "fae_redcap_cutthroat.dds", "1399", "redcap_cutthroat", "redcap_cutthroat"),
    ("7142", "WardenFae Redcap Marauder", "fae_redcap_marauder.dds", "1399", "redcap_marauder", "redcap_marauder"),
    ("7143", "WardenFae Shadow Malk", "fae_shadow_malk.dds", "3029", "shadow_malk", "ramp"),
    ("7144", "WardenFae Winter Malk", "fae_winter_malk.dds", "1300", "winter_malk", "ramp"),
    ("7145", "WardenFae Twilight Fetch", "faewra.dds", "6730", "twilight_fetch", "ramp"),
    ("7146", "WardenFae Rime Fetch", "fae_rime_fetch.dds", "1262", "rime_fetch", "ramp"),
    ("7147", "WardenFae Bog Hag", "faehag.dds", "6776", "bog_hag", "ramp"),
    ("7148", "WardenFae Rawhead Body", "fae_rawhedbody.dds", "6781", "rawhead_body", "ramp"),
    ("7149", "WardenFae Rawhead Head", "fae_rawhedhead.dds", "6782", "rawhead_head", "ramp"),
    ("7150", "WardenFae Glacial Frosthound", "fwlf.dds", "6003", "glacial_frosthound", "ramp"),
    ("7151", "WardenFae Frost Wendigo", "fae_frost_wendigo.dds", "1162", "frost_wendigo", "ramp"),
    ("7152", "WardenFae Unseelie Body", "fae_unseelie_body.dds", "6086", "unseelie_sentinel", "ramp"),
    ("7153", "WardenFae Unseelie Head", "fae_unseelie_head.dds", "6088", "unseelie_sentinel", "ramp"),
    ("7154", "WardenFae Unseelie Hair", "fae_unseelie_hair.dds", "6087", "unseelie_sentinel", "ramp"),
    ("7155", "WardenFae Lord Slate Body", "fae_slate_body.dds", "6083", "lord_slate", "ramp"),
    ("7156", "WardenFae Lord Slate Head", "fae_slate_head.dds", "6085", "lord_slate", "ramp"),
    ("7157", "WardenFae Lord Slate Hair", "fae_slate_hair.dds", "6084", "lord_slate", "ramp"),
    ("7158", "WardenFae Seelie Body", "fae_seelie_body.dds", "6086", "seelie_sentinel", "ramp"),
    ("7159", "WardenFae Seelie Head", "fae_seelie_head.dds", "6088", "seelie_sentinel", "ramp"),
    ("7160", "WardenFae Seelie Hair", "fae_seelie_hair.dds", "6087", "seelie_sentinel", "ramp"),
    ("7161", "WardenFae Lord Taliesin Body", "fae_taliesin_body.dds", "6083", "lord_taliesin", "ramp"),
    ("7162", "WardenFae Lord Taliesin Head", "fae_taliesin_head.dds", "6085", "lord_taliesin", "ramp"),
    ("7163", "WardenFae Lord Taliesin Hair", "fae_taliesin_hair.dds", "6084", "lord_taliesin", "ramp"),
    ("7164", "WardenFae Briar Thornsteed", "fae_briar_thornsteed.dds", "1078", "briar_thornsteed", "ramp"),
    ("7165", "WardenFae Elder Gruff", "fae_elder_gruff.dds", "1718", "elder_gruff", "ramp"),
]

# Custom animation sets (435..440) added to BOTH anims.csv and canims.csv
# (new_set_id, label, base_set_id, cast_triplet_kfas, canims_fill_kfas)
ANIM_SPECS = [
    ("435", "WardenFae Bramble Creeper", "126", ("1369", "1368", "1369"), ("1369", "1369", "1369")),
    ("436", "WardenFae Twilight/Rime Fetch", "70", ("742", "743", "744"), ("739", "739", "739")),
    ("437", "WardenFae Rawhead BossHulker", "71", ("754", "755", "756"), ("751", "749", "750")),
    ("438", "WardenFae Winter Malk Stalker", "83", ("1195", "1196", "1197"), ("861", "862", "861")),
    ("439", "WardenFae Briar Thornsteed", "41", ("72", "73", "71"), ("71", "72", "71")),
    ("440", "WardenFae Bramblewood Lurker", "89", ("926", "927", "928"), ("921", "922", "921")),
]

# Custom moneffects.csv rows (200..205) attaching particle NIFs to Bip01 / Head / R Hand
MONEFFECT_SPECS = [
    ["200", "WardenFae Thornweed Bip01", "58", "Bip01", "2500", "100", "1", "1", "0", "0", "0", "", ""],
    ["201", "WardenFae RedSkull Head", "4", "Bip01 Head", "2500", "100", "0", "1", "0", "0", "1", "", ""],
    ["202", "WardenFae Blood RHand", "1", "Bip01 R Hand", "2500", "100", "0", "1", "0", "0", "1", "", ""],
    ["203", "WardenFae ShadowSmoke Bip01", "185", "Bip01", "2500", "100", "0", "1", "0", "0", "0", "", ""],
    ["204", "WardenFae BlueRings Bip01", "173", "Bip01", "2500", "100", "1", "1", "0", "0", "0", "", ""],
    ["205", "WardenFae GreenRings Bip01", "174", "Bip01", "2500", "100", "1", "1", "0", "0", "0", "", ""],
]

# Custom moneffectsets.csv rows (198..209)
MONEFFECTSET_SPECS = [
    ["198", "WardenFae Bramble Creeper Aura", "200", "120", "0", "0", "0", "0", "", ""],
    ["199", "WardenFae Rawhead Boss Aura", "201", "10", "1", "0", "0", "0", "", ""],
    ["200", "WardenFae Redcap Marauder Aura", "202", "1", "0", "0", "0", "0", "", ""],
    ["201", "WardenFae Shadow Malk Aura", "203", "6", "0", "0", "0", "0", "", ""],
    ["202", "WardenFae Bog Hag Aura", "19", "102", "0", "0", "0", "0", "", ""],
    ["203", "WardenFae Frost Wendigo Aura", "24", "123", "2", "0", "0", "0", "", ""],
    ["204", "WardenFae Winter Sidhe Aura", "204", "123", "0", "0", "0", "0", "", ""],
    ["205", "WardenFae Summer Sidhe Aura", "205", "131", "0", "0", "0", "0", "", ""],
    ["206", "WardenFae Waystone Portal Aura", "137", "25", "2", "0", "0", "0", "", ""],
    ["207", "WardenFae Way-Wisp Lantern", "2", "25", "0", "0", "0", "0", "", ""],
    ["208", "WardenFae Will-o-the-Wisp", "3", "22", "0", "0", "0", "0", "", ""],
    ["209", "WardenFae Golden Willow Wisp", "5", "164", "0", "0", "0", "0", "", ""],
]

# Private monnifs.csv rows (20 rows in unused gaps 530..540 and 551..559, all < 1000)
# (nif_id, label, nif_stem, src_nif_id, anim_set_id, tex_renames)
MONNIF_SPECS = [
    ("530", "WardenFae Bramble Creeper", "WardenFae_BrambleCreeper", "382", "435", {}),
    ("531", "WardenFae Thorn Boggart", "WardenFae_ThornBoggart", "544", "216", {}),
    ("532", "WardenFae Briar Sprite", "WardenFae_BriarSprite", "541", "212", {}),
    ("533", "WardenFae Bramble Lurker", "WardenFae_BrambleLurker", "340", "440", {}),
    ("534", "WardenFae Redcap Cutthroat", "WardenFae_RedcapCutthroat", "361", "105", {}),
    ("535", "WardenFae Redcap Marauder", "WardenFae_RedcapMarauder", "361", "105", {}),
    ("536", "WardenFae Shadow Malk", "WardenFae_ShadowMalk", "520", "203", {}),
    ("537", "WardenFae Winter Malk", "WardenFae_WinterMalk", "332", "438", {}),
    ("538", "WardenFae Twilight Fetch", "WardenFae_TwilightFetch", "788", "436", {b"Wraith.tga": b"faewra.dds"}),
    ("539", "WardenFae Rime Fetch", "WardenFae_RimeFetch", "319", "436", {}),
    ("540", "WardenFae Bog Hag", "WardenFae_BogHag", "810", "67", {b"DR_Hag.dds": b"faehag.dds"}),
    ("551", "WardenFae Rawhead Boss", "WardenFae_RawheadBoss", "812", "437", {
        b"bosshulkerbody.dds": b"fae_rawhedbody.dds",
        b"bosshulkerhead.dds": b"fae_rawhedhead.dds",
    }),
    ("552", "WardenFae Glacial Frosthound", "WardenFae_GlacialFrosthound", "529", "209", {b"wolf.tga": b"fwlf.dds"}),
    ("553", "WardenFae Frost Wendigo", "WardenFae_FrostWendigo", "291", "35", {}),
    ("554", "WardenFae Unseelie Sentinel", "WardenFae_UnseelieSentinel", "663", "233", {}),
    ("555", "WardenFae Lord Slate", "WardenFae_LordSlate", "665", "233", {}),
    ("556", "WardenFae Seelie Sentinel", "WardenFae_SeelieSentinel", "663", "233", {}),
    ("557", "WardenFae Lord Taliesin", "WardenFae_LordTaliesin", "665", "233", {}),
    ("558", "WardenFae Briar Thornsteed", "WardenFae_BriarThornsteed", "285", "439", {}),
    ("559", "WardenFae Elder Gruff", "WardenFae_ElderGruff", "477", "182", {}),
]

# Private monsters.csv rows (1480..1503, in unused gap 1441..1562 < 2500)
# (model_id, label, src_monster_id, nif_id, skin_overrides_dict, moneffectset_id, extra_cols_dict)
MONSTER_SPECS = [
    ("1480", "WardenFae Bramble Creeper", "862", "530", {3: "7135", 5: "7136", 8: "7137"}, "198", {}),
    ("1481", "WardenFae Thorn Boggart", "112", "531", {3: "7138"}, "156", {}),
    ("1482", "WardenFae Briar Sprite", "1623", "532", {3: "7139"}, "157", {}),
    ("1483", "WardenFae Bramblewood Lurker", "767", "533", {3: "7140"}, "118", {}),
    ("1484", "WardenFae Redcap Cutthroat", "821", "534", {3: "7141"}, "1", {}),
    ("1485", "WardenFae Redcap Marauder", "821", "535", {3: "7142"}, "200", {12: "115"}),
    ("1486", "WardenFae Shadow Malk", "133", "536", {3: "7143"}, "201", {}),
    ("1487", "WardenFae Malk Stalker", "695", "537", {3: "7144"}, "121", {}),
    ("1488", "WardenFae Twilight Fetch", "655", "538", {3: "7145"}, "6", {32: "15", 33: "30", 39: "1"}),
    ("1489", "WardenFae Rime-Crusted Fetch", "655", "539", {3: "7146"}, "2", {32: "15", 33: "30", 39: "1"}),
    ("1490", "WardenFae Way-Wisp Lantern", "907", "399", {}, "207", {}),
    ("1491", "WardenFae Will-o-the-Wisp", "909", "399", {}, "208", {}),
    ("1492", "WardenFae Golden Willow Wisp", "907", "399", {}, "209", {}),
    ("1493", "WardenFae Bog Hag", "643", "540", {3: "7147"}, "202", {}),
    ("1494", "WardenFae Rawhead & Bloodybones", "661", "551", {3: "7148", 4: "7149"}, "199", {12: "135", 13: "115"}),
    ("1495", "WardenFae Glacial Frosthound", "1747", "552", {3: "7150"}, "121", {}),
    ("1496", "WardenFae Froststalker Wendigo", "4", "553", {3: "7151"}, "203", {12: "140", 13: "145"}),
    ("1497", "WardenFae Unseelie Sentinel", "1912", "554", {3: "7152", 4: "7153", 5: "7154"}, "204", {}),
    ("1498", "WardenFae Lord Slate", "1915", "555", {3: "7155", 4: "7156", 5: "7157"}, "204", {12: "112"}),
    ("1499", "WardenFae Seelie Scout", "1912", "556", {3: "7158", 4: "7159", 5: "7160"}, "125", {}),
    ("1500", "WardenFae Lord Taliesin", "1915", "557", {3: "7161", 4: "7162", 5: "7163"}, "205", {12: "112"}),
    ("1501", "WardenFae Briar Thornsteed", "463", "558", {3: "7164"}, "122", {}),
    ("1502", "WardenFae Elder Gruff", "1192", "559", {3: "7165"}, "205", {12: "118"}),
    ("1503", "WardenFae Waystone Portal", "907", "399", {}, "206", {12: "100"}),
]


# ---------------------------------------------------------------------------
# Catalog CSV Helpers
# ---------------------------------------------------------------------------
def csv_rows(data: bytes) -> list[list[str]]:
    return list(csv.reader(io.StringIO(data.decode("latin1"))))


def row_to_line(row: list[str]) -> str:
    buf = io.StringIO()
    csv.writer(buf, lineterminator="").writerow(row)
    return buf.getvalue()


def insert_rows_in_id_order(data: bytes, new_rows: list[list[str]], terminator_id: int | None = None) -> bytes:
    """Insert rows in strict ascending numeric order without blank lines or touching stock rows."""
    lines = data.decode("latin1").split("\r\n")
    for row in sorted(new_rows, key=lambda r: int(r[0])):
        rid = int(row[0])
        if terminator_id is not None:
            require(rid < terminator_id, f"Row ID {rid} must be below terminator {terminator_id}")
        numeric = [
            (i, int(line.split(",", 1)[0].strip()))
            for i, line in enumerate(lines)
            if line.split(",", 1)[0].strip().isdigit()
        ]
        require(not any(n == rid for _, n in numeric), f"Catalog ID {rid} already exists")
        lower = [i for i, n in numeric if n < rid]
        insert_idx = (max(lower) + 1) if lower else numeric[0][0]
        added = row_to_line(row)
        before_snapshot = list(lines)
        lines = lines[:insert_idx] + [added] + lines[insert_idx:]
        require(lines[:insert_idx] + lines[insert_idx + 1 :] == before_snapshot, "Stock row mutated during insert")

    # Verify ascending order & no internal blank lines
    ids = [int(r[0].strip()) for r in csv_rows("\r\n".join(lines).encode("latin1")) if r and r[0].strip().isdigit()]
    require(all(a <= b for a, b in zip(ids, ids[1:])), "Catalog IDs out of ascending order")
    if terminator_id is not None:
        require(ids[-1] == terminator_id, f"Terminator {terminator_id} is no longer last")
    require(all(line.strip() for line in lines[:-1]), "Blank line introduced inside catalog CSV")
    return "\r\n".join(lines).encode("latin1")


# ---------------------------------------------------------------------------
# Builder & Contact Sheet Generator
# ---------------------------------------------------------------------------
def build_all():
    gname, gentries = archive.read(GAMEDATA.read_bytes())
    gfiles = {e.name.lower(): e.data for e in gentries}
    tables = {
        name: {r[0].strip(): r for r in csv_rows(gfiles[name]) if r and r[0].strip().isdigit()}
        for name in (
            "monsters.csv",
            "monnifs.csv",
            "skins.csv",
            "anims.csv",
            "canims.csv",
            "moneffects.csv",
            "moneffectsets.csv",
        )
    }

    # 1. Load source DDS skins from figures/skins/skin*.mpk and generate recolored DDS blobs
    mpk_cache: dict[int, dict[str, archive.Entry]] = {}
    built_skins: dict[str, bytes] = {}
    skin_rows: list[list[str]] = []
    preview_pairs: list[tuple[str, bytes, bytes]] = []

    for skin_id, label, dds_name, src_skin_id, pal_key, mode in SKIN_SPECS:
        require(skin_id not in tables["skins.csv"], f"Skin ID {skin_id} already used")
        src_row = tables["skins.csv"][src_skin_id]
        arch_num = int(src_row[4].strip())
        if arch_num not in mpk_cache:
            mpk_path = CLIENT / "figures" / "skins" / f"skin{arch_num:03d}.mpk"
            _, entries = archive.read(mpk_path.read_bytes())
            mpk_cache[arch_num] = {e.name.lower(): e for e in entries}
        by_lower = mpk_cache[arch_num]
        src_fname = src_row[2].strip()
        stem = Path(src_fname).stem.lower()
        src_entry = by_lower.get(src_fname.lower()) or by_lower.get(stem + ".dds") or by_lower.get(stem + ".tga")
        require(src_entry is not None, f"Missing source skin {src_fname} in skin{arch_num:03d}.mpk")

        recolored = recolor_dds(src_entry.data, PALETTES[pal_key], mode)
        built_skins[dds_name] = recolored
        preview_pairs.append((label.replace("WardenFae ", ""), src_entry.data, recolored))

        new_srow = list(src_row)
        new_srow[0] = skin_id
        new_srow[1] = label
        new_srow[2] = dds_name
        new_srow[4] = "99"  # skin099.mpk
        new_srow[5] = "0"   # Expansion Only = 0
        skin_rows.append(new_srow)

    # Pack skin099.mpk
    sname, sentries = archive.read(SKINS_MPK.read_bytes())
    existing_skins = {e.name.lower(): e for e in sentries}
    for dds_name in built_skins:
        require(dds_name.lower() not in existing_skins, f"{dds_name} already in skin099.mpk; rollback first")
    ref_entry = sentries[0]
    new_skin_entries = sorted(
        sentries + [archive.Entry(k, v, ref_entry.timestamp, ref_entry.flags) for k, v in built_skins.items()],
        key=lambda e: e.name.lower(),
    )
    skin_mpk_blob = archive.write(sname, new_skin_entries)
    archive.verify_memory_image(skin_mpk_blob)

    # 2. Build private creature NIFs and monnifs.csv rows
    fig_index = {p.name.lower(): p for p in FIGURES.rglob("*") if p.is_file()}
    built_nifs: dict[Path, bytes] = {}
    monnif_rows: list[list[str]] = []

    for nif_id, label, nif_stem, src_nif_id, anim_set_id, tex_renames in MONNIF_SPECS:
        require(nif_id not in tables["monnifs.csv"], f"monnifs ID {nif_id} already used")
        src_nrow = tables["monnifs.csv"][src_nif_id]
        src_stem = src_nrow[2].strip().lower()
        src_path = fig_index.get(src_stem + ".nif")
        require(src_path is not None, f"Missing source NIF for {src_stem}")
        raw_nif = src_path.read_bytes()
        patched_nif = bytearray(raw_nif)
        for old_tex, new_tex in tex_renames.items():
            require(len(old_tex) == len(new_tex), f"Texture rename length mismatch: {old_tex} vs {new_tex}")
            require(raw_nif.count(old_tex) >= 1, f"Texture {old_tex!r} not found in {src_path.name}")
            patched_nif = bytearray(bytes(patched_nif).replace(old_tex, new_tex))
        require(len(patched_nif) == len(raw_nif), f"NIF size changed for {nif_stem}")
        target_nif_path = FIGURES / f"{nif_stem}.NIF"
        built_nifs[target_nif_path] = bytes(patched_nif)

        nrow = list(src_nrow)
        nrow[0] = nif_id
        nrow[1] = label
        nrow[2] = nif_stem
        nrow[3] = anim_set_id
        nrow[9] = "0"  # Expansion Only = 0
        monnif_rows.append(nrow)

    # 3. Build paired anims.csv and canims.csv rows (435..440)
    anim_rows: list[list[str]] = []
    canim_rows: list[list[str]] = []
    cast_slot_starts = (18, 34, 37, 40, 43, 46, 49, 52)
    for set_id, label, base_set_id, cast_kfas, canim_kfas in ANIM_SPECS:
        require(set_id not in tables["anims.csv"] and set_id not in tables["canims.csv"], f"Anim set {set_id} taken")
        arow = list(tables["anims.csv"][base_set_id])
        arow[0] = set_id
        arow[1] = label
        for cstart in cast_slot_starts:
            arow[cstart : cstart + 3] = list(cast_kfas)
        anim_rows.append(arow)

        crow = list(tables["canims.csv"][base_set_id])
        crow[0] = set_id
        crow[1] = label
        for idx in range(10, 60):
            if not crow[idx].strip() or crow[idx].strip() == "0":
                crow[idx] = canim_kfas[(idx - 10) % 3]
        canim_rows.append(crow)

    # 4. Build monsters.csv rows (1480..1503)
    monster_rows: list[list[str]] = []
    for model_id, label, src_mid, nif_id, skin_overrides, moneff_id, extra_cols in MONSTER_SPECS:
        require(model_id not in tables["monsters.csv"], f"Monster ID {model_id} already used")
        require(int(model_id) < 2500, f"Monster ID {model_id} must be < 2500")
        mrow = list(tables["monsters.csv"][src_mid])
        mrow[0] = model_id
        mrow[1] = label
        mrow[2] = nif_id
        # Clear classic body/head/arms/gloves/lbody/legs skin slots when overriding
        if skin_overrides:
            for s_col in range(3, 9):
                mrow[s_col] = "0"
            for s_col, s_val in skin_overrides.items():
                mrow[s_col] = s_val
        mrow[40] = moneff_id  # Mon Effect (moneffectsets.csv ID)
        for col_idx, col_val in extra_cols.items():
            mrow[col_idx] = col_val
        monster_rows.append(mrow)

    # 5. Update gamedata.mpk tables
    replaced = {
        "monsters.csv": insert_rows_in_id_order(gfiles["monsters.csv"], monster_rows, None),
        "monnifs.csv": insert_rows_in_id_order(gfiles["monnifs.csv"], monnif_rows, 1000),
        "skins.csv": insert_rows_in_id_order(gfiles["skins.csv"], skin_rows, None),
        "anims.csv": insert_rows_in_id_order(gfiles["anims.csv"], anim_rows, None),
        "canims.csv": insert_rows_in_id_order(gfiles["canims.csv"], canim_rows, None),
        "moneffects.csv": insert_rows_in_id_order(gfiles["moneffects.csv"], MONEFFECT_SPECS, None),
        "moneffectsets.csv": insert_rows_in_id_order(gfiles["moneffectsets.csv"], MONEFFECTSET_SPECS, None),
    }

    new_gentries = [
        archive.Entry(e.name, replaced.get(e.name.lower(), e.data), e.timestamp, e.flags)
        for e in gentries
    ]
    game_blob = archive.write(gname, new_gentries)
    archive.verify_memory_image(game_blob)
    for e in archive.read(game_blob)[1]:
        if e.name.lower() not in replaced:
            require(gfiles[e.name.lower()] == e.data, f"Unrelated gamedata entry changed: {e.name}")

    return built_nifs, skin_mpk_blob, game_blob, preview_pairs


def render_contact_sheet(preview_pairs: list[tuple[str, bytes, bytes]], out_path: Path) -> None:
    """Render a before/after contact sheet of custom DDS creature skins."""
    cols = 4
    rows = (len(preview_pairs) + cols - 1) // cols
    thumb = 64
    cell_w = thumb * 2 + 24
    cell_h = thumb + 20
    width = cols * cell_w + 16
    height = rows * cell_h + 16
    canvas = [[(20, 22, 28) for _ in range(width)] for _ in range(height)]

    for idx, (_label, before_dds, after_dds) in enumerate(preview_pairs):
        r = idx // cols
        c = idx % cols
        ox = 12 + c * cell_w
        oy = 12 + r * cell_h
        b_grid = decode_dds_thumb(before_dds, thumb)
        a_grid = decode_dds_thumb(after_dds, thumb)
        for y in range(thumb):
            for x in range(thumb):
                canvas[oy + y][ox + x] = b_grid[y][x]
                canvas[oy + y][ox + thumb + 8 + x] = a_grid[y][x]

    flat = bytearray()
    for row in canvas:
        for pr, pg, pb in row:
            flat.extend((pr, pg, pb))
    write_png(out_path, width, height, bytes(flat))


def preview() -> None:
    built_nifs, skin_mpk_blob, game_blob, preview_pairs = build_all()
    sheet_path = HERE / "work" / "nevernever_mob_skins_preview.png"
    render_contact_sheet(preview_pairs, sheet_path)
    print(f"Verified {len(built_nifs)} private NIFs, {len(SKIN_SPECS)} custom DDS skins, and {len(MONSTER_SPECS)} monster rows.")
    print(f"Contact sheet written to: {sheet_path}")
    print(f"gamedata.mpk sha256 -> {sha(game_blob)[:16]}... | skin099.mpk sha256 -> {sha(skin_mpk_blob)[:16]}...")


def install() -> None:
    built_nifs, skin_mpk_blob, game_blob, preview_pairs = build_all()
    for nif_path in built_nifs:
        require(not nif_path.exists(), f"{nif_path.name} already exists; rollback first")

    stamp = time.strftime("%Y%m%d-%H%M%S")
    backup = HERE / "install-backups" / f"nevernever-mobs-art-{stamp}"
    backup.mkdir(parents=True, exist_ok=True)
    shutil.copy2(GAMEDATA, backup / "gamedata.mpk")
    shutil.copy2(SKINS_MPK, backup / "skin099.mpk")

    manifest = {
        "created": stamp,
        "before": {
            "gamedata.mpk": sha(GAMEDATA.read_bytes()),
            "skin099.mpk": sha(SKINS_MPK.read_bytes()),
        },
        "after": {
            "gamedata.mpk": sha(game_blob),
            "skin099.mpk": sha(skin_mpk_blob),
        },
        "nifs": {p.name: sha(blob) for p, blob in built_nifs.items()},
    }
    (backup / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    for nif_path, blob in built_nifs.items():
        nif_path.write_bytes(blob)
    SKINS_MPK.write_bytes(skin_mpk_blob)
    GAMEDATA.write_bytes(game_blob)

    require(sha(GAMEDATA.read_bytes()) == manifest["after"]["gamedata.mpk"], "gamedata.mpk write verification failed")
    require(sha(SKINS_MPK.read_bytes()) == manifest["after"]["skin099.mpk"], "skin099.mpk write verification failed")
    for nif_path, blob in built_nifs.items():
        require(sha(nif_path.read_bytes()) == sha(blob), f"{nif_path.name} write verification failed")

    sheet_path = HERE / "work" / "nevernever_mob_skins_preview.png"
    render_contact_sheet(preview_pairs, sheet_path)
    print(f"Installed 24 Nevernever/Edinburgh custom models (1480..1503), 20 NIFs, 31 DDS skins, 6 anim sets, 12 aura sets.")
    print(f"Preview contact sheet: {sheet_path}")
    print(f'Rollback with:\n  python3 -B "{Path(__file__).resolve()}" rollback "{backup}"')


def rollback(folder: Path) -> None:
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
    shutil.copy2(folder / "gamedata.mpk", GAMEDATA)
    shutil.copy2(folder / "skin099.mpk", SKINS_MPK)
    for nif_name, expected_sha in manifest["nifs"].items():
        p = FIGURES / nif_name
        if p.exists() and sha(p.read_bytes()) == expected_sha:
            p.unlink()
    require(sha(GAMEDATA.read_bytes()) == manifest["before"]["gamedata.mpk"], "gamedata.mpk rollback hash mismatch")
    require(sha(SKINS_MPK.read_bytes()) == manifest["before"]["skin099.mpk"], "skin099.mpk rollback hash mismatch")
    print(f"Rolled back Nevernever mob art from {folder}")


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "preview"
    if cmd == "install":
        install()
    elif cmd == "rollback" and len(sys.argv) > 2:
        rollback(Path(sys.argv[2]))
    else:
        preview()
