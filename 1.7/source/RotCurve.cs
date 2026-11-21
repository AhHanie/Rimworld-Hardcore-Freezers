using System;
using System.Collections.Generic;
using Verse;

namespace Hardcore_Freezers
{
    internal sealed class RotCurve
    {
        internal const int JoinTemperature = 5;
        internal const float JoinRate = 0.5f;
        internal const int MinCutoff = -273;
        internal const int MaxCutoff = -1;
        internal const int DefaultCutoff = -100;

        private readonly double[] values;
        private readonly double[] slopes;

        internal int Cutoff { get; }

        internal enum Error
        {
            None,
            Cutoff,
            MissingPoints,
            Temperature,
            OutsideAnchors,
            Rate,
            Duplicate,
            Decreasing
        }

        internal static CurvePoint[] DefaultPoints() => new CurvePoint[]
        {
            new(-40, 0.015f),
            new(-20, 0.05f),
            new(-10, 0.15f),
            new(-5, 0.25f),
            new(0, 0.35f)
        };

        internal static Error Validate(int cutoff, List<CurvePoint> points, out float temperature)
        {
            temperature = 0f;
            if (cutoff < MinCutoff || cutoff > MaxCutoff)
                return Error.Cutoff;
            if (points == null)
                return Error.MissingPoints;

            foreach (CurvePoint point in points)
            {
                temperature = point.x;
                if (!float.IsFinite(point.x) || MathF.Truncate(point.x) != point.x)
                    return Error.Temperature;
                if (point.x <= cutoff || point.x >= JoinTemperature)
                    return Error.OutsideAnchors;
                if (!float.IsFinite(point.y) || point.y <= 0f || point.y > JoinRate)
                    return Error.Rate;
            }

            points.Sort(static (left, right) => left.x.CompareTo(right.x));
            for (int i = 1; i < points.Count; i++)
            {
                temperature = points[i].x;
                if (temperature == points[i - 1].x)
                    return Error.Duplicate;
                if (points[i].y < points[i - 1].y)
                    return Error.Decreasing;
            }

            return Error.None;
        }

        internal RotCurve(int cutoff, IReadOnlyList<CurvePoint> points)
        {
            Cutoff = cutoff;
            values = new double[JoinTemperature - cutoff];
            slopes = new double[values.Length];

            CurvePoint left = new(cutoff, 0f);
            for (int i = 0; i <= points.Count; i++)
            {
                CurvePoint right = i < points.Count ? points[i] : new CurvePoint(JoinTemperature, JoinRate);
                double slope = ((double)right.y - left.y) / (right.x - left.x);
                int start = (int)left.x - cutoff;
                int end = (int)right.x - cutoff;
                for (int j = start; j < end; j++)
                {
                    values[j] = left.y + (j - start) * slope;
                    slopes[j] = slope;
                }
                left = right;
            }
        }

        internal static bool UsesCustomRate(float temperature) => temperature < JoinTemperature && float.IsFinite(temperature);

        internal float RateAt(float temperature) => UsesCustomRate(temperature)
            ? RateBelowJoin(temperature)
            : GenTemperature.RotRateAtTemperature(temperature);

        internal float RateBelowJoin(float temperature)
        {
            if (temperature <= Cutoff)
                return 0f;

            int lower = (int)MathF.Floor(temperature);
            int index = lower - Cutoff;
            float rate = (float)(values[index] + (temperature - (double)lower) * slopes[index]);
            return MathF.Max(float.Epsilon, rate);
        }
    }
}
