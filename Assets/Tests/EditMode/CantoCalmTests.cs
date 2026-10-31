using Arreo.Herd;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    /// <summary>The cabrestero's canto: which cows hear it, and how it calms them.</summary>
    public class CantoCalmTests
    {
        private static CantoCalm CreateCalm() => new CantoCalm(radius: 30f, fullRadius: 15f,
            fearReduction: 0.75f, cohesionBoost: 1.5f, riseSeconds: 2f, fadeSeconds: 4f,
            stampedeShortening: 0.5f, regroupRadiusBoost: 1.5f);

        private static RiderSample Singer(float x) => new RiderSample(new Vector3(x, 0f, 0f), 4f, singing: true);
        private static RiderSample Silent(float x) => new RiderSample(new Vector3(x, 0f, 0f), 4f, singing: false);

        [Test]
        public void Hearing_FullUpToTheFullRadius()
        {
            CantoCalm calm = CreateCalm();

            Assert.That(calm.Hearing(0f), Is.EqualTo(1f));
            Assert.That(calm.Hearing(15f), Is.EqualTo(1f));
        }

        [Test]
        public void Hearing_FadesBetweenTheFullRadiusAndTheRadius()
        {
            Assert.That(CreateCalm().Hearing(22.5f), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Hearing_NoneAtTheRadiusOrBeyond()
        {
            CantoCalm calm = CreateCalm();

            Assert.That(calm.Hearing(30f), Is.EqualTo(0f));
            Assert.That(calm.Hearing(45f), Is.EqualTo(0f));
        }

        [Test]
        public void Hearing_OnlyRidersWhoSingCount()
        {
            Assert.That(CreateCalm().Hearing(Vector3.zero, new[] { Silent(5f) }), Is.EqualTo(0f));
            Assert.That(CreateCalm().Hearing(Vector3.zero, new[] { Silent(5f), Singer(10f) }), Is.EqualTo(1f));
        }

        [Test]
        public void Hearing_TheClosestSingerCounts_HeightDoesNot()
        {
            CantoCalm calm = CreateCalm();

            Assert.That(calm.Hearing(Vector3.zero, new[] { Singer(40f), Singer(-22.5f) }), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(calm.Hearing(new Vector3(0f, 50f, 0f), new[] { Singer(10f) }), Is.EqualTo(1f));
        }

        [Test]
        public void Hearing_SingerOutOfRange_CalmsNobody()
        {
            Assert.That(CreateCalm().Hearing(Vector3.zero, new[] { Singer(31f) }), Is.EqualTo(0f));
        }

        [Test]
        public void Step_CalmRisesOverTheRiseTime()
        {
            CantoCalm calm = CreateCalm();

            Assert.That(calm.Step(0f, hearing: 1f, deltaTime: 1f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(calm.Step(0.5f, hearing: 1f, deltaTime: 5f), Is.EqualTo(1f));
        }

        [Test]
        public void Step_CalmNeverGoesPastWhatTheCowHears()
        {
            Assert.That(CreateCalm().Step(0.4f, hearing: 0.5f, deltaTime: 3f), Is.EqualTo(0.5f));
        }

        [Test]
        public void Step_CalmLingersAndFadesOverTheFadeTime()
        {
            CantoCalm calm = CreateCalm();

            Assert.That(calm.Step(1f, hearing: 0f, deltaTime: 1f), Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(calm.Step(0.75f, hearing: 0f, deltaTime: 10f), Is.EqualTo(0f));
        }

        [Test]
        public void FearScale_FullCalmTakesTheFearReductionAway()
        {
            CantoCalm calm = CreateCalm();

            Assert.That(calm.FearScale(0f), Is.EqualTo(1f));
            Assert.That(calm.FearScale(1f), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(calm.FearScale(0.5f), Is.EqualTo(0.625f).Within(0.0001f));
        }

        [Test]
        public void StampedeTimeScale_FullCalmLivesThePanicTwiceAsFast()
        {
            CantoCalm calm = CreateCalm();

            Assert.That(calm.StampedeTimeScale(0f), Is.EqualTo(1f));
            Assert.That(calm.StampedeTimeScale(1f), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(calm.StampedeTimeScale(0.5f), Is.EqualTo(1f / 0.75f).Within(0.0001f));
        }

        [Test]
        public void Barajustada_ACowThatFullyHearsTheCantoPanicsHalfAsLong()
        {
            CantoCalm calm = CreateCalm();
            var stampede = new Stampede(holdSeconds: 5f, fadeSeconds: 6f);
            stampede.Trigger(Vector3.zero);

            Assert.That(SecondsOfPanic(stampede, calm.StampedeTimeScale(0f)), Is.EqualTo(11f).Within(0.05f));
            Assert.That(SecondsOfPanic(stampede, calm.StampedeTimeScale(1f)), Is.EqualTo(5.5f).Within(0.05f));
        }

        [Test]
        public void CohesionRadiusScale_FullCalmLooksForHerdMatesFarther()
        {
            CantoCalm calm = CreateCalm();

            Assert.That(calm.CohesionRadiusScale(0f), Is.EqualTo(1f));
            Assert.That(calm.CohesionRadiusScale(1f), Is.EqualTo(2.5f).Within(0.0001f));
        }

        // A cow's own stampede clock runs timeScale times as fast; how long until it's calm again.
        private static float SecondsOfPanic(Stampede stampede, float timeScale)
        {
            const float step = 0.01f;
            float age = 0f, seconds = 0f;
            while (stampede.IntensityAt(age) > 0f && seconds < 60f)
            {
                age += step * timeScale;
                seconds += step;
            }
            return seconds;
        }

        [Test]
        public void CohesionScale_FullCalmAddsTheCohesionBoost()
        {
            CantoCalm calm = CreateCalm();

            Assert.That(calm.CohesionScale(0f), Is.EqualTo(1f));
            Assert.That(calm.CohesionScale(1f), Is.EqualTo(2.5f).Within(0.0001f));
        }
    }
}
