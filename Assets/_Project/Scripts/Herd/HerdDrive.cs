using System;

namespace Arreo.Herd
{
    /// <summary>
    /// One drive (arreo) into a corral: counts cattle inside and times it from <see cref="Begin"/>
    /// until the whole herd is in. Once complete, the time stays frozen.
    /// </summary>
    public sealed class HerdDrive
    {
        public HerdDrive(int totalCattle)
        {
            if (totalCattle <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalCattle), totalCattle, "Must be at least one.");
            Total = totalCattle;
        }

        public int Total { get; }
        public int InCorral { get; private set; }
        public bool HasStarted { get; private set; }
        public bool IsComplete { get; private set; }
        public float ElapsedSeconds { get; private set; }

        public void Begin() => HasStarted = true;

        public void Tick(float deltaTime, int inCorral)
        {
            InCorral = inCorral;
            if (!HasStarted || IsComplete)
                return;

            ElapsedSeconds += deltaTime;
            if (inCorral >= Total)
                IsComplete = true;
        }
    }
}
