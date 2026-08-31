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
        private const float CannibalStaminaLossScale = 1.5f;
        private const float CannibalStaminaRegenScale = 2f;
        private const float CannibalStaminaStillRegenScale = 2f;
        private const float CannibalStaminaCrouchRegenScale = 1.5f;
        private const float CannibalStaminaBonusDelay = 5f;
        private const float CannibalAnimalFleshMood = 3f;
        private const float CannibalYellowFleshMood = 6f;
        private const float CannibalMeatHungerScale = 1.2f;
        private const float CannibalPlantHungerScale = 0.75f;
        private const float CannibalCorpseHappiness = 1.75f;
        private const float CannibalYellowDisappointChance = 0.02f;
        private const float CannibalExpieReputation = -25f;
        private const float CannibalMilkyReputation = -35f;
        private const float CannibalDuneReputation = 20f;
        private static readonly string[] CannibalFleshLines =
        {
            "Yummers!",
            "Nice and Juicy...",
            "Why doesn't everybody else like this?!?"
        };
        private const string CannibalYellowDisappointLine = "I feel...  Dissapointed...";
        private static readonly string[] CannibalCorpseLines =
        {
            "Food...",
            "It's time to eat..."
        };
        private const string CannibalCorpseShoutLine = "FOOD!!!";
        private const float CannibalTiredDamageDivisor = 3f;
        private const float CannibalCombatDamageThreshold = -1f;
        private const float VanillaPainCap = 100f;
        private const float NamelessOverfullFillScale = 0.4f;
        private const float NamelessVenomGainScale = 1.75f;
        private const float FullnessThreshold = 100f;
        private static bool _fleshEatApplied;
        private static bool _blockFleshVomit;
        private static float _fleshEatSickness = -1f;

        public static void AssignLocalClassIfNeeded(Body body)
        {
            if (body == null)
                return;

            if (ClassSelection.IsMultiplayerSession())
            {
                if (ClassNetwork.TryGetClassForBody(body, out string networked))
                    ApplyClass(body, networked);

                return;
            }

            if (!IsLocalBody(body))
                return;

            ClassSelection.RefreshSelectedClassFromRunSettings();
            ApplyClass(body, Plugin.SelectedClassId);
        }

        public static void ApplyClass(Body body, string classId)
        {
            if (body == null)
                return;

            classId = NormalizeClassId(classId);
            PlayerClassStatus status = body.GetStatus<PlayerClassStatus>();
            string previous = NormalizeClassId(status.ClassId);

            if (status.Assigned && previous == classId)
            {
                if (!status.StatsApplied)
                {
                    ApplyStartingStats(body, classId);
                    status.StatsApplied = true;
                }

                return;
            }

            status.ClassId = classId;
            if (!status.StatsApplied || previous != classId)
            {
                ApplyStartingStats(body, classId);
                status.StatsApplied = true;
            }

            status.Assigned = true;
            if (classId == Plugin.CannibalClassId)
                EnableCorpseMining();
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

        public static string GetClassId(Body body)
        {
            if (body == null)
                return NormalizeClassId(Plugin.SelectedClassId);

            return NormalizeClassId(body.GetStatus<PlayerClassStatus>().ClassId);
        }

        public static string GetClassDisplayName(Body body)
        {
            return ClassSelection.DisplayName(GetClassId(body));
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
            if (body == null)
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
            ScaleCannibalTiredDamage(body, status);
            TickPendingVomit(body, status);
        }

        public static float ScaleHungerRate(Body body, float vanilla)
        {
            return IsCannibal(body) ? vanilla * CannibalHungerRateScale : vanilla;
        }

        public static bool IsSafeCannibalFlesh(string itemId)
        {
            return itemId == "animalflesh" || itemId == "experimentflesh";
        }

        public static void BeginCannibalEat(Body body, Item item)
        {
            _fleshEatApplied = false;
            _blockFleshVomit = false;
            _fleshEatSickness = -1f;
            if (body == null || !IsCannibal(body))
                return;

            _fleshEatSickness = body.sicknessAmount;
            if (item != null && IsSafeCannibalFlesh(item.id))
                _blockFleshVomit = true;
        }

        public static bool ShouldBlockCannibalVomit()
        {
            return _blockFleshVomit;
        }

        public static void EndCannibalEatVomitBlock()
        {
            _blockFleshVomit = false;
        }

        public static void ApplyCannibalFleshEat(Body body, string itemId)
        {
            if (body == null || !IsCannibal(body) || string.IsNullOrEmpty(itemId))
                return;

            if (itemId == "blobflesh")
            {
                if (_fleshEatApplied)
                    return;

                _fleshEatApplied = true;
                SayCannibalFleshLine(body, yellowFlesh: false);
                return;
            }

            if (!IsSafeCannibalFlesh(itemId))
                return;

            if (_fleshEatApplied)
                return;

            _fleshEatApplied = true;
            if (_fleshEatSickness >= 0f)
                body.sicknessAmount = _fleshEatSickness;
            else if (itemId == "animalflesh")
                body.sicknessAmount = Mathf.Max(0f, body.sicknessAmount - 4f);
            else
                body.sicknessAmount = Mathf.Max(0f, body.sicknessAmount - 16f);

            _fleshEatSickness = -1f;

            if (itemId == "animalflesh")
            {
                body.happiness += 0.75f + CannibalAnimalFleshMood;
                SayCannibalFleshLine(body, yellowFlesh: false);
                return;
            }

            body.happiness += 6f + CannibalYellowFleshMood;
            SayCannibalFleshLine(body, yellowFlesh: true);
        }

        public static void ScaleCannibalEatHunger(Body body, Item item, ref float hungerAmount)
        {
            if (body == null || item == null || !IsCannibal(body))
                return;

            if (ItemHasQuality(item, "meat"))
            {
                hungerAmount *= CannibalMeatHungerScale;
                return;
            }

            if (ItemHasQuality(item, "produce") || ItemHasQuality(item, "foliage") || ItemHasTag(item, "fruit") || item.id == "pickles")
                hungerAmount *= CannibalPlantHungerScale;
        }

        public static void ApplyCannibalTraderReputation(TraderScript trader)
        {
            if (trader == null || !IsCannibal(LocalBody()))
                return;

            if (trader.character == 1)
                trader.reputation += CannibalMilkyReputation;
            else if (trader.character == 2)
                trader.reputation += CannibalDuneReputation;
            else
                trader.reputation += CannibalExpieReputation;
        }

        public static bool TryHandleCannibalHug(TraderScript trader)
        {
            if (trader == null || !IsCannibal(LocalBody()))
                return false;

            Body body = LocalBody();
            if (trader.character == 2)
            {
                trader.talker.Talk(Locale.GetCharacter("traderhugsuccess", trader.character));
                Sound.Play("combine", trader.transform.position);
                if (!trader.didHug)
                {
                    trader.reputation += 5f;
                    body.happiness += 2.5f;
                    trader.didHug = true;
                    trader.UpdateScreen();
                }

                PlayerCamera.main.PlayUISound(PlayerCamera.UISoundType.MiniClick);
                return true;
            }

            trader.talker.Talk(Locale.GetCharacter("traderhugfail", trader.character));
            if (!trader.didHug)
            {
                trader.reputation -= 8f;
                body.happiness -= 2.5f;
                body.SetVelocity((body.transform.position - trader.torso.transform.position).normalized * 3f);
                body.Ragdoll();
                Sound.Play("BSSwing1", trader.transform.position);
                trader.UpdateScreen();
                trader.didHug = true;
            }

            if (trader.reputation < 30f)
                trader.hostility = 100f;

            PlayerCamera.main.PlayUISound(PlayerCamera.UISoundType.MiniClick);
            return true;
        }

        public static void ApplyCannibalCorpseMood(Body body, float sadnessRemoved)
        {
            if (body == null || !IsCannibal(body))
                return;

            body.happiness += sadnessRemoved + CannibalCorpseHappiness;
        }

        public static void SayCannibalCorpseLine(Body body)
        {
            if (body == null || body.talker == null || !IsCannibal(body))
                return;

            string line;
            if (CanShoutCorpseFood(body))
            {
                int pick = UnityEngine.Random.Range(0, CannibalCorpseLines.Length + 1);
                line = pick >= CannibalCorpseLines.Length ? CannibalCorpseShoutLine : CannibalCorpseLines[pick];
            }
            else
            {
                line = CannibalCorpseLines[UnityEngine.Random.Range(0, CannibalCorpseLines.Length)];
            }

            body.talker.Talk(line, null, true, true);
            body.eyeScareTime = 0f;
        }

        public static bool ShouldAllowCorpseMining()
        {
            if (IsCannibal(LocalBody()))
                return true;

            Body[] bodies = Object.FindObjectsOfType<Body>();
            for (int i = 0; i < bodies.Length; i++)
            {
                if (IsCannibal(bodies[i]))
                    return true;
            }

            return false;
        }

        public static void EnableCorpseMining()
        {
            if (!ShouldAllowCorpseMining())
                return;

            CorpseScript[] corpses = Object.FindObjectsOfType<CorpseScript>();
            for (int i = 0; i < corpses.Length; i++)
                AllowCorpseMining(corpses[i]);
        }

        public static void AllowCorpseMining(CorpseScript corpse)
        {
            if (corpse == null)
                return;

            BuildingEntity building = corpse.GetComponent<BuildingEntity>();
            if (building != null)
                building.cantHit = false;
        }

        private static bool CanShoutCorpseFood(Body body)
        {
            return body.happiness < 5f || body.hunger < 50f || body.brainHealth < 90f;
        }

        private static void ScaleCannibalTiredDamage(Body body, CannibalStatus status)
        {
            if (body.limbs == null)
                return;

            if (status.LastMuscleHealth == null || status.LastMuscleHealth.Length != body.limbs.Length
                || status.LastSkinHealth == null || status.LastSkinHealth.Length != body.limbs.Length)
            {
                status.LastMuscleHealth = new float[body.limbs.Length];
                status.LastSkinHealth = new float[body.limbs.Length];
                for (int i = 0; i < body.limbs.Length; i++)
                {
                    Limb limb = body.limbs[i];
                    if (limb == null)
                        continue;

                    status.LastMuscleHealth[i] = limb.muscleHealth;
                    status.LastSkinHealth[i] = limb.skinHealth;
                }

                return;
            }

            float keep = CannibalDamageKeep(body);
            for (int i = 0; i < body.limbs.Length; i++)
            {
                Limb limb = body.limbs[i];
                if (limb == null)
                    continue;

                float muscleDelta = limb.muscleHealth - status.LastMuscleHealth[i];
                if (muscleDelta < CannibalCombatDamageThreshold)
                    limb.muscleHealth = status.LastMuscleHealth[i] + muscleDelta * keep;

                float skinDelta = limb.skinHealth - status.LastSkinHealth[i];
                if (skinDelta < CannibalCombatDamageThreshold)
                    limb.skinHealth = status.LastSkinHealth[i] + skinDelta * keep;

                status.LastMuscleHealth[i] = limb.muscleHealth;
                status.LastSkinHealth[i] = limb.skinHealth;
            }
        }

        private static float CannibalDamageKeep(Body body)
        {
            float tiredness = 1f - Mathf.Clamp01(body.energy * 0.01f);
            return 1f / (1f + (CannibalTiredDamageDivisor - 1f) * tiredness);
        }

        public static void UndoCannibalBreakCorpsePenalty(Body body)
        {
            if (body == null || !IsCannibal(body))
                return;

            body.happiness += 5f;
            body.eyeScareTime = 0f;
            SayCannibalCorpseLine(body);
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

            if (status.StaminaBonusCooldown > 0f)
                status.StaminaBonusCooldown = Mathf.Max(0f, status.StaminaBonusCooldown - Time.deltaTime);

            float delta = body.stamina - status.LastStamina;
            if (delta != 0f)
            {
                if (delta < 0f)
                    status.StaminaBonusCooldown = CannibalStaminaBonusDelay;

                float scale = delta < 0f ? CannibalStaminaLossScale : GetCannibalStaminaRegenScale(body, status);
                body.stamina = Mathf.Clamp(status.LastStamina + delta * scale, 0f, 100f);
            }

            status.LastStamina = body.stamina;
        }

        private static float GetCannibalStaminaRegenScale(Body body, CannibalStatus status)
        {
            float scale = CannibalStaminaRegenScale;
            if (status.StaminaBonusCooldown <= 0f)
            {
                if (IsStandingStill(body))
                    scale *= CannibalStaminaStillRegenScale;
                if (body.crouching || body.crouchAmount > 0.5f)
                    scale *= CannibalStaminaCrouchRegenScale;
            }

            return scale;
        }

        private static bool IsStandingStill(Body body)
        {
            if (!body.standing || body.exercising || body.currentClimbable)
                return false;

            if (Mathf.Abs(body.moveDir.x) >= 0.1f)
                return false;

            return body.rb == null || body.rb.velocity.magnitude < 1f;
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

        private static void SayCannibalFleshLine(Body body, bool yellowFlesh)
        {
            if (body == null || body.talker == null)
                return;

            if (yellowFlesh && UnityEngine.Random.value < CannibalYellowDisappointChance)
            {
                body.talker.Talk(CannibalYellowDisappointLine, null, true, true);
                body.GetStatus<CannibalStatus>().PendingVomitAt = Time.time + 4f;
                return;
            }

            body.talker.Talk(CannibalFleshLines[UnityEngine.Random.Range(0, CannibalFleshLines.Length)], null, true, true);
        }

        private static void TickPendingVomit(Body body, CannibalStatus status)
        {
            if (status.PendingVomitAt <= 0f || Time.time < status.PendingVomitAt)
                return;

            status.PendingVomitAt = 0f;
            if (body.vomiter != null)
                body.vomiter.Vomit();
        }

        private static bool ItemHasQuality(Item item, string qualityId)
        {
            if (item == null || item.Stats == null || item.Stats.qualities == null)
                return false;

            for (int i = 0; i < item.Stats.qualities.Count; i++)
            {
                CraftingQuality quality = item.Stats.qualities[i];
                if (quality != null && quality.id == qualityId)
                    return true;
            }

            return false;
        }

        private static bool ItemHasTag(Item item, string tag)
        {
            return item != null && item.Stats != null && item.Stats.HasTag(tag);
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
