using Arreo.Rope;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class LassoValidatorTests
    {
        private const float Radius = 1.4f;
        private const float Margin = 0.8f;
        private static readonly Vector3 Landing = new Vector3(10f, 0f, 0f);

        [Test]
        public void ClaimedCow_InReachWhereTheClientSawIt_IsCaughtEvenIfItMovedSince()
        {
            // On the host the cow already ran 3 m on, but the client saw it under the loop.
            var heads = new[] { new Vector3(13f, 1.1f, 0f) };

            int caught = LassoValidator.ResolveCatch(Landing, Radius, Margin, claimed: 0,
                hasClaimedView: true, claimedHeadAsSeen: new Vector3(10.5f, 1.1f, 0f), heads);

            Assert.That(caught, Is.EqualTo(0));
        }

        [Test]
        public void ClaimedCow_OutOfReachBothWays_IsNotCaught()
        {
            var heads = new[] { new Vector3(14f, 1.1f, 0f) };

            int caught = LassoValidator.ResolveCatch(Landing, Radius, Margin, 0, true, new Vector3(13f, 1.1f, 0f), heads);

            Assert.That(caught, Is.EqualTo(-1));
        }

        [Test]
        public void ClaimedCow_WithoutRewind_IsCaughtWithinTheExtraMargin()
        {
            var heads = new[] { new Vector3(12f, 1.1f, 0f) };

            int caught = LassoValidator.ResolveCatch(Landing, Radius, Margin, 0, hasClaimedView: false, Vector3.zero, heads);

            Assert.That(caught, Is.EqualTo(0));
        }

        [Test]
        public void NoClaim_CatchesTheNearestHeadUnderTheLoop()
        {
            var heads = new[] { new Vector3(30f, 1.1f, 0f), new Vector3(10.4f, 1.1f, 0.3f) };

            int caught = LassoValidator.ResolveCatch(Landing, Radius, Margin, claimed: -1, false, Vector3.zero, heads);

            Assert.That(caught, Is.EqualTo(1));
        }

        [Test]
        public void ClaimNotInTheHerd_FallsBackToWhatIsUnderTheLoop()
        {
            var heads = new[] { new Vector3(10.2f, 1.1f, 0f) };

            int caught = LassoValidator.ResolveCatch(Landing, Radius, Margin, claimed: 7, true, Landing, heads);

            Assert.That(caught, Is.EqualTo(0));
        }

        [TestCase(0.12f, 0.12f)]
        [TestCase(-0.5f, 0f)]
        [TestCase(2f, 0.4f)]
        public void RewindSeconds_IsClampedBetweenZeroAndTheMaximum(float lag, float expected)
        {
            Assert.That(LassoValidator.RewindSeconds(serverNow: 10f, clientViewTime: 10f - lag, maxRewind: 0.4f),
                Is.EqualTo(expected).Within(0.0001f));
        }
    }
}
