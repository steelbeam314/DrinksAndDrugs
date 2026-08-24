using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using System.Collections.Generic;
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

        // Crafting pickles from a full pickle jar leaves the brine jar behind.
        [HarmonyPatch(typeof(Recipe), nameof(Recipe.TryMake))]
        internal static class PickleJarCraftPatch
        {
            [HarmonyPostfix]
            private static void Postfix(Recipe __instance)
            {
                if (__instance?.result == null || __instance.result.id != "pickles")
                    return;

                if (__instance.items == null)
                    return;

                bool usedPickleJar = false;
                for (int i = 0; i < __instance.items.Count; i++)
                {
                    RecipeItem ingredient = __instance.items[i];
                    if (ingredient != null && ingredient.specificId == "picklejar")
                    {
                        usedPickleJar = true;
                        break;
                    }
                }

                if (!usedPickleJar)
                    return;

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

                Item leftover = spawned.GetComponent<Item>();
                if (leftover != null)
                    body.AutoPickUpItem(leftover);
            }
        }

        [HarmonyPatch(typeof(Body), nameof(Body.Update))]
        internal static class DeathJuiceFeverPatch
        {
            // Degrees Celsius added per second once the wait/cooling phase ends.
            private const float FeverDegreesPerSecond = 0.75f;

            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Body __instance)
            {
                PlayerClasses.AssignLocalClassIfNeeded(__instance);
                __instance.GetStatus<BrainfuckStatus>().Tick(__instance);

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

                    // Force the lerp every frame so vanilla temp recovery cannot fight the cool-down.
                    __instance.temperature = Mathf.Lerp(
                        status.CoolingStartTemperature,
                        status.CoolingTargetTemperature,
                        t);

                    if (t < 1f)
                        return;

                    status.CoolingActive = false;
                    status.CoolingElapsed = DeathJuiceStatus.WaitDurationSeconds;
                    __instance.temperature = status.CoolingTargetTemperature;
                    status.FeverActive = true;
                }

                MoodleRegistry.AddMoodle(
                    intensity: 3,
                    icon: MoodleIcons.Blank,
                    name: "Hyperactive Nanomachines",
                    description: "The nanomachines are overheating your body.",
                    key: "deathjuice.fever");

                __instance.temperature += FeverDegreesPerSecond * Time.deltaTime;
            }
        }

        // HandleBody can overwrite brainHealth after Update; stamp the drain back on.
        [HarmonyPatch(typeof(Body), nameof(Body.HandleBody))]
        internal static class BrainfuckHandleBodyPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Body __instance)
            {
                BrainfuckStatus status = __instance.GetStatus<BrainfuckStatus>();
                if (!status.Draining)
                    return;

                float t = Mathf.Clamp01(status.Elapsed / BrainfuckStatus.DrainDurationSeconds);
                __instance.brainHealth = Mathf.Lerp(status.StartBrainHealth, status.TargetBrainHealth, t);
                __instance.happiness = Mathf.Min(__instance.happiness, -20f);
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
    }
}
