using Arreo.Net;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class ServerTimeEstimatorTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void SteadyDelay_EstimatesServerTimeFromTheLocalClock()
        {
            var clock = new ServerTimeEstimator(windowSize: 10);
            // Server is 100 s behind the local clock, packets take a steady 0.05 s.
            for (int i = 0; i < 5; i++)
                clock.Observe(serverTime: i * 0.1f, localReceiveTime: 100.05f + i * 0.1f);

            Assert.That(clock.ServerNow(localTime: 101f), Is.EqualTo(0.95f).Within(Tolerance));
        }

        [Test]
        public void JitteryPackets_TrustTheLeastDelayedOne()
        {
            var clock = new ServerTimeEstimator(10);
            clock.Observe(0.0f, 100.10f);
            clock.Observe(0.1f, 100.15f);
            clock.Observe(0.2f, 100.40f);

            Assert.That(clock.ServerNow(101f), Is.EqualTo(0.95f).Within(Tolerance));
        }

        [Test]
        public void PermanentlySlowerLink_IsAdoptedOnceTheWindowMovesOn()
        {
            var clock = new ServerTimeEstimator(3);
            clock.Observe(0.0f, 100.05f);
            for (int i = 1; i <= 3; i++)
                clock.Observe(i * 0.1f, 100.25f + i * 0.1f);

            Assert.That(clock.ServerNow(101f), Is.EqualTo(0.75f).Within(Tolerance));
        }

        [Test]
        public void NothingObserved_IsNotReady()
        {
            Assert.That(new ServerTimeEstimator(10).IsReady, Is.False);
        }
    }
}
