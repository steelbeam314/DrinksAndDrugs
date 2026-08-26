using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace DrinksAndDrugs
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("net.cucorelib", BepInDependency.DependencyFlags.HardDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "mrdevman.drinksanddrugs";
        public const string ModName = "DrinksAndDrugs";
        public const string ModVersion = "0.1.0";

        public const string DefaultClassId = "scavenger";
        public const string DrugTesterClassId = "drugtester";
        public static string SelectedClassId = DefaultClassId;

        internal static new ManualLogSource Logger;
        private readonly Harmony _harmony = new(ModGUID);
        public static Plugin Instance { get; private set; } = null!;

        void Awake()
        {
            Logger = base.Logger;
            Instance = this;

            RegisterLiquids();
            RegisterLiquidContainers();
            RegisterPickleItems();
            ClassSelection.EnsureRegistered();
            ClassSelection.RegisterConsoleCommand();

            try
            {
                _harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Harmony PatchAll failed: {ex}");
            }

            Logger.LogInfo($"Plugin {ModName} is loaded!");
        }

        void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Instance = null!;
        }
    }
}
