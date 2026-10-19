using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.PropertyCalc;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Shared mechanics, inherent passives, and Cold Iron / Iron Sickness vs. Fae logic
    /// for the Council Warden class across Albion (MaulerAlb=60), Midgard (MaulerMid=61),
    /// and Hibernia (Vampiir=58).
    /// </summary>
    public static class CouncilWardenMechanics
    {
        public const string FAE_ENTITY_PROPERTY = "CouncilWarden_IsFaeEntity";
        public const string COLD_IRON_WEAPON_PROPERTY = "CouncilWarden_IsColdIronWeapon";

        public static bool IsCouncilWarden(GameLiving living)
        {
            if (living is IGamePlayer gp && gp.CharacterClass != null)
            {
                return gp.CharacterClass.ID is (int)eCharacterClass.MaulerAlb
                    or (int)eCharacterClass.MaulerMid
                    or (int)eCharacterClass.Vampiir;
            }

            return false;
        }

        /// <summary>
        /// Inherent Trait 1: Wizardly Cellular Resilience (Lv 10).
        /// Passively reduces the duration of all incoming Bleed, Poison/DoT, and Disease effects by 25%.
        /// </summary>
        public static double ApplyCellularResilienceDuration(GameLiving target, Spell spell, double duration)
        {
            if (target == null || spell == null || duration <= 0)
                return duration;

            if (IsCouncilWarden(target) && target.Level >= 10)
            {
                if (spell.SpellType is eSpellType.DamageOverTime
                    or eSpellType.CouncilWardenThermalSpark
                    or eSpellType.Disease
                    or eSpellType.StyleBleeding)
                {
                    return duration * 0.75;
                }
            }

            return duration;
        }

        /// <summary>
        /// Inherent Trait 1: Wizardly Cellular Resilience (Lv 10).
        /// Increases out-of-combat resting health regeneration by +20%.
        /// </summary>
        public static double ApplyCellularResilienceRestingRegen(GameLiving living, double regen)
        {
            if (living != null && IsCouncilWarden(living) && living.Level >= 10 && !living.InCombat && living.IsSitting)
                return regen * 1.20;

            return regen;
        }

        /// <summary>
        /// Returns true if the living currently has Opening The Sight toggled on.
        /// </summary>
        public static bool HasTheSightActive(GameLiving living)
        {
            if (living?.effectListComponent == null)
                return false;

            var effects = living.effectListComponent.GetEffects();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is TheSightECSEffect sight && sight.IsActive)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the living is currently marked by The Warden's Brand (Death Curse).
        /// </summary>
        public static bool HasWardensBrand(GameLiving living)
        {
            if (living?.effectListComponent == null)
                return false;

            var effects = living.effectListComponent.GetEffects();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is WardensBrandECSEffect brand && brand.IsActive)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the active Iron Sickness effect on the target, if any.
        /// </summary>
        public static IronSicknessECSEffect GetIronSicknessEffect(GameLiving living)
        {
            if (living?.effectListComponent == null)
                return null;

            var effects = living.effectListComponent.GetEffects();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is IronSicknessECSEffect ironSickness && ironSickness.IsActive)
                    return ironSickness;
            }

            return null;
        }

        /// <summary>
        /// Returns true if the living currently has Vinculum damage reflection active.
        /// </summary>
        public static VinculumECSEffect GetActiveVinculumEffect(GameLiving living)
        {
            if (living?.effectListComponent == null)
                return null;

            var effects = living.effectListComponent.GetEffects();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is VinculumECSEffect vinculum && vinculum.IsActive)
                    return vinculum;
            }

            return null;
        }

        public static bool HasVinculumActive(GameLiving living)
        {
            return GetActiveVinculumEffect(living) != null;
        }

        /// <summary>
        /// Returns true if the living is under the Probability Hex fumble curse.
        /// </summary>
        public static bool HasProbabilityHex(GameLiving living)
        {
            if (living?.effectListComponent == null)
                return false;

            var effects = living.effectListComponent.GetEffects();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is ProbabilityHexECSEffect hex && hex.IsActive)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Reflects incoming damage back to the attacker based on active Vinculum spell value (15% to 35%).
        /// </summary>
        public static void HandleVinculumReflection(GameLiving defender, AttackData ad)
        {
            if (defender == null || ad == null || ad.Attacker == null || ad.Damage <= 0)
                return;

            if (ad.SpellHandler is CouncilWardenVinculumSpellHandler)
                return;

            var vinculum = GetActiveVinculumEffect(defender);
            if (vinculum != null)
            {
                double reflectPct = (vinculum.SpellHandler?.Spell != null && vinculum.SpellHandler.Spell.Value > 0)
                    ? vinculum.SpellHandler.Spell.Value / 100.0
                    : 0.25;
                int reflectedDamage = (int)Math.Max(1, Math.Round(ad.Damage * reflectPct));
                AttackData reflectAd = new AttackData
                {
                    Attacker = defender,
                    Target = ad.Attacker,
                    Damage = reflectedDamage,
                    DamageType = eDamageType.Energy,
                    AttackType = AttackData.eAttackType.Spell,
                    AttackResult = eAttackResult.HitUnstyled,
                };

                if (defender is GamePlayer playerDefender)
                    playerDefender.Out?.SendMessage($"Your Vinculum reflects {reflectedDamage} damage back upon {ad.Attacker.GetName(0, false)}!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                if (ad.Attacker is GamePlayer playerAttacker)
                    playerAttacker.Out?.SendMessage($"{defender.GetName(0, true)}'s Vinculum reflects {reflectedDamage} damage back upon you!", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);

                ad.Attacker.TakeDamage(reflectAd);
            }
        }

        /// <summary>
        /// Inherent Trait 2: Sensation of Hostile Energies (Lv 20) & Opening The Sight (Lv 30) & Warden's Brand.
        /// </summary>
        public static bool CanDetectByWardenSightOrBrand(GameLiving detector, GameLiving stealthedTarget)
        {
            if (detector == null || stealthedTarget == null)
                return false;

            // A target marked by The Warden's Brand is revealed to all allies.
            if (HasWardensBrand(stealthedTarget))
                return true;

            // Opening The Sight grants 1,000-unit true vision.
            if (HasTheSightActive(detector) && detector.IsWithinRadius(stealthedTarget, 1000))
                return true;

            return false;
        }

        /// <summary>
        /// Inherent Trait 2: Sensation of Hostile Energies (Lv 20).
        /// Grants a passive +200 unit bonus to stealth detection radius.
        /// </summary>
        public static int GetSensationOfHostileEnergiesRadiusBonus(GameLiving detector)
        {
            if (detector != null && IsCouncilWarden(detector) && detector.Level >= 20)
                return 200;

            return 0;
        }

        /// <summary>
        /// Inherent Trait 3: Kinetic Grounding (Lv 25) (-30% spell critical damage taken)
        /// & Opening The Sight Sensory Overload (+10% magic damage taken while The Sight is open).
        /// </summary>
        public static void AdjustIncomingSpellDamage(AttackData ad)
        {
            if (ad?.Target == null || ad.AttackType != AttackData.eAttackType.Spell)
                return;

            // Kinetic Grounding (Lv 25): -30% critical damage taken from enemy spells.
            if (IsCouncilWarden(ad.Target) && ad.Target.Level >= 25 && ad.CriticalDamage > 0)
            {
                ad.CriticalDamage = (int)Math.Round(ad.CriticalDamage * 0.70);
            }

            // Sensory Overload (Psychic Trauma): +10% magic damage taken while The Sight is open.
            if (HasTheSightActive(ad.Target) && ad.Damage > 0)
            {
                int bonusDamage = Math.Max(1, (int)Math.Round(ad.Damage * 0.10));
                ad.Damage += bonusDamage;
            }
        }

        /// <summary>
        /// Inherent Trait 4: The Murphyonic Field (Lv 1).
        /// Enemy mechanical crossbows firing at a Warden suffer a +10% chance to misfire/fumble.
        /// </summary>
        public static bool RollMurphyonicCrossbowMisfire(GameLiving target, DbInventoryItem attackerWeapon)
        {
            if (target == null || attackerWeapon == null)
                return false;

            if (IsCouncilWarden(target) && attackerWeapon.Object_Type == (int)eObjectType.Crossbow)
                return Util.Chance(10);

            return false;
        }

        /// <summary>
        /// Inherent Trait 4: The Murphyonic Field (Lv 1).
        /// Wardens take 20% reduced damage from mechanical siege engines.
        /// </summary>
        public static void AdjustMurphyonicSiegeDamage(AttackData ad)
        {
            if (ad?.Target == null || !IsCouncilWarden(ad.Target))
                return;

            bool isSiegeAttack = ad.Attacker is GameSiegeWeapon
                || ad.SpellHandler?.Spell?.SpellType is eSpellType.SiegeDirectDamage or eSpellType.SiegeArrow;

            if (isSiegeAttack && ad.Damage > 0)
            {
                ad.Damage = (int)Math.Round(ad.Damage * 0.80);
            }
        }

        /// <summary>
        /// Inherent Trait 5: The Death Curse (Ultima Ratio) (Lv 50).
        /// When slain in combat, unleashes a 200-unit radial interrupt shockwave and brands the killer for 30s.
        /// </summary>
        public static void TriggerDeathCurse(GameLiving dyingWarden, GameObject killer)
        {
            if (dyingWarden == null || !IsCouncilWarden(dyingWarden) || dyingWarden.Level < 50)
                return;

            if (killer is not GameLiving killerLiving || killerLiving == dyingWarden)
                return;

            // 1. 200-unit radial kinetic shockwave that interrupts all enemy spellcasting.
            if (dyingWarden.CurrentRegion != null)
            {
                foreach (GamePlayer player in dyingWarden.GetPlayersInRadius(200))
                {
                    if (player != null && player != dyingWarden && GameServer.ServerRules.IsAllowedToAttack(dyingWarden, player, true))
                    {
                        player.StartInterruptTimer(dyingWarden.SpellInterruptDuration, AttackData.eAttackType.Spell, dyingWarden);
                        player.Out.SendMessage($"{dyingWarden.Name}'s Death Curse erupts in a violent shockwave, disrupting your concentration!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                    }
                }

                foreach (GameNPC npc in dyingWarden.GetNPCsInRadius(200))
                {
                    if (npc != null && npc != dyingWarden && GameServer.ServerRules.IsAllowedToAttack(dyingWarden, npc, true))
                    {
                        npc.StartInterruptTimer(dyingWarden.SpellInterruptDuration, AttackData.eAttackType.Spell, dyingWarden);
                    }
                }
            }

            // 2. Brand the killer with The Warden's Brand for 30 seconds.
            if (killerLiving.IsAlive && killerLiving.effectListComponent != null)
            {
                Spell brandSpell = new(new DbSpell
                {
                    Name = "The Warden's Brand",
                    SpellID = 99990,
                    ClientEffect = 14315,
                    Icon = 14315,
                    Target = "Enemy",
                    Type = eSpellType.SkillsDebuff.ToString(),
                    Duration = 30,
                    Value = 15,
                    DamageType = (int)eDamageType.Energy
                }, 50);

                SpellLine line = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
                SpellHandler handler = new(dyingWarden, brandSpell, line);
                ECSGameEffectFactory.Create(new(killerLiving, 30000, 1.0, handler), static (in i) => new WardensBrandECSEffect(i));

                if (dyingWarden is GamePlayer wardenPlayer)
                {
                    wardenPlayer.Out.SendMessage($"With your final breath, you unleash your Death Curse upon {killerLiving.Name}!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }
        }

        /// <summary>
        /// Determines whether the target is a Fae creature of the Nevernever.
        /// </summary>
        public static bool IsFaeEntity(GameLiving target)
        {
            if (target == null)
                return false;

            if (target.TempProperties?.GetProperty<bool>(FAE_ENTITY_PROPERTY) == true)
                return true;

            if (target is GameNPC npc)
            {
                string name = npc.Name ?? string.Empty;
                string guild = npc.GuildName ?? string.Empty;

                if (name.Contains("Sidhe", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Malk", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Redcap", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Fetch", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Fae", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Gruagach", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Winter", StringComparison.OrdinalIgnoreCase) && guild.Contains("Court", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Summer", StringComparison.OrdinalIgnoreCase) && guild.Contains("Court", StringComparison.OrdinalIgnoreCase)
                    || guild.Contains("Nevernever", StringComparison.OrdinalIgnoreCase)
                    || guild.Contains("Winter Court", StringComparison.OrdinalIgnoreCase)
                    || guild.Contains("Summer Court", StringComparison.OrdinalIgnoreCase)
                    || guild.Contains("Unseelie", StringComparison.OrdinalIgnoreCase)
                    || guild.Contains("Seelie", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a weapon is forged of True Cold Iron.
        /// </summary>
        public static bool IsColdIronWeapon(DbInventoryItem weapon)
        {
            if (weapon == null)
                return false;

            string id = weapon.Id_nb ?? string.Empty;
            string name = weapon.Name ?? string.Empty;
            string packageId = weapon.PackageID ?? string.Empty;
            string desc = weapon.Description ?? string.Empty;

            return id.Contains("cold_iron", StringComparison.OrdinalIgnoreCase)
                || id.Equals("legions_severing_edge", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Cold Iron", StringComparison.OrdinalIgnoreCase)
                || packageId.Equals("ColdIron", StringComparison.OrdinalIgnoreCase)
                || desc.Contains("Cold Iron", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether a weapon is a standard metal (iron/steel) melee weapon capable of triggering Iron Sickness on Fae.
        /// </summary>
        public static bool IsMetalMeleeWeapon(DbInventoryItem weapon)
        {
            if (weapon == null)
                return false;

            if (IsColdIronWeapon(weapon))
                return true;

            eObjectType objType = (eObjectType)weapon.Object_Type;
            return objType is eObjectType.Sword
                or eObjectType.TwoHandedWeapon
                or eObjectType.ThrustWeapon
                or eObjectType.Blades
                or eObjectType.Piercing
                or eObjectType.LargeWeapons
                or eObjectType.CelticSpear
                or eObjectType.PolearmWeapon
                or eObjectType.Flexible
                or eObjectType.Axe
                or eObjectType.Hammer
                or eObjectType.LeftAxe
                or eObjectType.HandToHand
                or eObjectType.FistWraps;
        }

        /// <summary>
        /// Applies Cold Iron (+50% melee damage, regen suppression) and Iron Sickness (8s magic ward shatter)
        /// when a melee attack hits a Fae creature.
        /// </summary>
        public static void ApplyColdIronAndIronSicknessOnMeleeHit(AttackData ad)
        {
            if (ad?.Target == null || ad.Attacker == null || !ad.IsMeleeAttack || !ad.IsHit)
                return;

            if (!IsFaeEntity(ad.Target))
                return;

            DbInventoryItem weapon = ad.Weapon;
            bool isColdIron = IsColdIronWeapon(weapon);
            bool isMetal = isColdIron || IsMetalMeleeWeapon(weapon) || IsCouncilWarden(ad.Attacker);

            if (!isMetal)
                return;

            // True Cold Iron weapons deal +50% bonus physical damage to Fae and suppress their health regeneration.
            if (isColdIron && ad.Damage > 0)
            {
                ad.Damage = (int)Math.Round(ad.Damage * 1.50);
                if (ad.StyleDamage > 0)
                    ad.StyleDamage = (int)Math.Round(ad.StyleDamage * 1.50);
            }

            // Apply or refresh 8-second Iron Sickness on the Fae target.
            if (ad.Target.IsAlive && ad.Target.effectListComponent != null)
            {
                IronSicknessECSEffect existing = GetIronSicknessEffect(ad.Target);
                if (existing != null)
                {
                    existing.ExpireTick = GameLoop.GameLoopTime + 8000;
                    if (isColdIron)
                        existing.SuppressHealthRegen = true;
                }
                else
                {
                    Spell sicknessSpell = new(new DbSpell
                    {
                        Name = "Iron Sickness",
                        SpellID = 99991,
                        ClientEffect = 14316,
                        Icon = 14316,
                        Target = "Enemy",
                        Type = eSpellType.BodyResistDebuff.ToString(),
                        Duration = 8,
                        Value = 75,
                        DamageType = (int)eDamageType.Body
                    }, ad.Attacker.Level);

                    SpellLine line = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
                    SpellHandler handler = new(ad.Attacker, sicknessSpell, line);
                    ECSGameEffectFactory.Create(new(ad.Target, 8000, 1.0, handler), isColdIron, static (in i, coldIron) => new IronSicknessECSEffect(i, coldIron));

                    if (ad.Attacker is GamePlayer playerAttacker)
                    {
                        playerAttacker.Out.SendMessage($"The bite of iron sears {ad.Target.Name}, shattering its Fae glamour ward with Iron Sickness!", eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                    }
                }
            }
        }

        /// <summary>
        /// Applies the Fae 85% natural magic ward, or shatters it by -75% while afflicted with Iron Sickness.
        /// </summary>
        public static void ApplyFaeMagicWard(AttackData ad)
        {
            if (ad?.Target == null || ad.Attacker == null || ad.AttackType != AttackData.eAttackType.Spell || ad.Damage <= 0)
                return;

            if (!IsFaeEntity(ad.Target))
                return;

            IronSicknessECSEffect ironSickness = GetIronSicknessEffect(ad.Target);
            int resistPierce = ad.Attacker.GetModified(eProperty.ResistPierce);

            // Base Fae Magic Ward is 85% mitigation. Iron Sickness shatters it by -75% (down to 10% base ward).
            double wardMitigation = ironSickness != null ? 0.10 : 0.85;
            wardMitigation = Math.Max(0.0, wardMitigation - (resistPierce * 0.01));

            if (wardMitigation > 0)
            {
                ad.Damage = Math.Max(1, (int)Math.Round(ad.Damage * (1.0 - wardMitigation)));
                if (ad.CriticalDamage > 0)
                    ad.CriticalDamage = (int)Math.Round(ad.CriticalDamage * (1.0 - wardMitigation));
            }
        }
    }

    // =========================================================================
    // 1. THERMAL SPARK (Upfront Heat DD + 6s Heat Burn DoT)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenThermalSpark)]
    public class CouncilWardenThermalSparkSpellHandler : DirectDamageSpellHandler
    {
        public override string ShortDescription =>
            $"Inflicts {Spell.Damage} {Spell.DamageTypeToString()} damage and ignites the target for {Spell.Value} Heat damage every 2 seconds for {(Spell.Duration > 0 ? Spell.Duration / 1000.0 : 6.0)} seconds.";

        public CouncilWardenThermalSparkSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override ECSGameSpellEffect CreateECSEffect(in ECSGameEffectInitParams initParams)
        {
            return ECSGameEffectFactory.Create(initParams, static (in i) => new ThermalSparkBurnECSEffect(i));
        }

        public override void ApplyEffectOnTarget(GameLiving target)
        {
            if (target == null || !target.IsAlive)
                return;

            // 1. Deal upfront Heat direct damage.
            OnDirectEffect(target);

            // 2. Apply the 6-second Heat burn DoT if the target survived the initial blast.
            if (target.IsAlive && target.effectListComponent != null && Spell.Value > 0)
            {
                int duration = Spell.Duration > 0 ? CalculateEffectDuration(target) : 6000;
                CreateECSEffect(new(target, duration, CalculateBuffDebuffEffectiveness(), this));
            }
        }

        public void DealBurnTickDamage(GameLiving target)
        {
            if (target == null || !target.IsAlive || target.ObjectState is not GameObject.eObjectState.Active)
                return;

            double effectiveness = CalculateDamageEffectiveness();
            double baseBurn = Spell.Value * effectiveness;
            double resistMod = 1.0 - (target.GetResist(Spell.DamageType) * 0.01);
            int damage = Math.Max(1, (int)Math.Round(baseBurn * resistMod));

            AttackData ad = new()
            {
                Attacker = Caster,
                Target = target,
                AttackType = AttackData.eAttackType.Spell,
                SpellHandler = this,
                AttackResult = eAttackResult.HitUnstyled,
                IsSpellResisted = false,
                Damage = damage,
                DamageType = Spell.DamageType,
                CausesCombat = false
            };

            CouncilWardenMechanics.ApplyFaeMagicWard(ad);
            CouncilWardenMechanics.AdjustIncomingSpellDamage(ad);

            if (Caster is GamePlayer playerCaster)
            {
                MessageToCaster($"Your {Spell.Name} burns {target.GetName(0, false)} for {ad.Damage} {Spell.DamageTypeToString()} damage!", eChatType.CT_Spell);
            }

            DamageTarget(ad, false);
        }
    }

    public class ThermalSparkBurnECSEffect : ECSGameSpellEffect
    {
        public ThermalSparkBurnECSEffect(in ECSGameEffectInitParams initParams) : base(initParams)
        {
            EffectType = eEffect.DamageOverTime;
            PulseFreq = SpellHandler?.Spell?.Frequency > 0 ? SpellHandler.Spell.Frequency : 2000;
            NextTick = StartTick + PulseFreq;
            // Ensure the final pulse at StartTick + Duration fires before expiration.
            ExpireTick = StartTick + Duration + 100;
        }

        public override void OnEffectPulse()
        {
            if (SpellHandler is CouncilWardenThermalSparkSpellHandler sparkHandler && Owner != null && Owner.IsAlive)
            {
                sparkHandler.DealBurnTickDamage(Owner);
            }
        }
    }

    // =========================================================================
    // 2. KINETIC LANCE (Piercing Kinetic Bolt + Barrier Shatter +25% Damage)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenKineticLance)]
    public class CouncilWardenKineticLanceSpellHandler : DirectDamageSpellHandler
    {
        private bool _shatteredBarrierOnCurrentTarget;

        public override string ShortDescription =>
            $"Launches a condensed lance of kinetic force for {Spell.Damage} {Spell.DamageTypeToString()} damage. Shatters active Ablative and Bladeturn barriers on the target to deal +25% bonus damage.";

        public CouncilWardenKineticLanceSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public static bool ShatterTargetBarriers(GameLiving caster, GameLiving target)
        {
            if (target?.effectListComponent == null)
                return false;

            bool shattered = false;
            var allEffects = target.effectListComponent.GetEffects();

            for (int i = 0; i < allEffects.Count; i++)
            {
                ECSGameEffect eff = allEffects[i];
                if (eff == null || !eff.IsActive)
                    continue;

                if (eff is AblativeArmorECSGameEffect ablative)
                {
                    ablative.RemainingValue = 0;
                    ablative.End(false);
                    shattered = true;
                }
                else if (eff.EffectType is eEffect.AblativeArmor or eEffect.Bladeturn)
                {
                    eff.End(false);
                    shattered = true;
                }
            }

            if (shattered)
            {
                if (caster is GamePlayer playerCaster)
                {
                    playerCaster.Out.SendMessage($"Your Kinetic Lance shatters {target.GetName(0, false)}'s protective barrier!", eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                }
                if (target is GamePlayer playerTarget)
                {
                    playerTarget.Out.SendMessage($"{caster?.GetName(0, true) ?? "A Council Warden"}'s Kinetic Lance shatters your protective barrier!", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                }
            }

            return shattered;
        }

        protected override void DealDamage(GameLiving target)
        {
            if (!target.IsAlive || target.ObjectState is not GameObject.eObjectState.Active)
                return;

            _shatteredBarrierOnCurrentTarget = ShatterTargetBarriers(Caster, target);
            try
            {
                base.DealDamage(target);
            }
            finally
            {
                _shatteredBarrierOnCurrentTarget = false;
            }
        }

        public override AttackData CalculateDamageToTarget(GameLiving target)
        {
            AttackData ad = base.CalculateDamageToTarget(target);
            if (_shatteredBarrierOnCurrentTarget)
            {
                ad.Damage = (int)Math.Round(ad.Damage * 1.25);
                if (ad.CriticalDamage > 0)
                    ad.CriticalDamage = (int)Math.Round(ad.CriticalDamage * 1.25);
            }
            return ad;
        }
    }

    // =========================================================================
    // 3. CONFLAGRATION (AoE Heat/Energy Blast + 10s Superheated Armor AF Debuff)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenConflagration)]
    public class CouncilWardenConflagrationSpellHandler : DirectDamageSpellHandler
    {
        public override string ShortDescription =>
            $"Engulfs the area in an explosive conflagration for {Spell.Damage} {Spell.DamageTypeToString()} damage and superheats enemy armor, reducing Armor Factor by {Spell.Value} for {(Spell.Duration > 0 ? Spell.Duration / 1000.0 : 10.0)} seconds.";

        public CouncilWardenConflagrationSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override ECSGameSpellEffect CreateECSEffect(in ECSGameEffectInitParams initParams)
        {
            return ECSGameEffectFactory.Create(initParams, static (in i) => new SuperheatAFDebuffECSEffect(i));
        }

        public override void ApplyEffectOnTarget(GameLiving target)
        {
            if (target == null || !target.IsAlive)
                return;

            // 1. Deal upfront AoE Heat/Energy direct damage.
            OnDirectEffect(target);

            // 2. Apply the 10-second Superheated Armor AF debuff.
            if (target.IsAlive && target.effectListComponent != null && Spell.Value > 0)
            {
                int duration = Spell.Duration > 0 ? CalculateEffectDuration(target) : 10000;
                CreateECSEffect(new(target, duration, CalculateBuffDebuffEffectiveness(), this));
            }
        }
    }

    public class SuperheatAFDebuffECSEffect : ECSGameSpellEffect
    {
        private int _appliedAfDebuff;

        public SuperheatAFDebuffECSEffect(in ECSGameEffectInitParams initParams) : base(initParams)
        {
            EffectType = eEffect.ArmorFactorDebuff;
        }

        public override void OnStartEffect()
        {
            if (Owner == null || SpellHandler?.Spell == null)
                return;

            _appliedAfDebuff = Math.Max(1, (int)Math.Round(SpellHandler.Spell.Value * Effectiveness));
            Owner.DebuffCategory[eProperty.ArmorFactor] += _appliedAfDebuff;

            if (Owner is GamePlayer player)
            {
                player.Out.SendUpdateWeaponAndArmorStats();
                player.Out.SendCharStatsUpdate();
                player.Out.SendMessage("Intense heat warps and superheats your armor, reducing your Armor Factor!", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
            }
        }

        public override void OnStopEffect()
        {
            if (Owner != null && _appliedAfDebuff != 0)
            {
                Owner.DebuffCategory[eProperty.ArmorFactor] -= _appliedAfDebuff;
                _appliedAfDebuff = 0;

                if (Owner is GamePlayer player)
                {
                    player.Out.SendUpdateWeaponAndArmorStats();
                    player.Out.SendCharStatsUpdate();
                }
            }
        }
    }

    // =========================================================================
    // 4. REPULSION WAVE (Instant PBAoE Matter Shockwave + Interrupt + 3s Stumble)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenRepulsionWave)]
    public class CouncilWardenRepulsionWaveSpellHandler : DirectDamageSpellHandler
    {
        public override string ShortDescription =>
            $"Unleashes an instant omnidirectional wave of kinetic force around the caster, inflicting {Spell.Damage} {Spell.DamageTypeToString()} damage, interrupting spellcasting, and stunning nearby enemies for {(Spell.Duration > 0 ? Spell.Duration / 1000.0 : 3.0)} seconds.";

        public CouncilWardenRepulsionWaveSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override ECSGameSpellEffect CreateECSEffect(in ECSGameEffectInitParams initParams)
        {
            return ECSGameEffectFactory.Create(initParams, static (in i) => new StunECSGameEffect(i));
        }

        public override void ApplyEffectOnTarget(GameLiving target)
        {
            if (target == null || !target.IsAlive)
                return;

            // 1. Deal upfront PBAoE Matter damage and interrupt casting.
            OnDirectEffect(target);
            target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);

            // 2. Apply the 3-second kinetic stumble (stun) if not stun-immune.
            if (target.IsAlive && target.effectListComponent != null)
            {
                if (EffectListService.GetEffectOnTarget(target, eEffect.StunImmunity) == null
                    && EffectListService.GetEffectOnTarget(target, eEffect.Stun) == null)
                {
                    int duration = Spell.Duration > 0 ? CalculateEffectDuration(target) : 3000;
                    CreateECSEffect(new(target, duration, CalculateBuffDebuffEffectiveness(), this));
                }
            }
        }
    }

    // =========================================================================
    // 5. SEVERANCE FIELD (15s 350-Radius Zone: -35% Cast Speed & +20% Fumble)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenSeveranceField)]
    public class CouncilWardenSeveranceFieldSpellHandler : SpellHandler
    {
        public override string ShortDescription =>
            $"Projects a 350-unit magnetic severance field for {Spell.Duration / 1000.0} seconds that disrupts hostile magic and balance, reducing enemy casting speed by 35% and increasing melee and spell fumble chance by 20%.";

        public CouncilWardenSeveranceFieldSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void FinishSpellCast(GameLiving target)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        public override ECSGameSpellEffect CreateECSEffect(in ECSGameEffectInitParams initParams)
        {
            return ECSGameEffectFactory.Create(initParams, static (in i) => new SeveranceFieldECSEffect(i));
        }

        public override void ApplyEffectOnTarget(GameLiving target)
        {
            if (target == null || !target.IsAlive || target.effectListComponent == null)
                return;

            int duration = Spell.Duration > 0 ? CalculateEffectDuration(target) : 15000;
            CreateECSEffect(new(target, duration, CalculateBuffDebuffEffectiveness(), this));
            target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
        }
    }

    public class SeveranceFieldECSEffect : ECSGameSpellEffect
    {
        private int _castSpeedDebuff;
        private int _fumbleDebuff;

        public SeveranceFieldECSEffect(in ECSGameEffectInitParams initParams) : base(initParams)
        {
            EffectType = eEffect.Unknown;
        }

        public override void OnStartEffect()
        {
            if (Owner == null)
                return;

            _castSpeedDebuff = 35;
            _fumbleDebuff = 20;

            Owner.DebuffCategory[eProperty.CastingSpeed] += _castSpeedDebuff;
            Owner.DebuffCategory[eProperty.FumbleChance] += _fumbleDebuff;
            Owner.DebuffCategory[eProperty.SpellFumbleChance] += _fumbleDebuff;

            if (Owner is GamePlayer player)
            {
                player.Out.SendMessage("A Severance Field warps the ambient currents around you, slowing your casting and disrupting your balance!", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
            }
        }

        public override void OnStopEffect()
        {
            if (Owner != null)
            {
                Owner.DebuffCategory[eProperty.CastingSpeed] -= _castSpeedDebuff;
                Owner.DebuffCategory[eProperty.FumbleChance] -= _fumbleDebuff;
                Owner.DebuffCategory[eProperty.SpellFumbleChance] -= _fumbleDebuff;
                _castSpeedDebuff = 0;
                _fumbleDebuff = 0;

                if (Owner is GamePlayer player)
                {
                    player.Out.SendMessage("The Severance Field fades.", eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                }
            }
        }
    }

    // =========================================================================
    // 6. SPELLSEVER (Lv 50 Melee Style Proc: Energy DD + Strip 1 Buff + 4s Interrupt)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenSpellsever)]
    public class CouncilWardenSpellseverSpellHandler : DirectDamageSpellHandler
    {
        public override string ShortDescription =>
            $"Severs magical threads on the target, inflicting {Spell.Damage} {Spell.DamageTypeToString()} damage, stripping 1 beneficial magical buff, and interrupting spellcasting for 4 seconds.";

        public CouncilWardenSpellseverSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public static bool StripOneBeneficialBuff(GameLiving caster, GameLiving target)
        {
            if (target?.effectListComponent == null)
                return false;

            var allSpellEffects = target.effectListComponent.GetSpellEffects();
            for (int i = 0; i < allSpellEffects.Count; i++)
            {
                ECSGameSpellEffect eff = allSpellEffects[i];
                if (eff == null || !eff.IsActive || !eff.HasPositiveEffect)
                    continue;

                if (eff is ECSPulseEffect || eff is TheSightECSEffect)
                    continue;

                Spell effSpell = eff.SpellHandler?.Spell;
                if (effSpell != null && effSpell.SpellType is eSpellType.PveResurrectionIllness or eSpellType.RvrResurrectionIllness)
                    continue;

                string strippedName = eff.Name;
                if (eff is AblativeArmorECSGameEffect ablative)
                    ablative.RemainingValue = 0;

                eff.End(false);

                if (caster is GamePlayer playerCaster)
                {
                    playerCaster.Out.SendMessage($"Your Spellsever slices through {target.GetName(0, false)}'s {strippedName}!", eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                }
                if (target is GamePlayer playerTarget)
                {
                    playerTarget.Out.SendMessage($"{caster?.GetName(0, true) ?? "A Council Warden"}'s Spellsever severs your {strippedName}!", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                }

                return true;
            }

            return false;
        }

        protected override void DealDamage(GameLiving target)
        {
            if (!target.IsAlive || target.ObjectState is not GameObject.eObjectState.Active)
                return;

            StripOneBeneficialBuff(Caster, target);
            AttackData ad = CalculateDamageToTarget(target);
            SendDamageMessages(ad);
            DamageTarget(ad, true);

            // 4-second spellcasting interrupt.
            target.StartInterruptTimer(4000, ad.AttackType, Caster);
        }
    }

    // =========================================================================
    // 7. OPENING THE SIGHT (Lv 30 Active Toggle: 1,000u True Vision / 2% Power Drain / +10% Magic Taken)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenTheSight)]
    public class CouncilWardenTheSightSpellHandler : SpellHandler
    {
        public override bool HasPositiveEffect => true;

        public override string ShortDescription =>
            "Toggles the Wizard's Sight: grants 1,000-unit true vision to detect all stealthed enemies and Fae entities, but drains 2% Power per pulse and increases magical damage taken by 10% while active.";

        public CouncilWardenTheSightSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void FinishSpellCast(GameLiving target)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        public override ECSGameSpellEffect CreateECSEffect(in ECSGameEffectInitParams initParams)
        {
            return ECSGameEffectFactory.Create(initParams, static (in i) => new TheSightECSEffect(i));
        }

        public override void ApplyEffectOnTarget(GameLiving target)
        {
            GameLiving owner = Caster;
            if (owner == null || !owner.IsAlive || owner.effectListComponent == null)
                return;

            // If The Sight is already open, casting again toggles it off.
            var effects = owner.effectListComponent.GetEffects();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is TheSightECSEffect existing && existing.IsActive)
                {
                    existing.End(false);
                    return;
                }
            }

            CreateECSEffect(new(owner, 3600000, 1.0, this));
        }
    }

    public class TheSightECSEffect : ECSGameSpellEffect
    {
        public TheSightECSEffect(in ECSGameEffectInitParams initParams) : base(initParams)
        {
            EffectType = eEffect.Unknown;
            PulseFreq = SpellHandler?.Spell?.Frequency > 0 ? SpellHandler.Spell.Frequency : 5000;
            NextTick = StartTick + PulseFreq;
        }

        public override void OnStartEffect()
        {
            if (Owner is GamePlayer player)
            {
                player.Out.SendMessage("You open your Third Eye. The indelible truth of the world blazes into view around you!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
            }
        }

        public override void OnEffectPulse()
        {
            if (Owner == null || !Owner.IsAlive)
            {
                End(false);
                return;
            }

            int powerDrain = Math.Max(1, (int)Math.Round(Owner.MaxMana * 0.02));
            if (Owner.Mana < powerDrain)
            {
                Owner.Mana = 0;
                if (Owner is GamePlayer player)
                {
                    player.Out.SendMessage("Your mental reserves are exhausted, forcing you to close your Third Eye.", eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                }
                End(false);
                return;
            }

            Owner.Mana -= powerDrain;
        }

        public override void OnStopEffect()
        {
            if (Owner is GamePlayer player)
            {
                player.Out.SendMessage("You close your Third Eye, shielding your mind from the overwhelming currents of the Sight.", eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
            }
        }
    }

    // =========================================================================
    // 8. THE WARDEN'S BRAND (30s Death Curse Mark) & IRON SICKNESS (8s Fae Debuff)
    // =========================================================================

    public class WardensBrandECSEffect : ECSGameSpellEffect
    {
        private const int RESIST_PENALTY = 15;

        public WardensBrandECSEffect(in ECSGameEffectInitParams initParams) : base(initParams)
        {
            EffectType = eEffect.Unknown;
        }

        public override void OnStartEffect()
        {
            if (Owner == null)
                return;

            // Break stealth immediately.
            if (Owner is GamePlayer playerOwner && playerOwner.IsStealthed)
                playerOwner.Stealth(false);

            // -15% to all magical resistances.
            Owner.DebuffCategory[eProperty.Resist_Body] += RESIST_PENALTY;
            Owner.DebuffCategory[eProperty.Resist_Cold] += RESIST_PENALTY;
            Owner.DebuffCategory[eProperty.Resist_Energy] += RESIST_PENALTY;
            Owner.DebuffCategory[eProperty.Resist_Heat] += RESIST_PENALTY;
            Owner.DebuffCategory[eProperty.Resist_Matter] += RESIST_PENALTY;
            Owner.DebuffCategory[eProperty.Resist_Spirit] += RESIST_PENALTY;

            // -25% movement speed.
            Owner.BuffBonusMultCategory1.Set((int)eProperty.MaxSpeed, this, 0.75);
            Owner.OnMaxSpeedChange();

            if (Owner is GamePlayer player)
            {
                player.Out.SendCharResistsUpdate();
                player.Out.SendMessage("You have been marked by a Council Warden's Death Curse! Your resistances and movement falter, and you cannot hide!", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
            }
        }

        public override void OnStopEffect()
        {
            if (Owner != null)
            {
                Owner.DebuffCategory[eProperty.Resist_Body] -= RESIST_PENALTY;
                Owner.DebuffCategory[eProperty.Resist_Cold] -= RESIST_PENALTY;
                Owner.DebuffCategory[eProperty.Resist_Energy] -= RESIST_PENALTY;
                Owner.DebuffCategory[eProperty.Resist_Heat] -= RESIST_PENALTY;
                Owner.DebuffCategory[eProperty.Resist_Matter] -= RESIST_PENALTY;
                Owner.DebuffCategory[eProperty.Resist_Spirit] -= RESIST_PENALTY;

                Owner.BuffBonusMultCategory1.Remove((int)eProperty.MaxSpeed, this);
                Owner.OnMaxSpeedChange();

                if (Owner is GamePlayer player)
                {
                    player.Out.SendCharResistsUpdate();
                    player.Out.SendMessage("The Warden's Brand finally fades from your aura.", eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                }
            }
        }
    }

    public class IronSicknessECSEffect : ECSGameSpellEffect
    {
        public bool SuppressHealthRegen { get; set; }

        public IronSicknessECSEffect(in ECSGameEffectInitParams initParams, bool suppressHealthRegen) : base(initParams)
        {
            EffectType = eEffect.Unknown;
            SuppressHealthRegen = suppressHealthRegen;
        }
    }

    // =========================================================================
    // 9. VINCULUM (Thaumaturgy Spec: 25% Damage Reflection)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenVinculum)]
    public class CouncilWardenVinculumSpellHandler : SpellHandler
    {
        public override bool HasPositiveEffect => true;

        public override string ShortDescription =>
            "Wreathes the Warden in a sympathetic lattice that reflects 25% of all incoming damage back upon the attacker as Energy damage.";

        public CouncilWardenVinculumSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void FinishSpellCast(GameLiving target)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        public override ECSGameSpellEffect CreateECSEffect(in ECSGameEffectInitParams initParams)
        {
            return ECSGameEffectFactory.Create(initParams, static (in i) => new VinculumECSEffect(i));
        }

        public override void ApplyEffectOnTarget(GameLiving target)
        {
            GameLiving owner = Caster;
            if (owner == null || !owner.IsAlive || owner.effectListComponent == null)
                return;

            int duration = Spell.Duration > 0 ? Spell.Duration * 1000 : 20000;
            CreateECSEffect(new(owner, duration, 1.0, this));
        }
    }

    public class VinculumECSEffect : ECSGameSpellEffect
    {
        public VinculumECSEffect(in ECSGameEffectInitParams initParams) : base(initParams)
        {
            EffectType = eEffect.Unknown;
        }

        public override void OnStartEffect()
        {
            if (Owner is GamePlayer player)
            {
                player.Out?.SendMessage("A shimmering silver vinculum binds around your aura, reflecting hostile attacks!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
            }
        }

        public override void OnStopEffect()
        {
            if (Owner is GamePlayer player)
            {
                player.Out?.SendMessage("Your Vinculum dissipates.", eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
            }
        }
    }

    // =========================================================================
    // 10. PROBABILITY HEX (Minor & Spec Thaumaturgy: Fumble & Casting Speed Debuff)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenProbabilityHex)]
    public class CouncilWardenProbabilityHexSpellHandler : SpellHandler
    {
        public override bool HasPositiveEffect => false;

        public override string ShortDescription =>
            $"Binds the target in hostile probability static, increasing weapon fumble chance and reducing combat casting and attack speed by {Spell.Value}%.";

        public CouncilWardenProbabilityHexSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void FinishSpellCast(GameLiving target)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        public override ECSGameSpellEffect CreateECSEffect(in ECSGameEffectInitParams initParams)
        {
            return ECSGameEffectFactory.Create(initParams, static (in i) => new ProbabilityHexECSEffect(i));
        }

        public override void ApplyEffectOnTarget(GameLiving target)
        {
            if (target == null || !target.IsAlive || target.effectListComponent == null)
                return;

            int duration = Spell.Duration > 0 ? Spell.Duration * 1000 : 15000;
            CreateECSEffect(new(target, duration, 1.0, this));
        }
    }

    public class ProbabilityHexECSEffect : ECSGameSpellEffect
    {
        private int _debuffValue;

        public ProbabilityHexECSEffect(in ECSGameEffectInitParams initParams) : base(initParams)
        {
            EffectType = eEffect.Unknown;
            _debuffValue = SpellHandler?.Spell?.Value > 0 ? (int)SpellHandler.Spell.Value : 20;
        }

        public override void OnStartEffect()
        {
            if (Owner == null)
                return;

            Owner.DebuffCategory[eProperty.MeleeSpeed] += _debuffValue;
            Owner.DebuffCategory[eProperty.CastingSpeed] += _debuffValue;

            if (Owner is GamePlayer player)
            {
                player.Out?.SendMessage("A probability hex settles over your hands! Your movements stammer and weapons fumble!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
            }
        }

        public override void OnStopEffect()
        {
            if (Owner != null)
            {
                Owner.DebuffCategory[eProperty.MeleeSpeed] -= _debuffValue;
                Owner.DebuffCategory[eProperty.CastingSpeed] -= _debuffValue;

                if (Owner is GamePlayer player)
                {
                    player.Out?.SendMessage("The probability hex clears from your aura.", eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                }
            }
        }
    }

    // =========================================================================
    // 11. CIRCLE OF CONTAINMENT (Thaumaturgy Spec: 350 Radius AoE Snare + Energy DD Pulse)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenCircleOfContainment)]
    public class CouncilWardenCircleOfContainmentSpellHandler : DamageSpeedDecreaseSpellHandler
    {
        public override string ShortDescription =>
            $"Erects a containment circle dealing {Spell.Damage} Energy damage and slowing enemy movement speed by {Spell.Value}% within a 350-unit radius.";

        public CouncilWardenCircleOfContainmentSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    // =========================================================================
    // 12. LEY-WELL INSCRIPTION (Minor Thaumaturgy: Stationary Mana Channel)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenLeyWell)]
    public class CouncilWardenLeyWellSpellHandler : SpellHandler
    {
        public override bool HasPositiveEffect => true;

        public override string ShortDescription =>
            $"Channels the local ley-line, restoring {Spell.Value} mana every 3 seconds while standing stationary.";

        public CouncilWardenLeyWellSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void FinishSpellCast(GameLiving target)
        {
            base.FinishSpellCast(target);
        }

        public override ECSGameSpellEffect CreateECSEffect(in ECSGameEffectInitParams initParams)
        {
            return ECSGameEffectFactory.Create(initParams, static (in i) => new LeyWellECSEffect(i));
        }

        public override void ApplyEffectOnTarget(GameLiving target)
        {
            GameLiving owner = Caster;
            if (owner == null || !owner.IsAlive || owner.effectListComponent == null)
                return;

            int duration = Spell.Duration > 0 ? Spell.Duration * 1000 : 30000;
            CreateECSEffect(new(owner, duration, 1.0, this));
        }
    }

    public class LeyWellECSEffect : ECSGameSpellEffect
    {
        private int _manaRestore;

        public LeyWellECSEffect(in ECSGameEffectInitParams initParams) : base(initParams)
        {
            EffectType = eEffect.Unknown;
            _manaRestore = SpellHandler?.Spell?.Value > 0 ? (int)SpellHandler.Spell.Value : 25;
            PulseFreq = SpellHandler?.Spell?.Frequency > 0 ? SpellHandler.Spell.Frequency : 3000;
            NextTick = StartTick + PulseFreq;
        }

        public override void OnStartEffect()
        {
            if (Owner is GamePlayer player)
            {
                player.Out?.SendMessage("You inscribe a ley-well into the earth, drawing upward currents of raw mana.", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
            }
        }

        public override void OnEffectPulse()
        {
            if (Owner == null || !Owner.IsAlive)
            {
                End(false);
                return;
            }

            if (Owner.IsMoving)
            {
                if (Owner is GamePlayer player)
                {
                    player.Out?.SendMessage("Movement breaks your communion with the ley-well.", eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                }
                End(false);
                return;
            }

            Owner.Mana = Math.Min(Owner.MaxMana, Owner.Mana + _manaRestore);
        }

        public override void OnStopEffect()
        {
            if (Owner is GamePlayer player)
            {
                player.Out?.SendMessage("The ley-well runs dormant.", eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
            }
        }
    }

    // =========================================================================
    // 13. LEY-LINE CONDUIT (Thaumaturgy Spec: Instant Mana Supercharge)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenLeyLineConduit)]
    public class CouncilWardenLeyLineConduitSpellHandler : SpellHandler
    {
        public override bool HasPositiveEffect => true;

        public override string ShortDescription =>
            $"Taps a deep subterranean ley-line conduit, instantly restoring {(Spell?.Value > 0 ? Spell.Value : 35):0}% of max mana.";

        public CouncilWardenLeyLineConduitSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void FinishSpellCast(GameLiving target)
        {
            base.FinishSpellCast(target);
        }

        public override void ApplyEffectOnTarget(GameLiving target)
        {
            GameLiving owner = Caster;
            if (owner == null || !owner.IsAlive)
                return;

            double restorePct = (Spell != null && Spell.Value > 0) ? Spell.Value / 100.0 : 0.35;
            int manaGain = Math.Max(1, (int)Math.Round(owner.MaxMana * restorePct));
            owner.Mana = Math.Min(owner.MaxMana, owner.Mana + manaGain);

            if (owner is GamePlayer player)
            {
                player.Out?.SendMessage($"A surging ley-conduit floods your aura, restoring {manaGain} mana!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
            }
        }
    }

    // =========================================================================
    // 14. SEVERING BOLT OF THE ABYSS (Legion's Severing Edge Charge: 185 Energy DD)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenSeveringBolt)]
    public class CouncilWardenSeveringBoltSpellHandler : DirectDamageSpellHandler
    {
        public override string ShortDescription =>
            "Unleashes a focused bolt of abyss-cleaving energy for 185 Energy damage.";

        public CouncilWardenSeveringBoltSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    // =========================================================================
    // 15. KINETIC DEFLECTION (Off-Hand Focus Reactive Proc: 100 pt Physical Ablative)
    // =========================================================================

    [SpellHandler(eSpellType.CouncilWardenKineticDeflection)]
    public class CouncilWardenKineticDeflectionSpellHandler : BothAblativeArmorSpellHandler
    {
        public override string ShortDescription =>
            "Your off-hand focus snaps a reactive kinetic deflection barrier into place, absorbing up to 100 physical damage.";

        public CouncilWardenKineticDeflectionSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
