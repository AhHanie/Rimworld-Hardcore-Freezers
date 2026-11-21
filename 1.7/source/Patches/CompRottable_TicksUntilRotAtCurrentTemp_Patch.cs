using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace Hardcore_Freezers.Patches
{
    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.TicksUntilRotAtCurrentTemp), MethodType.Getter)]
    internal static class CompRottable_TicksUntilRotAtCurrentTemp_Patch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            int start = RotTranspiler.FindCall(code,
                AccessTools.DeclaredMethod(typeof(Mathf), nameof(Mathf.RoundToInt), new[] { typeof(float) }));
            if (start + 1 >= code.Count || code[start + 1].opcode != OpCodes.Conv_R4)
                throw new InvalidOperationException("Expected conversion to float after temperature rounding.");

            RotTranspiler.Replace(code, start, 2, CodeInstruction.Call(typeof(RotRuntime), nameof(RotRuntime.EstimateTemperature)));
            return code;
        }
    }
}
