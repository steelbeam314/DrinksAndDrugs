using System.Collections.Generic;
using System.Reflection;
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
                description = "Precursor to most chemicals made by the company, smells strangely fruity. Toxic if swallowed.",
                color = new Color(0.77f, 0.41f, 0.11f),

                valuePerLiter = 20f,
                unobtainable = true,
                injectable = true,
                injectionSickness = 0.2f,
                onDrink = (ml, body) => ApplyDistilledTonicDrink(body, ml),
                onInject = (ml, limb) =>
                {
                    // 100 ml is treated as one full syringe.
                    float dose = ml * 0.01f;
                    Body body = limb.body;
                    AxyltallisalStatus.NoteForeignDrug(body);
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
                description = "You can hear the whirring of the nanites held within the viscous fluid.",
                color = new Color(0.15f, 0.1f, 0.1f),

                valuePerLiter = 20f,
                unobtainable = true,
                injectable = true,
                injectionSickness = 0f,
                onDrink = (ml, body) => ApplyDeathJuice(body, ml),
                onInject = (ml, limb) => ApplyDeathJuice(limb.body, ml),
                qualities = new List<CraftingQuality>
                {
                    CUCoreUtils.CreateCraftingQuality("water", 0.8f)
                }
            });

            LiquidRegistry.Register("stimfluid", new CustomLiquidInfo
            {
                name = "War Stimulant",
                description = "Used by the military to quickly bring soldiers back to action.  The label says that they are non-addictive.",
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
                onInject = (ml, limb) => ApplyBrainfuck(limb.body, ml)
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

            LiquidRegistry.Register("picklejuice", new CustomLiquidInfo
            {
                name = "Pickle Juice",
                description = "Salty, sour brine left behind after the pickles are gone.",
                color = new Color(0.72f, 0.78f, 0.42f),

                valuePerLiter = 8f,
                unobtainable = true,
                injectable = false,
                onDrink = (ml, body) =>
                {
                    float dose = ml * 0.01f;
                    body.Drink(dose * 6f);
                    body.happiness += dose * 0.5f;
                    body.temperature -= dose * 0.4f;
                },
                qualities = new List<CraftingQuality>
                {
                    CUCoreUtils.CreateCraftingQuality("water", 0.4f)
                }
            });

            LiquidRegistry.Register("peanutbutter", new CustomLiquidInfo
            {
                name = "Peanut Butter",
                description = "Thick, filling paste. Sits heavy in the stomach and is a little nauseating. Fatal to anyone with a peanut allergy.",
                color = new Color(0.78f, 0.55f, 0.22f),

                valuePerLiter = 10f,
                unobtainable = true,
                injectable = false,
                onDrink = (ml, body) => ApplyPeanutButter(body, ml)
            });

            LiquidRegistry.Register("axyltallisal", new CustomLiquidInfo
            {
                name = "Axyltallisal",
                description = "A rare red opiate. A 100 mL injection knocks you out; mixing it with other drugs is fatal. Do not swallow.",
                color = new Color(0.82f, 0.07f, 0.1f),

                valuePerLiter = 90f,
                unobtainable = false,
                injectable = true,
                injectionSickness = 0.15f,
                onDrink = (ml, body) => ApplyAxyltallisalDrink(body, ml),
                onHealthUse = (ml, limb) => ApplyAxyltallisalInject(limb != null ? limb.body : null, ml),
                onInject = (ml, limb) => ApplyAxyltallisalInject(limb != null ? limb.body : null, ml),
                qualities = new List<CraftingQuality>
                {
                    CUCoreUtils.CreateCraftingQuality("opiate", 1f)
                }
            });

            Logger.LogInfo("Registered liquids: Distilled Tonic, Death Juice, Stim Fluid, Brainfuck, Liquid Nitrogen, Pickle Juice, Peanut Butter, Axyltallisal");
        }

        private void RegisterLiquidContainers()
        {
            Color tonic = new Color(0.77f, 0.41f, 0.11f);
            Color deathJuice = new Color(0.15f, 0.1f, 0.1f);
            Color stim = new Color(0.2f, 0.85f, 0.55f);
            Color brainfuck = new Color(0.72f, 0.18f, 0.55f);
            Color nitrogen = new Color(0.72f, 0.88f, 1f);
            Color axyltallisal = new Color(0.82f, 0.07f, 0.1f);

            RegisterDrinkBottle(
                "distilledtonicbottle",
                "Distilled Tonic bottle",
                "A bottle of oddly sweet lab tonic.",
                "distilledtonic",
                tonic,
                value: 5,
                dropPool: DropPool.MedicalCrate | DropPool.AllTraders,
                iconFile: "tonic_bottle.png");

            RegisterDrinkBottle(
                "brainfuckbottle",
                "Brainfuck bottle",
                "A sealed lab bottle. Drinking it is a bad idea.",
                "brainfuck",
                brainfuck,
                value: 6,
                dropPool: DropPool.MedicalCrate | DropPool.Corpse,
                iconFile: "brainfuck_bottle.png");

            RegisterDrinkBottle(
                "liquidnitrogenbottle",
                "Liquid Nitrogen bottle",
                "A frost-covered bottle that bites the air around it.",
                "liquidnitrogen",
                nitrogen,
                value: 5,
                dropPool: DropPool.MedicalCrate | DropPool.ContainerCrate,
                iconFile: "liquid_nitrogen_bottle.png");

            RegisterSyringe(
                "deathjuicesyringe",
                "Death Juice syringe",
                "A prefilled syringe of something you probably should not inject.",
                "deathjuice",
                deathJuice,
                value: 10,
                dropPool: DropPool.MedicalCrate | DropPool.Corpse,
                iconFile: "death_juice_syringe.png");

            RegisterSyringe(
                "stimfluidsyringe",
                "War Stimulant syringe",
                "A military stimulant injector. One full syringe is a complete dose.",
                "stimfluid",
                stim,
                value: 12,
                dropPool: DropPool.MedicalCrate | DropPool.Corpse,
                iconFile: "war_stim.png");

            RegisterSyringe(
                "axyltallisalsyringe",
                "Axyltallisal syringe",
                "A prefilled syringe of a rare red opiate. One full syringe is a complete dose.",
                "axyltallisal",
                axyltallisal,
                value: 18,
                dropPool: DropPool.MedicalCrate | DropPool.Corpse,
                iconFile: "axyltallisal_syringe.png");

            Logger.LogInfo("Registered liquid containers: bottles and syringes");
        }

        private void RegisterPickleItems()
        {
            Sprite pickledJarIcon = LoadAssetSprite("pickle_jar_pickled.png");
            Sprite brineJarIcon = LoadAssetSprite("pickle_jar.png");
            Sprite picklesIcon = LoadAssetSprite("pickle.png");

            Sprite jarLiquidMask = ItemIcons.JarMaskMatching(brineJarIcon);

            ItemRegistry.Register(
                "picklejar",
                new CustomItemInfo
                {
                    fullName = "Pickle Jar",
                    description = "A sealed glass jar packed with pickles.",
                    category = "food",
                    slotRotation = -20f,
                    tags = "cangetwet",
                    usable = true,
                    usableOnLimb = false,
                    destroyAtZeroCondition = false,
                    combineable = true,
                    weight = 1.6f,
                    scaleWeightWithCondition = true,
                    capacity = 400f,
                    autoFill = false,
                    LiquidMask = jarLiquidMask,
                    defaultContents = new List<LiquidStack>
                    {
                        new LiquidStack("picklejuice", 400f)
                    },
                    useAction = (body, item) =>
                    {
                        WaterContainerItem container = item.GetComponent<WaterContainerItem>();
                        if (container != null)
                            container.Drink(body);
                    },
                    value = 7,
                    rec = new Recognition(2),
                    DropPool = DropPool.FoodCrate | DropPool.AllTraders,
                    SpawnFrequency = 1,
                    SpriteScaleDimensions = (14f, 14f, true)
                },
                pickledJarIcon);

            ItemRegistry.Register(
                "picklejuicejar",
                new CustomItemInfo
                {
                    fullName = "Pickle Jar",
                    description = "The pickles are gone now.",
                    category = "food",
                    slotRotation = -20f,
                    tags = "cangetwet",
                    usable = true,
                    usableOnLimb = false,
                    destroyAtZeroCondition = false,
                    combineable = true,
                    weight = 1.2f,
                    scaleWeightWithCondition = true,
                    capacity = 400f,
                    autoFill = false,
                    LiquidMask = jarLiquidMask,
                    defaultContents = new List<LiquidStack>(),
                    useAction = (body, item) =>
                    {
                        WaterContainerItem container = item.GetComponent<WaterContainerItem>();
                        if (container != null)
                            container.Drink(body);
                    },
                    value = 3,
                    rec = new Recognition(2),
                    SpawnFrequency = 0,
                    SpriteScaleDimensions = (14f, 14f, true)
                },
                brineJarIcon);

            ItemRegistry.Register(
                "pickles",
                new CustomItemInfo
                {
                    fullName = "Pickles",
                    description = "Crunchy, salty pickles pulled straight from the jar.",
                    category = "food",
                    usable = true,
                    usableOnLimb = false,
                    destroyAtZeroCondition = true,
                    combineable = true,
                    weight = 0.35f,
                    scaleWeightWithCondition = true,
                    decayMinutes = 240f,
                    tags = "cangetwet",
                    value = 4,
                    rec = new Recognition(2),
                    SpawnFrequency = 0,
                    SpriteScaleDimensions = (14f, 14f, true),
                    useAction = (body, item) =>
                    {
                        body.Eat(10f, 0.35f);
                        body.Drink(2f);
                        body.happiness += 1.5f;
                        item.condition -= 1f;
                        Sound.Play("eatCrunch", (Vector2)body.transform.position);
                    }
                },
                picklesIcon);

            RecipeRegistry.Register(new Recipe
            {
                INT = 2,
                specialKnown = true,
                category = Recipes.RecipeCategory.Food,
                result = new RecipeResult
                {
                    id = "pickles",
                    amount = 1,
                    resultCondition = 1f
                },
                items = new List<RecipeItem>
                {
                    new RecipeItem(0f) { specificId = "picklejar" }
                }
            });

            Logger.LogInfo("Registered pickle jar, pickles, and pickle extraction recipe");
        }

        private void RegisterPeanutItems()
        {
            Sprite peanutJarIcon = LoadAssetSprite("peanut_jar.png");
            Sprite jarLiquidMask = ItemIcons.JarMaskMatching(peanutJarIcon);

            ItemRegistry.Register(
                "peanutjar",
                new CustomItemInfo
                {
                    fullName = "Peanut Butter Jar",
                    description = "A glass jar packed with peanut butter.",
                    category = "food",
                    slotRotation = -20f,
                    tags = "cangetwet",
                    usable = true,
                    usableOnLimb = false,
                    destroyAtZeroCondition = false,
                    combineable = true,
                    weight = 1.8f,
                    scaleWeightWithCondition = true,
                    capacity = 400f,
                    autoFill = false,
                    LiquidMask = jarLiquidMask,
                    defaultContents = new List<LiquidStack>
                    {
                        new LiquidStack("peanutbutter", 400f)
                    },
                    useAction = (body, item) =>
                    {
                        WaterContainerItem container = item.GetComponent<WaterContainerItem>();
                        if (container != null)
                            container.Drink(body);
                    },
                    value = 6,
                    rec = new Recognition(2),
                    DropPool = DropPool.FoodCrate | DropPool.AllTraders,
                    SpawnFrequency = 1,
                    SpriteScaleDimensions = (14f, 14f, true)
                },
                peanutJarIcon);

            Logger.LogInfo("Registered peanut butter jar");
        }

        private static void RegisterDrinkBottle(
            string id,
            string name,
            string description,
            string liquidId,
            Color liquidColor,
            int value,
            DropPool dropPool,
            string iconFile = null)
        {
            Sprite assetIcon = LoadAssetSprite(iconFile);
            bool useAssetIcon = assetIcon != null;
            Sprite icon = assetIcon ?? ItemIcons.Bottle(liquidColor);

            CustomItemInfo info = new CustomItemInfo
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
                // Asset icons already depict the liquid; LiquidMask would cover them.
                LiquidMask = useAssetIcon ? null : ItemIcons.BottleMask(),
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
            };
            if (useAssetIcon)
                info.SpriteScaleDimensions = (14f, 14f, true);

            ItemRegistry.Register(id, info, icon);
        }

        private static void RegisterSyringe(
            string id,
            string name,
            string description,
            string liquidId,
            Color liquidColor,
            int value,
            DropPool dropPool,
            string iconFile = null)
        {
            Sprite assetIcon = LoadAssetSprite(iconFile);
            bool useAssetIcon = assetIcon != null;
            Sprite icon = assetIcon ?? ItemIcons.Syringe(liquidColor);

            CustomItemInfo info = new CustomItemInfo
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
                capacity = 100f,
                defaultContents = new List<LiquidStack>
                {
                    new LiquidStack(liquidId, 100f)
                },
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
            };
            if (useAssetIcon)
                info.SpriteScaleDimensions = (14f, 14f, true);

            ItemRegistry.Register(id, info, icon);
        }

        private static Sprite LoadAssetSprite(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            // Pass this assembly explicitly so GetCallingAssembly cannot resolve to CUCoreLib.
            Sprite sprite = AssetLoader.LoadEmbeddedSprite(
                "Assets." + fileName,
                pixelsPerUnit: 8f,
                sourceAssembly: Assembly.GetExecutingAssembly());

            if (sprite == null)
                Logger?.LogWarning($"Failed to load embedded sprite Assets.{fileName}");
            else
                Logger?.LogInfo($"Loaded embedded sprite Assets.{fileName}");

            return sprite;
        }

        /// <summary>
        /// Peanut butter is dense food: very filling, packs on weight, and is a little sickening.
        /// Failures are allergic and go into anaphylaxis.
        /// </summary>
        private static void ApplyPeanutButter(Body body, float ml)
        {
            if (body == null || ml <= 0f)
                return;

            float dose = ml * 0.01f;
            body.Eat(dose * 18f, dose * 1.25f);
            body.sicknessAmount = Mathf.Min(100f, body.sicknessAmount + dose * 8f);

            if (PlayerClasses.IsFailure(body))
                body.GetStatus<PeanutAllergyStatus>().Trigger(dose);
        }

        /// <summary>
        /// Swallowing Axyltallisal stops the heart and destroys the brain.
        /// </summary>
        internal static void ApplyAxyltallisalDrink(Body body, float ml)
        {
            if (body == null || ml <= 0f)
                return;

            AxyltallisalStatus.KillByCardiacArrest(body);
        }

        /// <summary>
        /// 100 mL knocks the body out for 90 seconds. 105 mL is an overdose.
        /// Mixing with any other drug during the knockout is fatal.
        /// The syringe minigame calls Inject every frame, so a just-emptied
        /// 100 mL syringe is counted as one full dose even if callbacks were split.
        /// </summary>
        internal static void ApplyAxyltallisalInject(Body body, float ml)
        {
            if (body == null || ml <= 0f)
                return;

            AxyltallisalStatus status = body.GetStatus<AxyltallisalStatus>();
            if (status.Resolved && !status.Dying && !status.KnockedOut)
            {
                status.Resolved = false;
                status.Fatal = false;
                status.AbsorbedMl = 0f;
            }

            status.AbsorbedMl += ml;

            float overdoseMl = PlayerClasses.ScaleOverdoseThreshold(body, AxyltallisalStatus.OverdoseMilliliters);
            bool pastGrace = status.KnockedOut && status.Elapsed >= AxyltallisalStatus.SamePlungeGraceSeconds;
            if (status.AbsorbedMl >= overdoseMl && pastGrace)
                status.Fatal = true;

            if (status.Dying)
                return;

            if (status.KnockedOut)
            {
                if (pastGrace)
                    status.Fatal = true;
                if (AxyltallisalStatus.HasOtherDrugs(body))
                    status.Fatal = true;
                status.ApplyToBody(body);
                return;
            }

            if (status.AbsorbedMl < AxyltallisalStatus.DoseMilliliters)
                return;

            status.BeginKnockout(body);
        }

        /// <summary>
        /// Distilled Tonic hydrates and warms, but it is poisonous for everyone except the Failure.
        /// Failures drink it safely and gain a small immunity boost.
        /// </summary>
        private static void ApplyDistilledTonicDrink(Body body, float ml)
        {
            if (body == null || ml <= 0f)
                return;

            float dose = ml * 0.01f;
            body.Drink(dose * 8f);
            body.temperature += dose * 0.5f;

            AxyltallisalStatus.NoteForeignDrug(body);

            if (PlayerClasses.IsFailure(body))
            {
                body.happiness += dose * 3f;
                body.immunity = Mathf.Min(200f, body.immunity + dose * 8f);
                return;
            }

            body.happiness -= dose * 4f;
            body.sicknessAmount = Mathf.Min(100f, body.sicknessAmount + dose * 22f);
        }

        /// <summary>
        /// Instantly clears physical injuries, cancels any active fever,
        /// cools toward 29C over 30s, then starts the delayed fever.
        /// Requires 100 mL total; partial uses add up until a full dose is reached.
        /// </summary>
        private static void ApplyDeathJuice(Body body, float ml)
        {
            if (body == null || ml <= 0f)
                return;

            AxyltallisalStatus.NoteForeignDrug(body);

            DeathJuiceStatus status = body.GetStatus<DeathJuiceStatus>();
            status.AbsorbedMl += ml;
            if (status.AbsorbedMl < 100f)
                return;

            status.AbsorbedMl -= 100f;
            HealPhysicalInjuries(body);

            status.FeverActive = false;
            status.FeverElapsed = 0f;
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

            AxyltallisalStatus.NoteForeignDrug(body);

            float dose = ml * 0.01f;
            body.sicknessAmount = Mathf.Min(100f, body.sicknessAmount + dose * 40f);
        }

        /// <summary>
        /// Injected dose: drops mood, then drains 90% of current brain health over 10 seconds.
        /// Requires 100 mL total; partial injections add up until a full dose is reached.
        /// </summary>
        private static void ApplyBrainfuck(Body body, float ml)
        {
            if (body == null || ml <= 0f)
                return;

            AxyltallisalStatus.NoteForeignDrug(body);

            BrainfuckStatus status = body.GetStatus<BrainfuckStatus>();
            status.AbsorbedMl += ml;
            if (status.AbsorbedMl < 100f)
                return;

            status.AbsorbedMl -= 100f;
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
        /// Requires 100 mL total; partial injections add up until a full dose is reached.
        /// </summary>
        private static void ApplyStimFluid(Limb limb, float ml)
        {
            if (limb == null || limb.body == null || ml <= 0f)
                return;

            AxyltallisalStatus.NoteForeignDrug(limb.body);

            StimFluidStatus status = limb.body.GetStatus<StimFluidStatus>();
            status.AbsorbedMl += ml;
            if (status.AbsorbedMl < 100f)
                return;

            status.AbsorbedMl -= 100f;
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
            if (limb == null)
                return;

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
            if (limb == null)
                return;

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
        internal static void HealPhysicalInjuries(Body body)
        {
            if (body == null || body.limbs == null)
                return;

            foreach (Limb limb in body.limbs)
            {
                if (limb == null)
                    continue;

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
