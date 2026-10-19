"""Combat Simulation Engine for Council Warden Balance Verification.
Simulates 1v1 engagements under authentic DAoC combat formulas across:
- Run 1: Full Epic Quest Gear + SC/RoG baseline.
- Run 2: Full Raid / DF / Sidi / SH + SC endgame.

Tests Warden 1H Duelist and Council Wizard against:
- Heavy Shield Tank (Armsman / Warrior)
- Stealth Assassin (Infiltrator / Shadowblade)
- Pure Cloth Caster (Wizard / Eldritch)
- Traditional Hybrid (Paladin / Thane)
"""
import random
import math

class Combatant:
    def __init__(self, name, hp, af, absorb, resists, weapon_spd, parry, block, evade, 
                 mana=0, role="melee", damage_spec=50, casting_speed_mod=1.0):
        self.name = name
        self.max_hp = hp
        self.hp = hp
        self.af = af
        self.absorb = absorb       # 0.34 plate, 0.27 chain, 0.19 leather, 0.00 cloth
        self.resists = resists     # {"Slash": 0.26, "Energy": 0.26, "Heat": 0.26, ...}
        self.weapon_spd = weapon_spd
        self.parry = parry
        self.block = block
        self.evade = evade
        self.max_mana = mana
        self.mana = mana
        self.endurance = 100
        self.role = role
        self.damage_spec = damage_spec
        self.casting_speed_mod = casting_speed_mod
        
        # State tracking
        self.next_swing = random.uniform(0.1, 0.4)
        self.cast_finish = 0.0
        self.is_casting = False
        self.current_spell = None
        self.stun_until = 0.0
        self.stun_immune_until = 0.0
        self.snare_until = 0.0
        self.fumble_debuff_until = 0.0
        self.fumble_rate = 0.02
        
        # Defensive buffs / ablatives
        self.physical_ablative = 0      # Deflective Aegis (BothAblative, 50% absorb rate)
        self.magic_ablative = 0         # Spell Dispersion (MagicAblative, 75% absorb rate)
        self.magic_ablative_cd = 0.0
        self.vinculum_active_until = 0.0
        self.severing_charge_cd = 0.0   # Legion's Severing Edge 185 DD charge (Run 2)
        self.quickcast_used = False

def simulate_1v1(c1_factory, c2_factory, rounds=1000, scenario="melee_clash"):
    c1_wins = 0
    c2_wins = 0
    total_time = 0.0
    c1_end_mana = []
    c1_end_hp_pct = []
    c1_end_endurance = []

    for _ in range(rounds):
        c1 = c1_factory()
        c2 = c2_factory()
        t = 0.0
        dt = 0.05  # 50ms tick resolution

        # Pre-combat buffs
        if "Duelist" in c1.name:
            c1.physical_ablative = 450 if "Run 1" in c1.name else 550
        elif "Council Wizard" in c1.name:
            c1.physical_ablative = 450 if "Run 1" in c1.name else 550

        # Scenario initial distance / opening
        if scenario == "assassin_ambush":
            # Assassin opens from stealth with Perforate Artery
            # PA: Huge opening damage, 9s stun
            pa_raw = random.uniform(550, 720) * (1.0 - c1.absorb) * (1.0 - c1.resists.get("Thrust", 0.26))
            c1.hp -= int(pa_raw)
            c1.stun_until = 9.0
            c1.stun_immune_until = 69.0
            c1_distance = 0.0
            c2_distance = 0.0
        elif scenario == "ranged_clash":
            c1_distance = 0.0 if "Wizard" in c1.name else 1400.0
            c2_distance = 0.0 if "Cloth Caster" in c2.name else 1400.0
        else: # melee duel
            c1_distance = 0.0
            c2_distance = 0.0

        while c1.hp > 0 and c2.hp > 0 and t < 60.0:
            t += dt

            # Movement closing
            if c1_distance > 0:
                speed = 220.0 if t >= c1.snare_until else 110.0
                c1_distance = max(0.0, c1_distance - speed * dt)
            if c2_distance > 0:
                speed = 220.0 if t >= c2.snare_until else 110.0
                c2_distance = max(0.0, c2_distance - speed * dt)

            # --- C1 (Council Warden) Logic ---
            if t >= c1.stun_until:
                # 1. Duelist Logic
                if "Duelist" in c1.name:
                    # Legion's Severing Edge Use Charge (Run 2: 185 Energy DD instant, 1500 range)
                    if "Run 2" in c1.name and c1.severing_charge_cd <= t:
                        c1.severing_charge_cd = t + 90.0
                        charge_dmg = 185.0 * (1.0 - c2.resists.get("Energy", 0.26))
                        c2.hp -= int(charge_dmg)

                    # Reactive Spell Dispersion: if facing pure caster or magic damage, pop instant 750 magic ablative
                    if ("Caster" in c2.name or "Paladin" in c2.name) and c1.magic_ablative <= 0 and t >= c1.magic_ablative_cd and c1.mana >= 36:
                        c1.magic_ablative = 540 if "Run 1" in c1.name else 750
                        c1.magic_ablative_cd = t + 45.0
                        c1.mana -= 36

                    # Melee Swings
                    if c1_distance <= 0 and t >= c1.next_swing:
                        c1.next_swing = t + c1.weapon_spd
                        # Fumble check
                        fumble_p = 0.20 if t < c1.fumble_debuff_until else 0.02
                        if random.random() >= fumble_p:
                            # Defense checks against C2 (Evade -> Block -> Parry)
                            blocked = random.random() < c2.block
                            parried = not blocked and (random.random() < c2.parry)
                            evaded = not blocked and not parried and (random.random() < c2.evade)

                            if blocked and "Tank" in c2.name and t >= c2.stun_until and t >= c1.stun_immune_until and random.random() < 0.35:
                                # Shield tank counter: Slam (9s stun)
                                c1.stun_until = t + 9.0
                                c1.stun_immune_until = t + 69.0

                            if not (blocked or parried or evaded):
                                # Hit connects!
                                # Base unstyled damage: ~150-180 pre-resist
                                base_swing = random.uniform(150, 185) * (c1.weapon_spd / 3.6)
                                style_bonus = 0.0
                                proc_dmg = 0.0

                                # Style choice: Spellsever Anytime if endurance allows
                                if c1.endurance >= 25:
                                    c1.endurance -= 25
                                    # Spellsever: High growth rate + 110 Energy DD proc + interrupt + strip
                                    style_bonus = random.uniform(160, 210)
                                    proc_dmg = 110.0 * (1.0 - c2.resists.get("Energy", 0.26))
                                    # Interrupt caster
                                    if c2.is_casting:
                                        c2.is_casting = False
                                        c2.cast_finish = t + 1.5
                                else:
                                    # Low endurance unstyled or basic style
                                    style_bonus = random.uniform(30, 50) if c1.endurance >= 8 else 0.0
                                    c1.endurance = max(0, c1.endurance - 8)

                                # Physical mitigation
                                phys_raw = (base_swing + style_bonus) * (1.0 - c2.absorb) * (1.0 - c2.resists.get("Slash", 0.26))
                                total_hit = phys_raw + proc_dmg
                                c2.hp -= int(total_hit)

                # 2. Council Wizard (Thaumaturgy) Logic
                elif "Council Wizard" in c1.name:
                    # Spell Dispersion defense
                    if ("Caster" in c2.name) and c1.magic_ablative <= 0 and t >= c1.magic_ablative_cd and c1.mana >= 36:
                        c1.magic_ablative = 540 if "Run 1" in c1.name else 750
                        c1.magic_ablative_cd = t + 45.0
                        c1.mana -= 36

                    # If not currently casting, decide spell
                    if not c1.is_casting and t >= c1.cast_finish:
                        # 1. Maintain Vinculum (Damage Reflection 25%)
                        if t >= c1.vinculum_active_until and c1.mana >= 40:
                            c1.is_casting = True
                            c1.cast_finish = t + (2.0 * c1.casting_speed_mod)
                            c1.current_spell = "Vinculum"
                            c1.mana -= 40
                        # 2. Snare / Containment if enemy is closing and not snared
                        elif c2_distance > 0 and t >= c2.snare_until and c1.mana >= 28:
                            c1.is_casting = True
                            c1.cast_finish = t + (2.0 * c1.casting_speed_mod)
                            c1.current_spell = "Containment"
                            c1.mana -= 28
                        # 3. Probability Hex if enemy in melee range and not hexed
                        elif c2_distance <= 0 and t >= c2.fumble_debuff_until and c1.mana >= 30:
                            c1.is_casting = True
                            c1.cast_finish = t + (2.0 * c1.casting_speed_mod)
                            c1.current_spell = "ProbabilityHex"
                            c1.mana -= 30
                        # 4. Artillery DD: Sympathetic Rupture / Kinetic Lance
                        elif c1.mana >= 38:
                            c1.is_casting = True
                            c1.cast_finish = t + (2.4 * c1.casting_speed_mod)
                            c1.current_spell = "ArtilleryDD"
                            # Focus mana reduction: with Focus 50, costs 80% (30 mana instead of 38)
                            c1.mana -= 30

                    # Spell Completion Check
                    if c1.is_casting and t >= c1.cast_finish:
                        c1.is_casting = False
                        if c1.current_spell == "Vinculum":
                            c1.vinculum_active_until = t + 20.0
                        elif c1.current_spell == "Containment":
                            c2.snare_until = t + 12.0
                        elif c1.current_spell == "ProbabilityHex":
                            c2.fumble_debuff_until = t + 15.0
                        elif c1.current_spell == "ArtilleryDD":
                            base_nuke = random.uniform(310, 390)
                            nuke_dmg = base_nuke * (1.0 - c2.resists.get("Energy", 0.26))
                            c2.hp -= int(nuke_dmg)

            # --- C2 (Opponent) Logic ---
            if t >= c2.stun_until:
                # Opponent: Shield Tank
                if "Tank" in c2.name:
                    if c2_distance <= 0 and t >= c2.next_swing:
                        c2.next_swing = t + c2.weapon_spd
                        fumble_p = 0.20 if t < c2.fumble_debuff_until else 0.02
                        if random.random() >= fumble_p:
                            blocked = False # Warden has 0 block
                            parried = random.random() < c1.parry
                            evaded = not parried and (random.random() < c1.evade)

                            if not (parried or evaded):
                                base_swing = random.uniform(160, 200) * (c2.weapon_spd / 4.2)
                                style_bonus = random.uniform(140, 180) # Tank style
                                raw_dmg = (base_swing + style_bonus) * (1.0 - c1.absorb) * (1.0 - c1.resists.get("Slash", 0.26))

                                # Vinculum reflection
                                if t < c1.vinculum_active_until:
                                    c2.hp -= int(raw_dmg * 0.25)

                                # Physical ablative absorption
                                if c1.physical_ablative > 0:
                                    absorb_amt = min(c1.physical_ablative, raw_dmg * 0.50)
                                    c1.physical_ablative -= absorb_amt
                                    raw_dmg -= absorb_amt

                                c1.hp -= int(raw_dmg)
                                # Melee hits interrupt casting on wizard
                                if c1.is_casting:
                                    c1.is_casting = False
                                    c1.cast_finish = t + 1.0

                # Opponent: Stealth Assassin
                elif "Assassin" in c2.name:
                    if c2_distance <= 0 and t >= c2.next_swing:
                        c2.next_swing = t + c2.weapon_spd
                        fumble_p = 0.20 if t < c2.fumble_debuff_until else 0.02
                        if random.random() >= fumble_p:
                            blocked = False
                            parried = random.random() < c1.parry
                            evaded = not parried and (random.random() < c1.evade)

                            if not (parried or evaded):
                                # Fast dual-wield chain: main hand + offhand
                                dw_swing = random.uniform(180, 240)
                                style_bonus = random.uniform(120, 160)
                                raw_dmg = (dw_swing + style_bonus) * (1.0 - c1.absorb) * (1.0 - c1.resists.get("Slash", 0.26))

                                if t < c1.vinculum_active_until:
                                    c2.hp -= int(raw_dmg * 0.25)

                                if c1.physical_ablative > 0:
                                    absorb_amt = min(c1.physical_ablative, raw_dmg * 0.50)
                                    c1.physical_ablative -= absorb_amt
                                    raw_dmg -= absorb_amt

                                c1.hp -= int(raw_dmg)
                                if c1.is_casting:
                                    c1.is_casting = False
                                    c1.cast_finish = t + 1.0

                # Opponent: Pure Cloth Caster (Wizard)
                elif "Cloth Caster" in c2.name:
                    if not c2.is_casting and t >= c2.cast_finish:
                        c2.is_casting = True
                        cast_duration = 2.2 * c2.casting_speed_mod
                        if not c2.quickcast_used:
                            c2.quickcast_used = True
                            cast_duration = 1.6 # Fast uninterruptible first nuke
                        c2.cast_finish = t + cast_duration

                    if c2.is_casting and t >= c2.cast_finish:
                        c2.is_casting = False
                        nuke_raw = random.uniform(420, 520) * (1.0 - c1.resists.get("Heat", 0.26))
                        
                        # Spell Dispersion ablative check (75% absorb)
                        if c1.magic_ablative > 0:
                            absorbed = min(c1.magic_ablative, nuke_raw * 0.75)
                            c1.magic_ablative -= absorbed
                            nuke_raw -= absorbed

                        c1.hp -= int(nuke_raw)

                # Opponent: Paladin Hybrid
                elif "Paladin" in c2.name:
                    if c2_distance <= 0 and t >= c2.next_swing:
                        c2.next_swing = t + c2.weapon_spd
                        fumble_p = 0.20 if t < c2.fumble_debuff_until else 0.02
                        if random.random() >= fumble_p:
                            blocked = False
                            parried = random.random() < c1.parry
                            evaded = not parried and (random.random() < c1.evade)

                            if not (parried or evaded):
                                base_swing = random.uniform(150, 185) * (c2.weapon_spd / 3.8)
                                style_bonus = random.uniform(120, 150)
                                raw_dmg = (base_swing + style_bonus) * (1.0 - c1.absorb) * (1.0 - c1.resists.get("Slash", 0.26))

                                # Chant damage add (Spirit/Holy DD)
                                chant_dmg = random.uniform(45, 65) * (1.0 - c1.resists.get("Spirit", 0.26))

                                if t < c1.vinculum_active_until:
                                    c2.hp -= int((raw_dmg + chant_dmg) * 0.25)

                                if c1.physical_ablative > 0:
                                    absorb_amt = min(c1.physical_ablative, raw_dmg * 0.50)
                                    c1.physical_ablative -= absorb_amt
                                    raw_dmg -= absorb_amt

                                c1.hp -= int(raw_dmg + chant_dmg)
                                if c1.is_casting:
                                    c1.is_casting = False
                                    c1.cast_finish = t + 1.0

        if c1.hp > 0 and c2.hp <= 0:
            c1_wins += 1
            c1_end_hp_pct.append(c1.hp / c1.max_hp)
            c1_end_mana.append(c1.mana / max(1, c1.max_mana))
            c1_end_endurance.append(c1.endurance / 100.0)
        elif c2.hp > 0 and c1.hp <= 0:
            c2_wins += 1

        total_time += t

    win_rate = (c1_wins / rounds) * 100
    avg_hp = (sum(c1_end_hp_pct) / len(c1_end_hp_pct) * 100) if c1_end_hp_pct else 0
    avg_mana = (sum(c1_end_mana) / len(c1_end_mana) * 100) if c1_end_mana else 0
    avg_end = (sum(c1_end_endurance) / len(c1_end_endurance) * 100) if c1_end_endurance else 0
    return win_rate, avg_hp, avg_mana, avg_end, total_time / rounds

# --- RUN 1 FACTORIES (Full Epic Quest Gear + SC/RoG Baseline) ---
def w_duelist_r1():
    return Combatant("Warden Duelist (Run 1)", hp=1850, af=360, absorb=0.27, 
                     resists={"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26}, 
                     weapon_spd=3.6, parry=0.28, block=0.0, evade=0.12, mana=520, role="melee")

def w_wizard_r1():
    return Combatant("Council Wizard (Run 1)", hp=1700, af=340, absorb=0.27, 
                     resists={"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26}, 
                     weapon_spd=4.0, parry=0.10, block=0.0, evade=0.10, mana=780, role="caster", casting_speed_mod=1.0)

def tank_r1():
    return Combatant("Shield Tank (Run 1)", hp=2250, af=420, absorb=0.34, 
                     resists={"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26}, 
                     weapon_spd=4.2, parry=0.18, block=0.45, evade=0.05)

def assassin_r1():
    return Combatant("Stealth Assassin (Run 1)", hp=1650, af=280, absorb=0.19, 
                     resists={"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26}, 
                     weapon_spd=3.0, parry=0.15, block=0.0, evade=0.32)

def caster_r1():
    return Combatant("Cloth Caster (Run 1)", hp=1450, af=120, absorb=0.00, 
                     resists={"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26}, 
                     weapon_spd=4.0, parry=0.0, block=0.0, evade=0.05, mana=1200, role="caster", casting_speed_mod=1.0)

def hybrid_paladin_r1():
    return Combatant("Paladin Hybrid (Run 1)", hp=2100, af=410, absorb=0.34, 
                     resists={"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26}, 
                     weapon_spd=3.8, parry=0.16, block=0.36, evade=0.05)

# --- RUN 2 FACTORIES (Endgame Raid: Sidi / Tuscaran / Galladoria + DF + SH + SC) ---
def w_duelist_r2():
    return Combatant("Warden Duelist (Run 2)", hp=2200, af=410, absorb=0.27, 
                     resists={"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31}, 
                     weapon_spd=3.6, parry=0.32, block=0.0, evade=0.14, mana=650, role="melee")

def w_wizard_r2():
    return Combatant("Council Wizard (Run 2)", hp=2000, af=380, absorb=0.27, 
                     resists={"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31}, 
                     weapon_spd=4.0, parry=0.12, block=0.0, evade=0.12, mana=950, role="caster", casting_speed_mod=0.88)

def tank_r2():
    return Combatant("Shield Tank (Run 2)", hp=2650, af=470, absorb=0.34, 
                     resists={"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31}, 
                     weapon_spd=4.2, parry=0.20, block=0.50, evade=0.06)

def assassin_r2():
    return Combatant("Stealth Assassin (Run 2)", hp=1950, af=310, absorb=0.19, 
                     resists={"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31}, 
                     weapon_spd=3.0, parry=0.18, block=0.0, evade=0.36)

def caster_r2():
    return Combatant("Cloth Caster (Run 2)", hp=1700, af=140, absorb=0.00, 
                     resists={"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31}, 
                     weapon_spd=4.0, parry=0.0, block=0.0, evade=0.06, mana=1450, role="caster", casting_speed_mod=0.88)

def hybrid_paladin_r2():
    return Combatant("Paladin Hybrid (Run 2)", hp=2450, af=460, absorb=0.34, 
                     resists={"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31}, 
                     weapon_spd=3.8, parry=0.18, block=0.40, evade=0.06)

matchups_r1 = [
    ("Warden Duelist vs Shield Tank (Melee Duel)", w_duelist_r1, tank_r1, "melee_clash"),
    ("Warden Duelist vs Stealth Assassin (Ambush)", w_duelist_r1, assassin_r1, "assassin_ambush"),
    ("Warden Duelist vs Cloth Caster (Ranged Start)", w_duelist_r1, caster_r1, "ranged_clash"),
    ("Warden Duelist vs Paladin Hybrid (Melee Duel)", w_duelist_r1, hybrid_paladin_r1, "melee_clash"),
    ("Council Wizard vs Shield Tank (Field Clash)", w_wizard_r1, tank_r1, "ranged_clash"),
    ("Council Wizard vs Stealth Assassin (Ambush)", w_wizard_r1, assassin_r1, "assassin_ambush"),
    ("Council Wizard vs Cloth Caster (Artillery Duel)", w_wizard_r1, caster_r1, "ranged_clash"),
    ("Council Wizard vs Paladin Hybrid (Field Clash)", w_wizard_r1, hybrid_paladin_r1, "ranged_clash"),
]

matchups_r2 = [
    ("Warden Duelist vs Shield Tank (Melee Duel)", w_duelist_r2, tank_r2, "melee_clash"),
    ("Warden Duelist vs Stealth Assassin (Ambush)", w_duelist_r2, assassin_r2, "assassin_ambush"),
    ("Warden Duelist vs Cloth Caster (Ranged Start)", w_duelist_r2, caster_r2, "ranged_clash"),
    ("Warden Duelist vs Paladin Hybrid (Melee Duel)", w_duelist_r2, hybrid_paladin_r2, "melee_clash"),
    ("Council Wizard vs Shield Tank (Field Clash)", w_wizard_r2, tank_r2, "ranged_clash"),
    ("Council Wizard vs Stealth Assassin (Ambush)", w_wizard_r2, assassin_r2, "assassin_ambush"),
    ("Council Wizard vs Cloth Caster (Artillery Duel)", w_wizard_r2, caster_r2, "ranged_clash"),
    ("Council Wizard vs Paladin Hybrid (Field Clash)", w_wizard_r2, hybrid_paladin_r2, "ranged_clash"),
]

print("=== RUN 1 SIMULATION RESULTS (EPIC QUEST GEAR + SC BASELINE) ===")
print(f"{'Matchup':50} | {'Win Rate':8} | {'Avg Rem HP':10} | {'Rem Mana':9} | {'Rem End':8} | {'Avg TTK':7}")
print("-" * 105)
for name, c1_f, c2_f, scn in matchups_r1:
    wr, hp, mana, end, dur = simulate_1v1(c1_f, c2_f, rounds=1000, scenario=scn)
    print(f"{name:50} | {wr:6.1f}%  | {hp:8.1f}%  | {mana:7.1f}%  | {end:6.1f}%  | {dur:5.1f}s")

print("\n=== RUN 2 SIMULATION RESULTS (RAID / DF / SIDI / SH + SC ENDGAME) ===")
print(f"{'Matchup':50} | {'Win Rate':8} | {'Avg Rem HP':10} | {'Rem Mana':9} | {'Rem End':8} | {'Avg TTK':7}")
print("-" * 105)
for name, c1_f, c2_f, scn in matchups_r2:
    wr, hp, mana, end, dur = simulate_1v1(c1_f, c2_f, rounds=1000, scenario=scn)
    print(f"{name:50} | {wr:6.1f}%  | {hp:8.1f}%  | {mana:7.1f}%  | {end:6.1f}%  | {dur:5.1f}s")

