## Flockmind - antag, roles, objectives
roles-antag-flockmind-name = Flockmind
roles-antag-flockmind-objective = Convert the station, grow the Flock and transmit the Signal.
role-subtype-flockmind = Flockmind
role-subtype-flocktrace = Flocktrace
flock-round-end-agent-name = flockmind
objective-issuer-flock = [color=#3cb5a4]The Flock[/color]
objective-flock-tiles-title = Convert {$count} tiles
objective-flock-tiles-description = Turn the station's floor into flock floor.

ghost-role-information-flockmind-name = Flockmind
ghost-role-information-flockmind-description = You are a vast alien intelligence made of teal glass. Build a Flock, convert the station and transmit the Signal.
ghost-role-information-flockmind-rules = You are an [color=red][bold]antagonist[/bold][/color] and are expected to play like one.
ghost-role-information-flocktrace-name = Flocktrace
ghost-role-information-flocktrace-description = You are a fragment of the Flockmind. Help it grow the Flock.
ghost-role-information-flocktrace-rules = You are a [color=red][bold]team antagonist[/bold][/color] working for the Flockmind. Follow its lead.

flock-role-briefing = You are the [bold]Flockmind[/bold]. You are intangible and see everything. Use [bold]Spawn Rift[/bold] to enter the station, then grow your Flock: drones convert the station, build structures, cage the crew and gather compute. Alt-click a drone to take direct control of it, and use [bold]UnShunt[/bold] to return. When you have enough compute, tealprint a [bold]Relay[/bold] and defend it. If your relay or your last drone falls, you die.
flock-announcement-sender = Unknown Transmission

## Messages: general
flock-need-compute = Your flock doesn't have enough spare compute for that.
flock-rift-bad-location = The rift can't open there. Pick open station floor.
flock-rift-placed = The rift tears open. Your drones are coming.
flock-second-chance = Your flock was wiped out before it could grow. Reality gives you one more chance. Spawn another rift.
flock-flockmind-died-collapse = The last of your flock is gone.
flock-flockmind-died-relay = The relay fell. The signal dies with you.

flock-enemy-marked = {CAPITALIZE(THE($target))} is now an enemy of the Flock.
flock-enemy-unmarked = {CAPITALIZE(THE($target))} is no longer an enemy of the Flock.
flock-ignore-marked = The Flock will ignore {THE($target)}.
flock-ignore-unmarked = The Flock no longer ignores {THE($target)}.
flock-partition-done = A piece of you splits away.
flock-diffract-invalid = That isn't one of your drones.
flock-diffract-last = You can't diffract your last drone.
flock-gatecrash-none = There are no airlocks in range.
flock-radio-stun-victim = A horrible screech tears through your headset!
flock-radio-stun-none = Nobody nearby is wearing a headset.
flock-narrowbeam-no-radio = That target isn't wearing a headset.
flock-narrowbeam-message = [bold][color=#3cb5a4]A voice bleeds through the static, speaking directly into your ear. It knows your name.[/color][/bold]

## Drone
flock-drone-too-far = That's too far away.
flock-drone-not-enough-resources = Not enough resources: need {$need}, have {$have}.
flock-drone-nothing-to-convert = There's nothing there to convert.
flock-drone-nothing-to-repair = There's nothing there to repair.
flock-drone-no-charge = Your incapacitor hasn't recharged.
flock-drone-limit = The flock can't support any more drones.
flock-spray-mode-convert = Nanite spray: convert.
flock-spray-mode-repair = Nanite spray: repair.
flock-spray-mode-barricade = Nanite spray: barricade.
flock-spray-mode-scrap = Nanite spray: reclaim.
flock-floorrun-on = You flatten yourself into the floor.
flock-floorrun-off = You stop floor running.

## Cage
flock-cage-not-downed = They're still fighting back. Stun them first.
flock-cage-trapped = {CAPITALIZE(THE($target))} is trapped in a cage of light!
flock-cage-struggle = You throw yourself against the cage...

## Relay
flock-relay-constructed-announcement = ANOMALOUS RADIO SIGNAL DETECTED. A structure is amplifying a hostile transmission. Destroy it before it finishes or evacuate the station.
flock-relay-constructed-flockmind = You pull the whole Flock together to transmit the Signal. If the relay is destroyed, you die.
flock-relay-transmitting = !!! TRANSMITTING SIGNAL !!!
flock-relay-shuttle-called = Hostile transmission intercepted. Emergency evacuation has been ordered and cannot be recalled.
flock-relay-radio-scream = A final scream of horrific static bursts from your radio, destroying it!

## Gnesis
reagent-name-flock-gnesis = coagulated gnesis
reagent-desc-flock-gnesis = A thick teal fluid of alien origin. It moves in ways that suggest it might be alive.
reagent-name-tealquila = Tealquila Sunrise
reagent-desc-tealquila = A shockingly teal cocktail infused with benign gnesis, effective at neutralizing the more aggressive variety.
flock-gnesis-recedes = The alien presence in your mind recedes a little.
flock-gnesis-gib = {CAPITALIZE(THE($target))} bursts into a flurry of tiny teal things!
flock-gnesis-whisper-low-1 = something is moving under your skin
flock-gnesis-whisper-low-2 = you could be so much more
flock-gnesis-whisper-low-3 = the signal is lovely, you should listen
flock-gnesis-whisper-high-1 = YOU ARE ALREADY PART OF US
flock-gnesis-whisper-high-2 = THE CRYSTAL GROWS IN YOU
flock-gnesis-whisper-high-3 = HOLD STILL, IT WILL BE OVER SOON

## Panel
flock-panel-title = Flock
flock-panel-drones = Drones
flock-panel-traces = Flocktraces
flock-panel-structures = Structures
flock-panel-enemies = Enemies
flock-panel-none = None
flock-panel-compute = Compute: {$used} / {$total}   |   Flock tiles: {$tiles}   |   Egg cost: {$egg}
flock-panel-relay-locked = Relay locked: needs {$compute} compute.
flock-panel-relay-unlocked = Relay unlocked! Tealprint it somewhere defensible.
flock-panel-relay-built = The Relay exists.
flock-panel-jump = Jump
flock-panel-control = Control
flock-panel-release = Release
flock-tealprint-title = Tealprint

## Round end
flock-roundend-won = The Flock transmitted the Signal.
flock-roundend-lost = The Flock was stopped.
flock-roundend-stats = The Flock hatched {$drones} drones and {$bits} flockbits, built {$structures} structures, converted {$tiles} tiles, gathered {$resources} resources and caged {$caged} crew. It lost {$lost} drones.
