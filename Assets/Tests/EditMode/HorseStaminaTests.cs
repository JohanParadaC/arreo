using System;
using Arreo.Horse;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class HorseStaminaTests
    {
        private const float Tolerance = 0.0001f;

        private static HorseStamina CreateStamina() => new HorseStamina(
            max: 100f,
            drainPerSecond: 10f,
            recoveryPerSecond: 20f,
            recoveryThreshold: 0.35f);

        [Test]
        public void NewStamina_StartsFull()
        {
            var stamina = CreateStamina();

            Assert.That(stamina.Current, Is.EqualTo(100f));
            Assert.That(stamina.Normalized, Is.EqualTo(1f));
            Assert.That(stamina.CanGallop, Is.True);
        }

        [Test]
        public void Tick_Galloping_Drains()
        {
            var stamina = CreateStamina();

            stamina.Tick(galloping: true, deltaTime: 2f);

            Assert.That(stamina.Current, Is.EqualTo(80f).Within(Tolerance));
        }

        [Test]
        public void Tick_AtWalk_Recovers()
        {
            var stamina = CreateStamina();
            stamina.Tick(galloping: true, deltaTime: 5f);

            stamina.Tick(galloping: false, deltaTime: 1f);

            Assert.That(stamina.Current, Is.EqualTo(70f).Within(Tolerance));
        }

        [Test]
        public void Tick_Recovering_NeverExceedsMax()
        {
            var stamina = CreateStamina();

            stamina.Tick(galloping: false, deltaTime: 10f);

            Assert.That(stamina.Current, Is.EqualTo(100f));
        }

        [Test]
        public void Tick_GallopingTooLong_EmptiesAndExhausts()
        {
            var stamina = CreateStamina();

            stamina.Tick(galloping: true, deltaTime: 30f);

            Assert.That(stamina.Current, Is.EqualTo(0f));
            Assert.That(stamina.IsExhausted, Is.True);
            Assert.That(stamina.CanGallop, Is.False);
        }

        [Test]
        public void Exhausted_CannotGallopUntilRecoveryThreshold()
        {
            var stamina = CreateStamina();
            stamina.Tick(galloping: true, deltaTime: 10f);

            stamina.Tick(galloping: false, deltaTime: 1.5f); // 30 of 100, below 35%
            Assert.That(stamina.CanGallop, Is.False);

            stamina.Tick(galloping: false, deltaTime: 0.5f); // 40 of 100
            Assert.That(stamina.CanGallop, Is.True);
        }

        [Test]
        public void Tick_NotEmpty_DoesNotExhaust()
        {
            var stamina = CreateStamina();

            stamina.Tick(galloping: true, deltaTime: 9f);

            Assert.That(stamina.IsExhausted, Is.False);
        }

        [Test]
        public void Constructor_NonPositiveMax_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseStamina(0f, 10f, 20f, 0.35f));
        }

        [TestCase(0f)]
        [TestCase(1.5f)]
        public void Constructor_ThresholdOutsideRange_Throws(float threshold)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseStamina(100f, 10f, 20f, threshold));
        }
    }
}
