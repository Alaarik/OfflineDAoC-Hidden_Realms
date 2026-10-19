"""Idempotent seeder for Council Warden Realm Abilities.

Populates ClassXRealmAbility_Atlas for Council Warden classes across all 3 realms:
- Albion: Class 60 (MaulerAlb)
- Midgard: Class 61 (MaulerMid)
- Hibernia: Class 58 (Vampiir)
"""
from __future__ import annotations

import argparse
import datetime
import shutil
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DB_PATH = ROOT / "playable-v0.35b" / "runtime" / "data" / "opendaoc.sqlite3.db"

COUNCIL_WARDEN_CLASSES = [
    (60, "CouncilWardenAlb"),
    (61, "CouncilWardenMid"),
    (58, "CouncilWardenHib"),
]

STANDARD_HYBRID_RAS = [
    # Stats (Passive)
    "AtlasOF_AugStr",
    "AtlasOF_AugCon",
    "AtlasOF_AugDex",
    "AtlasOF_AugQui",
    "AtlasOF_AugAcuity",
    "AtlasOF_Toughness",
    # Combat / Defense (Passive & Active)
    "AtlasOF_AvoidPain",
    "AtlasOF_MasteryOfArms",
    "AtlasOF_MasteryOfPain",
    "AtlasOF_MasteryOfParrying",
    "AtlasOF_HailOfBlows",
    "AtlasOF_DeterminationHybrid",
    "AtlasOF_AvoidanceOfMagic",
    "AtlasOF_EmptyMind",
    # Arcane / Caster (Passive & Active)
    "AtlasOF_MasteryOfMagery",
    "AtlasOF_WildPower",
    "AtlasOF_WildArcana",
    "AtlasOF_MasteryOfTheArcane",
    "AtlasOF_MasteryOfTheArt",
    "AtlasOF_MasteryOfConcentration",
    "AtlasOF_EtherealBond",
    "AtlasOF_MCL",
    "AtlasOF_Serenity",
    "AtlasOF_RagingPower",
    # Elemental / AoE (Active)
    "AtlasOF_RainOfFire",
    "AtlasOF_RainOfIce",
    "AtlasOF_RainOfAnnihilation",
    # Utility / Survival (Passive & Active)
    "AtlasOF_Purge",
    "AtlasOF_IgnorePain",
    "AtlasOF_SecondWind",
    "AtlasOF_Tireless",
    "AtlasOF_LongWind",
    "AtlasOF_FirstAid",
    "AtlasOF_Lifter",
    "AtlasOF_MasteryOfWater",
    "AtlasOF_VeilRecovery",
    # Unique RR5 Capstone
    "AtlasOF_ClosedCircle",
]


def copy_backup(db_path: Path) -> Path:
    timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_path = db_path.parent / f"{db_path.name}.backup_{timestamp}"
    shutil.copy2(db_path, backup_path)
    print(f"[Backup] Created database backup at: {backup_path}")
    return backup_path


def seed_realm_abilities(db_path: Path) -> None:
    print(f"[Seed] Seeding Council Warden Realm Abilities into {db_path}...")
    conn = sqlite3.connect(db_path)
    cur = conn.cursor()

    # Ensure AtlasOF_ClosedCircle and Closed Circle are registered in Ability table
    abilities_to_ensure = [
        (
            "AtlasOF_ClosedCircle",
            "The Closed Circle",
            3038,
            "Violently compresses the local kinetic field, dragging all enemies within 500 radius to the epicenter and stumbling them for 3 seconds, before sealing the area with an anti-magic Closed Circle that suppresses all enemy spellcasting, reduces movement speed by 35%, and pacifies enemy minions for 15 seconds.",
            4260,
            "DOL.GS.RealmAbilities.AtlasOF_ClosedCircle",
        ),
        (
            "Closed Circle",
            "The Closed Circle",
            3039,
            "Violently compresses the local kinetic field, dragging all enemies within 500 radius to the epicenter and stumbling them for 3 seconds, before sealing the area with an anti-magic Closed Circle that suppresses all enemy spellcasting, reduces movement speed by 35%, and pacifies enemy minions for 15 seconds.",
            4260,
            "DOL.GS.RealmAbilities.ClosedCircleAbility",
        ),
    ]

    for key_name, name, internal_id, desc, icon_id, impl in abilities_to_ensure:
        cur.execute("SELECT AbilityID FROM Ability WHERE KeyName = ?", (key_name,))
        row = cur.fetchone()
        if row is None:
            cur.execute("""
                INSERT INTO Ability (
                    KeyName, Name, InternalID, Description, IconID, Implementation, LastTimeRowUpdated
                ) VALUES (?, ?, ?, ?, ?, ?, '2000-01-01 00:00:00')
            """, (key_name, name, internal_id, desc, icon_id, impl))
            print(f"[Ability] Inserted '{key_name}' into Ability table.")
        else:
            cur.execute("""
                UPDATE Ability SET
                    Name = ?, InternalID = ?, Description = ?, IconID = ?, Implementation = ?, LastTimeRowUpdated = '2000-01-01 00:00:00'
                WHERE KeyName = ?
            """, (name, internal_id, desc, icon_id, impl, key_name))
            print(f"[Ability] Updated '{key_name}' in Ability table.")

    class_ids = [c[0] for c in COUNCIL_WARDEN_CLASSES]
    cur.execute(f"DELETE FROM ClassXRealmAbility_Atlas WHERE CharClass IN ({','.join(map(str, class_ids))})")

    inserted = 0
    for class_id, class_prefix in COUNCIL_WARDEN_CLASSES:
        for idx, ability_key in enumerate(STANDARD_HYBRID_RAS, start=1):
            atlas_id = f"{class_prefix}1-{idx // 9}-{idx % 9}"
            cur.execute("""
                INSERT OR REPLACE INTO ClassXRealmAbility_Atlas (
                    CharClass, AbilityKey, LastTimeRowUpdated, ClassXRealmAbility_Atlas_ID
                ) VALUES (?, ?, '2000-01-01 00:00:00', ?)
            """, (class_id, ability_key, atlas_id))
            inserted += 1

    conn.commit()
    conn.close()
    print(f"[Success] Seeded {inserted} Realm Ability associations ({len(STANDARD_HYBRID_RAS)} per realm) for Council Warden.")


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed Council Warden Realm Abilities into OpenDAoC database.")
    parser.add_argument("--db", type=Path, default=DEFAULT_DB_PATH, help="Path to opendaoc.sqlite3.db")
    parser.add_argument("--no-backup", action="store_true", help="Skip creating database backup")
    args = parser.parse_args()

    if not args.db.exists():
        print(f"Error: Database file not found at {args.db}", file=sys.stderr)
        sys.exit(1)

    if not args.no_backup:
        copy_backup(args.db)

    seed_realm_abilities(args.db)


if __name__ == "__main__":
    main()
