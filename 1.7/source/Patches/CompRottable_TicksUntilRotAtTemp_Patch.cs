using System;
using HarmonyLib;
using RimWorld;

namespace Hardcore_Freezers.Patches
{
    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.TicksUntilRotAtTemp), typeof(float))]
    internal static class CompRottable_TicksUntilRotAtTemp_Patch
    {
        private const int FrozenTicks = 72_000_000;
        internal const int MaxPositiveTicks = 71_999_992;

        private static bool Prefix(CompRottable __instance, float temp, ref int __result)
        {
            RotCurve curve = RotRuntime.ActiveCurve;
            if (!RotCurve.UsesCustomRate(temp) || curve == null)
                return true;

            float rate = __instance.Active ? curve.RateBelowJoin(temp) : 0f;
            if (rate == 0f)
            {
                __result = FrozenTicks;
                return false;
            }

            float remaining = (float)__instance.PropsRot.TicksToRotStart - __instance.RotProgress;
            __result = remaining <= 0f ? 0 : (int)Math.Round(Math.Min(MaxPositiveTicks, remaining / (double)rate));
            return false;
        }
    }
}
