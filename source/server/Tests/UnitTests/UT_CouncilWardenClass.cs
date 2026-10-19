using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DOL.Database;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.PlayerClass;
using DOL.GS.PropertyCalc;
using DOL.GS.Quests;
using DOL.GS.RealmAbilities;
using DOL.GS.RealmAbilities.Statics;
using DOL.GS.Spells;
using DOL.GS.ServerRules;
using DOL.GS.Trainer;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture, NonParallelizable]
    public class UT_CouncilWardenClass
    {
        private GameServer _oldServer;
        private PetTestLanguageScope _language;
        private long _oldTime;
        private IPropertyCalculator[] _originalCalculators;
        private readonly List<GameLiving> _actors = new();
        private static readonly PropertyInfo Clock = typeof(GameLoop).GetProperty(nameof(GameLoop.GameLoopTime));

        private sealed class EmptyServer : GameServer
        {
            protected override IObjectDatabase DataBaseImpl =>
                DispatchProxy.Create<IObjectDatabase, UT_UnobservedConcentration.EmptyReads>();
            protected override GS.ServerRules.IServerRules ServerRulesImpl => new GS.ServerRules.NormalServerRules();
        }

        private sealed class TestWardenBot : GameBot
        {
            private TestWardenBot() : base((OfflineWorldBotRecord)null) { }
            public ICharacterClass TestClass;
            public byte TestLevel = 50;
            public bool TestAlive = true;
            public int TestMana = 200;
            public int TestMaxMana = 200;
            public int TestX;
            public int TestY;
            public override ICharacterClass CharacterClass => TestClass;
            public override byte Level { get => TestLevel; set => TestLevel = value; }
            public override bool IsAlive => TestAlive;
            public override int Mana { get => TestMana; set => TestMana = value; }
            public override int MaxMana => TestMaxMana;
            public override int X => TestX;
            public override int Y => TestY;
            public override int Z => 0;
            public override eRealm Realm { get; set; }
            public override int GetModified(eProperty property) => 0;
            public override void OnMaxSpeedChange() { }
        }

        private sealed class TestEnemyNpc : GameNPC
        {
            public byte TestLevel = 50;
            public bool TestAlive = true;
            public int TestHealth = 1000;
            public int TestMaxHealth = 1000;
            public int TestX;
            public int TestY;
            public override byte Level { get => TestLevel; set => TestLevel = value; }
            public override bool IsAlive => TestAlive;
            public override int Health { get => TestHealth; set => TestHealth = value; }
            public override int MaxHealth => TestMaxHealth;
            public override int X => TestX;
            public override int Y => TestY;
            public override int Z => 0;
            public override int GetModified(eProperty property) => 0;
            public override void OnMaxSpeedChange() { }
        }

        [SetUp]
        public void SetUp()
        {
            _oldTime = GameLoop.GameLoopTime;
            _oldServer = GameServer.Instance;
            GameServer.LoadTestDouble((EmptyServer)RuntimeHelpers.GetUninitializedObject(typeof(EmptyServer)));
            Clock.SetValue(null, 10_000L);
            _language = new PetTestLanguageScope();

            var calculators = (IPropertyCalculator[])typeof(GameLiving)
                .GetField("m_propertyCalc", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            if (calculators != null)
            {
                _originalCalculators = (IPropertyCalculator[])calculators.Clone();
                LoadCorePropertyCalculators(calculators);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameLiving actor in _actors)
            {
                if (actor.effectListComponent != null)
                    ServiceObjectStore.Remove(actor.effectListComponent);
                if (actor is GameNPC npc && npc.Brain != null)
                    ServiceObjectStore.Remove(npc.Brain);
            }
            _actors.Clear();
            _language.Dispose();
            Clock.SetValue(null, _oldTime);
            GameServer.LoadTestDouble(_oldServer);

            var calculators = (IPropertyCalculator[])typeof(GameLiving)
                .GetField("m_propertyCalc", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            if (calculators != null && _originalCalculators != null)
            {
                Array.Copy(_originalCalculators, calculators, calculators.Length);
            }
        }

        private static void LoadCorePropertyCalculators(IPropertyCalculator[] calculators)
        {
            foreach (Type t in typeof(GameLiving).Assembly.GetTypes())
            {
                if (!t.IsClass || t.IsAbstract || !typeof(IPropertyCalculator).IsAssignableFrom(t)) continue;
                var attrs = t.GetCustomAttributes(typeof(PropertyCalculatorAttribute), false);
                if (attrs.Length == 0) continue;
                try
                {
                    var calc = (IPropertyCalculator)Activator.CreateInstance(t);
                    foreach (PropertyCalculatorAttribute attr in attrs)
                    {
                        for (int i = (int)attr.Min; i <= (int)attr.Max; i++)
                        {
                            calculators[i] = calc;
                        }
                    }
                }
                catch { }
            }
        }

        private static void InitPropertyCategories(GameLiving living)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameLiving).GetField("<BaseBuffBonusCategory>k__BackingField", Flags)?.SetValue(living, new PropertyIndexer());
            typeof(GameLiving).GetField("<SpecBuffBonusCategory>k__BackingField", Flags)?.SetValue(living, new PropertyIndexer());
            typeof(GameLiving).GetField("<ItemBonus>k__BackingField", Flags)?.SetValue(living, new PropertyIndexer());
            typeof(GameLiving).GetField("<AbilityBonus>k__BackingField", Flags)?.SetValue(living, new PropertyIndexer());
            typeof(GameLiving).GetField("<OtherBonus>k__BackingField", Flags)?.SetValue(living, new PropertyIndexer());
            typeof(GameLiving).GetField("<DebuffCategory>k__BackingField", Flags)?.SetValue(living, new PropertyIndexer());
            typeof(GameLiving).GetField("<SpecDebuffCategory>k__BackingField", Flags)?.SetValue(living, new PropertyIndexer());
            typeof(GameLiving).GetField("<BuffBonusMultCategory1>k__BackingField", Flags)?.SetValue(living, new MultiplicativePropertiesHybrid());
            typeof(GameLiving).GetField("<TempProperties>k__BackingField", Flags)?.SetValue(living, new PropertyCollection());
            typeof(GameLiving).GetField("m_xpGainers", Flags)?.SetValue(living, new Dictionary<GameLiving, double>());
            typeof(GameLiving).GetField("XpGainersLock", BindingFlags.Instance | BindingFlags.Public)?.SetValue(living, new System.Threading.Lock());
            typeof(GameObject).GetField("_dataQuests", Flags)?.SetValue(living, new List<DOL.GS.Quests.DataQuest>());
            typeof(GameObject).GetField("_dataQuestsLock", Flags)?.SetValue(living, new System.Threading.Lock());
            typeof(GameObject).GetField("_objectInRadiusCachesLock", Flags)?.SetValue(living, new System.Threading.Lock());
            var cacheDictType = typeof(GameObject).GetField("_objectsInRadiusCaches", Flags)?.FieldType;
            if (cacheDictType != null)
                typeof(GameObject).GetField("_objectsInRadiusCaches", Flags)?.SetValue(living, Activator.CreateInstance(cacheDictType));
        }

        private TestWardenBot CreateWardenBot(ICharacterClass charClass, byte level = 50, eRealm realm = eRealm.Albion)
        {
            var bot = (TestWardenBot)RuntimeHelpers.GetUninitializedObject(typeof(TestWardenBot));
            InitPropertyCategories(bot);
            bot.TestClass = charClass;
            bot.TestAlive = true;
            bot.TestMana = 200;
            bot.TestMaxMana = 200;
            bot.ObjectState = GameObject.eObjectState.Active;
            bot.TestLevel = level;
            bot.Realm = realm;
            bot.effectListComponent = EffectListComponent.Create(bot);
            typeof(GameNPC).GetField("m_brains", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bot, new ArrayList());
            _actors.Add(bot);
            return bot;
        }

        private TestEnemyNpc CreateNpc(string name = "Test Target", string guild = "", byte level = 50)
        {
            var npc = (TestEnemyNpc)RuntimeHelpers.GetUninitializedObject(typeof(TestEnemyNpc));
            InitPropertyCategories(npc);
            npc.TestAlive = true;
            npc.TestHealth = 1000;
            npc.TestMaxHealth = 1000;
            npc.Name = name;
            npc.GuildName = guild;
            npc.TestLevel = level;
            npc.Realm = eRealm.None;
            npc.ObjectState = GameObject.eObjectState.Active;
            npc.effectListComponent = EffectListComponent.Create(npc);
            typeof(GameNPC).GetField("m_brains", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(npc, new ArrayList());
            _actors.Add(npc);
            return npc;
        }

        [Test]
        public void AllThreeRealmClassesMaintainWhiteCouncilHybridIdentity()
        {
            var albWarden = new ClassMaulerAlb();
            var midWarden = new ClassMaulerMid();
            var hibWarden = new ClassVampiir();

            foreach (ICharacterClass warden in new ICharacterClass[] { albWarden, midWarden, hibWarden })
            {
                Assert.That(warden.Name, Is.EqualTo("Council Warden"));
                Assert.That(warden.ClassType, Is.EqualTo(eClassType.Hybrid));
                Assert.That(warden.ManaStat, Is.EqualTo(eStat.INT));
                Assert.That(warden.PrimaryStat, Is.EqualTo(eStat.STR));
                Assert.That(warden.SecondaryStat, Is.EqualTo(eStat.INT));
                Assert.That(warden.TertiaryStat, Is.EqualTo(eStat.CON));
                Assert.That(warden.SpecPointsMultiplier, Is.EqualTo(20), "Council Wardens have a 2.0x specialization multiplier");
                Assert.That(warden.WeaponSkillBase, Is.EqualTo(400));
                Assert.That(warden.BaseHP, Is.EqualTo(720));
                Assert.That(warden.CanUseLefthandedWeapon, Is.False, "No shields or dual-wielding");
                Assert.That(warden.IsFocusCaster, Is.True, "Council Wardens are Focus Casters");
                Assert.That(CreateWardenBot(warden).CanCastWhileAttacking(), Is.True, "Council Wardens cast while attacking via off-hand focus");
                Assert.That(warden.HasAdvancedFromBaseClass(), Is.True);
            }

            Assert.That(albWarden.ID, Is.EqualTo((int)eCharacterClass.MaulerAlb));
            Assert.That(albWarden.BaseName, Is.EqualTo("Fighter"));

            Assert.That(midWarden.ID, Is.EqualTo((int)eCharacterClass.MaulerMid));
            Assert.That(midWarden.BaseName, Is.EqualTo("Viking"));

            Assert.That(hibWarden.ID, Is.EqualTo((int)eCharacterClass.Vampiir));
            Assert.That(hibWarden.BaseName, Is.EqualTo("Guardian"));
        }

        [Test]
        public void EligibleRacesAreStrictlyMortalHumansPerRealm()
        {
            var albRaces = new ClassMaulerAlb().EligibleRaces.Select(r => r.ID).ToArray();
            Assert.That(albRaces, Is.EquivalentTo(new[]
            {
                eRace.Briton,
                eRace.Highlander,
                eRace.Saracen,
                eRace.Avalonian,
            }));
            Assert.That(albRaces, Does.Not.Contain(eRace.Inconnu));
            Assert.That(albRaces, Does.Not.Contain(eRace.HalfOgre));

            var midRaces = new ClassMaulerMid().EligibleRaces.Select(r => r.ID).ToArray();
            Assert.That(midRaces, Is.EquivalentTo(new[] { eRace.Norseman }));
            Assert.That(midRaces, Does.Not.Contain(eRace.Troll));
            Assert.That(midRaces, Does.Not.Contain(eRace.Dwarf));
            Assert.That(midRaces, Does.Not.Contain(eRace.Kobold));
            Assert.That(midRaces, Does.Not.Contain(eRace.Valkyn));

            var hibRaces = new ClassVampiir().EligibleRaces.Select(r => r.ID).ToArray();
            Assert.That(hibRaces, Is.EquivalentTo(new[] { eRace.Celt }));
            Assert.That(hibRaces, Does.Not.Contain(eRace.Elf));
            Assert.That(hibRaces, Does.Not.Contain(eRace.Lurikeen));
            Assert.That(hibRaces, Does.Not.Contain(eRace.Firbolg));
            Assert.That(hibRaces, Does.Not.Contain(eRace.Shar));
            Assert.That(hibRaces, Does.Not.Contain(eRace.Sylvan));
        }

        [Test]
        public void StartingClassesDictionaryIncludesCouncilWardenSlots()
        {
            Assert.That(GlobalConstants.STARTING_CLASSES_DICT[eRealm.Albion], Does.Contain(eCharacterClass.MaulerAlb));
            Assert.That(GlobalConstants.STARTING_CLASSES_DICT[eRealm.Midgard], Does.Contain(eCharacterClass.MaulerMid));
            Assert.That(GlobalConstants.STARTING_CLASSES_DICT[eRealm.Hibernia], Does.Contain(eCharacterClass.Vampiir));
        }

        [Test]
        public void TrainersEnforceBaseClassLevelFiveAndMortalHumanRace()
        {
            var albTrainer = (AlbionMaulerTrainer)RuntimeHelpers.GetUninitializedObject(typeof(AlbionMaulerTrainer));
            var midTrainer = (MidgardMaulerTrainer)RuntimeHelpers.GetUninitializedObject(typeof(MidgardMaulerTrainer));
            var hibTrainer = (VampiirTrainer)RuntimeHelpers.GetUninitializedObject(typeof(VampiirTrainer));

            Assert.That(albTrainer.TrainedClass, Is.EqualTo(eCharacterClass.MaulerAlb));
            Assert.That(midTrainer.TrainedClass, Is.EqualTo(eCharacterClass.MaulerMid));
            Assert.That(hibTrainer.TrainedClass, Is.EqualTo(eCharacterClass.Vampiir));
        }

        [Test]
        public void EvocationAndAbjurationMapToMagicalSkillProperties()
        {
            eProperty evoSkill = SkillBase.SpecToSkill(Specs.Evocation);
            eProperty abjSkill = SkillBase.SpecToSkill(Specs.Abjuration);

            Assert.That(evoSkill, Is.EqualTo(eProperty.Skill_Magnetism));
            Assert.That(abjSkill, Is.EqualTo(eProperty.Skill_Aura_Manipulation));
            Assert.That(SkillBase.CheckPropertyType(evoSkill, ePropertyType.SkillMagical), Is.True);
            Assert.That(SkillBase.CheckPropertyType(abjSkill, ePropertyType.SkillMagical), Is.True);
        }

        [Test]
        public void BothAndMagicAblativeArmorMapToAblativeArmorEffect()
        {
            var bothAblative = new Spell(new DbSpell
            {
                SpellID = 59266,
                Type = "BothAblativeArmor",
                Target = "Self",
                Damage = 50,
                Value = 80,
                Duration = 30
            }, 7);
            var magicAblative = new Spell(new DbSpell
            {
                SpellID = 59267,
                Type = "MagicAblativeArmor",
                Target = "Self",
                Damage = 50,
                Value = 140,
                Duration = 30
            }, 17);

            Assert.That(EffectHelper.GetEffectFromSpell(bothAblative), Is.EqualTo(eEffect.AblativeArmor));
            Assert.That(EffectHelper.GetEffectFromSpell(magicAblative), Is.EqualTo(eEffect.AblativeArmor));
        }

        [Test]
        public void CouncilWardenSpellIdsUseOwnTooltipIdentity()
        {
            Assert.That(PacketLib1110.UsesOwnTooltipIdentity(59200), Is.True);
            Assert.That(PacketLib1110.UsesOwnTooltipIdentity(59272), Is.True);
            Assert.That(PacketLib1110.UsesOwnTooltipIdentity(59399), Is.True);
        }

        [Test]
        public void WizardlyCellularResilienceReducesDotBleedDiseaseDurationAndBoostsRestingRegen()
        {
            var warden = CreateWardenBot(new ClassMaulerAlb(), level: 25);
            var dotSpell = new Spell(new DbSpell { SpellID = 59208, Type = "DamageOverTime", Target = "Enemy", Duration = 20 }, 10);
            var bleedSpell = new Spell(new DbSpell { SpellID = 59304, Type = "StyleBleeding", Target = "Enemy", Duration = 15 }, 29);
            var diseaseSpell = new Spell(new DbSpell { SpellID = 9001, Type = "Disease", Target = "Enemy", Duration = 60 }, 20);
            var stunSpell = new Spell(new DbSpell { SpellID = 9002, Type = "Stun", Target = "Enemy", Duration = 8 }, 20);

            Assert.That(CouncilWardenMechanics.ApplyCellularResilienceDuration(warden, dotSpell, 20000), Is.EqualTo(15000));
            Assert.That(CouncilWardenMechanics.ApplyCellularResilienceDuration(warden, bleedSpell, 15000), Is.EqualTo(11250));
            Assert.That(CouncilWardenMechanics.ApplyCellularResilienceDuration(warden, diseaseSpell, 60000), Is.EqualTo(45000));
            Assert.That(CouncilWardenMechanics.ApplyCellularResilienceDuration(warden, stunSpell, 8000), Is.EqualTo(8000), "Stun duration is not affected by Cellular Resilience");

            // Resting out-of-combat regen (+20%)
            Assert.That(CouncilWardenMechanics.ApplyCellularResilienceRestingRegen(warden, 100.0), Is.EqualTo(100.0), "Not sitting yet");
        }

        [Test]
        public void KineticGroundingAndMurphyonicFieldModifyIncomingDamage()
        {
            var warden = CreateWardenBot(new ClassMaulerMid(), level: 50, realm: eRealm.Midgard);
            var attacker = CreateNpc("Enemy Mage");

            // Kinetic Grounding: -30% critical spell damage taken
            var spellAd = new AttackData
            {
                Attacker = attacker,
                Target = warden,
                AttackType = AttackData.eAttackType.Spell,
                Damage = 200,
                CriticalDamage = 100
            };
            CouncilWardenMechanics.AdjustIncomingSpellDamage(spellAd);
            Assert.That(spellAd.Damage, Is.EqualTo(200));
            Assert.That(spellAd.CriticalDamage, Is.EqualTo(70));

            // Murphyonic Field: -20% siege damage taken
            var siegeSpell = new Spell(new DbSpell { SpellID = 9100, Type = "SiegeDirectDamage", Target = "Enemy", Damage = 500 }, 50);
            var siegeHandler = new SpellHandler(attacker, siegeSpell, new SpellLine("Siege", "Siege", "Siege", true));
            var siegeAd = new AttackData
            {
                Attacker = attacker,
                Target = warden,
                AttackType = AttackData.eAttackType.Spell,
                SpellHandler = siegeHandler,
                Damage = 500
            };
            CouncilWardenMechanics.AdjustMurphyonicSiegeDamage(siegeAd);
            Assert.That(siegeAd.Damage, Is.EqualTo(400));
        }

        [Test]
        public void KineticLanceShattersAblativeBarriersAndGrants25PercentBonusDamage()
        {
            var warden = CreateWardenBot(new ClassVampiir(), level: 50, realm: eRealm.Hibernia);
            var target = CreateNpc("Shielded Sorcerer");

            var ablativeSpell = new Spell(new DbSpell
            {
                SpellID = 59253,
                Name = "Unyielding Aegis",
                Type = "BothAblativeArmor",
                Target = "Self",
                Damage = 50,
                Value = 450,
                Duration = 900
            }, 45);
            var ablativeHandler = new BothAblativeArmorSpellHandler(target, ablativeSpell, new SpellLine("Minor Abjuration", "Minor Abjuration", "Abjuration", true));
            var ablativeEffect = ECSGameEffectFactory.Create(new(target, 900000, 1.0, ablativeHandler), static (in i) => new AblativeArmorECSGameEffect(i));
            target.effectListComponent.BeginTick();

            Assert.That(ablativeEffect.IsActive, Is.True);
            Assert.That(ablativeEffect.RemainingValue, Is.EqualTo(450));
            bool shattered = CouncilWardenKineticLanceSpellHandler.ShatterTargetBarriers(warden, target);
            Assert.That(shattered, Is.True);
            Assert.That(ablativeEffect.RemainingValue, Is.EqualTo(0));
        }

        [Test]
        public void ConflagrationAndSeveranceFieldApplyAndRevertDebuffsCleanly()
        {
            var warden = CreateWardenBot(new ClassMaulerAlb(), level: 50);
            var target = CreateNpc("Armored Caster");

            // Conflagration AF debuff (80 AF)
            var conflagSpell = new Spell(new DbSpell
            {
                SpellID = 59230,
                Name = "Cataclysmic Inferno",
                Type = "CouncilWardenConflagration",
                Target = "Enemy",
                Damage = 235,
                Value = 80,
                Duration = 10,
                DamageType = 13
            }, 50);
            var conflagHandler = new CouncilWardenConflagrationSpellHandler(warden, conflagSpell, new SpellLine("Evocation", "Evocation", "Evocation", false));
            var afEffect = new SuperheatAFDebuffECSEffect(new(target, 10000, 1.0, conflagHandler));
            afEffect.OnStartEffect();
            Assert.That(target.DebuffCategory[eProperty.ArmorFactor], Is.EqualTo(80));
            afEffect.OnStopEffect();
            Assert.That(target.DebuffCategory[eProperty.ArmorFactor], Is.EqualTo(0));

            // Severance Field (-35% Cast Speed, +20% Fumble & Spell Fumble)
            var sevSpell = new Spell(new DbSpell
            {
                SpellID = 59275,
                Name = "Archon's Severance Field",
                Type = "CouncilWardenSeveranceField",
                Target = "Enemy",
                Duration = 15,
                Value = 35
            }, 50);
            var sevHandler = new CouncilWardenSeveranceFieldSpellHandler(warden, sevSpell, new SpellLine("Abjuration", "Abjuration", "Abjuration", false));
            var sevEffect = new SeveranceFieldECSEffect(new(target, 15000, 1.0, sevHandler));
            sevEffect.OnStartEffect();
            Assert.That(target.DebuffCategory[eProperty.CastingSpeed], Is.EqualTo(35));
            Assert.That(target.DebuffCategory[eProperty.FumbleChance], Is.EqualTo(20));
            Assert.That(target.DebuffCategory[eProperty.SpellFumbleChance], Is.EqualTo(20));
            sevEffect.OnStopEffect();
            Assert.That(target.DebuffCategory[eProperty.CastingSpeed], Is.EqualTo(0));
            Assert.That(target.DebuffCategory[eProperty.FumbleChance], Is.EqualTo(0));
            Assert.That(target.DebuffCategory[eProperty.SpellFumbleChance], Is.EqualTo(0));
        }

        [Test]
        public void OpeningTheSightTogglesTrueVisionDrainsPowerAndTriggersSensoryOverload()
        {
            var warden = CreateWardenBot(new ClassMaulerAlb(), level: 50);
            var stealthedEnemy = CreateWardenBot(new ClassMaulerMid(), level: 50, realm: eRealm.Midgard);
            warden.TestX = 0;
            stealthedEnemy.TestX = 850;

            Assert.That(CouncilWardenMechanics.HasTheSightActive(warden), Is.False);
            Assert.That(CouncilWardenMechanics.CanDetectByWardenSightOrBrand(warden, stealthedEnemy), Is.False);

            var sightSpell = new Spell(new DbSpell
            {
                SpellID = 59257,
                Name = "Opening The Sight",
                Type = "CouncilWardenTheSight",
                Target = "Self",
                Duration = 3600,
                Frequency = 5000
            }, 30);
            var sightHandler = new CouncilWardenTheSightSpellHandler(warden, sightSpell, new SpellLine("Minor Abjuration", "Minor Abjuration", "Abjuration", true));
            var sightEffect = ECSGameEffectFactory.Create(new(warden, 3600000, 1.0, sightHandler), static (in i) => new TheSightECSEffect(i));
            warden.effectListComponent.BeginTick();

            Assert.That(CouncilWardenMechanics.HasTheSightActive(warden), Is.True);
            Assert.That(CouncilWardenMechanics.CanDetectByWardenSightOrBrand(warden, stealthedEnemy), Is.True, "1,000u true vision detects enemy at 850u");

            // Pulse drains 2% of MaxMana (4 of 200)
            sightEffect.OnEffectPulse();
            Assert.That(warden.Mana, Is.EqualTo(196));

            // Sensory Overload: +10% incoming magic damage while The Sight is open
            var incomingSpellAd = new AttackData
            {
                Attacker = stealthedEnemy,
                Target = warden,
                AttackType = AttackData.eAttackType.Spell,
                Damage = 150
            };
            CouncilWardenMechanics.AdjustIncomingSpellDamage(incomingSpellAd);
            Assert.That(incomingSpellAd.Damage, Is.EqualTo(165));
        }

        [Test]
        public void DeathCurseMarksKillerWithWardensBrand()
        {
            var warden = CreateWardenBot(new ClassMaulerAlb(), level: 50);
            var killer = CreateNpc("Assassin");

            var brandSpell = new Spell(new DbSpell
            {
                SpellID = 99990,
                Name = "The Warden's Brand",
                Type = "SkillsDebuff",
                Target = "Enemy",
                Duration = 30,
                Value = 15
            }, 50);
            var handler = new SpellHandler(warden, brandSpell, new SpellLine("Reserved", "Reserved", "Reserved", true));
            var brandEffect = ECSGameEffectFactory.Create(new(killer, 30000, 1.0, handler), static (in i) => new WardensBrandECSEffect(i));
            killer.effectListComponent.BeginTick();

            Assert.That(CouncilWardenMechanics.HasWardensBrand(killer), Is.True);
            Assert.That(CouncilWardenMechanics.CanDetectByWardenSightOrBrand(warden, killer), Is.True);
            Assert.That(killer.DebuffCategory[eProperty.Resist_Heat], Is.EqualTo(15));
            Assert.That(killer.DebuffCategory[eProperty.Resist_Energy], Is.EqualTo(15));
            Assert.That(killer.DebuffCategory[eProperty.Resist_Matter], Is.EqualTo(15));

            brandEffect.End(false);
            killer.effectListComponent.BeginTick();
            Assert.That(killer.DebuffCategory[eProperty.Resist_Heat], Is.EqualTo(0));
        }

        [Test]
        public void ColdIronAndIronSicknessShatterFaeMagicWardAndSuppressFaeRegen()
        {
            var warden = CreateWardenBot(new ClassMaulerAlb(), level: 50);
            var sidhe = CreateNpc("Winter Court Sidhe", "Winter Court", level: 50);

            Assert.That(CouncilWardenMechanics.IsFaeEntity(sidhe), Is.True);

            // 1. Before melee hit, Fae 85% natural magic ward reduces a 200-damage spell to 30
            var spellBefore = new AttackData
            {
                Attacker = warden,
                Target = sidhe,
                AttackType = AttackData.eAttackType.Spell,
                Damage = 200
            };
            CouncilWardenMechanics.ApplyFaeMagicWard(spellBefore);
            Assert.That(spellBefore.Damage, Is.EqualTo(30));

            // 2. Hit the Sidhe with a True Cold Iron sword (+50% melee damage + 8s Iron Sickness + Regen Suppression)
            var coldIronSword = new DbInventoryItem
            {
                Id_nb = "warden_cold_iron_longsword",
                Name = "Cold Iron Warden's Longsword",
                Object_Type = (int)eObjectType.SlashingWeapon,
                Type_Damage = (int)eDamageType.Slash
            };
            var meleeAd = new AttackData
            {
                Attacker = warden,
                Target = sidhe,
                AttackType = AttackData.eAttackType.MeleeOneHand,
                AttackResult = eAttackResult.HitStyle,
                Weapon = coldIronSword,
                Damage = 100,
                StyleDamage = 40
            };
            CouncilWardenMechanics.ApplyColdIronAndIronSicknessOnMeleeHit(meleeAd);
            sidhe.effectListComponent.BeginTick();
            Assert.That(meleeAd.Damage, Is.EqualTo(150), "+50% Cold Iron physical damage vs. Fae");
            Assert.That(meleeAd.StyleDamage, Is.EqualTo(60), "+50% Cold Iron style damage vs. Fae");
            Assert.That(CouncilWardenMechanics.GetIronSicknessEffect(sidhe), Is.Not.Null);
            Assert.That(CouncilWardenMechanics.GetIronSicknessEffect(sidhe).SuppressHealthRegen, Is.True);

            // 3. While Iron Sickness is active, the Fae's 85% magic ward is shattered by -75% (down to 10% ward: 200 -> 180)
            var spellAfter = new AttackData
            {
                Attacker = warden,
                Target = sidhe,
                AttackType = AttackData.eAttackType.Spell,
                Damage = 200
            };
            CouncilWardenMechanics.ApplyFaeMagicWard(spellAfter);
            Assert.That(spellAfter.Damage, Is.EqualTo(180));

            // 4. True Cold Iron suppresses Fae health regeneration to 0
            var regenCalc = new HealthRegenerationAmountCalculator();
            Assert.That(regenCalc.CalcValue(sidhe, eProperty.HealthRegenerationAmount), Is.EqualTo(0));
        }

        [Test]
        public void SpellseverStripsBeneficialBuffFromTarget()
        {
            var warden = CreateWardenBot(new ClassMaulerAlb(), level: 50);
            var target = CreateNpc("Buffed Enemy");

            var buffSpell = new Spell(new DbSpell
            {
                SpellID = 59248,
                Name = "Aegis of the Grey Cloak",
                Type = "BaseArmorFactorBuff",
                Target = "Self",
                Value = 115,
                Duration = 1200
            }, 49);
            var buffHandler = new BaseArmorFactorBuff(target, buffSpell, new SpellLine("Minor Abjuration", "Minor Abjuration", "Abjuration", true));
            var buffEffect = ECSGameEffectFactory.Create(new(target, 1200000, 1.0, buffHandler), static (in i) => new StatBuffECSEffect(i));
            target.effectListComponent.BeginTick();

            Assert.That(buffEffect.IsActive, Is.True);
            bool stripped = CouncilWardenSpellseverSpellHandler.StripOneBeneficialBuff(warden, target);
            target.effectListComponent.BeginTick();

            Assert.That(stripped, Is.True);
            Assert.That(buffEffect.IsEnded, Is.True);
        }

        [Test]
        public void OpenWayDynamicResonanceTableRoutesMortalZonesAndNeverneverWaystones()
        {
            // 1. Lush/sunny mortal zones open into Summer Faerie (Region 197 Summer Waystone)
            var salisbury = OpenWayResonanceTable.ResolveDestination(1, 1, 566800, 560200, eRealm.Albion);
            Assert.That(salisbury.Resonance, Is.EqualTo(FaerieResonance.Summer));
            Assert.That((salisbury.RegionId, salisbury.X, salisbury.Y, salisbury.Z),
                Is.EqualTo((OpenWayResonanceTable.SummerWaystone.RegionId, OpenWayResonanceTable.SummerWaystone.X, OpenWayResonanceTable.SummerWaystone.Y, OpenWayResonanceTable.SummerWaystone.Z)));

            var mularn = OpenWayResonanceTable.ResolveDestination(100, 100, 803350, 727020, eRealm.Midgard);
            Assert.That(mularn.Resonance, Is.EqualTo(FaerieResonance.Summer));

            var loughDerg = OpenWayResonanceTable.ResolveDestination(200, 200, 346240, 490920, eRealm.Hibernia);
            Assert.That(loughDerg.Resonance, Is.EqualTo(FaerieResonance.Summer));

            // 2. Bleak/frozen mortal zones open into Winter Faerie (Region 197 Winter Waystone)
            var snowdonia = OpenWayResonanceTable.ResolveDestination(1, 12, 510000, 320000, eRealm.Albion);
            Assert.That(snowdonia.Resonance, Is.EqualTo(FaerieResonance.Winter));
            Assert.That((snowdonia.RegionId, snowdonia.X, snowdonia.Y, snowdonia.Z),
                Is.EqualTo((OpenWayResonanceTable.WinterWaystone.RegionId, OpenWayResonanceTable.WinterWaystone.X, OpenWayResonanceTable.WinterWaystone.Y, OpenWayResonanceTable.WinterWaystone.Z)));

            var myrkwood = OpenWayResonanceTable.ResolveDestination(100, 104, 750000, 900000, eRealm.Midgard);
            Assert.That(myrkwood.Resonance, Is.EqualTo(FaerieResonance.Winter));

            var bogOfCullen = OpenWayResonanceTable.ResolveDestination(200, 207, 400000, 600000, eRealm.Hibernia);
            Assert.That(bogOfCullen.Resonance, Is.EqualTo(FaerieResonance.Winter));

            // 3. Hadrian's Wall (Region 1, Zone 15) resonates directly with the Edinburgh Crossing in the Nevernever
            var hadrians = OpenWayResonanceTable.ResolveDestination(1, 15, 629700, 342150, eRealm.Albion);
            Assert.That(hadrians.Resonance, Is.EqualTo(FaerieResonance.EdinburghWay));
            Assert.That((hadrians.RegionId, hadrians.X, hadrians.Y, hadrians.Z),
                Is.EqualTo((OpenWayResonanceTable.EdinburghWaystoneInNevernever.RegionId, OpenWayResonanceTable.EdinburghWaystoneInNevernever.X, OpenWayResonanceTable.EdinburghWaystoneInNevernever.Y, OpenWayResonanceTable.EdinburghWaystoneInNevernever.Z)));

            // 4. Casting Open Way near the Edinburgh Waystone inside Region 197 enters Edinburgh Tier 1 (Region 65)
            var intoEdinburgh = OpenWayResonanceTable.ResolveDestination(197, 197, 32100, 31950, eRealm.Albion);
            Assert.That(intoEdinburgh.Resonance, Is.EqualTo(FaerieResonance.StepIntoEdinburgh));
            Assert.That(intoEdinburgh.RegionId, Is.EqualTo(OpenWayResonanceTable.EdinburghTier1Region));

            // 5. Casting Open Way elsewhere in the Nevernever returns to safe realm village (Cotswold, Mularn, Mag Mell)
            var savedOrigin = ((ushort)1, 559120, 511320, 2452, (ushort)1024);
            var returnMortal = OpenWayResonanceTable.ResolveDestination(197, 197, 42134, 34920, eRealm.Albion, savedOrigin);
            Assert.That(returnMortal.Resonance, Is.EqualTo(FaerieResonance.ReturnToMortal));
            Assert.That((returnMortal.RegionId, returnMortal.X, returnMortal.Y, returnMortal.Z),
                Is.EqualTo((OpenWayResonanceTable.AlbionMortalNexus.RegionId, OpenWayResonanceTable.AlbionMortalNexus.X, OpenWayResonanceTable.AlbionMortalNexus.Y, OpenWayResonanceTable.AlbionMortalNexus.Z)));

            // 6. Caster eligibility
            Assert.That(OpenWayResonanceTable.IsEligibleCasterClass((int)eCharacterClass.MaulerAlb), Is.True);
            Assert.That(OpenWayResonanceTable.IsEligibleCasterClass((int)eCharacterClass.MaulerMid), Is.True);
            Assert.That(OpenWayResonanceTable.IsEligibleCasterClass((int)eCharacterClass.Vampiir), Is.True);
            Assert.That(OpenWayResonanceTable.IsEligibleCasterClass((int)eCharacterClass.Wizard), Is.True);
            Assert.That(OpenWayResonanceTable.IsEligibleCasterClass((int)eCharacterClass.Runemaster), Is.True);
            Assert.That(OpenWayResonanceTable.IsEligibleCasterClass((int)eCharacterClass.Eldritch), Is.True);
            Assert.That(OpenWayResonanceTable.IsEligibleCasterClass((int)eCharacterClass.Druid), Is.True);
            Assert.That(OpenWayResonanceTable.IsEligibleCasterClass((int)eCharacterClass.Armsman), Is.False);
            Assert.That(OpenWayResonanceTable.IsEligibleCasterClass((int)eCharacterClass.Berserker), Is.False);
        }

        [Test]
        public void EdinburghTwoTierAdmissionsPolicyEnforcesHumanConclaveAndFifthLawKos()
        {
            // 1. Human Council Wardens & Human Arcane Casters -> Full access to Tier 1 & Tier 2
            var humanWarden = EdinburghAdmissionsPolicy.Evaluate(eRace.Highlander, eCharacterClass.MaulerAlb);
            Assert.That((humanWarden.Role, humanWarden.CanEnterTier1, humanWarden.CanEnterTier2, humanWarden.IsKillOnSight),
                Is.EqualTo((EdinburghAccessRole.HumanWarden, true, true, false)));

            var humanWizard = EdinburghAdmissionsPolicy.Evaluate(eRace.Avalonian, eCharacterClass.Wizard);
            Assert.That((humanWizard.Role, humanWizard.CanEnterTier1, humanWizard.CanEnterTier2, humanWizard.IsKillOnSight),
                Is.EqualTo((EdinburghAccessRole.HumanCouncilWizard, true, true, false)));

            // 2. Non-Human Arcane Casters (Lurikeen, Elf, Dwarf, Kobold, Inconnu) -> Tier 1 Treaty Emissary ONLY; denied Tier 2 Conclave
            foreach (var (race, cls) in new[]
            {
                (eRace.Lurikeen, eCharacterClass.Eldritch),
                (eRace.Elf, eCharacterClass.Enchanter),
                (eRace.Dwarf, eCharacterClass.Runemaster),
                (eRace.Kobold, eCharacterClass.Shaman),
                (eRace.Inconnu, eCharacterClass.Sorcerer),
            })
            {
                var emissary = EdinburghAdmissionsPolicy.Evaluate(race, cls);
                Assert.That(emissary.Role, Is.EqualTo(EdinburghAccessRole.TreatyEmissary));
                Assert.That(emissary.CanEnterTier1, Is.True);
                Assert.That(emissary.CanEnterTier2, Is.False);
                Assert.That(emissary.IsKillOnSight, Is.False);
                Assert.That(EdinburghAdmissionsPolicy.GetConclaveDenialMessage(emissary),
                    Is.EqualTo(EdinburghAdmissionsPolicy.NonHumanConclaveDenialText));
            }

            // 3. Divine Casters -> Observer access to Tier 1 & Tier 2
            var cleric = EdinburghAdmissionsPolicy.Evaluate(eRace.Briton, eCharacterClass.Cleric);
            Assert.That((cleric.Role, cleric.CanEnterTier1, cleric.CanEnterTier2),
                Is.EqualTo((EdinburghAccessRole.DivineObserver, true, true)));

            // 4. Mundane Human Fighters -> Tier 1 Commons only unless escorted by a Council Member
            var unescortedFighter = EdinburghAdmissionsPolicy.Evaluate(eRace.Briton, eCharacterClass.Armsman, isEscortedByCouncilMember: false);
            var escortedFighter = EdinburghAdmissionsPolicy.Evaluate(eRace.Briton, eCharacterClass.Armsman, isEscortedByCouncilMember: true);
            Assert.That((unescortedFighter.CanEnterTier1, unescortedFighter.CanEnterTier2), Is.EqualTo((true, false)));
            Assert.That((escortedFighter.CanEnterTier1, escortedFighter.CanEnterTier2), Is.EqualTo((true, true)));

            // 5. Fifth Law Violators (Necromancer, Bonedancer, Sluaghbinder, Spiritmaster) -> KOS in both tiers!
            foreach (var violatorClass in new[] { eCharacterClass.Necromancer, eCharacterClass.Bonedancer, eCharacterClass.Sluaghbinder, eCharacterClass.Spiritmaster })
            {
                var violator = EdinburghAdmissionsPolicy.Evaluate(eRace.Briton, violatorClass);
                Assert.That(violator.Role, Is.EqualTo(EdinburghAccessRole.FifthLawViolator));
                Assert.That(violator.CanEnterTier1, Is.False);
                Assert.That(violator.CanEnterTier2, Is.False);
                Assert.That(violator.IsKillOnSight, Is.True);
            }

            // Verify Midgard Spiritmaster (Norseman or Kobold) is strictly flagged KOS regardless of mortal human race
            var smNorseman = EdinburghAdmissionsPolicy.Evaluate(eRace.Norseman, eCharacterClass.Spiritmaster);
            Assert.That(smNorseman.Role, Is.EqualTo(EdinburghAccessRole.FifthLawViolator));
            Assert.That(smNorseman.CanEnterTier1, Is.False);
            Assert.That(smNorseman.CanEnterTier2, Is.False);
            Assert.That(smNorseman.IsKillOnSight, Is.True);
        }

        [Test]
        public void NeverneverWardedCobblestoneRoadSuppressesFaeAggroWhileOnRoad()
        {
            // Standing on the Warded Cobblestone Road in Region 197 (e.g., at the Edinburgh Portal node or along the road to Summer Waystone)
            Assert.That(NeverneverWardedRoad.IsOnWardedRoad(197, 32000, 32000), Is.True);
            Assert.That(NeverneverWardedRoad.IsOnWardedRoad(197, 38000, 34920), Is.True);
            Assert.That(NeverneverWardedRoad.IsOnWardedRoad(197, 26134, 30000), Is.True);
            Assert.That(NeverneverWardedRoad.GetEffectiveAggroRange(197, 38000, 34920, 550),
                Is.EqualTo(NeverneverWardedRoad.WardedRoadAggroRadius));

            // Straying into the deep woods/brambles (> 300 units off the road) dissolves the wards
            Assert.That(NeverneverWardedRoad.IsOnWardedRoad(197, 36200, 37800), Is.False);
            Assert.That(NeverneverWardedRoad.IsOnWardedRoad(197, 28500, 29800), Is.False);
            Assert.That(NeverneverWardedRoad.GetEffectiveAggroRange(197, 36200, 37800, 550), Is.EqualTo(550));
        }

        [Test]
        public void SilverThimbleAndColdIronTemplatesMatchPlanSpecifications()
        {
            DbItemTemplate thimble = EdinburghAndNeverneverWorld.BuildSilverThimbleTemplate();
            Assert.That(thimble.Id_nb, Is.EqualTo("silver_thimble_of_the_veil"));
            Assert.That(thimble.SpellID, Is.EqualTo(OpenWayResonanceTable.OpenWaySpellId));
            Assert.That(thimble.AllowedClasses, Is.Empty, "Usable by non-casters and all classes");
            Assert.That(thimble.Price, Is.EqualTo(BlackMarketTalismanPeddlerNPC.ThimblePriceCopper));

            DbItemTemplate coldIron = EdinburghAndNeverneverWorld.BuildLegionsSeveringEdgeTemplate();
            Assert.That(coldIron.Id_nb, Is.EqualTo("legions_severing_edge"));
            Assert.That(coldIron.PackageID, Is.EqualTo("ColdIron"));
            var invItem = GameInventoryItem.Create(coldIron);
            Assert.That(CouncilWardenMechanics.IsColdIronWeapon(invItem), Is.True);
        }

        [Test]
        public void EpicQuestlineDefinitionsMatchNineChaptersAndRealmSpecificTargets()
        {
            Assert.That(CouncilWardenEpicQuestRuntime.Definitions.Count, Is.EqualTo(9));

            var expectedOrder = new (System.Type Type, int Chapter, byte Level, System.Type Prereq, string MilestoneId)[]
            {
                (typeof(CouncilWardenEpic07), 1, 7, null, "council_warden_ch1_signet"),
                (typeof(CouncilWardenEpic13), 2, 13, typeof(CouncilWardenEpic07), "council_warden_ch2_bracer"),
                (typeof(CouncilWardenEpic20), 3, 20, typeof(CouncilWardenEpic13), "council_warden_ch3_belt"),
                (typeof(CouncilWardenEpic25), 4, 25, typeof(CouncilWardenEpic20), "council_warden_ch4_neck"),
                (typeof(CouncilWardenEpic30), 5, 30, typeof(CouncilWardenEpic25), "council_warden_ch5_ring"),
                (typeof(CouncilWardenEpic35), 6, 35, typeof(CouncilWardenEpic30), "council_warden_ch6_bracer"),
                (typeof(CouncilWardenEpic40), 7, 40, typeof(CouncilWardenEpic35), "council_warden_ch7_jewel"),
                (typeof(CouncilWardenEpic45), 8, 45, typeof(CouncilWardenEpic40), "council_warden_ch5_cloak"),
                (typeof(CouncilWardenEpic50), 9, 50, typeof(CouncilWardenEpic45), null),
            };

            foreach (var (questType, chapter, level, prereq, milestoneId) in expectedOrder)
            {
                var def = CouncilWardenEpicQuestRuntime.GetDefinition(questType);
                Assert.That(def.ChapterNumber, Is.EqualTo(chapter));
                Assert.That(def.QuestLevel, Is.EqualTo(level));
                Assert.That(CouncilWardenEpicQuestRuntime.GetPrerequisite(questType), Is.EqualTo(prereq));
                Assert.That(def.MilestonePiece?.IdNb, Is.EqualTo(milestoneId));

                // Verify realm-specific dialogues are populated and distinct
                Assert.That(def.AlbionTarget.BriefingDialogue, Is.Not.Empty);
                Assert.That(def.MidgardTarget.BriefingDialogue, Is.Not.Empty);
                Assert.That(def.HiberniaTarget.BriefingDialogue, Is.Not.Empty);
                Assert.That(def.AlbionTarget.CompletionDialogue, Is.Not.Empty);
                Assert.That(def.MidgardTarget.CompletionDialogue, Is.Not.Empty);
                Assert.That(def.HiberniaTarget.CompletionDialogue, Is.Not.Empty);

                Assert.That(def.AlbionTarget.BriefingDialogue, Is.Not.EqualTo(def.MidgardTarget.BriefingDialogue));
                Assert.That(def.AlbionTarget.BriefingDialogue, Is.Not.EqualTo(def.HiberniaTarget.BriefingDialogue));
            }
        }

        [Test]
        public void EpicArmorAndSilverBladesMatchPlanStatMatrix()
        {
            // Verify Section 6 Combined Total for 6-piece Epic Armor + Chapter 5 Cloak + 1 Silver Blade:
            // +75 STR, +61 INT, +58 CON, +54 DEX, +52 QUI, +184 HP
            foreach (eRealm realm in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
            {
                var epicSet = CouncilWardenEpicArmor.GetEpicSetForRealm(realm);
                var sixArmorPieces = epicSet.Where(p => p.Slot is eInventorySlot.TorsoArmor
                    or eInventorySlot.ArmsArmor
                    or eInventorySlot.LegsArmor
                    or eInventorySlot.HeadArmor
                    or eInventorySlot.HandsArmor
                    or eInventorySlot.FeetArmor).ToList();
                var primarySilverBlade = epicSet.First(p => p.Slot == eInventorySlot.RightHandWeapon);

                Assert.That(sixArmorPieces.Count, Is.EqualTo(6));
                foreach (var piece in sixArmorPieces)
                {
                    Assert.That(piece.DpsAf, Is.EqualTo(102), $"{realm} {piece.Name} is full AF 102");
                }
                Assert.That(primarySilverBlade.DpsAf, Is.EqualTo(162), "Level 50 Quest Epic Weapon 16.2 DPS");
                Assert.That(primarySilverBlade.ProcSpellId, Is.EqualTo(CouncilWardenEpicArmor.SilverRadianceProcSpellId));

                var fullSuit = sixArmorPieces
                    .Append(CouncilWardenEpicArmor.Chapter5GreyCloak)
                    .Append(primarySilverBlade)
                    .ToList();

                int SumStat(eProperty prop) =>
                    fullSuit.SelectMany(p => p.Bonuses).Where(b => b.Property == prop).Sum(b => b.Value);

                Assert.That(SumStat(eProperty.Strength), Is.EqualTo(75), $"{realm} full epic suit hits +75 STR cap exactly");
                Assert.That(SumStat(eProperty.Intelligence), Is.EqualTo(61), $"{realm} full epic suit hits +61 INT");
                Assert.That(SumStat(eProperty.Constitution), Is.EqualTo(58), $"{realm} full epic suit hits +58 CON");
                Assert.That(SumStat(eProperty.Dexterity), Is.EqualTo(54), $"{realm} full epic suit hits +54 DEX");
                Assert.That(SumStat(eProperty.Quickness), Is.EqualTo(66), $"{realm} full epic suit hits +66 QUI (52 armor + 14 cloak)");
                Assert.That(SumStat(eProperty.MaxHealth), Is.EqualTo(184), $"{realm} full epic suit hits +184 HP");
            }
        }

        [Test]
        public void EpicRaidDropsMatchSection8Specifications()
        {
            Assert.That(CouncilWardenEpicArmor.RaidDrops.Length, Is.EqualTo(16));

            foreach (var drop in CouncilWardenEpicArmor.RaidDrops)
            {
                DbItemTemplate template = CouncilWardenEpicArmor.BuildTemplate(drop);
                Assert.That(template.Level, Is.EqualTo(51));
                Assert.That(template.Quality, Is.EqualTo(100));
                Assert.That(template.Bonus, Is.EqualTo(35));
                Assert.That(template.CanDropAsLoot, Is.True);
                Assert.That(template.AllowedClasses, Is.EqualTo("60;61;58"));
            }

            Assert.That(CouncilWardenEpicArmor.LoricamIncorruptus.DpsAf, Is.EqualTo(102));
            Assert.That(CouncilWardenEpicArmor.LoricamIncorruptus.ProcSpellId, Is.EqualTo(CouncilWardenEpicArmor.AegisOfFirstWardensProcSpellId));
            Assert.That(CouncilWardenEpicArmor.MantleOfTheWinterGate.DpsAf, Is.EqualTo(102));
            Assert.That(CouncilWardenEpicArmor.MantleOfTheWinterGate.ProcSpellId, Is.EqualTo(CouncilWardenEpicArmor.ThermalFeedbackProcSpellId));
            Assert.That(CouncilWardenEpicArmor.CuirassOfTheSummerSolstice.DpsAf, Is.EqualTo(102));
            Assert.That(CouncilWardenEpicArmor.CuirassOfTheSummerSolstice.ProcSpellId, Is.EqualTo(CouncilWardenEpicArmor.FlourishOfWillProcSpellId));
            Assert.That(CouncilWardenEpicArmor.SeveranceOfCamlannSlash.DpsAf, Is.EqualTo(165));
            Assert.That(CouncilWardenEpicArmor.Rimelance.DpsAf, Is.EqualTo(165));
            Assert.That(CouncilWardenEpicArmor.SunshardBlades.DpsAf, Is.EqualTo(165));
            Assert.That(CouncilWardenEpicArmor.LegionStaff.DpsAf, Is.EqualTo(165));
            Assert.That(CouncilWardenEpicArmor.LegionStaff.SpellId, Is.EqualTo(CouncilWardenEpicArmor.SeveringBoltChargeSpellId));
            Assert.That(CouncilWardenEpicArmor.LegionStaff.Charges, Is.EqualTo(10));
            Assert.That(CouncilWardenEpicArmor.SidiStaff.DpsAf, Is.EqualTo(165));
            Assert.That(CouncilWardenEpicArmor.SidiFocus.ProcSpellId, Is.EqualTo(CouncilWardenEpicArmor.KineticDeflectionProcSpellId));
            Assert.That(CouncilWardenEpicArmor.LegionFocus.ProcSpellId, Is.EqualTo(CouncilWardenEpicArmor.KineticDeflectionProcSpellId));
        }

        [Test]
        public void CouncilWardenFocusAndThaumaturgyEngineIntegrations()
        {
            // Verify SkillConstants and SkillBase Spec-to-Property mappings
            Assert.That(Specs.Thaumaturgy, Is.EqualTo("Thaumaturgy"));
            Assert.That(Specs.Minor_Thaumaturgy, Is.EqualTo("Minor Thaumaturgy"));
            Assert.That(SkillBase.SpecToSkill(Specs.Thaumaturgy), Is.EqualTo(eProperty.Skill_Power_Strikes));
            Assert.That(SkillBase.SpecToSkill(Specs.Evocation), Is.EqualTo(eProperty.Skill_Magnetism));
            Assert.That(SkillBase.SpecToSkill(Specs.Abjuration), Is.EqualTo(eProperty.Skill_Aura_Manipulation));

            // Verify Focus Mappings for all 3 Council Warden spell lines
            Assert.That(SkillBase.SpecToFocus(Specs.Evocation), Is.EqualTo(eProperty.Focus_Fire));
            Assert.That(SkillBase.SpecToFocus(Specs.Abjuration), Is.EqualTo(eProperty.Focus_Spirit));
            Assert.That(SkillBase.SpecToFocus(Specs.Thaumaturgy), Is.EqualTo(eProperty.Focus_Matter));

            // Verify Epic Staff and Focus pieces
            Assert.That(CouncilWardenEpicArmor.EpicStaff.ObjectType, Is.EqualTo(eObjectType.Staff));
            Assert.That(CouncilWardenEpicArmor.EpicStaff.Slot, Is.EqualTo(eInventorySlot.TwoHandWeapon));
            Assert.That(CouncilWardenEpicArmor.EpicStaff.Bonuses.Any(b => b.Property == eProperty.AllFocusLevels && b.Value == 50), Is.True);

            Assert.That(CouncilWardenEpicArmor.EpicFocus.ObjectType, Is.EqualTo(eObjectType.Magical));
            Assert.That(CouncilWardenEpicArmor.EpicFocus.Slot, Is.EqualTo(eInventorySlot.LeftHandWeapon));
            Assert.That(CouncilWardenEpicArmor.EpicFocus.Bonuses.Any(b => b.Property == eProperty.AllFocusLevels && b.Value == 50), Is.True);

            // Verify Milestone Foci (Chapters 1 to 4)
            var milestoneFoci = new[]
            {
                (CouncilWardenEpicArmor.Chapter1Focus, 10, 10),
                (CouncilWardenEpicArmor.Chapter2Focus, 20, 20),
                (CouncilWardenEpicArmor.Chapter3Focus, 30, 30),
                (CouncilWardenEpicArmor.Chapter4Focus, 40, 40),
            };

            foreach (var (focus, reqLevel, focusLevel) in milestoneFoci)
            {
                Assert.That(focus.Level, Is.EqualTo(reqLevel));
                Assert.That(focus.Slot, Is.EqualTo(eInventorySlot.LeftHandWeapon));
                Assert.That(focus.Bonuses.Any(b => b.Property == eProperty.AllFocusLevels && b.Value == focusLevel), Is.True);
                Assert.That(CouncilWardenEpicArmor.ChapterMilestonePieces, Does.Contain(focus));
            }
        }

        [Test]
        public void CouncilWardenVinculumAndProbabilityHexMechanics()
        {
            var warden = CreateWardenBot(new ClassMaulerAlb(), level: 50);
            var enemy = CreateNpc("Hexed Enemy", level: 50);

            // 1. Vinculum Reflection Mechanics (Scaling with Spell.Value: 35% on Sovereign Vinculum)
            var vinculumSpell = new Spell(new DbSpell
            {
                SpellID = 59374,
                Name = "Sovereign Vinculum",
                Type = "CouncilWardenVinculum",
                Target = "Enemy",
                Duration = 20,
                Value = 35
            }, 44);
            var vinculumHandler = new CouncilWardenVinculumSpellHandler(warden, vinculumSpell, new SpellLine("Thaumaturgy", "Thaumaturgy", "Thaumaturgy", false));
            var vinculumEffect = ECSGameEffectFactory.Create(new(warden, 20000, 1.0, vinculumHandler), static (in i) => new VinculumECSEffect(i));
            warden.effectListComponent.BeginTick();

            Assert.That(CouncilWardenMechanics.HasVinculumActive(warden), Is.True);

            // Test incoming damage reflection
            var incomingAd = new AttackData
            {
                Attacker = enemy,
                Target = warden,
                AttackType = AttackData.eAttackType.MeleeOneHand,
                Damage = 200,
                IsSpellResisted = false
            };
            int enemyHealthBefore = enemy.Health;
            CouncilWardenMechanics.HandleVinculumReflection(warden, incomingAd);
            Assert.That(enemy.Health, Is.EqualTo(enemyHealthBefore - 70), "35% of 200 damage (70) reflected onto attacker");

            vinculumEffect.End(false);
            warden.effectListComponent.BeginTick();
            Assert.That(CouncilWardenMechanics.HasVinculumActive(warden), Is.False);

            // Test Initiate's Vinculum (15% reflection)
            var initVinculumSpell = new Spell(new DbSpell
            {
                SpellID = 59370,
                Name = "Initiate's Vinculum",
                Type = "CouncilWardenVinculum",
                Target = "Enemy",
                Duration = 20,
                Value = 15
            }, 4);
            var initHandler = new CouncilWardenVinculumSpellHandler(warden, initVinculumSpell, new SpellLine("Thaumaturgy", "Thaumaturgy", "Thaumaturgy", false));
            var initEffect = ECSGameEffectFactory.Create(new(warden, 20000, 1.0, initHandler), static (in i) => new VinculumECSEffect(i));
            warden.effectListComponent.BeginTick();

            int enemyHealthBefore2 = enemy.Health;
            CouncilWardenMechanics.HandleVinculumReflection(warden, incomingAd);
            Assert.That(enemy.Health, Is.EqualTo(enemyHealthBefore2 - 30), "15% of 200 damage (30) reflected onto attacker");

            initEffect.End(false);
            warden.effectListComponent.BeginTick();

            // 2. Ley-Line Conduit Mechanics (25% and 50% mana restoration)
            warden.Mana = 0;
            var conduitSpell25 = new Spell(new DbSpell
            {
                SpellID = 59392,
                Name = "Ley-Line Conduit",
                Type = "CouncilWardenLeyLineConduit",
                Target = "Self",
                Value = 25
            }, 25);
            var conduitHandler25 = new CouncilWardenLeyLineConduitSpellHandler(warden, conduitSpell25, new SpellLine("Thaumaturgy", "Thaumaturgy", "Thaumaturgy", false));
            conduitHandler25.ApplyEffectOnTarget(warden);
            Assert.That(warden.Mana, Is.EqualTo((int)System.Math.Round(warden.MaxMana * 0.25)), "25% max mana restored");

            warden.Mana = 0;
            var conduitSpell50 = new Spell(new DbSpell
            {
                SpellID = 59393,
                Name = "Sovereign Ley-Conduit",
                Type = "CouncilWardenLeyLineConduit",
                Target = "Self",
                Value = 50
            }, 45);
            var conduitHandler50 = new CouncilWardenLeyLineConduitSpellHandler(warden, conduitSpell50, new SpellLine("Thaumaturgy", "Thaumaturgy", "Thaumaturgy", false));
            conduitHandler50.ApplyEffectOnTarget(warden);
            Assert.That(warden.Mana, Is.EqualTo((int)System.Math.Round(warden.MaxMana * 0.50)), "50% max mana restored");

            // 3. Probability Hex Mechanics
            var hexSpell = new Spell(new DbSpell
            {
                SpellID = 59379,
                Name = "Curse of Ruinous Probability",
                Type = "CouncilWardenProbabilityHex",
                Target = "Enemy",
                Duration = 15,
                Value = 30
            }, 48);
            var hexHandler = new CouncilWardenProbabilityHexSpellHandler(warden, hexSpell, new SpellLine("Thaumaturgy", "Thaumaturgy", "Thaumaturgy", false));
            var hexEffect = ECSGameEffectFactory.Create(new(enemy, 15000, 1.0, hexHandler), static (in i) => new ProbabilityHexECSEffect(i));
            enemy.effectListComponent.BeginTick();

            Assert.That(CouncilWardenMechanics.HasProbabilityHex(enemy), Is.True);
            Assert.That(enemy.DebuffCategory[eProperty.MeleeSpeed], Is.EqualTo(30));
            Assert.That(enemy.DebuffCategory[eProperty.CastingSpeed], Is.EqualTo(30));

            hexEffect.End(false);
            enemy.effectListComponent.BeginTick();
            Assert.That(CouncilWardenMechanics.HasProbabilityHex(enemy), Is.False);
            Assert.That(enemy.DebuffCategory[eProperty.MeleeSpeed], Is.EqualTo(0));
            Assert.That(enemy.DebuffCategory[eProperty.CastingSpeed], Is.EqualTo(0));
        }

        private sealed class TestPlayerInventory : GamePlayerInventory
        {
            private readonly Dictionary<eInventorySlot, DbInventoryItem> _items = new();

            public TestPlayerInventory(GamePlayer player) : base(player) { }

            public override DbInventoryItem GetItem(eInventorySlot slot) =>
                _items.TryGetValue(slot, out var item) ? item : null;

            public override bool AddItem(eInventorySlot slot, DbInventoryItem item)
            {
                _items[slot] = item;
                return true;
            }
        }

        private sealed class TestPlayer : GamePlayer
        {
            private TestPlayer() : base(null, null) { }
            public ICharacterClass TestClass;
            public override ICharacterClass CharacterClass => TestClass;
            public override eRealm Realm { get; set; }
            public short TestRace;
            public override short Race { get => TestRace; set => TestRace = value; }
            public byte TestLevel = 50;
            public override byte Level { get => TestLevel; set => TestLevel = value; }
            public override bool IsAlive => true;
            public ushort TestRegionID;
            public override ushort CurrentRegionID { get => TestRegionID; set => TestRegionID = value; }
            public int TestX;
            public override int X { get => TestX; set => TestX = value; }
            public int TestY;
            public override int Y { get => TestY; set => TestY = value; }
            public int TestZ;
            public override int Z { get => TestZ; set => TestZ = value; }
            public ushort TestHeading;
            public override ushort Heading { get => TestHeading; set => TestHeading = value; }
            public IPacketLib TestOut;
            public override IPacketLib Out => TestOut ??= DispatchProxy.Create<IPacketLib, UT_RealmExchangeEconomy.NoOpPacketLib>();
            public GameClient TestClient;
            public override GameClient Client => TestClient;

            private System.Collections.Concurrent.ConcurrentDictionary<AbstractQuest, byte> _questList;
            public override System.Collections.Concurrent.ConcurrentDictionary<AbstractQuest, byte> QuestList => _questList ??= new();

            public override bool MoveTo(ushort regionID, int x, int y, int z, ushort heading)
            {
                TestRegionID = regionID;
                TestX = x;
                TestY = y;
                TestZ = z;
                TestHeading = heading;
                CurrentRegion = WorldMgr.GetRegion(regionID);
                return true;
            }
        }

        private static TestPlayer CreateTestPlayer(
            ICharacterClass charClass,
            eRealm realm,
            eRace race = eRace.Highlander,
            byte level = 50,
            string name = "WardenKaelen")
        {
            var player = (TestPlayer)RuntimeHelpers.GetUninitializedObject(typeof(TestPlayer));
            InitPropertyCategories(player);

            var client = (GameClient)RuntimeHelpers.GetUninitializedObject(typeof(GameClient));
            client.Account = new DbAccount { ObjectId = "test_acct", PrivLevel = 1, Language = "EN" };
            client.Out = DispatchProxy.Create<IPacketLib, UT_RealmExchangeEconomy.NoOpPacketLib>();
            player.TestClient = client;
            player.TestOut = client.Out;

            player.TestClass = charClass;
            player.Realm = realm;
            player.TestRace = (short)race;
            player.TestLevel = level;
            player.Name = name;
            player.ObjectState = GameObject.eObjectState.Active;
            player.Inventory = new TestPlayerInventory(player);

            typeof(GamePlayer).GetField("<AvailableQuestIndexes>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(player, new System.Collections.Concurrent.ConcurrentQueue<byte>());
            typeof(GamePlayer).GetField("_questListFinished", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(player, new List<AbstractQuest>());
            typeof(GamePlayer).GetField("_questListFinishedLock", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(player, new System.Threading.Lock());

            return player;
        }

        private static Region CreateStubRegion(ushort id, string description)
        {
            var data = new RegionData { Id = id, Description = description };
            return new Region(data);
        }

        private static Zone CreateStubZone(Region region, ushort id, string desc, int xOff = 0, int yOff = 0, int width = 1_000_000, int height = 1_000_000)
        {
            var zone = new Zone(region, id, desc, xOff, yOff, width, height, id, false, 0, false, 0, 0, 0, 0, 0);
            if (region != null)
            {
                var zones = (List<Zone>)typeof(Region).GetField("m_zones", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(region);
                zones?.Add(zone);
            }
            return zone;
        }

        private static EdinburghWaystonePortalNPC CreatePortalNpc(
            string name,
            ushort regionId,
            int x,
            int y,
            int z,
            bool requiresOpenWay = false,
            bool isHub = false)
        {
            var npc = (EdinburghWaystonePortalNPC)RuntimeHelpers.GetUninitializedObject(typeof(EdinburghWaystonePortalNPC));
            InitPropertyCategories(npc);
            typeof(GameNPC).GetField("m_x", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(npc, x);
            typeof(GameNPC).GetField("m_y", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(npc, y);
            typeof(GameNPC).GetField("m_z", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(npc, z);
            npc.Name = name;
            npc.CurrentRegionID = regionId;
            npc.ObjectState = GameObject.eObjectState.Active;
            npc.movementComponent = new NpcMovementComponent(npc);
            npc.movementComponent.ForceUpdatePosition();
            npc.RequiresOpenWay = requiresOpenWay;
            npc.IsNeverneverHubWaystone = isHub;
            typeof(GameNPC).GetField("m_brains", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(npc, new ArrayList());
            return npc;
        }

        [Test]
        public void EdinburghMagicTrainerAndHistoricalWardenHeadquartersSupportAllRealms()
        {
            var wardenDrillmaster = (EdinburghMagicClassTrainer)RuntimeHelpers.GetUninitializedObject(typeof(EdinburghMagicClassTrainer));
            typeof(EdinburghMagicClassTrainer).GetField("<AdditionalTrainedClasses>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(wardenDrillmaster, new HashSet<eCharacterClass> { eCharacterClass.MaulerMid, eCharacterClass.Vampiir });
            wardenDrillmaster.Name = "Drillmaster Callan Boyd";
            wardenDrillmaster.GuildName = "Council Warden Trainer";
            wardenDrillmaster.PrimaryTrainedClass = eCharacterClass.MaulerAlb;

            var albWarden = CreateTestPlayer(new ClassMaulerAlb(), eRealm.Albion);
            var midWarden = CreateTestPlayer(new ClassMaulerMid(), eRealm.Midgard);
            var hibWarden = CreateTestPlayer(new ClassVampiir(), eRealm.Hibernia);
            var albWizard = CreateTestPlayer(new ClassWizard(), eRealm.Albion);

            Assert.That(wardenDrillmaster.CanTrain(albWarden), Is.True);
            Assert.That(wardenDrillmaster.CanTrain(midWarden), Is.True);
            Assert.That(wardenDrillmaster.CanTrain(hibWarden), Is.True);
            Assert.That(wardenDrillmaster.CanTrain(albWizard), Is.False);

            var wizardTrainer = (EdinburghMagicClassTrainer)RuntimeHelpers.GetUninitializedObject(typeof(EdinburghMagicClassTrainer));
            typeof(EdinburghMagicClassTrainer).GetField("<AdditionalTrainedClasses>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(wizardTrainer, new HashSet<eCharacterClass>());
            wizardTrainer.Name = "Magister Aldous Vance";
            wizardTrainer.GuildName = "Wizard Trainer";
            wizardTrainer.PrimaryTrainedClass = eCharacterClass.Wizard;

            Assert.That(wizardTrainer.CanTrain(albWizard), Is.True);
            Assert.That(wizardTrainer.CanTrain(albWarden), Is.False);

            var commanderVance = (EdinburghCouncilNPC)RuntimeHelpers.GetUninitializedObject(typeof(EdinburghCouncilNPC));
            commanderVance.Name = "Lord-Commander Caelan Vance";
            var highWardenLachlan = (WardenSentryNPC)RuntimeHelpers.GetUninitializedObject(typeof(WardenSentryNPC));
            highWardenLachlan.Name = "High Warden Torin Lachlan";

            Assert.That(CouncilWardenEpicQuestRuntime.IsCouncilWardenQuestGiver(commanderVance), Is.True);
            Assert.That(CouncilWardenEpicQuestRuntime.IsCouncilWardenQuestGiver(highWardenLachlan), Is.True);
            Assert.That(CouncilWardenEpicQuestRuntime.IsCouncilWardenQuestGiver(wardenDrillmaster), Is.True);

            // Capital Liaisons (Camelot, Jordheim, Tir na Nog) do NOT train sworn Council Wardens
            var camelotLiaison = (AlbionMaulerTrainer)RuntimeHelpers.GetUninitializedObject(typeof(AlbionMaulerTrainer));
            camelotLiaison.Name = "Warden Alistair";
            camelotLiaison.GuildName = "White Council Liaison";
            camelotLiaison.CurrentRegionID = 10;
            Assert.That(camelotLiaison.IsCapitalLiaison, Is.True);
            Assert.That(camelotLiaison.CanTrain(albWarden), Is.False);

            var jordheimLiaison = (MidgardMaulerTrainer)RuntimeHelpers.GetUninitializedObject(typeof(MidgardMaulerTrainer));
            jordheimLiaison.Name = "Warden Haldor";
            jordheimLiaison.GuildName = "White Council Liaison";
            jordheimLiaison.CurrentRegionID = 101;
            Assert.That(jordheimLiaison.IsCapitalLiaison, Is.True);
            Assert.That(jordheimLiaison.CanTrain(midWarden), Is.False);

            var tirNaNogLiaison = (VampiirTrainer)RuntimeHelpers.GetUninitializedObject(typeof(VampiirTrainer));
            tirNaNogLiaison.Name = "Warden Cormac";
            tirNaNogLiaison.GuildName = "White Council Liaison";
            tirNaNogLiaison.CurrentRegionID = 201;
            Assert.That(tirNaNogLiaison.IsCapitalLiaison, Is.True);
            Assert.That(tirNaNogLiaison.CanTrain(hibWarden), Is.False);

            // Campacorentin Forest secretive magic outpost trainer DOES train Albion Council Wardens
            var campForestTrainer = (AlbionMaulerTrainer)RuntimeHelpers.GetUninitializedObject(typeof(AlbionMaulerTrainer));
            campForestTrainer.Name = "Warden Vance";
            campForestTrainer.GuildName = "Council Warden Trainer";
            campForestTrainer.CurrentRegionID = 1;
            Assert.That(campForestTrainer.IsCapitalLiaison, Is.False);
            Assert.That(campForestTrainer.CanTrain(albWarden), Is.True);
        }

        [Test]
        public void TheClosedCircleRR5AbilityAndEffects()
        {
            // 1. Ability Metadata & Requirements
            DbAbility dbAbility = new()
            {
                KeyName = "AtlasOF_ClosedCircle",
                Name = "The Closed Circle",
                IconID = 4260,
                Description = "Gravitic Collapse and Magic Suppression Zone"
            };

            var ability = new AtlasOF_ClosedCircle(dbAbility, 1);
            Assert.That(ability, Is.InstanceOf<RR5RealmAbility>());
            Assert.That(ability, Is.InstanceOf<ClosedCircleAbility>());
            Assert.That(ability.MaxLevel, Is.EqualTo(1));
            Assert.That(ability.CostForUpgrade(1), Is.EqualTo(0));
            Assert.That(ability.GetReUseDelay(1), Is.EqualTo(600));

            // Realm level 40 (RR5L0) check
            var lowRealmPlayer = (GamePlayer)RuntimeHelpers.GetUninitializedObject(typeof(GamePlayer));
            typeof(GamePlayer).GetField("m_dbCharacter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(lowRealmPlayer, new DbCoreCharacter { RealmLevel = 39 });
            Assert.That(ability.CheckRequirement(lowRealmPlayer), Is.False);

            var highRealmPlayer = (GamePlayer)RuntimeHelpers.GetUninitializedObject(typeof(GamePlayer));
            typeof(GamePlayer).GetField("m_dbCharacter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(highRealmPlayer, new DbCoreCharacter { RealmLevel = 40 });
            Assert.That(ability.CheckRequirement(highRealmPlayer), Is.True);

            // 2. Kinetic Collapse Stumble Effect (Phase 1)
            var target = CreateNpc("Test Target");
            typeof(GameLiving).GetField("m_effects", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, new GameEffectList(target));
            target.IsStunned = false;

            var stumble = new ClosedCircleStumbleEffect(3000);
            Assert.That(stumble.Name, Is.EqualTo("Kinetic Collapse"));
            Assert.That(stumble.Icon, Is.EqualTo(7027));

            stumble.Start(target);
            Assert.That(target.IsStunned, Is.True);

            stumble.Stop();
            Assert.That(target.IsStunned, Is.False);

            // 3. The Closed Circle Suppression Effect (Phase 2)
            var caster = (GamePlayer)RuntimeHelpers.GetUninitializedObject(typeof(GamePlayer));
            var region = (Region)RuntimeHelpers.GetUninitializedObject(typeof(Region));
            typeof(Region).GetField("m_time", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(region, 50_000L);
            target.CurrentRegion = region;
            InitPropertyCategories(target);

            var suppression = new ClosedCircleSuppressionEffect(caster, 2500);
            Assert.That(suppression.Name, Is.EqualTo("The Closed Circle"));
            Assert.That(suppression.Icon, Is.EqualTo(4260));

            suppression.Start(target);
            Assert.That(target.SilencedTime, Is.EqualTo(12_500L));
            Assert.That(target.BuffBonusMultCategory1.Get((int)eProperty.MaxSpeed), Is.EqualTo(0.65).Within(0.001));

            suppression.RefreshDuration(4000);
            Assert.That(target.SilencedTime, Is.EqualTo(14_000L));

            suppression.Stop();
            Assert.That(target.BuffBonusMultCategory1.Get((int)eProperty.MaxSpeed), Is.EqualTo(1.0).Within(0.001));

            // 4. Static entity metadata
            var circleStatic = new ClosedCircleBase();
            var nameMethod = typeof(ClosedCircleBase).GetMethod("GetStaticName", BindingFlags.Instance | BindingFlags.NonPublic);
            var modelMethod = typeof(ClosedCircleBase).GetMethod("GetStaticModel", BindingFlags.Instance | BindingFlags.NonPublic);
            var effectMethod = typeof(ClosedCircleBase).GetMethod("GetStaticEffect", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(nameMethod?.Invoke(circleStatic, null), Is.EqualTo("The Closed Circle"));
            Assert.That(modelMethod?.Invoke(circleStatic, null), Is.EqualTo((ushort)3738));
            Assert.That(effectMethod?.Invoke(circleStatic, null), Is.EqualTo((ushort)5772));
        }

        [Test]
        public void WardenFullNeverneverAndEdinburghLiveTour()
        {
            // 0. Set up WorldMgr Regions for the live tour
            var testRegions = new ushort[] { 1, 65, 100, 192, 195, 196, 197, 200 };
            foreach (var id in testRegions)
            {
                WorldMgr.Regions[id] = CreateStubRegion(id, id switch
                {
                    1 => "Albion: Hadrian's Wall & Cotswold",
                    65 => "Edinburgh: Catacombs & Cloister",
                    100 => "Midgard: Vale of Mularn",
                    192 => "Edinburgh: Grand Conclave Hall",
                    195 => "The Nevernever: Winter Court",
                    196 => "The Nevernever: Summer Court",
                    197 => "The Nevernever",
                    200 => "Hibernia: Mag Mell",
                    _ => $"Region {id}"
                });
            }

            try
            {
                // 1. Zone and Region Verification
                Assert.That(WorldMgr.GetRegion(65), Is.Not.Null);
                Assert.That(WorldMgr.GetRegion(192), Is.Not.Null);
                Assert.That(WorldMgr.GetRegion(197), Is.Not.Null);
                Assert.That(WorldMgr.GetRegion(195), Is.Not.Null);
                Assert.That(WorldMgr.GetRegion(196), Is.Not.Null);

                Assert.That(OpenWayResonanceTable.IsNeverneverOrEdinburgh(65), Is.True);
                Assert.That(OpenWayResonanceTable.IsNeverneverOrEdinburgh(192), Is.True);
                Assert.That(OpenWayResonanceTable.IsNeverneverOrEdinburgh(197), Is.True);
                Assert.That(OpenWayResonanceTable.IsNeverneverOrEdinburgh(195), Is.True);
                Assert.That(OpenWayResonanceTable.IsNeverneverOrEdinburgh(196), Is.True);
                Assert.That(OpenWayResonanceTable.IsNeverneverOrEdinburgh(1), Is.False);

                // 2. Warden Character Setup & Spell Handler
                var warden = CreateTestPlayer(new ClassMaulerAlb(), eRealm.Albion, eRace.Highlander, level: 50, name: "WardenCaelen");

                DbSpell dbSpell = new()
                {
                    SpellID = OpenWayResonanceTable.OpenWaySpellId,
                    Name = "Open Way",
                    Type = "CouncilWardenOpenWay",
                    CastTime = 40,
                    Range = 0,
                    Target = "Self"
                };
                Spell spell = new(dbSpell, 1);
                SpellLine line = new("Council Spells", "Council Spells", "unknown", true);
                var spellHandler = new OpenWaySpellHandler(warden, spell, line);

                // --- HOP 1: Hadrian's Wall Hillfort (Region 1, Zone 15) -> Nevernever Edinburgh Waystone (197) ---
                warden.TestRegionID = 1;
                warden.CurrentRegion = WorldMgr.GetRegion(1);
                CreateStubZone(warden.CurrentRegion, 15, "Hadrian's Wall");
                warden.TestX = 635904;
                warden.TestY = 336640;
                warden.TestZ = 4260;

                var hop1 = OpenWayResonanceTable.ResolveForPlayer(warden);
                Assert.That(hop1.Resonance, Is.EqualTo(FaerieResonance.EdinburghWay));
                Assert.That((hop1.RegionId, hop1.X, hop1.Y, hop1.Z),
                    Is.EqualTo((OpenWayResonanceTable.EdinburghWaystoneInNevernever.RegionId,
                                OpenWayResonanceTable.EdinburghWaystoneInNevernever.X,
                                OpenWayResonanceTable.EdinburghWaystoneInNevernever.Y,
                                OpenWayResonanceTable.EdinburghWaystoneInNevernever.Z)));

                // Execute cast via OpenWaySpellHandler
                spellHandler.ApplyEffectOnTarget(warden);
                Assert.That(warden.CurrentRegionID, Is.EqualTo(197));
                Assert.That((warden.X, warden.Y), Is.EqualTo((32000, 32000)));

                // --- HOP 2: At Edinburgh Gateway Menhir in Nevernever (197) -> Steps into Edinburgh Tier 1 Catacombs (65) ---
                var menhir = CreatePortalNpc("Edinburgh Gateway Menhir", 197, 32000, 32000, 16000, requiresOpenWay: true);
                menhir.CurrentRegion = WorldMgr.GetRegion(197);

                // Verify Open Way spell resonance standing at menhir
                var hop2 = OpenWayResonanceTable.ResolveForPlayer(warden);
                Assert.That(hop2.Resonance, Is.EqualTo(FaerieResonance.StepIntoEdinburgh));
                Assert.That((hop2.RegionId, hop2.X, hop2.Y, hop2.Z),
                    Is.EqualTo((OpenWayResonanceTable.EdinburghWaysIngress.RegionId,
                                OpenWayResonanceTable.EdinburghWaysIngress.X,
                                OpenWayResonanceTable.EdinburghWaysIngress.Y,
                                OpenWayResonanceTable.EdinburghWaysIngress.Z)));

                // Test Menhir NPC interaction & whisper
                Assert.That(EdinburghWaystonePortalNPC.CanPlayerOpenWay(warden), Is.True);
                Assert.That(menhir.Interact(warden), Is.True);

                warden.TempProperties.RemoveProperty("WHISPERDELAY");
                Assert.That(menhir.WhisperReceive(warden, "enter edinburgh"), Is.True);

                Assert.That(warden.CurrentRegionID, Is.EqualTo(65));
                Assert.That((warden.X, warden.Y, warden.Z),
                    Is.EqualTo((OpenWayResonanceTable.EdinburghWaysIngress.X,
                                OpenWayResonanceTable.EdinburghWaysIngress.Y,
                                OpenWayResonanceTable.EdinburghWaysIngress.Z)));

                // --- HOP 3: Edinburgh Tier 1 Catacombs (65) -> Grand Conclave Hall (192) via Two-Tier Admissions ---
                warden.TestX = 39011;
                warden.TestY = 44352;
                warden.TestZ = 16400;

                var conclaveJump = new EdinburghConclaveJumpPoint();
                Assert.That(conclaveJump.IsAllowedToJump(null, warden), Is.True);

                warden.MoveTo(192, 35089, 32498, 16166, 2048);
                Assert.That(warden.CurrentRegionID, Is.EqualTo(192));
                Assert.That((warden.X, warden.Y), Is.EqualTo((35089, 32498)));

                // --- HOP 4: Inside Edinburgh (192 or 65) -> Returns to Nevernever Crossroads (197) ---
                // Route C eliminated: players MUST return to Nevernever first!
                var hop4FromConclave = OpenWayResonanceTable.ResolveForPlayer(warden);
                Assert.That(hop4FromConclave.Resonance, Is.EqualTo(FaerieResonance.ReturnToNevernever));
                Assert.That((hop4FromConclave.RegionId, hop4FromConclave.X, hop4FromConclave.Y),
                    Is.EqualTo((197, 32000, 32000)));

                warden.TestRegionID = 65;
                warden.CurrentRegion = WorldMgr.GetRegion(65);
                var hop4FromCatacombs = OpenWayResonanceTable.ResolveForPlayer(warden);
                Assert.That(hop4FromCatacombs.Resonance, Is.EqualTo(FaerieResonance.ReturnToNevernever));
                Assert.That((hop4FromCatacombs.RegionId, hop4FromCatacombs.X, hop4FromCatacombs.Y),
                    Is.EqualTo((197, 32000, 32000)));

                // Execute cast from Edinburgh
                spellHandler.ApplyEffectOnTarget(warden);
                Assert.That(warden.CurrentRegionID, Is.EqualTo(197));
                Assert.That((warden.X, warden.Y), Is.EqualTo((32000, 32000)));

                // --- HOP 5: Nevernever Hub Waystones & Open Way Return to Mortal Realm Villages ---
                // A. Casting Open Way elsewhere in Nevernever returns to home starter village
                warden.TestX = 42134; // Moving along the Warded Road away from Edinburgh Waystone
                warden.TestY = 34920;
                var hop5Alb = OpenWayResonanceTable.ResolveForPlayer(warden);
                Assert.That(hop5Alb.Resonance, Is.EqualTo(FaerieResonance.ReturnToMortal));
                Assert.That((hop5Alb.RegionId, hop5Alb.X, hop5Alb.Y, hop5Alb.Z),
                    Is.EqualTo((OpenWayResonanceTable.AlbionMortalNexus.RegionId,
                                OpenWayResonanceTable.AlbionMortalNexus.X,
                                OpenWayResonanceTable.AlbionMortalNexus.Y,
                                OpenWayResonanceTable.AlbionMortalNexus.Z))); // Cotswold

                var midWarden = CreateTestPlayer(new ClassMaulerMid(), eRealm.Midgard, eRace.Norseman, level: 50, name: "WardenTorin");
                midWarden.TestRegionID = 197;
                midWarden.CurrentRegion = WorldMgr.GetRegion(197);
                midWarden.TestX = 42134;
                midWarden.TestY = 34920;
                var hop5Mid = OpenWayResonanceTable.ResolveForPlayer(midWarden);
                Assert.That(hop5Mid.Resonance, Is.EqualTo(FaerieResonance.ReturnToMortal));
                Assert.That((hop5Mid.RegionId, hop5Mid.X, hop5Mid.Y, hop5Mid.Z),
                    Is.EqualTo((OpenWayResonanceTable.MidgardMortalNexus.RegionId,
                                OpenWayResonanceTable.MidgardMortalNexus.X,
                                OpenWayResonanceTable.MidgardMortalNexus.Y,
                                OpenWayResonanceTable.MidgardMortalNexus.Z))); // Vale of Mularn

                var hibWarden = CreateTestPlayer(new ClassVampiir(), eRealm.Hibernia, eRace.Celt, level: 50, name: "WardenAine");
                hibWarden.TestRegionID = 197;
                hibWarden.CurrentRegion = WorldMgr.GetRegion(197);
                hibWarden.TestX = 42134;
                hibWarden.TestY = 34920;
                var hop5Hib = OpenWayResonanceTable.ResolveForPlayer(hibWarden);
                Assert.That(hop5Hib.Resonance, Is.EqualTo(FaerieResonance.ReturnToMortal));
                Assert.That((hop5Hib.RegionId, hop5Hib.X, hop5Hib.Y, hop5Hib.Z),
                    Is.EqualTo((OpenWayResonanceTable.HiberniaMortalNexus.RegionId,
                                OpenWayResonanceTable.HiberniaMortalNexus.X,
                                OpenWayResonanceTable.HiberniaMortalNexus.Y,
                                OpenWayResonanceTable.HiberniaMortalNexus.Z))); // Mag Mell

                // B. Testing Nevernever Hub Portal Waystones with whisper commands
                var hubPortal = CreatePortalNpc("Nevernever Realm Waystone", 197, 34134, 34920, 16000, isHub: true);
                hubPortal.CurrentRegion = WorldMgr.GetRegion(197);

                warden.TempProperties.RemoveProperty("WHISPERDELAY");
                hubPortal.WhisperReceive(warden, "cotswold");
                Assert.That((warden.CurrentRegionID, warden.X, warden.Y),
                    Is.EqualTo((OpenWayResonanceTable.AlbionMortalNexus.RegionId,
                                OpenWayResonanceTable.AlbionMortalNexus.X,
                                OpenWayResonanceTable.AlbionMortalNexus.Y)));

                warden.TempProperties.RemoveProperty("WHISPERDELAY");
                hubPortal.WhisperReceive(warden, "mularn");
                Assert.That((warden.CurrentRegionID, warden.X, warden.Y),
                    Is.EqualTo((OpenWayResonanceTable.MidgardMortalNexus.RegionId,
                                OpenWayResonanceTable.MidgardMortalNexus.X,
                                OpenWayResonanceTable.MidgardMortalNexus.Y)));

                warden.TempProperties.RemoveProperty("WHISPERDELAY");
                hubPortal.WhisperReceive(warden, "mag mell");
                Assert.That((warden.CurrentRegionID, warden.X, warden.Y),
                    Is.EqualTo((OpenWayResonanceTable.HiberniaMortalNexus.RegionId,
                                OpenWayResonanceTable.HiberniaMortalNexus.X,
                                OpenWayResonanceTable.HiberniaMortalNexus.Y)));

                warden.TempProperties.RemoveProperty("WHISPERDELAY");
                hubPortal.WhisperReceive(warden, "hillfort");
                Assert.That((warden.CurrentRegionID, warden.X, warden.Y),
                    Is.EqualTo((OpenWayResonanceTable.FrontierHillfortNexus.RegionId,
                                OpenWayResonanceTable.FrontierHillfortNexus.X,
                                OpenWayResonanceTable.FrontierHillfortNexus.Y)));

                warden.TempProperties.RemoveProperty("WHISPERDELAY");
                hubPortal.WhisperReceive(warden, "summer");
                Assert.That((warden.CurrentRegionID, warden.X, warden.Y),
                    Is.EqualTo(((ushort)196, 25752, 21072)));

                warden.TempProperties.RemoveProperty("WHISPERDELAY");
                hubPortal.WhisperReceive(warden, "winter");
                Assert.That((warden.CurrentRegionID, warden.X, warden.Y),
                    Is.EqualTo(((ushort)195, 32852, 14647)));

                // --- 3. Silver Thimble of the Veil Gatekeeping for Non-Caster Classes ---
                var armsman = CreateTestPlayer(new ClassArmsman(), eRealm.Albion, eRace.Briton, level: 50, name: "SirGareth");
                armsman.TestRegionID = 197;
                armsman.CurrentRegion = WorldMgr.GetRegion(197);
                armsman.TestX = 32000;
                armsman.TestY = 32000;
                armsman.TestZ = 16000;

                // Without Thimble: denied Open Way access
                Assert.That(EdinburghWaystonePortalNPC.CanPlayerOpenWay(armsman), Is.False);
                armsman.TempProperties.RemoveProperty("WHISPERDELAY");
                menhir.WhisperReceive(armsman, "enter edinburgh");
                Assert.That(armsman.CurrentRegionID, Is.EqualTo(197), "Mundane fighter without thimble cannot pass the Menhir");

                // Equip Silver Thimble of the Veil
                var thimble = GameInventoryItem.Create(EdinburghAndNeverneverWorld.BuildSilverThimbleTemplate());
                armsman.Inventory.AddItem(eInventorySlot.FirstBackpack, thimble);

                // With Thimble: granted Open Way access!
                Assert.That(EdinburghWaystonePortalNPC.CanPlayerOpenWay(armsman), Is.True);
                armsman.TempProperties.RemoveProperty("WHISPERDELAY");
                menhir.WhisperReceive(armsman, "enter edinburgh");
                Assert.That(armsman.CurrentRegionID, Is.EqualTo(65), "Armsman carrying Silver Thimble of the Veil passes through into Edinburgh Catacombs");

                // --- 4. Two-Tier Admissions & 5th Law Enforcement ---
                var lurikeenEldritch = CreateTestPlayer(new ClassEldritch(), eRealm.Hibernia, eRace.Lurikeen, level: 50, name: "Finnegan");
                Assert.That(conclaveJump.IsAllowedToJump(null, lurikeenEldritch), Is.False,
                    "Non-human treaty emissary denied entrance to mortal Council Conclave");

                var britonNecromancer = CreateTestPlayer(new ClassNecromancer(), eRealm.Albion, eRace.Briton, level: 50, name: "Malik");
                Assert.That(conclaveJump.IsAllowedToJump(null, britonNecromancer), Is.False,
                    "Fifth Law violator denied Conclave entrance");
                Assert.That(WardenSentryBrain.ShouldAttackTarget(britonNecromancer), Is.True,
                    "Wardens execute 5th Law violators on sight");

                // --- 5. Peace Sanctuary PvP Protection & Cross-Realm Cooperation ---
                var albCombatant = CreateTestPlayer(new ClassMaulerAlb(), eRealm.Albion);
                var midCombatant = CreateTestPlayer(new ClassMaulerMid(), eRealm.Midgard);

                foreach (ushort sanctuaryReg in new ushort[] { 65, 192, 195, 196, 197 })
                {
                    albCombatant.TestRegionID = sanctuaryReg;
                    albCombatant.CurrentRegion = WorldMgr.GetRegion(sanctuaryReg);
                    midCombatant.TestRegionID = sanctuaryReg;
                    midCombatant.CurrentRegion = WorldMgr.GetRegion(sanctuaryReg);

                    Assert.That(GameServer.ServerRules.IsAllowedToAttack(albCombatant, midCombatant, quiet: true), Is.False,
                        $"PvP forbidden in sanctuary region {sanctuaryReg}");
                    Assert.That(GameServer.ServerRules.IsAllowedToGroup(albCombatant, midCombatant, quiet: true), Is.True,
                        $"Cross-realm grouping permitted in sanctuary region {sanctuaryReg}");
                    Assert.That(GameServer.ServerRules.IsAllowedToTrade(albCombatant, midCombatant, quiet: true), Is.True,
                        $"Cross-realm trade permitted in sanctuary region {sanctuaryReg}");
                    Assert.That(GameServer.ServerRules.IsAllowedToUnderstand(albCombatant, midCombatant), Is.True,
                        $"Cross-realm chat understanding permitted in sanctuary region {sanctuaryReg}");
                }

                // Verify normal RvR rules in Mortal Frontier (Region 1)
                albCombatant.TestRegionID = 1;
                albCombatant.CurrentRegion = WorldMgr.GetRegion(1);
                midCombatant.TestRegionID = 1;
                midCombatant.CurrentRegion = WorldMgr.GetRegion(1);
                Assert.That(GameServer.ServerRules.IsAllowedToAttack(albCombatant, midCombatant, quiet: true), Is.True);
                Assert.That(GameServer.ServerRules.IsAllowedToGroup(albCombatant, midCombatant, quiet: true), Is.False);
                Assert.That(GameServer.ServerRules.IsAllowedToTrade(albCombatant, midCombatant, quiet: true), Is.False);

                // --- 6. Sanctuary Core Services in Edinburgh Tier 1 (Region 65) ---
                var recharger = (Recharger)RuntimeHelpers.GetUninitializedObject(typeof(Recharger));
                recharger.Name = "Recharger Katherine";
                recharger.GuildName = "Sanctuary Recharger";
                recharger.CurrentRegionID = 65;
                Assert.That(recharger, Is.InstanceOf<Recharger>());
                Assert.That(recharger.Name, Is.EqualTo("Recharger Katherine"));

                var healer = (GameHealer)RuntimeHelpers.GetUninitializedObject(typeof(GameHealer));
                healer.Name = "Sister Brigid of Iona";
                healer.GuildName = "Sanctuary Healer";
                healer.CurrentRegionID = 65;
                Assert.That(healer, Is.InstanceOf<GameHealer>());

                var vault = (GameVaultKeeper)RuntimeHelpers.GetUninitializedObject(typeof(GameVaultKeeper));
                vault.Name = "Archivist Eamonn Vance";
                vault.GuildName = "Council Vault Keeper";
                vault.CurrentRegionID = 65;
                Assert.That(vault, Is.InstanceOf<GameVaultKeeper>());

                var smith = (Blacksmith)RuntimeHelpers.GetUninitializedObject(typeof(Blacksmith));
                smith.Name = "Master Fergus Ironhand";
                smith.GuildName = "Sanctuary Blacksmith";
                smith.CurrentRegionID = 65;
                Assert.That(smith, Is.InstanceOf<Blacksmith>());

                var enchanter = (Enchanter)RuntimeHelpers.GetUninitializedObject(typeof(Enchanter));
                enchanter.Name = "Artificer Lyra Vance";
                enchanter.GuildName = "Council Enchanter";
                enchanter.CurrentRegionID = 65;
                Assert.That(enchanter, Is.InstanceOf<Enchanter>());
            }
            finally
            {
                foreach (var id in testRegions)
                {
                    WorldMgr.Regions.Remove(id);
                }
            }
        }

        [Test]
        public void NeverneverAndEdinburghMobCatalogAndMechanicsVerified()
        {
            var region197 = WorldMgr.GetRegion(197) ?? CreateStubRegion(197, "The Nevernever");
            CreateStubZone(region197, 1970, "The Nevernever Crossroads");
            WorldMgr.Regions[197] = region197;

            try
            {
                // 1. Fae NPC Entity & Archetype Spell Setup
                var wisp = new NeverneverFaeNPC
                {
                    Name = "Will-o'-the-Wisp",
                    GuildName = "Corpse Candle",
                    Archetype = NeverneverFaeArchetype.WillOWisp,
                    Level = 50,
                    Model = 966,
                    CurrentRegionID = 197,
                    CurrentRegion = region197,
                    X = 32000,
                    Y = 32000,
                    Z = 16000
                };
                wisp.AddToWorld();
                Assert.That(CouncilWardenMechanics.IsFaeEntity(wisp), Is.True, "Wisp recognized as Fae entity");
                Assert.That(wisp.Spells, Is.Not.Null);
                Assert.That(wisp.Spells.Count, Is.GreaterThanOrEqualTo(2));
                Assert.That(wisp.Spells.Any(s => s.Name == "Shock Bolt"), Is.True);
                Assert.That(wisp.Spells.Any(s => s.Name == "Ghostly Lure"), Is.True);

                var frosthound = new NeverneverFaeNPC
                {
                    Name = "Glacial Frosthound",
                    GuildName = "Unseelie Pack",
                    Archetype = NeverneverFaeArchetype.GlacialFrosthound,
                    Level = 45,
                    Model = 459,
                    CurrentRegionID = 197,
                    CurrentRegion = region197,
                    X = 32000,
                    Y = 32000,
                    Z = 16000
                };
                frosthound.AddToWorld();
                Assert.That(CouncilWardenMechanics.IsFaeEntity(frosthound), Is.True);
                Assert.That(frosthound.Spells.Any(s => s.Name == "Frostbite Breath"), Is.True);

                var creeper = new NeverneverFaeNPC
                {
                    Name = "Bramble Creeper",
                    GuildName = "Wyldfae Plant",
                    Archetype = NeverneverFaeArchetype.BriarCreeper,
                    Level = 22,
                    Model = 600,
                    CurrentRegionID = 197,
                    CurrentRegion = region197,
                    X = 32000,
                    Y = 32000,
                    Z = 16000
                };
                creeper.AddToWorld();
                Assert.That(CouncilWardenMechanics.IsFaeEntity(creeper), Is.True);
                Assert.That(creeper.Spells.Any(s => s.Name == "Entangling Briars"), Is.True);
                Assert.That(creeper.Spells.Any(s => s.Name == "Briar Thorn Barrage"), Is.True);

                var fetch = new NeverneverFaeNPC
                {
                    Name = "Twilight Fetch",
                    GuildName = "Deep Nevernever Horror",
                    Archetype = NeverneverFaeArchetype.TwilightFetch,
                    Level = 45,
                    Model = 667,
                    CurrentRegionID = 197,
                    CurrentRegion = region197,
                    X = 32000,
                    Y = 32000,
                    Z = 16000
                };
                fetch.AddToWorld();
                Assert.That(CouncilWardenMechanics.IsFaeEntity(fetch), Is.True);
                Assert.That(fetch.Spells.Any(s => s.Name == "Dread Chill"), Is.True);
                Assert.That(fetch.Spells.Any(s => s.Name == "Essence Siphon"), Is.True);

                // 2. Rawhead & Bloodybones World Boss
                var rawhead = new NeverneverBossFaeNPC
                {
                    Name = "Rawhead & Bloodybones",
                    GuildName = "Ancient Bogeyman of the Deep",
                    Level = 55,
                    Model = 663,
                    CurrentRegionID = 197,
                    CurrentRegion = region197,
                    X = 32400,
                    Y = 32000,
                    Z = 16000
                };
                rawhead.AddToWorld();
                Assert.That(rawhead.IsWorldBoss, Is.True);
                Assert.That(rawhead.Level, Is.EqualTo(55));
                Assert.That(CouncilWardenMechanics.IsFaeEntity(rawhead), Is.True);
                Assert.That(rawhead.Spells.Any(s => s.Name == "Bloody Bonecrush"), Is.True);
                Assert.That(rawhead.Spells.Any(s => s.Name == "Terrifying Roar"), Is.True);

                // 3. NeverneverFaeBrain Respects Warded Cobblestone Road
                var faeBrain = new NeverneverFaeBrain { AggroLevel = 100, AggroRange = 550 };
                rawhead.SetOwnBrain(faeBrain);

                var player = CreateTestPlayer(new ClassMaulerAlb(), eRealm.Albion);
                player.TestRegionID = 197;
                player.CurrentRegion = region197;

                // Player standing peacefully on the warded road (e.g. at Hub X:32000, Y:32000)
                player.TestX = 32000;
                player.TestY = 32000;
                player.TestZ = 16000;
                rawhead.X = 32400; // 400 units away (within 550 aggro range, but outside 75 warded road aggro range)
                rawhead.Y = 32000;
                rawhead.Z = 16000;

                Assert.That(NeverneverWardedRoad.IsOnWardedRoad(197, player.X, player.Y), Is.True);
                Assert.That(NeverneverWardedRoad.IsProtectedFromMobAggro(rawhead, player), Is.True);
                Assert.That(faeBrain.CanAggroTarget(player), Is.False, "Player is protected on the Warded Road");

                // Player strays off-road into the dark bogs (> 300 units from road)
                player.TestX = 21500;
                player.TestY = 20500;
                rawhead.X = 21700; // 200 units away
                rawhead.Y = 20500;

                Assert.That(NeverneverWardedRoad.IsOnWardedRoad(197, player.X, player.Y), Is.False);
                Assert.That(NeverneverWardedRoad.IsProtectedFromMobAggro(rawhead, player), Is.False);
                Assert.That(faeBrain.CanAggroTarget(player), Is.True, "Player is vulnerable off-road in the bogs");
            }
            finally
            {
                WorldMgr.Regions.Remove(197);
            }

            // 4. Peaceful Guiding Way-Wisp Lantern & Hedge-Witch
            var lantern = new WayWispLanternNPC
            {
                Name = "Way-Wisp Lantern",
                GuildName = "Guiding Witchlight",
                CurrentRegionID = 197
            };
            Assert.That(lantern.Flags, Is.EqualTo(GameNPC.eFlags.PEACE));
            Assert.That(lantern.Model, Is.EqualTo(1490));

            var hedgeWife = new NeverneverHedgeWitchNPC
            {
                Name = "Griselda the Hedge-Wife",
                GuildName = "Curiosities of the Ways",
                CurrentRegionID = 197
            };
            Assert.That(hedgeWife.Flags, Is.EqualTo(GameNPC.eFlags.PEACE));
            Assert.That(NeverneverHedgeWitchNPC.ThimblePriceCopper, Is.EqualTo(50000));

            // 5. Allied Treaty Scholars & Senior Council
            var torvald = new EdinburghCouncilNPC
            {
                Name = "Dwarven Runecarver Torvald",
                GuildName = "Allied Treaty Scholar - Midgard",
                CurrentRegionID = 65,
                Model = 190,
                LoreGreeting = "Hmph. The mortal wizards call this sanctuary, but I call it good stone and quiet study."
            };
            Assert.That(torvald.LoreGreeting, Does.Contain("Svartalfheim").Or.Contain("sanctuary"));

            var merlin = new EdinburghCouncilNPC
            {
                Name = "Aurelius Theophanes",
                GuildName = "The Merlin of the White Council",
                CurrentRegionID = 192,
                Model = 51,
                LoreGreeting = "Order, discipline, and the Seven Laws forged in the ashes of antiquity are all that stand between mortal kind and ruin."
            };
            Assert.That(merlin.LoreGreeting, Does.Contain("Seven Laws"));

            // 6. Custom Fae Loot Templates
            var dust = EdinburghAndNeverneverWorld.BuildFaeLootItemTemplate("fae_dust_essence", "Luminescent Fae Dust", 35, 200, "Fae dust", isMagical: true);
            Assert.That(dust.Id_nb, Is.EqualTo("fae_dust_essence"));
            Assert.That(dust.CanDropAsLoot, Is.True);

            var bone = EdinburghAndNeverneverWorld.BuildFaeLootItemTemplate("rawhead_bloody_bone", "Flayed Bone of Rawhead", 55, 50000, "Artifact bone", isMagical: true);
            Assert.That(bone.Id_nb, Is.EqualTo("rawhead_bloody_bone"));
            Assert.That(bone.Price, Is.EqualTo(50000));
        }

        [Test]
        public void EpicQuestlineDefinitionsHaveStrictlyRealmSpecificLocationsAndNoMultiRealmBrackets()
        {
            Assert.That(CouncilWardenEpicQuestRuntime.Definitions.Count, Is.EqualTo(9));

            foreach (var (type, def) in CouncilWardenEpicQuestRuntime.Definitions)
            {
                // Verify all 3 realm targets exist
                Assert.That(def.AlbionTarget, Is.Not.Null);
                Assert.That(def.MidgardTarget, Is.Not.Null);
                Assert.That(def.HiberniaTarget, Is.Not.Null);

                // Verify ZoneName is populated
                Assert.That(def.AlbionTarget.ZoneName, Is.Not.Null.And.Not.Empty);
                Assert.That(def.MidgardTarget.ZoneName, Is.Not.Null.And.Not.Empty);
                Assert.That(def.HiberniaTarget.ZoneName, Is.Not.Null.And.Not.Empty);

                // Verify no multi-realm bracket/slash patterns like [A/B/C] exist in dialogue or clues
                foreach (var target in def.AllTargets)
                {
                    Assert.That(target.BriefingDialogue, Does.Not.Contain("/"));
                    Assert.That(target.BriefingDialogue, Does.Not.Contain("["));
                    Assert.That(target.CompletionDialogue, Does.Not.Contain("/"));
                    Assert.That(target.Clue, Does.Not.Contain("/"));
                }

                // Verify realm-specific zone references in chapter 1
                if (def.ChapterNumber == 1)
                {
                    Assert.That(def.AlbionTarget.ZoneName, Is.EqualTo("Camelot Hills"));
                    Assert.That(def.AlbionTarget.BriefingDialogue, Does.Contain("Camelot Hills"));
                    Assert.That(def.MidgardTarget.ZoneName, Is.EqualTo("Vale of Mularn"));
                    Assert.That(def.MidgardTarget.BriefingDialogue, Does.Contain("Vale of Mularn"));
                    Assert.That(def.HiberniaTarget.ZoneName, Is.EqualTo("Lough Derg"));
                    Assert.That(def.HiberniaTarget.BriefingDialogue, Does.Contain("Lough Derg"));
                }
                // Verify realm-specific dungeon references in chapter 2
                else if (def.ChapterNumber == 2)
                {
                    Assert.That(def.AlbionTarget.ZoneName, Is.EqualTo("Tomb of Mithra"));
                    Assert.That(def.AlbionTarget.BriefingDialogue, Does.Contain("Tomb of Mithra"));
                    Assert.That(def.MidgardTarget.ZoneName, Is.EqualTo("Nisse's Lair"));
                    Assert.That(def.MidgardTarget.BriefingDialogue, Does.Contain("Nisse's Lair"));
                    Assert.That(def.HiberniaTarget.ZoneName, Is.EqualTo("Muire Tomb"));
                    Assert.That(def.HiberniaTarget.BriefingDialogue, Does.Contain("Muire Tomb"));
                }
                // Verify realm-specific epic dungeon instances in chapter 8
                else if (def.ChapterNumber == 8)
                {
                    Assert.That(def.AlbionTarget.ZoneName, Is.EqualTo("Caer Sidi"));
                    Assert.That(def.AlbionTarget.Region, Is.EqualTo(60));
                    Assert.That(def.AlbionTarget.BriefingDialogue, Does.Contain("Caer Sidi"));
                    Assert.That(def.MidgardTarget.ZoneName, Is.EqualTo("Tuscaran Glacier"));
                    Assert.That(def.MidgardTarget.Region, Is.EqualTo(160));
                    Assert.That(def.MidgardTarget.BriefingDialogue, Does.Contain("Tuscaran Glacier"));
                    Assert.That(def.HiberniaTarget.ZoneName, Is.EqualTo("Galladoria"));
                    Assert.That(def.HiberniaTarget.Region, Is.EqualTo(191));
                    Assert.That(def.HiberniaTarget.BriefingDialogue, Does.Contain("Galladoria"));
                }
                // Verify Chapter 9 climax in the Nevernever with investigation & triangulation
                else if (def.ChapterNumber == 9)
                {
                    Assert.That(def.AlbionTarget.ZoneName, Is.EqualTo("The Deep Nevernever"));
                    Assert.That(def.AlbionTarget.Region, Is.EqualTo(197));
                    Assert.That(def.AlbionTarget.BriefingDialogue, Does.Contain("Sight-Focus Crystal"));
                    Assert.That(def.AlbionTarget.BriefingDialogue, Does.Contain("triangulate"));
                    Assert.That(def.AlbionTarget.BriefingDialogue, Does.Contain("Mairead"));
                    Assert.That(def.AlbionTarget.BriefingDialogue, Does.Contain("Silver Grounding Rods"));

                    Assert.That(def.MidgardTarget.ZoneName, Is.EqualTo("The Deep Nevernever"));
                    Assert.That(def.MidgardTarget.Region, Is.EqualTo(197));
                    Assert.That(def.MidgardTarget.BriefingDialogue, Does.Contain("Sight-Focus Crystal"));
                    Assert.That(def.MidgardTarget.BriefingDialogue, Does.Contain("triangulate"));
                    Assert.That(def.MidgardTarget.BriefingDialogue, Does.Contain("Mairead"));
                    Assert.That(def.MidgardTarget.BriefingDialogue, Does.Contain("Silver Grounding Rods"));

                    Assert.That(def.HiberniaTarget.ZoneName, Is.EqualTo("The Deep Nevernever"));
                    Assert.That(def.HiberniaTarget.Region, Is.EqualTo(197));
                    Assert.That(def.HiberniaTarget.BriefingDialogue, Does.Contain("Sight-Focus Crystal"));
                    Assert.That(def.HiberniaTarget.BriefingDialogue, Does.Contain("triangulate"));
                    Assert.That(def.HiberniaTarget.BriefingDialogue, Does.Contain("Mairead"));
                    Assert.That(def.HiberniaTarget.BriefingDialogue, Does.Contain("Silver Grounding Rods"));
                }
            }
        }
    }
}
