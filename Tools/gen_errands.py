import sys
ROOT = sys.argv[1]
# id, group, title, description, condition(yaml lines), reward(yaml lines), weight
def visit(comps, secs=10): return ['!type:VisitNearCondition', f'goal: {secs}', 'range: 6', f'whitelist: {{ components: [ {", ".join(comps)} ] }}']
def near(comps, n): return ['!type:CastNearCondition', f'goal: {n}', 'range: 8', f'whitelist: {{ components: [ {", ".join(comps)} ] }}']
def simple(t, goal, extra=None): return [f'!type:{t}', f'goal: {goal}'] + (extra or [])
MAXW = lambda n: ['!type:MaxWindReward', f'amount: {n}']
REGEN = lambda n: ['!type:RegenReward', f'amount: {n}']
NODES = ['!type:NodesReward', 'amount: 1']
SLOTS = ['!type:SlotsReward', 'amount: 1']
GLYPH = ['!type:GlyphReward', 'count: 1']
GLYPH2 = ['!type:GlyphReward', 'count: 2']
ROTE = ['!type:RoteReward', 'count: 1']
ATTUNE = ['!type:AttuneReward']
E = [
 ('ErrandVisitBridge','visit','Seek the Bridge','The Winds are curious about where the station is steered from. Spend some time near the communications console.',visit(['CommunicationsConsole']),MAXW(8),1),
 ('ErrandVisitCargo','visit','Seek the Cargo Bay','Things arrive here from nowhere. Spend some time near the cargo pallets.',visit(['CargoPallet']),GLYPH,1),
 ('ErrandVisitMedical','visit','Seek the Healers','The Winds want to understand mending. Spend some time near medical machinery.',visit(['MedicalScanner','CryoPod','Morgue']),REGEN(0.25),1),
 ('ErrandVisitScience','visit','Seek the Researchers','Others are trying to measure what you are. Spend some time near their servers or anomaly equipment.',visit(['ResearchServer','ArtifactAnalyzer']),ATTUNE,1),
 ('ErrandVisitEngineering','visit','Seek the Engines','The station hums with a smaller kind of power. Spend some time near a power store, a gravity generator or a solar panel.',visit(['Smes','GravityGenerator','SolarPanel']),NODES,1),
 ('ErrandVisitGarden','visit','Seek the Gardens','Growing things remember the Winds. Spend some time near a plant tray or seed extractor.',visit(['PlantHolder','SeedExtractor']),ROTE,1),
 ('ErrandVisitChemistry','visit','Seek the Alchemists','Mixing is a quiet kind of magic. Spend some time near a chemistry dispenser or mixer.',visit(['ChemMaster','ReagentDispenser']),MAXW(8),1),
 ('ErrandVisitService','visit','Seek the Vending Machines','They offer things for coins. How quaint. Spend some time near one.',visit(['VendingMachine'],8),REGEN(0.2),1),
 ('ErrandVisitFabrication','visit','Seek the Fabricators','Things are made here from sheets of nothing much. Spend some time near a lathe.',visit(['Lathe']),GLYPH,1),
 ('ErrandVisitOffice','visit','Seek the Offices','Words travel by machine here. Spend some time near a fax machine.',visit(['FaxMachine'],8),ROTE,1),
 ('ErrandCastAirlock','near','Show Them a Doorway','Cast 3 spells near an airlock. Doors are an old idea, and a rude one.',near(['Airlock'],3),NODES,1),
 ('ErrandCastPower','near','Wake the Wires','Cast 2 spells near an APC. Let the station feel it.',near(['Apc'],2),MAXW(10),1),
 ('ErrandCastComputer','near','Haunt a Terminal','Cast 3 spells near a computer. Let it wonder what that was.',near(['Computer'],3),GLYPH,1),
 ('ErrandAffectFive','combat','Be Noticed','Affect 5 different creatures with your spells. Each one counts once.',simple('AffectCreaturesCondition',5),MAXW(12),1),
 ('ErrandAffectThree','combat','Make a Few Friends','Affect 3 different creatures with your spells. Each one counts once.',simple('AffectCreaturesCondition',3),REGEN(0.25),1.2),
 ('ErrandFireSchool','school','Speak in Flame','Cast 4 spells that use Fire glyphs.',simple('CastSchoolCondition',4,['school: Fire']),GLYPH,1),
 ('ErrandIceSchool','school','Speak in Frost','Cast 4 spells that use Ice glyphs.',simple('CastSchoolCondition',4,['school: Ice']),GLYPH,1),
 ('ErrandLightningSchool','school','Speak in Thunder','Cast 4 spells that use Lightning glyphs.',simple('CastSchoolCondition',4,['school: Lightning']),GLYPH,1),
 ('ErrandHolySchool','school','Speak in Light','Cast 4 spells that use Holy glyphs.',simple('CastSchoolCondition',4,['school: Holy']),SLOTS,1),
 ('ErrandNatureSchool','school','Speak in Green','Cast 4 spells that use Nature glyphs.',simple('CastSchoolCondition',4,['school: Nature']),ROTE,1),
 ('ErrandEvocationSchool','school','Speak in Force','Cast 4 spells that use Evocation glyphs.',simple('CastSchoolCondition',4,['school: Evocation']),NODES,1),
 ('ErrandRunes','delivery','Leave a Mark','Place 2 runes. Spells with the Rune form count.',simple('CastDeliveryCondition',2,['delivery: Rune']),SLOTS,1),
 ('ErrandBolts','delivery','Throw Some Light','Fire 5 bolts, orbs or lances.',simple('CastDeliveryCondition',5,['delivery: Bolt']),GLYPH,1),
 ('ErrandTouch','delivery','Lay On Hands','Cast 3 spells with the Touch form.',simple('CastDeliveryCondition',3,['delivery: Touch']),ROTE,1),
 ('ErrandBurst','delivery','Make Some Room','Cast 3 spells with the Burst or Nova form.',simple('CastDeliveryCondition',3,['delivery: Burst']),MAXW(10),0.8),
 ('ErrandRite','rite','Keep the Old Customs','Perform a rite at a ritual circle.',simple('PerformRitesCondition',1),ATTUNE,0.8),
]
out = ['# GENERATED by tools/gen_errands.py. Edit that, not this file.', '']
loc = ['# GENERATED by tools/gen_errands.py.', '']
for eid, grp, title, desc, cond, reward, w in E:
    k = eid.lower()
    out += ['- type: errand', f'  id: {eid}', f'  group: {grp}', f'  weight: {w}', f'  title: errand-{k}-title', f'  description: errand-{k}-desc', '  condition: ' + cond[0]]
    out += ['    ' + c for c in cond[1:]]
    out += ['  reward: ' + reward[0]] + ['    ' + r for r in reward[1:]] + ['']
    loc += [f'errand-{k}-title = {title}', f'errand-{k}-desc = {desc}']
open(ROOT + '/Resources/Prototypes/_Starlight/Wizard/Casting/errands.yml', 'w').write('\n'.join(out))
loc += ['', 'errand-complete = Errand done: {$title}. {$reward}', 'errand-reward-maxwind = Your Wind grows by {$amount}.', 'errand-reward-regen = Your Wind returns {$amount} faster per second.',
        'errand-reward-nodes = You can weave {$amount} more glyph into one spell.', 'errand-reward-slots = You can hold {$amount} more woven spell.',
        'errand-reward-glyph = You learn a new glyph.', 'errand-reward-rote = You remember a prepared spell.', 'errand-reward-attune = You become attuned to a new school.',
        'errand-reward-prefix = Reward: {$reward}', 'spellcraft-ui-errands = Errands', 'spellcraft-ui-errands-help = The Winds ask small things of you around the station. Each one you finish makes you permanently stronger, apart from your objectives.',
        'spellcraft-ui-errands-done = Mastery has reached its limit. The Winds ask no more of you.', 'spellcraft-ui-errand-progress = {$progress} / {$goal}']
open(ROOT + '/Resources/Locale/en-US/_Starlight/wizard/errands.ftl', 'w').write('\n'.join(loc) + '\n')
print(len(E), 'errands')
