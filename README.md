# Drinks and Drugs

A Casualties Unknown mod that adds custom liquids, drink/syringe items, status effects, and a Scavenger / Drug Tester class choice.

Requires [CUCoreLib](https://cucorelib.web.app/) and BepInEx.

## Features

- **Liquids:** Distilled Tonic, Death Juice, Stim Fluid, Brainfuck, and Liquid Nitrogen
- **Items:** prefilled bottles (500 mL) and syringes (100 mL)
- **Statuses and moodles:** Death Juice cooling/fever, Brainfuck brain drain
- **Classes:** Scavenger (default) or Drug Tester, chosen on the pre-run screen (`setclass` in multiplayer)

Drug Testers cannot start syringe injections and have doubled overdose thresholds.

## Build

1. Install the game, BepInEx, and CUCoreLib.
2. Confirm `vars.targets` points at your game folder.
3. Build `DrinksAndDrugs/DrinksAndDrugs.csproj`.

The build copies `DrinksAndDrugs.dll` to `BepInEx/plugins/DrinksAndDrugs`.
