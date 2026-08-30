using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DrinksAndDrugs
{
    internal class Patches
    {
        [HarmonyPatch(typeof(ConsoleScript))]
        internal static class ConsolePatch
        {
            [HarmonyPatch(nameof(ConsoleScript.Start))]
            [HarmonyPostfix]
            private static void StartPatch()
            {
                ClassSelection.LogSelectedClassToGameConsole(Plugin.SelectedClassId);
            }

            [HarmonyPatch(nameof(ConsoleScript.ToggleActiveState))]
            [HarmonyPostfix]
            private static void OpenPatch(ConsoleScript __instance)
            {
                if (__instance.active)
                    ClassSelection.LogSelectedClassToGameConsole(Plugin.SelectedClassId);
            }
        }

        // Crafting pickles from a pickle jar leaves the brine jar behind with the remaining juice.
        [HarmonyPatch(typeof(Recipe), nameof(Recipe.TryMake))]
        internal static class PickleJarCraftPatch
        {
            private static List<LiquidStack> _leftoverLiquids;

            [HarmonyPrefix]
            private static void Prefix(Recipe __instance)
            {
                _leftoverLiquids = null;
                if (__instance?.result == null || __instance.result.id != "pickles")
                    return;

                List<Item> ingredients = __instance.GetItemsForRecipe();
                if (ingredients == null)
                    return;

                for (int i = 0; i < ingredients.Count; i++)
                {
                    Item item = ingredients[i];
                    if (item == null || item.id != "picklejar")
                        continue;

                    WaterContainerItem container = item.GetComponent<WaterContainerItem>();
                    _leftoverLiquids = CopyLiquidStacks(container);
                    return;
                }
            }

            [HarmonyPostfix]
            private static void Postfix()
            {
                if (_leftoverLiquids == null)
                    return;

                List<LiquidStack> leftoverLiquids = _leftoverLiquids;
                _leftoverLiquids = null;

                Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
                if (body == null)
                    return;

                GameObject spawned = CustomInstantiate.InstantiateReturn(
                    "picklejuicejar",
                    body.transform.position,
                    Quaternion.identity,
                    1f);

                if (spawned == null)
                    return;

                WaterContainerItem leftoverContainer = spawned.GetComponent<WaterContainerItem>();
                if (leftoverContainer != null)
                    ApplyLiquidStacks(leftoverContainer, leftoverLiquids);

                Item leftover = spawned.GetComponent<Item>();
                if (leftover != null)
                    body.AutoPickUpItem(leftover);
            }

            private static List<LiquidStack> CopyLiquidStacks(WaterContainerItem container)
            {
                var copy = new List<LiquidStack>();
                if (container == null || container.stack == null)
                    return copy;

                for (int i = 0; i < container.stack.Count; i++)
                {
                    LiquidStack stack = container.stack[i];
                    if (stack == null || stack.amount <= 0f)
                        continue;

                    copy.Add(new LiquidStack(stack.liquidId, stack.amount));
                }

                return copy;
            }

            private static void ApplyLiquidStacks(WaterContainerItem container, List<LiquidStack> stacks)
            {
                container.DrainAll();
                if (stacks == null)
                    return;

                for (int i = 0; i < stacks.Count; i++)
                {
                    LiquidStack stack = stacks[i];
                    if (stack == null || stack.amount <= 0f)
                        continue;

                    container.AddLiquid(stack.liquidId, stack.amount);
                }

                container.UpdateCondition();
            }
        }

        [HarmonyPatch(typeof(WaterContainerItem), "Start")]
        internal static class PickleJarFillBehindSpritePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void StartPostfix(WaterContainerItem __instance)
            {
                PutPickleJarFillBehindSprite(__instance);
            }
        }

        [HarmonyPatch(typeof(WaterContainerItem), "Update")]
        internal static class PickleJarFillBehindSpriteUpdatePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void UpdatePostfix(WaterContainerItem __instance)
            {
                PutPickleJarFillBehindSprite(__instance);
            }
        }

        private static void PutPickleJarFillBehindSprite(WaterContainerItem container)
        {
            if (container == null || container.fillRenderer == null)
                return;

            Item item = container.item != null ? container.item : container.GetComponent<Item>();
            if (item == null || (item.id != "picklejar" && item.id != "picklejuicejar" && item.id != "peanutjar"))
                return;

            SpriteRenderer itemRenderer = container.GetComponent<SpriteRenderer>();
            if (itemRenderer == null)
                return;

            SpriteRenderer fill = container.fillRenderer;
            fill.sortingLayerID = itemRenderer.sortingLayerID;
            fill.sortingOrder = itemRenderer.sortingOrder - 1;

            if (container.fillMaterial != null)
            {
                int itemQueue = 3000;
                if (itemRenderer.sharedMaterial != null)
                    itemQueue = itemRenderer.sharedMaterial.renderQueue;
                container.fillMaterial.renderQueue = itemQueue - 1;
            }
        }

        [HarmonyPatch(typeof(Body), nameof(Body.Update))]
        internal static class DeathJuiceFeverPatch
        {
            // Degrees Celsius added per second once the wait/cooling phase ends.
            private const float FeverDegreesPerSecond = 0.75f;
            private const float FeverMaxCelsius = 100f;

            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Body __instance)
            {
                PlayerClasses.AssignLocalClassIfNeeded(__instance);
                PlayerClasses.TickFailureEffects(__instance);
                __instance.GetStatus<BrainfuckStatus>().Tick(__instance);
                __instance.GetStatus<PeanutAllergyStatus>().Tick(__instance);
                __instance.GetStatus<AxyltallisalStatus>().Tick(__instance);

                DeathJuiceStatus status = __instance.GetStatus<DeathJuiceStatus>();
                if (!status.CoolingActive && !status.FeverActive)
                    return;

                if (status.CoolingActive)
                {
                    MoodleRegistry.AddMoodle(
                        intensity: 1,
                        icon: MoodleIcons.Blank,
                        name: "Nanomachine Cooling",
                        description: "Nanomachines are dumping your body heat.",
                        key: "deathjuice.cooling");

                    status.CoolingElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(status.CoolingElapsed / DeathJuiceStatus.WaitDurationSeconds);
                    ApplyDeathJuiceTemperature(__instance, status);

                    if (t < 1f)
                        return;

                    status.CoolingActive = false;
                    status.CoolingElapsed = DeathJuiceStatus.WaitDurationSeconds;
                    status.FeverActive = true;
                    status.FeverElapsed = 0f;
                }

                MoodleRegistry.AddMoodle(
                    intensity: 3,
                    icon: MoodleIcons.Blank,
                    name: "Hyperactive Nanomachines",
                    description: "The nanomachines are overheating your body.",
                    key: "deathjuice.fever");

                status.FeverElapsed += Time.deltaTime;
                ApplyDeathJuiceTemperature(__instance, status);

                if (__instance.temperature >= FeverMaxCelsius)
                    status.FeverActive = false;
            }

            internal static void ApplyDeathJuiceTemperature(Body body, DeathJuiceStatus status)
            {
                if (body == null || status == null)
                    return;

                if (status.CoolingActive)
                {
                    float t = Mathf.Clamp01(status.CoolingElapsed / DeathJuiceStatus.WaitDurationSeconds);
                    body.temperature = Mathf.Lerp(
                        status.CoolingStartTemperature,
                        status.CoolingTargetTemperature,
                        t);
                    return;
                }

                if (!status.FeverActive)
                    return;

                body.temperature = Mathf.Min(
                    FeverMaxCelsius,
                    status.CoolingTargetTemperature + FeverDegreesPerSecond * status.FeverElapsed);
            }
        }

        // HandleBody / HandleBodyTemperature overwrite vitals; stamp forced values back on.
        [HarmonyPatch(typeof(Body), nameof(Body.HandleBody))]
        internal static class ForcedVitalsHandleBodyPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Body __instance)
            {
                ApplyForcedVitals(__instance);
                PlayerClasses.ApplyNamelessSimulation(__instance);
                PlayerClasses.ApplyCannibalSimulation(__instance);
            }
        }

        [HarmonyPatch(typeof(Body), nameof(Body.HandleBodyTemperature))]
        internal static class ForcedVitalsHandleBodyTemperaturePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Body __instance)
            {
                ApplyForcedVitals(__instance);
            }
        }

        private static void ApplyForcedVitals(Body body)
        {
            if (body == null)
                return;

            body.GetStatus<BrainfuckStatus>().ApplyToBody(body);
            body.GetStatus<PeanutAllergyStatus>().ApplyToBody(body);
            DeathJuiceFeverPatch.ApplyDeathJuiceTemperature(body, body.GetStatus<DeathJuiceStatus>());
            body.GetStatus<AxyltallisalStatus>().ApplyToBody(body);
        }

        [HarmonyPatch(typeof(Body), nameof(Body.Eat))]
        internal static class NamelessEatPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Body __instance, out float __state)
            {
                __state = __instance != null ? __instance.hunger : 0f;
            }

            [HarmonyPostfix]
            private static void Postfix(Body __instance, float __state)
            {
                if (__instance == null || !PlayerClasses.IsNameless(__instance))
                    return;

                __instance.hunger = PlayerClasses.CompressFullness(__state, __instance.hunger);
            }
        }

        [HarmonyPatch(typeof(Body), nameof(Body.Drink))]
        internal static class NamelessDrinkPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Body __instance, out float __state)
            {
                __state = __instance != null ? __instance.thirst : 0f;
            }

            [HarmonyPostfix]
            private static void Postfix(Body __instance, float __state)
            {
                if (__instance == null || !PlayerClasses.IsNameless(__instance))
                    return;

                __instance.thirst = PlayerClasses.CompressFullness(__state, __instance.thirst);
            }
        }

        [HarmonyPatch(typeof(Body), nameof(Body.HandleVisuals))]
        internal static class NamelessPainShakePatch
        {
            [HarmonyPrefix]
            private static void Prefix(Body __instance, out bool __state)
            {
                PlayerClasses.BeginNamelessPainShake(__instance, out __state);
            }

            [HarmonyPostfix]
            private static void Postfix(Body __instance, bool __state)
            {
                PlayerClasses.EndNamelessPainShake(__instance, __state);
            }
        }

        [HarmonyPatch(typeof(ScrollableText), nameof(ScrollableText.UpdateText))]
        internal static class FailureScrambleReadableTextPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ref string str)
            {
                if (!PlayerClasses.ShouldScrambleReadableText())
                    return;

                str = PlayerClasses.ScrambleReadableText(str);
            }
        }

        // Inject before the run-settings UI is built from RunSettings.settingTypes.
        [HarmonyPatch(typeof(PreRunScript), nameof(PreRunScript.Start))]
        internal static class PreRunScriptStartPatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                ClassSelection.EnsureRegistered();
            }
        }

        [HarmonyPatch(typeof(PreRunScript), nameof(PreRunScript.Awake))]
        internal static class PreRunScriptAwakePatch
        {
            [HarmonyPostfix]
            private static void Postfix(Dictionary<string, object> ___runSettings)
            {
                ClassSelection.EnsureRegistered();
                ClassSelection.EnsureValue(___runSettings);
            }
        }

        [HarmonyPatch(typeof(PreRunScript), nameof(PreRunScript.ApplyPreset))]
        internal static class PreRunScriptApplyPresetPatch
        {
            [HarmonyPrefix]
            private static void Prefix(PreRunScript __instance, Dictionary<string, object> ___runSettings, out int __state)
            {
                if (ClassSelection.IsMultiplayerEnabled())
                {
                    __state = -1;
                    return;
                }

                int uiIndex = ClassSelection.ReadClassIndexFromUi(__instance);
                __state = uiIndex >= 0 ? uiIndex : ClassSelection.ReadClassIndex(___runSettings);
            }

            [HarmonyPostfix]
            private static void Postfix(PreRunScript __instance, Dictionary<string, object> ___runSettings, int __state)
            {
                if (ClassSelection.IsMultiplayerEnabled() || __state < 0)
                    return;

                ClassSelection.EnsureValue(___runSettings);
                ClassSelection.WriteClassIndex(___runSettings, __state);
                ClassSelection.ApplyClassIndexToUi(__instance, __state);
            }
        }

        [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.Awake))]
        internal static class WorldGenerationAwakePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                if (!ClassSelection.IsMultiplayerEnabled())
                    ClassSelection.RefreshSelectedClassFromRunSettings();

                ClassSelection.LogSelectedClassToGameConsole(Plugin.SelectedClassId);
            }
        }

        [HarmonyPatch(typeof(RunSettingDisplay), nameof(RunSettingDisplay.UpdateSetting))]
        internal static class RunSettingDisplayUpdatePatch
        {
            [HarmonyPostfix]
            private static void Postfix(RunSettingDisplay __instance, Dictionary<string, object> settings)
            {
                if (__instance.associated == null || __instance.associated.name != ClassSelection.SettingKey)
                    return;

                Plugin.SelectedClassId = ClassSelection.ReadSelectedClassId(settings);
            }
        }

        [HarmonyPatch(typeof(PreRunScript), nameof(PreRunScript.StartRun))]
        internal static class PreRunScriptStartRunPatch
        {
            [HarmonyPrefix]
            private static void Prefix(PreRunScript __instance, Dictionary<string, object> ___runSettings)
            {
                ClassSelection.ResetClassLog();

                if (!ClassSelection.IsMultiplayerEnabled())
                    ClassSelection.FlushUiIntoSettings(__instance, ___runSettings);

                Plugin.Logger?.LogInfo($"Class selection: id={Plugin.SelectedClassId} ui={ClassSelection.ReadClassIndexFromUi(__instance)} dict={ClassSelection.ReadClassIndex(___runSettings)} mp={ClassSelection.IsMultiplayerEnabled()}");
                ClassSelection.LogSelectedClassToGameConsole(Plugin.SelectedClassId);
            }
        }

        // Drug Testers cannot start syringe injections. Other players can still inject a Drug Tester.
        [HarmonyPatch(typeof(MinigameBase), nameof(MinigameBase.StartMinigame))]
        internal static class DrugTesterBlockSelfInjectPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Minigame minigame)
            {
                if (!(minigame is SyringeMinigame))
                    return true;

                if (!PlayerClasses.LocalPlayerIsDrugTester())
                    return true;

                return false;
            }
        }

        // The injection mark is spawned in the constructor. Let construction finish, then hide it.
        [HarmonyPatch(typeof(SyringeMinigame), MethodType.Constructor, new[] { typeof(SyringeMinigame.OnSyringeUse), typeof(Limb), typeof(Color?) })]
        internal static class DrugTesterHideInjectionMarkPatch
        {
            [HarmonyPostfix]
            private static void Postfix(SyringeMinigame __instance)
            {
                if (!PlayerClasses.LocalPlayerIsDrugTester() || __instance.syringe == null)
                    return;

                UnityEngine.Object.Destroy(__instance.syringe.gameObject);
                __instance.syringe = null;
            }
        }

        [HarmonyPatch(typeof(Liquids), nameof(Liquids.HighGradeStimulantStep))]
        internal static class DrugTesterHighGradeOverdosePatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return PlayerClasses.ScaleFloatConstants(
                    instructions,
                    AccessTools.Field(typeof(Limb), nameof(Limb.body)),
                    320f);
            }
        }

        [HarmonyPatch(typeof(Liquids), nameof(Liquids.LowGradeStimulantStep))]
        internal static class DrugTesterLowGradeOverdosePatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return PlayerClasses.ScaleFloatConstants(
                    instructions,
                    AccessTools.Field(typeof(Limb), nameof(Limb.body)),
                    160f);
            }
        }

        [HarmonyPatch(typeof(SleepingPills), nameof(SleepingPills.Update))]
        internal static class DrugTesterSleepingPillOverdosePatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return PlayerClasses.ScaleFloatConstants(
                    instructions,
                    AccessTools.Field(typeof(SleepingPills), nameof(SleepingPills.body)),
                    150f,
                    900f);
            }
        }

        [HarmonyPatch(typeof(Antidepressants), nameof(Antidepressants.Update))]
        internal static class DrugTesterAntidepressantOverdosePatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return PlayerClasses.ScaleFloatConstants(
                    instructions,
                    AccessTools.Field(typeof(Antidepressants), nameof(Antidepressants.body)),
                    250f);
            }
        }

        [HarmonyPatch(typeof(Painkillers), nameof(Painkillers.Update))]
        internal static class DrugTesterOpiateOverdosePatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return PlayerClasses.ScaleFloatConstants(
                    instructions,
                    AccessTools.Field(typeof(Painkillers), nameof(Painkillers.body)),
                    -34f);
            }
        }

        [HarmonyPatch(typeof(WorldGeneration), "DistributeMiniBarrels")]
        internal static class MiniBarrelDistributionPatch
        {
            internal static bool Active;

            [HarmonyPrefix]
            private static void Prefix()
            {
                Active = true;
            }

            [HarmonyPostfix]
            private static void Postfix()
            {
                Active = false;
            }
        }

        [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.AddLiquid))]
        internal static class AxyltallisalMiniBarrelPatch
        {
            private const float KeepChance = 0.08f;
            private const float MaxMilliliters = 20f;

            [HarmonyPrefix]
            private static bool Prefix(string liquidId, ref float amount)
            {
                if (!MiniBarrelDistributionPatch.Active)
                    return true;

                if (!string.Equals(liquidId, "axyltallisal", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (UnityEngine.Random.value > KeepChance)
                    return false;

                amount = Mathf.Min(amount, MaxMilliliters);
                if (amount < 1f)
                    amount = UnityEngine.Random.Range(2f, MaxMilliliters);

                return true;
            }
        }

        [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.Drink))]
        internal static class AxyltallisalDrinkMixPatch
        {
            [HarmonyPrefix]
            private static void Prefix(WaterContainerItem __instance, Body body)
            {
                AxyltallisalStatus.NoteForeignDrugsInContainer(body, __instance);
            }
        }

        [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.Inject))]
        internal static class AxyltallisalInjectMixPatch
        {
            [HarmonyPrefix]
            private static void Prefix(WaterContainerItem __instance, Limb limb, out bool __state)
            {
                __state = ContainerHasAxyltallisal(__instance);
                Body body = limb != null ? limb.body : null;
                AxyltallisalStatus.NoteForeignDrugsInContainer(body, __instance);
            }

            [HarmonyPostfix]
            private static void Postfix(WaterContainerItem __instance, Limb limb, bool __state)
            {
                if (!__state || ContainerHasAxyltallisal(__instance))
                    return;

                Body body = limb != null ? limb.body : null;
                if (body == null)
                    return;

                AxyltallisalStatus status = body.GetStatus<AxyltallisalStatus>();
                if (status.Dying)
                    return;

                float missing = AxyltallisalStatus.DoseMilliliters - status.AbsorbedMl;
                if (missing > 0f)
                    Plugin.ApplyAxyltallisalInject(body, missing);
                else if (status.KnockedOut && status.Elapsed >= AxyltallisalStatus.SamePlungeGraceSeconds)
                    status.Fatal = true;
            }

            private static bool ContainerHasAxyltallisal(WaterContainerItem container)
            {
                if (container == null || container.stack == null)
                    return false;

                for (int i = 0; i < container.stack.Count; i++)
                {
                    LiquidStack stack = container.stack[i];
                    if (stack != null
                        && stack.amount > 0.01f
                        && string.Equals(stack.liquidId, "axyltallisal", StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(Body), "get_BaseHungerRate")]
        internal static class CannibalHungerRatePatch
        {
            [HarmonyPostfix]
            private static void Postfix(Body __instance, ref float __result)
            {
                __result = PlayerClasses.ScaleHungerRate(__instance, __result);
            }
        }

        [HarmonyPatch(typeof(Body), nameof(Body.Eat))]
        internal static class CannibalEatHungerPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Body __instance, ref float hungerAmount)
            {
                Item item = CannibalItemUsePatch.CurrentItem;
                if (item == null && __instance != null && __instance.HoldingItem(__instance.handSlot))
                    item = __instance.GetItem(__instance.handSlot);

                PlayerClasses.BeginCannibalEat(__instance, item);
                PlayerClasses.ScaleCannibalEatHunger(__instance, item, ref hungerAmount);
            }

            [HarmonyPostfix]
            private static void Postfix()
            {
                PlayerClasses.EndCannibalEatVomitBlock();
            }
        }

        [HarmonyPatch(typeof(Vomiter), nameof(Vomiter.Vomit))]
        internal static class CannibalBlockFleshVomitPatch
        {
            [HarmonyPrefix]
            private static bool Prefix()
            {
                return !PlayerClasses.ShouldBlockCannibalVomit();
            }
        }

        [HarmonyPatch]
        internal static class CannibalYellowFleshPatch
        {
            private static MethodBase TargetMethod()
            {
                Type nested = typeof(Item).GetNestedType("<>c", BindingFlags.NonPublic);
                return AccessTools.Method(nested, "<SetupItems>b__40_192");
            }

            [HarmonyPostfix]
            private static void Postfix(Body __0)
            {
                PlayerClasses.ApplyCannibalFleshEat(__0, "experimentflesh");
            }
        }

        [HarmonyPatch]
        internal static class CannibalAnimalFleshPatch
        {
            private static MethodBase TargetMethod()
            {
                Type nested = typeof(Item).GetNestedType("<>c", BindingFlags.NonPublic);
                return AccessTools.Method(nested, "<SetupItems>b__40_193");
            }

            [HarmonyPostfix]
            private static void Postfix(Body __0)
            {
                PlayerClasses.ApplyCannibalFleshEat(__0, "animalflesh");
            }
        }

        [HarmonyPatch(typeof(Body), nameof(Body.UseItem))]
        internal static class CannibalItemUsePatch
        {
            internal static Item CurrentItem;

            [HarmonyPrefix]
            private static void Prefix(Item item)
            {
                CurrentItem = item;
            }

            [HarmonyPostfix]
            private static void Postfix(Body __instance, Item item)
            {
                PlayerClasses.ApplyCannibalFleshEat(__instance, item != null ? item.id : null);
                CurrentItem = null;
            }
        }

        [HarmonyPatch(typeof(Body), nameof(Body.UseItemInHand))]
        internal static class CannibalItemUseInHandPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Body __instance)
            {
                CannibalItemUsePatch.CurrentItem = __instance != null && __instance.HoldingItem(__instance.handSlot)
                    ? __instance.GetItem(__instance.handSlot)
                    : null;
            }

            [HarmonyPostfix]
            private static void Postfix(Body __instance)
            {
                Item item = CannibalItemUsePatch.CurrentItem;
                PlayerClasses.ApplyCannibalFleshEat(__instance, item != null ? item.id : null);
                CannibalItemUsePatch.CurrentItem = null;
            }
        }

        [HarmonyPatch(typeof(TraderScript), nameof(TraderScript.MeetPlayer))]
        internal static class CannibalTraderReputationPatch
        {
            [HarmonyPostfix]
            private static void Postfix(TraderScript __instance)
            {
                PlayerClasses.ApplyCannibalTraderReputation(__instance);
            }
        }

        [HarmonyPatch(typeof(TraderScript), nameof(TraderScript.TryHug))]
        internal static class CannibalTraderHugPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(TraderScript __instance)
            {
                return !PlayerClasses.TryHandleCannibalHug(__instance);
            }
        }

        [HarmonyPatch(typeof(CorpseScript), "OnWillRenderObject")]
        internal static class CannibalCorpseMoodPatch
        {
            [HarmonyPrefix]
            private static void Prefix(CorpseScript __instance, out float __state)
            {
                __state = 0f;
                if (__instance == null || __instance.animalCorpse || __instance.didComment)
                    return;

                Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
                if (!PlayerClasses.IsCannibal(body))
                    return;

                if (Vector2.Distance(body.transform.position, __instance.transform.position) >= 7f)
                    return;

                __state = 3.5f * body.desensitizedMult;
            }

            [HarmonyPostfix]
            private static void Postfix(CorpseScript __instance, float __state)
            {
                if (__state <= 0f)
                    return;

                Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
                PlayerClasses.ApplyCannibalCorpseMood(body, __state);
                PlayerClasses.SayCannibalCorpseLine(body);
            }
        }

        [HarmonyPatch(typeof(CorpseScript), "Start")]
        internal static class CannibalCorpseMineStartPatch
        {
            [HarmonyPostfix]
            private static void Postfix(CorpseScript __instance)
            {
                if (PlayerClasses.ShouldAllowCorpseMining())
                    PlayerClasses.AllowCorpseMining(__instance);
            }
        }

        [HarmonyPatch(typeof(CorpseScript), "OnDestroy")]
        internal static class CannibalBreakCorpsePatch
        {
            [HarmonyPrefix]
            private static void Prefix(CorpseScript __instance, out bool __state)
            {
                __state = false;
                if (__instance == null || __instance.animalCorpse || !__instance.gameObject.scene.isLoaded)
                    return;

                BuildingEntity building = __instance.GetComponent<BuildingEntity>();
                if (building == null || building.health > 0f)
                    return;

                Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
                if (!PlayerClasses.IsCannibal(body) || body.attackCooldown <= 0f)
                    return;

                if (Vector2.Distance(body.transform.position, __instance.transform.position) >= 10f)
                    return;

                __state = true;
            }

            [HarmonyPostfix]
            private static void Postfix(bool __state)
            {
                if (!__state)
                    return;

                Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
                PlayerClasses.UndoCannibalBreakCorpsePenalty(body);
            }
        }

        [HarmonyPatch(typeof(Talker), nameof(Talker.EatBad))]
        internal static class CannibalSkipEatBadPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Talker __instance)
            {
                return __instance == null || !PlayerClasses.IsCannibal(__instance.body);
            }
        }

        [HarmonyPatch(typeof(Talker), nameof(Talker.EatMediocre))]
        internal static class CannibalSkipEatMediocrePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Talker __instance)
            {
                return __instance == null || !PlayerClasses.IsCannibal(__instance.body);
            }
        }

        [HarmonyPatch(typeof(WoundView), nameof(WoundView.Start))]
        internal static class WoundViewClassNameStartPatch
        {
            [HarmonyPostfix]
            private static void Postfix(WoundView __instance)
            {
                ApplyClassName(__instance);
            }
        }

        [HarmonyPatch(typeof(WoundView), nameof(WoundView.UpdateView))]
        internal static class WoundViewClassNamePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(WoundView __instance)
            {
                ApplyClassName(__instance);
            }
        }

        private static void ApplyClassName(WoundView view)
        {
            if (view == null || view.nameText == null)
                return;

            Body body = view.body != null ? view.body : PlayerClasses.LocalBody();
            if (PlayerClasses.GetClassId(body) == Plugin.SurvivorClassId)
                return;

            view.nameText.text = PlayerClasses.GetClassDisplayName(body).ToUpperInvariant();
        }
    }
}
