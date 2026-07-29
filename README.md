# Animals at Work — Plowing

*Every beast earns its keep.*

First module of the **Animals at Work** series for RimWorld 1.6: animals doing useful, autonomous work.

## Features

- **No training, no combat cows.** Draft work is a matter of gear, not intelligence. True draft species qualify (horses, donkeys, cattle, muffalo, bison, yaks, dromedaries and elephants; animal mods can opt in with a two-line patch) — but none of them is a draft beast until you say so. One toggle per task on the beast itself (**Plowing**, **Cart hauling**, **Snow clearing**), each appearing once its research is unlocked.
- **A colonist does the handling.** Under Animal handling, a colonist fetches the harness and the right implement, gears the beast up, then leads it out to the field on a rope. When no work is left, a colonist ropes it and walks it back to its pen — beasts that can't leave a pen on their own included. Out working, a beast won't graze your sown crops: it feeds from a trough or wild plants, and a colonist brings it home if it ever reaches real starvation.
- **Draft harness.** Stitched from leather, the foundation of all draft work, buckled on by a colonist and carried as gear (~200 jobs per harness). Wear shows on the animal's inspect panel; an alert fires when work is waiting with no harness left.
- **Plow.** Wood and share (~50 cells per share). A harnessed beast drags it across your growing zones (+30% fertility), never in frozen ground, and only where the zone allows it (one toggle per growing zone). The bigger the beast, the faster the furrow. The soil settles back after a season; the work never ends.
- **Draft cart.** Hitched by any harnessed beast, drawn behind it with the cargo in plain sight. Loads several stacks at once (up to 300 kg / 8 stacks) and delivers the whole round in one trip, no more back-and-forth, plus an extra 100 kg in caravans. Wears out after ~100 loaded stacks. One implement per beast: plow, cart or scraper; gear drops when the beast dies.
- **Snow scraper.** A broad wooden blade (~100 cells per blade) for the same beasts whose plows sit idle in frozen ground. A harnessed beast drags it across the vanilla snow-clearing area and scrapes the cells bare, with no colonist shovels, and snow clearing never trained a skill anyway. Implements still swap with the seasons, colonist-driven: the plow gives way to the scraper when the earth freezes over or is fully turned, and back again when the snow is gone.
- **Tack rack.** A dedicated stand where colonists store harnesses, plows, carts and scrapers (three pieces per cell, no deterioration). They draw from it to gear a beast up and put the gear back when it's done; carts never treat draft gear as cargo.

Requires [Harmony](https://github.com/pardeike/HarmonyRimWorld). Standalone module. English + French included.

## The series

| Module | Repo |
|---|---|
| Plowing | *(this repo)* |
| Herding Dogs | [Animals-at-Work-Herding-Dogs](https://github.com/Steven1659139/Animals-at-Work-Herding-Dogs) |
| Handy Monkeys | [Animals-at-Work-Handy-Monkeys](https://github.com/Steven1659139/Animals-at-Work-Handy-Monkeys) |

## Manual install

Clone or download this repository into your RimWorld `Mods` folder, then enable **Animals at Work — Plowing** in the in-game mod list.

## Building from source

```
cd Source && dotnet build
```

Targets `net472` against [Krafs.Rimworld.Ref](https://www.nuget.org/packages/Krafs.Rimworld.Ref); the DLL lands in `Assemblies/`.

---

By **Royal-Tea**.
