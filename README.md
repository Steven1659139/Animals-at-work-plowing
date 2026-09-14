# Animals at Work — Plowing

First module of the **Animals at Work** series for RimWorld 1.6: animals doing useful work.

## Features

- **Choosing which animals work.** You turn on a task on the animal itself (**Plowing**, **Cart hauling**, **Snow clearing**), and each toggle only appears once its research is unlocked. The eligible species are horses, donkeys, cattle, muffalo, bison, yaks, dromedaries and elephants; none of them needs training, because draft work depends on the equipment rather than on what the animal can be taught.
- **Modded animals work out of the box.** Any species vanilla considers a pack animal is eligible, which covers most modded beasts of burden without you or the mod author doing anything. Beyond that, open the mod settings and tick anything else you want harnessed, or untick what you do not. Every animal in your game is listed there, largest first, with a search box, and each row says why it is already eligible. Your choice applies everywhere at once: plowing, cart hauling, snow clearing, the harness alert and the inspect panel. Being eligible only makes the task switches appear; you still turn each animal's work on yourself.
- **Colonist handling.** A colonist assigned to Animal handling brings the harness and the right implement, equips the animal, then leads it to the work area on a rope. Once there is nothing left to do, a colonist leads it back to its pen. While an animal is working it is not treated as loose livestock, so no handler comes to bring it back, no pen alert is raised, and it will not wander off. It also leaves sown crops alone and feeds from a trough or from wild plants, and a colonist leads it home if it reaches starvation.
- **Herding dogs as handlers.** With the [Herding Dogs](https://github.com/Steven1659139/Animals-at-Work-Herding-Dogs) module installed, a dog trained in herding does the leading in the colonists' place: the colonist harnesses the animal in the pen, the dog takes it out to the work area and brings it back once there is nothing left to do. Harnessing needs hands, so it stays with the colonists. Neither module requires the other.
- **Draft harness.** Made of leather and required for every draft task, it lasts well over a thousand jobs in plain leather, and longer still in a tougher hide. Its condition is shown on the animal's inspect panel, and an alert appears when work is waiting and no harness is available.
- **Plow.** A beam and a share, good for about 200 cells in wood, around 300 in steel, over 800 in plasteel. It turns the soil of a growing zone and raises its fertility by 30%, is never used on frozen ground, and each growing zone has its own toggle deciding whether it may be plowed there. Larger animals plow faster, and the soil returns to its original state after one season. Leave at least three cells between a growing zone and any fence: see *Known limitation* below.
- **Draft cart.** Carries several stacks at once (up to 300 kg, or as much as the animal can carry without being overloaded, whichever is less) and delivers them in a single trip instead of going back and forth. It also adds 100 kg of carrying capacity in caravans, and wears out after about 400 loaded stacks.
- **Snow scraper.** A blade good for about 400 cells in wood, half again as many in steel, for the same animals whose plows sit idle in frozen ground. It clears the vanilla snow-clearing area without a colonist having to shovel it. A colonist swaps the plow for the scraper when the ground freezes or the soil is fully turned, and swaps it back once the snow is gone.
- **One implement at a time.** An animal carries either the plow, the cart or the scraper, and its equipment falls to the ground when it dies.
- **Tack rack.** A stand where colonists store harnesses, plows, carts and scrapers, three items per cell and no deterioration. They draw from it to equip an animal and put the gear back when it is done, and carts never treat draft equipment as cargo.

Requires [Harmony](https://github.com/pardeike/HarmonyRimWorld). Standalone module. English + French included.

## For animal mod authors

If your species is a pack animal (`<packAnimal>true</packAnimal>`), it is already eligible and you have nothing to do. Otherwise players can tick it in the mod settings, or you can mark it yourself so they never have to. Drop this in a file under your mod's `Patches/` folder; `MayRequire` makes the whole operation vanish when Plowing is not installed, so it is safe to ship unconditionally:

```xml
<Operation Class="PatchOperationAddModExtension" MayRequire="royaltea.animalsatwork.plowing">
  <xpath>Defs/ThingDef[defName="YourAnimal"]</xpath>
  <value>
    <li Class="AnimalsAtWork.Plowing.ModExtension_BeteDeTrait" />
  </value>
</Operation>
```

No training and no research gating on your side: draft work depends on the equipment, so a marked species can be harnessed as soon as the player has researched it. A player can still untick a species you marked, if they would rather not harness it.

*(The [Herding Dogs](https://github.com/Steven1659139/Animals-at-Work-Herding-Dogs) module solves the same problem through vanilla training instead: herding carries the same requirements as vanilla hauling, so anything that hauls already herds, and mod authors steer it with the standard `trainableTags` and `untrainableTags`.)*

## Known limitation

**Keep growing zones clear of fences, at least three cells.** A zone that sits right against a fence, and above all one enclosed inside an animal pen, can leave the colonist leading the animal going back and forth through the gate instead of settling it down to work.

The cause is a pile-up of vanilla rules that contradict each other around a rope. A roped animal may cross a gate it could never open on its own (`Building_Door.PawnCanOpen` accepts `IsRopedByPawn`), so while the rope is held the animal *looks* able to reach the work from the wrong side of the fence, and once released it no longer is. The mod already checks proximity, reachability and pen membership before dropping the rope; this configuration still slips through. Fields laid out in the open are unaffected.

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
