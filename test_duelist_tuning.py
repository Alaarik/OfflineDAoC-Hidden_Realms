import random

class Combatant:
    def __init__(self, name, hp, af, absorb, resists, weapon_spd, parry, block, evade, 
                 mana=0, role="melee", casting_speed_mod=1.0):
        self.name = name
        self.max_hp = hp
        self.hp = hp
        self.af = af
        self.absorb = absorb
        self.resists = resists
        self.weapon_spd = weapon_spd
        self.parry = parry
        self.block = block
        self.evade = evade
        self.max_mana = mana
        self.mana = mana
        self.endurance = 100
        self.role = role
        self.casting_speed_mod = casting_speed_mod
        
        self.next_swing = random.uniform(0.1, 0.4)
        self.cast_finish = 0.0
        self.is_casting = False
        self.current_spell = None
        self.stun_until = 0.0
        self.stun_immune_until = 0.0
        self.snare_until = 0.0
        self.fumble_debuff_until = 0.0
        
        self.physical_ablative = 0
        self.magic_ablative = 0
        self.magic_ablative_cd = 0.0
        self.severing_charge_cd = 0.0
        self.repulsion_cd = 0.0
        self.parry_riposte_ready = False
        self.quickcast_used = False

def simulate_duelist_v2(duelist_factory, opponent_factory, rounds=1000, scenario="melee_clash", 
                        use_full_kit=True, spellsever_end=16):
    wins = 0
    total_time = 0.0
    end_hp = []
    end_mana = []
    end_end = []

    for _ in range(rounds):
        c1 = duelist_factory()
        c2 = opponent_factory()
        t = 0.0
        dt = 0.05

        c1.physical_ablative = 450 if "Run 1" in c1.name else 550

        if scenario == "assassin_ambush":
            pa_raw = random.uniform(550, 720) * (1.0 - c1.absorb) * (1.0 - c1.resists.get("Thrust", 0.26))
            c1.hp -= int(pa_raw)
            c1.stun_until = 9.0
            c1.stun_immune_until = 69.0
            c1_dist = 0.0
            c2_dist = 0.0
        elif scenario == "ranged_clash":
            c1_dist = 1400.0
            c2_dist = 0.0 if "Cloth Caster" in c2.name else 1400.0
        else:
            c1_dist = 0.0
            c2_dist = 0.0

        thermal_spark_ticks = [] # (tick_time, dmg)

        while c1.hp > 0 and c2.hp > 0 and t < 60.0:
            t += dt

            # Handle DoT ticks on C2
            for tick_t, dmg in list(thermal_spark_ticks):
                if t >= tick_t:
                    c2.hp -= dmg
                    thermal_spark_ticks.remove((tick_t, dmg))

            # Movement closing
            if c1_dist > 0:
                speed = 220.0 if t >= c1.snare_until else 110.0
                c1_dist = max(0.0, c1_dist - speed * dt)
            if c2_dist > 0:
                speed = 220.0 if t >= c2.snare_until else 110.0
                c2_dist = max(0.0, c2_dist - speed * dt)

            # C1 Duelist
            if t >= c1.stun_until:
                # 1. Severing Bolt charge (Run 2)
                if use_full_kit and "Run 2" in c1.name and c1.severing_charge_cd <= t:
                    c1.severing_charge_cd = t + 90.0
                    charge_dmg = 185.0 * (1.0 - c2.resists.get("Energy", 0.26))
                    c2.hp -= int(charge_dmg)

                # 2. Reactive Spell Dispersion against casters
                if ("Caster" in c2.name or "Paladin" in c2.name) and c1.magic_ablative <= 0 and t >= c1.magic_ablative_cd and c1.mana >= 36:
                    c1.magic_ablative = 540 if "Run 1" in c1.name else 750
                    c1.magic_ablative_cd = t + 45.0
                    c1.mana -= 36

                # 3. Repulsion Wave (instant PBAoE 3s stun/interrupt, 30s cd)
                if use_full_kit and c1_dist <= 0 and t >= c1.repulsion_cd and c1.mana >= 38:
                    c1.repulsion_cd = t + 30.0
                    c1.mana -= 38
                    rep_dmg = random.uniform(180, 225) * (1.0 - c2.resists.get("Matter", 0.26))
                    c2.hp -= int(rep_dmg)
                    if t >= c2.stun_immune_until:
                        c2.stun_until = max(c2.stun_until, t + 3.0)
                        c2.stun_immune_until = t + 63.0
                    if c2.is_casting:
                        c2.is_casting = False
                        c2.cast_finish = t + 1.5

                # 4. Spell Weaving (CanCastWhileAttacking): Weave Thermal Spark if mana permits
                if use_full_kit and not c1.is_casting and t >= c1.cast_finish and c1.mana >= 30:
                    c1.is_casting = True
                    c1.cast_finish = t + (2.0 * c1.casting_speed_mod)
                    c1.current_spell = "ThermalSpark"
                    c1.mana -= 24 # with Focus 50

                if c1.is_casting and t >= c1.cast_finish:
                    c1.is_casting = False
                    if c1.current_spell == "ThermalSpark":
                        # 180 Heat DD upfront + 46 every 2s for 6s
                        heat_nuke = 180.0 * (1.0 - c2.resists.get("Heat", 0.26))
                        c2.hp -= int(heat_nuke)
                        burn_dmg = int(46.0 * (1.0 - c2.resists.get("Heat", 0.26)))
                        thermal_spark_ticks.append((t + 2.0, burn_dmg))
                        thermal_spark_ticks.append((t + 4.0, burn_dmg))
                        thermal_spark_ticks.append((t + 6.0, burn_dmg))

                # 5. Melee Swings
                if c1_dist <= 0 and t >= c1.next_swing:
                    c1.next_swing = t + c1.weapon_spd
                    fumble_p = 0.20 if t < c1.fumble_debuff_until else 0.02
                    if random.random() >= fumble_p:
                        blocked = random.random() < c2.block
                        parried = not blocked and (random.random() < c2.parry)
                        evaded = not blocked and not parried and (random.random() < c2.evade)

                        if blocked and "Tank" in c2.name and t >= c2.stun_until and t >= c1.stun_immune_until and random.random() < 0.35:
                            c1.stun_until = t + 9.0
                            c1.stun_immune_until = t + 69.0

                        if not (blocked or parried or evaded):
                            base_swing = random.uniform(150, 185) * (c1.weapon_spd / 3.6)
                            style_bonus = 0.0
                            proc_dmg = 0.0

                            # Style Selection
                            if use_full_kit and c1.parry_riposte_ready and c1.endurance >= 5:
                                # Riposte of Force (3s stun on parry counter, 5% end)
                                c1.parry_riposte_ready = False
                                c1.endurance -= 5
                                style_bonus = random.uniform(120, 150)
                                if t >= c2.stun_immune_until:
                                    c2.stun_until = max(c2.stun_until, t + 3.0)
                                    c2.stun_immune_until = t + 63.0
                            elif c1.endurance >= spellsever_end:
                                c1.endurance -= spellsever_end
                                style_bonus = random.uniform(160, 210)
                                proc_dmg = 110.0 * (1.0 - c2.resists.get("Energy", 0.26))
                                if c2.is_casting:
                                    c2.is_casting = False
                                    c2.cast_finish = t + 1.5
                            elif c1.endurance >= 10:
                                # Silver Arc anytime (10% end)
                                c1.endurance -= 10
                                style_bonus = random.uniform(70, 95)
                                proc_dmg = 35.0 * (1.0 - c2.resists.get("Energy", 0.26)) # Kinetic Shock proc
                            else:
                                c1.endurance = max(0, c1.endurance - 4)

                            # Kinetic Infusion Damage Add (+50 energy dmg per swing if full kit)
                            dmg_add = (50.0 * (1.0 - c2.resists.get("Energy", 0.26))) if use_full_kit else 0.0

                            phys_raw = (base_swing + style_bonus) * (1.0 - c2.absorb) * (1.0 - c2.resists.get("Slash", 0.26))
                            c2.hp -= int(phys_raw + proc_dmg + dmg_add)

            # C2 Logic
            if t >= c2.stun_until:
                if "Tank" in c2.name:
                    if c2_dist <= 0 and t >= c2.next_swing:
                        c2.next_swing = t + c2.weapon_spd
                        fumble_p = 0.20 if t < c2.fumble_debuff_until else 0.02
                        if random.random() >= fumble_p:
                            parried = random.random() < c1.parry
                            evaded = not parried and (random.random() < c1.evade)
                            if parried:
                                c1.parry_riposte_ready = True
                            if not (parried or evaded):
                                base_swing = random.uniform(160, 200) * (c2.weapon_spd / 4.2)
                                style_bonus = random.uniform(140, 180)
                                raw_dmg = (base_swing + style_bonus) * (1.0 - c1.absorb) * (1.0 - c1.resists.get("Slash", 0.26))
                                if c1.physical_ablative > 0:
                                    abs_amt = min(c1.physical_ablative, raw_dmg * 0.50)
                                    c1.physical_ablative -= abs_amt
                                    raw_dmg -= abs_amt
                                c1.hp -= int(raw_dmg)

                elif "Assassin" in c2.name:
                    if c2_dist <= 0 and t >= c2.next_swing:
                        c2.next_swing = t + c2.weapon_spd
                        fumble_p = 0.20 if t < c2.fumble_debuff_until else 0.02
                        if random.random() >= fumble_p:
                            parried = random.random() < c1.parry
                            evaded = not parried and (random.random() < c1.evade)
                            if parried:
                                c1.parry_riposte_ready = True
                            if not (parried or evaded):
                                dw_swing = random.uniform(180, 240)
                                style_bonus = random.uniform(120, 160)
                                raw_dmg = (dw_swing + style_bonus) * (1.0 - c1.absorb) * (1.0 - c1.resists.get("Slash", 0.26))
                                if c1.physical_ablative > 0:
                                    abs_amt = min(c1.physical_ablative, raw_dmg * 0.50)
                                    c1.physical_ablative -= abs_amt
                                    raw_dmg -= abs_amt
                                c1.hp -= int(raw_dmg)

                elif "Cloth Caster" in c2.name:
                    if not c2.is_casting and t >= c2.cast_finish:
                        c2.is_casting = True
                        cast_duration = 2.2 * c2.casting_speed_mod
                        if not c2.quickcast_used:
                            c2.quickcast_used = True
                            cast_duration = 1.6
                        c2.cast_finish = t + cast_duration

                    if c2.is_casting and t >= c2.cast_finish:
                        c2.is_casting = False
                        nuke_raw = random.uniform(420, 520) * (1.0 - c1.resists.get("Heat", 0.26))
                        if c1.magic_ablative > 0:
                            absorbed = min(c1.magic_ablative, nuke_raw * 0.75)
                            c1.magic_ablative -= absorbed
                            nuke_raw -= absorbed
                        c1.hp -= int(nuke_raw)

                elif "Paladin" in c2.name:
                    if c2_dist <= 0 and t >= c2.next_swing:
                        c2.next_swing = t + c2.weapon_spd
                        fumble_p = 0.20 if t < c2.fumble_debuff_until else 0.02
                        if random.random() >= fumble_p:
                            parried = random.random() < c1.parry
                            evaded = not parried and (random.random() < c1.evade)
                            if parried:
                                c1.parry_riposte_ready = True
                            if not (parried or evaded):
                                base_swing = random.uniform(150, 185) * (c2.weapon_spd / 3.8)
                                style_bonus = random.uniform(120, 150)
                                raw_dmg = (base_swing + style_bonus) * (1.0 - c1.absorb) * (1.0 - c1.resists.get("Slash", 0.26))
                                chant_dmg = random.uniform(45, 65) * (1.0 - c1.resists.get("Spirit", 0.26))
                                if c1.physical_ablative > 0:
                                    abs_amt = min(c1.physical_ablative, raw_dmg * 0.50)
                                    c1.physical_ablative -= abs_amt
                                    raw_dmg -= abs_amt
                                c1.hp -= int(raw_dmg + chant_dmg)

        if c1.hp > 0 and c2.hp <= 0:
            wins += 1
            end_hp.append(c1.hp / c1.max_hp)
            end_mana.append(c1.mana / max(1, c1.max_mana))
            end_end.append(c1.endurance / 100.0)
        total_time += t

    wr = (wins / rounds) * 100
    avg_hp = (sum(end_hp) / len(end_hp) * 100) if end_hp else 0
    avg_mana = (sum(end_mana) / len(end_mana) * 100) if end_mana else 0
    avg_end = (sum(end_end) / len(end_end) * 100) if end_end else 0
    return wr, avg_hp, avg_mana, avg_end, total_time / rounds

# Run 2 factories
def w_duelist_r2():
    return Combatant("Warden Duelist (Run 2)", 2200, 410, 0.27, {"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31, "Matter":0.31}, 3.6, 0.32, 0.0, 0.14, mana=650, role="melee", casting_speed_mod=0.90)

def tank_r2():
    return Combatant("Shield Tank (Run 2)", 2650, 470, 0.34, {"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31, "Matter":0.31}, 4.2, 0.20, 0.50, 0.06)

def assassin_r2():
    return Combatant("Stealth Assassin (Run 2)", 1950, 310, 0.19, {"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31, "Matter":0.31}, 3.0, 0.18, 0.0, 0.36)

def caster_r2():
    return Combatant("Cloth Caster (Run 2)", 1700, 140, 0.00, {"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31, "Matter":0.31}, 4.0, 0.0, 0.0, 0.06, mana=1450, role="caster", casting_speed_mod=0.88)

def paladin_r2():
    return Combatant("Paladin Hybrid (Run 2)", 2450, 460, 0.34, {"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31, "Matter":0.31}, 3.8, 0.18, 0.40, 0.06)

print("--- COMPARISON: BASELINE DUELIST vs FULL-KIT SPELL-WEAVING DUELIST (RUN 2 ENDGAME) ---")
for opp_name, opp_factory, scn in [
    ("Shield Tank", tank_r2, "melee_clash"),
    ("Stealth Assassin", assassin_r2, "assassin_ambush"),
    ("Cloth Caster", caster_r2, "ranged_clash"),
    ("Paladin Hybrid", paladin_r2, "melee_clash"),
]:
    # Baseline (25% end, no spells, no damage add, no repulsion)
    wr0, hp0, m0, e0, ttk0 = simulate_duelist_v2(w_duelist_r2, opp_factory, rounds=1000, scenario=scn, use_full_kit=False, spellsever_end=25)
    # Full kit (16% end, spell weaving, Damage Add, Repulsion Wave, Riposte)
    wr1, hp1, m1, e1, ttk1 = simulate_duelist_v2(w_duelist_r2, opp_factory, rounds=1000, scenario=scn, use_full_kit=True, spellsever_end=16)
    print(f"vs {opp_name:18} | Baseline WR: {wr0:5.1f}% (TTK {ttk0:4.1f}s) -> Full Kit WR: {wr1:5.1f}% (TTK {ttk1:4.1f}s, RemHP {hp1:4.1f}%, RemMana {m1:4.1f}%)")

