"""Idempotent database seeder for Spellcrafting ToA Jewels and Craftable Jewelry.

Adds:
1. Base craftable jewelry templates across tiers (Rings, Bracers, Necklaces, Belts, Jewels, Foci)
   with Object_Type=41 (Magical) and appropriate Item_Type slots, IsCrafted=1.
2. Crafting recipes in CraftedItem and ingredients in CraftedXItem under:
   - Armorcrafting (Rings, Bracers, Necklaces, Metal Focus)
   - Leathercrafting (Belts)
   - Spellcrafting (Jewels, Arcane Focus)
   - Fletching (Wooden Focus)
3. 13 families of ToA Spellcraft Gems (5 tiers each):
   - Stat Caps (STR, DEX, CON, QUI, INT, PIE, EMP, CHA, Acuity)
   - Resist Caps (Body, Cold, Crush, Energy, Heat, Matter, Slash, Spirit, Thrust)
   - HP Cap
   - Spell Range, Casting Speed, Spell Damage, Spell Duration
   - Melee Damage, Melee Speed (Haste), Style Damage
   - Healing Effectiveness, Buff Effectiveness, Power Pool %
4. Crafting recipes for all ToA gems under Spellcrafting (Skill 13).

Creates a backup of the target SQLite database before applying changes.
"""
from __future__ import annotations

import argparse
import shutil
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DB_PATH = ROOT / "playable-v0.35b" / "runtime" / "data" / "opendaoc.sqlite3.db"

# eProperty enum values
PROP_SPELL_RANGE = 153
PROP_MELEE_SPEED = 155
PROP_MELEE_DAMAGE = 173
PROP_CASTING_SPEED = 191
PROP_BUFF_EFFECTIVENESS = 190
PROP_HEALING_EFFECTIVENESS = 195
PROP_POWER_POOL = 196
PROP_SPELL_DAMAGE = 198
PROP_SPELL_DURATION = 199
PROP_STYLE_DAMAGE = 200

PROP_STR_CAP = 201
PROP_DEX_CAP = 202
PROP_CON_CAP = 203
PROP_QUI_CAP = 204
PROP_INT_CAP = 205
PROP_PIE_CAP = 206
PROP_EMP_CAP = 207
PROP_CHA_CAP = 208
PROP_ACU_CAP = 209
PROP_HP_CAP = 210

PROP_BODY_RES_CAP = 221
PROP_COLD_RES_CAP = 222
PROP_CRUSH_RES_CAP = 223
PROP_ENERGY_RES_CAP = 224
PROP_HEAT_RES_CAP = 225
PROP_MATTER_RES_CAP = 226
PROP_SLASH_RES_CAP = 227
PROP_SPIRIT_RES_CAP = 228
PROP_THRUST_RES_CAP = 229

# eCraftingSkill values
SKILL_ARMORCRAFTING = 2
SKILL_LEATHERCRAFTING = 7
SKILL_TAILORING = 11
SKILL_FLETCHING = 12
SKILL_SPELLCRAFTING = 13

# Inventory slot constants
SLOT_LEFT_HAND = 11
SLOT_JEWELRY = 24
SLOT_NECK = 29
SLOT_WAIST = 32
SLOT_LEFT_BRACER = 33
SLOT_LEFT_RING = 35


def ensure_backup(db_path: Path) -> Path:
    backup_path = db_path.with_suffix(".sqlite3.db.bak_sc")
    if not backup_path.exists():
        shutil.copy2(db_path, backup_path)
        print(f"[Backup] Created backup at {backup_path}")
    return backup_path


def seed_database(db_path: Path) -> None:
    conn = sqlite3.connect(db_path)
    cur = conn.cursor()

    print(f"[Seed] Seeding database at {db_path}...")

    # -------------------------------------------------------------
    # 1. BASE CRAFTABLE JEWELRY TEMPLATES
    # -------------------------------------------------------------
    # Metal tiers for Rings, Bracers, Necklaces (Armorcrafting)
    metal_tiers = [
        ("bronze", "Bronze", 10, "bronze_metal_bars", 2),
        ("iron", "Iron", 20, "iron_metal_bars", 3),
        ("steel", "Steel", 30, "steel_metal_bars", 4),
        ("alloy", "Alloy", 40, "alloy_metal_bars", 5),
        ("fine_alloy", "Fine Alloy", 45, "alloy_to_fine_alloy1", 6),
        ("mithril", "Mithril", 48, "mithril_metal_bars", 7),
        ("adamantium", "Adamantium", 50, "adamantium_metal_bars", 8),
        ("arcanium", "Arcanium", 51, "arcanium_metal_bars", 10),
    ]

    recipe_id_counter = 35000

    # Clean existing seeded items to ensure idempotency
    cur.execute("DELETE FROM ItemTemplate WHERE PackageID = 'SpellcraftJewelryExpansion'")
    cur.execute("DELETE FROM CraftedItem WHERE cast(CraftedItemID as integer) >= 35000")
    cur.execute("DELETE FROM CraftedXItem WHERE CraftedXItem_ID LIKE 'cxi_scj_%'")

    jewelry_items_count = 0
    recipes_count = 0

    # RINGS (Slot 35, Model 103)
    for key, name, lvl, bar_id, bar_count in metal_tiers:
        item_id = f"crafted_ring_{key}"
        cur.execute("""
            INSERT OR REPLACE INTO ItemTemplate (
                Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
                Quality, Object_Type, Item_Type, Model, Weight, Price, IsTradable,
                IsDropable, IsPickable, PackageID
            ) VALUES (?, ?, ?, 50000, 50000, 50000, 50000, 99, 41, ?, 103, 5, ?, 1, 1, 1, 'SpellcraftJewelryExpansion')
        """, (item_id, f"{name} Ring", lvl, SLOT_LEFT_RING, lvl * 5000))
        jewelry_items_count += 1

        # Recipe in CraftedItem
        recipe_id = str(recipe_id_counter)
        recipe_id_counter += 1
        cur.execute("""
            INSERT OR REPLACE INTO CraftedItem (
                CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID
            ) VALUES (?, ?, ?, ?, 0, ?)
        """, (recipe_id, item_id, lvl * 18, SKILL_ARMORCRAFTING, f"ci_{recipe_id}"))

        # Ingredient in CraftedXItem
        cxi_id = f"cxi_scj_{recipe_id}_{bar_id}"
        cur.execute("""
            INSERT OR REPLACE INTO CraftedXItem (
                CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID
            ) VALUES (?, ?, ?, ?)
        """, (item_id, bar_id, bar_count, cxi_id))
        recipes_count += 1

    # BRACERS (Slot 33, Model 598)
    for key, name, lvl, bar_id, bar_count in metal_tiers:
        item_id = f"crafted_bracer_{key}"
        cur.execute("""
            INSERT OR REPLACE INTO ItemTemplate (
                Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
                Quality, Object_Type, Item_Type, Model, Weight, Price, IsTradable,
                IsDropable, IsPickable, PackageID
            ) VALUES (?, ?, ?, 50000, 50000, 50000, 50000, 99, 41, ?, 598, 8, ?, 1, 1, 1, 'SpellcraftJewelryExpansion')
        """, (item_id, f"{name} Bracer", lvl, SLOT_LEFT_BRACER, lvl * 6000))
        jewelry_items_count += 1

        recipe_id = str(recipe_id_counter)
        recipe_id_counter += 1
        cur.execute("""
            INSERT OR REPLACE INTO CraftedItem (
                CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID
            ) VALUES (?, ?, ?, ?, 0, ?)
        """, (recipe_id, item_id, lvl * 18, SKILL_ARMORCRAFTING, f"ci_{recipe_id}"))

        cxi_id = f"cxi_scj_{recipe_id}_{bar_id}"
        cur.execute("""
            INSERT OR REPLACE INTO CraftedXItem (
                CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID
            ) VALUES (?, ?, ?, ?)
        """, (item_id, bar_id, bar_count + 1, cxi_id))
        recipes_count += 1

    # NECKLACES (Slot 29, Model 101)
    for key, name, lvl, bar_id, bar_count in metal_tiers:
        item_id = f"crafted_necklace_{key}"
        cur.execute("""
            INSERT OR REPLACE INTO ItemTemplate (
                Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
                Quality, Object_Type, Item_Type, Model, Weight, Price, IsTradable,
                IsDropable, IsPickable, PackageID
            ) VALUES (?, ?, ?, 50000, 50000, 50000, 50000, 99, 41, ?, 101, 6, ?, 1, 1, 1, 'SpellcraftJewelryExpansion')
        """, (item_id, f"{name} Necklace", lvl, SLOT_NECK, lvl * 7000))
        jewelry_items_count += 1

        recipe_id = str(recipe_id_counter)
        recipe_id_counter += 1
        cur.execute("""
            INSERT OR REPLACE INTO CraftedItem (
                CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID
            ) VALUES (?, ?, ?, ?, 0, ?)
        """, (recipe_id, item_id, lvl * 18, SKILL_ARMORCRAFTING, f"ci_{recipe_id}"))

        cxi_id = f"cxi_scj_{recipe_id}_{bar_id}"
        cur.execute("""
            INSERT OR REPLACE INTO CraftedXItem (
                CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID
            ) VALUES (?, ?, ?, ?)
        """, (item_id, bar_id, bar_count, cxi_id))
        recipes_count += 1

    # BELTS (Slot 32, Model 597, Leathercrafting)
    leather_tiers = [
        ("rawhide", "Rawhide", 10, "rawhide_leather_square", 3),
        ("tanned", "Tanned", 20, "tanned_leather_square", 4),
        ("cured", "Cured", 30, "cured_leather_square", 5),
        ("hard", "Hard", 40, "cured_to_hard1", 6),
        ("embossed", "Embossed", 45, "embossed_leather_square", 7),
        ("imbued", "Imbued", 48, "imbued_leather_square", 8),
        ("eldritch", "Eldritch", 50, "eldritch_leather_square", 9),
        ("arcanium", "Tempered", 51, "tempered_leather_square", 10),
    ]
    for key, name, lvl, l_id, l_count in leather_tiers:
        item_id = f"crafted_belt_{key}"
        cur.execute("""
            INSERT OR REPLACE INTO ItemTemplate (
                Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
                Quality, Object_Type, Item_Type, Model, Weight, Price, IsTradable,
                IsDropable, IsPickable, PackageID
            ) VALUES (?, ?, ?, 50000, 50000, 50000, 50000, 99, 41, ?, 597, 10, ?, 1, 1, 1, 'SpellcraftJewelryExpansion')
        """, (item_id, f"{name} Belt", lvl, SLOT_WAIST, lvl * 5500))
        jewelry_items_count += 1

        recipe_id = str(recipe_id_counter)
        recipe_id_counter += 1
        cur.execute("""
            INSERT OR REPLACE INTO CraftedItem (
                CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID
            ) VALUES (?, ?, ?, ?, 0, ?)
        """, (recipe_id, item_id, lvl * 18, SKILL_LEATHERCRAFTING, f"ci_{recipe_id}"))

        cxi_id = f"cxi_scj_{recipe_id}_{l_id}"
        cur.execute("""
            INSERT OR REPLACE INTO CraftedXItem (
                CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID
            ) VALUES (?, ?, ?, ?)
        """, (item_id, l_id, l_count, cxi_id))
        recipes_count += 1

    # WEARABLE JEWELS / TALISMANS (Slot 24, Model 118, Spellcrafting)
    jewel_tiers = [
        ("rough", "Rough Arcane Talisman", 10, "on_gem", 2),
        ("flawed", "Flawed Arcane Talisman", 25, "ee_gem", 2),
        ("polished", "Polished Arcane Talisman", 40, "mon_gem", 3),
        ("precious", "Precious Arcane Talisman", 48, "zo_gem", 3),
        ("flawless", "Flawless Arcane Talisman", 50, "kath_gem", 4),
        ("perfect", "Perfect Arcane Talisman", 51, "ra_gem", 4),
    ]
    for key, name, lvl, gem_id, gem_count in jewel_tiers:
        item_id = f"crafted_jewel_{key}"
        cur.execute("""
            INSERT OR REPLACE INTO ItemTemplate (
                Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
                Quality, Object_Type, Item_Type, Model, Weight, Price, IsTradable,
                IsDropable, IsPickable, PackageID
            ) VALUES (?, ?, ?, 50000, 50000, 50000, 50000, 99, 41, ?, 118, 4, ?, 1, 1, 1, 'SpellcraftJewelryExpansion')
        """, (item_id, name, lvl, SLOT_JEWELRY, lvl * 8000))
        jewelry_items_count += 1

        recipe_id = str(recipe_id_counter)
        recipe_id_counter += 1
        cur.execute("""
            INSERT OR REPLACE INTO CraftedItem (
                CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID
            ) VALUES (?, ?, ?, ?, 0, ?)
        """, (recipe_id, item_id, lvl * 18, SKILL_SPELLCRAFTING, f"ci_{recipe_id}"))

        cxi_id = f"cxi_scj_{recipe_id}_{gem_id}"
        cur.execute("""
            INSERT OR REPLACE INTO CraftedXItem (
                CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID
            ) VALUES (?, ?, ?, ?)
        """, (item_id, gem_id, gem_count, cxi_id))
        recipes_count += 1

    # OFF-HAND FOCI (Slot 11)
    # 1. Arcane Focus Orb (Spellcrafting)
    cur.execute("""
        INSERT OR REPLACE INTO ItemTemplate (
            Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
            Quality, Object_Type, Item_Type, Model, Weight, Price, IsTradable,
            IsDropable, IsPickable, PackageID
        ) VALUES ('crafted_focus_arcane_orb', 'Arcane Focus Orb', 51, 50000, 50000, 50000, 50000, 99, 41, ?, 595, 20, 500000, 1, 1, 1, 'SpellcraftJewelryExpansion')
    """, (SLOT_LEFT_HAND,))
    jewelry_items_count += 1
    recipe_id = str(recipe_id_counter)
    recipe_id_counter += 1
    cur.execute("""
        INSERT OR REPLACE INTO CraftedItem (CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID)
        VALUES (?, 'crafted_focus_arcane_orb', 1000, ?, 0, ?)
    """, (recipe_id, SKILL_SPELLCRAFTING, f"ci_{recipe_id}"))
    cur.execute("""
        INSERT OR REPLACE INTO CraftedXItem (CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID)
        VALUES ('crafted_focus_arcane_orb', 'ra_gem', 4, ?)
    """, (f"cxi_scj_{recipe_id}_ra_gem",))
    recipes_count += 1

    # 2. Metallic Focus Reliquary (Armorcrafting)
    cur.execute("""
        INSERT OR REPLACE INTO ItemTemplate (
            Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
            Quality, Object_Type, Item_Type, Model, Weight, Price, IsTradable,
            IsDropable, IsPickable, PackageID
        ) VALUES ('crafted_focus_reliquary', 'Arcanium Warding Reliquary', 51, 50000, 50000, 50000, 50000, 99, 41, ?, 485, 25, 500000, 1, 1, 1, 'SpellcraftJewelryExpansion')
    """, (SLOT_LEFT_HAND,))
    jewelry_items_count += 1
    recipe_id = str(recipe_id_counter)
    recipe_id_counter += 1
    cur.execute("""
        INSERT OR REPLACE INTO CraftedItem (CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID)
        VALUES (?, 'crafted_focus_reliquary', 1000, ?, 0, ?)
    """, (recipe_id, SKILL_ARMORCRAFTING, f"ci_{recipe_id}"))
    cur.execute("""
        INSERT OR REPLACE INTO CraftedXItem (CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID)
        VALUES ('crafted_focus_reliquary', 'arcanium_metal_bars', 12, ?)
    """, (f"cxi_scj_{recipe_id}_arcanium_metal_bars",))
    recipes_count += 1

    # 3. Carved Focus Totem (Fletching)
    cur.execute("""
        INSERT OR REPLACE INTO ItemTemplate (
            Id_nb, Name, Level, Durability, MaxDurability, Condition, MaxCondition,
            Quality, Object_Type, Item_Type, Model, Weight, Price, IsTradable,
            IsDropable, IsPickable, PackageID
        ) VALUES ('crafted_focus_totem', 'Carved Runic Totem', 51, 50000, 50000, 50000, 50000, 99, 41, ?, 381, 15, 500000, 1, 1, 1, 'SpellcraftJewelryExpansion')
    """, (SLOT_LEFT_HAND,))
    jewelry_items_count += 1
    recipe_id = str(recipe_id_counter)
    recipe_id_counter += 1
    cur.execute("""
        INSERT OR REPLACE INTO CraftedItem (CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID)
        VALUES (?, 'crafted_focus_totem', 1000, ?, 0, ?)
    """, (recipe_id, SKILL_FLETCHING, f"ci_{recipe_id}"))
    cur.execute("""
        INSERT OR REPLACE INTO CraftedXItem (CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID)
        VALUES ('crafted_focus_totem', 'runewood_wooden_boards', 10, ?)
    """, (f"cxi_scj_{recipe_id}_runewood_wooden_boards",))
    recipes_count += 1

    print(f"[Jewelry] Added {jewelry_items_count} jewelry base templates and {recipes_count} crafting recipes.")

    # -------------------------------------------------------------
    # 2. TOA SPELLCRAFT GEMS
    # -------------------------------------------------------------
    gem_tiers_meta = [
        (1, "Rough", "pal_gem", 1, 200),
        (2, "Flawed", "mon_gem", 2, 400),
        (3, "Polished", "ros_gem", 3, 600),
        (4, "Flawless", "zo_gem", 4, 800),
        (5, "Perfect", "ra_gem", 5, 1000),
    ]

    # Definitions of all 13 ToA jewel families: (family_key, family_title, property_enum, is_percent, step_multiplier)
    toa_families = [
        # Stat Caps (+1..+5)
        ("str_cap", "Essence of Titanic Might", PROP_STR_CAP, False, 1),
        ("dex_cap", "Essence of Graceful Agility", PROP_DEX_CAP, False, 1),
        ("con_cap", "Essence of Unyielding Vigor", PROP_CON_CAP, False, 1),
        ("qui_cap", "Essence of Fleeting Celerity", PROP_QUI_CAP, False, 1),
        ("int_cap", "Essence of Deep Wisdom", PROP_INT_CAP, False, 1),
        ("pie_cap", "Essence of Devout Sanctity", PROP_PIE_CAP, False, 1),
        ("emp_cap", "Essence of Harmonious Balance", PROP_EMP_CAP, False, 1),
        ("cha_cap", "Essence of Radiant Majesty", PROP_CHA_CAP, False, 1),
        ("acu_cap", "Essence of Arcane Acuity", PROP_ACU_CAP, False, 1),

        # HP Cap (+10..+50)
        ("hp_cap", "Essence of Immortal Vitality", PROP_HP_CAP, False, 10),

        # Resist Caps (+1%..+5%)
        ("body_res_cap", "Warding Jewel of Flesh", PROP_BODY_RES_CAP, True, 1),
        ("cold_res_cap", "Warding Jewel of Glacial Frost", PROP_COLD_RES_CAP, True, 1),
        ("crush_res_cap", "Warding Jewel of Impact", PROP_CRUSH_RES_CAP, True, 1),
        ("energy_res_cap", "Warding Jewel of Storms", PROP_ENERGY_RES_CAP, True, 1),
        ("heat_res_cap", "Warding Jewel of the Inferno", PROP_HEAT_RES_CAP, True, 1),
        ("matter_res_cap", "Warding Jewel of Earthen Density", PROP_MATTER_RES_CAP, True, 1),
        ("slash_res_cap", "Warding Jewel of Deflection", PROP_SLASH_RES_CAP, True, 1),
        ("spirit_res_cap", "Warding Jewel of Aetherial Grace", PROP_SPIRIT_RES_CAP, True, 1),
        ("thrust_res_cap", "Warding Jewel of Piercing Guard", PROP_THRUST_RES_CAP, True, 1),

        # Utility % (+2%..+10%)
        ("spell_range", "Far-Sight Jewel", PROP_SPELL_RANGE, True, 2),
        ("casting_speed", "Chrono Spell Jewel", PROP_CASTING_SPEED, True, 2),
        ("spell_damage", "Arcane Power Jewel", PROP_SPELL_DAMAGE, True, 2),
        ("spell_duration", "Eternal Essence Jewel", PROP_SPELL_DURATION, True, 2),
        ("melee_damage", "Ferocity Battle Jewel", PROP_MELEE_DAMAGE, True, 2),
        ("melee_speed", "Alacrity Battle Jewel", PROP_MELEE_SPEED, True, 2),
        ("style_damage", "Finesse Battle Jewel", PROP_STYLE_DAMAGE, True, 2),
        ("healing_eff", "Mending Essence Jewel", PROP_HEALING_EFFECTIVENESS, True, 2),
        ("buff_eff", "Amplifying Essence Jewel", PROP_BUFF_EFFECTIVENESS, True, 2),
        ("power_pool", "Ley-Line Conduit Jewel", PROP_POWER_POOL, True, 2),
    ]

    toa_gems_count = 0
    toa_recipes_count = 0

    for family_key, family_title, prop_enum, is_percent, multiplier in toa_families:
        for tier, tier_prefix, base_gem_id, gem_count, craft_level in gem_tiers_meta:
            value = tier * multiplier
            suffix = "%" if is_percent else (" HP" if prop_enum == PROP_HP_CAP else " cap")
            if is_percent and prop_enum in (PROP_BODY_RES_CAP, PROP_COLD_RES_CAP, PROP_CRUSH_RES_CAP, PROP_ENERGY_RES_CAP, PROP_HEAT_RES_CAP, PROP_MATTER_RES_CAP, PROP_SLASH_RES_CAP, PROP_SPIRIT_RES_CAP, PROP_THRUST_RES_CAP):
                suffix = "% cap"

            gem_id = f"toa_gem_{family_key}_{tier}"
            gem_name = f"{tier_prefix} {family_title}"
            gem_desc = f"+{value}{suffix}"

            cur.execute("""
                INSERT OR REPLACE INTO ItemTemplate (
                    Id_nb, Name, Description, Level, Durability, MaxDurability, Condition, MaxCondition,
                    Quality, Object_Type, Item_Type, Model, Weight, Price, Bonus1Type, Bonus1,
                    IsTradable, IsDropable, IsPickable, MaxCount, PackSize, PackageID
                ) VALUES (?, ?, ?, ?, 50000, 50000, 50000, 50000, 100, 48, 40, 114, 2, ?, ?, ?, 1, 1, 1, 1, 1, 'SpellcraftJewelryExpansion')
            """, (gem_id, gem_name, gem_desc, tier * 10, tier * 10000, prop_enum, value))
            toa_gems_count += 1

            recipe_id = str(recipe_id_counter)
            recipe_id_counter += 1
            cur.execute("""
                INSERT OR REPLACE INTO CraftedItem (
                    CraftedItemID, Id_nb, CraftingLevel, CraftingSkillType, MakeTemplated, CraftedItem_ID
                ) VALUES (?, ?, ?, ?, 1, ?)
            """, (recipe_id, gem_id, craft_level, SKILL_SPELLCRAFTING, f"ci_{recipe_id}"))

            cxi_id = f"cxi_scj_{recipe_id}_{base_gem_id}"
            cur.execute("""
                INSERT OR REPLACE INTO CraftedXItem (
                    CraftedItemId_nb, IngredientId_nb, Count, CraftedXItem_ID
                ) VALUES (?, ?, ?, ?)
            """, (gem_id, base_gem_id, gem_count, cxi_id))
            toa_recipes_count += 1

    conn.commit()
    conn.close()

    print(f"[ToA Gems] Added {toa_gems_count} ToA gems and {toa_recipes_count} recipes.")
    print("[Complete] All jewelry base items and ToA gems successfully seeded into database.")


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed Spellcrafting ToA Jewels and Craftable Jewelry.")
    parser.add_argument("--db", type=Path, default=DEFAULT_DB_PATH, help="Path to opendaoc.sqlite3.db")
    args = parser.parse_args()

    db_path = args.db.resolve()
    if not db_path.exists():
        print(f"Error: Database file does not exist at {db_path}", file=sys.stderr)
        sys.exit(1)

    ensure_backup(db_path)
    seed_database(db_path)


if __name__ == "__main__":
    main()
