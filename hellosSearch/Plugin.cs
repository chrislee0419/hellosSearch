using System;
using System.Reflection;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using HarmonyLib;
using SiraUtil.Zenject;
using HUISearch.Installers;
using IPALogger = IPA.Logging.Logger;

namespace HUISearch
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        internal static readonly Harmony harmony = new Harmony(HarmonyId);

        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        internal const string HarmonyId = "com.chrislee0419.BeatSaber.HUI.Search";

        [Init]
        public Plugin(IPALogger logger, Config config, Zenjector zenjector)
        {
            Instance = this;
            Plugin.Log = logger;

            PluginConfig.Instance = config.Generated<PluginConfig>();

            zenjector.OnMenu<SearchInstaller>();
            zenjector.OnMenu<UIInstaller>();
        }

        [OnEnable]
        public void OnEnable()
        {
            ApplyHarmonyPatches();
        }

        [OnDisable]
        public void OnDisable()
        {
            RemoveHarmonyPatches();
        }

        internal static void ApplyHarmonyPatches()
        {
            try
            {
                Plugin.Log?.Debug("Applying Harmony patches.");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error("Error applying Harmony patches: " + ex.Message);
                Plugin.Log?.Debug(ex);
            }
        }

        internal static void RemoveHarmonyPatches()
        {
            try
            {
                // Removes all patches with this HarmonyId
                harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error("Error removing Harmony patches: " + ex.Message);
                Plugin.Log?.Debug(ex);
            }
        }
    }
}
