using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using CUCoreLib.Networking;
using CUCoreLib.Registries;
using TMPro;
using UnityEngine;

namespace DrinksAndDrugs
{
    internal static class ClassSelection
    {
        public const string SettingKey = "class";
        public const string SurvivorChoice = "Survivor";
        public const string DrugTesterChoice = "DrugTester";
        public const string FailureChoice = "Failure";
        public const string NamelessChoice = "Nameless";
        public const string SetClassCommandName = "setclass";

        public static readonly string[] Choices = { SurvivorChoice, DrugTesterChoice, FailureChoice, NamelessChoice };
        public static readonly string[] ClassIds = { Plugin.SurvivorClassId, Plugin.DrugTesterClassId, Plugin.FailureClassId, Plugin.NamelessClassId };

        public static bool IsMultiplayerEnabled()
        {
            if (Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey("KrokoshaCasualtiesMP"))
                return true;

            return MultiplayerApi.IsAvailable || MultiplayerApi.IsRunning;
        }

        public static bool IsRunStarted()
        {
            if (WorldGeneration.world != null)
                return true;

            return PlayerCamera.main != null && PlayerCamera.main.body != null;
        }

        public static void EnsureRegistered()
        {
            LocaleRegistry.Get("other", "runsetclass", "Class");
            LocaleRegistry.Get("other", "runsetclassdsc", "Your starting character class.");
            LocaleRegistry.Get("other", "runsetclassSurvivor", "Survivor");
            LocaleRegistry.Get("other", "runsetclassDrugTester", "Drug Tester");
            LocaleRegistry.Get("other", "runsetclassFailure", "Failure");
            LocaleRegistry.Get("other", "runsetclassNameless", "Nameless");

            if (RunSettings.settingTypes == null)
                return;

            if (IsMultiplayerEnabled())
            {
                RemoveClassSetting();
                return;
            }

            int existing = IndexOfClassSetting();
            if (existing < 0)
            {
                RunSettings.settingTypes.Insert(0, new RunSettingDropdown(SettingKey, Choices));
            }
            else
            {
                if (RunSettings.settingTypes[existing] is RunSettingDropdown dropdown)
                    dropdown.choices = Choices;

                if (existing > 0)
                {
                    RunSetting setting = RunSettings.settingTypes[existing];
                    RunSettings.settingTypes.RemoveAt(existing);
                    RunSettings.settingTypes.Insert(0, setting);
                }
            }

            EnsureValue(PreRunScript.instance != null ? PreRunScript.instance.runSettings : null);

            if (RunSettings.presets == null)
                return;

            foreach (RunSettingsPreset preset in RunSettings.presets)
                EnsureValue(preset.presetValues);
        }

        private static bool _consoleCommandRegistered;
        private static bool _hasLoggedSelectedClass;

        public static void ResetClassLog()
        {
            _hasLoggedSelectedClass = false;
        }

        public static void RegisterConsoleCommand()
        {
            if (_consoleCommandRegistered)
                return;

            Dictionary<int, List<string>> autofill = new Dictionary<int, List<string>>
            {
                { 0, new List<string> { "survivor", "drugtester", "failure", "nameless" } }
            };

            ConsoleCommandRegistry.Register(new Command(
                SetClassCommandName,
                "Set your character class before a run starts. In multiplayer this replaces the Class dropdown.",
                HandleSetClassCommand,
                autofill,
                new (string, string)[]
                {
                    ("string name", "Class name: survivor, drugtester, failure, or nameless")
                }));

            _consoleCommandRegistered = true;
        }

        public static void HandleSetClassCommand(string[] args)
        {
            ConsoleScript console = ConsoleScript.instance;
            if (console == null)
                return;

            if (args == null || args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                console.LogToConsole("Current class: " + DisplayName(Plugin.SelectedClassId));
                console.LogToConsole("Usage: setclass <survivor|drugtester|failure|nameless>");
                return;
            }

            string raw = args[1];
            for (int i = 2; i < args.Length; i++)
                raw += " " + args[i];

            if (!TrySetClassFromName(raw, out string error))
            {
                console.LogToConsole(error);
                return;
            }

            console.LogToConsole("Class set to " + DisplayName(Plugin.SelectedClassId) + ". This takes effect when the run starts.");
        }

        public static bool TrySetClassFromName(string raw, out string error)
        {
            if (IsRunStarted())
            {
                error = "Class can only be changed before a run starts.";
                return false;
            }

            string classId = ResolveClassId(raw);
            if (classId == null)
            {
                error = "Unknown class. Use: survivor, drugtester, failure, nameless";
                return false;
            }

            Plugin.SelectedClassId = classId;
            error = null;
            return true;
        }

        public static string ResolveClassId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            string key = raw.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "-");
            if (key == "survivor" || key == "scavenger" || key == "scav")
                return Plugin.SurvivorClassId;
            if (key == "drugtester" || key == "drug-tester" || key == "dt")
                return Plugin.DrugTesterClassId;
            if (key == "failure" || key == "fail")
                return Plugin.FailureClassId;
            if (key == "nameless" || key == "name" || key == "nl")
                return Plugin.NamelessClassId;

            return null;
        }

        public static void EnsureValue(Dictionary<string, object> settings)
        {
            if (settings == null)
                return;

            if (!settings.ContainsKey(SettingKey))
                settings[SettingKey] = 0;
        }

        public static int ReadClassIndex(Dictionary<string, object> settings)
        {
            if (settings == null || !settings.TryGetValue(SettingKey, out object raw) || raw == null)
                return 0;

            try
            {
                return Convert.ToInt32(raw);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static string ReadSelectedClassId(Dictionary<string, object> settings)
        {
            return ClassIdFromIndex(ReadClassIndex(settings));
        }

        public static string ClassIdFromIndex(int index)
        {
            if (index < 0 || index >= ClassIds.Length)
                return Plugin.DefaultClassId;

            return ClassIds[index];
        }

        public static void WriteClassIndex(Dictionary<string, object> settings, int index)
        {
            if (index < 0 || index >= ClassIds.Length)
                index = 0;

            if (settings != null)
                settings[SettingKey] = index;

            Plugin.SelectedClassId = ClassIds[index];
        }

        public static int ReadClassIndexFromUi(PreRunScript preRun)
        {
            if (preRun == null || preRun.runSettingObjects == null)
                return -1;

            for (int i = 0; i < preRun.runSettingObjects.Count; i++)
            {
                RunSettingDisplay display = preRun.runSettingObjects[i];
                if (display == null || display.associated == null || display.associated.name != SettingKey)
                    continue;

                if (display.transform.childCount < 2)
                    continue;

                TMP_Dropdown dropdown = display.transform.GetChild(1).GetComponent<TMP_Dropdown>();
                if (dropdown != null)
                    return dropdown.value;
            }

            return -1;
        }

        public static void ApplyClassIndexToUi(PreRunScript preRun, int index)
        {
            if (preRun == null || preRun.runSettingObjects == null)
                return;

            if (index < 0 || index >= ClassIds.Length)
                index = 0;

            for (int i = 0; i < preRun.runSettingObjects.Count; i++)
            {
                RunSettingDisplay display = preRun.runSettingObjects[i];
                if (display == null || display.associated == null || display.associated.name != SettingKey)
                    continue;

                if (display.transform.childCount < 2)
                    continue;

                TMP_Dropdown dropdown = display.transform.GetChild(1).GetComponent<TMP_Dropdown>();
                if (dropdown != null)
                    dropdown.SetValueWithoutNotify(index);
            }
        }

        public static void FlushUiIntoSettings(PreRunScript preRun, Dictionary<string, object> settings)
        {
            if (preRun == null || settings == null)
                return;

            if (preRun.runSettingObjects != null)
            {
                for (int i = 0; i < preRun.runSettingObjects.Count; i++)
                {
                    RunSettingDisplay display = preRun.runSettingObjects[i];
                    if (display == null)
                        continue;

                    try
                    {
                        display.UpdateSetting(settings);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            int uiIndex = ReadClassIndexFromUi(preRun);
            if (uiIndex >= 0)
                WriteClassIndex(settings, uiIndex);
            else
                WriteClassIndex(settings, ReadClassIndex(settings));
        }

        public static void RefreshSelectedClassFromRunSettings()
        {
            if (IsMultiplayerEnabled())
                return;

            Dictionary<string, object> settings = WorldGeneration.runSettings;
            if (settings == null && PreRunScript.instance != null)
                settings = PreRunScript.instance.runSettings;

            if (settings == null)
                return;

            EnsureValue(settings);
            Plugin.SelectedClassId = ReadSelectedClassId(settings);
        }

        public static string DisplayName(string classId)
        {
            classId = PlayerClasses.NormalizeClassId(classId);
            if (classId == Plugin.DrugTesterClassId)
                return "Drug Tester";
            if (classId == Plugin.FailureClassId)
                return "Failure";
            if (classId == Plugin.NamelessClassId)
                return "Nameless";

            return "Survivor";
        }

        public static void LogSelectedClassToGameConsole(string classId)
        {
            if (_hasLoggedSelectedClass)
                return;

            ConsoleScript console = ConsoleScript.instance;
            if (console == null)
                return;

            _hasLoggedSelectedClass = true;
            console.LogToConsole("Selected class: " + DisplayName(classId));
        }

        private static void RemoveClassSetting()
        {
            int existing = IndexOfClassSetting();
            if (existing >= 0)
                RunSettings.settingTypes.RemoveAt(existing);
        }

        private static int IndexOfClassSetting()
        {
            for (int i = 0; i < RunSettings.settingTypes.Count; i++)
            {
                if (RunSettings.settingTypes[i] != null && RunSettings.settingTypes[i].name == SettingKey)
                    return i;
            }

            return -1;
        }
    }
}
