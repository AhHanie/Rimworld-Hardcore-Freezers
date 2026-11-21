using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Hardcore_Freezers.Patches
{
    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.CompInspectStringExtra))]
    internal static class CompRottable_CompInspectStringExtra_Patch
    {
        private static bool Prefix(CompRottable __instance, ref string __result, out float __state)
        {
            __state = 0f;
            RotCurve curve = RotRuntime.ActiveCurve;
            if (curve == null || !__instance.Active)
                return true;
            if (__instance.Stage != RotStage.Fresh)
            {
                __state = FreezeDamageRuntime.CurrentRate(__instance.parent);
                return true;
            }

            float temperature = __instance.parent.AmbientTemperature;
            if (!float.IsFinite(temperature))
                return true;

            __state = FreezeDamageRuntime.RateAt(__instance.parent, temperature);
            TaggedString state;
            if (temperature <= 0f)
            {
                float rate = curve.RateAt(temperature);
                if (rate == 0f)
                {
                    state = "HardcoreFreezers.FrozenNeverSpoils".Translate();
                }
                else
                {
                    int ticksUntilRot = __instance.TicksUntilRotAtTemp(temperature);
                    string duration = ticksUntilRot.ToStringTicksToPeriod();
                    if (ticksUntilRot == CompRottable_TicksUntilRotAtTemp_Patch.MaxPositiveTicks)
                        duration = "HardcoreFreezers.MinimumDuration".Translate(duration);

                    double slowdown = 1d - rate;
                    state = Math.Round(slowdown, 4) >= 1d
                        ? "HardcoreFreezers.FrozenAlmostStopped".Translate(0.9999d.ToString("0.##%"), duration)
                        : "HardcoreFreezers.FrozenSlowed".Translate(slowdown.ToString("0.##%"), duration);
                }
            }
            else
            {
                float estimateTemp = temperature < RotCurve.JoinTemperature ? temperature : Mathf.RoundToInt(temperature);
                string key = curve.RateAt(estimateTemp) < 0.999f ? "CurrentlyRefrigerated" : "NotRefrigerated";
                state = key.Translate(__instance.TicksUntilRotAtTemp(estimateTemp).ToStringTicksToPeriod());
            }

            __result = $"{"RotStateFresh".Translate()} ({state})";
            return false;
        }

        private static void Postfix(float __state, ref string __result)
        {
            if (__state > 0f)
                __result += $"\n{"HardcoreFreezers.FreezeDamageInspect".Translate(__state.ToString("G4"))}";
        }
    }
}
