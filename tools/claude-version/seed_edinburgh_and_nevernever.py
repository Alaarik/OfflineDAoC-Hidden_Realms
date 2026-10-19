"""Idempotent database seeder for Edinburgh (White Council Sanctuary) and The Nevernever.

Seeds:
1. Travel & Portals:
   - Hadrian's Wall Hillfort (Region 1 at X:635904, Y:336640, Z:4256) Entrance to Edinburgh Catacombs
   - Edinburgh Catacombs (Region 65) Exit to Hadrian's Wall Hillfort
   - Edinburgh Catacombs (Region 65) to/from The Nevernever (Region 197)
   - Portal Stones and Menhirs (WorldObject)
2. Items & Equipment Templates:
   - Fae drop items: Luminescent Fae Dust, Barbed Bramble Thorn, Rime-Covered Fang, Shadow Malk Claw, Flayed Bone of Rawhead, Fetid Bog Eye, Waystone Shard
   - Silver Thimble of the Veil & Legion's Severing Edge (True Cold Iron)
   - NPC Equipment Templates: Warden Standard, Conclave Honor Guard, Senior Council
   - Loot Templates & MobXLootTemplate links for all Fae predators
3. Edinburgh Tier 1: Catacombs & Diplomatic Cloister (Region 65):
   - Sanctuary Core Services: Sister Brigid, Archivist Eamonn, Fergus Ironhand, Artificer Lyra, Recharger Katherine
   - Crafting Stations & Crafting Masters / Merchants (Armor, Weapon, Spellcraft, Alchemy, Tailoring, Fletching)
   - Council Warden Barracks: Callan Boyd & Gregor MacLeod
   - 16 Arcane, Primal & Song Trainers across Albion, Midgard, and Hibernia
   - Allied Treaty Scholars: Torvald, Fionn, Vaelen, Lyndis, Brokkr, Finvarra
   - Threshold Warden Sentries (Murdoch, Isla, Callum, Malcolm, Elspeth, Alistair, Fiona)
4. Edinburgh Tier 2: The Grand Conclave Hall (Region 192):
   - The Senior Council: Aurelius Theophanes (The Merlin), Cassian Valerius (The Blackstaff), Tariq ibn Ziyad al-Hakim (The Gatekeeper), Branwen ferch Llyr, Halvdan Ulfsson, Acharya Devadatta, Master Shen Xuanzong
   - Warden High Command: Lord-Commander Caelan Vance, High Warden Torin Lachlan, Senior Warden Rhona
   - Conclave Honor Guards with silver claymores and grey cloaks
5. The Nevernever Crossroads & Wilderness Ecologies (Region 197):
   - Central Hub Menhir, Return Waystones (Albion, Midgard, Hibernia, Frontier, Summer, Winter)
   - Hub Sentries: Kenneth, Morven, Alana
   - Guiding Way-Wisp Lanterns along the Warded Cobblestone Road
   - Crossroads Hedge-Witch: Griselda the Hedge-Wife
   - Zone 1: The Brambles & Briar Thickets (Bramble Creepers, Thorn Boggarts, Briar Sprites, Bramblewood Lurkers)
   - Zone 2: The Twilight Mist & Shadow Woods (Redcap Cutthroats, Redcap Marauders, Shadow Malks, Twilight Fetches)
   - Zone 3: The Dark Water Margins & Stygian Bogs (Will-o'-the-Wisps, Bog Hags, Rawhead & Bloodybones World Boss)
   - Zone 4: Outer Winter Borderlands (Glacial Frosthounds, Unseelie Sentinels, Rime-Crusted Fetches, Froststalker Wendigo)
   - Zone 5: Outer Summer Borderlands (Seelie Scouts, Briar Thornsteeds, Golden Willow Wisps, Elder Gruff)
6. Summer Court Outpost (Region 196) & Winter Court Outpost (Region 195) Border Guards
"""
from __future__ import annotations

import argparse
import datetime
import shutil
import sqlite3
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DB_PATH = ROOT / "playable-v0.35b" / "runtime" / "data" / "opendaoc.sqlite3.db"
PACKAGE_ID = "EdinburghNeverneverExpansion"


def copy_backup(db_path: Path) -> Path:
    timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_path = db_path.parent / f"{db_path.name}.backup_{timestamp}"
    shutil.copy2(db_path, backup_path)
    print(f"[Backup] Created database backup at: {backup_path}")
    return backup_path


def seed_database(db_path: Path) -> None:
    print(f"[Seed] Seeding Edinburgh and Nevernever mobs, loot, and world into {db_path}...")
    conn = sqlite3.connect(db_path)
    cur = conn.cursor()

    # 1. Clean prior seed records for idempotency
    cur.execute("DELETE FROM Mob WHERE PackageID IN (?, 'EdinburghNevernever')", (PACKAGE_ID,))
    cur.execute("DELETE FROM WorldObject WHERE WorldObject_ID LIKE 'wo_edinburgh_%' OR WorldObject_ID LIKE 'wo_nevernever_%' OR WorldObject_ID = 'wo_hadrian_portal'")
    cur.execute("DELETE FROM ZonePoint WHERE ZonePoint_ID IN ('zp_hadrian_to_edinburgh', 'zp_nevernever_to_edinburgh')")
    cur.execute("DELETE FROM ItemTemplate WHERE PackageID IN (?, 'EdinburghNevernever', 'ColdIron')", (PACKAGE_ID,))
    cur.execute("DELETE FROM NPCEquipment WHERE TemplateID IN ('Warden_Standard_Equip', 'Conclave_Honor_Guard_Equip', 'The_Merlin_Equip', 'The_Blackstaff_Equip', 'The_Gatekeeper_Equip')")
    cur.execute("DELETE FROM LootTemplate WHERE TemplateName LIKE 'loot_nevernever_%' OR TemplateName = 'loot_rawhead_worldboss'")
    cur.execute("DELETE FROM MobXLootTemplate WHERE LootTemplateName LIKE 'loot_nevernever_%' OR LootTemplateName = 'loot_rawhead_worldboss'")

    # -------------------------------------------------------------
    # 2. TRAVEL & PORTAL TRIGGERS
    # -------------------------------------------------------------
    # Update ZonePoint 243 (Exit from Edinburgh Catacombs -> Hadrian's Wall Hillfort)
    cur.execute("""
        UPDATE ZonePoint
        SET TargetRegion = 1,
            TargetX = 635904,
            TargetY = 336640,
            TargetZ = 4260,
            TargetHeading = 180,
            LastTimeRowUpdated = CURRENT_TIMESTAMP
        WHERE Id = 243
    """)

    # Entrance ZonePoint: Hadrian's Wall Hillfort (Region 1) -> Edinburgh Catacombs (Region 65)
    cur.execute("""
        INSERT OR REPLACE INTO ZonePoint (
            Id, TargetX, TargetY, TargetZ, TargetRegion, TargetHeading,
            SourceX, SourceY, SourceZ, SourceRegion, Realm, ClassType,
            LastTimeRowUpdated, ZonePoint_ID
        ) VALUES (
            242, 34707, 47400, 16145, 65, 2048,
            635904, 336640, 4256, 1, 0, 'DOL.GS.ZonePoint',
            CURRENT_TIMESTAMP, 'zp_hadrian_to_edinburgh'
        )
    """)

    # Return ZonePoint: The Nevernever Crossroads (Region 197) -> Edinburgh Catacombs (Region 65)
    cur.execute("""
        INSERT OR REPLACE INTO ZonePoint (
            Id, TargetX, TargetY, TargetZ, TargetRegion, TargetHeading,
            SourceX, SourceY, SourceZ, SourceRegion, Realm, ClassType,
            LastTimeRowUpdated, ZonePoint_ID
        ) VALUES (
            246, 37210, 29150, 16400, 65, 0,
            32000, 32000, 16000, 197, 0, 'DOL.GS.ZonePoint',
            CURRENT_TIMESTAMP, 'zp_nevernever_to_edinburgh'
        )
    """)

    # -------------------------------------------------------------
    # 3. WORLD OBJECTS & STATIC FIXTURES
    # -------------------------------------------------------------
    # Portal Stone at Hadrian's Wall Hillfort
    cur.execute("""
        INSERT OR REPLACE INTO WorldObject (
            ClassType, Name, ExamineArticle, X, Y, Z, Heading, Region, Model,
            Emblem, Realm, RespawnInterval, LastTimeRowUpdated, WorldObject_ID
        ) VALUES (
            'DOL.GS.GameStaticItem', 'Ancient Council Waystone', 'an',
            635904, 336640, 4256, 0, 1, 2603,
            0, 0, 0, CURRENT_TIMESTAMP, 'wo_hadrian_portal'
        )
    """)

    # Crafting Stations in Edinburgh Catacombs (Region 65) and Hub Waystones in Nevernever (Region 197)
    crafting_stations = [
        ("wo_edinburgh_forge", "Council Forge", 478, 65, 34525, 40489, 15935, 0),
        ("wo_edinburgh_alchemy", "Council Alchemy Table", 820, 65, 34785, 42099, 15931, 0),
        ("wo_edinburgh_lathe", "Council Lathe", 481, 65, 33074, 40111, 15935, 0),
        ("wo_edinburgh_nevernever_portal", "The Ways Ingress", 2603, 65, 37210, 29100, 16400, 2048),
        ("wo_nevernever_edinburgh_portal", "Edinburgh Gateway Menhir", 2603, 197, 32000, 32000, 16000, 0),
        ("wo_nevernever_albion_waystone", "Albion Waystone - Cotswold", 2603, 197, 32080, 31920, 16000, 180),
        ("wo_nevernever_midgard_waystone", "Midgard Waystone - Mularn", 2603, 197, 31920, 31920, 16000, 180),
        ("wo_nevernever_hibernia_waystone", "Hibernia Waystone - Mag Mell", 2603, 197, 31920, 32080, 16000, 0),
        ("wo_nevernever_frontier_waystone", "Frontier Waystone - Hadrian's Hillfort", 2603, 197, 32080, 32080, 16000, 0),
        ("wo_nevernever_summer_waystone", "Summer Borderlands Waystone", 2603, 197, 42134, 34920, 16000, 1024),
        ("wo_nevernever_winter_waystone", "Winter Borderlands Waystone", 2603, 197, 26134, 26920, 16000, 3072),
    ]
    for w_id, w_name, w_model, w_reg, w_x, w_y, w_z, w_heading in crafting_stations:
        cur.execute("""
            INSERT OR REPLACE INTO WorldObject (
                ClassType, Name, ExamineArticle, X, Y, Z, Heading, Region, Model,
                Emblem, Realm, RespawnInterval, LastTimeRowUpdated, WorldObject_ID
            ) VALUES (
                'DOL.GS.GameStaticItem', ?, 'a',
                ?, ?, ?, ?, ?, ?,
                0, 0, 0, CURRENT_TIMESTAMP, ?
            )
        """, (w_name, w_x, w_y, w_z, w_heading, w_reg, w_model, w_id))

    # -------------------------------------------------------------
    # 4. CUSTOM ITEM TEMPLATES (NEVERNEVER LOOT & RELICS)
    # -------------------------------------------------------------
    custom_items = [
        ("silver_thimble_of_the_veil", "Silver Thimble of the Veil", 5, 499, 14, 50000, "EdinburghNevernever",
         "A tarnished silver thimble etched with smuggled Way-finding sigils. Allows non-casters to prick the Veil and step into the Nevernever."),
        ("legions_severing_edge", "Legion's Severing Edge", 51, 3, 10, 200, "ColdIron",
         "Forged from unrefined meteoritic Cold Iron. Deals +50% bonus damage to Fae entities, shatters their magical wards, and halts Fae regeneration."),
        ("fae_dust_essence", "Luminescent Fae Dust", 35, 499, 40, 200, PACKAGE_ID,
         "A pouch of shimmering, phosphorescent dust harvested from Nevernever wisps and sprites. Highly prized in alchemy and enchanting."),
        ("fae_bramble_thorn", "Barbed Bramble Thorn", 25, 499, 40, 150, PACKAGE_ID,
         "A razor-sharp thorn harvested from the aggressive briars and boggarts of the Nevernever thickets."),
        ("frosthound_rime_fang", "Rime-Covered Fang", 42, 499, 40, 250, PACKAGE_ID,
         "A freezing, crystalline tooth harvested from an Unseelie frosthound. Remains cold to the touch even beside a blazing hearth."),
        ("malk_shadow_claw", "Shadow Malk Claw", 40, 499, 40, 300, PACKAGE_ID,
         "A curved obsidian-like claw from a predatory Nevernever Malk. Possesses an unnerving ability to pierce magical protections."),
        ("rawhead_bloody_bone", "Flayed Bone of Rawhead", 55, 499, 40, 50000, PACKAGE_ID,
         "An ancient, blood-stained bone harvested from Rawhead & Bloodybones. Radiates primal dread and ancient bog sorcery."),
        ("rawhead_bog_eye", "Fetid Bog Eye of Rawhead", 55, 499, 40, 20000, PACKAGE_ID,
         "A milky, dead eye plucked from the depths of the stygian mire. A gruesome trophy of victory over the ancient nursery nightmare."),
        ("tarnished_silver_waystone_shard", "Waystone Resonance Shard", 50, 499, 40, 10000, PACKAGE_ID,
         "A fragment of silver-infused menhir stone that resonates with the resonance frequency of the Ways."),
    ]
    for i_id, i_name, i_lvl, i_mod, i_slot, i_price, i_pkg, i_desc in custom_items:
        cur.execute("""
            INSERT OR REPLACE INTO ItemTemplate (
                Id_nb, Name, Level, Quality, Durability, MaxDurability, Condition, MaxCondition,
                Model, Object_Type, Item_Type, Realm, Price, IsPickable, IsDropable, IsTradable,
                CanDropAsLoot, PackageID, Description, LastTimeRowUpdated
            ) VALUES (
                ?, ?, ?, 100, 50000, 50000, 50000, 50000,
                ?, 0, ?, 0, ?, 1, 1, 1,
                1, ?, ?, CURRENT_TIMESTAMP
            )
        """, (i_id, i_name, i_lvl, i_mod, i_slot, i_price, i_pkg, i_desc))

    # -------------------------------------------------------------
    # 5. NPC EQUIPMENT TEMPLATES
    # -------------------------------------------------------------
    equip_templates = [
        # Standard Warden Sentry (Grey Cloak, Silver Sword, Plate/Mail)
        ("Warden_Standard_Equip", 10, 3, 0),    # RightHand Silver Sword
        ("Warden_Standard_Equip", 26, 443, 43), # Cloak (Charcoal Grey)
        ("Warden_Standard_Equip", 25, 69, 43),  # Torso
        ("Warden_Standard_Equip", 27, 70, 43),  # Legs
        ("Warden_Standard_Equip", 28, 71, 43),  # Arms
        ("Warden_Standard_Equip", 22, 72, 43),  # Hands
        ("Warden_Standard_Equip", 23, 73, 43),  # Feet
        # Conclave Honor Guard (Silver Claymore TwoHand, Grey Cloak)
        ("Conclave_Honor_Guard_Equip", 12, 25, 0),   # TwoHand Claymore
        ("Conclave_Honor_Guard_Equip", 26, 443, 43), # Cloak
        ("Conclave_Honor_Guard_Equip", 25, 69, 43),  # Torso
        ("Conclave_Honor_Guard_Equip", 27, 70, 43),  # Legs
        ("Conclave_Honor_Guard_Equip", 28, 71, 43),  # Arms
        ("Conclave_Honor_Guard_Equip", 22, 72, 43),  # Hands
        ("Conclave_Honor_Guard_Equip", 23, 73, 43),  # Feet
        # The Merlin (Ancient Staff, Blue Robes)
        ("The_Merlin_Equip", 10, 19, 0),
        ("The_Merlin_Equip", 26, 443, 69),
        ("The_Merlin_Equip", 25, 319, 69),
        # The Blackstaff (Ebony Staff, Black Cloak)
        ("The_Blackstaff_Equip", 10, 19, 0),
        ("The_Blackstaff_Equip", 26, 443, 0),
        ("The_Blackstaff_Equip", 25, 319, 0),
        # The Gatekeeper (Scimitar, Bronze/Desert Cloak)
        ("The_Gatekeeper_Equip", 10, 3, 0),
        ("The_Gatekeeper_Equip", 26, 443, 19),
        ("The_Gatekeeper_Equip", 25, 319, 19),
    ]
    for eq_t, eq_slot, eq_mod, eq_col in equip_templates:
        eq_id = f"eq_{eq_t}_{eq_slot}"
        cur.execute("""
            INSERT OR REPLACE INTO NPCEquipment (
                TemplateID, Slot, Model, Color, Effect, Extension, Emblem,
                LastTimeRowUpdated, NPCEquipment_ID
            ) VALUES (
                ?, ?, ?, ?, 0, 0, 0,
                CURRENT_TIMESTAMP, ?
            )
        """, (eq_t, eq_slot, eq_mod, eq_col, eq_id))

    # -------------------------------------------------------------
    # 6. LOOT TEMPLATES & MOB LOOT ASSOCIATIONS
    # -------------------------------------------------------------
    loot_defs = [
        ("loot_nevernever_dust", "fae_dust_essence", 70, 1),
        ("loot_nevernever_thorn", "fae_bramble_thorn", 75, 1),
        ("loot_nevernever_frosthound", "frosthound_rime_fang", 70, 1),
        ("loot_nevernever_malk", "malk_shadow_claw", 70, 1),
        ("loot_rawhead_worldboss", "rawhead_bloody_bone", 100, 1),
        ("loot_rawhead_worldboss", "rawhead_bog_eye", 100, 1),
        ("loot_rawhead_worldboss", "tarnished_silver_waystone_shard", 50, 1),
    ]
    for lt_name, lt_item, lt_chance, lt_count in loot_defs:
        lt_id = f"lt_{lt_name}_{lt_item}"
        cur.execute("""
            INSERT OR REPLACE INTO LootTemplate (
                TemplateName, ItemTemplateID, Chance, Count, LastTimeRowUpdated, LootTemplate_ID
            ) VALUES (
                ?, ?, ?, ?, CURRENT_TIMESTAMP, ?
            )
        """, (lt_name, lt_item, lt_chance, lt_count, lt_id))

    mob_loot_links = [
        ("Bramble Creeper", "loot_nevernever_thorn"),
        ("Thorn Boggart", "loot_nevernever_thorn"),
        ("Briar Sprite", "loot_nevernever_dust"),
        ("Bramblewood Lurker", "loot_nevernever_thorn"),
        ("Redcap Cutthroat", "loot_nevernever_dust"),
        ("Redcap Marauder", "loot_nevernever_dust"),
        ("Shadow Malk", "loot_nevernever_malk"),
        ("Twilight Fetch", "loot_nevernever_dust"),
        ("Will-o'-the-Wisp", "loot_nevernever_dust"),
        ("Bog Hag of the Sunder", "loot_nevernever_dust"),
        ("Glacial Frosthound", "loot_nevernever_frosthound"),
        ("Unseelie Border Sentinel", "loot_nevernever_dust"),
        ("Rime-Crusted Fetch", "loot_nevernever_dust"),
        ("Froststalker Wendigo", "loot_nevernever_frosthound"),
        ("Seelie Border Scout", "loot_nevernever_dust"),
        ("Briar Thornsteed", "loot_nevernever_thorn"),
        ("Golden Willow Wisp", "loot_nevernever_dust"),
        ("Elder Gruff of the Wildwood", "loot_nevernever_dust"),
        ("Rawhead & Bloodybones", "loot_rawhead_worldboss"),
    ]
    for m_name, m_loot in mob_loot_links:
        mlt_id = f"mlt_{m_name.lower().replace(' ', '_').replace('&', 'and')}_{m_loot}"
        cur.execute("""
            INSERT OR REPLACE INTO MobXLootTemplate (
                MobName, LootTemplateName, DropCount, LastTimeRowUpdated, MobXLootTemplate_ID
            ) VALUES (
                ?, ?, 1, CURRENT_TIMESTAMP, ?
            )
        """, (m_name, m_loot, mlt_id))

    # -------------------------------------------------------------
    # 7. MOB CATALOG DEFINITIONS
    # (Name, Guild, ClassType, Brain, Model, Region, X, Y, Z, Heading, Level, Realm, EquipTemplate, AggroRange, AggroLevel)
    # -------------------------------------------------------------
    all_mobs = []

    # --- Surface Entrance (Region 1 Hadrian's Wall Hillfort) ---
    all_mobs.append(("Warden Alistair MacKinnon", "<White Council Sentry>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 32, 1, 635950, 336680, 4256, 2048, 55, 0, "Warden_Standard_Equip", 650, 100))

    # --- Edinburgh Tier 1: Catacombs & Diplomatic Cloister (Region 65) ---
    tier1_wardens = [
        ("Senior Warden Murdoch", "<Keeper of the Inner Threshold>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 30, 65, 38950, 44220, 16400, 1024, 60, 0, "Warden_Standard_Equip", 650, 100),
        ("Warden Sentry Isla", "<White Council Enforcer>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 31, 65, 34760, 47350, 16145, 2048, 58, 0, "Warden_Standard_Equip", 650, 100),
        ("Warden Sentry Callum", "<Ways Watch>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 32, 65, 37150, 29220, 16400, 0, 58, 0, "Warden_Standard_Equip", 650, 100),
        ("Warden Sentry Malcolm", "<Warden of the White Council>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 30, 65, 32480, 32050, 16465, 0, 55, 0, "Warden_Standard_Equip", 650, 100),
        ("Warden Sentry Elspeth", "<Warden of the White Council>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 31, 65, 32600, 32050, 16465, 0, 55, 0, "Warden_Standard_Equip", 650, 100),
        ("Warden Gate-Guard Alistair", "<Warden of the White Council>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 32, 65, 35000, 32450, 16166, 0, 60, 0, "Warden_Standard_Equip", 650, 100),
        ("Warden Gate-Guard Fiona", "<Warden of the White Council>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 31, 65, 35150, 32450, 16166, 0, 60, 0, "Warden_Standard_Equip", 650, 100),
    ]
    all_mobs.extend(tier1_wardens)

    # Core Sanctuary Services (Rotunda)
    tier1_services = [
        ("Sister Brigid of Iona", "<Sanctuary Healer>", "DOL.GS.GameHealer", "DOL.AI.Brain.BlankBrain", 46, 65, 32820, 35680, 16481, 2048, 50, 0, None, 0, 0),
        ("Archivist Eamonn Vance", "<Council Vault Keeper>", "DOL.GS.GameVaultKeeper", "DOL.AI.Brain.BlankBrain", 41, 65, 33080, 35680, 16481, 2048, 50, 0, None, 0, 0),
        ("Master Fergus Ironhand", "<Sanctuary Blacksmith>", "DOL.GS.Blacksmith", "DOL.AI.Brain.BlankBrain", 185, 65, 32820, 35920, 16481, 0, 50, 0, None, 0, 0),
        ("Artificer Lyra Vance", "<Council Enchanter>", "DOL.GS.Enchanter", "DOL.AI.Brain.BlankBrain", 330, 65, 33080, 35920, 16481, 0, 50, 0, None, 0, 0),
        ("Recharger Katherine", "<Sanctuary Recharger>", "DOL.GS.Recharger", "DOL.AI.Brain.BlankBrain", 330, 65, 32950, 35800, 16481, 0, 50, 0, None, 0, 0),
    ]
    all_mobs.extend(tier1_services)

    # Crafting Masters & Merchants
    tier1_crafting = [
        ("Master Armorer Duncan", "<Master Armorer>", "DOL.GS.ArmorCraftingMaster", "DOL.AI.Brain.BlankBrain", 40, 65, 34430, 40420, 15935, 2048, 50, 0, None, 0, 0),
        ("Master Bladesmith Gavran", "<Master Weaponsmith>", "DOL.GS.WeaponCraftingMaster", "DOL.AI.Brain.BlankBrain", 185, 65, 34620, 40420, 15935, 2048, 50, 0, None, 0, 0),
        ("Magister Aurelia", "<Master Spellcrafter>", "DOL.GS.SpellCraftingMaster", "DOL.AI.Brain.BlankBrain", 55, 65, 34690, 42020, 15931, 2048, 50, 0, None, 0, 0),
        ("Apothecary Severin", "<Master Alchemist>", "DOL.GS.BasicCraftingMaster", "DOL.AI.Brain.BlankBrain", 718, 65, 34880, 42020, 15931, 2048, 50, 0, None, 0, 0),
        ("Master Bowyer Finn", "<Master Fletcher>", "DOL.GS.BasicCraftingMaster", "DOL.AI.Brain.BlankBrain", 330, 65, 32980, 40030, 15935, 2048, 50, 0, None, 0, 0),
        ("Master Weaver Catriona", "<Master Tailor>", "DOL.GS.BasicCraftingMaster", "DOL.AI.Brain.BlankBrain", 46, 65, 33170, 40030, 15935, 2048, 50, 0, None, 0, 0),
        ("Factor Baird", "<Metal, Wood & Leather Merchant>", "DOL.GS.GameMerchant", "DOL.AI.Brain.BlankBrain", 39, 65, 34430, 40560, 15935, 0, 50, 0, None, 0, 0),
        ("Chandler Morag", "<Crafting Tools & Cloth Merchant>", "DOL.GS.GameMerchant", "DOL.AI.Brain.BlankBrain", 44, 65, 34620, 40560, 15935, 0, 50, 0, None, 0, 0),
        ("Herbalist Rowan", "<Alchemy & Poison Supplies>", "DOL.GS.GameMerchant", "DOL.AI.Brain.BlankBrain", 342, 65, 34690, 42180, 15931, 0, 50, 0, None, 0, 0),
        ("Lapidary Caelia", "<Spellcrafting Gems & Powders>", "DOL.GS.GameMerchant", "DOL.AI.Brain.BlankBrain", 334, 65, 34880, 42180, 15931, 0, 50, 0, None, 0, 0),
        ("Dyer Maeve", "<Sanctuary Dye Merchant>", "DOL.GS.GameMerchant", "DOL.AI.Brain.BlankBrain", 48, 65, 34785, 42260, 15931, 0, 50, 0, None, 0, 0),
        ("Mercer Elowen", "<Tailoring Threads & Squares>", "DOL.GS.GameMerchant", "DOL.AI.Brain.BlankBrain", 355, 65, 32980, 40190, 15935, 0, 50, 0, None, 0, 0),
        ("Timberwright Torcall", "<Masterwork Wood & Thread Merchant>", "DOL.GS.GameMerchant", "DOL.AI.Brain.BlankBrain", 192, 65, 33170, 40190, 15935, 0, 50, 0, None, 0, 0),
        ("Fletcher Tamsin", "<Fletching Feathers & Shafts>", "DOL.GS.GameMerchant", "DOL.AI.Brain.BlankBrain", 43, 65, 33074, 40270, 15935, 0, 50, 0, None, 0, 0),
    ]
    all_mobs.extend(tier1_crafting)

    # Barracks & Trainers
    tier1_trainers = [
        ("Quartermaster Gregor MacLeod", "<Warden Armory & Barracks>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 40, 65, 32480, 31950, 16465, 2048, 50, 0, "Warden_Standard_Equip", 0, 0),
        ("Drillmaster Callan Boyd", "<Council Warden Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 35, 65, 32610, 32050, 16465, 2048, 55, 0, "Warden_Standard_Equip", 0, 0),
        # Albion Trainers
        ("Magister Aldous Vance", "<Wizard Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 61, 65, 26020, 32580, 16761, 1024, 50, 1, None, 0, 0),
        ("Preceptor Galen", "<Theurgist Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 63, 65, 26280, 32580, 16761, 3072, 50, 1, None, 0, 0),
        ("Domina Lysandra", "<Sorcerer Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 55, 65, 26020, 32800, 16761, 1024, 50, 1, None, 0, 0),
        ("Savant Malakor", "<Cabalist Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 718, 65, 26280, 32800, 16761, 3072, 50, 1, None, 0, 0),
        ("Troubadour Perrin", "<Minstrel Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 42, 65, 26150, 34560, 16611, 0, 50, 1, None, 0, 0),
        # Midgard Trainers
        ("Runemaster Hrogar", "<Runemaster Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 185, 65, 28280, 33900, 15871, 1024, 50, 2, None, 0, 0),
        ("Seithkona Astrid", "<Spiritmaster Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 158, 65, 28520, 33900, 15871, 3072, 50, 2, None, 0, 0),
        ("Runekeeper Skuld", "<Shaman Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 169, 65, 28520, 34100, 15871, 3072, 50, 2, None, 0, 0),
        # Hibernia Trainers
        ("Ollamh Aelindor", "<Eldritch Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 330, 65, 26020, 37950, 16611, 1024, 50, 3, None, 0, 0),
        ("Illusionist Finvarra", "<Enchanter Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 355, 65, 26280, 37950, 16611, 3072, 50, 3, None, 0, 0),
        ("Mindweaver Maeve", "<Mentalist Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 334, 65, 26020, 38150, 16611, 1024, 50, 3, None, 0, 0),
        ("Grove-Caller Cian", "<Animist Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 382, 65, 26280, 38150, 16611, 3072, 50, 3, None, 0, 0),
        ("Scythe-Weir Lorcan", "<Valewalker Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 382, 65, 27800, 37980, 16761, 1024, 50, 3, None, 0, 0),
        ("Archdruid Cathbad", "<Druid Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 302, 65, 26020, 40690, 16761, 1024, 50, 3, None, 0, 0),
        ("Grove-Warden Niamh", "<Warden Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 307, 65, 26280, 40690, 16761, 3072, 50, 3, None, 0, 0),
        ("Fili Amergin", "<Bard Trainer>", "DOL.GS.EdinburghMagicClassTrainer", "DOL.AI.Brain.BlankBrain", 303, 65, 26151, 40890, 16761, 0, 50, 3, None, 0, 0),
        # Allied Treaty Scholars
        ("Master Brokkr of Svartalfheim", "<Dwarven Rune-Master & Treaty Scholar>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 185, 65, 28402, 33999, 15871, 1024, 55, 0, None, 0, 0),
        ("Emissary Finvarra of the Veil", "<Lurikeen Illusionist & Sidhe Liaison>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 355, 65, 27908, 38051, 16761, 2048, 55, 0, None, 0, 0),
        ("Dwarven Runecarver Torvald", "<Allied Treaty Scholar - Midgard>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 190, 65, 28350, 34200, 15871, 1024, 50, 0, None, 0, 0),
        ("Lurikeen Scribe Fionn", "<Allied Treaty Scholar - Hibernia>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 340, 65, 27850, 38150, 16761, 2048, 50, 0, None, 0, 0),
        ("Inconnu Crypt-Scholar Vaelen", "<Allied Treaty Scholar - Albion>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 46, 65, 26151, 33000, 16761, 1024, 50, 0, None, 0, 0),
        ("Elven Illusionist Lyndis", "<Allied Treaty Scholar - Hibernia>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 335, 65, 26350, 38250, 16611, 2048, 50, 0, None, 0, 0),
    ]
    all_mobs.extend(tier1_trainers)

    # --- Edinburgh Tier 2: Grand Conclave Hall (Region 192) ---
    tier2_council = [
        ("Aurelius Theophanes", "<The Merlin of the White Council>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 51, 192, 32145, 32469, 15653, 3072, 75, 0, "The_Merlin_Equip", 0, 0),
        ("Cassian Valerius", "<The Blackstaff>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 63, 192, 32220, 32360, 15653, 3072, 75, 0, "The_Blackstaff_Equip", 0, 0),
        ("Branwen ferch Llyr", "<Senior Council>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 45, 192, 32070, 32560, 15653, 3072, 70, 0, None, 0, 0),
        ("Halvdan Ulfsson", "<Senior Council>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 137, 192, 32250, 32570, 15653, 3072, 70, 0, None, 0, 0),
        ("Tariq ibn Ziyad al-Hakim", "<The Gatekeeper>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 72, 192, 32330, 32470, 15653, 3072, 75, 0, "The_Gatekeeper_Equip", 0, 0),
        ("Acharya Devadatta", "<Senior Council>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 42, 192, 32080, 32350, 15653, 3072, 70, 0, None, 0, 0),
        ("Master Shen Xuanzong", "<Senior Council>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 61, 192, 32160, 32640, 15653, 3072, 70, 0, None, 0, 0),
        ("Lord-Commander Caelan Vance", "<Commander of the Wardens>", "DOL.GS.EdinburghCouncilNPC", "DOL.AI.Brain.BlankBrain", 55, 192, 32913, 32213, 15653, 1024, 70, 0, "Warden_Standard_Equip", 0, 0),
        ("High Warden Torin Lachlan", "<Second-in-Command of the Wardens>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 32, 192, 32980, 32120, 15653, 1024, 68, 0, "Warden_Standard_Equip", 650, 100),
        ("Senior Warden Rhona", "<Warden of the White Council>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 31, 192, 35140, 32650, 16170, 0, 65, 0, "Warden_Standard_Equip", 650, 100),
        # Conclave Honor Guards
        ("Conclave Warden Guard", "<Warden Honor Guard>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 30, 192, 34950, 32700, 16170, 1024, 65, 0, "Conclave_Honor_Guard_Equip", 650, 100),
        ("Conclave Warden Guard", "<Warden Honor Guard>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 32, 192, 35230, 32700, 16170, 3072, 65, 0, "Conclave_Honor_Guard_Equip", 650, 100),
        ("Conclave Warden Guard", "<Warden Honor Guard>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 30, 192, 35000, 32380, 16175, 2048, 65, 0, "Conclave_Honor_Guard_Equip", 650, 100),
        ("Conclave Warden Guard", "<Warden Honor Guard>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 32, 192, 35180, 32380, 16175, 2048, 65, 0, "Conclave_Honor_Guard_Equip", 650, 100),
    ]
    all_mobs.extend(tier2_council)

    # --- The Nevernever Crossroads & Wilderness (Region 197) ---
    nevernever_mobs = [
        # Hub Portals & Waystones
        ("Edinburgh Gateway Menhir", "<The White Council Sanctuary>", "DOL.GS.EdinburghWaystonePortalNPC", "DOL.AI.Brain.BlankBrain", 1923, 197, 32000, 32000, 16000, 0, 50, 0, None, 0, 0),
        ("Albion Waystone", "<To Cotswold Village>", "DOL.GS.EdinburghWaystonePortalNPC", "DOL.AI.Brain.BlankBrain", 1923, 197, 32080, 31920, 16000, 180, 50, 0, None, 0, 0),
        ("Midgard Waystone", "<To Vale of Mularn>", "DOL.GS.EdinburghWaystonePortalNPC", "DOL.AI.Brain.BlankBrain", 1923, 197, 31920, 31920, 16000, 180, 50, 0, None, 0, 0),
        ("Hibernia Waystone", "<To Mag Mell Village>", "DOL.GS.EdinburghWaystonePortalNPC", "DOL.AI.Brain.BlankBrain", 1923, 197, 31920, 32080, 16000, 0, 50, 0, None, 0, 0),
        ("Frontier Waystone", "<To Hadrian's Wall Hillfort>", "DOL.GS.EdinburghWaystonePortalNPC", "DOL.AI.Brain.BlankBrain", 1923, 197, 32080, 32080, 16000, 0, 50, 0, None, 0, 0),
        ("Summer Borderlands Waystone", "<Seelie Crossing>", "DOL.GS.EdinburghWaystonePortalNPC", "DOL.AI.Brain.BlankBrain", 1923, 197, 42134, 34920, 16000, 1024, 50, 0, None, 0, 0),
        ("Winter Borderlands Waystone", "<Unseelie Crossing>", "DOL.GS.EdinburghWaystonePortalNPC", "DOL.AI.Brain.BlankBrain", 1923, 197, 26134, 26920, 16000, 3072, 50, 0, None, 0, 0),

        # Hub Sentries & Services
        ("Warden Way-Watcher Kenneth", "<Warden of the Ways>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 32, 197, 32040, 32040, 16000, 0, 55, 0, "Warden_Standard_Equip", 650, 100),
        ("Warden Way-Watcher Morven", "<Warden of the Ways>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 31, 197, 31960, 32040, 16000, 0, 55, 0, "Warden_Standard_Equip", 650, 100),
        ("Warden Patrol Officer Alana", "<Warden of the Ways>", "DOL.GS.WardenSentryNPC", "DOL.AI.Brain.WardenSentryBrain", 35, 197, 32000, 32150, 16000, 2048, 56, 0, "Warden_Standard_Equip", 650, 100),
        ("Griselda the Hedge-Wife", "<Curiosities of the Ways>", "DOL.GS.NeverneverHedgeWitchNPC", "DOL.AI.Brain.BlankBrain", 46, 197, 32120, 32120, 16000, 1024, 48, 0, None, 0, 0),

        # Guiding Way-Wisp Lanterns along the Warded Cobblestones
        ("Way-Wisp Lantern", "<Guiding Witchlight>", "DOL.GS.WayWispLanternNPC", "DOL.AI.Brain.BlankBrain", 966, 197, 32000, 32000, 16000, 0, 50, 0, None, 0, 0),
        ("Way-Wisp Lantern", "<Guiding Witchlight>", "DOL.GS.WayWispLanternNPC", "DOL.AI.Brain.BlankBrain", 966, 197, 35000, 33000, 16000, 0, 50, 0, None, 0, 0),
        ("Way-Wisp Lantern", "<Guiding Witchlight>", "DOL.GS.WayWispLanternNPC", "DOL.AI.Brain.BlankBrain", 966, 197, 38000, 34000, 16000, 0, 50, 0, None, 0, 0),
        ("Way-Wisp Lantern", "<Guiding Witchlight>", "DOL.GS.WayWispLanternNPC", "DOL.AI.Brain.BlankBrain", 966, 197, 41500, 34700, 16000, 0, 50, 0, None, 0, 0),
        ("Way-Wisp Lantern", "<Guiding Witchlight>", "DOL.GS.WayWispLanternNPC", "DOL.AI.Brain.BlankBrain", 966, 197, 29500, 29800, 16000, 0, 50, 0, None, 0, 0),
        ("Way-Wisp Lantern", "<Guiding Witchlight>", "DOL.GS.WayWispLanternNPC", "DOL.AI.Brain.BlankBrain", 966, 197, 26500, 27200, 16000, 0, 50, 0, None, 0, 0),

        # Zone 1: The Brambles & Briar Thickets (Levels 18–30, North/Northeast)
        ("Bramble Creeper", "<Wyldfae Plant>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 600, 197, 34500, 39000, 16000, 0, 20, 0, None, 550, 100),
        ("Bramble Creeper", "<Wyldfae Plant>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 600, 197, 36200, 41500, 16000, 0, 22, 0, None, 550, 100),
        ("Thorn Boggart", "<Wyldfae Scavenger>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 602, 197, 35500, 38500, 16000, 0, 24, 0, None, 550, 100),
        ("Thorn Boggart", "<Wyldfae Scavenger>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 602, 197, 37500, 40000, 16000, 0, 26, 0, None, 550, 100),
        ("Briar Sprite", "<Seelie Outrider>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 603, 197, 39000, 39500, 16000, 0, 25, 0, None, 550, 100),
        ("Briar Sprite", "<Seelie Outrider>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 603, 197, 41000, 41000, 16000, 0, 28, 0, None, 550, 100),
        ("Bramblewood Lurker", "<Briar Beast>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 764, 197, 33500, 43000, 16000, 0, 29, 0, None, 550, 100),
        ("Bramblewood Lurker", "<Briar Beast>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 764, 197, 36000, 44000, 16000, 0, 30, 0, None, 550, 100),

        # Zone 2: The Twilight Mist & Shadow Woods (Levels 33–46, South/Southeast)
        ("Redcap Cutthroat", "<Unseelie Marauder>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 672, 197, 35500, 25500, 16000, 0, 33, 0, None, 550, 100),
        ("Redcap Cutthroat", "<Unseelie Marauder>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 672, 197, 37000, 24500, 16000, 0, 35, 0, None, 550, 100),
        ("Redcap Marauder", "<Unseelie Marauder>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 671, 197, 38500, 23500, 16000, 0, 38, 0, None, 550, 100),
        ("Redcap Marauder", "<Unseelie Marauder>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 671, 197, 40000, 22500, 16000, 0, 40, 0, None, 550, 100),
        ("Shadow Malk", "<Stalker of the Ways>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 695, 197, 39000, 26500, 16000, 0, 39, 0, None, 550, 100),
        ("Shadow Malk", "<Stalker of the Ways>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 695, 197, 41000, 25500, 16000, 0, 42, 0, None, 550, 100),
        ("Twilight Fetch", "<Deep Nevernever Horror>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 667, 197, 42500, 22000, 16000, 0, 43, 0, None, 550, 100),
        ("Twilight Fetch", "<Deep Nevernever Horror>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 667, 197, 40500, 20500, 16000, 0, 46, 0, None, 550, 100),

        # Zone 3: The Dark Water Margins & Stygian Bogs (Levels 48–55, Deep Southwest)
        ("Will-o'-the-Wisp", "<Corpse Candle>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 966, 197, 23000, 22000, 16000, 0, 48, 0, None, 550, 100),
        ("Will-o'-the-Wisp", "<Corpse Candle>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 966, 197, 25000, 20000, 16000, 0, 50, 0, None, 550, 100),
        ("Bog Hag of the Sunder", "<Wicked Coven>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 664, 197, 22500, 22500, 16000, 0, 50, 0, None, 550, 100),
        ("Bog Hag of the Sunder", "<Wicked Coven>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 664, 197, 24000, 18500, 16000, 0, 52, 0, None, 550, 100),
        ("Rawhead & Bloodybones", "<Ancient Bogeyman of the Deep>", "DOL.GS.NeverneverBossFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 663, 197, 21500, 20500, 15900, 0, 55, 0, None, 650, 100),

        # Zone 4: Outer Winter Borderlands (Approaching Region 195, Levels 43–52, West)
        ("Glacial Frosthound", "<Unseelie Pack>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 459, 197, 24500, 28000, 16000, 0, 43, 0, None, 550, 100),
        ("Glacial Frosthound", "<Unseelie Pack>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 459, 197, 23000, 26000, 16000, 0, 45, 0, None, 550, 100),
        ("Unseelie Border Sentinel", "<The Winter Court>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 680, 197, 25000, 26000, 16000, 0, 47, 0, None, 550, 100),
        ("Unseelie Border Sentinel", "<The Winter Court>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 680, 197, 24000, 25000, 16000, 0, 49, 0, None, 550, 100),
        ("Rime-Crusted Fetch", "<Winter Shadow>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 658, 197, 22000, 27000, 16000, 0, 50, 0, None, 550, 100),
        ("Froststalker Wendigo", "<Apex Winter Beast>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 690, 197, 20500, 26000, 16000, 0, 52, 0, None, 550, 100),

        # Zone 5: Outer Summer Borderlands (Approaching Region 196, Levels 43–52, East)
        ("Seelie Border Scout", "<The Summer Court>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 679, 197, 43500, 36000, 16000, 0, 43, 0, None, 550, 100),
        ("Seelie Border Scout", "<The Summer Court>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 679, 197, 44500, 34000, 16000, 0, 45, 0, None, 550, 100),
        ("Briar Thornsteed", "<Seelie Steed>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 870, 197, 45000, 36500, 16000, 0, 47, 0, None, 550, 100),
        ("Briar Thornsteed", "<Seelie Steed>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 870, 197, 46000, 33500, 16000, 0, 49, 0, None, 550, 100),
        ("Golden Willow Wisp", "<Summer Court Wisp>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 966, 197, 44000, 37000, 16000, 0, 49, 0, None, 550, 100),
        ("Elder Gruff of the Wildwood", "<Summer Champion>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 682, 197, 46500, 37000, 16000, 0, 52, 0, None, 550, 100),
    ]
    all_mobs.extend(nevernever_mobs)

    # --- Summer Court Outpost (Region 196) ---
    summer_outpost = [
        ("Seelie Sidhe Sentinel", "<Summer Court Guard>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 679, 196, 25752, 21200, 16000, 2048, 45, 0, None, 550, 100),
        ("Elder Gruff of the Oak", "<Summer Court Champion>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 682, 196, 25850, 21100, 16000, 2048, 50, 0, None, 550, 100),
        ("Lord Taliesin of the Golden Bough", "<Summer Court High Sidhe>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 679, 196, 25752, 21400, 16000, 2048, 55, 0, None, 550, 100),
        ("Briar Sprite Outrider", "<Summer Court Sprite>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 603, 196, 25650, 21300, 16000, 2048, 46, 0, None, 550, 100),
    ]
    all_mobs.extend(summer_outpost)

    # --- Winter Court Outpost (Region 195) ---
    winter_outpost = [
        ("Unseelie Sidhe Arbiter", "<Winter Court Guard>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 680, 195, 32852, 14750, 16000, 2048, 45, 0, None, 550, 100),
        ("Malk Stalker of Arctis Tor", "<Winter Court Hunter>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 695, 195, 32950, 14650, 16000, 2048, 50, 0, None, 550, 100),
        ("Lord Slate of the Winter Mantle", "<Winter Court High Sidhe>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 680, 195, 32852, 14950, 16000, 2048, 55, 0, None, 550, 100),
        ("Glacial Frosthound Hunter", "<Winter Court Hound>", "DOL.GS.NeverneverFaeNPC", "DOL.AI.Brain.NeverneverFaeBrain", 459, 195, 32750, 14850, 16000, 2048, 46, 0, None, 550, 100),
    ]
    all_mobs.extend(winter_outpost)

    # Insert all mobs into database
    for name, guild, class_type, brain, model, reg, x, y, z, heading, level, realm, eq_template, aggro_range, aggro_level in all_mobs:
        mob_id = f"mob_{reg}_{name.lower().replace(' ', '_').replace('-', '_').replace('&', 'and')}_{x}_{y}"
        cur.execute("""
            INSERT OR REPLACE INTO Mob (
                ClassType, Name, Guild, X, Y, Z, Heading, Region, Model,
                Level, Realm, EquipmentTemplateID, AggroRange, AggroLevel,
                Brain, PackageID, LastTimeRowUpdated, Mob_ID
            ) VALUES (
                ?, ?, ?, ?, ?, ?, ?, ?, ?,
                ?, ?, ?, ?, ?,
                ?, ?, CURRENT_TIMESTAMP, ?
            )
        """, (
            class_type, name, guild, x, y, z, heading, reg, model,
            level, realm, eq_template, aggro_range, aggro_level,
            brain, PACKAGE_ID, mob_id
        ))

    conn.commit()
    conn.close()
    print(f"[Success] Seeded {len(all_mobs)} Mobs, {len(custom_items)} ItemTemplates, {len(equip_templates)} NPCEquipment entries, {len(loot_defs)} LootTemplates, and {len(crafting_stations) + 1} WorldObjects for Edinburgh & Nevernever.")


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed Edinburgh and Nevernever into OpenDAoC database.")
    parser.add_argument("--db", type=Path, default=DEFAULT_DB_PATH, help="Path to opendaoc.sqlite3.db")
    parser.add_argument("--no-backup", action="store_true", help="Skip creating database backup")
    args = parser.parse_args()

    if not args.db.exists():
        print(f"Error: Database file not found at {args.db}", file=sys.stderr)
        sys.exit(1)

    if not args.no_backup:
        copy_backup(args.db)

    seed_database(args.db)


if __name__ == "__main__":
    main()
