using System.Collections.Generic;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

namespace DrinksAndDrugs
{
    public partial class Plugin
    {
        private void RegisterLiquids()
        {
            // CUCoreLib turns this into a vanilla LiquidType and locale entries.
            // See https://cucorelib.web.app/docs/liquids/
            LiquidRegistry.Register("distilledtonic", new CustomLiquidInfo
            {
                name = "Distilled Tonic",
                description = "Extremely potent, oddly sweet, but smells strangely like the labs.",
                color = new Color(0.77f, 0.41f, 0.11f),

                valuePerLiter = 20f,
                unobtainable = true,
                injectable = true,
                injectionSickness = 0.2f,
                onInject = (ml, limb) =>
                {
                    // 100 ml is treated as one full syringe.
                    float dose = ml * 0.01f;
                    Body body = limb.body;
                    body.happiness += dose * 3f;
                    body.temperature += dose * 0.5f;
                },
                qualities = new List<CraftingQuality>
                {
                    CUCoreUtils.CreateCraftingQuality("water", 0.8f)
                }
            });

            LiquidRegistry.Register("deathjuice", new CustomLiquidInfo
            {
                name = "Death Juice",
                description = "It smells so good... but you know what? You're not sure you want to drink it.",
                color = new Color(0.15f, 0.1f, 0.1f),

                valuePerLiter = 20f,
                unobtainable = true,
                injectable = true,
                injectionSickness = 0f,
                onDrink = (ml, body) => ApplyDeathJuice(body),
                onInject = (ml, limb) => ApplyDeathJuice(limb.body),
                qualities = new List<CraftingQuality>
                {
                    CUCoreUtils.CreateCraftingQuality("water", 0.8f)
                }
            });

            LiquidRegistry.Register("stimfluid", new CustomLiquidInfo
            {
                name = "Stim Fluid",
                description = "Used by the military in the heroic crusade against their enemies...  Which happens to include the experiments on occasion.",
                color = new Color(0.2f, 0.85f, 0.55f),

                valuePerLiter = 35f,
                unobtainable = true,
                injectable = true,
                injectionSickness = 0.15f,
                onInject = (ml, limb) => ApplyStimFluid(limb, ml)
            });

            LiquidRegistry.Register("brainfuck", new CustomLiquidInfo
            {
                name = "Brainfuck",
                description = "The scientists used this to neutralize experiments before euthanization...",
                color = new Color(0.72f, 0.18f, 0.55f),

                valuePerLiter = 12f,
                unobtainable = true,
                injectable = true,
                injectionSickness = 0.6f,
                onDrink = (ml, body) => ApplyBrainfuckDrink(body, ml),
                onInject = (ml, limb) => ApplyBrainfuck(limb.body, ml),
                onHealthUse = (ml, limb) => ApplyBrainfuck(limb.body, ml)
            });

            LiquidRegistry.Register("liquidnitrogen", new CustomLiquidInfo
            {
                name = "Liquid Nitrogen",
                description = "It's terribly cold, but you feel like you've seen it somewhere before...",
                color = new Color(0.72f, 0.88f, 1f),

                valuePerLiter = 15f,
                unobtainable = true,
                injectable = false,
                onDrink = (ml, body) =>
                {
                    float dose = ml * 0.01f;
                    body.temperature -= dose * 10f;
                }
            });

            Logger.LogInfo("Registered liquids: Distilled Tonic, Death Juice, Stim Fluid, Brainfuck, Liquid Nitrogen");
        }

        private void RegisterLiquidContainers()
        {
            Color tonic = new Color(0.77f, 0.41f, 0.11f);
            Color deathJuice = new Color(0.15f, 0.1f, 0.1f);
            Color stim = new Color(0.2f, 0.85f, 0.55f);
            Color brainfuck = new Color(0.72f, 0.18f, 0.55f);
            Color nitrogen = new Color(0.72f, 0.88f, 1f);

            RegisterDrinkBottle(
                "deathjuicebottle",
                "Death Juice bottle",
                "A dark bottle that smells better than it should.",
                "deathjuice",
                deathJuice,
                value: 8,
                dropPool: DropPool.MedicalCrate | DropPool.Corpse);

            RegisterDrinkBottle(
                "brainfuckbottle",
                "Brainfuck bottle",
                "A sealed lab bottle. Drinking it is a bad idea.",
                "brainfuck",
                brainfuck,
                value: 6,
                dropPool: DropPool.MedicalCrate | DropPool.Corpse);

            RegisterDrinkBottle(
                "liquidnitrogenbottle",
                "Liquid Nitrogen bottle",
                "A frost-covered bottle that bites the air around it.",
                "liquidnitrogen",
                nitrogen,
                value: 5,
                dropPool: DropPool.MedicalCrate | DropPool.ContainerCrate);

            RegisterSyringe(
                "distilledtonicsyringe",
                "Distilled Tonic syringe",
                "A prefilled syringe of oddly sweet lab tonic.",
                "distilledtonic",
                tonic,
                value: 6,
                dropPool: DropPool.MedicalCrate | DropPool.AllTraders);

            RegisterSyringe(
                "deathjuicesyringe",
                "Death Juice syringe",
                "A prefilled syringe of something you probably should not inject.",
                "deathjuice",
                deathJuice,
                value: 10,
                dropPool: DropPool.MedicalCrate | DropPool.Corpse);

            RegisterSyringe(
                "stimfluidsyringe",
                "Stim Fluid syringe",
                "A military stimulant injector. One full syringe is a complete dose.",
                "stimfluid",
                stim,
                value: 12,
                dropPool: DropPool.MedicalCrate | DropPool.Corpse);

            RegisterSyringe(
                "brainfucksyringe",
                "Brainfuck syringe",
                "A neutralization syringe. A full 100 mL dose starts the brain drain.",
                "brainfuck",
                brainfuck,
                value: 9,
                dropPool: DropPool.MedicalCrate | DropPool.Corpse);

            Logger.LogInfo("Registered liquid containers: bottles and syringes");
        }

        private static void RegisterDrinkBottle(
            string id,
            string name,
            string description,
            string liquidId,
            Color liquidColor,
            int value,
            DropPool dropPool)
        {
            ItemRegistry.Register(
                id,
                new CustomItemInfo
                {
                    fullName = name,
                    description = description,
                    category = "water",
                    slotRotation = -45f,
                    tags = "cangetwet",
                    usable = true,
                    usableOnLimb = false,
                    destroyAtZeroCondition = false,
                    combineable = true,
                    weight = 1.25f,
                    scaleWeightWithCondition = true,
                    capacity = 500f,
                    autoFill = false,
                    LiquidMask = ItemIcons.BottleMask(),
                    defaultContents = new List<LiquidStack>
                    {
                        new LiquidStack(liquidId, 500f)
                    },
                    useAction = (body, item) =>
                    {
                        WaterContainerItem container = item.GetComponent<WaterContainerItem>();
                        if (container != null)
                            container.Drink(body);
                    },
                    value = value,
                    rec = new Recognition(2),
                    DropPool = dropPool,
                    SpawnFrequency = 1
                },
                ItemIcons.Bottle(liquidColor));
        }

        private static void RegisterSyringe(
            string id,
            string name,
            string description,
            string liquidId,
            Color liquidColor,
            int value,
            DropPool dropPool)
        {
            ItemRegistry.Register(
                id,
                new CustomItemInfo
                {
                    fullName = name,
                    description = description,
                    category = "medicine",
                    tags = "medicine",
                    slotRotation = -45f,
                    destroyAtZeroCondition = false,
                    combineable = true,
                    scaleWeightWithCondition = true,
                    weight = 0.25f,
                    value = value,
                    rec = new Recognition(5),
                    Syringe = new SyringeProperties
                    {
                        Capacity = 100f,
                        AmountPerFullUse = 100f,
                        AutoFill = false,
                        UseAverageColor = true,
                        DefaultContents = new List<LiquidStack>
                        {
                            new LiquidStack(liquidId, 100f)
                        }
                    },
                    DropPool = dropPool,
                    SpawnFrequency = 1
                },
                ItemIcons.Syringe(liquidColor));
        }

        /// <summary>
        /// Instantly clears physical injuries, cancels any active fever,
        /// cools toward 29C over 30s, then starts the delayed fever.
        /// </summary>
        private static void ApplyDeathJuice(Body body)
        {
            HealPhysicalInjuries(body);

            DeathJuiceStatus status = body.GetStatus<DeathJuiceStatus>();
            status.FeverActive = false;
            status.CoolingActive = true;
            status.CoolingElapsed = 0f;
            status.CoolingStartTemperature = body.temperature;
            // Only cool downward; if already at/below 29C, hold steady during the wait.
            status.CoolingTargetTemperature = Mathf.Min(
                DeathJuiceStatus.CoolTargetCelsius,
                status.CoolingStartTemperature);
        }

        /// <summary>
        /// Drinking Brainfuck only nauseates; it does not start the brain-drain dose.
        /// </summary>
        private static void ApplyBrainfuckDrink(Body body, float ml)
        {
            if (ml <= 0f)
                return;

            float dose = ml * 0.01f;
            body.sicknessAmount = Mathf.Min(100f, body.sicknessAmount + dose * 40f);
        }

        /// <summary>
        /// Injected dose: drops mood, then drains 90% of current brain health over 10 seconds.
        /// Requires 100 mL total; partial injections add up until a full dose is reached.
        /// </summary>
        private static void ApplyBrainfuck(Body body, float ml)
        {
            if (ml <= 0f)
                return;

            BrainfuckStatus status = body.GetStatus<BrainfuckStatus>();
            status.AbsorbedMl += ml;
            if (status.AbsorbedMl < 100f)
                return;

            status.AbsorbedMl = 0f;
            body.happiness -= 10f;
            status.Draining = true;
            status.Elapsed = 0f;
            status.StartBrainHealth = body.brainHealth;
            status.TargetBrainHealth = body.brainHealth * 0.1f;
            Logger.LogInfo($"Brainfuck full dose: draining brain {status.StartBrainHealth:F1} -> {status.TargetBrainHealth:F1}");
        }

        /// <summary>
        /// Fully heals the injected limb, then heals adjacent tissue and stops nearby bleeding
        /// without resetting neighboring fractures or dislocations.
        /// Requires a full 100 mL dose; smaller injections do nothing.
        /// </summary>
        private static void ApplyStimFluid(Limb limb, float ml)
        {
            if (ml < 100f)
                return;

            HealLimbCompletely(limb);

            if (limb.connectedLimbs != null)
            {
                foreach (Limb adjacent in limb.connectedLimbs)
                {
                    if (adjacent == null)
                        continue;

                    HealAdjacentLimbTissue(adjacent);
                }
            }

            limb.body.happiness += 8f;
        }

        private static void HealLimbCompletely(Limb limb)
        {
            limb.muscleHealth = 100f;
            limb.skinHealth = 100f;
            limb.boneHealTimer = 0f;
            limb.dislocationTimer = 0f;
            limb.infectionAmount = 0f;
            limb.bleedAmount = 0f;
            limb.pain = 0f;
            limb.shrapnel = 0;
            limb.infected = false;
            limb.broken = false;
            limb.dislocated = false;
            limb.strokeAffected = false;
        }

        private static void HealAdjacentLimbTissue(Limb limb)
        {
            limb.muscleHealth = 100f;
            limb.skinHealth = 100f;
            limb.infectionAmount = 0f;
            limb.bleedAmount = 0f;
            limb.pain = 0f;
            limb.shrapnel = 0;
            limb.infected = false;
            limb.strokeAffected = false;
        }

        /// <summary>
        /// Restores limb/body trauma without wiping hunger, thirst, mood, or temperature.
        /// </summary>
        private static void HealPhysicalInjuries(Body body)
        {
            foreach (Limb limb in body.limbs)
            {
                limb.muscleHealth = 100f;
                limb.skinHealth = 100f;
                limb.boneHealTimer = 0f;
                limb.dislocationTimer = 0f;
                limb.infectionAmount = 0f;
                limb.bleedAmount = 0f;
                limb.pain = 0f;
                limb.shrapnel = 0;
                limb.infected = false;
                limb.broken = false;
                limb.dislocated = false;
                limb.strokeAffected = false;
            }

            body.brainHealth = 100f;
            body.bloodVolume = 100f;
            body.bloodOxygen = 100f;
            body.bloodPressure = 120f;
            body.heartRate = 70f;
            body.bloodVesselSize = 1f;
            body.bloodViscosity = 0f;
            body.respiratoryRate = 100f;
            body.strokeAmount = 0f;
            body.hasPulmonaryEmbolism = false;
            body.fibrillationProgress = 0f;
            body.septicShock = 0f;
            body.shock = 0f;
            body.painShock = 0f;
            body.consciousness = 100f;
            body.internalBleeding = 0f;
            body.hemothorax = 0f;
            body.traumaAmount = 0f;
            body.hearingLoss = 0f;
            body.clawHealth = 100f;
            body.clawRegrowTime = 0f;
            body.disfigured = false;
            body.eyeGone = false;
            body.bothEyesGone = false;
        }
    }
}
