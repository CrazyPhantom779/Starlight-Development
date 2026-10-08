# Wizard rework: v3

Base: your **Pt2 commit f963a40c7** ("Remove scripts"). Four patches, in order. `--binary`, so the new icons are included.
Not compiled or run (no NuGet access). Verified instead: every C# file parses; every YAML parses; every prototype id the
wizard YAML references exists in the repo; every component name used in a whitelist exists; every locale key used in
code or YAML exists with no duplicates; the patches reproduce the tree via `git am`. Built to your conventions
(partial UI classes, `field`, `[SubscribeLocalEvent]`, no parentheses inside `* / %` groups, no hiding `Margin`/`Owner`).

```
git checkout Winds-of-Magic
git am patches/*.patch
```
Or copy `files/` over the repo root (it holds every added/changed file, PNGs included).
`tools/` regenerates glyph/rote/tarot/errand YAML, icons and locale: `python3 tools/gen_glyphs.py <repo>` and
`python3 tools/gen_errands.py <repo>`. `BALANCE.md` lists every cost.

## What's new

**Wounds slow Wind.** Wind recovery falls toward 20% as you near critical. The value is settled whenever damage changes, so the
lazy regeneration stays exact. Hover the Wind bar to see your rate and whether wounds are slowing it.

**Randomisation.**
- **Tides:** each wizard has a per-school cost multiplier (x0.8 to x1.3) that drifts every 5-9 minutes; woven spell costs follow, and you get a message when a school runs hot or cool. Shown as chips in the window.
- **Variance:** every spell step varies in strength (±6%, wider with Instability).
- **16 Aspects** (8 new), **8 Gusts**, the existing random starting kit, and random objectives and errands.

**Errands (power separate from objectives).** Each wizard always has 3 errands, drawn at random from 26, each from a different
group so you get sent to different places: spend time near a department's machinery (bridge console, cargo pallets, medical, science, engines,
gardens, chemistry, vending, lathes, fax), cast near airlocks/APCs/computers, affect N different creatures, use a school, place runes / fire
bolts / touch, perform a rite. Each one gives a reward (max Wind, regen, node/slot limits, glyph, rote, attunement) **and a point of Mastery**
(+4 max Wind, +0.06 Wind/s), up to 12. New **Errands tab**, with progress bars. The window refreshes live.

**84 glyphs** (15 new): Lightning *Smite*, polymorph *Hex*, *Mire*/*Wither* (slow), *Quickening* (haste), *Leap*, *Slam*, *Gale*, *Brand*,
*Jolt*, *Purge*, banana peels, webs. New **Beam** form (a line, stopped by walls, never hits anyone twice). New **Swift** augment. 44 rotes (14 new).

**Balance.** Costs retuned (see BALANCE.md). Chain jumps now weaken (x0.75 each) and cost more. **Replenish can no longer refill its caster** (that was an
infinite-Wind loop). Starting wizards only get effects costing 20 or less; the strongest are earned. Base regen 1.6/s.

**Objectives.** 8 new random objectives: perform rites, play tarot cards, read scrolls, bind spells, affect creatures, use N schools, finish errands,
reach a max Wind. Counters now cover everything (items, rites, bolts). Fixed overcast counting.

**UI.** Minimum size 700x480 (was 780x560); tabs scroll; lists keep their scroll position when state refreshes (KeptScroll); views only rebuild
when their content changes (so clicks are never lost to a refresh); Sigil tab layout fixed; circuit canvas fits small windows; tooltips for tides and regen.

## Test guide
- `SpellweaverTomeDebug`: everything unlocked. Errands tab shows 3 errands; `spellcraft_errand_done` finishes one.
- **Wounds:** take damage, hover the Wind bar.
- **Tides:** `spellcraft_tide Fire 1.3`; costs update.
- **Beam:** Glyphwork, Beam + Chill (or Beam + Scorch + Widen).
- **Hex:** Touch + Hex on someone. **Smite:** Aimed + Smite. **Leap:** Aimed + Leap.
- Objectives: make a wizard and check their list (3 plain objectives + Survive).

## Likeliest trouble spots
- `LightningGlyphEffect`, `PolymorphGlyphEffect`, `SlowGlyphEffect`/`HasteGlyphEffect` call engine systems (`LightningSystem`, `PolymorphSystem`, `MovementModStatusSystem`) that I checked by signature only.
- Errand proximity uses `LookupFlags.Static | Dynamic | Sundries`; if a machine isn't found, check its component name in the whitelist.
- `KeptScroll` restores position over 4 frames; if scroll jumps, that's where to look.
- Tide/wound popups use `PopupType.Medium`/`LargeCaution`.

## Still not built
Prep-space map and non-euclidean rooms, "Touched" status, wand-part station affinities, counterplay items and the counter-arcane ERT, Lore Fragments research, the Ascendant tier, speech scrambling.
