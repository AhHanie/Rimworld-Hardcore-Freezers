using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Verse;
using PointRow = Hardcore_Freezers.ModSettingsWindow.PointRow;

namespace Hardcore_Freezers
{
    internal sealed class FreezeDamageSettingsWindow
    {
        private const float RowHeight = 32f;
        private const float Gap = 12f;
        private const float PlotHeight = 180f;
        private const int SampleTemperature = -75;

        private readonly SimpleCurveDrawerStyle plotStyle = new()
        {
            DrawBackground = true,
            DrawMeasures = true,
            UseAntiAliasedLines = true,
            UseFixedScale = true,
            UseFixedSection = true,
            XIntegersOnly = true,
            YIntegersOnly = true,
            MeasureLabelsYCount = 6
        };

        private List<PointRow> rows;
        private List<CurvePoint> points;
        private FreezeDamageCurve curve;
        private SimpleCurve plot;
        private Vector2 scroll;
        private TaggedString error;
        private bool enabled;
        private bool dirty;

        internal void ClearDraft()
        {
            rows = null;
            points = null;
            curve = null;
            plot = null;
            scroll = Vector2.zero;
        }

        internal void Draw(Rect parent)
        {
            if (rows == null)
            {
                SetFields(ModSettings.FreezeDamageEnabled, ModSettings.FreezeDamagePoints.Points);
                dirty = true;
            }

            Rect viewport = new(parent.x, parent.y, parent.width, parent.height - 84f);
            float width = viewport.width - 20f;
            float contentHeight = RowHeight * (rows.Count + 7) + PlotHeight + 4f;
            Widgets.BeginScrollView(viewport, ref scroll, new Rect(0f, 0f, width, contentHeight));
            DrawDraft(width);
            Widgets.EndScrollView();

            if (!error.NullOrEmpty())
                Widgets.Label(new Rect(parent.x, viewport.yMax + 4f, parent.width, 44f), error);

            float buttonY = parent.yMax - RowHeight;
            const float buttonWidth = 180f;
            if (Widgets.ButtonText(new Rect(parent.x, buttonY, buttonWidth, RowHeight),
                "HardcoreFreezers.Apply".Translate(), active: curve != null))
            {
                ModSettings.ApplyFreezeDamage(enabled, curve, points);
                SetFields(ModSettings.FreezeDamageEnabled, ModSettings.FreezeDamagePoints.Points);
            }

            if (Widgets.ButtonText(new Rect(parent.x + buttonWidth + Gap, buttonY, buttonWidth, RowHeight),
                "HardcoreFreezers.Reset".Translate()))
            {
                SetFields(true, FreezeDamageCurve.DefaultPoints());
                dirty = true;
                ValidateDraft();
                ModSettings.ApplyFreezeDamage(enabled, curve, points);
                scroll = Vector2.zero;
            }
        }

        private void DrawDraft(float width)
        {
            float temperatureWidth = width * 0.30f;
            float rateWidth = width * 0.43f;
            float rateX = temperatureWidth + Gap;
            float buttonX = rateX + rateWidth + Gap;
            float buttonWidth = width - buttonX;
            Widgets.CheckboxLabeled(new Rect(0f, 0f, width, RowHeight), "HardcoreFreezers.EnableFreezeDamage".Translate(), ref enabled);

            float y = RowHeight;
            Widgets.Label(new Rect(0f, y, width, RowHeight), "HardcoreFreezers.FreezeDamageRestart".Translate());
            y += RowHeight;
            Widgets.Label(new Rect(0f, y + 4f, temperatureWidth, RowHeight), "HardcoreFreezers.Temperature".Translate());
            Widgets.Label(new Rect(rateX, y + 4f, rateWidth, RowHeight), "HardcoreFreezers.FreezeDamageRate".Translate());

            int removeIndex = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                y += RowHeight;
                PointRow row = rows[i];
                Edit(new Rect(0f, y, temperatureWidth, RowHeight - 4f), ref row.Temperature);
                Edit(new Rect(rateX, y, rateWidth, RowHeight - 4f), ref row.Rate);
                if (Widgets.ButtonText(new Rect(buttonX, y, buttonWidth, RowHeight - 4f), "HardcoreFreezers.Remove".Translate()))
                    removeIndex = i;
            }

            if (removeIndex >= 0)
            {
                rows.RemoveAt(removeIndex);
                dirty = true;
            }

            y += RowHeight;
            Widgets.Label(new Rect(0f, y + 4f, width, RowHeight), "HardcoreFreezers.FreezeDamageWarmAnchor".Translate());
            ValidateDraft();

            int? newTemperature = curve == null ? null : FindOpenTemperature();
            y += RowHeight;
            if (Widgets.ButtonText(new Rect(0f, y, temperatureWidth, RowHeight - 4f),
                "HardcoreFreezers.Add".Translate(), active: newTemperature.HasValue))
            {
                rows.Add(new PointRow(newTemperature.Value, curve.RateAt(newTemperature.Value), rateScale: 1));
                rows.Sort(static (left, right) => int.Parse(left.Temperature).CompareTo(int.Parse(right.Temperature)));
                dirty = true;
                ValidateDraft();
            }

            if (plot != null)
            {
                y += RowHeight;
                Widgets.Label(new Rect(0f, y + 4f, width, RowHeight), "HardcoreFreezers.FreezeDamagePreview".Translate());
                SimpleCurveDrawer.DrawCurve(new Rect(0f, y + RowHeight, width, PlotHeight), plot, plotStyle);
                Widgets.Label(new Rect(0f, y + RowHeight + PlotHeight, width, RowHeight),
                    "HardcoreFreezers.FreezeDamageSample".Translate(SampleTemperature.ToString(), curve.RateAt(SampleTemperature).ToString("G4")));
            }
        }

        private void SetFields(bool isEnabled, IEnumerable<CurvePoint> source)
        {
            enabled = isEnabled;
            rows = source.Select(point => new PointRow((int)point.x, point.y, rateScale: 1)).ToList();
        }

        private void Edit(Rect rect, ref string text)
        {
            string updated = Widgets.TextField(rect, text, 32);
            if (updated != text)
            {
                text = updated;
                dirty = true;
            }
        }

        private void ValidateDraft()
        {
            if (!dirty)
                return;

            dirty = false;
            curve = null;
            plot = null;
            error = default;
            points = new List<CurvePoint>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                PointRow row = rows[i];
                if (!int.TryParse(row.Temperature, out int temperature) || temperature < FreezeDamageCurve.MinTemperature || temperature >= FreezeDamageCurve.WarmAnchor)
                {
                    error = "HardcoreFreezers.ErrorFreezeDamageTemperature".Translate(i + 1, FreezeDamageCurve.MinTemperature, FreezeDamageCurve.WarmAnchor - 1);
                    return;
                }

                if (!float.TryParse(row.Rate, NumberStyles.Float, CultureInfo.CurrentCulture, out float rate) ||
                    !(rate >= 0f && rate <= FreezeDamageCurve.MaxDamagePerDay))
                {
                    error = "HardcoreFreezers.ErrorFreezeDamageRate".Translate(temperature, FreezeDamageCurve.MaxDamagePerDay.ToString());
                    return;
                }
                points.Add(new CurvePoint(temperature, rate));
            }

            FreezeDamageCurve.Error validation = FreezeDamageCurve.Validate(points, out float invalidTemperature);
            switch (validation)
            {
                case FreezeDamageCurve.Error.Duplicate:
                    error = "HardcoreFreezers.ErrorDuplicate".Translate(invalidTemperature.ToString());
                    return;
                case FreezeDamageCurve.Error.Increasing:
                    error = "HardcoreFreezers.ErrorFreezeDamageIncreasing".Translate(invalidTemperature.ToString());
                    return;
                case FreezeDamageCurve.Error.None:
                    break;
                default:
                    error = "HardcoreFreezers.ErrorInvalidCurve".Translate();
                    return;
            }

            curve = new FreezeDamageCurve(points);
            float coldest = points.Count == 0 ? -150 : Math.Min(SampleTemperature - 5, points[0].x);
            float highestRate = curve.RateAt(coldest);
            plot = new SimpleCurve();
            if (points.Count == 0 || coldest < points[0].x)
                plot.Add(coldest, highestRate, sort: false);
            foreach (CurvePoint point in points)
                plot.Add(point, sort: false);
            plot.Add(FreezeDamageCurve.WarmAnchor, 0f, sort: false);
            plot.View.SetDebugInput(this, SampleTemperature);
            plotStyle.FixedSection = new FloatRange(coldest, FreezeDamageCurve.WarmAnchor);
            plotStyle.FixedScale = new Vector2(0f, Math.Max(1f, MathF.Ceiling(highestRate)));
        }

        private int? FindOpenTemperature()
        {
            if (points.Count == 0)
                return SampleTemperature;

            int? midpoint = null;
            int largestGap = 1;
            int left = FreezeDamageCurve.MinTemperature - 1;
            for (int i = 0; i <= points.Count; i++)
            {
                int right = i < points.Count ? (int)points[i].x : FreezeDamageCurve.WarmAnchor;
                int gap = right - left;
                if (gap > largestGap)
                {
                    largestGap = gap;
                    midpoint = left + gap / 2;
                }
                left = right;
            }
            return midpoint;
        }
    }
}
