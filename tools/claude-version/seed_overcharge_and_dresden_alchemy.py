"""Idempotent database seeder for Overcharge Stabilization & Dresden Active Alchemy Tinctures.

Seeds:
1. Reagent: Draught of Greater Stability (reagent_stability_draught) & Alchemy Recipe (ID 36000)
2. Spells:
   - 85001: Kinetic Force Blast (DD 250 Crush, Range 1000, 90s CD)
   - 85002: Aegis Shield Barrier (400 Ablative Shield, 120s CD)
   - 85003: Ursine Vigor Surge (35% Power/Endu Heal, 300s CD)
   - 85004: Pyrofuego Force Beam (DD 300 Heat, Range 1500, 90s CD)
3. LineXSpell mapping to 'Item Effects'
4. Dresden Alchemy Tinctures in ItemTemplate (Object_Type = 47):
   - tincture_dresden_kinetic_force (SpellID=85001, CanUseEvery=90)
   - tincture_dresden_shield_aegis (SpellID=85002, CanUseEvery=120)
   - tincture_dresden_ursine_vigor (SpellID=85003, CanUseEvery=300)
   - tincture_dresden_pyrofuego (SpellID=85004, CanUseEvery=90)
5. Alchemy crafting recipes in CraftedItem (IDs 36001-36004) and CraftedXItem
"""
from __future__ import annotations

import argparse
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DB_PATH = ROOT / "playable-v0.35b" / "runtime" / "data" / "opendaoc.sqlite3.db"

SKILL_ALCHEMY = 4
OBJ_ALCHEMY_TINCTURE = 47
OBJ_SPELLCRAFT_GEM = 48


def seed_database(db_path: Path) -> None:
    conn = sqlite3.connect(db_path)
    cur = conn.cursor()

    print(f"[Seed] Seeding Overcharge & Dresden Alchemy at {db_path}...")

    # 1. Clean existing records for idempotency
    cur.execute("DELETE FROM ItemTemplate WHERE PackageID = 'OverchargeDresdenExpansion'")
    cur.execute("DELETE FROM CraftedItem WHERE cast(CraftedItemID as integer) >= 36000 AND cast(CraftedItemID as integer) <= 36010")
    cur.execute("DELETE FROM CraftedXItem WHERE CraftedXItem_ID LIKE 'cxi_dresden_%'")
    cur.execute("DELETE FROM Spell WHERE PackageID = 'OverchargeDresdenExpansion'")
    cur.execute("DELETE FROM LineXSpell WHERE LineXSpell_ID LIKE 'lxs_dresden_%'")

    # -------------------------------------------------------------
    # 2. SEED REAGENT: DRAUGHT OF GREATER STABILITY
    # -------------------------------------------------------------
    cur.execute("""
        INSERT OR REPLACE INTO ItemTemplate (
            Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
            Quality, Object_Type, Item_Type, Model, Weight, Price, IsTradable,
            IsDropable, IsPickable, PackageID
        ) VALUES (
            'reagent_stability_draught', 'Draught of Greater Stability', 50,
            50000, 50000, 50000, 50000, 100, ?, 0, 524, 5, 100000, 1, 1, 1,
            'OverchargeDresdenExpansion'
        )
    """, (OBJ_SPELLCRAFT_GEM,))

    # Recipe for Draught of Greater Stability
    cur.execute("""
        INSERT OR REPLACE INTO CraftedItem (
            CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID
        ) VALUES ('36000', 'reagent_stability_draught', 850, ?, 0, 'ci_36000')
    """, (SKILL_ALCHEMY,))

    # Ingredients: crystal_flask (1), stable_catalyst (2), Ra_gem (1), flask_of_water (4)
    draught_mats = [
        ("crystal_flask", 1),
        ("stable_catalyst", 2),
        ("Ra_gem", 1),
        ("flask_of_water", 4),
    ]
    for mat_id, count in draught_mats:
        cur.execute("""
            INSERT OR REPLACE INTO CraftedXItem (
                CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID
            ) VALUES ('reagent_stability_draught', ?, ?, ?)
        """, (mat_id, count, f"cxi_dresden_36000_{mat_id}"))

    # -------------------------------------------------------------
    # 3. SEED DRESDEN SPELLS
    # -------------------------------------------------------------
    spells = [
        (
            85001, 11118, 11118, "Kinetic Force Blast",
            "Releases stored kinetic energy in a concussive blast, staggering your target.",
            "Enemy", 1000, 0, 0.0, 250.0, 1, "DirectDamage", 0, 90, 1,
        ),
        (
            85002, 4301, 11238, "Aegis Shield Barrier",
            "Projects a shimmering dome of kinetic and elemental deflection that absorbs up to 400 damage.",
            "Self", 0, 0, 0.0, 0.0, 0, "AblativeArmor", 120, 120, 2,
        ),
        (
            85003, 4210, 4210, "Ursine Vigor Surge",
            "Draws upon emergency reserves to instantly revitalize endurance and mana.",
            "Self", 0, 0, 0.0, 0.0, 0, "PowerHeal", 0, 300, 3,
        ),
        (
            85004, 11120, 11120, "Pyrofuego Force Beam",
            "Channels a focused beam of incandescent flame and kinetic force from your staff.",
            "Enemy", 1500, 0, 0.0, 300.0, 10, "DirectDamage", 0, 90, 1,
        ),
    ]

    for spell_id, client_eff, icon, name, desc, target, rng, power, cast_t, dmg, dmg_type, s_type, dur, cd, timer_grp in spells:
        val = 400.0 if spell_id == 85002 else (35.0 if spell_id == 85003 else 0.0)
        cur.execute("""
            INSERT OR REPLACE INTO Spell (
                SpellID, ClientEffect, Icon, Name, Description, Target, Range, Power,
                CastTime, Damage, DamageType, Type, Duration, RecastDelay, SharedTimerGroup,
                Value, PackageID, Spell_ID
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'OverchargeDresdenExpansion', ?)
        """, (
            spell_id, client_eff, icon, name, desc, target, rng, power,
            cast_t, dmg, dmg_type, s_type, dur, cd, timer_grp, val, str(spell_id)
        ))

        # Map into LineXSpell 'Item Effects'
        cur.execute("""
            INSERT OR REPLACE INTO LineXSpell (
                LineName, SpellID, Level, LineXSpell_ID
            ) VALUES ('Item Effects', ?, 50, ?)
        """, (spell_id, f"lxs_dresden_{spell_id}"))

    # -------------------------------------------------------------
    # 4. SEED DRESDEN ALCHEMY TINCTURES
    # -------------------------------------------------------------
    tinctures = [
        ("tincture_dresden_kinetic_force", "Tincture of Kinetic Impact", 85001, 90, "36001", [("crystal_flask", 1), ("stable_catalyst", 4), ("arcanium_metal_bars", 2), ("Ra_gem", 1)]),
        ("tincture_dresden_shield_aegis", "Tincture of the Warded Aegis", 85002, 120, "36002", [("crystal_flask", 1), ("stable_catalyst", 4), ("arcanium_metal_bars", 2), ("aqueous_distill", 2)]),
        ("tincture_dresden_ursine_vigor", "Elixir of Ursine Vigor", 85003, 300, "36003", [("crystal_flask", 1), ("stable_catalyst", 4), ("Ra_gem", 2), ("earthen_distill", 2)]),
        ("tincture_dresden_pyrofuego", "Tincture of Channeling Fire", 85004, 90, "36004", [("crystal_flask", 1), ("stable_catalyst", 4), ("fiery_distill", 4), ("arcanium_metal_bars", 2)]),
    ]

    for item_id, name, spell_id, cd, recipe_id, mats in tinctures:
        cur.execute("""
            INSERT OR REPLACE INTO ItemTemplate (
                Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
                Quality, Object_Type, Item_Type, Model, Weight, Price, IsTradable,
                IsDropable, IsPickable, SpellID, CanUseEvery, LevelRequirement, PackageID
            ) VALUES (
                ?, ?, 50, 50000, 50000, 50000, 50000, 99, ?, 0, 524, 5, 250000, 1,
                1, 1, ?, ?, 50, 'OverchargeDresdenExpansion'
            )
        """, (item_id, name, OBJ_ALCHEMY_TINCTURE, spell_id, cd))

        # Recipe in CraftedItem
        cur.execute("""
            INSERT OR REPLACE INTO CraftedItem (
                CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID
            ) VALUES (?, ?, 950, ?, 0, ?)
        """, (recipe_id, item_id, SKILL_ALCHEMY, f"ci_{recipe_id}"))

        # Ingredients in CraftedXItem
        for mat_id, count in mats:
            cur.execute("""
                INSERT OR REPLACE INTO CraftedXItem (
                    CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID
                ) VALUES (?, ?, ?, ?)
            """, (item_id, mat_id, count, f"cxi_dresden_{recipe_id}_{mat_id}"))

    conn.commit()
    conn.close()
    print(f"[Seed] Successfully seeded {db_path}!")


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed Overcharge & Dresden Alchemy data.")
    parser.add_argument("--db", type=Path, default=DEFAULT_DB_PATH, help="Path to opendaoc.sqlite3.db")
    args = parser.parse_args()

    seed_database(args.db)


if __name__ == "__main__":
    main()
