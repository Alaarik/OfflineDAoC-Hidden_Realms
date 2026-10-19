"""Idempotent database seeder for the Council Warden (Warden of the White Council)
cross-realm hybrid class across Albion (Class 60), Midgard (Class 61), and
Hibernia (Class 58).

Creates a backup of the target SQLite database before applying changes.
Supports `--db <path>` to target any SQLite database (e.g. a temporary copy
for verification).
"""
from __future__ import annotations

import argparse
import sqlite3
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DB_PATH = ROOT / "playable-v0.35b" / "runtime" / "data" / "opendaoc.sqlite3.db"
if not DEFAULT_DB_PATH.exists() and (ROOT / "runtime" / "data" / "opendaoc.sqlite3.db").exists():
    DEFAULT_DB_PATH = ROOT / "runtime" / "data" / "opendaoc.sqlite3.db"

CLASS_ALB = 60  # eCharacterClass.MaulerAlb -> Albion Council Warden
CLASS_MID = 61  # eCharacterClass.MaulerMid -> Midgard Council Warden
CLASS_HIB = 58  # eCharacterClass.Vampiir   -> Hibernia Council Warden
WARDEN_CLASS_IDS = (CLASS_ALB, CLASS_MID, CLASS_HIB)

WARDEN_CLIENT_EFFECTS: dict[int, int] = {
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
}

CAREER_ALB = "MaulerAlbCareer"
CAREER_MID = "MaulerMidCareer"
CAREER_HIB = "VampiirCareer"

SPEC_EVOCATION = "Evocation"
SPEC_ABJURATION = "Abjuration"
SPEC_THAUMATURGY = "Thaumaturgy"

LINE_MINOR_EVOCATION = "Minor Evocation"
LINE_EVOCATION = "Evocation"
LINE_MINOR_ABJURATION = "Minor Abjuration"
LINE_ABJURATION = "Abjuration"
LINE_MINOR_THAUMATURGY = "Minor Thaumaturgy"
LINE_THAUMATURGY = "Thaumaturgy"

WARDEN_SPEC_KEYS = (CAREER_ALB, CAREER_MID, CAREER_HIB, SPEC_EVOCATION, SPEC_ABJURATION, SPEC_THAUMATURGY)
WARDEN_LINE_KEYS = (LINE_MINOR_EVOCATION, LINE_EVOCATION, LINE_MINOR_ABJURATION, LINE_ABJURATION, LINE_MINOR_THAUMATURGY, LINE_THAUMATURGY)

CLIENT_TOOLTIP_START = 29200
CLIENT_TOOLTIP_LIMIT = 29399

MOB_IDS = (
    "council_warden_trainer_camelot",
    "council_warden_trainer_cotswold",
    "council_warden_trainer_camp_forest",
    "council_warden_trainer_jordheim",
    "council_warden_trainer_mularn",
    "council_warden_trainer_tir_na_nog",
    "council_warden_trainer_mag_mell",
)


def copy_backup(db_path: Path, backup_path: Path) -> None:
    if backup_path.exists():
        return
    source = sqlite3.connect(f"file:{db_path.as_posix()}?mode=ro", uri=True)
    target = sqlite3.connect(backup_path)
    try:
        source.backup(target)
    finally:
        target.close()
        source.close()


def clone_spell(
    conn: sqlite3.Connection,
    template_id: int,
    spell_id: int,
    name: str,
    description: str,
    line: str | None,
    line_level: int = 1,
    **changes: object,
) -> None:
    template = conn.execute("SELECT * FROM Spell WHERE SpellID = ?", (template_id,)).fetchone()
    if template is None:
        raise RuntimeError(f"Missing spell template {template_id}")
    columns = [row[1] for row in conn.execute("PRAGMA table_info(Spell)")]
    values = dict(zip(columns, template))

    tooltip_id = changes.pop("TooltipId", None)
    if tooltip_id is None:
        tooltip_id = CLIENT_TOOLTIP_START + (spell_id - 59200)
        if tooltip_id > CLIENT_TOOLTIP_LIMIT:
            raise RuntimeError(f"TooltipId {tooltip_id} exceeds Council Warden range {CLIENT_TOOLTIP_LIMIT}")

    values.update({
        "SpellID": spell_id,
        "Name": name,
        "Description": description,
        "TooltipId": int(tooltip_id),
        "Spell_ID": f"CouncilWarden_{spell_id}",
        "PackageID": "CouncilWarden",
        "LastTimeRowUpdated": "2000-01-01 00:00:00",
    })
    if "ClientEffect" not in changes and spell_id in WARDEN_CLIENT_EFFECTS:
        values["ClientEffect"] = WARDEN_CLIENT_EFFECTS[spell_id]
    values.update(changes)
    conn.execute(
        f"INSERT INTO Spell ({','.join(columns)}) VALUES ({','.join('?' for _ in columns)})",
        [values[column] for column in columns],
    )
    if line is not None:
        conn.execute(
            "INSERT INTO LineXSpell (LineName, SpellID, Level, LastTimeRowUpdated, LineXSpell_ID) VALUES (?, ?, ?, ?, ?)",
            (line, spell_id, line_level, "2000-01-01 00:00:00", f"{line}_{spell_id}"),
        )


def clone_class_styles(conn: sqlite3.Connection, source_class_id: int, target_class_id: int, spec_key: str) -> None:
    style_columns = [row[1] for row in conn.execute("PRAGMA table_info(Style)") if row[1] != "StyleID"]
    rows = conn.execute(
        f"SELECT {','.join(style_columns)} FROM Style WHERE ClassId = ? AND SpecKeyName = ? ORDER BY ID",
        (source_class_id, spec_key),
    ).fetchall()
    if not rows:
        raise RuntimeError(f"No reference styles found for ClassId={source_class_id}, SpecKeyName={spec_key}")
    for row in rows:
        values = dict(zip(style_columns, row))
        values["ClassId"] = target_class_id
        values["LastTimeRowUpdated"] = "2000-01-01 00:00:00"
        conn.execute(
            f"INSERT INTO Style ({','.join(style_columns)}) VALUES ({','.join('?' for _ in style_columns)})",
            [values[col] for col in style_columns],
        )


def seed_warden_custom_styles(conn: sqlite3.Connection, now: str) -> None:
    """Seeds the 5 custom Warden Swordsmanship styles and their StyleXSpell procs
    across all 5 Council Warden weapon trees (Slash, Thrust, Sword, Blades, Piercing).
    """
    # 1. Seed style proc spells (59301..59307)
    # 59301: Silver Arc proc -> Kinetic Shock (35 pt Energy DD)
    clone_spell(
        conn, 471, 59301, "Kinetic Shock",
        "Discharges a 35-point burst of kinetic energy along the blade.",
        None, 10,
        Type="DirectDamage", Target="Enemy", Range=350, CastTime=0.0,
        Damage=35.0, DamageType=12, Power=0, Duration=0,
    )
    # 59302: Riposte of Force proc -> 3s Stun
    clone_spell(
        conn, 471, 59302, "Force Riposte",
        "Stuns the attacker for 3 seconds with a concussive parry counter.",
        None, 18,
        Type="StyleStun", Target="Enemy", Range=350, CastTime=0.0,
        Damage=0.0, DamageType=14, Power=0, Duration=3,
    )
    # 59304: Flank Sever sub-spell -> Bleed (5/tick for 15s)
    clone_spell(
        conn, 301, 59304, "Severed Artery",
        "Inflicts 5 bleeding damage every 3 seconds for 15 seconds.",
        None, 29,
        Type="StyleBleeding", Target="Enemy", Range=350, CastTime=0.0,
        Damage=5.0, DamageType=10, Power=0, Duration=15, Frequency=3000,
    )
    # 59303: Flank Sever proc -> 40% Movement Snare (15s) + Bleed sub-spell (59304)
    clone_spell(
        conn, 171, 59303, "Hamstringing Sever",
        "Reduces the target's movement speed by 40% for 15 seconds and causes bleeding.",
        None, 29,
        Type="StyleSpeedDecrease", Target="Enemy", Range=350, CastTime=0.0,
        Damage=0.0, Value=40.0, DamageType=10, Power=0, Duration=15,
        SubSpellID=59304,
    )
    # 59306: Counter-Thrust sub-spell -> -10% Attack Speed debuff (15s)
    clone_spell(
        conn, 171, 59306, "Searing Lethargy",
        "Reduces the target's melee attack speed by 10% for 15 seconds.",
        None, 39,
        Type="StyleCombatSpeedDebuff", Target="Enemy", Range=350, CastTime=0.0,
        Damage=0.0, Value=10.0, DamageType=13, Power=0, Duration=15,
    )
    # 59305: Counter-Thrust proc -> Searing Thrust (85 pt Heat DD + 59306 attack speed debuff)
    clone_spell(
        conn, 303, 59305, "Searing Thrust",
        "Sears the target for 85 Heat damage and slows their attack speed by 10%.",
        None, 39,
        Type="DirectDamage", Target="Enemy", Range=350, CastTime=0.0,
        Damage=85.0, DamageType=13, Power=0, Duration=0,
        SubSpellID=59306,
    )
    # 59307: Spellsever proc -> Arcane Disruption (110 pt Energy DD + Strip 1 buff + 4s interrupt)
    clone_spell(
        conn, 474, 59307, "Arcane Disruption",
        "Inflicts 110 Energy damage, strips 1 beneficial magical buff, and interrupts spellcasting for 4 seconds.",
        None, 50,
        Type="CouncilWardenSpellsever", Target="Enemy", Range=350, CastTime=0.0,
        Damage=110.0, DamageType=12, Power=0, Duration=0,
    )

    weapon_trees = [
        (CLASS_ALB, "Slash", 3, 56, 5900),
        (CLASS_ALB, "Thrust", 4, 131, 5910),
        (CLASS_MID, "Sword", 11, 146, 5920),
        (CLASS_HIB, "Blades", 19, 245, 5930),
        (CLASS_HIB, "Piercing", 21, 275, 5940),
    ]

    for class_id, spec_key, weapon_type, base_anim, base_style_id in weapon_trees:
        counter_thrust_id = base_style_id + 4
        custom_styles = [
            # (id_offset, name, spec_lvl, end_cost, open_type, open_val, atk_req, growth, hit, def, proc_spell_id)
            (1, "Silver Arc", 10, 10, 0, 0, 0, 0.48, 5, 0, 59301),
            (2, "Riposte of Force", 18, 5, 1, 0, 3, 0.62, 15, 10, 59302),
            (3, "Flank Sever", 29, 10, 2, 1, 0, 0.68, 10, 5, 59303),
            (4, "Counter-Thrust", 39, 5, 1, 0, 5, 0.78, 20, 10, 59305),
            (5, "Spellsever", 50, 16, 0, 0, 0, 0.88, 15, 10, 59307),
        ]
        for offset, name, spec_lvl, end_cost, open_type, open_val, atk_req, growth, hit, defense, proc_spell_id in custom_styles:
            style_id = base_style_id + offset
            conn.execute(
                """INSERT INTO Style (
                    ID, ClassId, Name, SpecKeyName, SpecLevelRequirement, Icon, EnduranceCost,
                    StealthRequirement, OpeningRequirementType, OpeningRequirementValue,
                    AttackResultRequirement, WeaponTypeRequirement, GrowthOffset, GrowthRate,
                    BonusToHit, BonusToDefense, TwoHandAnimation, RandomProc, ArmorHitLocation,
                    LastTimeRowUpdated
                ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
                (
                    style_id, class_id, name, spec_key, spec_lvl, base_anim + offset, end_cost,
                    0, open_type, open_val,
                    atk_req, weapon_type, 0.0, growth,
                    hit, defense, base_anim + offset, 0, 0,
                    now,
                ),
            )
            conn.execute(
                "INSERT INTO StyleXSpell (SpellID, ClassID, StyleID, Chance, LastTimeRowUpdated, StyleXSpell_ID) VALUES (?,?,?,?,?,?)",
                (proc_spell_id, class_id, style_id, 100, now, f"CouncilWarden_StyleProc_{class_id}_{style_id}"),
            )


def seed_edinburgh_and_nevernever(conn: sqlite3.Connection, now: str) -> None:
    """Seeds Phase 3:
    1. 'Open Way (Apertura Viae)' spell (SpellID 62090) granted at Level 5 to Council Wardens
       and all Arcane/Primal caster baseline spell lines.
    2. Factions 500 (Seelie Court) and 501 (Unseelie Court) linked 1:1 as opposing courts.
    3. Edinburgh (Regions 65 & 192) and Nevernever (Regions 197, 196, 195, 94) region/zone
       descriptions and bidirectional ZonePoint transitions (including EdinburghConclaveJumpPoint).
    4. Black-Market Talisman ('silver_thimble_of_the_veil') and True Cold Iron weapon ('legions_severing_edge').
    """
    # 1. Open Way (Apertura Viae) - SpellID 62090
    clone_spell(
        conn, 471, 62090, "Open Way (Apertura Viae)",
        "Tears open a shimmering rift across the Veil into the Nevernever using the mortal zone's resonance (Summer Faerie in lush zones, Winter Faerie in bleak zones, or the Edinburgh Crossing at Hadrian's Wall).",
        LINE_MINOR_ABJURATION, 5,
        TooltipId=29390,
        Type="CouncilWardenOpenWay", Target="Self", Range=0, CastTime=4.0,
        Duration=0, Damage=0.0, Value=0.0, Power=15,
        RecastDelay=10, SharedTimerGroup=62090, ClientEffect=4310, Icon=4310,
    )

    caster_baseline_lines = (
        "Path of Earth",
        "Matter",
        "Darkness",
        "Cursing",
        "Subterranean Incantations",
        "Way of the Sun",
        "Arboreal Path",
        "Valewalker Arboreal Path Base",
        "Spectral Force",
        "Regrowth",
        "Deathsight",
        "Abhartach's Rot",
    )
    for line_name in caster_baseline_lines:
        exists = conn.execute("SELECT 1 FROM SpellLine WHERE KeyName = ?", (line_name,)).fetchone()
        if exists is not None:
            conn.execute(
                "INSERT INTO LineXSpell (LineName, SpellID, Level, LastTimeRowUpdated, LineXSpell_ID) VALUES (?, ?, ?, ?, ?)",
                (line_name, 62090, 5, now, f"OpenWay_{line_name.replace(' ', '_')}_62090"),
            )

    # 2. Seelie Court (500) & Unseelie Court (501) Factions
    conn.executemany(
        "INSERT INTO Faction (ID, Name, BaseAggroLevel, LastTimeRowUpdated, Faction_ID) VALUES (?, ?, ?, ?, ?)",
        [
            (500, "The Seelie Court (Summer Court)", 0, now, "Faction_SeelieCourt_500"),
            (501, "The Unseelie Court (Winter Court)", 0, now, "Faction_UnseelieCourt_501"),
        ],
    )
    conn.executemany(
        "INSERT INTO LinkedFaction (FactionID, LinkedFactionID, IsFriend, LastTimeRowUpdated, LinkedFaction_ID) VALUES (?, ?, ?, ?, ?)",
        [
            (500, 501, 0, now, "LinkedFaction_500_501"),
            (501, 500, 0, now, "LinkedFaction_501_500"),
        ],
    )

    # 3. Update Edinburgh & Nevernever Region/Zone descriptions and seed ZonePoints
    region_descriptions = [
        ("Edinburgh: Catacombs & Cloister", 65),
        ("Edinburgh: Grand Conclave Hall", 192),
        ("The Nevernever", 197),
        ("The Nevernever: Summer Court", 196),
        ("The Nevernever: Winter Court", 195),
        ("The Queen's Labyrinth", 94),
    ]
    for desc, reg_id in region_descriptions:
        conn.execute("UPDATE Regions SET Description = ? WHERE RegionID = ?", (desc, reg_id))
        conn.execute("UPDATE Zones SET Name = ? WHERE RegionID = ?", (desc, reg_id))

    # (Id, TargetRegion, TargetX, TargetY, TargetZ, TargetHeading, SourceRegion, SourceX, SourceY, SourceZ, Realm, ClassType)
    zone_points = [
        # Region 65 (Edinburgh Tier 1) -> Hadrian's Wall (Region 1)
        (243, 1, 629700, 342150, 4200, 2048, 65, 34707, 47542, 16145, 0, ""),
        # Region 65 (Edinburgh Tier 1) -> Region 192 (Edinburgh Tier 2 Grand Conclave Hall - Mortal Humans Only)
        (244, 192, 35089, 32498, 16166, 2048, 65, 39011, 44352, 16400, 0, "DOL.GS.ServerRules.EdinburghConclaveJumpPoint"),
        # Region 65 (Edinburgh Tier 1) -> Region 197 (The Nevernever - Edinburgh Portal Waystone)
        (245, 197, 32000, 32000, 16000, 2048, 65, 37210, 29052, 16400, 0, ""),
        # Region 192 (Edinburgh Tier 2) -> Region 65 (Edinburgh Tier 1 Catacombs)
        (731, 65, 39011, 44100, 16400, 2048, 192, 35089, 32498, 16166, 0, ""),
        # Region 197 (The Nevernever) -> Region 195 (Winter Court Wastes)
        (258, 195, 32852, 14647, 16000, 2048, 197, 19332, 24042, 16000, 0, ""),
        # Region 197 (The Nevernever) -> Region 196 (Summer Court Heartlands)
        (259, 196, 25752, 21072, 16000, 2048, 197, 49917, 32592, 16000, 0, ""),
        # Region 196 (Summer Court) -> Region 197 (The Nevernever)
        (430, 197, 49400, 32592, 16000, 1024, 196, 25752, 21072, 16000, 0, ""),
        # Region 196 (Summer Court) -> Region 94 (The Queen's Labyrinth)
        (594, 94, 34842, 22217, 20400, 2048, 196, 40432, 39829, 15000, 0, ""),
        # Region 195 (Winter Court) -> Region 197 (The Nevernever)
        (429, 197, 19800, 24042, 16000, 3072, 195, 32852, 14647, 16000, 0, ""),
        # Region 195 (Winter Court) -> Region 94 (The Queen's Labyrinth)
        (586, 94, 40632, 24417, 21100, 2048, 195, 44032, 36247, 15000, 0, ""),
        # Region 94 (Queen's Labyrinth) -> Region 195 (Winter Court)
        (264, 195, 44032, 36247, 15000, 2048, 94, 40632, 24417, 21100, 0, ""),
        # Region 94 (Queen's Labyrinth) -> Region 196 (Summer Court)
        (265, 196, 40432, 39829, 15000, 2048, 94, 34842, 22217, 20400, 0, ""),
    ]
    for zp_id, t_reg, tx, ty, tz, th, s_reg, sx, sy, sz, realm, ctype in zone_points:
        conn.execute(
            """INSERT INTO ZonePoint (
                Id, TargetX, TargetY, TargetZ, TargetRegion, TargetHeading,
                SourceX, SourceY, SourceZ, SourceRegion, Realm, ClassType,
                LastTimeRowUpdated, ZonePoint_ID
            ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
            (zp_id, tx, ty, tz, t_reg, th, sx, sy, sz, s_reg, realm, ctype, now, f"EdinburghNevernever_ZP_{zp_id}"),
        )

    # 4. Seed Silver Thimble of the Veil and Legion's Severing Edge ItemTemplates
    ref_item = conn.execute("SELECT * FROM ItemTemplate LIMIT 1").fetchone()
    if ref_item is not None:
        item_cols = [row[1] for row in conn.execute("PRAGMA table_info(ItemTemplate)")]
        base_item = dict(zip(item_cols, ref_item))
        for k in item_cols:
            if k.startswith(("Bonus", "ExtraBonus")):
                base_item[k] = 0

        thimble = dict(base_item)
        thimble.update({
            "Id_nb": "silver_thimble_of_the_veil",
            "Name": "Silver Thimble of the Veil",
            "Level": 5,
            "Quality": 100,
            "Durability": 50000,
            "MaxDurability": 50000,
            "Condition": 50000,
            "MaxCondition": 50000,
            "Model": 499,
            "Object_Type": 41,  # Magical
            "Item_Type": 40,    # Backpack usable
            "Realm": 0,
            "AllowedClasses": "",
            "SpellID": 62090,
            "Charges": 50,
            "MaxCharges": 50,
            "Price": 50000,
            "IsPickable": 1,
            "IsDropable": 1,
            "IsTradable": 1,
            "CanDropAsLoot": 0,
            "PackageID": "EdinburghNevernever",
            "Description": "A tarnished silver thimble etched with smuggled Way-finding sigils. Allows non-casters to prick the Veil and step into the Nevernever.",
            "LastTimeRowUpdated": now,
            "ItemTemplate_ID": "silver_thimble_of_the_veil",
        })
        conn.execute(
            f"INSERT INTO ItemTemplate ({','.join(item_cols)}) VALUES ({','.join('?' for _ in item_cols)})",
            [thimble[c] for c in item_cols],
        )

        cold_iron = dict(base_item)
        cold_iron.update({
            "Id_nb": "legions_severing_edge",
            "Name": "Legion's Severing Edge",
            "Level": 51,
            "Quality": 100,
            "Bonus": 35,
            "Durability": 50000,
            "MaxDurability": 50000,
            "Condition": 50000,
            "MaxCondition": 50000,
            "Model": 3,
            "Object_Type": 1,   # GenericWeapon
            "Item_Type": 10,    # RightHandWeapon
            "Type_Damage": 2,   # Slash
            "DPS_AF": 165,
            "SPD_ABS": 36,
            "Hand": 0,
            "Realm": 0,
            "AllowedClasses": "60;61;58",
            "Bonus1": 18, "Bonus1Type": 1,   # STR
            "Bonus2": 18, "Bonus2Type": 5,   # INT
            "Bonus3": 15, "Bonus3Type": 2,   # DEX
            "Bonus4": 4,  "Bonus4Type": 163, # AllMeleeWeaponSkills
            "Bonus5": 4,  "Bonus5Type": 22,  # Parry
            "Price": 200,
            "IsPickable": 1,
            "IsDropable": 1,
            "IsTradable": 0,
            "CanDropAsLoot": 1,
            "Charges": 10,
            "MaxCharges": 10,
            "SpellID": 59360,
            "PackageID": "ColdIron",
            "Description": "Forged from unrefined meteoritic Cold Iron. Deals +50% bonus damage to Fae entities, shatters their magical wards, and completely halts Fae regeneration.",
            "LastTimeRowUpdated": now,
            "ItemTemplate_ID": "legions_severing_edge",
        })
        conn.execute(
            f"INSERT INTO ItemTemplate ({','.join(item_cols)}) VALUES ({','.join('?' for _ in item_cols)})",
            [cold_iron[c] for c in item_cols],
        )


def seed_phase4_epic_quests_and_raid_drops(conn: sqlite3.Connection, now: str) -> None:
    """Seeds Phase 4:
    1. Item proc and use spells (59320..59334) for The Warden's Grey Cloak, Silver Blades,
       and Epic Dungeon Raid Drops.
    2. All 37 Council Warden ItemTemplates (Initiate Cloak, Chapters 1-5 milestone items,
       Albion/Midgard/Hibernia Level 50 Epic Armor + Silver Blades, and Section 8 Raid Drops).
    3. LootTemplate entries linking Section 8 Raid Drops to Apocalypse (Caer Sidi),
       Queen Kula (Tuscaran Glacier), Olcasgean (Galladoria), Legion (Darkness Falls),
       and Grand Summoner Govannon (Summoner's Hall).
    """
    # 1. Seed Item Use & Proc Spells (59320..59334)
    # 59320: Circle of Grounding (Cloak Use - 150 pt MagicAblativeArmor)
    clone_spell(
        conn, 8041, 59320, "Circle of Grounding (Cloak)",
        "Erects a personal kinetic grounding barrier absorbing up to 150 magical damage.",
        None, 45,
        Type="MagicAblativeArmor", Target="Self", Range=0, CastTime=0.0,
        Duration=60, Damage=75.0, Value=150.0, Power=0,
    )
    # 59321: Fera-Silver Weave (Cloak Reactive Proc - 85 Energy DD)
    clone_spell(
        conn, 474, 59321, "Fera-Silver Weave",
        "Discharges a reactive kinetic pulse dealing 85 Energy damage to the attacker.",
        None, 45,
        Type="DirectDamage", Target="Enemy", Range=350, CastTime=0.0,
        Damage=85.0, DamageType=12, Power=0,
    )
    # 59322: Silver Radiance (Silver Blade Offensive Proc - 65 Spirit DD)
    clone_spell(
        conn, 474, 59322, "Silver Radiance",
        "Severs hostile wards with a flash of enchanted silver, dealing 65 Spirit damage.",
        None, 50,
        Type="DirectDamage", Target="Enemy", Range=350, CastTime=0.0,
        Damage=65.0, DamageType=15, Power=0,
    )
    # 59323: Aegis of the First Wardens (Loricam Incorruptus Defensive Proc - 80 pt BothAblativeArmor)
    clone_spell(
        conn, 8041, 59323, "Aegis of the First Wardens",
        "Merlin's Camlann glyphs absorb up to 80 points of incoming physical or magical damage.",
        None, 50,
        Type="BothAblativeArmor", Target="Self", Range=0, CastTime=0.0,
        Duration=30, Damage=50.0, Value=80.0, Power=0,
    )
    # 59330: Kinetic Drag Sub-Spell (-15% Attack Speed for 10s)
    clone_spell(
        conn, 1072, 59330, "Kinetic Drag (Slow)",
        "Slows the target's melee attack speed by 15% for 10 seconds.",
        None, 50,
        Type="CombatSpeedDebuff", Target="Enemy", Range=350, CastTime=0.0,
        Duration=10, Value=15.0, DamageType=15, Power=0,
    )
    # 59324: Kinetic Drag (Severance of Camlann Proc - 70 Spirit DD + 59330 SubSpell)
    clone_spell(
        conn, 474, 59324, "Kinetic Drag",
        "Inflicts 70 Spirit damage and slows the target's melee attack speed by 15% for 10 seconds.",
        None, 50,
        Type="DirectDamage", Target="Enemy", Range=350, CastTime=0.0,
        Damage=70.0, DamageType=15, Power=0, SubSpellID=59330,
    )
    # 59331: Thermal Feedback Sub-Spell (80 pt BothAblativeArmor)
    clone_spell(
        conn, 8041, 59331, "Thermal Feedback (Ward)",
        "Absorbs up to 80 points of incoming damage.",
        None, 50,
        Type="BothAblativeArmor", Target="Self", Range=0, CastTime=0.0,
        Duration=30, Damage=50.0, Value=80.0, Power=0,
    )
    # 59325: Thermal Feedback (Mantle of the Winter Gate Defensive Proc - 65 Heat DD + 59331 SubSpell)
    clone_spell(
        conn, 303, 59325, "Thermal Feedback",
        "Absorbs 80 points of incoming damage and lashes the attacker for 65 Heat damage.",
        None, 50,
        Type="DirectDamage", Target="Enemy", Range=350, CastTime=0.0,
        Damage=65.0, DamageType=13, Power=0, SubSpellID=59331,
    )
    # 59332: Glacial Rupture Sub-Spell (25% Movement Snare for 5s)
    clone_spell(
        conn, 171, 59332, "Glacial Rupture (Snare)",
        "Slows the target's movement speed by 25% for 5 seconds.",
        None, 50,
        Type="SpeedDecrease", Target="Enemy", Range=350, CastTime=0.0,
        Duration=5, Value=25.0, DamageType=11, Power=0,
    )
    # 59326: Glacial Rupture (Rimelance Proc - 70 Cold DD + 59332 SubSpell)
    clone_spell(
        conn, 1, 59326, "Glacial Rupture",
        "Inflicts 70 Cold damage and slows the target's movement speed by 25% for 5 seconds.",
        None, 50,
        Type="DirectDamage", Target="Enemy", Range=350, CastTime=0.0,
        Damage=70.0, DamageType=11, Power=0, SubSpellID=59332,
    )
    # 59333: Flourish of Will Sub-Spell (+60 Endurance)
    clone_spell(
        conn, 1, 59333, "Flourish of Will (Vigor)",
        "Restores 60 Endurance.",
        None, 50,
        Type="EnduranceHeal", Target="Self", Range=0, CastTime=0.0,
        Value=60.0, Power=0,
    )
    # 59327: Flourish of Will (Cuirass of the Summer Solstice Defensive Proc - 80 pt BothAblativeArmor + 59333 SubSpell)
    clone_spell(
        conn, 8041, 59327, "Flourish of Will",
        "Absorbs 80 points of incoming damage and restores 60 Endurance.",
        None, 50,
        Type="BothAblativeArmor", Target="Self", Range=0, CastTime=0.0,
        Duration=30, Damage=50.0, Value=80.0, Power=0, SubSpellID=59333,
    )
    # 59334: Solar Flare Sub-Spell (WeaponskillConstitutionDebuff for 8s)
    clone_spell(
        conn, 1081, 59334, "Solar Flare (Blind)",
        "Reduces the target's combat accuracy for 8 seconds.",
        None, 50,
        Type="WeaponskillConstitutionDebuff", Target="Enemy", Range=350, CastTime=0.0,
        Duration=8, Value=25.0, DamageType=13, Power=0,
    )
    # 59328: Solar Flare (Sunshard Proc - 65 Heat DD + 59334 SubSpell)
    clone_spell(
        conn, 303, 59328, "Solar Flare",
        "Inflicts 65 Heat damage and reduces the target's combat accuracy for 8 seconds.",
        None, 50,
        Type="DirectDamage", Target="Enemy", Range=350, CastTime=0.0,
        Damage=65.0, DamageType=13, Power=0, SubSpellID=59334,
    )
    # 59329: Sigil of Truth (Inquisitor's Torque Use - CureDisease/Poison)
    clone_spell(
        conn, 1, 59329, "Sigil of Truth",
        "Invokes the White Council's Sigil of Truth to purge hostile corruptions.",
        None, 50,
        Type="CureDisease", Target="Self", Range=0, CastTime=0.0,
        Power=0,
    )
    # 59360: Severing Bolt of the Abyss (Legion's Severing Edge / Stave Use Charge - 185 Energy DD)
    clone_spell(
        conn, 471, 59360, "Severing Bolt of the Abyss",
        "Unleashes a focused bolt of abyss-cleaving energy for 185 Energy damage.",
        None, 50,
        Type="CouncilWardenSeveringBolt", Target="Enemy", Range=1500, CastTime=0.0,
        Damage=185.0, DamageType=12, Power=0, RecastDelay=90,
    )
    # 59361: Kinetic Deflection (Off-Hand Focus Reactive Proc - 100 pt Physical Ablative)
    clone_spell(
        conn, 8040, 59361, "Kinetic Deflection",
        "Your off-hand focus snaps a reactive kinetic deflection barrier into place, absorbing up to 100 physical damage.",
        None, 50,
        Type="CouncilWardenKineticDeflection", Target="Self", Range=0, CastTime=0.0,
        Value=100.0, Duration=30, Power=0,
    )

    # 2. Seed Council Warden Quest, Epic Armor, Silver Blade, and Raid Drop ItemTemplates
    ref_item = conn.execute("SELECT * FROM ItemTemplate LIMIT 1").fetchone()
    if ref_item is not None:
        item_cols = [r[1] for r in conn.execute("PRAGMA table_info(ItemTemplate)")]
        base_item = dict(zip(item_cols, ref_item))

        chest_bonuses = [(1, 15), (3, 15), (10, 40), (11, 8), (19, 8), (163, 3)]
        arms_bonuses = [(1, 14), (2, 14), (4, 12), (17, 8), (18, 8)]
        legs_bonuses = [(3, 15), (5, 15), (10, 36), (14, 8), (12, 8), (108, 3)]
        helm_bonuses = [(5, 14), (4, 14), (2, 12), (10, 0), (16, 8), (13, 8), (164, 2)]
        hands_bonuses = [(1, 14), (2, 14), (4, 12), (15, 8), (163, 3), (155, 3)]
        boots_bonuses = [(3, 14), (4, 14), (10, 32), (11, 8), (17, 8), (108, 3)]
        blade_bonuses = [(1, 16), (5, 16), (2, 14), (10, 32), (163, 3), (108, 3), (191, 3)]

        # (id_nb, name, level, model, obj_type, item_type, dps_af, spd_abs, weight, realm, bonuses, type_dmg, proc_id, spell_id, charges, can_drop, desc)
        phase4_items = [
            ("council_warden_initiate_cloak", "Initiate's Grey Cloak", 5, 443, 41, 26, 0, 0, 8, 0, [(1, 3), (5, 3), (10, 8), (13, 2), (18, 2)], 0, 0, 0, 0, 0, "The grey wool duster presented to a newly sworn initiate of the White Council Wardens."),
            ("council_warden_ch1_signet", "Signet of the Novice Warden", 10, 499, 41, 35, 0, 0, 2, 0, [(1, 4), (5, 4), (10, 12), (18, 2)], 0, 0, 0, 0, 0, "A bronze signet bearing the nascent seal of Edinburgh, attuned to novice warding circles."),
            ("council_warden_ch2_bracer", "Operative's Warded Bracer", 20, 498, 41, 33, 0, 0, 4, 0, [(1, 7), (3, 7), (10, 20), (17, 4), (19, 4)], 0, 0, 0, 0, 0, "Inscribed with border-ward runes commemorating the ancient Arthurian cold-iron compacts with the Sidhe."),
            ("council_warden_ch3_belt", "Cincture of the Ley-Caster", 30, 597, 41, 32, 0, 0, 5, 0, [(5, 10), (2, 10), (4, 10), (10, 28), (16, 6), (13, 6)], 0, 0, 0, 0, 0, "Woven from ley-line silver thread, grounding excess magical discharge during intense combat."),
            ("council_warden_ch4_neck", "Pendant of the White Council", 40, 494, 41, 29, 0, 0, 3, 0, [(1, 13), (3, 13), (5, 13), (10, 36), (14, 6), (12, 6), (164, 2)], 0, 0, 0, 0, 0, "Official credentials worn by full-fledged Council investigators dispatched to the frontier."),
            ("council_warden_ch5_cloak", "The Warden's Grey Cloak", 45, 443, 41, 26, 0, 0, 10, 0, [(1, 16), (5, 16), (3, 14), (4, 14), (10, 44), (16, 8), (18, 8), (13, 8), (14, 8)], 0, 59321, 59320, 10, 0, "The iconic symbol of a White Council Warden. A heavy, warded grey wool duster treated to repel both the elements and magical aggression."),
            # Albion Epic Set (Chain AF 102 Abs 27% + Silver Blades)
            ("council_warden_epic_alb_hauberk", "Hauberk of the White Council", 50, 713, 35, 25, 102, 27, 45, 1, chest_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_alb_sleeves", "Sleeves of the White Council", 50, 715, 35, 28, 102, 27, 25, 1, arms_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_alb_chausses", "Chausses of the White Council", 50, 714, 35, 27, 102, 27, 32, 1, legs_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_alb_coif", "Coif of the White Council", 50, 822, 35, 21, 102, 27, 20, 1, helm_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_alb_gauntlets", "Gauntlets of the White Council", 50, 716, 35, 22, 102, 27, 16, 1, hands_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_alb_boots", "Sabatons of the White Council", 50, 717, 35, 23, 102, 27, 18, 1, boots_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_alb_broadsword", "Silver Broadsword of the Council", 50, 3, 3, 10, 162, 38, 28, 1, blade_bonuses, 2, 59322, 0, 0, 0, "Custom-forged from enchanted silver by Senior Council metallurgists. Channels Silver Radiance on strike."),
            ("council_warden_epic_alb_estoc", "Silver Estoc of the Council", 50, 22, 4, 10, 162, 33, 24, 1, blade_bonuses, 3, 59322, 0, 0, 0, "Custom-forged from enchanted silver by Senior Council metallurgists. Channels Silver Radiance on strike."),
            # Midgard Epic Set (Studded AF 102 Abs 19% + Silver Spatha)
            ("council_warden_epic_mid_hauberk", "Studded Hauberk of the Warden", 50, 761, 34, 25, 102, 19, 40, 2, chest_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_mid_vambraces", "Studded Vambraces of the Warden", 50, 763, 34, 28, 102, 19, 22, 2, arms_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_mid_leggings", "Studded Leggings of the Warden", 50, 762, 34, 27, 102, 19, 28, 2, legs_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_mid_helm", "Studded Helm of the Warden", 50, 829, 34, 21, 102, 19, 18, 2, helm_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_mid_gauntlets", "Studded Gauntlets of the Warden", 50, 764, 34, 22, 102, 19, 14, 2, hands_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_mid_sabatons", "Studded Sabatons of the Warden", 50, 765, 34, 23, 102, 19, 16, 2, boots_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_mid_spatha", "Silver Spatha of the Council", 50, 311, 11, 10, 162, 37, 27, 2, blade_bonuses, 2, 59322, 0, 0, 0, "Custom-forged from enchanted silver by Senior Council metallurgists. Channels Silver Radiance on strike."),
            # Hibernia Epic Set (Reinforced AF 102 Abs 19% + Silver Blades)
            ("council_warden_epic_hib_vest", "Reinforced Vest of the Council", 50, 738, 38, 25, 102, 19, 40, 3, chest_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_hib_sleeves", "Reinforced Sleeves of the Council", 50, 740, 38, 28, 102, 19, 22, 3, arms_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_hib_leggings", "Reinforced Leggings of the Council", 50, 739, 38, 27, 102, 19, 28, 3, legs_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_hib_cap", "Reinforced Cap of the Council", 50, 834, 38, 21, 102, 19, 18, 3, helm_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_hib_gloves", "Reinforced Gloves of the Council", 50, 741, 38, 22, 102, 19, 14, 3, hands_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_hib_boots", "Reinforced Boots of the Council", 50, 742, 38, 23, 102, 19, 16, 3, boots_bonuses, 0, 0, 0, 0, 0, "Forged in the Sanctuary of Edinburgh for Wardens of the White Council."),
            ("council_warden_epic_hib_longblade", "Silver Longblade of the Council", 50, 445, 19, 10, 162, 36, 26, 3, blade_bonuses, 2, 59322, 0, 0, 0, "Custom-forged from enchanted silver by Senior Council metallurgists. Channels Silver Radiance on strike."),
            ("council_warden_epic_hib_stiletto", "Silver Stiletto of the Council", 50, 456, 21, 10, 162, 29, 20, 3, blade_bonuses, 3, 59322, 0, 0, 0, "Custom-forged from enchanted silver by Senior Council metallurgists. Channels Silver Radiance on strike."),
            # Section 8 Raid Drops
            ("council_warden_raid_loricam_incorruptus", "Loricam Incorruptus", 51, 713, 35, 25, 102, 27, 45, 1, [(1, 18), (3, 18), (5, 18), (10, 60), (18, 12), (17, 8), (19, 8), (163, 4), (108, 4), (155, 4)], 0, 59323, 0, 0, 1, "Inscribed with the original grounding glyphs devised by Merlin in the wake of Camlann to insulate the mortal body against physical violence and supernatural corruption."),
            ("council_warden_raid_severance_slash", "Severance of Camlann", 51, 3, 3, 10, 165, 38, 28, 1, [(1, 18), (2, 15), (4, 15), (102, 4), (108, 4)], 2, 59324, 0, 0, 1, "A masterwork silver-alloy broadsword forged to sever hostile enchantments and cut cleanly through enemy defenses."),
            ("council_warden_raid_severance_thrust", "Severance of Camlann (Estoc)", 51, 22, 4, 10, 165, 33, 24, 1, [(1, 18), (2, 15), (4, 15), (103, 4), (108, 4)], 3, 59324, 0, 0, 1, "A masterwork silver-alloy estoc forged to sever hostile enchantments and cut cleanly through enemy defenses."),
            ("council_warden_raid_mantle_winter_gate", "Mantle of the Winter Gate", 51, 761, 34, 25, 102, 19, 40, 2, [(1, 18), (3, 18), (5, 18), (10, 60), (12, 12), (11, 8), (19, 8), (114, 4), (108, 4), (155, 4)], 0, 59325, 0, 0, 1, "Recovered from an ancient Warden who fell defending the mortal frontier from Unseelie incursions."),
            ("council_warden_raid_rimelance", "Rimelance", 51, 311, 11, 10, 165, 37, 27, 2, [(1, 18), (2, 15), (4, 15), (114, 4), (108, 4)], 2, 59326, 0, 0, 1, "A glacial spatha forged in the deep rime, rupturing enemy footing with Winter's chill."),
            ("council_warden_raid_cuirass_summer_solstice", "Cuirass of the Summer Solstice", 51, 738, 38, 25, 102, 19, 40, 3, [(1, 18), (3, 18), (5, 18), (10, 60), (14, 12), (17, 8), (15, 8), (163, 4), (108, 4), (155, 4)], 0, 59327, 0, 0, 1, "Woven from iron-treated ironwood and living amber, imbued under the Summer Solstice to channel vitality."),
            ("council_warden_raid_sunshard_blades", "Sunshard", 51, 445, 19, 10, 165, 36, 26, 3, [(1, 18), (2, 15), (4, 15), (125, 4), (108, 4)], 2, 59328, 0, 0, 1, "A solar longblade of living amber and silver that blinds enemy attackers on impact."),
            ("council_warden_raid_sunshard_piercing", "Sunshard (Stiletto)", 51, 456, 21, 10, 165, 29, 20, 3, [(1, 18), (2, 15), (4, 15), (127, 4), (108, 4)], 3, 59328, 0, 0, 1, "A solar stiletto of living amber and silver that blinds enemy attackers on impact."),
            # Milestone Foci (Off-Hand Focus items)
            ("council_warden_ch1_focus", "Apprentice's Blasting Rod", 10, 494, 41, 11, 0, 0, 2, 0, [(165, 10), (5, 5), (40, 1)], 0, 0, 0, 0, 0, "An ash-wood blasting rod tipped with silver, focusing early kinetic discharges."),
            ("council_warden_ch2_focus", "Journeyman's Focus Orb", 20, 494, 41, 11, 0, 0, 3, 0, [(165, 20), (5, 10), (40, 2), (173, 3)], 0, 0, 0, 0, 0, "A polished obsidian orb bound in silver wire, balancing martial defense and spellcraft."),
            ("council_warden_ch3_focus", "Adept's Ley-Talisman", 30, 494, 41, 11, 0, 0, 3, 0, [(165, 30), (5, 15), (40, 3), (173, 4)], 0, 0, 0, 0, 0, "Carved with Edinburgh threshold glyphs, drawing power directly from the land's ley-lines."),
            ("council_warden_ch4_focus", "Magus's Blasting Rod", 40, 494, 41, 11, 0, 0, 4, 0, [(165, 40), (5, 18), (40, 4), (173, 5)], 0, 0, 0, 0, 0, "A heavy silver-banded ironwood rod tuned to senior combat evocations."),
            # Chapter 6 Epic Staff & Epic Focus
            ("council_warden_epic_staff", "Staff of the White Council", 50, 48, 12, 12, 165, 40, 30, 0, [(165, 50), (163, 4), (153, 5), (185, 5), (5, 16), (10, 40), (191, 4)], 1, 0, 0, 0, 0, "A polished silver-bound oak staff of the White Council, focusing all magical disciplines."),
            ("council_warden_epic_focus", "The Warden's Blasting Rod", 50, 494, 41, 11, 0, 0, 4, 0, [(165, 50), (163, 4), (40, 4), (173, 5), (155, 5), (10, 60), (5, 16)], 0, 0, 0, 0, 0, "The master blasting rod of a White Council Warden, anchoring defensive kinetic parries and spellcraft."),
            # Section 8 Raid Staves and Foci
            ("council_warden_raid_sidi_staff", "Sigil-Stave of the First Warden", 51, 48, 12, 12, 165, 40, 30, 1, [(165, 50), (163, 4), (153, 6), (185, 6), (5, 18), (2, 15), (10, 50), (191, 5)], 1, 0, 0, 0, 1, "Inscribed with the original Camlann grounding glyphs, focusing senior thaumaturgical rites."),
            ("council_warden_raid_sidi_focus", "Sigil-Talisman of Camlann", 51, 494, 41, 11, 0, 0, 4, 1, [(165, 50), (163, 4), (40, 4), (173, 6), (155, 6), (10, 70), (5, 18), (3, 18)], 0, 59361, 0, 0, 1, "Forged in the depths of Caer Sidi, projecting kinetic deflection shields when deflecting enemy assault."),
            ("council_warden_raid_tuscaran_staff", "Rime-Carved Focus-Staff", 51, 48, 12, 12, 165, 40, 30, 2, [(165, 50), (163, 4), (153, 6), (185, 6), (5, 18), (2, 15), (10, 50), (191, 5)], 1, 0, 0, 0, 1, "Petrified frostwood capped in silver from the Winter border, channeling devastating frost-thaumaturgy."),
            ("council_warden_raid_tuscaran_focus", "Glacial Focus-Orb", 51, 494, 41, 11, 0, 0, 4, 2, [(165, 50), (163, 4), (40, 4), (173, 6), (155, 6), (10, 70), (5, 18), (3, 18)], 0, 59361, 0, 0, 1, "Formed from permafrost and etched runestones deep in Tuscaran Glacier, deflecting kinetic force upon impact."),
            ("council_warden_raid_galladoria_staff", "Amber Branch of the Sun-Warder", 51, 48, 12, 12, 165, 40, 30, 3, [(165, 50), (163, 4), (153, 6), (185, 6), (5, 18), (2, 15), (10, 50), (191, 5)], 1, 0, 0, 0, 1, "Living amber and silver-grafted ironwood of the Summer Court, pulsing with Hibernian ley energy."),
            ("council_warden_raid_galladoria_focus", "Amber Branch of the Summer Court", 51, 494, 41, 11, 0, 0, 4, 3, [(165, 50), (163, 4), (40, 4), (173, 6), (155, 6), (10, 70), (5, 18), (3, 18)], 0, 59361, 0, 0, 1, "Carved from enchanted living amber within Galladoria, storing and releasing kinetic warding pulses."),
            ("council_warden_raid_legion_staff", "Legion's Severing Stave", 51, 48, 12, 12, 165, 40, 30, 0, [(165, 50), (163, 4), (153, 8), (185, 8), (5, 20), (2, 18), (10, 60), (191, 6)], 1, 0, 59360, 10, 1, "Torn from the demonic vortex beneath Darkness Falls, unleashing bolts that sever supernatural life energy."),
            ("council_warden_raid_legion_focus", "Legion's Abyssal Focus-Core", 51, 494, 41, 11, 0, 0, 4, 0, [(165, 50), (163, 4), (40, 4), (173, 6), (155, 6), (10, 70), (5, 18), (3, 18)], 0, 59361, 0, 0, 1, "Salvaged from the demonic core of Legion, projecting impervious kinetic deflection wards."),
        ]

        for id_nb, name, level, model, obj_type, item_type, dps_af, spd_abs, weight, realm, bonuses, type_dmg, proc_id, spell_id, charges, can_drop, desc in phase4_items:
            row = dict(base_item)
            for k in list(row.keys()):
                if k.startswith("Bonus"):
                    row[k] = 0
            row.update({
                "Id_nb": id_nb,
                "Name": name,
                "Level": level,
                "Durability": 50000,
                "MaxDurability": 50000,
                "Condition": 50000,
                "MaxCondition": 50000,
                "Quality": 100,
                "DPS_AF": dps_af,
                "SPD_ABS": spd_abs,
                "Hand": 1 if item_type == 12 else (2 if item_type == 11 else 0),
                "Type_Damage": type_dmg,
                "Object_Type": obj_type,
                "Item_Type": item_type,
                "Color": 0,
                "Emblem": 0,
                "Effect": 0,
                "Weight": weight,
                "Model": model,
                "Extension": 0,
                "Bonus": 35 if level >= 45 else (25 if level >= 30 else 15),
                "IsPickable": 1,
                "IsDropable": 1,
                "CanDropAsLoot": can_drop,
                "IsTradable": 0,
                "Price": 200,
                "MaxCount": 1,
                "PackSize": 1,
                "Charges": charges,
                "MaxCharges": charges,
                "Charges1": 0,
                "MaxCharges1": 0,
                "SpellID": spell_id,
                "SpellID1": 0,
                "ProcSpellID": proc_id,
                "ProcSpellID1": 0,
                "ProcChance": (20 if item_type in (10, 12) else 15) if proc_id > 0 else 0,
                "PoisonSpellID": 0,
                "PoisonCharges": 0,
                "PoisonMaxCharges": 0,
                "Realm": realm,
                "AllowedClasses": "60;61;58",
                "CanUseEvery": 0,
                "Flags": 0,
                "BonusLevel": 0,
                "LevelRequirement": level,
                "PackageID": "CouncilWardenEpic",
                "Description": desc,
                "ClassType": "",
                "SalvageYieldID": 0,
                "TranslationId": "",
                " ExamineArticle": "",
                "MessageArticle": "",
                "LastTimeRowUpdated": now,
                "ItemTemplate_ID": id_nb,
            })
            for idx, (b_type, b_val) in enumerate(bonuses[:10], start=1):
                row[f"Bonus{idx}"] = b_val
                row[f"Bonus{idx}Type"] = b_type
            conn.execute(
                f"INSERT INTO ItemTemplate ({','.join(item_cols)}) VALUES ({','.join('?' for _ in item_cols)})",
                [row[c] for c in item_cols],
            )

    # 3. Tag all 5 Legion metal weapons with ColdIron package ID and anti-Fae description
    conn.execute("""
        UPDATE ItemTemplate
        SET PackageID = 'ColdIron',
            Description = 'Forged from unrefined meteoritic Cold Iron. Deals +50% bonus damage to Fae entities, shatters their magical wards, and completely halts Fae regeneration.'
        WHERE Id_nb IN ('Legions_Soul_Crusher', 'Legions_Flawless_Soul_Crusher', 'Legions_Soul_Splitter', 'Legions_Flawless_Soul_Splitter')
    """)

    # 4. Seed Raid Boss LootTemplate rows
    loot_cols = [r[1] for r in conn.execute("PRAGMA table_info(LootTemplate)")]
    raid_loot = [
        ("Apocalypse", "council_warden_raid_loricam_incorruptus", 99, 1),
        ("Apocalypse", "council_warden_raid_severance_slash", 99, 1),
        ("Apocalypse", "council_warden_raid_severance_thrust", 99, 1),
        ("Apocalypse", "council_warden_raid_sidi_staff", 99, 1),
        ("Apocalypse", "council_warden_raid_sidi_focus", 99, 1),
        ("Queen Kula", "council_warden_raid_mantle_winter_gate", 99, 1),
        ("Queen Kula", "council_warden_raid_rimelance", 99, 1),
        ("Queen Kula", "council_warden_raid_tuscaran_staff", 99, 1),
        ("Queen Kula", "council_warden_raid_tuscaran_focus", 99, 1),
        ("Olcasgean", "council_warden_raid_cuirass_summer_solstice", 99, 1),
        ("Olcasgean", "council_warden_raid_sunshard_blades", 99, 1),
        ("Olcasgean", "council_warden_raid_sunshard_piercing", 99, 1),
        ("Olcasgean", "council_warden_raid_galladoria_staff", 99, 1),
        ("Olcasgean", "council_warden_raid_galladoria_focus", 99, 1),
        ("Legion", "legions_severing_edge", 99, 1),
        ("Legion", "council_warden_raid_legion_staff", 99, 1),
        ("Legion", "council_warden_raid_legion_focus", 99, 1),
        ("Legion", "Legions_Soul_Crusher", 99, 1),
        ("Legion", "Legions_Flawless_Soul_Crusher", 99, 1),
        ("Legion", "Legions_Soul_Splitter", 99, 1),
        ("Legion", "Legions_Flawless_Soul_Splitter", 99, 1),
    ]
    for boss_name, item_id, chance, count in raid_loot:
        loot_row = {
            "TemplateName": boss_name,
            "ItemTemplateID": item_id,
            "Chance": chance,
            "Count": count,
            "LastTimeRowUpdated": now,
            "LootTemplate_ID": f"CouncilWardenLoot_{boss_name.replace(' ', '_')}_{item_id}",
        }
        conn.execute(
            f"INSERT INTO LootTemplate ({','.join(loot_cols)}) VALUES ({','.join('?' for _ in loot_cols)})",
            [loot_row[c] for c in loot_cols],
        )


def seed_database(db_path: Path) -> Path:
    if not db_path.exists():
        raise SystemExit(f"Target database not found: {db_path}")
    backup_path = db_path.with_name(f"{db_path.stem}-before-council-warden{db_path.suffix}")
    copy_backup(db_path, backup_path)

    conn = sqlite3.connect(db_path)
    conn.execute("PRAGMA foreign_keys = OFF")
    now = "2000-01-01 00:00:00"

    try:
        # 1. Remove any prior Council Warden & Phase 3/4 rows for idempotent re-seeding
        class_placeholders = ",".join("?" for _ in WARDEN_CLASS_IDS)
        spec_placeholders = ",".join("?" for _ in WARDEN_SPEC_KEYS)
        line_placeholders = ",".join("?" for _ in WARDEN_LINE_KEYS)

        conn.execute(f"DELETE FROM ClassXSpecialization WHERE ClassID IN ({class_placeholders})", WARDEN_CLASS_IDS)
        conn.execute(f"DELETE FROM SpecXAbility WHERE Spec IN ({spec_placeholders})", WARDEN_SPEC_KEYS)
        conn.execute(f"DELETE FROM Specialization WHERE KeyName IN ({spec_placeholders})", WARDEN_SPEC_KEYS)
        conn.execute(f"DELETE FROM LineXSpell WHERE LineName IN ({line_placeholders}) OR SpellID = 62090", WARDEN_LINE_KEYS)
        conn.execute(f"DELETE FROM SpellLine WHERE KeyName IN ({line_placeholders})", WARDEN_LINE_KEYS)
        conn.execute("DELETE FROM Spell WHERE SpellID BETWEEN 59200 AND 59399 OR SpellID = 62090 OR Spell_ID LIKE 'CouncilWarden_%'")
        conn.execute("DELETE FROM StyleXSpell WHERE StyleID BETWEEN 5900 AND 5999 OR StyleXSpell_ID LIKE 'CouncilWarden_%'")
        conn.execute(f"DELETE FROM Style WHERE ClassId IN ({class_placeholders})", WARDEN_CLASS_IDS)
        conn.execute("DELETE FROM Faction WHERE ID IN (500, 501)")
        conn.execute("DELETE FROM LinkedFaction WHERE FactionID IN (500, 501) OR LinkedFactionID IN (500, 501)")
        conn.execute("DELETE FROM ZonePoint WHERE ZonePoint_ID LIKE 'EdinburghNevernever_%' OR Id IN (243, 244, 245, 731, 258, 259, 430, 594, 429, 586, 264, 265)")
        conn.execute("DELETE FROM ItemTemplate WHERE Id_nb IN ('silver_thimble_of_the_veil', 'legions_severing_edge') OR Id_nb LIKE 'council_warden_%'")
        conn.execute("DELETE FROM LootTemplate WHERE LootTemplate_ID LIKE 'CouncilWardenLoot_%'")
        for mob_id in MOB_IDS:
            conn.execute("DELETE FROM Mob WHERE Mob_ID = ?", (mob_id,))

        # 2. Seed Specializations
        specs = [
            (CAREER_ALB, "Albion Council Warden Career", 0, "Albion Council Warden class career.", "DOL.GS.LiveCareerSpecialization", now),
            (CAREER_MID, "Midgard Council Warden Career", 0, "Midgard Council Warden class career.", "DOL.GS.LiveCareerSpecialization", now),
            (CAREER_HIB, "Hibernia Council Warden Career", 0, "Hibernia Council Warden class career.", "DOL.GS.LiveCareerSpecialization", now),
            (SPEC_EVOCATION, "Evocation", 301, "Kinetic force, fire, wind, and winter frost evocations of the White Council.", "DOL.GS.LiveSpellHybridSpecialization", now),
            (SPEC_ABJURATION, "Abjuration", 1701, "Kinetic wards, runed blade enchantments, and somatic reinforcement of the White Council.", "DOL.GS.LiveSpellHybridSpecialization", now),
            (SPEC_THAUMATURGY, "Thaumaturgy", 7001, "Sympathetic tethers, probability curses, ritual circles, and ley-line thaumaturgy of the White Council.", "DOL.GS.LiveSpellHybridSpecialization", now),
        ]
        conn.executemany(
            "INSERT INTO Specialization (KeyName,Name,Icon,Description,Implementation,LastTimeRowUpdated) VALUES (?,?,?,?,?,?)",
            specs,
        )

        # 3. Seed ClassXSpecialization for all 3 realms
        class_specs = [
            # Albion (60)
            (CLASS_ALB, CAREER_ALB, -100, now),
            (CLASS_ALB, "CharacterStyleUserCareer", -100, now),
            (CLASS_ALB, "Slash", 0, now),
            (CLASS_ALB, "Thrust", 0, now),
            (CLASS_ALB, "Parry", 5, now),
            (CLASS_ALB, SPEC_EVOCATION, 5, now),
            (CLASS_ALB, SPEC_ABJURATION, 5, now),
            (CLASS_ALB, SPEC_THAUMATURGY, 5, now),
            # Midgard (61)
            (CLASS_MID, CAREER_MID, -100, now),
            (CLASS_MID, "CharacterStyleUserCareer", -100, now),
            (CLASS_MID, "Sword", 0, now),
            (CLASS_MID, "Parry", 5, now),
            (CLASS_MID, SPEC_EVOCATION, 5, now),
            (CLASS_MID, SPEC_ABJURATION, 5, now),
            (CLASS_MID, SPEC_THAUMATURGY, 5, now),
            # Hibernia (58)
            (CLASS_HIB, CAREER_HIB, -100, now),
            (CLASS_HIB, "CharacterStyleUserCareer", -100, now),
            (CLASS_HIB, "Blades", 0, now),
            (CLASS_HIB, "Piercing", 0, now),
            (CLASS_HIB, "Parry", 5, now),
            (CLASS_HIB, SPEC_EVOCATION, 5, now),
            (CLASS_HIB, SPEC_ABJURATION, 5, now),
            (CLASS_HIB, SPEC_THAUMATURGY, 5, now),
        ]
        conn.executemany(
            "INSERT INTO ClassXSpecialization (ClassID,SpecKeyName,LevelAcquired,LastTimeRowUpdated) VALUES (?,?,?,?)",
            class_specs,
        )

        # 4. Seed SpecXAbility (Medium Armor, Edged/Piercing Weapons, Sprint, Evade I-IV; NO Shields, NO Blunt)
        spec_abilities = [
            # Albion Career (Class 60)
            (CAREER_ALB, 1, "Sprint", 0, 0, now),
            (CAREER_ALB, 1, "AlbArmor", 4, 0, now),
            (CAREER_ALB, 1, "Weaponry: Slashing", 0, 0, now),
            (CAREER_ALB, 1, "Weaponry: Thrusting", 0, 0, now),
            (CAREER_ALB, 5, "Evade", 1, 0, now),
            (CAREER_ALB, 15, "Evade", 2, 0, now),
            (CAREER_ALB, 25, "Evade", 3, 0, now),
            (CAREER_ALB, 35, "Evade", 4, 0, now),
            # Midgard Career (Class 61)
            (CAREER_MID, 1, "Sprint", 0, 0, now),
            (CAREER_MID, 1, "MidArmor", 3, 0, now),
            (CAREER_MID, 1, "Weaponry: Swords", 0, 0, now),
            (CAREER_MID, 5, "Evade", 1, 0, now),
            (CAREER_MID, 15, "Evade", 2, 0, now),
            (CAREER_MID, 25, "Evade", 3, 0, now),
            (CAREER_MID, 35, "Evade", 4, 0, now),
            # Hibernia Career (Class 58)
            (CAREER_HIB, 1, "Sprint", 0, 0, now),
            (CAREER_HIB, 1, "HibArmor", 3, 0, now),
            (CAREER_HIB, 1, "Weaponry: Blades", 0, 0, now),
            (CAREER_HIB, 1, "Weaponry: Piercing", 0, 0, now),
            (CAREER_HIB, 5, "Evade", 1, 0, now),
            (CAREER_HIB, 15, "Evade", 2, 0, now),
            (CAREER_HIB, 25, "Evade", 3, 0, now),
            (CAREER_HIB, 35, "Evade", 4, 0, now),
        ]
        conn.executemany(
            "INSERT INTO SpecXAbility (Spec,SpecLevel,AbilityKey,AbilityLevel,ClassId,LastTimeRowUpdated) VALUES (?,?,?,?,?,?)",
            spec_abilities,
        )

        # 5. Clone Standard Realm Weapon Styles + Seed Custom Warden Swordsmanship Styles
        clone_class_styles(conn, 1, CLASS_ALB, "Slash")
        clone_class_styles(conn, 1, CLASS_ALB, "Thrust")
        clone_class_styles(conn, 21, CLASS_MID, "Sword")
        clone_class_styles(conn, 45, CLASS_HIB, "Blades")
        clone_class_styles(conn, 45, CLASS_HIB, "Piercing")

        # 6. Seed SpellLines (Baseline & Specialization, shared across all 3 Council Warden classes via ClassIDHint=0)
        spell_lines = [
            (LINE_MINOR_EVOCATION, "Minor Evocation", SPEC_EVOCATION, 1, 0, now),
            (LINE_EVOCATION, "Evocation", SPEC_EVOCATION, 0, 0, now),
            (LINE_MINOR_ABJURATION, "Minor Abjuration", SPEC_ABJURATION, 1, 0, now),
            (LINE_ABJURATION, "Abjuration", SPEC_ABJURATION, 0, 0, now),
            (LINE_MINOR_THAUMATURGY, "Minor Thaumaturgy", SPEC_THAUMATURGY, 1, 0, now),
            (LINE_THAUMATURGY, "Thaumaturgy", SPEC_THAUMATURGY, 0, 0, now),
        ]
        conn.executemany(
            "INSERT INTO SpellLine (KeyName,Name,Spec,IsBaseLine,ClassIDHint,LastTimeRowUpdated) VALUES (?,?,?,?,?,?)",
            spell_lines,
        )

        # 7. Seed Spells across the 4 Spell Lines (per Section 3 of warden_class_plan.md)
        # --- 7.1 Evocation Baseline: Minor Evocation ---
        # Line 1: Kinetic Discharge (Range: 1,500 | Cast: 2.5s | Energy=12)
        for spell_id, level, template, damage, power, name in [
            (59200, 1, 471, 9.0, 2, "Kinetic Snap"),
            (59201, 6, 472, 24.0, 4, "Kinetic Jolt"),
            (59202, 12, 473, 46.0, 7, "Kinetic Pulse"),
            (59203, 19, 474, 74.0, 11, "Kinetic Bolt"),
            (59204, 27, 475, 108.0, 16, "Kinetic Blast"),
            (59205, 36, 476, 146.0, 22, "Kinetic Surge"),
            (59206, 45, 476, 188.0, 28, "Kinetic Rupture"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Projects a raw bolt of telekinetic force at the target.",
                LINE_MINOR_EVOCATION, level,
                Type="DirectDamage", Target="Enemy", Range=1500, CastTime=2.5,
                Damage=damage, DamageType=12, Power=power, SpellGroup=59200,
            )

        # Line 2: Thermal Spark (Range: 1,500 | Cast: 2.5s | Heat=13 | Upfront DD + 6s Burn every 2s)
        for spell_id, level, template, damage, burn, power, name in [
            (59207, 3, 301, 12.0, 4.0, 3, "Ember Flicker"),
            (59208, 10, 302, 32.0, 9.0, 6, "Searing Spark"),
            (59209, 18, 303, 60.0, 16.0, 11, "Cinder Flare"),
            (59210, 28, 304, 96.0, 25.0, 17, "Scorching Ignition"),
            (59211, 38, 305, 136.0, 35.0, 24, "Blazing Flash"),
            (59212, 48, 306, 180.0, 46.0, 31, "White-Hot Spark"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                f"Snaps a spark of concentrated heat for {int(damage)} damage and ignites the target for {int(burn)} Heat damage every 2 seconds for 6 seconds.",
                LINE_MINOR_EVOCATION, level,
                Type="CouncilWardenThermalSpark", Target="Enemy", Range=1500, CastTime=2.5,
                Damage=damage, Value=burn, Duration=6, Frequency=2000, DamageType=13, Power=power,
                SpellGroup=59207,
            )

        # Line 3: Aura of Force (Self | Duration: 20m | Cast: 3.0s | Matter=14 DamageShield)
        for spell_id, level, template, damage, power, name in [
            (59213, 4, 51, 1.5, 3, "Faint Kinetic Field"),
            (59214, 14, 52, 2.6, 7, "Bracing Kinetic Field"),
            (59215, 24, 53, 3.8, 12, "Repulsive Field"),
            (59216, 34, 54, 5.1, 18, "Crushing Field"),
            (59217, 44, 54, 6.5, 25, "Warden's Force Shield"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Surrounds the Warden in a reactive kinetic field that inflicts Matter damage on melee attackers.",
                LINE_MINOR_EVOCATION, level,
                Type="DamageShield", Target="Self", Range=0, CastTime=3.0,
                Duration=1200, Damage=damage, DamageType=14, Power=power,
                EffectGroup=5, SpellGroup=59213,
            )

        # --- 7.2 Evocation Specialization: Evocation ---
        # Line 1: Kinetic Lance / Forzare (Range: 1,500 | Cast: 2.4s | Energy=12 | Shatters Barriers for +25% bonus damage)
        for spell_id, level, template, damage, power, name in [
            (59218, 2, 471, 15.0, 3, "Focused Thrust"),
            (59219, 8, 472, 38.0, 6, "Piercing Force"),
            (59220, 15, 473, 68.0, 10, "Lance of Will"),
            (59221, 23, 474, 105.0, 15, "Shattering Lance"),
            (59222, 32, 475, 148.0, 21, "Impaling Force"),
            (59223, 41, 476, 192.0, 28, "Sovereign Lance"),
            (59224, 49, 476, 238.0, 35, "Archon's Kinetic Lance"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Launches a piercing lance of kinetic force. Shatters active Ablative and Bladeturn barriers on the target to deal +25% bonus damage.",
                LINE_EVOCATION, level,
                Type="CouncilWardenKineticLance", Target="Enemy", Range=1500, CastTime=2.4,
                Damage=damage, DamageType=12, Power=power, SpellGroup=59218,
            )

        # Line 2: Conflagration / Fuego (Range: 1,500 | Radius: 350 | Cast: 2.8s | Heat=13 | AoE DD + 10s AF Debuff)
        for spell_id, level, template, damage, af_debuff, power, name in [
            (59225, 5, 302, 22.0, 40.0, 5, "Thermal Burst"),
            (59226, 13, 303, 52.0, 40.0, 10, "Combustion Wave"),
            (59227, 22, 304, 92.0, 60.0, 17, "Roaring Pyre"),
            (59228, 31, 305, 136.0, 60.0, 24, "Raging Conflagration"),
            (59229, 40, 306, 184.0, 80.0, 32, "Solar Detonation"),
            (59230, 50, 306, 235.0, 80.0, 40, "Cataclysmic Inferno"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                f"Engulfs the area in an explosive conflagration for {int(damage)} Heat damage and superheats enemy armor, reducing Armor Factor by {int(af_debuff)} for 10 seconds.",
                LINE_EVOCATION, level,
                Type="CouncilWardenConflagration", Target="Enemy", Range=1500, Radius=350, CastTime=2.8,
                Damage=damage, Value=af_debuff, Duration=10, DamageType=13, Power=power,
                SpellGroup=59225,
            )

        # Line 3: Repulsion Wave / Ventas Servitas (PBAoE Radius: 300 | Instant | Recast: 30s | Matter=14 | DD + Interrupt + 3s Stumble)
        for spell_id, level, template, damage, power, name in [
            (59231, 10, 1661, 40.0, 8, "Kinetic Shove"),
            (59232, 20, 1663, 82.0, 15, "Shockwave"),
            (59233, 30, 1665, 128.0, 22, "Concussive Ring"),
            (59234, 40, 1666, 178.0, 30, "Tectonic Repulsion"),
            (59235, 48, 1666, 225.0, 38, "Sovereign Shockwave"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Unleashes an omnidirectional wave of kinetic force, dealing Matter damage, interrupting spellcasting, and stunning nearby enemies for 3 seconds.",
                LINE_EVOCATION, level,
                Type="CouncilWardenRepulsionWave", Target="Enemy", Range=0, Radius=300, CastTime=0.0,
                Damage=damage, Duration=3, DamageType=14, Power=power,
                RecastDelay=30, SharedTimerGroup=59231, SpellGroup=59231,
            )

        # Line 4: Kinetic Infusion (Self | Duration: 20m | Cast: 3.0s | Energy=12 DamageAdd)
        for spell_id, level, template, damage, power, name in [
            (59236, 3, 11, 1.8, 3, "Charged Edge"),
            (59237, 11, 12, 3.4, 7, "Thumming Steel"),
            (59238, 19, 13, 5.2, 12, "Kinetic Edge"),
            (59239, 28, 14, 7.4, 18, "Searing Edge"),
            (59240, 37, 15, 9.8, 24, "Rending Force-Blade"),
            (59241, 46, 15, 12.4, 32, "Warden's Wrath-Blade"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Channels focused kinetic momentum through the Warden's silver-inlaid sword on every melee strike.",
                LINE_EVOCATION, level,
                Type="DamageAdd", Target="Self", Range=0, CastTime=3.0,
                Duration=1200, Damage=damage, DamageType=12, Value=0.0, Power=power,
                EffectGroup=40, SpellGroup=59236,
            )

        # --- 7.3 Abjuration Baseline: Minor Abjuration ---
        # Line 1: Warding Shell (Self | Duration: 20m | Cast: 3.0s | BaseArmorFactorBuff)
        for spell_id, level, template, value, power, name in [
            (59242, 1, 1, 14.0, 2, "Minor Ward"),
            (59243, 7, 2, 26.0, 5, "Lesser Ward"),
            (59244, 14, 3, 40.0, 9, "Warding Shell"),
            (59245, 22, 4, 56.0, 14, "Reinforced Ward"),
            (59246, 31, 5, 74.0, 20, "Greater Warding Shell"),
            (59247, 41, 6, 94.0, 26, "Bastion of Will"),
            (59248, 49, 7, 115.0, 32, "Aegis of the Grey Cloak"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Wove a protective kinetic lattice into the Warden's Grey Cloak, increasing Base Armor Factor.",
                LINE_MINOR_ABJURATION, level,
                Type="BaseArmorFactorBuff", Target="Self", Range=0, CastTime=3.0,
                Duration=1200, Value=value, Power=power, EffectGroup=1, SpellGroup=59242,
            )

        # Line 2: Deflective Aegis (Self | Duration: 15m | Cast: 3.0s | BothAblativeArmor 50% rate)
        for spell_id, level, template, value, power, name in [
            (59249, 5, 8040, 75.0, 5, "Deflective Film"),
            (59250, 15, 8041, 150.0, 11, "Deflective Barrier"),
            (59251, 25, 8041, 225.0, 18, "Deflective Aegis"),
            (59252, 35, 8041, 325.0, 25, "Resilient Aegis"),
            (59253, 45, 8041, 450.0, 34, "Unyielding Aegis"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                f"Creates a shimmering kinetic buffer that absorbs 50% of incoming physical and magical damage up to {int(value)} total points.",
                LINE_MINOR_ABJURATION, level,
                Type="BothAblativeArmor", Target="Self", Range=0, CastTime=3.0,
                Duration=900, Damage=50.0, Value=value, Power=power,
                EffectGroup=59249, SpellGroup=59249,
            )

        # Line 3: Circle of Grounding (Group | Radius: 1,500 | Duration: 20m | Cast: 3.0s | EnergyHeatColdResistBuff)
        for spell_id, level, template, value, power, name in [
            (59254, 12, 1055, 8.0, 10, "Lesser Grounding"),
            (59255, 26, 1056, 16.0, 20, "Circle of Grounding"),
            (59256, 42, 1056, 24.0, 32, "Sovereign Grounding"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                f"Grounds hostile elemental energies around the group, increasing Heat, Cold, and Energy resistances by {int(value)}%.",
                LINE_MINOR_ABJURATION, level,
                Type="EnergyHeatColdResistBuff", Target="Group", Range=1500, CastTime=3.0,
                Duration=1200, Value=value, Power=power, EffectGroup=59254, SpellGroup=59254,
            )

        # Line 4: Opening The Sight (Lv 30 Active Toggle | Self | Instant | 5s Recast)
        clone_spell(
            conn, 1, 59257, "Opening The Sight",
            "Opens the Wizard's Third Eye: grants 1,000-unit true vision to reveal all stealthed enemies and Fae entities, draining 2% Power per pulse and increasing magical damage taken by 10% while active.",
            LINE_MINOR_ABJURATION, 30,
            Type="CouncilWardenTheSight", Target="Self", Range=0, CastTime=0.0,
            Duration=3600, Frequency=5000, Power=5, RecastDelay=5, SpellGroup=59257,
        )

        # --- 7.4 Abjuration Specialization: Abjuration ---
        # Line 1: Bulwark of the Council (Self | Duration: 20m | Cast: 3.0s | SpecAF + STR/CON sub-spell)
        for idx, (spell_id, level, template, spec_af, str_con, power, name) in enumerate([
            (59258, 2, 1701, 12.0, 15.0, 3, "Initiate's Bulwark"),
            (59259, 9, 1704, 24.0, 24.0, 7, "Warden's Bulwark"),
            (59260, 17, 1706, 38.0, 34.0, 12, "Stalwart Bulwark"),
            (59261, 26, 1706, 54.0, 45.0, 18, "Iron-Willed Bulwark"),
            (59262, 36, 1706, 72.0, 58.0, 25, "Bulwark of Edinburgh"),
            (59263, 47, 1706, 92.0, 72.0, 34, "Bulwark of the White Council"),
        ]):
            sub_spell_id = 59280 + idx
            clone_spell(
                conn, 3535, sub_spell_id, f"{name} (Somatic Might)",
                f"Reinforces the Warden's body with somatic force, increasing Strength and Constitution by {int(str_con)}.",
                None, level,
                Type="StrengthConstitutionBuff", Target="Self", Range=0, CastTime=0.0,
                Duration=1200, Value=str_con, Power=0, EffectGroup=204, SpellGroup=59280,
            )
            clone_spell(
                conn, template, spell_id, name,
                f"Hardens the Warden's silvered wards and somatic frame, granting +{int(spec_af)} Spec AF and +{int(str_con)} STR/CON.",
                LINE_ABJURATION, level,
                Type="SpecArmorFactorBuff", Target="Self", Range=0, CastTime=3.0,
                Duration=1200, Value=spec_af, Power=power, SubSpellID=sub_spell_id,
                EffectGroup=2, SpellGroup=59258,
            )

        # Line 2: Spell Dispersion (Self | Instant | Duration: 15s | Recast: 45s | MagicAblativeArmor 75% rate)
        for spell_id, level, template, value, power, name in [
            (59264, 6, 8040, 120.0, 6, "Lesser Dispersion"),
            (59265, 16, 8041, 240.0, 12, "Spell Dispersion"),
            (59266, 28, 8041, 380.0, 20, "Greater Dispersion"),
            (59267, 39, 8041, 540.0, 28, "Superior Dispersion"),
            (59268, 48, 8041, 750.0, 36, "Absolute Dispersion"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                f"Snaps an instant counter-ward into place for 15 seconds, absorbing 75% of incoming magical spell damage up to {int(value)} total points.",
                LINE_ABJURATION, level,
                Type="MagicAblativeArmor", Target="Self", Range=0, CastTime=0.0,
                Duration=15, Damage=75.0, Value=value, Power=power,
                RecastDelay=45, SharedTimerGroup=59264, EffectGroup=59264, SpellGroup=59264,
            )

        # Line 3: Circle of Binding (Range: 1,500 | Radius: 250 | Cast: 2.5s | 99% Root)
        for spell_id, level, template, duration, power, name in [
            (59269, 8, 171, 12, 7, "Lesser Binding Circle"),
            (59270, 20, 172, 18, 14, "Binding Circle"),
            (59271, 33, 172, 24, 22, "Greater Binding Circle"),
            (59272, 45, 172, 30, 32, "Grand Circle of Binding"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                f"Anchors a geometric circle of force that roots enemies in a 250-unit radius in place for {duration} seconds.",
                LINE_ABJURATION, level,
                Type="SpeedDecrease", Target="Enemy", Range=1500, Radius=250, CastTime=2.5,
                Duration=duration, Value=99.0, DamageType=14, Power=power,
                EffectGroup=65, SpellGroup=59269,
            )

        # Line 4: Severance Field (Range: 1,500 | Radius: 350 | Cast: 2.5s | Duration: 15s | Recast: 60s | -35% Cast Speed & +20% Fumble)
        for spell_id, level, template, power, name in [
            (59273, 24, 1663, 20, "Disruption Field"),
            (59274, 38, 1665, 30, "Severance Field"),
            (59275, 50, 1666, 42, "Archon's Severance Field"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Projects a 350-unit anti-magic severance field for 15 seconds, reducing enemy casting speed by 35% and increasing melee and spell fumble chance by 20%.",
                LINE_ABJURATION, level,
                Type="CouncilWardenSeveranceField", Target="Enemy", Range=1500, Radius=350, CastTime=2.5,
                Duration=15, Value=35.0, DamageType=12, Power=power,
                RecastDelay=60, SharedTimerGroup=59273, SpellGroup=59273,
            )

        # --- 7.5 Thaumaturgy Baseline: Minor Thaumaturgy ---
        # Line 1: Sympathetic Needle (Range: 1,500 | Cast: 2.5s | Energy=12 DD)
        for spell_id, level, template, damage, power, name in [
            (59340, 2, 471, 12.0, 3, "Sympathetic Needle"),
            (59341, 7, 472, 32.0, 5, "Sympathetic Dart"),
            (59342, 13, 473, 58.0, 9, "Resonant Needle"),
            (59343, 21, 474, 95.0, 14, "Thaumaturgic Needle"),
            (59344, 30, 475, 138.0, 20, "Spike of Sympathy"),
            (59345, 40, 476, 182.0, 27, "Sovereign Needle"),
            (59346, 50, 476, 226.0, 34, "Archon's Needle"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Focuses sympathetic energy to strike the target for direct Energy damage.",
                LINE_MINOR_THAUMATURGY, level,
                Type="DirectDamage", Target="Enemy", Range=1500, CastTime=2.5,
                Damage=damage, DamageType=12, Power=power, SpellGroup=59340,
            )

        # Line 2: Lesser Probability Hex (Range: 1,500 | Cast: 2.0s | Duration: 15s | Fumble & Speed debuff)
        for spell_id, level, template, value, power, name in [
            (59347, 5, 1081, 8.0, 4, "Lesser Probability Hex"),
            (59348, 15, 1081, 10.0, 8, "Probability Hex"),
            (59349, 25, 1081, 12.0, 14, "Greater Probability Hex"),
            (59350, 35, 1081, 15.0, 20, "Superior Probability Hex"),
            (59351, 45, 1081, 18.0, 28, "Catastrophic Probability Hex"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Binds the target in hostile probability static, causing combat actions to fumble.",
                LINE_MINOR_THAUMATURGY, level,
                Type="CouncilWardenProbabilityHex", Target="Enemy", Range=1500, CastTime=2.0,
                Duration=15, Value=value, DamageType=12, Power=power, SpellGroup=59347,
            )

        # Line 3: Ley-Well Inscription (Self | Cast: 3.0s | Duration: 30s | Recast: 60s | Stationary Mana Channel)
        for spell_id, level, template, value, power, name in [
            (59352, 10, 1055, 15.0, 10, "Inscribed Ley-Well"),
            (59353, 20, 1055, 25.0, 18, "Resonant Ley-Well"),
            (59354, 30, 1055, 35.0, 26, "Potent Ley-Well"),
            (59355, 40, 1055, 50.0, 36, "Greater Ley-Well"),
            (59356, 50, 1055, 65.0, 48, "Sovereign Ley-Well"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Channels the local ley-line, restoring mana every 3 seconds while standing stationary.",
                LINE_MINOR_THAUMATURGY, level,
                Type="CouncilWardenLeyWell", Target="Self", Range=0, CastTime=3.0,
                Duration=30, Frequency=3000, Value=value, Power=power, RecastDelay=60,
                SharedTimerGroup=59352, SpellGroup=59352,
            )

        # --- 7.6 Thaumaturgy Specialization: Thaumaturgy ---
        # Line 1: Vinculum (Target: Enemy | Range: 1,500 | Cast: 2.0s | Duration: 20s | Recast: 30s | 15%..35% Damage Reflection)
        for spell_id, level, template, value, power, name in [
            (59370, 4, 1, 15.0, 6, "Initiate's Vinculum"),
            (59371, 14, 1, 20.0, 12, "Warden's Vinculum"),
            (59372, 24, 1, 25.0, 20, "Resonant Vinculum"),
            (59373, 34, 1, 30.0, 28, "Master's Vinculum"),
            (59374, 44, 1, 35.0, 38, "Sovereign Vinculum"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                f"Wreathes the Warden in a sympathetic lattice that reflects {int(value)}% of all incoming damage back upon the attacker.",
                LINE_THAUMATURGY, level,
                Type="CouncilWardenVinculum", Target="Enemy", Range=1500, CastTime=2.0,
                Duration=20, Value=value, DamageType=12, Power=power, RecastDelay=30,
                SharedTimerGroup=59370, SpellGroup=59370,
            )

        # Line 2: Probability Hex (Target: Enemy | Range: 1,500 | Cast: 2.0s | Duration: 15s | Recast: 45s)
        for spell_id, level, template, value, power, name in [
            (59375, 8, 1081, 12.0, 8, "Jinx of Probability"),
            (59376, 18, 1081, 16.0, 15, "Hex of Probability"),
            (59377, 28, 1081, 20.0, 22, "Curse of Probability"),
            (59378, 38, 1081, 25.0, 30, "Greater Hex of Probability"),
            (59379, 48, 1081, 30.0, 40, "Curse of Ruinous Probability"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Heavily distorts probability around the target, increasing fumble chance and slowing actions.",
                LINE_THAUMATURGY, level,
                Type="CouncilWardenProbabilityHex", Target="Enemy", Range=1500, CastTime=2.0,
                Duration=15, Value=value, DamageType=12, Power=power, RecastDelay=45,
                SharedTimerGroup=59375, SpellGroup=59375,
            )

        # Line 3: Circle of Containment (Target: Ground Area | Range: 1,500 | Radius: 350 | Cast: 3.0s | Duration: 25s | Recast: 60s)
        for spell_id, level, template, damage, power, name in [
            (59380, 6, 171, 25.0, 8, "Circle of Binding Wards"),
            (59381, 16, 171, 60.0, 16, "Circle of Containment"),
            (59382, 26, 171, 105.0, 24, "Greater Containment Circle"),
            (59383, 36, 171, 155.0, 34, "Master's Containment Ward"),
            (59384, 46, 171, 210.0, 44, "Archon's Circle of Containment"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Erects a containment circle dealing Energy damage and slowing enemy movement speed by 50% within a 350-unit radius.",
                LINE_THAUMATURGY, level,
                Type="CouncilWardenCircleOfContainment", Target="Area", Range=1500, Radius=350, CastTime=3.0,
                Duration=25, Damage=damage, Value=50.0, DamageType=12, Power=power, RecastDelay=60,
                SharedTimerGroup=59380, SpellGroup=59380,
            )

        # Line 4: Sympathetic Rupture (Range: 1,500 | Cast: 2.8s | Energy=12 DD Heavy Ritual Strike)
        for spell_id, level, template, damage, power, name in [
            (59385, 3, 471, 20.0, 4, "Sympathetic Tap"),
            (59386, 11, 472, 50.0, 8, "Sympathetic Strike"),
            (59387, 19, 473, 90.0, 14, "Sympathetic Rupture"),
            (59388, 27, 474, 135.0, 21, "Greater Sympathetic Rupture"),
            (59389, 35, 475, 185.0, 29, "Superior Sympathetic Rupture"),
            (59390, 43, 476, 235.0, 38, "Sovereign Sympathetic Rupture"),
            (59391, 50, 476, 280.0, 46, "Archon's Sympathetic Rupture"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                "Channels devastating ritual energy into the sympathetic connection, dealing massive Energy damage.",
                LINE_THAUMATURGY, level,
                Type="DirectDamage", Target="Enemy", Range=1500, CastTime=2.8,
                Damage=damage, DamageType=12, Power=power, SpellGroup=59385,
            )

        # Line 5: Ley-Line Conduit (Target: Self | Instant | Recast: 90s | 25%..50% Max Mana Surge)
        for spell_id, level, template, value, power, name in [
            (59392, 25, 1, 25.0, 10, "Ley-Line Conduit"),
            (59393, 45, 1, 50.0, 20, "Sovereign Ley-Conduit"),
        ]:
            clone_spell(
                conn, template, spell_id, name,
                f"Taps a deep subterranean ley-line conduit, instantly restoring {int(value)}% of max mana.",
                LINE_THAUMATURGY, level,
                Type="CouncilWardenLeyLineConduit", Target="Self", Range=0, CastTime=0.0,
                Value=value, Power=power, RecastDelay=90,
                SharedTimerGroup=59392, SpellGroup=59392,
            )

        # Seed custom Warden Swordsmanship styles & their proc spells (59301..59307)
        seed_warden_custom_styles(conn, now)

        # Seed Phase 3 Edinburgh, Nevernever, Open Way (62090), Factions (500/501), ZonePoints, and ItemTemplates
        seed_edinburgh_and_nevernever(conn, now)

        # Seed Phase 4 Epic Quests, Milestone Rewards, Level 50 Epic Armor + Silver Blades, and Raid Drops
        seed_phase4_epic_quests_and_raid_drops(conn, now)

        # 8. Seed White Council Liaison / Council Warden Trainers in Capital Cities & Starter Towns
        ref_mob = conn.execute(
            "SELECT * FROM Mob WHERE Region = 10 AND ClassType = 'DOL.GS.Trainer.ArmsmanTrainer' LIMIT 1"
        ).fetchone()
        if ref_mob is None:
            raise RuntimeError("Reference ArmsmanTrainer row in Region 10 is missing")
        mob_columns = [row[1] for row in conn.execute("PRAGMA table_info(Mob)")]
        base_trainer = dict(zip(mob_columns, ref_mob))

        trainers = [
            # Albion: Camelot Capital Liaison (Region 10) & Campacorentin Forest Secretive Outpost Trainer (Region 1)
            {
                "Mob_ID": "council_warden_trainer_camelot",
                "ClassType": "DOL.GS.Trainer.AlbionMaulerTrainer",
                "Name": "Warden Alistair",
                "Guild": "White Council Liaison",
                "Region": 10,
                "X": 31280,
                "Y": 31680,
                "Z": 8016,
                "Heading": 2025,
                "Realm": 1,
                "Model": 39,
                "Race": 2005,
                "Gender": 1,
                "EquipmentTemplateID": "ArmsmanTrainer",
            },
            {
                "Mob_ID": "council_warden_trainer_camp_forest",
                "ClassType": "DOL.GS.Trainer.AlbionMaulerTrainer",
                "Name": "Warden Vance",
                "Guild": "Council Warden Trainer",
                "Region": 1,
                "X": 497550,
                "Y": 587700,
                "Z": 2308,
                "Heading": 2048,
                "Realm": 1,
                "Model": 33,
                "Race": 2005,
                "Gender": 1,
                "EquipmentTemplateID": "FighterTrainer",
            },
            # Midgard: Jordheim Capital Liaison (Region 101)
            {
                "Mob_ID": "council_warden_trainer_jordheim",
                "ClassType": "DOL.GS.Trainer.MidgardMaulerTrainer",
                "Name": "Warden Haldor",
                "Guild": "White Council Liaison",
                "Region": 101,
                "X": 29760,
                "Y": 34935,
                "Z": 8005,
                "Heading": 205,
                "Realm": 2,
                "Model": 159,
                "Race": 0,
                "Gender": 1,
                "EquipmentTemplateID": "MidStuddedChainHammer",
            },
            # Hibernia: Tir na Nog Capital Liaison (Region 201)
            {
                "Mob_ID": "council_warden_trainer_tir_na_nog",
                "ClassType": "DOL.GS.Trainer.VampiirTrainer",
                "Name": "Warden Cormac",
                "Guild": "White Council Liaison",
                "Region": 201,
                "X": 31800,
                "Y": 27860,
                "Z": 7767,
                "Heading": 1456,
                "Realm": 3,
                "Model": 302,
                "Race": 10,
                "Gender": 1,
                "EquipmentTemplateID": "HibScaleAlt2",
            },
        ]

        for spec in trainers:
            row_values = dict(base_trainer)
            row_values.update({
                "TranslationId": "",
                "Suffix": "",
                "Size": 50,
                "Level": 50,
                "ItemsListTemplateID": None,
                "NPCTemplateID": -1,
                "IsCloakHoodUp": 1,
                "VisibleWeaponSlots": 34,
                "Flags": 16,  # PEACE
                "AggroLevel": 0,
                "AggroRange": 0,
                "RespawnInterval": 0,
                "Brain": "",
                "PathID": "",
                "RoamingRange": 0,
                "LastTimeRowUpdated": now,
            })
            row_values.update(spec)
            conn.execute(
                f"INSERT INTO Mob ({','.join(mob_columns)}) VALUES ({','.join('?' for _ in mob_columns)})",
                [row_values[col] for col in mob_columns],
            )

        conn.commit()
        check = conn.execute("PRAGMA quick_check").fetchone()[0]
        if check != "ok":
            raise RuntimeError(f"SQLite quick_check failed: {check}")
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()

    return backup_path


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed Council Warden class data into OpenDAoC SQLite DB.")
    parser.add_argument("--db", type=Path, default=DEFAULT_DB_PATH, help="Path to target opendaoc.sqlite3.db")
    args = parser.parse_args()
    backup_path = seed_database(args.db)
    print(f"Seeded Council Warden class data into: {args.db}")
    print(f"Backup: {backup_path}")


if __name__ == "__main__":
    main()
