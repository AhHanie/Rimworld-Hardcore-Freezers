using System;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace Hardcore_Freezers
{
    internal static class FreezeDamageRuntime
    {
        private static volatile FreezeDamageCurve activeCurve;
        private static readonly ConditionalWeakTable<CompRottable, DamageProgress> progress = new();

        internal static void Initialize(FreezeDamageCurve curve) => activeCurve = curve;

        internal static void ApplyCurve(FreezeDamageCurve curve)
        {
            if (activeCurve != null)
                activeCurve = curve;
        }

        internal static float RateAt(Thing thing, float temperature)
        {
            float rate = activeCurve?.RateAt(temperature) ?? 0f;
            return rate > 0f && thing.def.useHitPoints ? rate : 0f;
        }

        internal static float CurrentRate(Thing thing) =>
            activeCurve is FreezeDamageCurve curve && thing.def.useHitPoints ? curve.RateAt(thing.AmbientTemperature) : 0f;

        internal static bool TryApply(float temperature, CompRottable comp, int delta)
        {
            if (temperature >= FreezeDamageCurve.WarmAnchor || delta <= 0)
                return false;

            Thing thing = comp.parent;
            float rate = RateAt(thing, temperature);
            if (rate == 0f)
                return false;

            DamageProgress entry = progress.GetOrCreateValue(comp);
            double total = entry.Pending + (double)rate * delta;
            double wholeDamage = Math.Floor(total / GenDate.TicksPerDay);
            entry.Pending = total - wholeDamage * GenDate.TicksPerDay;
            if (wholeDamage == 0d)
                return false;

            thing.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, (float)Math.Min(wholeDamage, thing.HitPoints)));
            return thing.Destroyed;
        }

        internal static void ExposeProgress(CompRottable comp)
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                double pending = GetProgress(comp);
                if (pending > 0d)
                    Scribe_Values.Look(ref pending, "hardcoreFreezersColdDamageProgress");
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars && comp.parent.def.useHitPoints)
            {
                double pending = 0d;
                Scribe_Values.Look(ref pending, "hardcoreFreezersColdDamageProgress");
                SetProgress(comp, pending > 0d && pending < GenDate.TicksPerDay ? pending : 0d);
            }
        }

        internal static void AbsorbProgress(CompRottable target, Thing source, int count)
        {
            CompRottable from = source.TryGetComp<CompRottable>();
            double targetProgress = GetProgress(target);
            double sourceProgress = from == null ? 0d : GetProgress(from);
            if (count > 0 && (targetProgress != 0d || sourceProgress != 0d))
            {
                double originalCount = target.parent.stackCount;
                SetProgress(target, (targetProgress * originalCount + sourceProgress * count) / (originalCount + count));
            }
        }

        internal static void CopyProgress(CompRottable source, Thing piece)
        {
            double pending = GetProgress(source);
            if (pending > 0d && piece.TryGetComp<CompRottable>() is CompRottable target && target != source)
                SetProgress(target, pending);
        }

        private static double GetProgress(CompRottable comp) =>
            progress.TryGetValue(comp, out DamageProgress entry) ? entry.Pending : 0d;

        private static void SetProgress(CompRottable comp, double pending)
        {
            if (pending > 0d)
                progress.GetOrCreateValue(comp).Pending = pending;
            else
                progress.Remove(comp);
        }

        private sealed class DamageProgress
        {
            internal double Pending;
        }
    }
}
