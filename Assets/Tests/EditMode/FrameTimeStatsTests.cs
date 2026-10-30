using System.Linq;
using Arreo.Core;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class FrameTimeStatsTests
    {
        private static FrameTimeStats Frames(params float[] milliseconds)
        {
            var stats = new FrameTimeStats();
            foreach (float ms in milliseconds)
                stats.Add(ms);
            return stats;
        }

        [Test]
        public void SteadySixtyFps_AveragesSixty()
        {
            FrameTimeStats stats = Frames(Enumerable.Repeat(1000f / 60f, 120).ToArray());

            Assert.That(stats.Count, Is.EqualTo(120));
            Assert.That(stats.AverageFps, Is.EqualTo(60f).Within(0.01f));
            Assert.That(stats.Percentile(0.5f), Is.EqualTo(1000f / 60f).Within(0.001f));
        }

        [Test]
        public void SlowFrames_ShowInTheHighPercentilesAndTheShareOverBudget()
        {
            // 98 quick frames and two hitches.
            FrameTimeStats stats = Frames(Enumerable.Repeat(10f, 98).Concat(new[] { 30f, 50f }).ToArray());

            Assert.That(stats.Percentile(0.5f), Is.EqualTo(10f));
            Assert.That(stats.Percentile(0.99f), Is.EqualTo(30f), "nearest rank: the 99th of 100 frames");
            Assert.That(stats.Max, Is.EqualTo(50f));
            Assert.That(stats.ShareSlowerThan(20f), Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(stats.AverageFps, Is.EqualTo(100f / (98 * 0.01f + 0.03f + 0.05f)).Within(0.01f));
        }

        [Test]
        public void NoFrames_IsAllZero()
        {
            var stats = new FrameTimeStats();

            Assert.That(stats.AverageFps, Is.EqualTo(0f));
            Assert.That(stats.Percentile(0.99f), Is.EqualTo(0f));
            Assert.That(stats.ShareSlowerThan(16.7f), Is.EqualTo(0f));
        }
    }
}
