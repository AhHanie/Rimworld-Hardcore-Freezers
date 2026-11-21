using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Hardcore_Freezers.Patches
{
    [HarmonyPatch(typeof(CompRottable), "TickInterval", typeof(int))]
    internal static class CompRottable_TickInterval_Patch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> code = instructions.ToList();
            int start = RotTranspiler.FindCall(code, AccessTools.DeclaredMethod(typeof(GenTemperature),
                nameof(GenTemperature.RotRateAtTemperature), new[] { typeof(float) }));
            const int length = 11;
            if (start + length > code.Count ||
                !code[start + 1].IsStloc() || !code[start + 2].IsLdarg(0) || !code[start + 3].IsLdarg(0) ||
                !code[start + 4].Calls(AccessTools.DeclaredPropertyGetter(typeof(CompRottable), nameof(CompRottable.RotProgress))) ||
                !code[start + 5].IsLdloc() || code[start + 5].LocalIndex() != code[start + 1].LocalIndex() ||
                code[start + 5].opcode == OpCodes.Ldloca || code[start + 5].opcode == OpCodes.Ldloca_S ||
                !code[start + 6].IsLdarg(1) || code[start + 7].opcode != OpCodes.Conv_R4 ||
                code[start + 8].opcode != OpCodes.Mul || code[start + 9].opcode != OpCodes.Add ||
                !code[start + 10].Calls(AccessTools.DeclaredPropertySetter(
                    typeof(CompRottable), nameof(CompRottable.RotProgress))))
                throw new InvalidOperationException("Expected the 1.7 RotProgress increment after the rot-rate call.");

            int rateLocal = code[start + 1].LocalIndex();
            if (code.Count(instruction => (instruction.IsLdloc() || instruction.IsStloc()) && instruction.LocalIndex() == rateLocal) != 2)
                throw new InvalidOperationException("The rot-rate local is used outside the progress update.");

            if (code[^1].opcode != OpCodes.Ret || code.Any(instruction => instruction.blocks.Count != 0))
                throw new InvalidOperationException("Expected a direct return from the rot update after freeze damage destroys the item.");

            Label end = generator.DefineLabel();
            code[^1].labels.Add(end);
            RotTranspiler.Replace(code, start, length,
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                CodeInstruction.Call(typeof(CompRottable_TickInterval_Patch), nameof(AdvanceAndDamage)),
                new CodeInstruction(OpCodes.Brtrue, end));
            return code;
        }

        private static bool AdvanceAndDamage(float temperature, CompRottable comp, int delta)
        {
            RotRuntime.Advance(temperature, comp, delta);
            return FreezeDamageRuntime.TryApply(temperature, comp, delta);
        }
    }
}
