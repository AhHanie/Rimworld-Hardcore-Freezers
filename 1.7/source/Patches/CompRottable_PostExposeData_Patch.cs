using HarmonyLib;
using RimWorld;

namespace Hardcore_Freezers.Patches
{
    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.PostExposeData))]
    internal static class CompRottable_PostExposeData_Patch
    {
        private static void Postfix(CompRottable __instance)
        {
            RotRuntime.ExposeCarry(__instance);
            FreezeDamageRuntime.ExposeProgress(__instance);
        }
    }
}
