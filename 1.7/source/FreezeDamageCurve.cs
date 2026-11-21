using System;
using System.Collections.Generic;
using Verse;

namespace Hardcore_Freezers
{
    internal sealed class FreezeDamageCurve
    {
        internal const int MinTemperature = -273;
        internal const int WarmAnchor = 0;
        internal const float MaxDamagePerDay = 1000f;

        private readonly int coldest;
        private readonly int threshold;
        private readonly float[] rates;

        internal enum Error
        {
            None,
            Temperature,
            Rate,
            Duplicate,
            Increasing
        }

        internal static CurvePoint[] DefaultPoints() => new CurvePoint[]
        {
            new(-150, 10f),
            new(-100, 5f),
            new(-75, 2f),
            new(-40, 0f)
        };

        internal static Error Validate(List<CurvePoint> points, out float temperature)
        {
            temperature = 0f;
            foreach (CurvePoint point in points)
            {
                temperature = point.x;
                if (!float.IsFinite(point.x) || MathF.Truncate(point.x) != point.x || point.x < MinTemperature || point.x >= WarmAnchor)
                    return Error.Temperature;
                if (!float.IsFinite(point.y) || point.y < 0f || point.y > MaxDamagePerDay)
                    return Error.Rate;
            }

            points.Sort(static (left, right) => left.x.CompareTo(right.x));
            for (int i = 1; i < points.Count; i++)
            {
                temperature = points[i].x;
                if (temperature == points[i - 1].x)
                    return Error.Duplicate;
                if (points[i].y > points[i - 1].y)
                    return Error.Increasing;
            }

            return Error.None;
        }

        internal FreezeDamageCurve(IReadOnlyList<CurvePoint> points)
        {
            if (points.Count == 0 || points[0].y == 0f)
            {
                rates = Array.Empty<float>();
                return;
            }

            coldest = (int)points[0].x;
            threshold = WarmAnchor;
            foreach (CurvePoint point in points)
            {
                if (point.y == 0f)
                {
                    threshold = (int)point.x;
                    break;
                }
            }

            rates = new float[threshold - coldest + 1];
            CurvePoint left = points[0];
            for (int i = 1; left.x < threshold; i++)
            {
                CurvePoint right = i < points.Count ? points[i] : new CurvePoint(WarmAnchor, 0f);
                int width = (int)(right.x - left.x);
                for (int j = 0; j < width; j++)
                {
                    double blend = (double)j / width;
                    rates[(int)left.x - coldest + j] = (float)((1d - blend) * left.y + blend * right.y);
                }
                left = right;
            }
        }

        internal float RateAt(float temperature)
        {
            if (!(temperature < threshold) || rates.Length == 0 || !float.IsFinite(temperature))
                return 0f;
            if (temperature <= coldest)
                return rates[0];

            int lower = (int)MathF.Floor(temperature);
            int index = lower - coldest;
            double blend = (double)temperature - lower;
            return (float)((1d - blend) * rates[index] + blend * rates[index + 1]);
        }
    }
}
