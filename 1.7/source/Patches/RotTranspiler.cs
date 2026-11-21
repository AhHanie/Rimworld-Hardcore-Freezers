using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Hardcore_Freezers.Patches
{
    internal static class RotTranspiler
    {
        internal static int FindCall(List<CodeInstruction> instructions, MethodInfo method)
        {
            if (method == null || instructions.Count(instruction => instruction.Calls(method)) != 1)
                throw new InvalidOperationException($"Expected exactly one call to {method?.Name}.");

            return instructions.FindIndex(instruction => instruction.Calls(method));
        }

        internal static void Replace(List<CodeInstruction> instructions, int start, int length, params CodeInstruction[] replacement)
        {
            CodeInstruction first = instructions[start];
            CodeInstruction last = instructions[start + length - 1];
            if (first.blocks.Any(block => block.blockType == ExceptionBlockType.EndExceptionBlock))
                throw new InvalidOperationException("An exception block ends at the start of the rot update.");

            for (int i = start + 1; i < start + length; i++)
            {
                CodeInstruction instruction = instructions[i];
                if (instruction.labels.Count != 0 || instruction.blocks.Any(block =>
                    instruction != last || block.blockType != ExceptionBlockType.EndExceptionBlock))
                    throw new InvalidOperationException("The rot update has an internal branch or exception boundary.");
            }

            first.MoveLabelsTo(replacement[0]);
            first.MoveBlocksTo(replacement[0]);
            last.MoveBlocksTo(replacement[^1]);
            instructions.RemoveRange(start, length);
            instructions.InsertRange(start, replacement);
        }
    }
}
