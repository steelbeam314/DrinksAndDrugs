using CUCoreLib.Data;
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

        public void Tick(Body body)
        {
            if (!Draining || body == null)
                return;

            Elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(Elapsed / DrainDurationSeconds);
            body.brainHealth = Mathf.Lerp(StartBrainHealth, TargetBrainHealth, t);
            body.happiness = Mathf.Min(body.happiness, -20f);

            if (t < 1f)
                return;

            Draining = false;
            body.brainHealth = TargetBrainHealth;
        }
    }

    [StatusOptions(Key = "modnamespace.classselection", SaveEnabled = true)]
    public sealed class PlayerClassStatus : BodyStatus
    {
        // Stable class ID chosen from the pre-run screen (e.g. "scavenger").
        public string ClassId = Plugin.DefaultClassId;

        // Prevent re-applying the class every frame.
        public bool Assigned;
    }
}
