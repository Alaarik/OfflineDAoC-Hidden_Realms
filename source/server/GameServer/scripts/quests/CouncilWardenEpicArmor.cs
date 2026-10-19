using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;
using DOL.GS.PacketHandler;

namespace DOL.GS.Quests
{
    /// <summary>
    /// Defines and manages all Council Warden quest rewards and epic raid drops from
    /// warden_class_plan.md Sections 6 & 8:
    ///   - Level 5 Promotion Gift: Initiate's Grey Cloak
    ///   - Chapters 1–5 Milestone Rewards (Levels 10, 20, 30, 40, 45)
    ///   - Chapter 6 (Level 50): Complete 6-Piece Epic Armor Set + The Warden's Silver Blade(s)
    ///   - Section 8 Epic Dungeon & Raid Drops (Caer Sidi, Tuscaran Glacier, Galladoria,
    ///     Darkness Falls, Summoner's Hall)
    /// </summary>
    public static class CouncilWardenEpicArmor
    {
        public const string Prefix = "council_warden_";
        public const string AllowedWardenClasses = "60;61;58";

        // Spell IDs for Cloak, Silver Weapons, and Raid Procs
        public const int CircleOfGroundingUseSpellId = 59320;
        public const int FeraSilverWeaveProcSpellId = 59321;
        public const int SilverRadianceProcSpellId = 59322;
        public const int AegisOfFirstWardensProcSpellId = 59323;
        public const int KineticDragProcSpellId = 59324;
        public const int ThermalFeedbackProcSpellId = 59325;
        public const int GlacialRuptureProcSpellId = 59326;
        public const int FlourishOfWillProcSpellId = 59327;
        public const int SolarFlareProcSpellId = 59328;
        public const int SigilOfTruthUseSpellId = 59329;
        public const int KineticDeflectionProcSpellId = 59361;
        public const int SeveringBoltChargeSpellId = 59360;

        public sealed record Piece(
            string IdNb,
            string Name,
            int Level,
            int Model,
            eObjectType ObjectType,
            eInventorySlot Slot,
            int DpsAf,
            int SpdAbs,
            int Weight,
            eRealm Realm,
            (eProperty Property, int Value)[] Bonuses,
            int TypeDamage = 0,
            int Hand = 0,
            int ProcSpellId = 0,
            int SpellId = 0,
            int Charges = 0,
            bool CanDropAsLoot = false,
            string Description = "Forged in the Sanctuary of Edinburgh for Wardens of the White Council.");

        // -----------------------------------------------------------------
        // 1. Level 5 Promotion Gift & Chapters 1–5 Milestone Rewards
        // -----------------------------------------------------------------

        public static readonly Piece InitiateGreyCloak = new(
            "council_warden_initiate_cloak",
            "Initiate's Grey Cloak",
            5, 443, eObjectType.Magical, eInventorySlot.Cloak, 0, 0, 8, eRealm.None,
            new[]
            {
                (eProperty.Strength, 3),
                (eProperty.Intelligence, 3),
                (eProperty.MaxHealth, 8),
                (eProperty.Resist_Energy, 2),
                (eProperty.Resist_Spirit, 2),
            },
            Description: "The grey wool duster presented to a newly sworn initiate of the White Council Wardens.");

        public static readonly Piece Chapter1Signet = new(
            "council_warden_ch1_signet",
            "Signet of the Novice Warden",
            7, 499, eObjectType.Magical, eInventorySlot.LeftRing, 0, 0, 2, eRealm.None,
            new[]
            {
                (eProperty.Strength, 4),
                (eProperty.Intelligence, 4),
                (eProperty.MaxHealth, 12),
                (eProperty.Resist_Spirit, 2),
            },
            Description: "A bronze signet bearing the nascent seal of Edinburgh, attuned to novice warding circles.");

        public static readonly Piece Chapter2Bracer = new(
            "council_warden_ch2_bracer",
            "Operative's Warded Bracer",
            13, 498, eObjectType.Magical, eInventorySlot.LeftBracer, 0, 0, 4, eRealm.None,
            new[]
            {
                (eProperty.Strength, 6),
                (eProperty.Constitution, 6),
                (eProperty.MaxHealth, 16),
                (eProperty.Resist_Slash, 4),
                (eProperty.Resist_Thrust, 4),
            },
            Description: "Inscribed with border-ward runes commemorating the ancient Arthurian cold-iron compacts with the Sidhe.");

        public static readonly Piece Chapter3Belt = new(
            "council_warden_ch3_belt",
            "Cincture of the Ley-Caster",
            20, 597, eObjectType.Magical, eInventorySlot.Waist, 0, 0, 5, eRealm.None,
            new[]
            {
                (eProperty.Intelligence, 8),
                (eProperty.Dexterity, 8),
                (eProperty.Quickness, 8),
                (eProperty.MaxHealth, 22),
                (eProperty.Resist_Body, 4),
                (eProperty.Resist_Energy, 4),
            },
            Description: "Woven from ley-line silver thread, grounding excess magical discharge during intense combat.");

        public static readonly Piece Chapter4Necklace = new(
            "council_warden_ch4_neck",
            "Pendant of the White Council",
            25, 494, eObjectType.Magical, eInventorySlot.Neck, 0, 0, 3, eRealm.None,
            new[]
            {
                (eProperty.Strength, 10),
                (eProperty.Constitution, 10),
                (eProperty.Intelligence, 10),
                (eProperty.MaxHealth, 28),
                (eProperty.Resist_Heat, 6),
                (eProperty.Resist_Cold, 6),
                (eProperty.AllMagicSkills, 1),
            },
            Description: "Official credentials worn by full-fledged Council investigators dispatched to the frontier.");

        public static readonly Piece Chapter5Ring = new(
            "council_warden_ch5_ring",
            "Band of the Way-Keeper",
            30, 499, eObjectType.Magical, eInventorySlot.RightRing, 0, 0, 2, eRealm.None,
            new[]
            {
                (eProperty.Dexterity, 10),
                (eProperty.Quickness, 10),
                (eProperty.MaxHealth, 24),
                (eProperty.Resist_Cold, 6),
                (eProperty.Resist_Matter, 6),
            },
            Description: "Recovered from the Nevernever Crossroads, attuned to traversing the unwarded ways.");

        public static readonly Piece Chapter6Bracer = new(
            "council_warden_ch6_bracer",
            "Bracers of the Meteoric Quench",
            35, 498, eObjectType.Magical, eInventorySlot.RightBracer, 0, 0, 4, eRealm.None,
            new[]
            {
                (eProperty.Strength, 12),
                (eProperty.Constitution, 12),
                (eProperty.MaxHealth, 32),
                (eProperty.Resist_Heat, 6),
                (eProperty.Resist_Energy, 6),
                (eProperty.MeleeDamage, 3),
            },
            Description: "Forged in the secret wild foundry from the first ingot of meteoric silver.");

        public static readonly Piece Chapter7Jewel = new(
            "council_warden_ch7_jewel",
            "Talisman of the Bleeding Menhir",
            40, 494, eObjectType.Magical, eInventorySlot.Mythical, 0, 0, 2, eRealm.None,
            new[]
            {
                (eProperty.Intelligence, 14),
                (eProperty.Constitution, 14),
                (eProperty.MaxHealth, 36),
                (eProperty.Resist_Spirit, 8),
                (eProperty.Resist_Body, 8),
                (eProperty.AllMagicSkills, 2),
            },
            Description: "Chiseled from the purified stone of the Continental Anchor after banishing the Outsider.");

        public static readonly Piece Chapter8GreyCloak = new(
            "council_warden_ch5_cloak",
            "The Warden's Grey Cloak",
            45, 443, eObjectType.Magical, eInventorySlot.Cloak, 0, 0, 10, eRealm.None,
            new[]
            {
                (eProperty.Strength, 16),
                (eProperty.Intelligence, 16),
                (eProperty.Constitution, 14),
                (eProperty.Quickness, 14),
                (eProperty.MaxHealth, 44),
                (eProperty.Resist_Body, 8),
                (eProperty.Resist_Spirit, 8),
                (eProperty.Resist_Energy, 8),
                (eProperty.Resist_Heat, 8),
            },
            ProcSpellId: FeraSilverWeaveProcSpellId,
            SpellId: CircleOfGroundingUseSpellId,
            Charges: 10,
            Description: "The iconic symbol of a White Council Warden. A heavy, warded grey wool duster treated to repel both the elements and magical aggression.");

        public static readonly Piece Chapter5GreyCloak = Chapter8GreyCloak;

        public static readonly Piece Chapter1Focus = new(
            "council_warden_ch1_focus",
            "Apprentice's Blasting Rod",
            10, 494, eObjectType.Magical, eInventorySlot.LeftHandWeapon, 0, 0, 2, eRealm.None,
            new[]
            {
                (eProperty.AllFocusLevels, 10),
                (eProperty.Intelligence, 5),
                (eProperty.Skill_Parry, 1),
            },
            Hand: 2,
            Description: "An ash-wood blasting rod tipped with silver, focusing early kinetic discharges.");

        public static readonly Piece Chapter2Focus = new(
            "council_warden_ch2_focus",
            "Journeyman's Focus Orb",
            20, 494, eObjectType.Magical, eInventorySlot.LeftHandWeapon, 0, 0, 3, eRealm.None,
            new[]
            {
                (eProperty.AllFocusLevels, 20),
                (eProperty.Intelligence, 10),
                (eProperty.Skill_Parry, 2),
                (eProperty.MeleeDamage, 3),
            },
            Hand: 2,
            Description: "A polished obsidian orb bound in silver wire, balancing martial defense and spellcraft.");

        public static readonly Piece Chapter3Focus = new(
            "council_warden_ch3_focus",
            "Adept's Ley-Talisman",
            30, 494, eObjectType.Magical, eInventorySlot.LeftHandWeapon, 0, 0, 3, eRealm.None,
            new[]
            {
                (eProperty.AllFocusLevels, 30),
                (eProperty.Intelligence, 15),
                (eProperty.Skill_Parry, 3),
                (eProperty.MeleeDamage, 4),
            },
            Hand: 2,
            Description: "Carved with Edinburgh threshold glyphs, drawing power directly from the land's ley-lines.");

        public static readonly Piece Chapter4Focus = new(
            "council_warden_ch4_focus",
            "Magus's Blasting Rod",
            40, 494, eObjectType.Magical, eInventorySlot.LeftHandWeapon, 0, 0, 4, eRealm.None,
            new[]
            {
                (eProperty.AllFocusLevels, 40),
                (eProperty.Intelligence, 18),
                (eProperty.Skill_Parry, 4),
                (eProperty.MeleeDamage, 5),
            },
            Hand: 2,
            Description: "A heavy silver-banded ironwood rod tuned to senior combat evocations.");

        public static readonly Piece[] ChapterMilestonePieces =
        {
            InitiateGreyCloak,
            Chapter1Signet,
            Chapter1Focus,
            Chapter2Bracer,
            Chapter2Focus,
            Chapter3Belt,
            Chapter3Focus,
            Chapter4Necklace,
            Chapter4Focus,
            Chapter5Ring,
            Chapter6Bracer,
            Chapter7Jewel,
            Chapter8GreyCloak,
        };

        // -----------------------------------------------------------------
        // 2. Chapter 6 (Level 50) Epic Armor Sets & Silver Blades
        // -----------------------------------------------------------------

        private static readonly (eProperty Property, int Value)[] ChestBonuses =
        {
            (eProperty.Strength, 15),
            (eProperty.Constitution, 15),
            (eProperty.MaxHealth, 40),
            (eProperty.Resist_Crush, 8),
            (eProperty.Resist_Thrust, 8),
            (eProperty.AllMeleeWeaponSkills, 3),
        };

        private static readonly (eProperty Property, int Value)[] ArmsBonuses =
        {
            (eProperty.Strength, 14),
            (eProperty.Dexterity, 14),
            (eProperty.Quickness, 12),
            (eProperty.Resist_Slash, 8),
            (eProperty.Resist_Spirit, 8),
        };

        private static readonly (eProperty Property, int Value)[] LegsBonuses =
        {
            (eProperty.Constitution, 15),
            (eProperty.Intelligence, 15),
            (eProperty.MaxHealth, 36),
            (eProperty.Resist_Heat, 8),
            (eProperty.Resist_Cold, 8),
            (eProperty.Skill_Parry, 3),
        };

        private static readonly (eProperty Property, int Value)[] HelmBonuses =
        {
            (eProperty.Intelligence, 14),
            (eProperty.Quickness, 14),
            (eProperty.Dexterity, 12),
            (eProperty.Resist_Body, 8),
            (eProperty.Resist_Energy, 8),
            (eProperty.AllMagicSkills, 2),
        };

        private static readonly (eProperty Property, int Value)[] HandsBonuses =
        {
            (eProperty.Strength, 14),
            (eProperty.Dexterity, 14),
            (eProperty.Quickness, 12),
            (eProperty.Resist_Matter, 8),
            (eProperty.AllMeleeWeaponSkills, 3),
            (eProperty.MeleeDamage, 3),
        };

        private static readonly (eProperty Property, int Value)[] BootsBonuses =
        {
            (eProperty.Constitution, 14),
            (eProperty.Quickness, 14),
            (eProperty.MaxHealth, 32),
            (eProperty.Resist_Crush, 8),
            (eProperty.Resist_Slash, 8),
            (eProperty.Skill_Parry, 3),
        };

        private static readonly (eProperty Property, int Value)[] SilverBladeBonuses =
        {
            (eProperty.Strength, 16),
            (eProperty.Intelligence, 16),
            (eProperty.Dexterity, 14),
            (eProperty.MaxHealth, 32),
            (eProperty.AllMeleeWeaponSkills, 3),
            (eProperty.Skill_Parry, 3),
            (eProperty.CastingSpeed, 3),
        };

        private static readonly (eProperty Property, int Value)[] EpicStaffBonuses =
        {
            (eProperty.AllFocusLevels, 50),
            (eProperty.AllMagicSkills, 4),
            (eProperty.SpellRange, 5),
            (eProperty.SpellPowerCost, 5),
            (eProperty.Intelligence, 16),
            (eProperty.MaxHealth, 40),
            (eProperty.CastingSpeed, 4),
        };

        private static readonly (eProperty Property, int Value)[] EpicFocusBonuses =
        {
            (eProperty.AllFocusLevels, 50),
            (eProperty.AllMagicSkills, 4),
            (eProperty.Skill_Parry, 4),
            (eProperty.MeleeDamage, 5),
            (eProperty.MeleeSpeed, 5),
            (eProperty.MaxHealth, 60),
            (eProperty.Intelligence, 16),
        };

        public static readonly Piece EpicStaff = new(
            "council_warden_epic_staff",
            "Staff of the White Council",
            50, 48, eObjectType.Staff, eInventorySlot.TwoHandWeapon, 165, 40, 30, eRealm.None,
            EpicStaffBonuses,
            TypeDamage: (int)eDamageType.Crush,
            Hand: 1,
            Description: "A polished silver-bound oak staff of the White Council, focusing all magical disciplines.");

        public static readonly Piece EpicFocus = new(
            "council_warden_epic_focus",
            "The Warden's Blasting Rod",
            50, 494, eObjectType.Magical, eInventorySlot.LeftHandWeapon, 0, 0, 4, eRealm.None,
            EpicFocusBonuses,
            Hand: 2,
            Description: "The master blasting rod of a White Council Warden, anchoring defensive kinetic parries and spellcraft.");

        public static readonly Piece[] AlbionEpicSet =
        {
            new("council_warden_epic_alb_hauberk", "Hauberk of the White Council", 50, 713, eObjectType.Chain, eInventorySlot.TorsoArmor, 102, 27, 45, eRealm.Albion, ChestBonuses),
            new("council_warden_epic_alb_sleeves", "Sleeves of the White Council", 50, 715, eObjectType.Chain, eInventorySlot.ArmsArmor, 102, 27, 25, eRealm.Albion, ArmsBonuses),
            new("council_warden_epic_alb_chausses", "Chausses of the White Council", 50, 714, eObjectType.Chain, eInventorySlot.LegsArmor, 102, 27, 32, eRealm.Albion, LegsBonuses),
            new("council_warden_epic_alb_coif", "Coif of the White Council", 50, 822, eObjectType.Chain, eInventorySlot.HeadArmor, 102, 27, 20, eRealm.Albion, HelmBonuses),
            new("council_warden_epic_alb_gauntlets", "Gauntlets of the White Council", 50, 716, eObjectType.Chain, eInventorySlot.HandsArmor, 102, 27, 16, eRealm.Albion, HandsBonuses),
            new("council_warden_epic_alb_boots", "Sabatons of the White Council", 50, 717, eObjectType.Chain, eInventorySlot.FeetArmor, 102, 27, 18, eRealm.Albion, BootsBonuses),
            new("council_warden_epic_alb_broadsword", "Silver Broadsword of the Council", 50, 3, eObjectType.SlashingWeapon, eInventorySlot.RightHandWeapon, 162, 38, 28, eRealm.Albion, SilverBladeBonuses, TypeDamage: (int)eDamageType.Slash, ProcSpellId: SilverRadianceProcSpellId, Description: "Custom-forged from enchanted silver by Senior Council metallurgists. Channels Silver Radiance on strike."),
            new("council_warden_epic_alb_estoc", "Silver Estoc of the Council", 50, 22, eObjectType.ThrustWeapon, eInventorySlot.RightHandWeapon, 162, 33, 24, eRealm.Albion, SilverBladeBonuses, TypeDamage: (int)eDamageType.Thrust, ProcSpellId: SilverRadianceProcSpellId, Description: "Custom-forged from enchanted silver by Senior Council metallurgists. Channels Silver Radiance on strike."),
            EpicStaff,
            EpicFocus,
        };

        public static readonly Piece[] MidgardEpicSet =
        {
            new("council_warden_epic_mid_hauberk", "Studded Hauberk of the Warden", 50, 761, eObjectType.Studded, eInventorySlot.TorsoArmor, 102, 19, 40, eRealm.Midgard, ChestBonuses),
            new("council_warden_epic_mid_vambraces", "Studded Vambraces of the Warden", 50, 763, eObjectType.Studded, eInventorySlot.ArmsArmor, 102, 19, 22, eRealm.Midgard, ArmsBonuses),
            new("council_warden_epic_mid_leggings", "Studded Leggings of the Warden", 50, 762, eObjectType.Studded, eInventorySlot.LegsArmor, 102, 19, 28, eRealm.Midgard, LegsBonuses),
            new("council_warden_epic_mid_helm", "Studded Helm of the Warden", 50, 829, eObjectType.Studded, eInventorySlot.HeadArmor, 102, 19, 18, eRealm.Midgard, HelmBonuses),
            new("council_warden_epic_mid_gauntlets", "Studded Gauntlets of the Warden", 50, 764, eObjectType.Studded, eInventorySlot.HandsArmor, 102, 19, 14, eRealm.Midgard, HandsBonuses),
            new("council_warden_epic_mid_sabatons", "Studded Sabatons of the Warden", 50, 765, eObjectType.Studded, eInventorySlot.FeetArmor, 102, 19, 16, eRealm.Midgard, BootsBonuses),
            new("council_warden_epic_mid_spatha", "Silver Spatha of the Council", 50, 311, eObjectType.Sword, eInventorySlot.RightHandWeapon, 162, 37, 27, eRealm.Midgard, SilverBladeBonuses, TypeDamage: (int)eDamageType.Slash, ProcSpellId: SilverRadianceProcSpellId, Description: "Custom-forged from enchanted silver by Senior Council metallurgists. Channels Silver Radiance on strike."),
            EpicStaff,
            EpicFocus,
        };

        public static readonly Piece[] HiberniaEpicSet =
        {
            new("council_warden_epic_hib_vest", "Reinforced Vest of the Council", 50, 738, eObjectType.Reinforced, eInventorySlot.TorsoArmor, 102, 19, 40, eRealm.Hibernia, ChestBonuses),
            new("council_warden_epic_hib_sleeves", "Reinforced Sleeves of the Council", 50, 740, eObjectType.Reinforced, eInventorySlot.ArmsArmor, 102, 19, 22, eRealm.Hibernia, ArmsBonuses),
            new("council_warden_epic_hib_leggings", "Reinforced Leggings of the Council", 50, 739, eObjectType.Reinforced, eInventorySlot.LegsArmor, 102, 19, 28, eRealm.Hibernia, LegsBonuses),
            new("council_warden_epic_hib_cap", "Reinforced Cap of the Council", 50, 834, eObjectType.Reinforced, eInventorySlot.HeadArmor, 102, 19, 18, eRealm.Hibernia, HelmBonuses),
            new("council_warden_epic_hib_gloves", "Reinforced Gloves of the Council", 50, 741, eObjectType.Reinforced, eInventorySlot.HandsArmor, 102, 19, 14, eRealm.Hibernia, HandsBonuses),
            new("council_warden_epic_hib_boots", "Reinforced Boots of the Council", 50, 742, eObjectType.Reinforced, eInventorySlot.FeetArmor, 102, 19, 16, eRealm.Hibernia, BootsBonuses),
            new("council_warden_epic_hib_longblade", "Silver Longblade of the Council", 50, 445, eObjectType.Blades, eInventorySlot.RightHandWeapon, 162, 36, 26, eRealm.Hibernia, SilverBladeBonuses, TypeDamage: (int)eDamageType.Slash, ProcSpellId: SilverRadianceProcSpellId, Description: "Custom-forged from enchanted silver by Senior Council metallurgists. Channels Silver Radiance on strike."),
            new("council_warden_epic_hib_stiletto", "Silver Stiletto of the Council", 50, 456, eObjectType.Piercing, eInventorySlot.RightHandWeapon, 162, 29, 20, eRealm.Hibernia, SilverBladeBonuses, TypeDamage: (int)eDamageType.Thrust, ProcSpellId: SilverRadianceProcSpellId, Description: "Custom-forged from enchanted silver by Senior Council metallurgists. Channels Silver Radiance on strike."),
            EpicStaff,
            EpicFocus,
        };

        // -----------------------------------------------------------------
        // 3. Section 8 Epic Dungeon & Raid Item Drops (Level 51 / AF 102 / 16.5 DPS)
        // -----------------------------------------------------------------

        public static readonly Piece LoricamIncorruptus = new(
            "council_warden_raid_loricam_incorruptus",
            "Loricam Incorruptus",
            51, 713, eObjectType.Chain, eInventorySlot.TorsoArmor, 102, 27, 45, eRealm.Albion,
            new[]
            {
                (eProperty.Strength, 18),
                (eProperty.Constitution, 18),
                (eProperty.Intelligence, 18),
                (eProperty.MaxHealth, 60),
                (eProperty.Resist_Spirit, 12),
                (eProperty.Resist_Slash, 8),
                (eProperty.Resist_Thrust, 8),
                (eProperty.AllMeleeWeaponSkills, 4),
                (eProperty.Skill_Parry, 4),
                (eProperty.MeleeDamage, 4),
            },
            ProcSpellId: AegisOfFirstWardensProcSpellId,
            CanDropAsLoot: true,
            Description: "Inscribed with the original grounding glyphs devised by Merlin in the wake of Camlann to insulate the mortal body against physical violence and supernatural corruption.");

        public static readonly Piece SeveranceOfCamlannSlash = new(
            "council_warden_raid_severance_slash",
            "Severance of Camlann",
            51, 3, eObjectType.SlashingWeapon, eInventorySlot.RightHandWeapon, 165, 38, 28, eRealm.Albion,
            new[]
            {
                (eProperty.Strength, 18),
                (eProperty.Dexterity, 15),
                (eProperty.Quickness, 15),
                (eProperty.Skill_Slashing, 4),
                (eProperty.Skill_Parry, 4),
            },
            TypeDamage: (int)eDamageType.Slash,
            ProcSpellId: KineticDragProcSpellId,
            CanDropAsLoot: true,
            Description: "A masterwork silver-alloy broadsword forged to sever hostile enchantments and cut cleanly through enemy defenses.");

        public static readonly Piece SeveranceOfCamlannThrust = new(
            "council_warden_raid_severance_thrust",
            "Severance of Camlann (Estoc)",
            51, 22, eObjectType.ThrustWeapon, eInventorySlot.RightHandWeapon, 165, 33, 24, eRealm.Albion,
            new[]
            {
                (eProperty.Strength, 18),
                (eProperty.Dexterity, 15),
                (eProperty.Quickness, 15),
                (eProperty.Skill_Thrusting, 4),
                (eProperty.Skill_Parry, 4),
            },
            TypeDamage: (int)eDamageType.Thrust,
            ProcSpellId: KineticDragProcSpellId,
            CanDropAsLoot: true,
            Description: "A masterwork silver-alloy estoc forged to sever hostile enchantments and cut cleanly through enemy defenses.");

        public static readonly Piece MantleOfTheWinterGate = new(
            "council_warden_raid_mantle_winter_gate",
            "Mantle of the Winter Gate",
            51, 761, eObjectType.Studded, eInventorySlot.TorsoArmor, 102, 19, 40, eRealm.Midgard,
            new[]
            {
                (eProperty.Strength, 18),
                (eProperty.Constitution, 18),
                (eProperty.Intelligence, 18),
                (eProperty.MaxHealth, 60),
                (eProperty.Resist_Cold, 12),
                (eProperty.Resist_Crush, 8),
                (eProperty.Resist_Thrust, 8),
                (eProperty.Skill_Sword, 4),
                (eProperty.Skill_Parry, 4),
                (eProperty.MeleeDamage, 4),
            },
            ProcSpellId: ThermalFeedbackProcSpellId,
            CanDropAsLoot: true,
            Description: "Recovered from an ancient Warden who fell defending the mortal frontier from Unseelie incursions.");

        public static readonly Piece Rimelance = new(
            "council_warden_raid_rimelance",
            "Rimelance",
            51, 311, eObjectType.Sword, eInventorySlot.RightHandWeapon, 165, 37, 27, eRealm.Midgard,
            new[]
            {
                (eProperty.Strength, 18),
                (eProperty.Dexterity, 15),
                (eProperty.Quickness, 15),
                (eProperty.Skill_Sword, 4),
                (eProperty.Skill_Parry, 4),
            },
            TypeDamage: (int)eDamageType.Slash,
            ProcSpellId: GlacialRuptureProcSpellId,
            CanDropAsLoot: true,
            Description: "A glacial spatha forged in the deep rime, rupturing enemy footing with Winter's chill.");

        public static readonly Piece CuirassOfTheSummerSolstice = new(
            "council_warden_raid_cuirass_summer_solstice",
            "Cuirass of the Summer Solstice",
            51, 738, eObjectType.Reinforced, eInventorySlot.TorsoArmor, 102, 19, 40, eRealm.Hibernia,
            new[]
            {
                (eProperty.Strength, 18),
                (eProperty.Constitution, 18),
                (eProperty.Intelligence, 18),
                (eProperty.MaxHealth, 60),
                (eProperty.Resist_Heat, 12),
                (eProperty.Resist_Slash, 8),
                (eProperty.Resist_Matter, 8),
                (eProperty.AllMeleeWeaponSkills, 4),
                (eProperty.Skill_Parry, 4),
                (eProperty.MeleeDamage, 4),
            },
            ProcSpellId: FlourishOfWillProcSpellId,
            CanDropAsLoot: true,
            Description: "Woven from iron-treated ironwood and living amber, imbued under the Summer Solstice to channel vitality.");

        public static readonly Piece SunshardBlades = new(
            "council_warden_raid_sunshard_blades",
            "Sunshard",
            51, 445, eObjectType.Blades, eInventorySlot.RightHandWeapon, 165, 36, 26, eRealm.Hibernia,
            new[]
            {
                (eProperty.Strength, 18),
                (eProperty.Dexterity, 15),
                (eProperty.Quickness, 15),
                (eProperty.Skill_Blades, 4),
                (eProperty.Skill_Parry, 4),
            },
            TypeDamage: (int)eDamageType.Slash,
            ProcSpellId: SolarFlareProcSpellId,
            CanDropAsLoot: true,
            Description: "A solar longblade of living amber and silver that blinds enemy attackers on impact.");

        public static readonly Piece SunshardPiercing = new(
            "council_warden_raid_sunshard_piercing",
            "Sunshard (Stiletto)",
            51, 456, eObjectType.Piercing, eInventorySlot.RightHandWeapon, 165, 29, 20, eRealm.Hibernia,
            new[]
            {
                (eProperty.Strength, 18),
                (eProperty.Dexterity, 15),
                (eProperty.Quickness, 15),
                (eProperty.Skill_Piercing, 4),
                (eProperty.Skill_Parry, 4),
            },
            TypeDamage: (int)eDamageType.Thrust,
            ProcSpellId: SolarFlareProcSpellId,
            CanDropAsLoot: true,
            Description: "A solar stiletto of living amber and silver that blinds enemy attackers on impact.");

        private static readonly (eProperty Property, int Value)[] RaidStaffBonuses =
        {
            (eProperty.AllFocusLevels, 50),
            (eProperty.AllMagicSkills, 4),
            (eProperty.SpellRange, 6),
            (eProperty.SpellPowerCost, 6),
            (eProperty.Intelligence, 18),
            (eProperty.Dexterity, 15),
            (eProperty.MaxHealth, 50),
            (eProperty.CastingSpeed, 5),
        };

        private static readonly (eProperty Property, int Value)[] RaidLegionStaffBonuses =
        {
            (eProperty.AllFocusLevels, 50),
            (eProperty.AllMagicSkills, 4),
            (eProperty.SpellRange, 8),
            (eProperty.SpellPowerCost, 8),
            (eProperty.Intelligence, 20),
            (eProperty.Dexterity, 18),
            (eProperty.MaxHealth, 60),
            (eProperty.CastingSpeed, 6),
        };

        private static readonly (eProperty Property, int Value)[] RaidFocusBonuses =
        {
            (eProperty.AllFocusLevels, 50),
            (eProperty.AllMagicSkills, 4),
            (eProperty.Skill_Parry, 4),
            (eProperty.MeleeDamage, 6),
            (eProperty.MeleeSpeed, 6),
            (eProperty.MaxHealth, 70),
            (eProperty.Intelligence, 18),
            (eProperty.Constitution, 18),
        };

        public static readonly Piece SidiStaff = new(
            "council_warden_raid_sidi_staff",
            "Sigil-Stave of the First Warden",
            51, 48, eObjectType.Staff, eInventorySlot.TwoHandWeapon, 165, 40, 30, eRealm.Albion,
            RaidStaffBonuses,
            TypeDamage: (int)eDamageType.Crush,
            Hand: 1,
            CanDropAsLoot: true,
            Description: "Inscribed with the original Camlann grounding glyphs, focusing senior thaumaturgical rites.");

        public static readonly Piece SidiFocus = new(
            "council_warden_raid_sidi_focus",
            "Sigil-Talisman of Camlann",
            51, 494, eObjectType.Magical, eInventorySlot.LeftHandWeapon, 0, 0, 4, eRealm.Albion,
            RaidFocusBonuses,
            Hand: 2,
            ProcSpellId: KineticDeflectionProcSpellId,
            CanDropAsLoot: true,
            Description: "Forged in the depths of Caer Sidi, projecting kinetic deflection shields when deflecting enemy assault.");

        public static readonly Piece TuscaranStaff = new(
            "council_warden_raid_tuscaran_staff",
            "Rime-Carved Focus-Staff",
            51, 48, eObjectType.Staff, eInventorySlot.TwoHandWeapon, 165, 40, 30, eRealm.Midgard,
            RaidStaffBonuses,
            TypeDamage: (int)eDamageType.Crush,
            Hand: 1,
            CanDropAsLoot: true,
            Description: "Petrified frostwood capped in silver from the Winter border, channeling devastating frost-thaumaturgy.");

        public static readonly Piece TuscaranFocus = new(
            "council_warden_raid_tuscaran_focus",
            "Glacial Focus-Orb",
            51, 494, eObjectType.Magical, eInventorySlot.LeftHandWeapon, 0, 0, 4, eRealm.Midgard,
            RaidFocusBonuses,
            Hand: 2,
            ProcSpellId: KineticDeflectionProcSpellId,
            CanDropAsLoot: true,
            Description: "Formed from permafrost and etched runestones deep in Tuscaran Glacier, deflecting kinetic force upon impact.");

        public static readonly Piece GalladoriaStaff = new(
            "council_warden_raid_galladoria_staff",
            "Amber Branch of the Sun-Warder",
            51, 48, eObjectType.Staff, eInventorySlot.TwoHandWeapon, 165, 40, 30, eRealm.Hibernia,
            RaidStaffBonuses,
            TypeDamage: (int)eDamageType.Crush,
            Hand: 1,
            CanDropAsLoot: true,
            Description: "Living amber and silver-grafted ironwood of the Summer Court, pulsing with Hibernian ley energy.");

        public static readonly Piece GalladoriaFocus = new(
            "council_warden_raid_galladoria_focus",
            "Amber Branch of the Summer Court",
            51, 494, eObjectType.Magical, eInventorySlot.LeftHandWeapon, 0, 0, 4, eRealm.Hibernia,
            RaidFocusBonuses,
            Hand: 2,
            ProcSpellId: KineticDeflectionProcSpellId,
            CanDropAsLoot: true,
            Description: "Carved from enchanted living amber within Galladoria, storing and releasing kinetic warding pulses.");

        public static readonly Piece LegionStaff = new(
            "council_warden_raid_legion_staff",
            "Legion's Severing Stave",
            51, 48, eObjectType.Staff, eInventorySlot.TwoHandWeapon, 165, 40, 30, eRealm.None,
            RaidLegionStaffBonuses,
            TypeDamage: (int)eDamageType.Crush,
            Hand: 1,
            SpellId: SeveringBoltChargeSpellId,
            Charges: 10,
            CanDropAsLoot: true,
            Description: "Torn from the demonic vortex beneath Darkness Falls, unleashing bolts that sever supernatural life energy.");

        public static readonly Piece LegionFocus = new(
            "council_warden_raid_legion_focus",
            "Legion's Abyssal Focus-Core",
            51, 494, eObjectType.Magical, eInventorySlot.LeftHandWeapon, 0, 0, 4, eRealm.None,
            RaidFocusBonuses,
            Hand: 2,
            ProcSpellId: KineticDeflectionProcSpellId,
            CanDropAsLoot: true,
            Description: "Salvaged from the demonic core of Legion, projecting impervious kinetic deflection wards.");

        public static readonly Piece[] RaidDrops =
        {
            LoricamIncorruptus,
            SeveranceOfCamlannSlash,
            SeveranceOfCamlannThrust,
            MantleOfTheWinterGate,
            Rimelance,
            CuirassOfTheSummerSolstice,
            SunshardBlades,
            SunshardPiercing,
            SidiStaff,
            SidiFocus,
            TuscaranStaff,
            TuscaranFocus,
            GalladoriaStaff,
            GalladoriaFocus,
            LegionStaff,
            LegionFocus,
        };

        public static IEnumerable<Piece> AllPieces =>
            ChapterMilestonePieces
                .Concat(AlbionEpicSet)
                .Concat(MidgardEpicSet)
                .Concat(HiberniaEpicSet)
                .Concat(RaidDrops);

        public static Piece[] GetEpicSetForRealm(eRealm realm) => realm switch
        {
            eRealm.Albion => AlbionEpicSet,
            eRealm.Midgard => MidgardEpicSet,
            eRealm.Hibernia => HiberniaEpicSet,
            _ => AlbionEpicSet,
        };

        public static long SellPrice() =>
            Math.Max(1, (long)Math.Ceiling(100.0 / Math.Max(1, ServerProperties.Properties.ITEM_SELL_RATIO)));

        public static DbItemTemplate BuildTemplate(Piece piece, DbItemTemplate template = null)
        {
            template ??= new DbItemTemplate();
            template.Id_nb = piece.IdNb;
            template.Name = piece.Name;
            template.Level = piece.Level;
            template.Quality = 100;
            template.Condition = template.MaxCondition = 50000;
            template.Durability = template.MaxDurability = 50000;
            template.Model = piece.Model;
            template.Object_Type = (int)piece.ObjectType;
            template.Item_Type = (int)piece.Slot;
            template.DPS_AF = piece.DpsAf;
            template.SPD_ABS = piece.SpdAbs;
            template.Type_Damage = piece.TypeDamage;
            template.Hand = piece.Hand;
            template.Effect = 0;
            template.Weight = piece.Weight;
            template.Realm = (int)piece.Realm;
            template.AllowedClasses = AllowedWardenClasses;
            template.Bonus = piece.Level >= 45 ? 35 : (piece.Level >= 30 ? 25 : 15);
            template.Color = 0;
            template.Emblem = 0;
            template.Price = SellPrice();
            template.IsPickable = true;
            template.IsDropable = true;
            template.IsTradable = false;
            template.CanDropAsLoot = piece.CanDropAsLoot;
            template.MaxCount = 1;
            template.PackSize = 1;
            template.ProcSpellID = piece.ProcSpellId;
            template.ProcChance = piece.ProcSpellId > 0
                ? (piece.Slot == eInventorySlot.RightHandWeapon ? (byte)20 : (byte)15)
                : (byte)0;
            template.SpellID = piece.SpellId;
            template.Charges = piece.Charges;
            template.MaxCharges = piece.Charges;
            template.PackageID = "CouncilWardenEpic";
            template.Description = piece.Description;

            var slots = new int[10];
            var types = new int[10];
            for (int i = 0; i < piece.Bonuses.Length && i < 10; i++)
            {
                slots[i] = piece.Bonuses[i].Value;
                types[i] = (int)piece.Bonuses[i].Property;
            }

            template.Bonus1 = slots[0]; template.Bonus1Type = types[0];
            template.Bonus2 = slots[1]; template.Bonus2Type = types[1];
            template.Bonus3 = slots[2]; template.Bonus3Type = types[2];
            template.Bonus4 = slots[3]; template.Bonus4Type = types[3];
            template.Bonus5 = slots[4]; template.Bonus5Type = types[4];
            template.Bonus6 = slots[5]; template.Bonus6Type = types[5];
            template.Bonus7 = slots[6]; template.Bonus7Type = types[6];
            template.Bonus8 = slots[7]; template.Bonus8Type = types[7];
            template.Bonus9 = slots[8]; template.Bonus9Type = types[8];
            template.Bonus10 = slots[9]; template.Bonus10Type = types[9];
            return template;
        }

        public static void EnsureTemplates()
        {
            if (GameServer.Database == null)
                return;

            foreach (Piece piece in AllPieces)
            {
                DbItemTemplate existing = GameServer.Database.FindObjectByKey<DbItemTemplate>(piece.IdNb);
                DbItemTemplate template = BuildTemplate(piece, existing);
                if (existing == null)
                    GameServer.Database.AddObject(template);
                else
                    GameServer.Database.SaveObject(template);
            }
        }

        public static bool GivePiece(GamePlayer player, Piece piece, GameNPC source = null)
        {
            if (player?.Inventory == null || piece == null)
                return false;

            DbItemTemplate template = GameServer.Database?.FindObjectByKey<DbItemTemplate>(piece.IdNb)
                ?? BuildTemplate(piece);

            if (player.Inventory.AddTemplate(GameInventoryItem.Create(template), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
            {
                if (source != null)
                    InventoryLogging.LogInventoryAction(source, player, eInventoryActionType.Quest, template, 1);
                player.Out?.SendMessage($"You receive {piece.Name}.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return true;
            }

            player.Out?.SendMessage($"Your backpack is full! Clear space and ask your Council Warden trainer to [reclaim the set].", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            return false;
        }

        public static List<Piece> MissingEpicPieces(GamePlayer player)
        {
            if (player?.Inventory == null)
                return new List<Piece>();

            var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DbInventoryItem item in player.Inventory.AllItems)
            {
                if (!string.IsNullOrEmpty(item?.Id_nb))
                    owned.Add(item.Id_nb);
            }

            List<Piece> expected = new(GetEpicSetForRealm(player.Realm));
            if (!owned.Contains(Chapter5GreyCloak.IdNb))
                expected.Add(Chapter5GreyCloak);

            return expected.Where(p => !owned.Contains(p.IdNb)).ToList();
        }

        public static (int Given, int Left) GiveMissingEpicSet(GamePlayer player, GameNPC source)
        {
            int given = 0, left = 0;
            foreach (Piece piece in MissingEpicPieces(player))
            {
                DbItemTemplate template = GameServer.Database?.FindObjectByKey<DbItemTemplate>(piece.IdNb)
                    ?? BuildTemplate(piece);

                if (player.Inventory.AddTemplate(GameInventoryItem.Create(template), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                {
                    given++;
                    if (source != null)
                        InventoryLogging.LogInventoryAction(source, player, eInventoryActionType.Quest, template, 1);
                }
                else
                {
                    left++;
                }
            }

            return (given, left);
        }

        public static void GrantEpicSet(GamePlayer player, GameNPC source, bool reclaim)
        {
            if (player == null)
                return;

            (int given, int left) = GiveMissingEpicSet(player, source);
            string head = reclaim
                ? "The Senior Council restores your panoply."
                : "By the authority of the Senior Council in Edinburgh, you are consecrated a full Warden of the White Council!";
            if (given > 0)
            {
                player.Out?.SendMessage($"{head} You receive {given} piece{(given == 1 ? string.Empty : "s")} of the Council Warden's Epic panoply.",
                    eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            if (left > 0)
            {
                player.Out?.SendMessage($"Your bags are full: {left} piece{(left == 1 ? " was" : "s were")} held back. Make room and ask your trainer to [reclaim the set].",
                    eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            if (given == 0 && left == 0 && reclaim)
            {
                player.Out?.SendMessage("You already carry every piece of the Council Warden Epic set and Silver Blades.",
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
    }
}
