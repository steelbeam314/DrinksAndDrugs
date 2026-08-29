# Drinks and Drugs

A Casualties Unknown mod that adds custom liquids, drink/syringe items, status effects, and character classes.

Requires [CUCoreLib](https://cucorelib.web.app/) and BepInEx.

## Install

1. Install BepInEx and CUCoreLib.
2. Download the real [`plugin/DrinksAndDrugs.dll`](https://github.com/steelbeam314/DrinksAndDrugs/raw/main/plugin/DrinksAndDrugs.dll) (use that raw link, not the GitHub file page). It should be tens of KB, not a few KB HTML download.
3. Copy it to `Casualties Unknown Demo/BepInEx/plugins/DrinksAndDrugs/DrinksAndDrugs.dll`.
4. After launch, `BepInEx/LogOutput.log` should contain `Plugin DrinksAndDrugs is loaded!`

## Features

- **Liquids:** Distilled Tonic, Death Juice, Stim Fluid, Brainfuck, Liquid Nitrogen, pickle brine, peanut butter, and Axyltallisal
- **Items:** prefilled bottles/jars and syringes
- **Statuses and moodles:** Death Juice cooling/fever, Brainfuck brain drain, Failure peanut allergy, Axyltallisal knockout
- **Classes:** Survivor, Drug Tester, Failure, Nameless, or Cannibal, chosen on the pre-run screen (`setclass` in multiplayer)

Drug Testers cannot start syringe injections and have doubled overdose thresholds.

## Build

1. Install the game, BepInEx, and CUCoreLib.
2. Confirm `vars.targets` points at your game folder.
3. Build `DrinksAndDrugs/DrinksAndDrugs.csproj`.

The build copies `DrinksAndDrugs.dll` to `BepInEx/plugins/DrinksAndDrugs`.
