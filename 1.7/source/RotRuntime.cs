using System;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace Hardcore_Freezers
{
    internal static class RotRuntime
    {
        private static volatile RotCurve activeCurve;
        private static readonly ConditionalWeakTable<CompRottable, FractionalProgress> carry = new();

        internal static RotCurve ActiveCurve => activeCurve;

        internal static void Enable(RotCurve curve) => activeCurve = curve;

        internal static void Apply(RotCurve curve)
        {
            if (activeCurve != null)
                activeCurve = curve;
        }

        internal static float EstimateTemperature(float temperature) =>
            activeCurve != null && RotCurve.UsesCustomRate(temperature) ? temperature : Mathf.RoundToInt(temperature);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Advance(float temperature, CompRottable comp, int delta)
        {
            if (RotCurve.UsesCustomRate(temperature) && activeCurve is RotCurve curve)
            {
                AdvanceBelowJoin(comp, delta, curve.RateBelowJoin(temperature));
                return;
            }

            comp.RotProgress += GenTemperature.RotRateAtTemperature(temperature) * delta;
        }

        private static void AdvanceBelowJoin(CompRottable comp, int delta, float rate)
        {
            if (rate == 0f || delta == 0)
                return;

            float progress = comp.RotProgress;
            float increment = rate * delta;
            if (delta < 0 || !float.IsFinite(progress) || increment > MathF.Abs(progress) * (64f / 8388608f))
            {
                comp.RotProgress = progress + increment;
                return;
            }

            AdvanceFractional(comp, progress, (double)rate * delta);
        }

        private static void AdvanceFractional(CompRottable comp, float progress, double increment)
        {
            if (carry.TryGetValue(comp, out FractionalProgress fraction) && fraction.Anchor == progress)
                increment += fraction.Remainder;

            float updated = (float)(progress + increment);
            double remainder = increment - ((double)updated - progress);
            comp.RotProgress = updated;

            if (remainder == 0d || !float.IsFinite(updated))
            {
                if (fraction != null)
                    carry.Remove(comp);
                return;
            }

            fraction ??= carry.GetOrCreateValue(comp);
            fraction.Anchor = updated;
            fraction.Remainder = remainder;
        }

        internal static void InvalidateCarry(CompRottable comp)
        {
            if (activeCurve != null)
                carry.Remove(comp);
        }

        internal static void ExposeCarry(CompRottable comp)
        {
            if (activeCurve == null)
                return;

            float progress = comp.RotProgress;
            float anchor = float.NaN;
            double remainder = 0d;

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (carry.TryGetValue(comp, out FractionalProgress fraction) && fraction.Anchor == progress &&
                    IsValidRemainder(progress, fraction.Remainder))
                {
                    anchor = progress;
                    remainder = fraction.Remainder;
                    Scribe_Values.Look(ref anchor, "hardcoreFreezersRotAnchor", forceSave: true);
                    Scribe_Values.Look(ref remainder, "hardcoreFreezersRotCarry");
                }
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref anchor, "hardcoreFreezersRotAnchor", float.NaN);
                Scribe_Values.Look(ref remainder, "hardcoreFreezersRotCarry");
                if (anchor == progress && IsValidRemainder(anchor, remainder))
                {
                    FractionalProgress fraction = carry.GetOrCreateValue(comp);
                    fraction.Anchor = anchor;
                    fraction.Remainder = remainder;
                }
                else
                {
                    carry.Remove(comp);
                }
            }
        }

        private static bool IsValidRemainder(float anchor, double remainder)
        {
            if (!float.IsFinite(anchor) || !double.IsFinite(remainder) || remainder == 0d)
                return false;

            float neighbor = remainder > 0d ? MathF.BitIncrement(anchor) : MathF.BitDecrement(anchor);
            double halfStep = Math.Abs((double)neighbor - anchor) * 0.5d;
            return float.IsFinite(neighbor) && Math.Abs(remainder) <= halfStep && (float)(anchor + remainder) == anchor;
        }

        private sealed class FractionalProgress
        {
            internal float Anchor;
            internal double Remainder;
        }
    }
}
