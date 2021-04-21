using System;
using HarmonyLib;
using VRUIControls;

namespace HUISearch.HarmonyPatches
{
    [HarmonyPatch(typeof(VRInputModule), "Process")]
    internal class VRInputModuleProcessPatch
    {
        public static event Action<VRInputModule> ProcessHook;

        private static void Postfix(VRInputModule __instance)
        {
            try
            {
                ProcessHook?.Invoke(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("Exception thrown in VRInputModule Process hook. Removing harmony patch to prevent further exceptions");
                Plugin.Log.Debug(e);

                Plugin.harmony.Unpatch(typeof(VRInputModule).GetMethod("Process"), HarmonyPatchType.Postfix, Plugin.HarmonyId);
            }
        }
    }
}
