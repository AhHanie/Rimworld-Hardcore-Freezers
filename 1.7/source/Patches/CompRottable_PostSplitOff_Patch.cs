using HarmonyLib;
using RimWorld;
using Verse;

namespace Hardcore_Freezers.Patches
{
    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.PostSplitOff), typeof(Thing))]
    internal static class CompRottable_PostSplitOff_Patch
    {
        [HarmonyPrepare]
        private static bool Prepare() => ModSettings.FreezeDamageEnabled;

        private static void Postfix(CompRottable __instance, Thing piece, bool __runOriginal)
        {
            if (__runOriginal)
                FreezeDamageRuntime.CopyProgress(__instance, piece);
        }
    }
}
