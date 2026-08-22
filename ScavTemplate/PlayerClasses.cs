using CUCoreLib.Helpers;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ModNamespace
{
    internal static class PlayerClasses
    {
        public static void AssignLocalClassIfNeeded(Body body)
        {
            if (body == null || !IsLocalBody(body))
                return;

            PlayerClassStatus status = body.GetStatus<PlayerClassStatus>();
            if (status.Assigned)
                return;

            if (!ClassSelection.IsMultiplayerEnabled())
                ClassSelection.RefreshSelectedClassFromRunSettings();

            status.ClassId = Plugin.SelectedClassId;
            status.Assigned = true;
        }

        public static bool IsDrugTester(Body body)
        {
            if (body == null)
                return false;

            return body.GetStatus<PlayerClassStatus>().ClassId == Plugin.DrugTesterClassId;
        }

        public static bool IsLocalBody(Body body)
        {
            return body != null && PlayerCamera.main != null && PlayerCamera.main.body == body;
        }

        public static Body LocalBody()
        {
            if (PlayerCamera.main == null)
                return null;

            return PlayerCamera.main.body;
        }

        public static bool LocalPlayerIsDrugTester()
        {
            return IsDrugTester(LocalBody());
        }

        public static float ScaleOverdoseThreshold(Body body, float vanilla)
        {
            return IsDrugTester(body) ? vanilla * 2f : vanilla;
        }

        public static IEnumerable<CodeInstruction> ScaleFloatConstants(
            IEnumerable<CodeInstruction> instructions,
            FieldInfo bodyField,
            params float[] values)
        {
            MethodInfo scale = AccessTools.Method(typeof(PlayerClasses), nameof(ScaleOverdoseThreshold));

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float value && Contains(values, value))
                {
                    CodeInstruction loadThis = new CodeInstruction(OpCodes.Ldarg_0);
                    loadThis.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();

                    yield return loadThis;
                    yield return new CodeInstruction(OpCodes.Ldfld, bodyField);
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call, scale);
                    continue;
                }

                yield return instruction;
            }
        }

        private static bool Contains(float[] values, float value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                    return true;
            }

            return false;
        }
    }
}
