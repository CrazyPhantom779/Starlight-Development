# Source of truth for glyph content. Run gen_glyphs.py to emit YAML, locale and icons.
SCHOOLS = {
    'Fire': '#ff6a3d', 'Ice': '#7fd6ff', 'Lightning': '#ffe14d', 'Holy': '#fff0a8',
    'Evocation': '#c18cff', 'Ender': '#8a6bff', 'Nature': '#6ed16e', 'Blood': '#e0475a', 'Eldritch': '#4fd0c0',
}
FORM_COLOR = '#e0b84c'
AUG_COLOR = '#5fd1d1'

# id, name, desc, cost, delivery, extras
FORMS = [
    ('FormAimed', 'Aimed', 'Takes effect instantly at the point you choose.', 4, 'Aimed', {}),
    ('FormSelf', 'Self', 'Takes effect on you, without aiming.', 2, 'Self', {}),
    ('FormBolt', 'Bolt', 'Fires a bolt that takes effect where it hits or lands.', 6, 'Bolt', {'projectile': 'SpellBolt', 'projectileSpeed': 18}),
    ('FormOrb', 'Orb', 'Releases a slow, glowing orb that takes effect where it hits.', 8, 'Bolt', {'projectile': 'SpellOrb', 'projectileSpeed': 6}),
    ('FormLance', 'Lance', 'Fires a very fast bolt.', 9, 'Bolt', {'projectile': 'SpellBolt', 'projectileSpeed': 34}),
    ('FormTouch', 'Touch', 'Takes effect on whatever you click, within arm\'s reach.', 2, 'Touch', {'range': 2.5}),
    ('FormBurst', 'Burst', 'Takes effect in a ring around you.', 6, 'Burst', {'radius': 2.5}),
    ('FormNova', 'Nova', 'A wide burst around you.', 12, 'Burst', {'radius': 4.5}),
    ('FormRune', 'Rune', 'Places a rune at the point you choose. It takes effect when something steps on it.', 5, 'Rune', {'rune': 'SpellRune'}),
]

# id, name, desc, cost, kind, value
AUGMENTS = [
    ('AugmentEcho', 'Echo', 'Repeats the effect it follows once more.', 3, 'Repeat', 1),
    ('AugmentReverb', 'Reverb', 'Repeats the effect it follows twice more.', 6, 'Repeat', 2),
    ('AugmentDelay', 'Delay', 'The effect it follows happens 1.5 seconds later.', 0, 'Delay', 1.5),
    ('AugmentSlowFuse', 'Slow Fuse', 'The effect it follows happens 4 seconds later.', 0, 'Delay', 4),
    ('AugmentFrugal', 'Frugal', 'Reduces the whole spell\'s cost by a fifth.', 2, 'Cheaper', 0.8),
    ('AugmentMiser', 'Miser', 'Reduces the whole spell\'s cost by over a third.', 6, 'Cheaper', 0.65),
    ('AugmentAmplify', 'Amplify', 'Makes the effect it follows stronger, at a higher cost.', 2, 'Amplify', 1),
    ('AugmentWiden', 'Widen', 'Widens the area of the effect it follows by one tile.', 1, 'Widen', 1),
    ('AugmentFork', 'Fork', 'A Bolt, Orb or Lance form fires two extra bolts in a fan.', 3, 'Split', 2),
    ('AugmentTwin', 'Twin', 'A Bolt, Orb or Lance form fires one extra bolt.', 2, 'Split', 1),
    ('AugmentChain', 'Chain', 'After it takes effect, the effect jumps to one more nearby creature.', 0, 'Chain', 1),
    ('AugmentLinger', 'Linger', 'The effect it follows repeats three times, once a second, in the same place.', 2, 'Linger', 3),
    ('AugmentQuicken', 'Quicken', 'Shortens the spell\'s cooldown by a third.', 3, 'Quicken', 0.3),
]

def dmg(**kw): return {'dmg': kw}
# Effect spec: (id, name, desc, cost, [schools], kind, payload)
# kind 'u' => universal list of effect dicts; 'w' => legacy world event yaml; 'i' => legacy instant event yaml
EFFECTS = [
    # --- Fire
    ('EffectFlame', 'Flame', 'Sets creatures at the point alight.', 6, ['Fire'], 'u', [('Ignite', {'fireStacks': 2, 'radius': 1.2})]),
    ('EffectScorch', 'Scorch', 'A searing flash of heat.', 9, ['Fire'], 'u', [('Damage', {'damage': {'types': {'Heat': 14}}, 'radius': 1.0})]),
    ('EffectInferno', 'Inferno', 'A wide burst of flame that burns and ignites.', 20, ['Fire'], 'u', [('Damage', {'damage': {'types': {'Heat': 9}}, 'radius': 2.8}), ('Ignite', {'fireStacks': 3, 'radius': 2.8})]),
    ('EffectBlast', 'Blast', 'A small explosion.', 26, ['Fire', 'Evocation'], 'u', [('Explode', {'totalIntensity': 60, 'slope': 6, 'maxIntensity': 12})]),
    ('EffectFlameRune', 'Flame Rune', 'Leaves a rune that ignites whoever steps on it.', 12, ['Fire'], 'u', [('Spawn', {'prototypes': ['IgniteRuneWiz']})]),
    ('EffectPowderRune', 'Powder Rune', 'Leaves a rune that explodes when stepped on.', 24, ['Fire'], 'u', [('Spawn', {'prototypes': ['ExplosionRuneWiz']})]),
    ('EffectEruption', 'Eruption', 'The ground erupts.', 26, ['Fire'], 'u', [('Spawn', {'prototypes': ['EruptionRuneWiz']})]),
    ('EffectFirebolt', 'Firebolt', 'A fast bolt of fire. Aimed only.', 8, ['Fire'], 'w', "!type:ProjectileSpellEvent\n    prototype: ProjectileFirebolt"),
    ('EffectFireball', 'Fireball', 'An explosive ball of fire. Aimed only.', 30, ['Fire'], 'w', "!type:ProjectileSpellEvent\n    prototype: ProjectileFireball"),
    ('EffectFireOrb', 'Fire Orb', 'A slow, heavy orb of fire. Aimed only.', 18, ['Fire'], 'w', "!type:ProjectileSpellEvent\n    prototype: ProjectileFireOrb\n    projectileSpeed: 1"),
    ('EffectFireArrows', 'Fire Arrows', 'A short spread of burning arrows. Aimed only.', 14, ['Fire'], 'w', "!type:MultiProjectileSpellEvent\n    prototype: ProjectileFireArrow\n    projectileCount: 3\n    spreadDegrees: 16"),
    # --- Ice
    ('EffectChill', 'Chill', 'A biting cold.', 8, ['Ice'], 'u', [('Damage', {'damage': {'types': {'Cold': 12}}, 'radius': 1.2})]),
    ('EffectFrostbite', 'Frostbite', 'Freezes and trips everything nearby.', 18, ['Ice'], 'u', [('Damage', {'damage': {'types': {'Cold': 8}}, 'radius': 2.2}), ('Stun', {'seconds': 1.5, 'radius': 2.2})]),
    ('EffectFreezeRune', 'Freeze Rune', 'Leaves a rune that freezes whoever steps on it.', 16, ['Ice'], 'u', [('Spawn', {'prototypes': ['FreezeRuneWiz', 'FreezeRune2Wiz']})]),
    ('EffectIcePillars', 'Ice Pillars', 'Raises a pillar of ice.', 14, ['Ice'], 'u', [('Spawn', {'prototypes': ['IcePillars']})]),
    ('EffectSummonIce', 'Summon Ice', 'Calls ice up from nothing.', 14, ['Ice'], 'u', [('Spawn', {'prototypes': ['RuneSummonIce']})]),
    ('EffectIceShard', 'Ice Shard', 'A single shard of ice. Aimed only.', 10, ['Ice'], 'w', "!type:ProjectileSpellEvent\n    prototype: ProjectileIceShard"),
    ('EffectIceOrb', 'Ice Orb', 'A slow orb of ice. Aimed only.', 18, ['Ice'], 'w', "!type:ProjectileSpellEvent\n    prototype: ProjectileIceOrb\n    projectileSpeed: 2"),
    ('EffectIceStorm', 'Ice Storm', 'A tight volley of ice spikes. Aimed only.', 20, ['Ice'], 'w', "!type:MultiProjectileSpellEvent\n    prototype: ProjectileIceStormSingle\n    projectileCount: 3\n    spreadDegrees: 12"),
    ('EffectArcticGlare', 'Arctic Glare', 'A wide fan of ice shards. Aimed only.', 28, ['Ice'], 'w', "!type:MultiProjectileSpellEvent\n    prototype: ProjectileIceGlare\n    projectileCount: 15\n    spreadDegrees: 120"),
    # --- Lightning
    ('EffectShock', 'Shock', 'A crack of lightning.', 10, ['Lightning'], 'u', [('Damage', {'damage': {'types': {'Shock': 16}}, 'radius': 1.0})]),
    ('EffectStatic', 'Static', 'Locks muscles. Stuns everything nearby.', 14, ['Lightning'], 'u', [('Stun', {'seconds': 3, 'knockdownOnly': False, 'radius': 2.0})]),
    ('EffectStormcall', 'Stormcall', 'A wide storm of shocks and stumbling.', 24, ['Lightning'], 'u', [('Damage', {'damage': {'types': {'Shock': 10}}, 'radius': 3.0}), ('Stun', {'seconds': 1.5, 'radius': 3.0})]),
    ('EffectStunRune', 'Stun Rune', 'Leaves a rune that stuns whoever steps on it.', 10, ['Lightning'], 'u', [('Spawn', {'prototypes': ['StunRune']})]),
    # --- Holy / life
    ('EffectMend', 'Mend', 'Closes wounds.', 12, ['Holy'], 'u', [('Heal', {'heal': {'types': {'Blunt': -8, 'Slash': -8, 'Piercing': -8, 'Heat': -6, 'Shock': -6, 'Cold': -6}}, 'radius': 1.2})]),
    ('EffectRenew', 'Renew', 'Soothes poison and suffocation.', 12, ['Holy'], 'u', [('Heal', {'heal': {'types': {'Poison': -12, 'Asphyxiation': -12}}, 'radius': 1.2})]),
    ('EffectWideMend', 'Wide Mend', 'Heals everyone in a wide area, a little.', 22, ['Holy'], 'u', [('Heal', {'heal': {'types': {'Blunt': -4, 'Slash': -4, 'Piercing': -4, 'Heat': -4, 'Shock': -4, 'Cold': -4}}, 'radius': 3.5})]),
    ('EffectFlashRune', 'Flash Rune', 'Leaves a rune that bursts with light.', 7, ['Holy'], 'u', [('Spawn', {'prototypes': ['FlashRune']})]),
    ('EffectWisp', 'Wisp', 'Conjures a small orb of light into your hand.', 6, ['Holy'], 'u', [('Spawn', {'prototypes': ['ElfLuminousOrb'], 'inHand': True, 'snapToGrid': False})]),
    ('EffectCleansingSmoke', 'Cleansing Smoke', 'A soothing cloud that cleanses.', 12, ['Holy', 'Nature'], 'u', [('Spawn', {'prototypes': ['ElfCleanseSmoke']})]),
    ('EffectHaemostatic', 'Haemostatic Rune', 'Leaves a rune that stops bleeding.', 12, ['Holy'], 'u', [('Spawn', {'prototypes': ['ElfRuneHaemostatic']})]),
    # --- Evocation
    ('EffectImpact', 'Impact', 'A hammering blow of force.', 12, ['Evocation'], 'u', [('Damage', {'damage': {'types': {'Blunt': 14}}, 'radius': 1.0}), ('Stun', {'seconds': 1.0, 'radius': 1.0})]),
    ('EffectShove', 'Shove', 'Throws everything nearby away from the point.', 8, ['Evocation'], 'u', [('Push', {'strength': 8, 'radius': 3.0})]),
    ('EffectPull', 'Pull', 'Drags everything nearby toward the point.', 8, ['Evocation'], 'u', [('Push', {'strength': 6, 'radius': 5.0, 'pull': True})]),
    ('EffectForceWall', 'Forcewall', 'Raises a wall of force.', 10, ['Evocation'], 'u', [('Spawn', {'prototypes': ['WallForce']})]),
    ('EffectSmoke', 'Smoke', 'A cloud of smoke.', 8, ['Evocation', 'Nature'], 'u', [('Spawn', {'prototypes': ['WizardSmoke']})]),
    ('EffectKnock', 'Knock', 'Opens nearby doors and lockers. Self or Burst only.', 6, ['Evocation'], 'i', "!type:KnockSpellEvent {}"),
    ('EffectRepulse', 'Repulse', 'A shockwave that pushes everything away. Self or Burst only.', 14, ['Evocation'], 'i', "!type:RepulseAttractActionEvent {}"),
    # --- Ender
    ('EffectBlink', 'Blink', 'Step instantly to the target point. Aimed only.', 12, ['Ender'], 'w', "!type:TeleportSpellEvent {}"),
    ('EffectStep', 'Step', 'Carry yourself to wherever the spell lands.', 10, ['Ender'], 'u', [('Teleport', {'maxDistance': 14})]),
    ('EffectFold', 'Fold', 'Folds space so you trade places with the creature nearest the point.', 14, ['Ender'], 'u', [('Swap', {'radius': 1.5})]),
    # --- Nature
    ('EffectVenom', 'Venom', 'A cloud of poison.', 10, ['Nature'], 'u', [('Damage', {'damage': {'types': {'Poison': 14}}, 'radius': 1.2})]),
    ('EffectMagicarp', 'Magicarp', 'Summons hostile magical carp.', 22, ['Nature'], 'u', [('Spawn', {'prototypes': ['MobCarpMagic'], 'amount': 2, 'scatter': 0.8, 'preventCollideWithCaster': True})]),
    # --- Blood / eldritch
    ('EffectDrain', 'Siphon Life', 'Takes life from those nearby and gives half to you.', 16, ['Blood'], 'u', [('Drain', {'damage': {'types': {'Cellular': 12}}, 'radius': 1.2, 'healFraction': 0.5})]),
    ('EffectDecay', 'Decay', 'Rots flesh.', 12, ['Blood', 'Eldritch'], 'u', [('Damage', {'damage': {'types': {'Cellular': 14}}, 'radius': 1.0})]),
    ('EffectSiphonWind', 'Siphon Wind', 'Drains Wind from nearby casters and gives it to you.', 8, ['Eldritch'], 'u', [('Wind', {'amount': -20, 'radius': 2.0})]),
    ('EffectReplenish', 'Replenish', 'Gives Wind to nearby casters.', 10, ['Eldritch'], 'u', [('Wind', {'amount': 25, 'radius': 1.2})]),
]
