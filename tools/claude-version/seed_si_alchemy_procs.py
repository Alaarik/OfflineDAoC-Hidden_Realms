"""Idempotent database seeder for Shrouded Isles & Classic Calibrated Alchemy Procs & Overcharge Draught.

Adds:
1. Reagent: Draught of Greater Stability (reagent_stability_draught) & Recipe 36000 (Alchemy).
2. 7 Classic + Shrouded Isles calibrated raid-substitute procs:
   Weapons (Volatile - On-Hit):
   - 86001: Adrenaline Strike (EnduranceHeal, 25.0, Self)
   - 86002: Sundering Impact (StrengthConstitutionDebuff, 50.0, 45s, Enemy, Range 1500)
   - 86003: Glacial Rime (SpeedDecrease, 35.0, 8s, Enemy, Range 1500)
   Armor (Reactive - On-Struck):
   - 86004: Verdant Mending (Heal, 140.0, Self)
   - 86005: Aegis Ward (AblativeArmor, 180.0, 600s, Self)
   - 86006: Kinetic Dampening (CombatSpeedDebuff, 20.0, 10s, Enemy, Range 1500)
   - 86007: Adrenaline Rush (EnduranceHeal, 20.0, Self)
3. LineXSpell mappings under 'Item Effects'.
4. Tincture Items in ItemTemplate (Object_Type = 47) with ProcChance = 15%.
5. Alchemy crafting recipes in CraftedItem (IDs 36000, 36011-36017) and ingredients in CraftedXItem.
"""
from __future__ import annotations

import argparse
import sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DB_PATH = ROOT / "playable-v0.35b" / "runtime" / "data" / "opendaoc.sqlite3.db"

SKILL_ALCHEMY = 4
OBJ_ALCHEMY_TINCTURE = 47
OBJ_SPELLCRAFT_GEM = 48


def seed_database(db_path: Path) -> None:
    conn = sqlite3.connect(db_path)
    cur = conn.cursor()

    print(f"[Seed] Seeding Classic & SI Alchemy Procs at {db_path}...")

    # 1. Clean existing records for idempotency (including old temporary clicky records)
    cur.execute("DELETE FROM ItemTemplate WHERE PackageID IN ('OverchargeDresdenExpansion', 'AlchemySIProcsExpansion')")
    cur.execute("DELETE FROM CraftedItem WHERE cast(CraftedItemID as integer) >= 36000 AND cast(CraftedItemID as integer) <= 36020")
    cur.execute("DELETE FROM CraftedXItem WHERE CraftedXItem_ID LIKE 'cxi_dresden_%' OR CraftedXItem_ID LIKE 'cxi_siprocs_%'")
    cur.execute("DELETE FROM Spell WHERE PackageID IN ('OverchargeDresdenExpansion', 'AlchemySIProcsExpansion')")
    cur.execute("DELETE FROM LineXSpell WHERE LineXSpell_ID LIKE 'lxs_dresden_%' OR LineXSpell_ID LIKE 'lxs_siprocs_%'")

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
            'AlchemySIProcsExpansion'
        )
    """, (OBJ_SPELLCRAFT_GEM,))

    # Recipe for Draught of Greater Stability
    cur.execute("""
        INSERT OR REPLACE INTO CraftedItem (
            CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID
        ) VALUES ('36000', 'reagent_stability_draught', 850, ?, 0, 'ci_36000')
    """, (SKILL_ALCHEMY,))

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
        """, (mat_id, count, f"cxi_siprocs_36000_{mat_id}"))

    # -------------------------------------------------------------
    # 3. SEED 7 CLASSIC + SI CALIBRATED PROCS
    # -------------------------------------------------------------
    # (SpellID, ClientEffect, Icon, Name, Description, Target, Range, Value, Duration, Type, DamageType)
    proc_spells = [
        # Weapons (Volatile)
        (86001, 8064, 8064, "Adrenaline Strike", "Replenishes 25 endurance to the user upon a successful weapon strike.", "Self", 0, 25.0, 0, "EnduranceHeal", 0),
        (86002, 966, 966, "Sundering Impact", "Reduces the target's Strength and Constitution by 50 for 45 seconds.", "Enemy", 1500, 50.0, 45, "StrengthConstitutionDebuff", 10),
        (86003, 10082, 10082, "Glacial Rime", "Encases the target's feet in frost, slowing movement by 35% for 8 seconds.", "Enemy", 1500, 35.0, 8, "SpeedDecrease", 11),

        # Armor (Reactive)
        (86004, 8052, 8051, "Verdant Mending", "Heals the wearer for 140 hit points when struck in combat.", "Self", 0, 140.0, 0, "Heal", 0),
        (86005, 4301, 11253, "Aegis Ward", "Creates an ablative barrier absorbing up to 180 physical damage when struck.", "Self", 0, 180.0, 600, "AblativeArmor", 0),
        (86006, 721, 10301, "Kinetic Dampening", "Dampens the attacker's momentum, slowing their melee attack speed by 20% for 10 seconds.", "Enemy", 1500, 20.0, 10, "CombatSpeedDebuff", 0),
        (86007, 8064, 8064, "Adrenaline Rush", "Restores 20 endurance to the wearer when struck in combat.", "Self", 0, 20.0, 0, "EnduranceHeal", 0),
    ]

    for spell_id, client_eff, icon, name, desc, target, rng, val, dur, s_type, dmg_type in proc_spells:
        cur.execute("""
            INSERT OR REPLACE INTO Spell (
                SpellID, ClientEffect, Icon, Name, Description, Target, Range, Power,
                CastTime, Damage, DamageType, Type, Duration, RecastDelay, SharedTimerGroup,
                Value, PackageID, Spell_ID
            ) VALUES (?, ?, ?, ?, ?, ?, ?, 0, 0.0, 0.0, ?, ?, ?, 0, 0, ?, 'AlchemySIProcsExpansion', ?)
        """, (spell_id, client_eff, icon, name, desc, target, rng, dmg_type, s_type, dur, val, str(spell_id)))

        cur.execute("""
            INSERT OR REPLACE INTO LineXSpell (
                LineName, SpellID, Level, LineXSpell_ID
            ) VALUES ('Item Effects', ?, 50, ?)
        """, (spell_id, f"lxs_siprocs_{spell_id}"))

    # -------------------------------------------------------------
    # 4. SEED ALCHEMY TINCTURE ITEMS
    # -------------------------------------------------------------
    # (Item_ID, DisplayName, SpellID, RecipeID, Ingredients)
    tinctures_meta = [
        # Weapons (Volatile)
        (
            "volatile_adrenaline_tincture", "volatile adrenaline weapon tincture", 86001, "36011",
            [("crystal_flask", 1), ("volatile_catalyst", 4), ("arcanium_metal_bars", 2), ("fiery_distill", 2)]
        ),
        (
            "volatile_sundering_tincture", "volatile sundering weapon tincture", 86002, "36012",
            [("crystal_flask", 1), ("volatile_catalyst", 4), ("arcanium_metal_bars", 2), ("earthen_distill", 2)]
        ),
        (
            "volatile_glacial_tincture", "volatile glacial weapon tincture", 86003, "36013",
            [("crystal_flask", 1), ("volatile_catalyst", 4), ("arcanium_metal_bars", 2), ("aqueous_distill", 2)]
        ),

        # Armor (Reactive)
        (
            "reactive_verdant_tincture", "reactive verdant armor tincture", 86004, "36014",
            [("crystal_flask", 1), ("stable_catalyst", 4), ("arcanium_metal_bars", 2), ("aqueous_distill", 2)]
        ),
        (
            "reactive_aegis_tincture", "reactive aegis armor tincture", 86005, "36015",
            [("crystal_flask", 1), ("stable_catalyst", 4), ("arcanium_metal_bars", 2), ("earthen_distill", 2)]
        ),
        (
            "reactive_dampening_tincture", "reactive dampening armor tincture", 86006, "36016",
            [("crystal_flask", 1), ("stable_catalyst", 4), ("arcanium_metal_bars", 2), ("aerated_distill", 2)]
        ),
        (
            "reactive_adrenaline_tincture", "reactive adrenaline armor tincture", 86007, "36017",
            [("crystal_flask", 1), ("stable_catalyst", 4), ("arcanium_metal_bars", 2), ("fiery_distill", 2)]
        ),
    ]

    for item_id, name, spell_id, recipe_id, mats in tinctures_meta:
        type_damage = 1 if "volatile" in item_id else 0
        cur.execute("""
            INSERT OR REPLACE INTO ItemTemplate (
                Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
                Quality, Object_Type, Item_Type, Type_Damage, Model, Weight, Price, IsTradable,
                IsDropable, IsPickable, ProcSpellID, ProcChance, LevelRequirement, PackageID
            ) VALUES (
                ?, ?, 50, 50000, 50000, 50000, 50000, 99, ?, 0, ?, 524, 5, 250000, 1,
                1, 1, ?, 15, 50, 'AlchemySIProcsExpansion'
            )
        """, (item_id, name, OBJ_ALCHEMY_TINCTURE, type_damage, spell_id))

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
            """, (item_id, mat_id, count, f"cxi_siprocs_{recipe_id}_{mat_id}"))

    conn.commit()
    conn.close()
    print(f"[Seed] Successfully seeded {db_path}!")


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed Classic & SI Alchemy Procs.")
    parser.add_argument("--db", type=Path, default=DEFAULT_DB_PATH, help="Path to opendaoc.sqlite3.db")
    args = parser.parse_args()

    seed_database(args.db)


if __name__ == "__main__":
    main()
