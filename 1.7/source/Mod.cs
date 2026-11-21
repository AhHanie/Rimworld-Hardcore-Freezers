using HarmonyLib;
using Hardcore_Freezers.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Hardcore_Freezers
{
    public class Mod : Verse.Mod
    {
        private const string HarmonyOwner = "sk.hardcorefreezers";
        private static readonly (Type PatchClass, bool FreezeDamageOnly)[] PatchClasses =
        {
            (typeof(CompRottable_TickInterval_Patch), false),
            (typeof(CompRottable_TicksUntilRotAtTemp_Patch), false),
            (typeof(CompRottable_TicksUntilRotAtCurrentTemp_Patch), false),
            (typeof(CompRottable_CompInspectStringExtra_Patch), false),
            (typeof(CompRottable_PostExposeData_Patch), false),
            (typeof(CompRottable_PreAbsorbStack_Patch), false),
            (typeof(CompRottable_PostSplitOff_Patch), true)
        };

        private readonly ModSettingsWindow settingsWindow = new();

        public Mod(ModContentPack content) : base(content)
        {
            LongEventHandler.QueueLongEvent(Init, "HardcoreFreezers.LoadingLabel", doAsynchronously: true, null);
        }

        private void Init()
        {
            GetSettings<ModSettings>();
            bool freezeDamageEnabled = ModSettings.FreezeDamageEnabled;
            RotCurve curve = new(ModSettings.Cutoff, ModSettings.InteriorPoints.Points);
            FreezeDamageCurve freezeDamage = freezeDamageEnabled ? new FreezeDamageCurve(ModSettings.FreezeDamagePoints.Points) : null;
            Harmony harmony = new(HarmonyOwner);
            string target = null;

            try
            {
                foreach ((Type patchClass, bool freezeDamageOnly) in PatchClasses)
                {
                    HarmonyMethod patch = patchClass.GetCustomAttribute<HarmonyPatch>().info;
                    target = $"{patch.declaringType.FullName}.{patch.methodName}";
                    int expected = freezeDamageOnly && !freezeDamageEnabled ? 0 : 1;
                    if (harmony.CreateClassProcessor(patchClass).Patch().Count != expected)
                        throw new InvalidOperationException($"Expected {expected} patched methods.");
                }

                RotRuntime.Enable(curve);
                FreezeDamageRuntime.Initialize(freezeDamage);
            }
            catch (Exception exception)
            {
                List<Exception> failures = new() { exception };
                foreach (MethodBase method in harmony.GetPatchedMethods().ToArray())
                {
                    try
                    {
                        harmony.Unpatch(method, HarmonyPatchType.All, HarmonyOwner);
                    }
                    catch (Exception rollbackFailure)
                    {
                        failures.Add(rollbackFailure);
                    }
                }

                Logger.Exception(failures.Count == 1 ? exception : new AggregateException(failures),
                    $"Could not install the temperature curves at {target}");
            }
        }

        public override string SettingsCategory()
        {
            return "HardcoreFreezers.SettingsTitle".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            settingsWindow.Draw(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            settingsWindow.ClearDraft();
        }
    }
}
