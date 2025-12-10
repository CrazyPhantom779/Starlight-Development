## TISSUE/SKIN DAMAGE STATES

surgical-state-lacerated-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a shallow laceration.[/color]
surgical-state-lacerated-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a deep laceration with torn tissue.[/color]
surgical-state-lacerated-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a gaping wound with extensive tissue damage.[/color]

surgical-state-crushed-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } light bruising from blunt trauma.[/color]
surgical-state-crushed-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } crushed tissue with internal damage.[/color]
surgical-state-crushed-severe = [color=red]{ CAPITALIZE(POSS-ADJ($target)) } tissue is heavily compressed and may be permanently damaged.[/color]

surgical-state-bruised-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } minor bruising with light discoloration.[/color]
surgical-state-bruised-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } extensive bruising and tenderness.[/color]
surgical-state-bruised-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a severe contusion with deep tissue damage.[/color]

surgical-state-swollen-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } slight swelling from inflammation.[/color]
surgical-state-swollen-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } noticeably swollen with edema.[/color]
surgical-state-swollen-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } severely swollen, restricting movement.[/color]

surgical-state-abraded-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } minor scraping with skin loss.[/color]
surgical-state-abraded-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } significant skin abrasion.[/color]
surgical-state-abraded-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } deep tissue abrasion.[/color]

surgical-state-punctured-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a small puncture wound.[/color]
surgical-state-punctured-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a deep puncture wound.[/color]
surgical-state-punctured-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a large puncture wound compromising internal structures.[/color]

surgical-state-mangled-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } torn tissue with irregular tearing.[/color]
surgical-state-mangled-moderate = [color=orange]{ CAPITALIZE(POSS-ADJ($target)) } tissue is severely torn and shredded.[/color]
surgical-state-mangled-severe = [color=red]{ CAPITALIZE(POSS-ADJ($target)) } tissue is completely mangled beyond recognition.[/color]

surgical-state-torn-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a minor tear in the tissue.[/color]
surgical-state-torn-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } significant traumatic tearing.[/color]
surgical-state-torn-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } extensive tissue disruption.[/color]

## BURN STATES

surgical-state-burn-first-degree-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } slight redness from a superficial burn.[/color]
surgical-state-burn-first-degree-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a first degree burn with reddened skin.[/color]
surgical-state-burn-first-degree-severe = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a severe first degree burn with widespread redness.[/color]

surgical-state-burn-second-degree-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } minor blistering.[/color]
surgical-state-burn-second-degree-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a second degree burn with significant blistering.[/color]
surgical-state-burn-second-degree-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } extensive blistering with weeping wounds.[/color]

surgical-state-burn-third-degree-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } white or charred patches appearing on { POSS-ADJ($target) } skin.[/color]
surgical-state-burn-third-degree-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a third degree burn with white, charred skin.[/color]
surgical-state-burn-third-degree-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } extensive third degree burns with widespread charring.[/color]

surgical-state-burn-fourth-degree-mild = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a deep burn extending past skin layers.[/color]
surgical-state-burn-fourth-degree-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a fourth degree burn reaching muscle and tendon.[/color]
surgical-state-burn-fourth-degree-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a complete fourth degree burn exposing bone with carbonized tissue.[/color]

surgical-state-chemical-burn-mild = [color=yellowgreen]{ CAPITALIZE(POSS-ADJ($target)) } skin is reacting to chemical exposure.[/color]
surgical-state-chemical-burn-moderate = [color=yellowgreen]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a chemical burn with tissue damage.[/color]
surgical-state-chemical-burn-severe = [color=yellowgreen]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } severe chemical burns with deep tissue dissolution.[/color]

surgical-state-radiation-burn-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } minor radiation damage.[/color]
surgical-state-radiation-burn-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } radiation burns with cellular breakdown.[/color]
surgical-state-radiation-burn-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } acute radiation syndrome with severe cellular damage.[/color]

surgical-state-frostbite-mild = [color=lightblue]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } minor frostbite with pale discoloration.[/color]
surgical-state-frostbite-moderate = [color=lightblue]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } frostbite with frozen tissue and ice crystal formation.[/color]
surgical-state-frostbite-severe = [color=lightblue]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } severe frostbite with black necrotic tissue.[/color]

## BLEEDING STATES

surgical-state-bleeding-mild = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } minor bleeding from a wound.[/color]
surgical-state-bleeding-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } bleeding steadily.[/color]
surgical-state-bleeding-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } bleeding heavily![/color]

surgical-state-hemorrhaging-mild = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } light internal hemorrhaging.[/color]
surgical-state-hemorrhaging-moderate = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } hemorrhaging internally![/color]
surgical-state-hemorrhaging-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } life-threatening internal bleeding![/color]

surgical-state-hematoma-mild = [color=purple]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a small hematoma with blood pooling under the skin.[/color]
surgical-state-hematoma-moderate = [color=purple]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a significant hematoma forming a lump.[/color]
surgical-state-hematoma-severe = [color=purple]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a large hematoma with extensive blood pooling.[/color]

## FRACTURE STATES

surgical-state-bone-fractured-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a hairline bone fracture.[/color]
surgical-state-bone-fractured-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a bone fracture requiring stabilization.[/color]
surgical-state-bone-fractured-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a severe bone fracture with displacement.[/color]

surgical-state-compound-fracture-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a minor compound fracture near the skin surface.[/color]
surgical-state-compound-fracture-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a compound fracture with bone piercing through skin.[/color]
surgical-state-compound-fracture-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a severe compound fracture with multiple bone fragments protruding![/color]

surgical-state-comminuted-fracture-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a partially shattered bone with several cracks.[/color]
surgical-state-comminuted-fracture-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a comminuted fracture with bone broken into pieces.[/color]
surgical-state-comminuted-fracture-severe = [color=crimson]{ CAPITALIZE(POSS-ADJ($target)) } bone is completely shattered into numerous fragments![/color]

surgical-state-rib-fractured-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a cracked rib.[/color]
surgical-state-rib-fractured-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a broken rib.[/color]
surgical-state-rib-fractured-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } multiple broken ribs affecting respiration.[/color]

surgical-state-flail-chest-mild = [color=orange]{ CAPITALIZE(POSS-ADJ($target)) } chest shows abnormal movement from an unstable segment.[/color]
surgical-state-flail-chest-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } flail chest with paradoxical breathing.[/color]
surgical-state-flail-chest-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } severe flail chest with respiratory failure![/color]

surgical-state-skull-fractured-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a hairline skull fracture.[/color]
surgical-state-skull-fractured-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a skull fracture requiring monitoring.[/color]
surgical-state-skull-fractured-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a severe skull fracture with multiple fragments.[/color]

surgical-state-skull-depressed-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a minor depressed skull fracture.[/color]
surgical-state-skull-depressed-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a depressed skull fracture pressing on { POSS-ADJ($target) } brain.[/color]
surgical-state-skull-depressed-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } severe skull depression compressing brain tissue![/color]

surgical-state-spinal-fracture-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a minor vertebral crack.[/color]
surgical-state-spinal-fracture-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a spinal fracture risking nerve damage.[/color]
surgical-state-spinal-fracture-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } an unstable spinal fracture with cord compression.[/color]

surgical-state-dislocated-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a partial joint dislocation.[/color]
surgical-state-dislocated-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a completely dislocated joint.[/color]
surgical-state-dislocated-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a severe dislocation with ligament damage.[/color]

## INFECTION STATES

surgical-state-infected-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } an early infection with redness and warmth.[/color]
surgical-state-infected-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } infected with pus formation.[/color]
surgical-state-infected-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a severe infection spreading to surrounding tissue.[/color]

surgical-state-septic-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } early sepsis with systemic infection.[/color]
surgical-state-septic-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } septic with dangerous systemic infection.[/color]
surgical-state-septic-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } in septic shock![/color]

surgical-state-necrotic-mild = [color=gray]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } tissue discoloration from early necrosis.[/color]
surgical-state-necrotic-moderate = [color=darkgray]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } necrotic tissue requiring debridement.[/color]
surgical-state-necrotic-severe = [color=black]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } extensive necrosis with large areas of dead, blackened tissue.[/color]

surgical-state-gangrenous-mild = [color=darkgray]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } early gangrene with infected dead tissue.[/color]
surgical-state-gangrenous-moderate = [color=black]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } gangrene with gas-producing infection.[/color]
surgical-state-gangrenous-severe = [color=black]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } advanced gangrene requiring amputation![/color]

surgical-state-abscessed-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a small abscess forming.[/color]
surgical-state-abscessed-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a pus-filled abscess requiring drainage.[/color]
surgical-state-abscessed-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a large abscess with extensive pus accumulation.[/color]

surgical-state-inflamed-mild = [color=pink]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } slight inflammation with warmth and redness.[/color]
surgical-state-inflamed-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } inflamed with hot, red, swollen tissue.[/color]
surgical-state-inflamed-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } severe inflammation with intense pain.[/color]

## EMBEDDED OBJECTS

surgical-state-glass-embedded-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } small glass shards embedded in tissue.[/color]
surgical-state-glass-embedded-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } embedded glass penetrating deep into tissue.[/color]
surgical-state-glass-embedded-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } large glass shards causing internal damage.[/color]

surgical-state-shrapnel-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } small shrapnel pieces in tissue.[/color]
surgical-state-shrapnel-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } metal shrapnel fragments embedded throughout the area.[/color]
surgical-state-shrapnel-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } heavy shrapnel causing extensive damage.[/color]

surgical-state-bullet-wound-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a bullet graze through soft tissue.[/color]
surgical-state-bullet-wound-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a bullet wound with the projectile lodged in tissue.[/color]
surgical-state-bullet-wound-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a deep bullet wound penetrating vital structures.[/color]

surgical-state-foreign-object-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a small foreign object embedded superficially.[/color]
surgical-state-foreign-object-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a foreign object lodged in tissue.[/color]
surgical-state-foreign-object-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a large foreign body causing damage.[/color]

## VASCULAR/NEURAL DAMAGE

surgical-state-vessel-severed-mild = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a minor severed blood vessel.[/color]
surgical-state-vessel-severed-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a major blood vessel completely severed.[/color]
surgical-state-vessel-severed-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } multiple severed vessels with extensive vascular damage![/color]

surgical-state-nerve-damaged-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } nerve bruising from trauma.[/color]
surgical-state-nerve-damaged-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } partial nerve damage.[/color]
surgical-state-nerve-damaged-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } complete nerve severance with loss of function.[/color]

surgical-state-arterial-damage-mild = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a small arterial nick.[/color]
surgical-state-arterial-damage-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } significant arterial damage.[/color]
surgical-state-arterial-damage-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a major arterial rupture![/color]

surgical-state-paralyzed-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } numbness with partial loss of sensation.[/color]
surgical-state-paralyzed-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } paralyzed with complete loss of motor function.[/color]
surgical-state-paralyzed-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } total paralysis from permanent nerve damage.[/color]

surgical-state-ischemic-mild = [color=lightblue]{ CAPITALIZE(POSS-ADJ($target)) } tissue has reduced blood circulation.[/color]
surgical-state-ischemic-moderate = [color=lightblue]{ CAPITALIZE(POSS-ADJ($target)) } tissue is ischemic with significantly reduced blood supply.[/color]
surgical-state-ischemic-severe = [color=lightblue]{ CAPITALIZE(POSS-ADJ($target)) } tissue has severe ischemia causing tissue death.[/color]

surgical-state-aneurysm-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a small aneurysm with vessel wall weakness.[/color]
surgical-state-aneurysm-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } an aneurysm with dangerous vessel bulging.[/color]
surgical-state-aneurysm-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a ruptured aneurysm with massive hemorrhage![/color]

## ORGAN DAMAGE

surgical-state-organ-failure-mild = [color=orange]{ CAPITALIZE(POSS-ADJ($target)) } organ is showing dysfunction with reduced function.[/color]
surgical-state-organ-failure-moderate = [color=red]{ CAPITALIZE(POSS-ADJ($target)) } organ is critically failing.[/color]
surgical-state-organ-failure-severe = [color=crimson]{ CAPITALIZE(POSS-ADJ($target)) } organ has completely shut down![/color]

surgical-state-organ-ruptured-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a small tear in organ tissue.[/color]
surgical-state-organ-ruptured-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a major organ rupture leaking contents.[/color]
surgical-state-organ-ruptured-severe = [color=crimson]{ CAPITALIZE(POSS-ADJ($target)) } organ has completely ruptured with massive damage![/color]

surgical-state-organ-hemorrhaging-mild = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } minor internal organ bleeding.[/color]
surgical-state-organ-hemorrhaging-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } significant organ hemorrhaging.[/color]
surgical-state-organ-hemorrhaging-severe = [color=crimson]{ CAPITALIZE(POSS-ADJ($target)) } organ is bleeding uncontrollably![/color]

surgical-state-organ-necrotic-mild = [color=darkgray]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } parts of organ tissue dying.[/color]
surgical-state-organ-necrotic-moderate = [color=black]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a necrotic organ with large sections of dead tissue.[/color]
surgical-state-organ-necrotic-severe = [color=black]{ CAPITALIZE(POSS-ADJ($target)) } organ is completely necrotic![/color]

## TRAUMA STATES

surgical-state-concussion-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a light concussion with dizziness.[/color]
surgical-state-concussion-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a concussion with traumatic brain injury symptoms.[/color]
surgical-state-concussion-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a severe concussion with major head trauma.[/color]

surgical-state-traumatic-brain-injury-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } mild traumatic brain injury.[/color]
surgical-state-traumatic-brain-injury-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } traumatic brain injury with significant brain damage.[/color]
surgical-state-traumatic-brain-injury-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } severe TBI with life-threatening brain trauma![/color]

surgical-state-cerebral-edema-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } minor brain swelling.[/color]
surgical-state-cerebral-edema-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } cerebral edema with dangerous brain swelling.[/color]
surgical-state-cerebral-edema-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } severe cerebral edema with herniation risk![/color]

surgical-state-subdural-hematoma-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a small subdural bleed under the skull.[/color]
surgical-state-subdural-hematoma-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a subdural hematoma with blood pooling beneath the dura mater.[/color]
surgical-state-subdural-hematoma-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a large subdural hematoma causing brain compression![/color]

surgical-state-pneumothorax-mild = [color=lightblue]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a partial lung collapse.[/color]
surgical-state-pneumothorax-moderate = [color=lightblue]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } pneumothorax requiring intervention.[/color]
surgical-state-pneumothorax-severe = [color=lightblue]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } complete pneumothorax with fully collapsed lung.[/color]

surgical-state-tension-pneumothorax-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } building pressure in chest.[/color]
surgical-state-tension-pneumothorax-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } tension pneumothorax crushing { POSS-ADJ($target) } lung and heart.[/color]
surgical-state-tension-pneumothorax-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } critical tension pneumothorax requiring immediate decompression![/color]

surgical-state-hemothorax-mild = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } blood in the chest cavity.[/color]
surgical-state-hemothorax-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } hemothorax with significant blood accumulation.[/color]
surgical-state-hemothorax-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } massive hemothorax with chest filling with blood![/color]

surgical-state-cardiac-tamponade-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } pericardial fluid surrounding the heart.[/color]
surgical-state-cardiac-tamponade-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } cardiac tamponade with fluid compressing heart function.[/color]
surgical-state-cardiac-tamponade-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } severe tamponade with heart unable to pump![/color]

surgical-state-spinal-cord-injury-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } minor spinal cord trauma with bruising.[/color]
surgical-state-spinal-cord-injury-moderate = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } spinal cord injury with partial damage.[/color]
surgical-state-spinal-cord-injury-severe = [color=crimson]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } complete spinal cord severance![/color]

## SPECIAL CONDITIONS

surgical-state-damaged-mild = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } light internal tissue damage.[/color]
surgical-state-damaged-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } significant structural damage.[/color]
surgical-state-damaged-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } extensive internal damage.[/color]

surgical-state-hypoxic-mild = [color=lightblue]{ CAPITALIZE(POSS-ADJ($target)) } tissue has oxygen deficiency.[/color]
surgical-state-hypoxic-moderate = [color=lightblue]{ CAPITALIZE(POSS-ADJ($target)) } tissue is hypoxic with dangerous oxygen deprivation.[/color]
surgical-state-hypoxic-severe = [color=lightblue]{ CAPITALIZE(POSS-ADJ($target)) } tissue has severe hypoxia causing cell death.[/color]

surgical-state-irradiated-mild = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } radiation exposure with low-level damage.[/color]
surgical-state-irradiated-moderate = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } irradiated with significant cellular damage.[/color]
surgical-state-irradiated-severe = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } acute radiation damage with severe cellular breakdown.[/color]

surgical-state-poisoned-mild = [color=yellowgreen]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } toxin exposure with low-level accumulation.[/color]
surgical-state-poisoned-moderate = [color=yellowgreen]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } poisoned with dangerous toxin levels.[/color]
surgical-state-poisoned-severe = [color=yellowgreen]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } severe poisoning with critical toxin concentration.[/color]

surgical-state-crystallized-mild = [color=cyan]{ CAPITALIZE(POSS-ADJ($target)) } tissue shows minor crystallization.[/color]
surgical-state-crystallized-moderate = [color=cyan]{ CAPITALIZE(POSS-ADJ($target)) } tissue is crystallized with significant crystal structures.[/color]
surgical-state-crystallized-severe = [color=cyan]{ CAPITALIZE(POSS-ADJ($target)) } tissue is completely crystallized![/color]

# Procedural/single-state descriptions (no severity variants)

surgical-state-anesthetized = [color=lightblue]{ CAPITALIZE(POSS-ADJ($target)) } area is anesthetized and pain-free.[/color]
surgical-state-dirty = [color=brown]{ CAPITALIZE(POSS-ADJ($target)) } surgical field needs cleaning.[/color]
surgical-state-bloodied = [color=red]Blood obscures { POSS-ADJ($target) } visibility.[/color]
surgical-state-sterile = [color=white]{ CAPITALIZE(POSS-ADJ($target)) } surgical field is properly sterilized.[/color]
surgical-state-contaminated = [color=yellowgreen]{ CAPITALIZE(POSS-ADJ($target)) } area is contaminated with toxins or chemicals.[/color]
surgical-state-incised = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a shallow scalpel incision.[/color]
surgical-state-cut-open = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } cut open with deep incision.[/color]
surgical-state-retracted = [color=orange]{ CAPITALIZE(POSS-ADJ($target)) } incision is held open with retractors.[/color]
surgical-state-scarred = [color=gray]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } old scar tissue.[/color]
surgical-state-clamped = [color=silver]{ CAPITALIZE(POSS-ADJ($target)) } blood vessels are clamped with hemostat.[/color]
surgical-state-cauterized = [color=orange]{ CAPITALIZE(POSS-ADJ($target)) } bleeding has been stopped via cauterization.[/color]
surgical-state-sutured = [color=white]{ CAPITALIZE(POSS-ADJ($target)) } wound is closed with sutures.[/color]
surgical-state-bandaged = [color=white]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a dressing applied to the wound.[/color]
surgical-state-sealed = [color=white]{ CAPITALIZE(POSS-ADJ($target)) } wound is sealed with surgical glue.[/color]
surgical-state-stapled = [color=silver]{ CAPITALIZE(POSS-ADJ($target)) } wound is closed with staples.[/color]
surgical-state-gaping = [color=red]{ CAPITALIZE(POSS-ADJ($target)) } wound is gaping wide open![/color]
surgical-state-bone-exposed = [color=white]{ CAPITALIZE(POSS-ADJ($target)) } bone is visible and exposed.[/color]
surgical-state-bone-sawed = [color=white]{ CAPITALIZE(POSS-ADJ($target)) } bone has been cut with a saw.[/color]
surgical-state-bone-set = [color=yellow]{ CAPITALIZE(POSS-ADJ($target)) } bone fracture has been aligned for healing.[/color]
surgical-state-relocated = [color=yellow]{ CAPITALIZE(POSS-ADJ($target)) } joint dislocation has been reduced.[/color]
surgical-state-skull-open = [color=red]{ CAPITALIZE(POSS-ADJ($target)) } skull is opened from craniotomy.[/color]
surgical-state-ribcage-open = [color=red]{ CAPITALIZE(POSS-ADJ($target)) } ribcage is opened from thoracotomy.[/color]
surgical-state-organ-removed = [color=orange]{ CAPITALIZE(POSS-ADJ($target)) } organ has been extracted.[/color]
surgical-state-organ-installed = [color=green]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a transplanted organ in place.[/color]
surgical-state-amputated = [color=red]{ CAPITALIZE(POSS-ADJ($target)) } limb has been removed.[/color]
surgical-state-reattached = [color=yellow]{ CAPITALIZE(POSS-ADJ($target)) } limb is surgically reattached.[/color]
surgical-state-prosthetic = [color=silver]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } an artificial replacement installed.[/color]
surgical-state-augmented = [color=cyan]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a cybernetic enhancement installed.[/color]
