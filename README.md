# Animals at Work — Plowing

*Every beast earns its keep.*

First module of the **Animals at Work** series for RimWorld 1.6: animals doing useful, autonomous work.

## Features

- **No training, no combat cows.** Draft work is a matter of gear, not intelligence. True draft species (horses, donkeys, cattle, muffalo, bison, yaks, dromedaries and elephants; animal mods can opt in with a two-line patch) equip themselves and get to work. An **Unhitch** button takes it all off.
- **Draft harness.** Stitched from leather, the foundation of all draft work, donned by the beast itself and carried as gear (~200 jobs per harness). Wear shows on the animal's inspect panel; an alert fires when work is waiting with no harness left.
- **Plow.** Wood and share (~50 cells per share). A harnessed beast drags it across your growing zones (+30% fertility), never in frozen ground, and only where the zone allows it (one toggle per growing zone). The bigger the beast, the faster the furrow. The soil settles back after a season; the work never ends.
- **Draft cart.** Hitched by any harnessed beast, drawn behind it with the cargo in plain sight. Loads several stacks at once (up to 300 kg / 8 stacks) and delivers the whole round in one trip, no more back-and-forth, plus an extra 100 kg in caravans. Wears out after ~100 loaded stacks. One implement per beast: plow, cart or scraper; gear drops when the beast dies.
- **Snow scraper.** A broad wooden blade (~100 cells per blade) for the same beasts whose plows sit idle in frozen ground. A harnessed beast drags it across the vanilla snow-clearing area and scrapes the cells bare, with no colonist shovels, and snow clearing never trained a skill anyway. Beasts swap implements with the seasons on their own: plow down when the earth freezes over or is fully turned, scraper down when the snow is gone.
- **Tack rack.** A dedicated stand where colonists store harnesses, plows, carts and scrapers (three pieces per cell, no deterioration). Beasts help themselves; carts never treat draft gear as cargo.

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
