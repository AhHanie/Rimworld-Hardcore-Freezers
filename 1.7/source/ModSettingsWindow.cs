using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Verse;

namespace Hardcore_Freezers
{
    internal sealed class ModSettingsWindow
    {
        private const float RowHeight = 32f;
        private const float Gap = 12f;
        private const float PlotHeight = 180f;
        private const int SampleTemperature = -5;

        private readonly SimpleCurveDrawerStyle plotStyle = new()
        {
            DrawBackground = true,
            DrawMeasures = true,
            UseAntiAliasedLines = true,
            UseFixedScale = true,
            FixedScale = new Vector2(0f, 50f),
            UseFixedSection = true,
            XIntegersOnly = true,
            YIntegersOnly = true,
            MeasureLabelsYCount = 6
        };

        private readonly FreezeDamageSettingsWindow freezeDamageWindow = new();
        private bool showFreezeDamage;

        private Vector2 scroll;
        private string cutoffText;
        private List<PointRow> rows;
        private List<CurvePoint> points;
        private RotCurve curve;
        private SimpleCurve plot;
        private TaggedString error;
        private bool dirty;

        internal void ClearDraft()
        {
            rows = null;
            points = null;
            curve = null;
            plot = null;
            scroll = Vector2.zero;
            freezeDamageWindow.ClearDraft();
        }

        internal void Draw(Rect parent)
        {
            parent.yMin += TabDrawer.TabHeight;
            TabDrawer.DrawTabs(parent, new List<TabRecord>
            {
                new("HardcoreFreezers.SpoilageTab".Translate(), () => showFreezeDamage = false, !showFreezeDamage),
                new("HardcoreFreezers.FreezeDamageTab".Translate(), () => showFreezeDamage = true, showFreezeDamage)
            });
            parent.yMin += Gap;
            if (showFreezeDamage)
            {
                freezeDamageWindow.Draw(parent);
                return;
            }

            if (rows == null)
            {
                SetFields(ModSettings.Cutoff, ModSettings.InteriorPoints.Points);
                dirty = true;
            }

            Rect viewport = new(parent.x, parent.y, parent.width, parent.height - 84f);
            float width = viewport.width - 20f;
            float contentHeight = RowHeight * (rows.Count + 6) + PlotHeight + 4f;
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
                ModSettings.Apply(curve, points);
                SetFields(ModSettings.Cutoff, ModSettings.InteriorPoints.Points);
            }

            if (Widgets.ButtonText(new Rect(parent.x + buttonWidth + Gap, buttonY, buttonWidth, RowHeight),
                "HardcoreFreezers.Reset".Translate()))
            {
                SetFields(RotCurve.DefaultCutoff, RotCurve.DefaultPoints());
                dirty = true;
                ValidateDraft();
                ModSettings.Apply(curve, points);
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
            Widgets.Label(new Rect(0f, 4f, temperatureWidth, RowHeight), "HardcoreFreezers.Cutoff".Translate());
            Edit(new Rect(rateX, 0f, rateWidth, RowHeight - 4f), ref cutoffText);

            float y = RowHeight;
            Widgets.Label(new Rect(0f, y + 4f, temperatureWidth, RowHeight), "HardcoreFreezers.Temperature".Translate());
            Widgets.Label(new Rect(rateX, y + 4f, rateWidth, RowHeight), "HardcoreFreezers.RotRate".Translate());

            int.TryParse(cutoffText, out int cutoff);
            int removeIndex = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                y += RowHeight;
                PointRow row = rows[i];
                Rect temperatureField = new(0f, y, temperatureWidth, RowHeight - 4f);
                if (int.TryParse(row.Temperature, out int temperature) &&
                    (temperature >= RotCurve.JoinTemperature || (cutoff < 0 && temperature <= cutoff)))
                    Widgets.DrawBoxSolid(temperatureField.ExpandedBy(1f), Color.red);

                Edit(temperatureField, ref row.Temperature);
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
            Widgets.Label(new Rect(0f, y + 4f, width, RowHeight), "HardcoreFreezers.WarmAnchor".Translate(
                RotCurve.JoinTemperature.ToString(), RotCurve.JoinRate.ToString("0.##%")));

            ValidateDraft();
            int? newTemperature = curve == null ? null : FindOpenTemperature();
            y += RowHeight;
            if (Widgets.ButtonText(new Rect(0f, y, temperatureWidth, RowHeight - 4f),
                "HardcoreFreezers.Add".Translate(), active: newTemperature.HasValue))
            {
                rows.Add(new PointRow(newTemperature.Value, curve.RateAt(newTemperature.Value)));
                rows.Sort(static (left, right) => int.Parse(left.Temperature).CompareTo(int.Parse(right.Temperature)));
                dirty = true;
                ValidateDraft();
            }

            if (plot != null)
            {
                y += RowHeight;
                Widgets.Label(new Rect(0f, y + 4f, width, RowHeight), "HardcoreFreezers.Preview".Translate());
                plotStyle.FixedSection = new FloatRange(curve.Cutoff, RotCurve.JoinTemperature);
                SimpleCurveDrawer.DrawCurve(new Rect(0f, y + RowHeight, width, PlotHeight), plot, plotStyle);

                if (curve.Cutoff <= SampleTemperature)
                {
                    float rate = curve.RateAt(SampleTemperature);
                    string sampleRate = rate > 0f && MathF.Round(rate, 4) == 0f
                        ? "HardcoreFreezers.PreviewTinyRate".Translate(0.0001f.ToString("0.##%"))
                        : rate.ToString("0.##%");
                    Widgets.Label(new Rect(0f, y + RowHeight + PlotHeight, width, RowHeight),
                        "HardcoreFreezers.PreviewSample".Translate(SampleTemperature.ToString(), sampleRate));
                }
            }
        }

        private void SetFields(int cutoff, IEnumerable<CurvePoint> source)
        {
            cutoffText = cutoff.ToString();
            rows = source.Select(point => new PointRow((int)point.x, point.y)).ToList();
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
            if (!int.TryParse(cutoffText, out int cutoff) || cutoff < RotCurve.MinCutoff || cutoff > RotCurve.MaxCutoff)
            {
                error = "HardcoreFreezers.ErrorCutoff".Translate(RotCurve.MinCutoff.ToString(), RotCurve.MaxCutoff.ToString());
                return;
            }

            points = new List<CurvePoint>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                PointRow row = rows[i];
                if (!int.TryParse(row.Temperature, out int temperature))
                {
                    error = "HardcoreFreezers.ErrorTemperature".Translate(i + 1);
                    return;
                }

                if (!double.TryParse(row.Rate, NumberStyles.Float, CultureInfo.CurrentCulture, out double percent) ||
                    !(percent > 0d && percent <= 50d) || (float)(percent / 100d) == 0f)
                {
                    error = "HardcoreFreezers.ErrorRate".Translate(temperature.ToString());
                    return;
                }

                points.Add(new CurvePoint(temperature, (float)(percent / 100d)));
            }

            RotCurve.Error validation = RotCurve.Validate(cutoff, points, out float invalidTemperature);
            string invalid = invalidTemperature.ToString();
            switch (validation)
            {
                case RotCurve.Error.OutsideAnchors:
                    error = "HardcoreFreezers.ErrorOutsideAnchors".Translate(invalid, cutoff.ToString(), RotCurve.JoinTemperature.ToString());
                    return;
                case RotCurve.Error.Duplicate:
                    error = "HardcoreFreezers.ErrorDuplicate".Translate(invalid);
                    return;
                case RotCurve.Error.Decreasing:
                    error = "HardcoreFreezers.ErrorDecreasing".Translate(invalid);
                    return;
                case RotCurve.Error.None:
                    break;
                default:
                    error = "HardcoreFreezers.ErrorInvalidCurve".Translate();
                    return;
            }

            curve = new RotCurve(cutoff, points);
            plot = new SimpleCurve();
            plot.Add(cutoff, 0f, sort: false);
            foreach (CurvePoint point in points)
                plot.Add(point.x, 100f * curve.RateAt(point.x), sort: false);
            plot.Add(RotCurve.JoinTemperature, 100f * RotCurve.JoinRate, sort: false);
            if (cutoff <= SampleTemperature)
                plot.View.SetDebugInput(this, SampleTemperature);
        }

        private int? FindOpenTemperature()
        {
            int? midpoint = null;
            int largestGap = 1;
            int left = curve.Cutoff;
            for (int i = 0; i <= points.Count; i++)
            {
                int right = i < points.Count ? (int)points[i].x : RotCurve.JoinTemperature;
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

        internal sealed class PointRow
        {
            internal string Temperature;
            internal string Rate;

            internal PointRow(int temperature, float rate, int rateScale = 100)
            {
                Temperature = temperature.ToString();
                Rate = (rateScale * double.Parse(rate.ToString("R"))).ToString("G9");
            }
        }
    }
}
