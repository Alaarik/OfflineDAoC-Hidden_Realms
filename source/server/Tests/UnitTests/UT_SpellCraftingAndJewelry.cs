using System;
using System.Collections;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture, NonParallelizable]
    public class UT_SpellCraftingAndJewelry
    {
        private PetTestLanguageScope _language;
        private SpellCrafting _spellCrafting;

        [SetUp]
        public void SetUp()
        {
            _language = new PetTestLanguageScope();
            _spellCrafting = new SpellCrafting();
        }

        [TearDown]
        public void TearDown()
        {
            _language?.Dispose();
        }

        [Test]
        public void IsJewelry_RecognizesAllJewelrySlots()
        {
            // Object_Type 41 = Magical
            Assert.IsTrue(GlobalConstants.IsJewelry((int)eObjectType.Magical, (int)eInventorySlot.Jewelry)); // Slot 24
            Assert.IsTrue(GlobalConstants.IsJewelry((int)eObjectType.Magical, (int)eInventorySlot.Neck));    // Slot 29
            Assert.IsTrue(GlobalConstants.IsJewelry((int)eObjectType.Magical, (int)eInventorySlot.Waist));   // Slot 32
            Assert.IsTrue(GlobalConstants.IsJewelry((int)eObjectType.Magical, (int)eInventorySlot.LeftBracer)); // Slot 33
            Assert.IsTrue(GlobalConstants.IsJewelry((int)eObjectType.Magical, (int)eInventorySlot.RightBracer)); // Slot 34
            Assert.IsTrue(GlobalConstants.IsJewelry((int)eObjectType.Magical, (int)eInventorySlot.LeftRing)); // Slot 35
            Assert.IsTrue(GlobalConstants.IsJewelry((int)eObjectType.Magical, (int)eInventorySlot.RightRing)); // Slot 36
            Assert.IsTrue(GlobalConstants.IsJewelry((int)eObjectType.Magical, (int)eInventorySlot.LeftHandWeapon)); // Slot 11 (Off-hand Focus)

            // Non-jewelry slots or non-magical items
            Assert.IsFalse(GlobalConstants.IsJewelry((int)eObjectType.Magical, (int)eInventorySlot.HeadArmor));
            Assert.IsFalse(GlobalConstants.IsJewelry((int)eObjectType.Plate, (int)eInventorySlot.TorsoArmor));
            Assert.IsFalse(GlobalConstants.IsJewelry((int)eObjectType.Sword, (int)eInventorySlot.RightHandWeapon));
        }

        [Test]
        public void IsToABonus_IdentifiesCorrectProperties()
        {
            // Stat caps
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.StrCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.DexCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.ConCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.QuiCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.IntCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.PieCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.EmpCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.ChaCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.AcuCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.MaxHealthCapBonus));

            // Resist caps
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.BodyResCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.ColdResCapBonus));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.SlashResCapBonus));

            // Utility %
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.SpellRange));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.CastingSpeed));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.SpellDamage));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.SpellDuration));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.MeleeDamage));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.MeleeSpeed));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.StyleDamage));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.HealingEffectiveness));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.BuffEffectiveness));
            Assert.IsTrue(SpellCrafting.IsToABonus((int)eProperty.PowerPool));

            // Core bonuses (should NOT be ToA)
            Assert.IsFalse(SpellCrafting.IsToABonus((int)eProperty.Strength));
            Assert.IsFalse(SpellCrafting.IsToABonus((int)eProperty.Constitution));
            Assert.IsFalse(SpellCrafting.IsToABonus((int)eProperty.MaxHealth));
            Assert.IsFalse(SpellCrafting.IsToABonus((int)eProperty.MaxMana));
            Assert.IsFalse(SpellCrafting.IsToABonus((int)eProperty.Resist_Crush));
            Assert.IsFalse(SpellCrafting.IsToABonus((int)eProperty.Resist_Heat));
            Assert.IsFalse(SpellCrafting.IsToABonus((int)eProperty.Skill_Blades));
        }

        [Test]
        public void GetGemImbuePoints_CoreFormulasPreserved()
        {
            // Classic stats: ((value - 1) * 2 / 3) + 1
            Assert.AreEqual(1, _spellCrafting.GetGemImbuePoints((int)eProperty.Strength, 1));
            Assert.AreEqual(9, _spellCrafting.GetGemImbuePoints((int)eProperty.Strength, 13));
            Assert.AreEqual(17, _spellCrafting.GetGemImbuePoints((int)eProperty.Strength, 26));

            // Classic resists: (value * 2) - 2
            Assert.AreEqual(2, _spellCrafting.GetGemImbuePoints((int)eProperty.Resist_Slash, 2));
            Assert.AreEqual(8, _spellCrafting.GetGemImbuePoints((int)eProperty.Resist_Slash, 5));
            Assert.AreEqual(14, _spellCrafting.GetGemImbuePoints((int)eProperty.Resist_Slash, 8));

            // Classic HP: value / 4
            Assert.AreEqual(10, _spellCrafting.GetGemImbuePoints((int)eProperty.MaxHealth, 40));
            Assert.AreEqual(19, _spellCrafting.GetGemImbuePoints((int)eProperty.MaxHealth, 76));

            // Classic Mana: (value * 2) - 2
            Assert.AreEqual(18, _spellCrafting.GetGemImbuePoints((int)eProperty.MaxMana, 10));
        }

        [Test]
        public void GetGemImbuePoints_ToAFormulasCorrect()
        {
            // Stat Caps: value * 2
            Assert.AreEqual(2, _spellCrafting.GetGemImbuePoints((int)eProperty.StrCapBonus, 1));
            Assert.AreEqual(4, _spellCrafting.GetGemImbuePoints((int)eProperty.StrCapBonus, 2));
            Assert.AreEqual(6, _spellCrafting.GetGemImbuePoints((int)eProperty.StrCapBonus, 3));
            Assert.AreEqual(8, _spellCrafting.GetGemImbuePoints((int)eProperty.StrCapBonus, 4));
            Assert.AreEqual(10, _spellCrafting.GetGemImbuePoints((int)eProperty.StrCapBonus, 5));

            // Resist Caps: value * 2
            Assert.AreEqual(2, _spellCrafting.GetGemImbuePoints((int)eProperty.HeatResCapBonus, 1));
            Assert.AreEqual(6, _spellCrafting.GetGemImbuePoints((int)eProperty.HeatResCapBonus, 3));
            Assert.AreEqual(10, _spellCrafting.GetGemImbuePoints((int)eProperty.HeatResCapBonus, 5));

            // HP Cap: value / 5
            Assert.AreEqual(2, _spellCrafting.GetGemImbuePoints((int)eProperty.MaxHealthCapBonus, 10));
            Assert.AreEqual(6, _spellCrafting.GetGemImbuePoints((int)eProperty.MaxHealthCapBonus, 30));
            Assert.AreEqual(10, _spellCrafting.GetGemImbuePoints((int)eProperty.MaxHealthCapBonus, 50));

            // Percentages: value (2..10)
            Assert.AreEqual(2, _spellCrafting.GetGemImbuePoints((int)eProperty.SpellRange, 2));
            Assert.AreEqual(6, _spellCrafting.GetGemImbuePoints((int)eProperty.SpellRange, 6));
            Assert.AreEqual(10, _spellCrafting.GetGemImbuePoints((int)eProperty.SpellRange, 10));

            Assert.AreEqual(2, _spellCrafting.GetGemImbuePoints((int)eProperty.CastingSpeed, 2));
            Assert.AreEqual(10, _spellCrafting.GetGemImbuePoints((int)eProperty.CastingSpeed, 10));

            Assert.AreEqual(2, _spellCrafting.GetGemImbuePoints((int)eProperty.SpellDamage, 2));
            Assert.AreEqual(10, _spellCrafting.GetGemImbuePoints((int)eProperty.SpellDamage, 10));

            Assert.AreEqual(2, _spellCrafting.GetGemImbuePoints((int)eProperty.SpellDuration, 2));
            Assert.AreEqual(10, _spellCrafting.GetGemImbuePoints((int)eProperty.SpellDuration, 10));

            Assert.AreEqual(2, _spellCrafting.GetGemImbuePoints((int)eProperty.MeleeSpeed, 2));
            Assert.AreEqual(10, _spellCrafting.GetGemImbuePoints((int)eProperty.MeleeSpeed, 10));

            Assert.AreEqual(2, _spellCrafting.GetGemImbuePoints((int)eProperty.MeleeDamage, 2));
            Assert.AreEqual(10, _spellCrafting.GetGemImbuePoints((int)eProperty.MeleeDamage, 10));
        }

        [Test]
        public void ImbueCapacities_Level51Masterpiece()
        {
            var item = new DbInventoryItem
            {
                Level = 51,
                Quality = 100
            };

            int coreMax = _spellCrafting.GetItemMaxImbuePoints(item);
            int toaMax = _spellCrafting.GetItemMaxToAImbuePoints(item);

            Assert.AreEqual(32, coreMax, "Level 51 MP core imbue capacity should be 32 points");
            Assert.AreEqual(16, toaMax, "Level 51 MP ToA imbue capacity should be 16 points");
        }

        [Test]
        public void ImbueCapacities_Level51Quality99()
        {
            var item = new DbInventoryItem
            {
                Level = 51,
                Quality = 99
            };

            int coreMax = _spellCrafting.GetItemMaxImbuePoints(item);
            int toaMax = _spellCrafting.GetItemMaxToAImbuePoints(item);

            Assert.AreEqual(28, coreMax, "Level 51 99% core imbue capacity should be 28 points");
            Assert.AreEqual(14, toaMax, "Level 51 99% ToA imbue capacity should be 14 points");
        }

        [Test]
        public void ToAOverchargeScenarios_16Budget()
        {
            // Scenario 1: 1 Top Tier (T5=10) + 1 Mid Tier (T3=6) = 16 pts
            int safeCombo = _spellCrafting.GetGemImbuePoints((int)eProperty.SpellDamage, 10) +
                            _spellCrafting.GetGemImbuePoints((int)eProperty.SpellRange, 6);
            Assert.AreEqual(16, safeCombo);
            Assert.IsTrue(safeCombo <= 16, "T5 + T3 should fit safely within 16-point cap");

            // Scenario 2: 2 High Tiers (T4=8 each) = 16 pts
            int highCombo = _spellCrafting.GetGemImbuePoints((int)eProperty.SpellDamage, 8) +
                            _spellCrafting.GetGemImbuePoints((int)eProperty.CastingSpeed, 8);
            Assert.AreEqual(16, highCombo);
            Assert.IsTrue(highCombo <= 16, "T4 + T4 should fit safely within 16-point cap");

            // Scenario 3: 1 Top Tier (T5=10) + 1 High Tier (T4=8) = 18 pts (+2 Overcharge)
            int oc2Combo = _spellCrafting.GetGemImbuePoints((int)eProperty.SpellDamage, 10) +
                           _spellCrafting.GetGemImbuePoints((int)eProperty.CastingSpeed, 8);
            Assert.AreEqual(18, oc2Combo);
            Assert.AreEqual(2, oc2Combo - 16, "T5 + T4 should be exactly +2 overcharge");

            // Scenario 4: 2 Top Tiers (T5=10 each) = 20 pts (+4 Overcharge)
            int oc4Combo = _spellCrafting.GetGemImbuePoints((int)eProperty.SpellDamage, 10) +
                           _spellCrafting.GetGemImbuePoints((int)eProperty.CastingSpeed, 10);
            Assert.AreEqual(20, oc4Combo);
            Assert.AreEqual(4, oc4Combo - 16, "T5 + T5 should be exactly +4 overcharge (Double Max gamble)");
        }

        [Test]
        public void OptionB_GracePeriod_OverchargeLeq2_Guarantees100PercentPreservation()
        {
            var item = new DbInventoryItem { Level = 51, Quality = 100 };
            int maxCore = 32;
            int maxToA = 16;

            // +1 overcharge total -> 100% preservation (0% destroy chance)
            int preserve1 = _spellCrafting.CalculateChanceToPreserveItem(null, item, maxCore, 33, maxToA, 16);
            Assert.AreEqual(100, preserve1, "+1 overcharge must have 100% preservation under Option B grace period");

            // +2 overcharge total -> 100% preservation (0% destroy chance)
            int preserve2 = _spellCrafting.CalculateChanceToPreserveItem(null, item, maxCore, 32, maxToA, 18);
            Assert.AreEqual(100, preserve2, "+2 overcharge must have 100% preservation under Option B grace period");

            // +1 core and +1 ToA (total +2) -> 100% preservation
            int preserveCombo2 = _spellCrafting.CalculateChanceToPreserveItem(null, item, maxCore, 33, maxToA, 17);
            Assert.AreEqual(100, preserveCombo2, "+1 core + +1 ToA (total +2) must be 100% preserved");
        }

        [Test]
        public void LeyLineSanctum_DetectsWildernessAndEdinburghLocations()
        {
            // 1. Stonehenge (Salisbury Plains, Region 1)
            Assert.IsTrue(LeyLineSanctum.IsInSanctum(1, 585762, 585248, out string s1));
            Assert.AreEqual("Stonehenge", s1);

            // 2. Far away in Salisbury Plains (outside 4500 radius)
            Assert.IsFalse(LeyLineSanctum.IsInSanctum(1, 500000, 500000, out _));

            // 3. Hagall Runestones (Midgard, Region 100)
            Assert.IsTrue(LeyLineSanctum.IsInSanctum(100, 704904, 737619, out string s2));
            Assert.AreEqual("Hagall Runestones", s2);

            // 4. Lough Gur (Hibernia, Region 200)
            Assert.IsTrue(LeyLineSanctum.IsInSanctum(200, 334818, 719908, out string s3));
            Assert.AreEqual("Cromlech of Lough Gur", s3);

            // 5. Edinburgh Catacombs (Region 65, Whole Region)
            Assert.IsTrue(LeyLineSanctum.IsInSanctum(65, 12345, 67890, out string s4));
            Assert.AreEqual("Catacombs of Edinburgh", s4);

            // 6. Edinburgh Grand Conclave (Region 192, Whole Region)
            Assert.IsTrue(LeyLineSanctum.IsInSanctum(192, 12345, 67890, out string s5));
            Assert.AreEqual("Grand Conclave of Edinburgh", s5);

            // 7. Camelot City (Region 10) - Intentionally not a wilderness sanctum
            Assert.IsFalse(LeyLineSanctum.IsInSanctum(10, 35000, 30000, out _));
        }

        [Test]
        public void JewelrySlotHelpers_CorrectlyIdentifySlots()
        {
            var ring = new DbInventoryItem { Object_Type = 41, Item_Type = (int)eInventorySlot.LeftRing };
            var bracer = new DbInventoryItem { Object_Type = 41, Item_Type = (int)eInventorySlot.RightBracer };
            var belt = new DbInventoryItem { Object_Type = 41, Item_Type = (int)eInventorySlot.Waist };
            var focus = new DbInventoryItem { Object_Type = 41, Item_Type = (int)eInventorySlot.LeftHandWeapon };
            var sword = new DbInventoryItem { Object_Type = (int)eObjectType.Sword, Item_Type = (int)eInventorySlot.RightHandWeapon };

            Assert.IsTrue(GlobalConstants.IsRing(ring));
            Assert.IsFalse(GlobalConstants.IsRing(bracer));

            Assert.IsTrue(GlobalConstants.IsBracer(bracer));
            Assert.IsFalse(GlobalConstants.IsBracer(ring));

            Assert.IsTrue(GlobalConstants.IsBelt(belt));
            Assert.IsFalse(GlobalConstants.IsBelt(ring));

            Assert.IsTrue(GlobalConstants.IsFocus(focus));
            Assert.IsFalse(GlobalConstants.IsFocus(sword));

            Assert.IsTrue(GlobalConstants.IsWeapon(sword));
            Assert.IsFalse(GlobalConstants.IsWeapon(ring));
        }

        [Test]
        public void OptionA_And_OptionD_StackingPreservation()
        {
            var item = new DbInventoryItem { Level = 51, Quality = 100 };
            int maxCore = 32;
            int maxToA = 16;
            // Overcharge of +3 (exceeds Grace Period <= 2)
            int coreImbue = 35;
            int toaImbue = 16;

            int baseline = _spellCrafting.CalculateChanceToPreserveItem(null, item, maxCore, coreImbue, maxToA, toaImbue, forceDraught: false, forceSanctum: false);
            int withDraught = _spellCrafting.CalculateChanceToPreserveItem(null, item, maxCore, coreImbue, maxToA, toaImbue, forceDraught: true, forceSanctum: false);
            int inSanctum = _spellCrafting.CalculateChanceToPreserveItem(null, item, maxCore, coreImbue, maxToA, toaImbue, forceDraught: false, forceSanctum: true);
            int withBoth = _spellCrafting.CalculateChanceToPreserveItem(null, item, maxCore, coreImbue, maxToA, toaImbue, forceDraught: true, forceSanctum: true);

            Assert.AreEqual(50, baseline, "Baseline preservation with 1000 skill and no modifiers should be 50%");
            Assert.AreEqual(85, withDraught, "Draught of Greater Stability should add +35% preservation (total 85%)");
            Assert.AreEqual(85, inSanctum, "Ley-Line Sanctum should add +35% preservation (total 85%)");
            Assert.AreEqual(100, withBoth, "Sanctum and Draught stack (+35% + +35% = +70%, capped at 100%)");
        }

        [Test]
        public void Sanctum_IncreasesOverchargeSuccessChance()
        {
            var item96 = new DbInventoryItem { Level = 51, Quality = 96 };
            int maxCore = 18;
            int maxToA = 8;

            // +5 overcharge on 96% item (diff = 5)
            int coreImbue = 23;
            int toaImbue = 8;

            int normalSuccess = _spellCrafting.CalculateChanceToOverchargeItem(null, item96, maxCore, coreImbue, maxToA, toaImbue, forceSanctum: false);
            int sanctumSuccess = _spellCrafting.CalculateChanceToOverchargeItem(null, item96, maxCore, coreImbue, maxToA, toaImbue, forceSanctum: true);

            Assert.AreEqual(70, normalSuccess, "Normal overcharge success at +5 on 96% item should be 70%");
            Assert.AreEqual(85, sanctumSuccess, "Sanctum overcharge success should receive +15% boost (70% -> 85%)");

            // Overcharge > 5 is hard capped at 0% regardless
            int impossibleNormal = _spellCrafting.CalculateChanceToOverchargeItem(null, item96, maxCore, 25, maxToA, toaImbue, forceSanctum: false);
            int impossibleSanctum = _spellCrafting.CalculateChanceToOverchargeItem(null, item96, maxCore, 25, maxToA, toaImbue, forceSanctum: true);
            Assert.AreEqual(0, impossibleNormal);
            Assert.AreEqual(0, impossibleSanctum);
        }
    }
}
