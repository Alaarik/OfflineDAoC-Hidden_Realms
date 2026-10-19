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

def simulate_matchup(duelist_factory, opp_factory, rounds=1000, scenario="melee_clash",
                      dmg_add_active=True, spellsever_end=18, allow_parry_riposte=True,
                      use_repulsion_wave=True, weave_spells=False):
    wins = 0
    total_time = 0.0
    end_hp = []
    end_mana = []
    end_end = []

    for _ in range(rounds):
        c1 = duelist_factory()
        c2 = opp_factory()
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

        while c1.hp > 0 and c2.hp > 0 and t < 60.0:
            t += dt

            # Movement closing
            if c1_dist > 0:
                speed = 220.0 if t >= c1.snare_until else 110.0
                c1_dist = max(0.0, c1_dist - speed * dt)
            if c2_dist > 0:
                speed = 220.0 if t >= c2.snare_until else 110.0
                c2_dist = max(0.0, c2_dist - speed * dt)

            # C1 Duelist
            if t >= c1.stun_until:
                # Run 2 weapon charge
                if "Run 2" in c1.name and c1.severing_charge_cd <= t:
                    c1.severing_charge_cd = t + 90.0
                    charge_dmg = 185.0 * (1.0 - c2.resists.get("Energy", 0.26))
                    c2.hp -= int(charge_dmg)

                # Reactive Spell Dispersion
                if ("Caster" in c2.name or "Paladin" in c2.name) and c1.magic_ablative <= 0 and t >= c1.magic_ablative_cd and c1.mana >= 36:
                    c1.magic_ablative = 540 if "Run 1" in c1.name else 750
                    c1.magic_ablative_cd = t + 45.0
                    c1.mana -= 36

                # Repulsion Wave (instant PBAoE 3s stun/interrupt, 30s cd)
                if use_repulsion_wave and c1_dist <= 0 and t >= c1.repulsion_cd and c1.mana >= 38:
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

                # Melee Swings
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

                            # Smart style rotation:
                            # 1. If parried recently -> Riposte of Force (3s stun, 5% end)
                            if allow_parry_riposte and c1.parry_riposte_ready and c1.endurance >= 5 and t >= c2.stun_immune_until:
                                c1.parry_riposte_ready = False
                                c1.endurance -= 5
                                style_bonus = random.uniform(120, 150)
                                c2.stun_until = max(c2.stun_until, t + 3.0)
                                c2.stun_immune_until = t + 63.0
                            # 2. Spellsever if endurance >= 18 and opponent has buffs or is casting
                            elif c1.endurance >= spellsever_end:
                                c1.endurance -= spellsever_end
                                style_bonus = random.uniform(160, 210)
                                proc_dmg = 110.0 * (1.0 - c2.resists.get("Energy", 0.26))
                                if c2.is_casting:
                                    c2.is_casting = False
                                    c2.cast_finish = t + 1.5
                            # 3. Silver Arc anytime (10% end)
                            elif c1.endurance >= 10:
                                c1.endurance -= 10
                                style_bonus = random.uniform(70, 95)
                                proc_dmg = 35.0 * (1.0 - c2.resists.get("Energy", 0.26))
                            else:
                                c1.endurance = max(0, c1.endurance - 4)

                            # Kinetic Infusion Damage Add
                            dmg_add = (38.0 * (1.0 - c2.resists.get("Energy", 0.26))) if dmg_add_active else 0.0

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

def w_duelist_r1():
    return Combatant("Warden Duelist (Run 1)", 1850, 360, 0.27, {"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26, "Matter":0.26}, 3.6, 0.28, 0.0, 0.12, mana=520, role="melee")

def tank_r1():
    return Combatant("Shield Tank (Run 1)", 2250, 420, 0.34, {"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26, "Matter":0.26}, 4.2, 0.18, 0.45, 0.05)

def assassin_r1():
    return Combatant("Stealth Assassin (Run 1)", 1650, 280, 0.19, {"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26, "Matter":0.26}, 3.0, 0.15, 0.0, 0.32)

def caster_r1():
    return Combatant("Cloth Caster (Run 1)", 1450, 120, 0.00, {"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26, "Matter":0.26}, 4.0, 0.0, 0.0, 0.05, mana=1200, role="caster", casting_speed_mod=1.0)

def paladin_r1():
    return Combatant("Paladin Hybrid (Run 1)", 2100, 410, 0.34, {"Slash":0.26, "Energy":0.26, "Heat":0.26, "Thrust":0.26, "Spirit":0.26, "Matter":0.26}, 3.8, 0.16, 0.36, 0.05)

def w_duelist_r2():
    return Combatant("Warden Duelist (Run 2)", 2200, 410, 0.27, {"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31, "Matter":0.31}, 3.6, 0.32, 0.0, 0.14, mana=650, role="melee")

def tank_r2():
    return Combatant("Shield Tank (Run 2)", 2650, 470, 0.34, {"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31, "Matter":0.31}, 4.2, 0.20, 0.50, 0.06)

def assassin_r2():
    return Combatant("Stealth Assassin (Run 2)", 1950, 310, 0.19, {"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31, "Matter":0.31}, 3.0, 0.18, 0.0, 0.36)

def caster_r2():
    return Combatant("Cloth Caster (Run 2)", 1700, 140, 0.00, {"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31, "Matter":0.31}, 4.0, 0.0, 0.0, 0.06, mana=1450, role="caster", casting_speed_mod=0.88)

def paladin_r2():
    return Combatant("Paladin Hybrid (Run 2)", 2450, 460, 0.34, {"Slash":0.31, "Energy":0.31, "Heat":0.31, "Thrust":0.31, "Spirit":0.31, "Matter":0.31}, 3.8, 0.18, 0.40, 0.06)

print("=== BALANCED DUELIST SIMULATION (RUN 1: EPIC QUEST + SC) ===")
for name, opp_f, scn in [
    ("vs Shield Tank", tank_r1, "melee_clash"),
    ("vs Stealth Assassin", assassin_r1, "assassin_ambush"),
    ("vs Cloth Caster", caster_r1, "ranged_clash"),
    ("vs Paladin Hybrid", paladin_r1, "melee_clash"),
]:
    wr, hp, m, e, ttk = simulate_matchup(w_duelist_r1, opp_f, rounds=1000, scenario=scn,
                                          dmg_add_active=True, spellsever_end=16, allow_parry_riposte=True, use_repulsion_wave=True)
    print(f"{name:25} | Win: {wr:5.1f}% | Rem HP: {hp:4.1f}% | Rem Mana: {m:4.1f}% | Rem End: {e:4.1f}% | TTK: {ttk:4.1f}s")

print("\n=== BALANCED DUELIST SIMULATION (RUN 2: ENDGAME RAID / DF / SH) ===")
for name, opp_f, scn in [
    ("vs Shield Tank", tank_r2, "melee_clash"),
    ("vs Stealth Assassin", assassin_r2, "assassin_ambush"),
    ("vs Cloth Caster", caster_r2, "ranged_clash"),
    ("vs Paladin Hybrid", paladin_r2, "melee_clash"),
]:
    wr, hp, m, e, ttk = simulate_matchup(w_duelist_r2, opp_f, rounds=1000, scenario=scn,
                                          dmg_add_active=True, spellsever_end=16, allow_parry_riposte=True, use_repulsion_wave=True)
    print(f"{name:25} | Win: {wr:5.1f}% | Rem HP: {hp:4.1f}% | Rem Mana: {m:4.1f}% | Rem End: {e:4.1f}% | TTK: {ttk:4.1f}s")
