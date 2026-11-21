using HarmonyLib;
using RimWorld;
using Verse;

namespace Hardcore_Freezers.Patches
{
    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.PreAbsorbStack), typeof(Thing), typeof(int))]
    internal static class CompRottable_PreAbsorbStack_Patch
    {
        private static void Postfix(CompRottable __instance, Thing otherStack, int count, bool __runOriginal)
        {
            if (__runOriginal)
            {
                RotRuntime.InvalidateCarry(__instance);
                FreezeDamageRuntime.AbsorbProgress(__instance, otherStack, count);
            }
        }
    }
}
