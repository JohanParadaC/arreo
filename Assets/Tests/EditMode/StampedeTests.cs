using Arreo.Herd;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class StampedeTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void IntensityAt_FollowsTheHoldAndFadeForAnyCowsClock()
        {
            var stampede = new Stampede(4f, 4f);
            stampede.Trigger(Vector3.zero);

            Assert.That(stampede.IntensityAt(3f), Is.EqualTo(1f));
            Assert.That(stampede.IntensityAt(6f), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(stampede.IntensityAt(8f), Is.EqualTo(0f));
            Assert.That(stampede.IntensityAt(20f), Is.EqualTo(0f));
        }

        [Test]
        public void IntensityAt_NeverTriggered_IsZero()
        {
            Assert.That(new Stampede(4f, 4f).IntensityAt(1f), Is.EqualTo(0f));
        }

        [Test]
        public void NotTriggered_IsInactive()
        {
            var stampede = new Stampede(holdSeconds: 4f, fadeSeconds: 4f);

            Assert.That(stampede.IsActive, Is.False);
            Assert.That(stampede.Intensity, Is.EqualTo(0f));
        }

        [Test]
        public void Triggered_IsFullStrengthDuringHold()
        {
            var stampede = new Stampede(4f, 4f);
            stampede.Trigger(new Vector3(1f, 0f, 2f));

            stampede.Tick(3f);

            Assert.That(stampede.Intensity, Is.EqualTo(1f));
            Assert.That(stampede.Origin, Is.EqualTo(new Vector3(1f, 0f, 2f)));
        }

        [Test]
        public void AfterHold_FadesOutSoTheHerdCanBeGathered()
        {
            var stampede = new Stampede(4f, 4f);
            stampede.Trigger(Vector3.zero);

            stampede.Tick(6f);
            Assert.That(stampede.Intensity, Is.EqualTo(0.5f).Within(Tolerance));

            stampede.Tick(2.5f);
            Assert.That(stampede.Intensity, Is.EqualTo(0f));
            Assert.That(stampede.IsActive, Is.False);
        }

        [Test]
        public void TriggerAgain_RestartsAtFullStrength()
        {
            var stampede = new Stampede(4f, 4f);
            stampede.Trigger(Vector3.zero);
            stampede.Tick(7f);

            stampede.Trigger(Vector3.one);

            Assert.That(stampede.Intensity, Is.EqualTo(1f));
        }
    }
}
