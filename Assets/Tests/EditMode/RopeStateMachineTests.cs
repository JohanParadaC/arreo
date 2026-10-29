using Arreo.Rope;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class RopeStateMachineTests
    {
        private const float Taut = 300f;
        private const float Break = 3000f;

        private static RopeStateMachine CreateRope() => new RopeStateMachine(
            tautTension: Taut,
            breakTension: Break,
            breakSeconds: 2f,
            retractSeconds: 1f,
            brokenSeconds: 1.5f);

        private static RopeStateMachine CreateLassoedRope()
        {
            var rope = CreateRope();
            rope.Throw();
            rope.Land(caught: true);
            return rope;
        }

        [Test]
        public void NewRope_IsCoiled()
        {
            Assert.That(CreateRope().State, Is.EqualTo(RopeState.Coiled));
        }

        [Test]
        public void Throw_FromCoiled_StartsThrowing()
        {
            var rope = CreateRope();

            Assert.That(rope.Throw(), Is.True);
            Assert.That(rope.State, Is.EqualTo(RopeState.Throwing));
        }

        [Test]
        public void Throw_WhileAlreadyThrowing_IsRefused()
        {
            var rope = CreateRope();
            rope.Throw();

            Assert.That(rope.Throw(), Is.False);
        }

        [Test]
        public void Land_OnACow_Lassoes()
        {
            Assert.That(CreateLassoedRope().State, Is.EqualTo(RopeState.Lassoed));
        }

        [Test]
        public void Land_Missing_RetractsAndCoilsAfterRetractTime()
        {
            var rope = CreateRope();
            rope.Throw();

            rope.Land(caught: false);
            Assert.That(rope.State, Is.EqualTo(RopeState.Released));

            rope.Tick(0.5f, 0f);
            Assert.That(rope.State, Is.EqualTo(RopeState.Released));

            rope.Tick(0.6f, 0f);
            Assert.That(rope.State, Is.EqualTo(RopeState.Coiled));
        }

        [Test]
        public void Land_WhenNotThrowing_IsIgnored()
        {
            var rope = CreateRope();

            rope.Land(caught: true);

            Assert.That(rope.State, Is.EqualTo(RopeState.Coiled));
        }

        [Test]
        public void Tension_AboveTautThreshold_MakesItTaut_AndBackToLassoedWhenSlack()
        {
            var rope = CreateLassoedRope();

            rope.Tick(0.1f, Taut + 1f);
            Assert.That(rope.State, Is.EqualTo(RopeState.Taut));

            rope.Tick(0.1f, Taut - 1f);
            Assert.That(rope.State, Is.EqualTo(RopeState.Lassoed));
        }

        [Test]
        public void Tension_OverBreakLimitLongEnough_Snaps()
        {
            var rope = CreateLassoedRope();

            rope.Tick(1.5f, Break + 100f);
            Assert.That(rope.State, Is.EqualTo(RopeState.Taut));
            Assert.That(rope.BreakProgress, Is.EqualTo(0.75f).Within(0.0001f));

            rope.Tick(0.6f, Break + 100f);
            Assert.That(rope.State, Is.EqualTo(RopeState.Broken));
        }

        [Test]
        public void Tension_DippingBelowBreakLimit_ResetsTheBreakClock()
        {
            var rope = CreateLassoedRope();
            rope.Tick(1.5f, Break + 100f);

            rope.Tick(0.1f, Break - 100f);
            rope.Tick(1.5f, Break + 100f);

            Assert.That(rope.State, Is.EqualTo(RopeState.Taut));
        }

        [Test]
        public void Broken_CoilsAgainAfterBrokenTime()
        {
            var rope = CreateLassoedRope();
            rope.Tick(2.1f, Break + 100f);

            rope.Tick(1f, 0f);
            Assert.That(rope.State, Is.EqualTo(RopeState.Broken));

            rope.Tick(0.6f, 0f);
            Assert.That(rope.State, Is.EqualTo(RopeState.Coiled));
        }

        [Test]
        public void Release_WhileTaut_LetsGoAndRetracts()
        {
            var rope = CreateLassoedRope();
            rope.Tick(0.1f, Taut + 1f);

            rope.Release();

            Assert.That(rope.State, Is.EqualTo(RopeState.Released));
            Assert.That(rope.BreakProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Release_WhenCoiled_DoesNothing()
        {
            var rope = CreateRope();

            rope.Release();

            Assert.That(rope.State, Is.EqualTo(RopeState.Coiled));
        }

        [Test]
        public void Snapping_RemembersThePeakTensionOfTheStrainThatBrokeIt()
        {
            var rope = CreateLassoedRope();
            rope.Tick(0.5f, Break + 100f);
            rope.Tick(0.5f, Break + 2000f);

            rope.Tick(1.1f, Break + 500f);

            Assert.That(rope.State, Is.EqualTo(RopeState.Broken));
            Assert.That(rope.SnapTension, Is.EqualTo(Break + 2000f));
        }

        [Test]
        public void SnapTension_IgnoresEarlierStrainsThatDidNotBreakIt()
        {
            var rope = CreateLassoedRope();
            rope.Tick(1f, Break + 5000f);
            rope.Tick(0.1f, Break - 100f);

            rope.Tick(2.1f, Break + 200f);

            Assert.That(rope.SnapTension, Is.EqualTo(Break + 200f));
        }
    }
}
