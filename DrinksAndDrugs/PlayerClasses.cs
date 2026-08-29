using CUCoreLib.Helpers;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace DrinksAndDrugs
{
    internal static class PlayerClasses
    {
        private const float PickleHeldHappiness = 1.25f;
        private const float EmptyPickleJarHappiness = -1.25f;
        private const float NamelessPainRiseRate = 0.4f;
        private const float NamelessPainShakeScale = 0.5f;
        private const float CannibalPainRiseRate = 1.4f;
        private const float CannibalPainMax = 125f;
        private const float CannibalPainShakeScale = 0.35f;
        private const float CannibalHungerRateScale = 1.2f;
        private const float CannibalStaminaScale = 2f;
        private const float CannibalAnimalFleshMood = 3f;
        private const float CannibalYellowFleshMood = 6f;
        private const float VanillaPainCap = 100f;
        private const float NamelessOverfullFillScale = 0.4f;
        private const float NamelessVenomGainScale = 1.75f;
        private const float FullnessThreshold = 100f;

        public static void AssignLocalClassIfNeeded(Body body)
        {
            if (body == null || !IsLocalBody(body))
                return;

            PlayerClassStatus status = body.GetStatus<PlayerClassStatus>();
            status.ClassId = NormalizeClassId(status.ClassId);

            if (status.Assigned)
            {
                if (!status.StatsApplied)
                {
                    ApplyStartingStats(body, status.ClassId);
                    status.StatsApplied = true;
                }

                return;
            }

            if (!ClassSelection.IsMultiplayerEnabled())
                ClassSelection.RefreshSelectedClassFromRunSettings();

            status.ClassId = NormalizeClassId(Plugin.SelectedClassId);
            ApplyStartingStats(body, status.ClassId);
            status.StatsApplied = true;
            status.Assigned = true;
        }

        public static string NormalizeClassId(string classId)
        {
            if (classId == "scavenger")
                return Plugin.SurvivorClassId;

            if (string.IsNullOrEmpty(classId))
                return Plugin.DefaultClassId;

            return classId;
        }

        public static bool IsDrugTester(Body body)
        {
            return body != null && body.GetStatus<PlayerClassStatus>().ClassId == Plugin.DrugTesterClassId;
        }

        public static bool IsFailure(Body body)
        {
            return body != null && body.GetStatus<PlayerClassStatus>().ClassId == Plugin.FailureClassId;
        }

        public static bool IsNameless(Body body)
        {
            return body != null && body.GetStatus<PlayerClassStatus>().ClassId == Plugin.NamelessClassId;
        }

        public static bool IsCannibal(Body body)
        {
            return body != null && body.GetStatus<PlayerClassStatus>().ClassId == Plugin.CannibalClassId;
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

        public static bool LocalPlayerIsFailure()
        {
            return IsFailure(LocalBody());
        }

        public static bool ShouldScrambleReadableText()
        {
            Body body = LocalBody();
            if (body != null)
                return IsFailure(body);

            return Plugin.SelectedClassId == Plugin.FailureClassId;
        }

        public static float ScaleOverdoseThreshold(Body body, float vanilla)
        {
            return IsDrugTester(body) ? vanilla * 2f : vanilla;
        }

        public static void TickFailureEffects(Body body)
        {
            if (body == null || !IsLocalBody(body))
                return;

            PlayerClassStatus status = body.GetStatus<PlayerClassStatus>();
            float pickleMood = 0f;
            if (IsFailure(body))
            {
                if (body.HoldingItem("picklejar"))
                    pickleMood += PickleHeldHappiness;

                if (HasItemInInventory(body, "picklejuicejar"))
                    pickleMood += EmptyPickleJarHappiness;
            }

            body.happiness += pickleMood - status.PickleMoodApplied;
            status.PickleMoodApplied = pickleMood;
        }

        public static string ScrambleReadableText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var rng = new System.Random(unchecked((int)text.GetHashCode()));
            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (i + 1 < text.Length && text[i] == '<' && text[i + 1] == '>')
                {
                    sb.Append("<>");
                    i += 2;
                    continue;
                }

                if (char.IsLetter(text[i]))
                {
                    int start = i;
                    while (i < text.Length && char.IsLetter(text[i]))
                        i++;

                    char[] word = new char[i - start];
                    text.CopyTo(start, word, 0, word.Length);
                    for (int n = word.Length - 1; n > 0; n--)
                    {
                        int k = rng.Next(n + 1);
                        char tmp = word[n];
                        word[n] = word[k];
                        word[k] = tmp;
                    }

                    sb.Append(word);
                    continue;
                }

                sb.Append(text[i]);
                i++;
            }

            return sb.ToString();
        }

        public static void ApplyNamelessSimulation(Body body)
        {
            if (body == null || !IsNameless(body))
                return;

            NamelessStatus status = body.GetStatus<NamelessStatus>();
            SlowPainRise(body, status);
            AmplifyVenomGain(body, status);
        }

        public static void ApplyCannibalSimulation(Body body)
        {
            if (body == null || !IsCannibal(body))
                return;

            CannibalStatus status = body.GetStatus<CannibalStatus>();
            ScaleCannibalPain(body, status);
            ScaleCannibalStamina(body, status);
        }

        public static float ScaleHungerRate(Body body, float vanilla)
        {
            return IsCannibal(body) ? vanilla * CannibalHungerRateScale : vanilla;
        }

        public static void ApplyCannibalFleshEat(Body body, string itemId)
        {
            if (body == null || !IsCannibal(body) || string.IsNullOrEmpty(itemId))
                return;

            if (itemId == "animalflesh")
            {
                body.sicknessAmount = Mathf.Max(0f, body.sicknessAmount - 4f);
                body.happiness += 0.75f + CannibalAnimalFleshMood;
                return;
            }

            if (itemId == "experimentflesh")
            {
                body.sicknessAmount = Mathf.Max(0f, body.sicknessAmount - 16f);
                body.happiness += 6f + CannibalYellowFleshMood;
            }
        }

        public static float CompressFullness(float before, float after)
        {
            return CompressAbove(before, after, FullnessThreshold, NamelessOverfullFillScale);
        }

        public static void BeginNamelessPainShake(Body body, out bool scaled)
        {
            scaled = false;
            if (body == null)
                return;

            float scale = GetPainShakeScale(body);
            if (scale >= 1f)
                return;

            if (IsNameless(body))
                body.GetStatus<NamelessStatus>().ShakePainBackup = body.averagePain;
            else if (IsCannibal(body))
                body.GetStatus<CannibalStatus>().ShakePainBackup = body.averagePain;
            else
                return;

            body.averagePain *= scale;
            scaled = true;
        }

        public static void EndNamelessPainShake(Body body, bool scaled)
        {
            if (!scaled || body == null)
                return;

            if (IsNameless(body))
                body.averagePain = body.GetStatus<NamelessStatus>().ShakePainBackup;
            else if (IsCannibal(body))
                body.averagePain = body.GetStatus<CannibalStatus>().ShakePainBackup;
        }

        private static float GetPainShakeScale(Body body)
        {
            if (IsNameless(body))
                return NamelessPainShakeScale;
            if (IsCannibal(body))
                return CannibalPainShakeScale;
            return 1f;
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

        private static void ApplyStartingStats(Body body, string classId)
        {
            if (body == null || body.skills == null)
                return;

            Skills skills = body.skills;
            if (classId == Plugin.FailureClassId)
            {
                skills.STR += 1;
                skills.RES += 2;
                skills.INT -= 1;
            }
            else if (classId == Plugin.NamelessClassId)
            {
                skills.STR -= 1;
                skills.RES -= 1;
                skills.INT += 3;
            }
            else if (classId == Plugin.CannibalClassId)
            {
                skills.STR += 1;
                skills.RES -= 1;
                skills.INT += 1;
            }
            else
            {
                return;
            }

            skills.UpdateExpBoundaries();
        }

        private static void ScaleCannibalPain(Body body, CannibalStatus status)
        {
            if (body.limbs != null)
            {
                if (status.LastLimbPain == null || status.LastLimbPain.Length != body.limbs.Length)
                {
                    status.LastLimbPain = new float[body.limbs.Length];
                    for (int i = 0; i < body.limbs.Length; i++)
                    {
                        if (body.limbs[i] != null)
                            status.LastLimbPain[i] = body.limbs[i].pain;
                    }
                }

                for (int i = 0; i < body.limbs.Length; i++)
                {
                    Limb limb = body.limbs[i];
                    if (limb == null)
                        continue;

                    limb.pain = NextCannibalPain(limb.pain, status.LastLimbPain[i]);
                    status.LastLimbPain[i] = limb.pain;
                }
            }

            if (!status.PainInitialized)
            {
                status.LastAveragePain = body.averagePain;
                status.PainInitialized = true;
            }
            else
            {
                body.averagePain = NextCannibalPain(body.averagePain, status.LastAveragePain);
                status.LastAveragePain = body.averagePain;
            }
        }

        private static float NextCannibalPain(float vanilla, float last)
        {
            float delta = vanilla - last;
            float pain;
            if (last > VanillaPainCap && vanilla >= VanillaPainCap - 0.5f && delta < 0f)
                pain = last + Time.deltaTime * 8f;
            else if (delta > 0f)
                pain = last + delta * CannibalPainRiseRate;
            else
                pain = vanilla;

            return Mathf.Clamp(pain, 0f, CannibalPainMax);
        }

        private static void ScaleCannibalStamina(Body body, CannibalStatus status)
        {
            if (!status.StaminaInitialized)
            {
                status.LastStamina = body.stamina;
                status.StaminaInitialized = true;
                return;
            }

            float delta = body.stamina - status.LastStamina;
            if (delta != 0f)
                body.stamina = Mathf.Clamp(status.LastStamina + delta * CannibalStaminaScale, 0f, 100f);

            status.LastStamina = body.stamina;
        }

        private static void SlowPainRise(Body body, NamelessStatus status)
        {
            if (body.limbs != null)
            {
                if (status.LastLimbPain == null || status.LastLimbPain.Length != body.limbs.Length)
                    status.LastLimbPain = new float[body.limbs.Length];

                for (int i = 0; i < body.limbs.Length; i++)
                {
                    Limb limb = body.limbs[i];
                    if (limb == null)
                        continue;

                    float last = status.LastLimbPain[i];
                    float vanilla = limb.pain;
                    float delta = vanilla - last;
                    if (delta > 0f)
                        limb.pain = last + delta * NamelessPainRiseRate;

                    status.LastLimbPain[i] = limb.pain;
                }
            }

            float averageDelta = body.averagePain - status.LastAveragePain;
            if (averageDelta > 0f)
                body.averagePain = status.LastAveragePain + averageDelta * NamelessPainRiseRate;
            status.LastAveragePain = body.averagePain;
        }

        private static void AmplifyVenomGain(Body body, NamelessStatus status)
        {
            float delta = body.venomCurrent - status.LastVenomCurrent;
            if (delta > 0f)
                body.venomCurrent = status.LastVenomCurrent + delta * NamelessVenomGainScale;
            status.LastVenomCurrent = body.venomCurrent;
        }

        private static float CompressAbove(float before, float after, float threshold, float excessScale)
        {
            if (after <= before || after <= threshold)
                return after;

            float added = after - before;
            if (before >= threshold)
                return before + added * excessScale;

            return threshold + (added - (threshold - before)) * excessScale;
        }

        private static bool HasItemInInventory(Body body, string itemId)
        {
            List<Item> items = body.GetAllItemsThorough();
            if (items == null)
                return false;

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item != null && item.id == itemId)
                    return true;
            }

            return false;
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
