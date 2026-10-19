using System;
using System.Collections.Generic;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;

namespace DOL.GS
{
    /// <summary>
    /// Archetypes of Fae entities inhabiting the Nevernever crossroads, brambles,
    /// bogs, and court borderlands.
    /// </summary>
    public enum NeverneverFaeArchetype
    {
        BriarCreeper,
        ThornBoggart,
        BriarSprite,
        BramblewoodLurker,
        RedcapCutthroat,
        RedcapMarauder,
        ShadowMalk,
        TwilightFetch,
        WillOWisp,
        BogHag,
        GlacialFrosthound,
        UnseelieSentinel,
        RimeCrustedFetch,
        FroststalkerWendigo,
        SeelieScout,
        BriarThornsteed,
        GoldenWillowWisp,
        ElderGruff,
        RawheadBloodybones
    }

    /// <summary>
    /// Specialized GameNPC representing Fae entities in The Nevernever.
    /// Automatically tags itself as a Fae entity for True Cold Iron and Iron Sickness mechanics,
    /// configures archetype-specific spells and abilities, and uses NeverneverFaeBrain.
    /// </summary>
    public class NeverneverFaeNPC : GameNPC
    {
        private NeverneverFaeArchetype _archetype;
        public NeverneverFaeArchetype Archetype
        {
            get => _archetype;
            set
            {
                _archetype = value;
                ConfigureArchetypeAbilities();
            }
        }
        public bool IsWorldBoss { get; set; }

        public NeverneverFaeNPC()
        {
            BodyType = (ushort)NpcTemplateMgr.eBodyType.Magical;
            Realm = eRealm.None;
            Flags = 0;
            RespawnInterval = 90000;
            TempProperties.SetProperty(CouncilWardenMechanics.FAE_ENTITY_PROPERTY, true);
        }

        public override bool AddToWorld()
        {
            TempProperties.SetProperty(CouncilWardenMechanics.FAE_ENTITY_PROPERTY, true);
            ConfigureArchetypeAbilities();
            Health = MaxHealth;
            return base.AddToWorld();
        }

        protected override void BuildAmbientTexts()
        {
            if (GameServer.Instance?.NpcManager?.AmbientBehaviour != null && !string.IsNullOrEmpty(Name))
            {
                base.BuildAmbientTexts();
            }
        }

        public virtual void ConfigureArchetypeAbilities()
        {
            List<Spell> spells = new();

            switch (Archetype)
            {
                case NeverneverFaeArchetype.WillOWisp:
                case NeverneverFaeArchetype.GoldenWillowWisp:
                    MaxSpeedBase = 240;
                    spells.Add(CreateFaeSpell(
                        spellId: 99801,
                        name: Archetype == NeverneverFaeArchetype.WillOWisp ? "Shock Bolt" : "Searing Radiance",
                        spellType: eSpellType.DirectDamage.ToString(),
                        damageType: Archetype == NeverneverFaeArchetype.WillOWisp ? eDamageType.Energy : eDamageType.Heat,
                        damage: 135,
                        range: 1200,
                        castTime: 2.0,
                        clientEffect: (ushort)(Archetype == NeverneverFaeArchetype.WillOWisp ? 5740 : 5744)));
                    spells.Add(CreateFaeSpell(
                        spellId: 99802,
                        name: "Ghostly Lure",
                        spellType: eSpellType.Mesmerize.ToString(),
                        damageType: eDamageType.Spirit,
                        damage: 0,
                        duration: 6,
                        range: 1000,
                        castTime: 1.5,
                        clientEffect: 5763));
                    break;

                case NeverneverFaeArchetype.GlacialFrosthound:
                case NeverneverFaeArchetype.FroststalkerWendigo:
                    MaxSpeedBase = 225;
                    spells.Add(CreateFaeSpell(
                        spellId: 99803,
                        name: "Frostbite Breath",
                        spellType: eSpellType.DamageSpeedDecrease.ToString(),
                        damageType: eDamageType.Cold,
                        damage: 120,
                        value: 25,
                        duration: 8,
                        range: 650,
                        castTime: 1.0,
                        clientEffect: 5771));
                    break;

                case NeverneverFaeArchetype.BriarCreeper:
                case NeverneverFaeArchetype.BriarSprite:
                    MaxSpeedBase = 190;
                    spells.Add(CreateFaeSpell(
                        spellId: 99804,
                        name: "Entangling Briars",
                        spellType: eSpellType.SpeedDecrease.ToString(),
                        damageType: eDamageType.Matter,
                        damage: 40,
                        value: 99,
                        duration: 6,
                        range: 850,
                        castTime: 1.5,
                        clientEffect: 5758));
                    spells.Add(CreateFaeSpell(
                        spellId: 99805,
                        name: "Briar Thorn Barrage",
                        spellType: eSpellType.DirectDamage.ToString(),
                        damageType: eDamageType.Slash,
                        damage: 95,
                        range: 950,
                        castTime: 2.0,
                        clientEffect: 5761));
                    break;

                case NeverneverFaeArchetype.TwilightFetch:
                case NeverneverFaeArchetype.RimeCrustedFetch:
                    MaxSpeedBase = 200;
                    spells.Add(CreateFaeSpell(
                        spellId: 99806,
                        name: "Dread Chill",
                        spellType: eSpellType.DirectDamage.ToString(),
                        damageType: eDamageType.Cold,
                        damage: 155,
                        range: 1100,
                        castTime: 2.0,
                        clientEffect: 5771));
                    spells.Add(CreateFaeSpell(
                        spellId: 99807,
                        name: "Essence Siphon",
                        spellType: eSpellType.Lifedrain.ToString(),
                        damageType: eDamageType.Spirit,
                        damage: 125,
                        range: 900,
                        castTime: 2.0,
                        clientEffect: 5766));
                    break;

                case NeverneverFaeArchetype.BogHag:
                    MaxSpeedBase = 195;
                    spells.Add(CreateFaeSpell(
                        spellId: 99808,
                        name: "Curse of Feeble Bones",
                        spellType: eSpellType.StrengthConstitutionDebuff.ToString(),
                        damageType: eDamageType.Body,
                        damage: 0,
                        value: 45,
                        duration: 15,
                        range: 900,
                        castTime: 2.0,
                        clientEffect: 5763));
                    spells.Add(CreateFaeSpell(
                        spellId: 99809,
                        name: "Bramble Mire",
                        spellType: eSpellType.SpeedDecrease.ToString(),
                        damageType: eDamageType.Matter,
                        damage: 50,
                        value: 99,
                        duration: 5,
                        range: 850,
                        castTime: 1.5,
                        clientEffect: 5758));
                    break;

                case NeverneverFaeArchetype.RawheadBloodybones:
                    IsWorldBoss = true;
                    RespawnInterval = 300000;
                    MaxSpeedBase = 215;
                    spells.Add(CreateFaeSpell(
                        spellId: 99810,
                        name: "Bloody Bonecrush",
                        spellType: eSpellType.DirectDamage.ToString(),
                        damageType: eDamageType.Crush,
                        damage: 380,
                        range: 400,
                        castTime: 1.0,
                        clientEffect: 5748));
                    spells.Add(CreateFaeSpell(
                        spellId: 99811,
                        name: "Terrifying Roar",
                        spellType: eSpellType.Stun.ToString(),
                        damageType: eDamageType.Body,
                        damage: 0,
                        duration: 4,
                        range: 500,
                        radius: 400,
                        castTime: 0.5,
                        clientEffect: 5760));
                    break;

                case NeverneverFaeArchetype.ShadowMalk:
                    MaxSpeedBase = 250;
                    break;

                case NeverneverFaeArchetype.RedcapCutthroat:
                case NeverneverFaeArchetype.RedcapMarauder:
                    MaxSpeedBase = 215;
                    break;

                default:
                    MaxSpeedBase = 200;
                    break;
            }

            if (spells.Count > 0)
            {
                Spells = spells;
            }
        }

        public static Spell CreateFaeSpell(
            int spellId,
            string name,
            string spellType,
            eDamageType damageType,
            int damage,
            int range,
            double castTime,
            int clientEffect,
            int duration = 0,
            int value = 0,
            int radius = 0)
        {
            DbSpell dbSpell = new()
            {
                SpellID = spellId,
                Name = name,
                Type = spellType,
                DamageType = (int)damageType,
                Damage = damage,
                Range = range,
                Radius = radius,
                CastTime = castTime,
                Duration = duration,
                Value = value,
                Target = eSpellTarget.ENEMY.ToString(),
                ClientEffect = clientEffect,
                Icon = clientEffect,
                Description = $"{name} used by Fae predators of the Nevernever.",
                AllowAdd = false
            };
            return new Spell(dbSpell, 50);
        }
    }

    /// <summary>
    /// Specialized Boss NPC for Rawhead & Bloodybones (Level 55 World Boss in Region 197).
    /// Roams the deep dark water margins, broadcasts menacing combat yells,
    /// and grants guaranteed trophy drops.
    /// </summary>
    public class NeverneverBossFaeNPC : NeverneverFaeNPC
    {
        private bool _hasEngaged;

        public NeverneverBossFaeNPC()
        {
            Archetype = NeverneverFaeArchetype.RawheadBloodybones;
            IsWorldBoss = true;
            Level = 55;
            Model = 1494; // WardenFae Rawhead & Bloodybones (DR_bosshulker high-poly flayed crimson & bone)
            Size = 75;
            RespawnInterval = 300000;
        }

        public override void OnAttackedByEnemy(AttackData ad)
        {
            base.OnAttackedByEnemy(ad);

            if (!_hasEngaged && ad?.Attacker != null)
            {
                _hasEngaged = true;
                Yell("Rawhead & Bloodybones surges from the stygian mire with a bloodcurdling bellow! 'FLESH FOR THE SKULL! BONES FOR THE DEEP!'");
            }
        }

        public override void Die(GameObject killer)
        {
            Yell("With a final agonizing wail, the flayed horror sinks beneath the black waters...");
            _hasEngaged = false;
            base.Die(killer);
        }
    }

    /// <summary>
    /// Peaceful guiding light along the Warded Cobblestone Road in Region 197.
    /// Reassures travelers and provides lore guidance about staying on the warded stones.
    /// </summary>
    public class WayWispLanternNPC : GameNPC
    {
        public WayWispLanternNPC()
        {
            Model = 1490; // WardenFae Way-Wisp Lantern (Cyan-blue aurora node)
            Size = 40;
            Level = 50;
            Realm = eRealm.None;
            Flags = eFlags.PEACE;
            RespawnInterval = 10000;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 4000);
            player.Out.SendMessage(
                "The cyan witchlight pulses gently above the warded cobblestones. A soft whisper echoes in your mind: " +
                "'Stay upon the stones of the Way... the ancient wards veil your scent from the Fae. " +
                "Stray into the briars or black water, and the hunger of the Nevernever will find you.'",
                eChatType.CT_Say,
                eChatLoc.CL_PopupWindow);

            return true;
        }
    }

    /// <summary>
    /// Crossroads Hedge-Witch merchant in Region 197 (Griselda the Hedge-Wife).
    /// Located at the road fork, providing travelers with potions, reagents,
    /// and the Silver Thimble of the Veil.
    /// </summary>
    public class NeverneverHedgeWitchNPC : GameNPC
    {
        public const int ThimblePriceCopper = 50000;

        public NeverneverHedgeWitchNPC()
        {
            Model = 46; // Inconnu / Ancient Witch
            Size = 48;
            Level = 48;
            Realm = eRealm.None;
            Flags = eFlags.PEACE;
            RespawnInterval = 30000;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 5000);
            player.Out.SendMessage(
                $"{Name} peers at you from beneath a moth-eaten shawl. 'Lost in the Ways, traveler? " +
                "Or seeking the secret paths of the White Council? I have charms to keep your soul intact... " +
                $"Whisper [thimble] to purchase a Silver Thimble of the Veil for {ThimblePriceCopper / 10000} gold, " +
                "or [supplies] for traveling reagents.'",
                eChatType.CT_Say,
                eChatLoc.CL_PopupWindow);

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text) || source is not GamePlayer player)
                return false;

            if (text.Equals("thimble", StringComparison.OrdinalIgnoreCase))
            {
                if (player.GetCurrentMoney() < ThimblePriceCopper)
                {
                    SayTo(player, "Your purse is too light, traveler! Come back when you have the coin.");
                    return true;
                }

                if (!player.RemoveMoney(ThimblePriceCopper))
                {
                    SayTo(player, "A transaction error occurred. Check your coin purse.");
                    return true;
                }

                DbItemTemplate thimbleTemplate = EdinburghAndNeverneverWorld.EnsureSilverThimbleTemplate();
                GameInventoryItem thimbleItem = GameInventoryItem.Create(thimbleTemplate);
                if (player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, thimbleItem))
                {
                    SayTo(player, "Take it, and prick your finger with care! The Veil gives way to silver and blood.");
                }
                else
                {
                    player.AddMoney(ThimblePriceCopper);
                    SayTo(player, "Your pack is full! Make room before taking a talisman.");
                }
                return true;
            }

            if (text.Equals("supplies", StringComparison.OrdinalIgnoreCase))
            {
                SayTo(player, "I keep wolfsbane, salt crystals, and cold well-water for those who journey toward Arctis Tor or the Summer Glades.");
                return true;
            }

            return true;
        }
    }

    /// <summary>
    /// AI Brain for Nevernever Fae predators.
    /// Honors the Warded Cobblestone Road (suppresses aggro range to 75 units while players stay on road),
    /// unleashes full aggro (550+ units) when players stray into the brambles or bogs,
    /// and reacts with agony emotes when struck by True Cold Iron.
    /// </summary>
    public class NeverneverFaeBrain : StandardMobBrain
    {
        public NeverneverFaeBrain()
        {
            AggroLevel = 100;
            AggroRange = 550;
            ThinkInterval = 1500;
        }

        public override bool CanAggroTarget(GameLiving target)
        {
            if (target == null || !target.IsAlive)
                return false;

            if (NeverneverWardedRoad.IsProtectedFromMobAggro(Body, target))
                return false;

            return base.CanAggroTarget(target);
        }

        protected override void CheckPlayerAggro()
        {
            if (Body == null || !Body.IsAlive)
                return;

            foreach (GamePlayer player in Body.GetPlayersInRadius((ushort)AggroRange))
            {
                if (player == null || !player.IsAlive)
                    continue;

                if (NeverneverWardedRoad.IsProtectedFromMobAggro(Body, player))
                    continue;

                if (CanAggroTarget(player))
                {
                    AddToAggroList(player, 100);
                    Body.StartAttack(player);
                    return;
                }
            }
        }

        public override void OnAttackedByEnemy(AttackData ad)
        {
            base.OnAttackedByEnemy(ad);

            if (ad?.Weapon != null && Body != null && Body.IsAlive)
            {
                if (CouncilWardenMechanics.IsColdIronWeapon(ad.Weapon))
                {
                    if (Util.Chance(40))
                    {
                        Body.Say($"{Body.Name} screeches in agony as True Cold Iron sears its immortal flesh!");
                    }
                }
            }
        }
    }
}
