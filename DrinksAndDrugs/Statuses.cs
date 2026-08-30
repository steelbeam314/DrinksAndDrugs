using System;
using System.Collections.Generic;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

namespace DrinksAndDrugs
{
    [StatusOptions(Key = "modnamespace.deathjuice", SaveEnabled = true)]
    public sealed class DeathJuiceStatus : BodyStatus
    {
        public const float WaitDurationSeconds = 30f;
        public const float CoolTargetCelsius = 29f;

        /// <summary>True while cooling toward the target temperature.</summary>
        public bool CoolingActive;

        /// <summary>True once the delayed fever has started.</summary>
        public bool FeverActive;

        /// <summary>Seconds elapsed in the current cooling phase.</summary>
        public float CoolingElapsed;

        /// <summary>Seconds elapsed in the delayed fever phase.</summary>
        public float FeverElapsed;

        /// <summary>Unused milliliters toward the next full 100 mL dose.</summary>
        public float AbsorbedMl;

        /// <summary>Body temperature when this dose was taken.</summary>
        public float CoolingStartTemperature;

        /// <summary>Temperature to reach by the end of the wait (usually 29C).</summary>
        public float CoolingTargetTemperature;
    }

    [StatusOptions(Key = "modnamespace.brainfuck", SaveEnabled = true)]
    public sealed class BrainfuckStatus : BodyStatus
    {
        public const float DrainDurationSeconds = 10f;

        public bool Draining;
        public float Elapsed;
        public float StartBrainHealth;
        public float TargetBrainHealth;
        public float AbsorbedMl;

        public void ApplyToBody(Body body)
        {
            if (!Draining || body == null)
                return;

            float t = Mathf.Clamp01(Elapsed / DrainDurationSeconds);
            body.brainHealth = Mathf.Lerp(StartBrainHealth, TargetBrainHealth, t);
            body.happiness = Mathf.Min(body.happiness, -20f);

            if (t < 1f)
                return;

            Draining = false;
            body.brainHealth = TargetBrainHealth;
        }

        public void Tick(Body body)
        {
            if (!Draining || body == null)
                return;

            Elapsed += Time.deltaTime;
            ApplyToBody(body);
        }
    }

    [StatusOptions(Key = "modnamespace.stimfluid", SaveEnabled = true)]
    public sealed class StimFluidStatus : BodyStatus
    {
        public float AbsorbedMl;
    }

    [StatusOptions(Key = "modnamespace.classselection", SaveEnabled = true)]
    public sealed class PlayerClassStatus : BodyStatus
    {
        // Stable class ID chosen from the pre-run screen (e.g. "survivor").
        public string ClassId = Plugin.DefaultClassId;

        // Prevent re-applying the class every frame.
        public bool Assigned;

        // Starting STR/RES/INT modifiers applied once per body.
        public bool StatsApplied;

        // Happiness currently added by Failure pickle-jar mood.
        public float PickleMoodApplied;
    }

    [StatusOptions(Key = "modnamespace.nameless", SaveEnabled = true)]
    public sealed class NamelessStatus : BodyStatus
    {
        public float LastVenomCurrent;
        public float LastAveragePain;
        public float[] LastLimbPain;
        public float ShakePainBackup;
    }

    [StatusOptions(Key = "modnamespace.cannibal", SaveEnabled = true)]
    public sealed class CannibalStatus : BodyStatus
    {
        public float LastAveragePain;
        public float[] LastLimbPain;
        public float ShakePainBackup;
        public float LastStamina;
        public float StaminaBonusCooldown;
        public bool StaminaInitialized;
        public bool PainInitialized;
        public float PendingVomitAt;
        public float[] LastMuscleHealth;
        public float[] LastSkinHealth;
    }

    [StatusOptions(Key = "modnamespace.peanutallergy", SaveEnabled = true)]
    public sealed class PeanutAllergyStatus : BodyStatus
    {
        public const float DurationSeconds = 75f;
        public const float RampSeconds = 6f;

        public bool Active;
        public float Elapsed;
        public float Intensity;
        public bool HasVomited;

        public void Trigger(float dose)
        {
            bool alreadyReacting = Active && Elapsed > 0f && Elapsed < DurationSeconds;
            Active = true;
            Intensity = Mathf.Clamp(Intensity + Mathf.Max(0.45f, dose), 0.45f, 1.75f);
            if (!alreadyReacting)
                Elapsed = 0f;
            else if (Elapsed > DurationSeconds - 15f)
                Elapsed = RampSeconds;
        }

        public void Tick(Body body)
        {
            if (!Active || body == null)
                return;

            Elapsed += Time.deltaTime;

            MoodleRegistry.AddMoodle(
                intensity: 3,
                icon: MoodleIcons.Blank,
                name: "Anaphylaxis",
                description: "Peanut allergy. Your airway is closing.",
                key: "peanut.anaphylaxis");

            if (!HasVomited && Elapsed >= 0.35f && body.vomiter != null)
            {
                HasVomited = true;
                body.vomiter.Vomit();
            }

            if (Elapsed < DurationSeconds)
                return;

            Active = false;
            Intensity = 0f;
            Elapsed = 0f;
            HasVomited = false;
        }

        public void ApplyToBody(Body body)
        {
            if (!Active || body == null)
                return;

            float ramp = Mathf.Clamp01(Elapsed / RampSeconds);
            float fade = 1f;
            if (Elapsed > DurationSeconds - 15f)
                fade = Mathf.Clamp01((DurationSeconds - Elapsed) / 15f);

            // One bite is a full crisis; extra peanut butter pushes vitals a little further.
            float strength = Mathf.Clamp01(0.9f + (Intensity - 0.45f) * 0.15f);
            float t = ramp * fade * strength;

            body.sicknessAmount = Mathf.Max(body.sicknessAmount, 100f * t);
            body.shock = Mathf.Max(body.shock, 90f * t);
            body.bloodOxygen = Mathf.Min(body.bloodOxygen, Mathf.Lerp(100f, 38f, t));
            body.respiratoryRate = Mathf.Min(body.respiratoryRate, Mathf.Lerp(100f, 28f, t));
            body.bloodPressure = Mathf.Min(body.bloodPressure, Mathf.Lerp(120f, 48f, t));
            body.consciousness = Mathf.Min(body.consciousness, Mathf.Lerp(100f, 18f, t));
            body.happiness = Mathf.Min(body.happiness, Mathf.Lerp(0f, -40f, t));
        }
    }

    [StatusOptions(Key = "modnamespace.axyltallisal", SaveEnabled = true)]
    public sealed class AxyltallisalStatus : BodyStatus
    {
        public const float DoseMilliliters = 100f;
        public const float OverdoseMilliliters = 105f;
        public const float KnockoutSeconds = 90f;
        public const float SamePlungeGraceSeconds = 1.25f;
        public const float WakeHappiness = 18f;
        public const float WakeOpiateAmount = 70f;

        public bool KnockedOut;
        public bool Dying;
        public bool Fatal;
        public bool Resolved;
        public float Elapsed;
        public float AbsorbedMl;

        public static void NoteForeignDrug(Body body)
        {
            if (body == null)
                return;

            AxyltallisalStatus status = body.GetStatus<AxyltallisalStatus>();
            if (status.KnockedOut)
                status.Fatal = true;
        }

        public static void NoteForeignDrugsInContainer(Body body, WaterContainerItem container)
        {
            if (body == null || container == null || container.stack == null)
                return;

            AxyltallisalStatus status = body.GetStatus<AxyltallisalStatus>();
            if (!status.KnockedOut)
                return;

            for (int i = 0; i < container.stack.Count; i++)
            {
                LiquidStack stack = container.stack[i];
                if (stack == null || stack.amount <= 0f)
                    continue;

                if (IsForeignDrugLiquid(stack.liquidId))
                {
                    status.Fatal = true;
                    return;
                }
            }
        }

        private static bool IsForeignDrugLiquid(string liquidId)
        {
            return !string.IsNullOrEmpty(liquidId)
                && !string.Equals(liquidId, "axyltallisal", StringComparison.OrdinalIgnoreCase)
                && ForeignDrugLiquidIds.Contains(liquidId);
        }

        private static readonly HashSet<string> ForeignDrugLiquidIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "morphine", "opium", "painkillers", "heroin", "fentanyl",
            "naloxone", "naltrexone",
            "highgradestimulant", "midgradestimulant", "lowgradestimulant",
            "sleepingpills", "antidepressants", "chloroform", "alcohol",
            "mindwipe", "braingrow", "epinephrine", "oxyline", "lrdserum",
            "distilledtonic", "deathjuice", "stimfluid", "brainfuck"
        };

        public static bool HasOtherDrugs(Body body)
        {
            if (body == null)
                return false;

            Painkillers opiates = body.GetComponent<Painkillers>();
            if (opiates != null && opiates.opiateAmount > 0.5f)
                return true;

            SleepingPills sleepingPills = body.GetComponent<SleepingPills>();
            if (sleepingPills != null && sleepingPills.amount > 0.5f)
                return true;

            Antidepressants antidepressants = body.GetComponent<Antidepressants>();
            if (antidepressants != null && (antidepressants.amount > 0.5f || antidepressants.currentAmount > 0.5f))
                return true;

            if (body.onHardStimulants || body.stimulantMultiplier > 1f)
                return true;

            DeathJuiceStatus deathJuice = body.GetStatus<DeathJuiceStatus>();
            if (deathJuice.CoolingActive || deathJuice.FeverActive)
                return true;

            if (body.GetStatus<BrainfuckStatus>().Draining)
                return true;

            return false;
        }

        public static void KillByCardiacArrest(Body body)
        {
            if (body == null)
                return;

            body.brainHealth = 0f;
            body.heartRate = 0f;
            body.consciousness = 0f;
            body.bloodOxygen = 0f;
            body.TryStartFibrillation(true);
        }

        public void Tick(Body body)
        {
            if (body == null)
                return;

            if (Dying)
            {
                KillByCardiacArrest(body);
                return;
            }

            if (!KnockedOut)
                return;

            if (HasOtherDrugs(body))
                Fatal = true;

            Elapsed += Time.deltaTime;

            MoodleRegistry.AddMoodle(
                intensity: 3,
                icon: MoodleIcons.Blank,
                name: "Axyltallisal",
                description: "You are unconscious. Mixing this with other drugs is fatal.",
                key: "axyltallisal.knockout");

            if (Elapsed < KnockoutSeconds)
                return;

            KnockedOut = false;
            Resolved = true;
            AbsorbedMl = 0f;

            if (Fatal || UnityEngine.Random.value < 0.2f)
            {
                Dying = true;
                KillByCardiacArrest(body);
                return;
            }

            Wake(body);
        }

        public void ApplyToBody(Body body)
        {
            if (body == null)
                return;

            if (Dying)
            {
                KillByCardiacArrest(body);
                return;
            }

            if (KnockedOut)
            {
                body.consciousness = 0f;
                body.sleeping = true;
            }
        }

        public void BeginKnockout(Body body)
        {
            KnockedOut = true;
            Elapsed = 0f;
            Resolved = false;
            if (HasOtherDrugs(body))
                Fatal = true;
            ApplyToBody(body);
        }

        private void Wake(Body body)
        {
            Plugin.HealPhysicalInjuries(body);
            body.happiness += WakeHappiness;
            body.WakeUp();

            Painkillers opiates = body.GetComponent<Painkillers>();
            if (opiates == null)
                opiates = body.gameObject.AddComponent<Painkillers>();
            opiates.opiateAmount += WakeOpiateAmount;
        }
    }
}
