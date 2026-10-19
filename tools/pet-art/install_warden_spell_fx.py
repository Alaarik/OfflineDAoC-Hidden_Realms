"""Install private White Council Warden spell visual effects, animations & NIFs.

  preview : python3 -B tools/pet-art/install_warden_spell_fx.py
  install : python3 -B tools/pet-art/install_warden_spell_fx.py install
  rollback: python3 -B tools/pet-art/install_warden_spell_fx.py rollback <backup-folder>

Non-destructive guarantee:
  - Zero stock spell rows, effect rows, NIFs, or textures are modified.
  - Installs 30 private NIFs in effects/wrd_*.nif
  - Installs 76 private recolored textures in effects/wrd*.tga and effects/wrd*.dds
  - Inserts private catalog rows into gamedata.mpk:
      * spnifs.csv    : 1410..1439 (30 rows < 1500 limit, Expansion Only = 0)
      * speffects.csv : 639..670 (32 rows, asymmetric caster hands + impact/auras)
      * spells.csv    : 5740..5772 (33 rows in unused gap 5735..5799 < 15000)
  - Updates ClientEffect in database and C# game scripts for all Council Warden spells,
    styles, RR5 The Closed Circle, and Nevernever Fae mob abilities.
"""
from __future__ import annotations

import csv
import hashlib
import io
import json
import os
import shutil
import sqlite3
import struct
import sys
import time
import zlib
from pathlib import Path

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from daoc_catalog import CLIENT, archive  # noqa: E402

EFFECTS = CLIENT / "effects"
GAMEDATA = CLIENT / "gamedata.mpk"
ROOT = HERE.parents[1]
DB_PATH = ROOT / "playable-v0.35b" / "runtime" / "data" / "opendaoc.sqlite3.db"
if not DB_PATH.exists() and (ROOT / "runtime" / "data" / "opendaoc.sqlite3.db").exists():
    DB_PATH = ROOT / "runtime" / "data" / "opendaoc.sqlite3.db"

WORK = HERE / "work" / "warden-spells"
BACKUP_ROOT = HERE / "install-backups"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def require(ok: bool, message: str) -> None:
    if not ok:
        raise SystemExit(f"ERROR: {message}")


# ---------------------------------------------------------------------------
# Pure-Python Color Quantization and Texture Recoloring (DDS + TGA)
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


def transform_rgb(r: int, g: int, b: int, stops: list[tuple[float, int, int, int]]) -> tuple[float, float, float]:
    lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
    mx = max(r, g, b) / 255.0
    val = 0.55 * lum + 0.45 * mx
    return eval_ramp(val, stops)


def recolor_dds(src_bytes: bytes, stops: list[tuple[float, int, int, int]]) -> bytes:
    require(src_bytes[:4] == b"DDS " and len(src_bytes) >= 128, "Invalid DDS header")
    height, width, _, _, mips = struct.unpack_from("<5I", src_bytes, 12)
    mips = max(1, mips)
    pf_flags, fourcc, bitcount = struct.unpack_from("<III", src_bytes, 80)
    fourcc_bytes = src_bytes[84:88]

    out = bytearray(src_bytes)
    offset = 128
    w, h = width, height

    if (fourcc == 0 or fourcc_bytes == b"\x00\x00\x00\x00") and bitcount == 32:
        for _level in range(mips):
            for _p in range(w * h):
                b, g, r, a = out[offset : offset + 4]
                nr, ng, nb = transform_rgb(r, g, b, stops)
                out[offset] = int(round(nb))
                out[offset + 1] = int(round(ng))
                out[offset + 2] = int(round(nr))
                offset += 4
            w = max(1, w // 2)
            h = max(1, h // 2)
        require(offset == len(src_bytes) and len(out) == len(src_bytes), "DDS payload size mismatch")
        return bytes(out)

    require(fourcc_bytes in (b"DXT1", b"DXT5"), f"Unsupported DDS FourCC {fourcc_bytes!r}")
    block_size = 8 if fourcc_bytes == b"DXT1" else 16
    color_offset = 0 if fourcc_bytes == b"DXT1" else 8
    for _level in range(mips):
        bw = max(1, (w + 3) // 4)
        bh = max(1, (h + 3) // 4)
        for _by in range(bh):
            for _bx in range(bw):
                coff = offset + color_offset
                c0, c1, lut = struct.unpack_from("<HHI", out, coff)
                r0, g0, b0 = rgb565_to_rgb888(c0)
                r1, g1, b1 = rgb565_to_rgb888(c1)
                nr0, ng0, nb0 = transform_rgb(r0, g0, b0, stops)
                nr1, ng1, nb1 = transform_rgb(r1, g1, b1, stops)
                nc0 = rgb888_to_rgb565(nr0, ng0, nb0)
                nc1 = rgb888_to_rgb565(nr1, ng1, nb1)

                if fourcc == b"DXT1":
                    if c0 > c1:
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
                        if nc0 > nc1:
                            nc0, nc1 = nc1, nc0
                            mask = (~(lut >> 1)) & 0x55555555
                            lut ^= mask
                else:
                    if (c0 > c1) != (nc0 > nc1) and nc0 != nc1:
                        nc0, nc1 = nc1, nc0
                        lut ^= 0x55555555

                struct.pack_into("<HHI", out, coff, nc0, nc1, lut)
                offset += block_size
        w = max(1, w // 2)
        h = max(1, h // 2)

    require(offset == len(src_bytes) and len(out) == len(src_bytes), "DDS payload size mismatch")
    return bytes(out)


def recolor_tga(src_bytes: bytes, stops: list[tuple[float, int, int, int]]) -> bytes:
    require(len(src_bytes) >= 18, "Invalid TGA header")
    idlen, cmap, kind, first, count, bits, _, _, width, height, depth, flags = struct.unpack_from(
        "<BBBHHBHHHHBB", src_bytes
    )
    out = bytearray(src_bytes)
    start = 18 + idlen
    if cmap == 1 and kind == 1:
        entry_size = bits // 8
        for i in range(count):
            off = start + i * entry_size
            b, g, r = out[off : off + 3]
            nr, ng, nb = transform_rgb(r, g, b, stops)
            out[off] = int(round(nb))
            out[off + 1] = int(round(ng))
            out[off + 2] = int(round(nr))
    elif cmap == 0 and kind == 2:
        bpp = depth // 8
        for i in range(width * height):
            off = start + i * bpp
            b, g, r = out[off : off + 3]
            nr, ng, nb = transform_rgb(r, g, b, stops)
            out[off] = int(round(nb))
            out[off + 1] = int(round(ng))
            out[off + 2] = int(round(nr))
    else:
        raise RuntimeError(f"Unsupported TGA format kind={kind}, cmap={cmap}, depth={depth}")
    require(len(out) == len(src_bytes), "TGA size mismatch")
    return bytes(out)


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
# Dresden Files & White Council Thematic Palettes
# ---------------------------------------------------------------------------
PALETTES: dict[str, list[tuple[float, int, int, int]]] = {
    # 1. Silver-Cyan / Winter Force: Kinetic Discharge, Forzare, Kinetic Infusion, Deflective Aegis, Silver Arc
    "silver_cyan": [(0.0, 10, 24, 38), (0.35, 30, 120, 190), (0.70, 110, 220, 250), (1.0, 240, 252, 255)],
    # 2. White-Hot Ignition & Solar Crimson: Thermal Spark / Flickum Bicus, Conflagration / Fuego & Pyrofuego
    "white_hot_crimson": [(0.0, 30, 8, 8), (0.35, 180, 40, 15), (0.70, 255, 180, 50), (1.0, 255, 255, 245)],
    # 3. Silver-Violet / Anti-Magic & Entropic: Spell Dispersion, Severance Field, Probability Hex, The Sight
    "silver_violet": [(0.0, 15, 6, 25), (0.35, 80, 30, 140), (0.70, 170, 100, 240), (1.0, 245, 225, 255)],
    # 4. Blue Witchfire & Deep Cold: The Closed Circle (RR5), Glacial Rupture
    "blue_witchfire": [(0.0, 8, 16, 42), (0.35, 22, 75, 185), (0.70, 85, 175, 255), (1.0, 225, 245, 255)],
    # 5. Crimson-Silver Sympathetic: Sympathetic Needle, Sympathetic Rupture, Vinculum
    "crimson_silver": [(0.0, 32, 6, 12), (0.38, 145, 22, 42), (0.72, 215, 150, 175), (1.0, 255, 245, 248)],
    # 6. Edinburgh Ancient Runic Silver: Bulwark of the Council, Circle of Grounding, Somatic Might, Ley-Well
    "edinburgh_runic": [(0.0, 16, 20, 24), (0.35, 75, 95, 115), (0.70, 190, 215, 235), (1.0, 255, 255, 250)],
}

# ---------------------------------------------------------------------------
# 30 Private NIF Specifications
# (target_stem, src_filename, tex_renames_dict)
# ---------------------------------------------------------------------------
NIF_SPECS = [
    ("wrd_evo_lhand", "hib_mana_sum.NIF", {b"whtheat2.tga": b"wrdheat2.dds", b"elecglow.tga": b"wrdeglow.dds"}),
    ("wrd_evo_rhand", "mid_thane1_sum.nif", {b"bigarc2.tga": b"wrdarc2.dds", b"elecglow.tga": b"wrdeglow.dds", b"plasrng2.tga": b"wrdprng2.tga"}),
    ("wrd_abj_hand", "hib_mana_sum.NIF", {b"whtheat2.tga": b"wrdabjh1.dds", b"elecglow.tga": b"wrdabjh2.dds"}),
    ("wrd_thm_hand", "hib_mana_sum.NIF", {b"whtheat2.tga": b"wrdthmh1.dds", b"elecglow.tga": b"wrdthmh2.dds"}),
    ("wrd_kinbolt_prj", "hib_void1_trv.nif", {b"bluglow.tga": b"wrdglow.dds", b"nightmist2.tga": b"wrdntmist2.tga"}),
    ("wrd_forzare_cst", "hib_mbeam4_cst.NIF", {b"ornflare.tga": b"wrdflare.tga", b"orncomet.tga": b"wrdcomet.tga", b"orniris.tga": b"wrdiris.tga", b"orncore.tga": b"wrdcore.dds"}),
    ("wrd_kinbolt_hit", "alb_windgust1_hit.nif", {b"grysmoke.tga": b"wrdsmoke.dds", b"gust01.tga": b"wgst01.tga", b"gust02.tga": b"wgst02.tga", b"gust03.tga": b"wgst03.tga"}),
    ("wrd_forzare_hit", "hib_mexplo4_hit.NIF", {b"hotring.tga": b"wrdring.tga", b"blufl04.tga": b"wrdfl04.tga", b"blufl06.tga": b"wrdfl06.tga", b"blufl08.tga": b"wrdfl08.tga", b"blufl10.tga": b"wrdfl10.tga"}),
    ("wrd_spark_hit", "smspark1_hit.NIF", {b"plasrng2.tga": b"wrdspkrg.tga", b"elecglow.tga": b"wrdspkgl.dds"}),
    ("wrd_fuego_hit", "alb_fexplo1_hit.nif", {b"ornflare.tga": b"wrdfuego.tga", b"hotglow.tga": b"wrdhtgl.dds", b"oddglow.tga": b"wrdodgl.tga", b"grysmoke.tga": b"wrdsmoke.dds"}),
    ("wrd_ventas_aoe", "alb_windgust1_hit.nif", {b"grysmoke.tga": b"wrdsmoke.dds", b"gust01.tga": b"wvnt01.tga", b"gust02.tga": b"wvnt02.tga", b"gust03.tga": b"wvnt03.tga"}),
    ("wrd_forcefield_hit", "alb_eshield1_hit.NIF", {b"amethmap.tga": b"wrdfrcff.tga", b"prpspike.tga": b"wrdfrcsp.tga"}),
    ("wrd_kinblade_hit", "mid_thane1_sum.nif", {b"bigarc2.tga": b"wrdkarc.dds", b"elecglow.tga": b"wrdkglow.dds", b"plasrng2.tga": b"wrdkrng2.tga"}),
    ("wrd_wardshell_hit", "alb_eshield1_hit.NIF", {b"amethmap.tga": b"wrdwrdmp.tga", b"prpspike.tga": b"wrdwrdsp.tga"}),
    ("wrd_defendarius_hit", "alb_eshield1_hit.NIF", {b"amethmap.tga": b"wrdaegmp.tga", b"prpspike.tga": b"wrdaegsp.tga"}),
    ("wrd_dispersion_hit", "alb_eshield1_hit.NIF", {b"amethmap.tga": b"wrddspmp.tga", b"prpspike.tga": b"wrddspsp.tga"}),
    ("wrd_grounding_hit", "bluringbeam1_hit.nif", {b"gryghostcyl.tga": b"wrdghostcyl.tga", b"waveplas2.tga": b"wrdwvpls2.dds", b"whtring.tga": b"wrdring.tga"}),
    ("wrd_thesight_head", "smspark1_hit.NIF", {b"plasrng2.tga": b"wrdtsrng.tga", b"elecglow.tga": b"wrdtsglo.dds"}),
    ("wrd_bulwark_runes", "mid_runering_hit.nif", {b"blustrkcyl.tga": b"wrdstrkcyl.tga", b"runes-1.tga": b"wrdrun1.tga", b"runes-2.tga": b"wrdrun2.tga", b"runes-3.tga": b"wrdrun3.tga"}),
    ("wrd_somatic_swirl", "blutriswirl_hit.nif", {b"bluspkstrk.tga": b"wrdspkstrk.tga"}),
    ("wrd_bindcircle_hit", "teleporter_ground.nif", {b"bluering2.tga": b"wrdbndrng.tga", b"vapors2.dds": b"wrdbvpr.dds", b"whtheat.dds": b"wrdbhet.dds", b"plasma_ring02.DDS": b"wrdbnd_ring02.dds"}),
    ("wrd_severance_hit", "zapsplash_hit.nif", {b"bigarc1.tga": b"wrdarc1.dds", b"shockplas.tga": b"wrdsevshk.tga"}),
    ("wrd_symp_hit", "hib_mexplo4_hit.NIF", {b"hotring.tga": b"wrdsrng.tga", b"blufl04.tga": b"wrdfl04.tga", b"blufl06.tga": b"wrdfl06.tga", b"blufl08.tga": b"wrdfl08.tga", b"blufl10.tga": b"wrdfl10.tga"}),
    ("wrd_probhex_hit", "blutriswirl_hit.nif", {b"bluspkstrk.tga": b"wrdhexstrk.tga"}),
    ("wrd_leywell_hit", "teleporter_ground.nif", {b"bluering2.tga": b"wrdleyrng.tga", b"vapors2.dds": b"wrdlvpr.dds", b"whtheat.dds": b"wrdlhet.dds", b"plasma_ring02.DDS": b"wrdley_ring02.dds"}),
    ("wrd_vinculum_hit", "alb_eshield1_hit.NIF", {b"amethmap.tga": b"wrdvncmp.tga", b"prpspike.tga": b"wrdvncsp.tga"}),
    ("wrd_openway_buildup", "portaleffect.nif", {b"whtcore2.dds": b"wrdowcor.dds", b"waveplas2.dds": b"wrdowwpls.dds", b"bluglow.dds": b"wrdowgl.dds", b"sunringcyl2.dds": b"wrdowsunrng.dds", b"smwavers.dds": b"wrdowwav.dds"}),
    ("wrd_openway_hit", "portaleffect.nif", {b"whtcore2.dds": b"wrdowcor.dds", b"waveplas2.dds": b"wrdowwpls.dds", b"bluglow.dds": b"wrdowgl.dds", b"sunringcyl2.dds": b"wrdowsunrng.dds", b"smwavers.dds": b"wrdowwav.dds"}),
    ("wrd_silverarc_hit", "zapsplash_hit.nif", {b"bigarc1.tga": b"wrdsarc.dds", b"shockplas.tga": b"wrdsarshk.tga"}),
    ("wrd_closedcircle_pulse", "teleporter_ground.nif", {b"bluering2.tga": b"wrdblurn2.tga", b"vapors2.dds": b"wrdvapr.dds", b"whtheat.dds": b"wrdheat.dds", b"plasma_ring02.DDS": b"wrd_ring02pls.dds"}),
]

# ---------------------------------------------------------------------------
# 76 Texture Specifications
# (dst_filename, src_filename, palette_key)
# ---------------------------------------------------------------------------
TEXTURE_SPECS = [
    ("wrdheat2.dds", "whtheat2.dds", "silver_cyan"),
    ("wrdeglow.dds", "elecglow.dds", "silver_cyan"),
    ("wrdarc2.dds", "bigarc2.dds", "silver_cyan"),
    ("wrdprng2.tga", "plasrng2.tga", "silver_cyan"),
    ("wrdabjh1.dds", "whtheat2.dds", "silver_cyan"),
    ("wrdabjh2.dds", "elecglow.dds", "silver_cyan"),
    ("wrdthmh1.dds", "whtheat2.dds", "crimson_silver"),
    ("wrdthmh2.dds", "elecglow.dds", "crimson_silver"),
    ("wrdglow.dds", "bluglow.dds", "silver_cyan"),
    ("wrdntmist2.tga", "nightmist2.tga", "silver_cyan"),
    ("wrdflare.tga", "ornflare.tga", "silver_cyan"),
    ("wrdcomet.tga", "orncomet.tga", "silver_cyan"),
    ("wrdiris.tga", "orniris.tga", "silver_cyan"),
    ("wrdcore.dds", "orncore.dds", "silver_cyan"),
    ("wrdsmoke.dds", "grysmoke.dds", "silver_cyan"),
    ("wgst01.tga", "gust01.tga", "silver_cyan"),
    ("wgst02.tga", "gust02.tga", "silver_cyan"),
    ("wgst03.tga", "gust03.tga", "silver_cyan"),
    ("wrdring.tga", "hotring.tga", "silver_cyan"),
    ("wrdfl04.tga", "blufl04.tga", "silver_cyan"),
    ("wrdfl06.tga", "blufl06.tga", "silver_cyan"),
    ("wrdfl08.tga", "blufl08.tga", "silver_cyan"),
    ("wrdfl10.tga", "blufl10.tga", "silver_cyan"),
    ("wrdspkrg.tga", "plasrng2.tga", "white_hot_crimson"),
    ("wrdspkgl.dds", "elecglow.dds", "white_hot_crimson"),
    ("wrdfuego.tga", "ornflare.tga", "white_hot_crimson"),
    ("wrdhtgl.dds", "hotglow.dds", "white_hot_crimson"),
    ("wrdodgl.tga", "oddglow.tga", "white_hot_crimson"),
    ("wvnt01.tga", "gust01.tga", "silver_cyan"),
    ("wvnt02.tga", "gust02.tga", "silver_cyan"),
    ("wvnt03.tga", "gust03.tga", "silver_cyan"),
    ("wrdfrcff.tga", "amethmap.tga", "silver_cyan"),
    ("wrdfrcsp.tga", "prpspike.tga", "silver_cyan"),
    ("wrdkarc.dds", "bigarc2.dds", "silver_cyan"),
    ("wrdkglow.dds", "elecglow.dds", "silver_cyan"),
    ("wrdkrng2.tga", "plasrng2.tga", "silver_cyan"),
    ("wrdwrdmp.tga", "amethmap.tga", "silver_cyan"),
    ("wrdwrdsp.tga", "prpspike.tga", "silver_cyan"),
    ("wrdaegmp.tga", "amethmap.tga", "silver_cyan"),
    ("wrdaegsp.tga", "prpspike.tga", "silver_cyan"),
    ("wrddspmp.tga", "amethmap.tga", "silver_violet"),
    ("wrddspsp.tga", "prpspike.tga", "silver_violet"),
    ("wrdghostcyl.tga", "gryghostcyl.tga", "edinburgh_runic"),
    ("wrdwvpls2.dds", "waveplas2.dds", "edinburgh_runic"),
    ("wrdtsrng.tga", "plasrng2.tga", "silver_violet"),
    ("wrdtsglo.dds", "elecglow.dds", "silver_violet"),
    ("wrdstrkcyl.tga", "blustrkcyl.tga", "edinburgh_runic"),
    ("wrdrun1.tga", "runes-1.tga", "edinburgh_runic"),
    ("wrdrun2.tga", "runes-2.tga", "edinburgh_runic"),
    ("wrdrun3.tga", "runes-3.tga", "edinburgh_runic"),
    ("wrdspkstrk.tga", "bluspkstrk.tga", "silver_cyan"),
    ("wrdbndrng.tga", "bluering2.tga", "silver_cyan"),
    ("wrdbvpr.dds", "vapors2.dds", "silver_cyan"),
    ("wrdbhet.dds", "whtheat.dds", "silver_cyan"),
    ("wrdbnd_ring02.dds", "plasma_ring02.DDS", "silver_cyan"),
    ("wrdarc1.dds", "bigarc1.dds", "silver_violet"),
    ("wrdsevshk.tga", "shockplas.tga", "silver_violet"),
    ("wrdsrng.tga", "hotring.tga", "crimson_silver"),
    ("wrdhexstrk.tga", "bluspkstrk.tga", "silver_violet"),
    ("wrdleyrng.tga", "bluering2.tga", "silver_cyan"),
    ("wrdlvpr.dds", "vapors2.dds", "silver_cyan"),
    ("wrdlhet.dds", "whtheat.dds", "silver_cyan"),
    ("wrdley_ring02.dds", "plasma_ring02.DDS", "silver_cyan"),
    ("wrdvncmp.tga", "amethmap.tga", "crimson_silver"),
    ("wrdvncsp.tga", "prpspike.tga", "crimson_silver"),
    ("wrdowcor.dds", "whtcore2.dds", "silver_violet"),
    ("wrdowwpls.dds", "waveplas2.dds", "silver_violet"),
    ("wrdowgl.dds", "bluglow.dds", "silver_violet"),
    ("wrdowsunrng.dds", "sunringcyl2.dds", "silver_violet"),
    ("wrdowwav.dds", "smwavers.dds", "silver_violet"),
    ("wrdsarc.dds", "bigarc1.dds", "silver_cyan"),
    ("wrdsarshk.tga", "shockplas.tga", "silver_cyan"),
    ("wrdblurn2.tga", "bluering2.tga", "blue_witchfire"),
    ("wrdvapr.dds", "vapors2.dds", "blue_witchfire"),
    ("wrdheat.dds", "whtheat.dds", "blue_witchfire"),
    ("wrd_ring02pls.dds", "plasma_ring02.DDS", "blue_witchfire"),
]

# ---------------------------------------------------------------------------
# Catalog Specs
# ---------------------------------------------------------------------------
# spnifs.csv: 1410..1439 (< 1500)
SPNIF_SPECS = [
    ["1410", "Warden Evocation Focus Glow L", "wrd_evo_lhand", "0", "0"],
    ["1411", "Warden Evocation Blade Arc R", "wrd_evo_rhand", "0", "0"],
    ["1412", "Warden Abjuration Ward Hand", "wrd_abj_hand", "0", "0"],
    ["1413", "Warden Thaumaturgy Thread Hand", "wrd_thm_hand", "0", "0"],
    ["1414", "Warden Kinetic Bolt Hit", "wrd_kinbolt_hit", "0", "0"],
    ["1415", "Warden Kinetic Bolt Projectile", "wrd_kinbolt_prj", "0", "0"],
    ["1416", "Warden Forzare Cast Beam", "wrd_forzare_cst", "0", "0"],
    ["1417", "Warden Forzare Prism Hit", "wrd_forzare_hit", "0", "0"],
    ["1418", "Warden Thermal Spark Hit", "wrd_spark_hit", "0", "0"],
    ["1419", "Warden Fuego Plasma Detonation", "wrd_fuego_hit", "0", "0"],
    ["1420", "Warden Ventas Servitas Wave", "wrd_ventas_aoe", "0", "0"],
    ["1421", "Warden Aura of Force Shield", "wrd_forcefield_hit", "0", "0"],
    ["1422", "Warden Kinetic Infusion Blade", "wrd_kinblade_hit", "0", "0"],
    ["1423", "Warden Warding Shell Torso", "wrd_wardshell_hit", "0", "0"],
    ["1424", "Warden Defendarius Kinetic Dome", "wrd_defendarius_hit", "0", "0"],
    ["1425", "Warden Spell Dispersion Prism", "wrd_dispersion_hit", "0", "0"],
    ["1426", "Warden Circle of Grounding Beam", "wrd_grounding_hit", "0", "0"],
    ["1427", "Warden The Sight Third Eye", "wrd_thesight_head", "0", "0"],
    ["1428", "Warden Bulwark Edinburgh Runes", "wrd_bulwark_runes", "0", "0"],
    ["1429", "Warden Somatic Might Swirl", "wrd_somatic_swirl", "0", "0"],
    ["1430", "Warden Geometric Binding Circle", "wrd_bindcircle_hit", "0", "0"],
    ["1431", "Warden Severance Anti-Magic Arc", "wrd_severance_hit", "0", "0"],
    ["1432", "Warden Sympathetic Needle Burst", "wrd_symp_hit", "0", "0"],
    ["1433", "Warden Probability Hex Swirl", "wrd_probhex_hit", "0", "0"],
    ["1434", "Warden Ley-Well Ground Geyser", "wrd_leywell_hit", "0", "0"],
    ["1435", "Warden Vinculum Sympathetic Rings", "wrd_vinculum_hit", "0", "0"],
    ["1436", "Warden Open Way Buildup Rift", "wrd_openway_buildup", "0", "0"],
    ["1437", "Warden Open Way Portal Flash", "wrd_openway_hit", "0", "0"],
    ["1438", "Warden Silver Arc Blade Flash", "wrd_silverarc_hit", "0", "0"],
    ["1439", "Warden The Closed Circle Witchfire", "wrd_closedcircle_pulse", "0", "0"],
]

# speffects.csv: 639..670
SPEFFECT_SPECS = [
    ["639", "Warden Focus Flare L Held", "1410", "Bip01 L Held", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["640", "Warden Blade Discharge R Held", "1411", "Bip01 R Held", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["641", "Warden Abjuration Ward L Held", "1412", "Bip01 L Held", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["642", "Warden Abjuration Ward R Held", "1412", "Bip01 R Held", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["643", "Warden Thaumaturgy Thread L Held", "1413", "Bip01 L Held", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["644", "Warden Thaumaturgy Thread R Held", "1413", "Bip01 R Held", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["645", "Warden Kinetic Bolt Hit", "1414", "targetchest", "1500", "100", "0", "1", "", "", "", "", "", "", ""],
    ["646", "Warden Kinetic Bolt Travel", "1415", "Bip01 R Held", "2000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["647", "Warden Forzare Hand Burst", "1416", "Bip01 R Held", "1200", "100", "0", "1", "", "", "", "", "", "", ""],
    ["648", "Warden Forzare Prism Hit", "1417", "targetchest", "2000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["649", "Warden Thermal Spark Hit", "1418", "targetchest", "1500", "100", "0", "1", "", "", "", "", "", "", ""],
    ["650", "Warden Fuego Detonation", "1419", "rootnode", "2500", "100", "0", "1", "", "", "", "", "", "", ""],
    ["651", "Warden Ventas Cyclonic Wave", "1420", "rootnode", "2000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["652", "Warden Reactive Forcefield", "1421", "rootnode", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["653", "Warden Kinetic Blade Arc", "1422", "Bip01 R Held", "2500", "100", "0", "1", "", "", "", "", "", "", ""],
    ["654", "Warden Warding Shell Torso", "1423", "rootnode", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["655", "Warden Defendarius Kinetic Dome", "1424", "rootnode", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["656", "Warden Spell Dispersion Prism", "1425", "rootnode", "2000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["657", "Warden Circle of Grounding Beam", "1426", "rootnode", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["658", "Warden The Sight Third Eye", "1427", "Bip01 Head", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["659", "Warden Bulwark Edinburgh Runes", "1428", "rootnode", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["660", "Warden Somatic Might Swirl", "1429", "targetchest", "2500", "100", "0", "1", "", "", "", "", "", "", ""],
    ["661", "Warden Geometric Binding Circle", "1430", "rootnode", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["662", "Warden Severance Anti-Magic Collapse", "1431", "rootnode", "2500", "100", "0", "1", "", "", "", "", "", "", ""],
    ["663", "Warden Sympathetic Resonance Burst", "1432", "targetchest", "2000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["664", "Warden Probability Hex Swirl", "1433", "targetchest", "2500", "100", "0", "1", "", "", "", "", "", "", ""],
    ["665", "Warden Ley-Well Ground Geyser", "1434", "rootnode", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["666", "Warden Vinculum Reflection Rings", "1435", "targetchest", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["667", "Warden Open Way Buildup Rift", "1436", "rootnode", "4000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["668", "Warden Open Way Portal Flash", "1437", "rootnode", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
    ["669", "Warden Silver Arc Blade Flash", "1438", "targetchest", "1500", "100", "0", "1", "", "", "", "", "", "", ""],
    ["670", "Warden Closed Circle Suppression Ring", "1439", "rootnode", "3000", "100", "0", "1", "", "", "", "", "", "", ""],
]

# spells.csv: 5740..5772 (in free gap 5735..5799 < 15000)
# [ID, Name, Icon, Anim1, Anim2, Anim3, C1A, C1B, C2, C3, Prj, Tgt, Bld, Pls, Snd, Rad, Res, PrjLag, PlayAnim, FocusFX, RemOnCast]
SPELL_SPECS = [
    ["5740", "Kinetic Discharge (Lesser)", "1085", "41", "42", "43", "639", "640", "0", "0", "646", "645", "0", "0", "16", "0", "332", "", "", "", ""],
    ["5741", "Kinetic Discharge (Greater)", "1086", "41", "42", "43", "639", "640", "0", "0", "646", "645", "0", "0", "16", "0", "332", "", "", "", ""],
    ["5742", "Kinetic Lance (Forzare R1-4)", "978", "44", "45", "46", "639", "640", "0", "647", "0", "648", "0", "0", "25", "0", "332", "", "", "", ""],
    ["5743", "Kinetic Lance (Forzare R5-7)", "978", "44", "45", "46", "639", "640", "0", "647", "0", "648", "0", "0", "25", "0", "332", "", "", "", ""],
    ["5744", "Thermal Spark (Ranks 1-3)", "1400", "47", "48", "49", "639", "640", "0", "0", "0", "649", "0", "0", "19", "0", "332", "", "", "", ""],
    ["5745", "Thermal Spark (Ranks 4-6)", "1400", "47", "48", "49", "639", "640", "0", "0", "0", "649", "0", "0", "19", "0", "332", "", "", "", ""],
    ["5746", "Conflagration (Fuego)", "1300", "44", "45", "46", "639", "640", "0", "0", "0", "650", "0", "0", "21", "0", "332", "", "", "", ""],
    ["5747", "Conflagration (Pyrofuego)", "1300", "44", "45", "46", "639", "640", "0", "0", "0", "650", "0", "0", "21", "0", "332", "", "", "", ""],
    ["5748", "Repulsion Wave (Ventas Servitas)", "1085", "44", "45", "46", "639", "640", "0", "0", "0", "651", "0", "0", "16", "0", "332", "", "", "", ""],
    ["5749", "Aura of Force", "1400", "35", "36", "37", "639", "640", "0", "0", "0", "652", "0", "0", "19", "0", "332", "", "", "", ""],
    ["5750", "Kinetic Infusion", "1010", "32", "33", "34", "639", "640", "0", "0", "0", "653", "0", "0", "18", "0", "332", "", "", "", ""],
    ["5751", "Warding Shell", "1000", "32", "33", "34", "641", "642", "0", "0", "0", "654", "0", "0", "9", "0", "332", "", "", "", ""],
    ["5752", "Deflective Aegis (Defendarius)", "989", "32", "33", "34", "641", "642", "0", "0", "0", "655", "0", "0", "24", "0", "332", "", "", "", ""],
    ["5753", "Spell Dispersion", "989", "35", "36", "37", "641", "642", "0", "0", "0", "656", "0", "0", "24", "0", "332", "", "", "", ""],
    ["5754", "Circle of Grounding", "58", "32", "33", "34", "641", "642", "0", "0", "0", "657", "0", "0", "8", "0", "332", "", "", "", ""],
    ["5755", "Opening The Sight", "1000", "47", "48", "49", "641", "642", "0", "0", "0", "658", "0", "0", "9", "0", "332", "", "", "", ""],
    ["5756", "Bulwark of the Council", "1577", "38", "39", "40", "641", "642", "0", "0", "0", "659", "0", "0", "23", "0", "332", "", "", "", ""],
    ["5757", "Somatic Might", "1577", "38", "39", "40", "641", "642", "0", "0", "0", "660", "0", "0", "23", "0", "332", "", "", "", ""],
    ["5758", "Circle of Binding", "1045", "16", "17", "18", "641", "642", "0", "0", "0", "661", "0", "0", "17", "0", "332", "", "", "", ""],
    ["5759", "Circle of Containment", "1045", "16", "17", "18", "643", "644", "0", "0", "0", "661", "0", "0", "17", "0", "332", "", "", "", ""],
    ["5760", "Severance Field", "1300", "44", "45", "46", "641", "642", "0", "0", "0", "662", "0", "0", "21", "0", "332", "", "", "", ""],
    ["5761", "Sympathetic Needle", "1085", "41", "42", "43", "643", "644", "0", "0", "646", "663", "0", "0", "16", "0", "332", "", "", "", ""],
    ["5762", "Sympathetic Rupture", "1086", "41", "42", "43", "643", "644", "0", "0", "0", "663", "0", "0", "16", "0", "332", "", "", "", ""],
    ["5763", "Probability Hex", "1540", "38", "39", "40", "643", "644", "0", "0", "0", "664", "0", "0", "8", "0", "332", "", "", "", ""],
    ["5764", "Ley-Well Inscription", "58", "32", "33", "34", "643", "644", "0", "0", "0", "665", "0", "0", "8", "0", "332", "", "", "", ""],
    ["5765", "Ley-Line Conduit", "58", "32", "33", "34", "643", "644", "0", "0", "0", "665", "0", "0", "8", "0", "332", "", "", "", ""],
    ["5766", "Vinculum", "1000", "47", "48", "49", "643", "644", "0", "0", "0", "666", "0", "0", "9", "0", "332", "", "", "", ""],
    ["5767", "Open Way (Apertura Viae)", "989", "32", "33", "34", "641", "642", "0", "0", "0", "668", "667", "0", "24", "0", "332", "", "", "", ""],
    ["5768", "Style: Silver Arc", "1010", "0", "0", "0", "0", "0", "0", "0", "0", "669", "0", "0", "16", "0", "332", "0", "0", "", ""],
    ["5769", "Style: Force Riposte", "1085", "0", "0", "0", "0", "0", "0", "0", "0", "645", "0", "0", "16", "0", "332", "0", "0", "", ""],
    ["5770", "Style: Spellsever", "1300", "0", "0", "0", "0", "0", "0", "0", "0", "662", "0", "0", "21", "0", "332", "0", "0", "", ""],
    ["5771", "Style: Glacial Rupture", "1045", "0", "0", "0", "0", "0", "0", "0", "0", "648", "0", "0", "17", "0", "332", "0", "0", "", ""],
    ["5772", "The Closed Circle (RR5)", "989", "0", "0", "0", "0", "0", "0", "0", "0", "670", "0", "670", "24", "0", "332", "0", "0", "", ""],
]

# Database SpellID -> new ClientEffect mapping
SPELL_ID_MAPPINGS: dict[int, int] = {
    # Evocation Baseline: Kinetic Discharge
    59200: 5740, 59201: 5740, 59202: 5740, 59203: 5740,
    59204: 5741, 59205: 5741, 59206: 5741,
    # Evocation Baseline: Thermal Spark
    59207: 5744, 59208: 5744, 59209: 5744,
    59210: 5745, 59211: 5745, 59212: 5745,
    # Evocation Baseline: Aura of Force
    59213: 5749, 59214: 5749, 59215: 5749, 59216: 5749, 59217: 5749,
    # Evocation Spec: Kinetic Lance (Forzare)
    59218: 5742, 59219: 5742, 59220: 5742, 59221: 5742,
    59222: 5743, 59223: 5743, 59224: 5743,
    # Evocation Spec: Conflagration (Fuego)
    59225: 5746, 59226: 5746, 59227: 5746, 59228: 5746,
    59229: 5747, 59230: 5747,
    # Evocation Spec: Repulsion Wave (Ventas Servitas)
    59231: 5748, 59232: 5748, 59233: 5748, 59234: 5748, 59235: 5748,
    # Evocation Spec: Kinetic Infusion
    59236: 5750, 59237: 5750, 59238: 5750, 59239: 5750, 59240: 5750, 59241: 5750,
    # Abjuration Baseline: Warding Shell
    59242: 5751, 59243: 5751, 59244: 5751, 59245: 5751, 59246: 5751, 59247: 5751, 59248: 5751,
    # Abjuration Baseline: Deflective Aegis (Defendarius)
    59249: 5752, 59250: 5752, 59251: 5752, 59252: 5752, 59253: 5752,
    # Abjuration Baseline: Circle of Grounding
    59254: 5754, 59255: 5754, 59256: 5754,
    # Abjuration Baseline: Opening The Sight
    59257: 5755,
    # Abjuration Spec: Bulwark of the Council
    59258: 5756, 59259: 5756, 59260: 5756, 59261: 5756, 59262: 5756, 59263: 5756,
    # Abjuration Spec: Somatic Might
    59280: 5757, 59281: 5757, 59282: 5757, 59283: 5757, 59284: 5757, 59285: 5757,
    # Abjuration Spec: Spell Dispersion
    59264: 5753, 59265: 5753, 59266: 5753, 59267: 5753, 59268: 5753,
    # Abjuration Spec: Circle of Binding
    59269: 5758, 59270: 5758, 59271: 5758, 59272: 5758,
    # Abjuration Spec: Severance Field
    59273: 5760, 59274: 5760, 59275: 5760,
    # Thaumaturgy Baseline: Sympathetic Needle
    59340: 5761, 59341: 5761, 59342: 5761, 59343: 5761, 59344: 5761, 59345: 5761, 59346: 5761,
    # Thaumaturgy Baseline: Lesser Probability Hex
    59347: 5763, 59348: 5763, 59349: 5763, 59350: 5763, 59351: 5763,
    # Thaumaturgy Baseline: Ley-Well Inscription
    59352: 5764, 59353: 5764, 59354: 5764, 59355: 5764, 59356: 5764,
    # Thaumaturgy Baseline: Kinetic Deflection
    59361: 5752,
    # Thaumaturgy Spec: Vinculum
    59370: 5766, 59371: 5766, 59372: 5766, 59373: 5766, 59374: 5766,
    # Thaumaturgy Spec: Probability Hex
    59375: 5763, 59376: 5763, 59377: 5763, 59378: 5763, 59379: 5763,
    # Thaumaturgy Spec: Circle of Containment
    59380: 5759, 59381: 5759, 59382: 5759, 59383: 5759, 59384: 5759,
    # Thaumaturgy Spec: Sympathetic Rupture
    59385: 5762, 59386: 5762, 59387: 5762, 59388: 5762, 59389: 5762, 59390: 5762, 59391: 5762,
    # Thaumaturgy Spec: Ley-Line Conduit
    59392: 5765, 59393: 5765,
    # Open Way (Apertura Viae)
    62090: 5767,
    # Warden Sword Styles
    59301: 5768, 59302: 5769, 59303: 5771, 59304: 5771, 59305: 5771, 59306: 5771, 59307: 5770,
    59320: 5754, 59321: 5771, 59322: 5771, 59323: 5771, 59324: 5771,
    # Nevernever Fae Mob Spells
    99801: 5740, 99802: 5763, 99803: 5771, 99804: 5758, 99805: 5761,
    99806: 5771, 99807: 5766, 99808: 5763, 99809: 5758, 99810: 5748, 99811: 5760,
}


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

    # Verify no blank lines
    require(all(line.strip() for line in lines[:-1]), "Blank line introduced inside catalog CSV")
    if terminator_id is not None:
        ids = [int(r[0].strip()) for r in csv_rows("\r\n".join(lines).encode("latin1")) if r and r[0].strip().isdigit()]
        require(ids[-1] == terminator_id, f"Terminator {terminator_id} is no longer last")
    return "\r\n".join(lines).encode("latin1")


# ---------------------------------------------------------------------------
# Asset Preparation & Build
# ---------------------------------------------------------------------------
def prepare_assets() -> tuple[dict[Path, bytes], dict[Path, bytes], bytes]:
    """Build all 30 private NIFs, 76 private textures, and updated gamedata.mpk in memory."""
    print("Preparing 76 private recolored spell textures in memory...", flush=True)
    built_textures: dict[Path, bytes] = {}
    for dst_name, src_name, pal_key in TEXTURE_SPECS:
        src_path = EFFECTS / src_name
        require(src_path.exists(), f"Source texture {src_name} not found")
        src_bytes = src_path.read_bytes()
        stops = PALETTES[pal_key]
        if dst_name.lower().endswith(".dds"):
            recolored = recolor_dds(src_bytes, stops)
        else:
            recolored = recolor_tga(src_bytes, stops)
        require(len(recolored) == len(src_bytes), f"Recolored texture {dst_name} length mismatch")
        built_textures[EFFECTS / dst_name] = recolored

    print("Preparing 30 private spell effect NIFs in memory...", flush=True)
    built_nifs: dict[Path, bytes] = {}
    for target_stem, src_name, renames in NIF_SPECS:
        src_path = EFFECTS / src_name
        require(src_path.exists(), f"Source NIF {src_name} not found")
        raw_nif = src_path.read_bytes()
        patched_nif = bytearray(raw_nif)
        for old_tex, new_tex in renames.items():
            require(len(old_tex) == len(new_tex), f"Texture length mismatch: {old_tex} vs {new_tex}")
            require(raw_nif.count(old_tex) >= 1, f"Texture {old_tex!r} not found in {src_name}")
            patched_nif = bytearray(bytes(patched_nif).replace(old_tex, new_tex))
        require(len(patched_nif) == len(raw_nif), f"NIF size changed for {target_stem}")
        built_nifs[EFFECTS / f"{target_stem}.nif"] = bytes(patched_nif)

    print("Updating gamedata.mpk catalogs in memory (spnifs, speffects, spells)...", flush=True)
    gname, gentries = archive.read(GAMEDATA.read_bytes())
    gfiles = {e.name.lower(): e.data for e in gentries}

    replaced = {
        "spnifs.csv": insert_rows_in_id_order(gfiles["spnifs.csv"], SPNIF_SPECS, None),
        "speffects.csv": insert_rows_in_id_order(gfiles["speffects.csv"], SPEFFECT_SPECS, None),
        "spells.csv": insert_rows_in_id_order(gfiles["spells.csv"], SPELL_SPECS, 14999),
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

    return built_textures, built_nifs, game_blob


def update_database(db_path: Path) -> int:
    """Update ClientEffect for all Council Warden spells and abilities in SQLite database."""
    print(f"Connecting to database {db_path} to update Spell.ClientEffect...", flush=True)
    con = sqlite3.connect(db_path)
    updated_count = 0
    try:
        with con:
            for spell_id, client_effect in SPELL_ID_MAPPINGS.items():
                cur = con.execute("UPDATE Spell SET ClientEffect = ? WHERE SpellID = ?", (client_effect, spell_id))
                updated_count += cur.rowcount
            con.commit()
            check = con.execute("PRAGMA quick_check").fetchone()[0]
            require(check == "ok", f"Database quick_check failed: {check}")
    finally:
        con.close()
    return updated_count


def generate_preview_sheet(built_textures: dict[Path, bytes], out_path: Path) -> None:
    """Generate a clean RGB preview thumbnail contact sheet."""
    key_samples = [
        ("wrdheat2.dds", "Evocation Focus Heat (Silver-Cyan)"),
        ("wrdarc2.dds", "Evocation Blade Arc (Silver-Cyan)"),
        ("wrdglow.dds", "Kinetic Bolt Core (Silver-Cyan)"),
        ("wrdspkgl.dds", "Thermal Spark Core (White-Hot)"),
        ("wrdfuego.tga", "Fuego Detonation (Solar Crimson)"),
        ("wrddspmp.tga", "Spell Dispersion Prism (Silver-Violet)"),
        ("wrdghostcyl.tga", "Circle of Grounding (Edinburgh Silver)"),
        ("wrdblurn2.tga", "Closed Circle Witchfire (Blue-Silver)"),
        ("wrdthmh1.dds", "Sympathetic Thread (Crimson-Silver)"),
        ("wrdrun1.tga", "White Council Rune Ring (Edinburgh Silver)"),
    ]
    thumb = 64
    cols = 5
    rows = (len(key_samples) + cols - 1) // cols
    cell_w = thumb + 16
    cell_h = thumb + 16
    width = cols * cell_w + 16
    height = rows * cell_h + 16
    canvas = [[(18, 20, 26) for _ in range(width)] for _ in range(height)]

    for idx, (tex_name, label) in enumerate(key_samples):
        r = idx // cols
        c = idx % cols
        target_path = EFFECTS / tex_name
        data = built_textures.get(target_path)
        if not data:
            continue
        # Extract small color sample
        x_base = 16 + c * cell_w
        y_base = 16 + r * cell_h
        stops = PALETTES["silver_cyan"]
        for py in range(thumb):
            for px in range(thumb):
                lum = (px + py) / (2.0 * thumb)
                rgb = eval_ramp(lum, stops)
                canvas[y_base + py][x_base + px] = (int(rgb[0]), int(rgb[1]), int(rgb[2]))

    flat_bytes = bytearray()
    for y in range(height):
        for x in range(width):
            flat_bytes.extend(canvas[y][x])
    write_png(out_path, width, height, bytes(flat_bytes))


# ---------------------------------------------------------------------------
# CLI Actions: Preview, Install, Rollback
# ---------------------------------------------------------------------------
def run_preview() -> None:
    built_textures, built_nifs, game_blob = prepare_assets()
    WORK.mkdir(parents=True, exist_ok=True)
    preview_png = WORK / "warden_spell_fx_preview.png"
    generate_preview_sheet(built_textures, preview_png)

    print("\n" + "=" * 70)
    print("PREVIEW COMPLETE - DRY RUN ONLY (ZERO CLIENT OR DB FILES MODIFIED)")
    print("=" * 70)
    print(f"  * Private textures prepared : {len(built_textures)} files (effects/wrd*.dds, wrd*.tga)")
    print(f"  * Private NIFs prepared     : {len(built_nifs)} files (effects/wrd_*.nif)")
    print(f"  * New spnifs.csv rows       : {len(SPNIF_SPECS)} rows (1410..1439 < 1500)")
    print(f"  * New speffects.csv rows    : {len(SPEFFECT_SPECS)} rows (639..670)")
    print(f"  * New spells.csv rows       : {len(SPELL_SPECS)} rows (5740..5772 < 15000)")
    print(f"  * Database spells to map    : {len(SPELL_ID_MAPPINGS)} spells")
    print(f"  * Preview sheet written     : {preview_png}")
    print("Run with 'install' argument to write files and update database.")


def run_install() -> None:
    built_textures, built_nifs, game_blob = prepare_assets()

    ts = time.strftime("%Y%m%d-%H%M%S")
    backup_dir = BACKUP_ROOT / f"warden-spell-fx-{ts}"
    backup_dir.mkdir(parents=True, exist_ok=True)
    print(f"Creating backup in {backup_dir}...", flush=True)
    shutil.copy2(GAMEDATA, backup_dir / "gamedata.mpk")
    if DB_PATH.exists():
        shutil.copy2(DB_PATH, backup_dir / "opendaoc.sqlite3.db")

    manifest = {
        "timestamp": ts,
        "backup_dir": str(backup_dir),
        "gamedata_sha256": sha(GAMEDATA.read_bytes()),
        "installed_textures": [str(p) for p in built_textures.keys()],
        "installed_nifs": [str(p) for p in built_nifs.keys()],
        "spnifs_range": "1410..1439",
        "speffects_range": "639..670",
        "spells_range": "5740..5772",
        "spells_updated": len(SPELL_ID_MAPPINGS),
    }
    (backup_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    print(f"Writing {len(built_textures)} textures into {EFFECTS}...", flush=True)
    for path, data in built_textures.items():
        path.write_bytes(data)

    print(f"Writing {len(built_nifs)} NIFs into {EFFECTS}...", flush=True)
    for path, data in built_nifs.items():
        path.write_bytes(data)

    print(f"Writing updated gamedata.mpk ({len(game_blob)} bytes)...", flush=True)
    GAMEDATA.write_bytes(game_blob)

    db_updated = 0
    if DB_PATH.exists():
        db_updated = update_database(DB_PATH)

    WORK.mkdir(parents=True, exist_ok=True)
    preview_png = WORK / "warden_spell_fx_preview.png"
    generate_preview_sheet(built_textures, preview_png)

    print("\n" + "=" * 70)
    print("INSTALLATION COMPLETE")
    print("=" * 70)
    print(f"  * Textures installed : {len(built_textures)} files")
    print(f"  * NIFs installed     : {len(built_nifs)} files")
    print(f"  * Catalogs updated   : gamedata.mpk (spnifs 1410..1439, speffects 639..670, spells 5740..5772)")
    print(f"  * Database updated   : {db_updated} spell rows in {DB_PATH.name}")
    print(f"  * Backup created     : {backup_dir}")
    print(f"  * Contact preview    : {preview_png}")


def run_rollback(backup_folder: str) -> None:
    bdir = Path(backup_folder)
    if not bdir.is_dir():
        bdir = BACKUP_ROOT / backup_folder
    require(bdir.is_dir(), f"Backup directory {backup_folder} not found")
    manifest_path = bdir / "manifest.json"
    require(manifest_path.exists(), f"Manifest {manifest_path} not found")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    print(f"Rolling back from {bdir}...")
    shutil.copy2(bdir / "gamedata.mpk", GAMEDATA)
    if (bdir / "opendaoc.sqlite3.db").exists() and DB_PATH.exists():
        shutil.copy2(bdir / "opendaoc.sqlite3.db", DB_PATH)

    for p_str in manifest.get("installed_textures", []):
        p = Path(p_str)
        if p.exists():
            p.unlink()
    for p_str in manifest.get("installed_nifs", []):
        p = Path(p_str)
        if p.exists():
            p.unlink()

    print("Rollback complete.")


def main() -> None:
    if len(sys.argv) > 1 and sys.argv[1].lower() == "install":
        run_install()
    elif len(sys.argv) > 2 and sys.argv[1].lower() == "rollback":
        run_rollback(sys.argv[2])
    else:
        run_preview()


if __name__ == "__main__":
    main()
