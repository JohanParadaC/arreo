using Arreo.Player;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class AimBlendTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void New_IsNotAiming()
        {
            var aim = new AimBlend(blendSeconds: 0.2f);

            Assert.That(aim.IsAiming, Is.False);
            Assert.That(aim.Weight, Is.EqualTo(0f));
        }

        [Test]
        public void Holding_BlendsInOverTheBlendTime()
        {
            var aim = new AimBlend(0.2f);

            aim.Tick(0.1f, held: true);
            Assert.That(aim.IsAiming, Is.True);
            Assert.That(aim.Weight, Is.EqualTo(0.5f).Within(Tolerance));

            aim.Tick(0.1f, held: true);
            Assert.That(aim.Weight, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void HoldingLonger_NeverGoesPastFull()
        {
            var aim = new AimBlend(0.2f);

            aim.Tick(5f, held: true);

            Assert.That(aim.Weight, Is.EqualTo(1f));
        }

        [Test]
        public void Releasing_BlendsBackOut()
        {
            var aim = new AimBlend(0.2f);
            aim.Tick(0.2f, held: true);

            aim.Tick(0.05f, held: false);

            Assert.That(aim.IsAiming, Is.False);
            Assert.That(aim.Weight, Is.EqualTo(0.75f).Within(Tolerance));
        }

        [Test]
        public void Eased_StartsAndEndsSlowly()
        {
            var aim = new AimBlend(0.2f);

            aim.Tick(0.05f, held: true);
            Assert.That(aim.Eased, Is.LessThan(aim.Weight));

            aim.Tick(0.05f, held: true);
            Assert.That(aim.Eased, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void NoBlendTime_SnapsInstantly()
        {
            var aim = new AimBlend(0f);

            aim.Tick(0.01f, held: true);

            Assert.That(aim.Weight, Is.EqualTo(1f));
        }
    }
}
