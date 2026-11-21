using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Hardcore_Freezers
{
    public class ModSettings : Verse.ModSettings
    {
        public static int Cutoff = RotCurve.DefaultCutoff;
        public static StoredCurve InteriorPoints = new(RotCurve.DefaultPoints());
        public static bool FreezeDamageEnabled = true;
        public static StoredCurve FreezeDamagePoints = new(FreezeDamageCurve.DefaultPoints());

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Cutoff, "deepFreezeCutoff", int.MinValue);
            Scribe_Deep.Look(ref InteriorPoints, "interiorPoints");
            Scribe_Values.Look(ref FreezeDamageEnabled, "freezeDamageEnabled", true);
            Scribe_Deep.Look(ref FreezeDamagePoints, "freezeDamagePoints");

            if (Scribe.mode == LoadSaveMode.LoadingVars && (InteriorPoints?.HasPoints != true ||
                RotCurve.Validate(Cutoff, InteriorPoints.Points, out _) != RotCurve.Error.None))
            {
                Cutoff = RotCurve.DefaultCutoff;
                InteriorPoints = new StoredCurve(RotCurve.DefaultPoints());
            }

            if (Scribe.mode == LoadSaveMode.LoadingVars && (FreezeDamagePoints?.HasPoints != true ||
                FreezeDamageCurve.Validate(FreezeDamagePoints.Points, out _) != FreezeDamageCurve.Error.None))
                FreezeDamagePoints = new StoredCurve(FreezeDamageCurve.DefaultPoints());
        }

        internal static void Apply(RotCurve curve, List<CurvePoint> points)
        {
            if (Cutoff == curve.Cutoff && InteriorPoints.Points.SequenceEqual(points))
                return;

            InteriorPoints = new StoredCurve(points);
            Cutoff = curve.Cutoff;
            RotRuntime.Apply(curve);
        }

        internal static void ApplyFreezeDamage(bool enabled, FreezeDamageCurve curve, List<CurvePoint> points)
        {
            FreezeDamageEnabled = enabled;
            if (FreezeDamagePoints.Points.SequenceEqual(points))
                return;

            FreezeDamagePoints = new StoredCurve(points);
            FreezeDamageRuntime.ApplyCurve(curve);
        }

        public sealed class StoredCurve : SimpleCurve, IExposable
        {
            internal bool HasPoints { get; private set; }

            public StoredCurve()
            {
            }

            internal StoredCurve(IEnumerable<CurvePoint> points) : base(points)
            {
            }

            void IExposable.ExposeData()
            {
                List<Vector2> coordinates = Scribe.mode == LoadSaveMode.Saving
                    ? Points.Select(point => point.Loc).ToList()
                    : null;
                Scribe_Collections.Look(ref coordinates, "points", LookMode.Value);

                if (Scribe.mode == LoadSaveMode.LoadingVars && coordinates != null)
                {
                    SetPoints(coordinates.Select(coordinate => new CurvePoint(coordinate)));
                    HasPoints = true;
                }
            }
        }
    }
}
