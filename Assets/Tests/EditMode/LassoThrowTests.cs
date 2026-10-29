using Arreo.Rope;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class LassoThrowTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void ClampToRange_TargetInRange_IsUnchanged()
        {
            var target = new Vector3(5f, 0f, 5f);

            Assert.That(LassoThrow.ClampToRange(Vector3.zero, target, maxRange: 12f), Is.EqualTo(target));
        }

        [Test]
        public void ClampToRange_TargetTooFar_FallsShortAtMaxRange()
        {
            Vector3 clamped = LassoThrow.ClampToRange(new Vector3(0f, 1.6f, 0f), new Vector3(0f, 0f, 30f), maxRange: 12f);

            Assert.That(clamped.z, Is.EqualTo(12f).Within(Tolerance));
            Assert.That(clamped.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ArcPoint_StartsAndEndsAtTheEnds()
        {
            var start = new Vector3(0f, 1.6f, 0f);
            var end = new Vector3(0f, 1f, 10f);

            Assert.That(Vector3.Distance(LassoThrow.ArcPoint(start, end, 2f, 0f), start), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(LassoThrow.ArcPoint(start, end, 2f, 1f), end), Is.LessThan(Tolerance));
        }

        [Test]
        public void ArcPoint_Midway_RisesByApexHeight()
        {
            Vector3 middle = LassoThrow.ArcPoint(Vector3.zero, new Vector3(0f, 0f, 10f), apexHeight: 2f, t: 0.5f);

            Assert.That(middle.y, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(middle.z, Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void FlightTime_IsDistanceOverSpeed_WithAMinimum()
        {
            Assert.That(LassoThrow.FlightTime(distance: 12f, throwSpeed: 20f, minTime: 0.25f), Is.EqualTo(0.6f).Within(Tolerance));
            Assert.That(LassoThrow.FlightTime(distance: 1f, throwSpeed: 20f, minTime: 0.25f), Is.EqualTo(0.25f).Within(Tolerance));
        }

        [Test]
        public void FindCatch_PicksNearestHeadWithinRadius()
        {
            var heads = new[] { new Vector3(10f, 1.1f, 0f), new Vector3(1f, 1.1f, 0.5f), new Vector3(0.5f, 1.1f, 0f) };

            Assert.That(LassoThrow.FindCatch(Vector3.zero, heads, catchRadius: 1.5f), Is.EqualTo(2));
        }

        [Test]
        public void FindCatch_IgnoresHeightOfTheHead()
        {
            var heads = new[] { new Vector3(0.3f, 1.4f, 0f) };

            Assert.That(LassoThrow.FindCatch(Vector3.zero, heads, catchRadius: 0.5f), Is.EqualTo(0));
        }

        [Test]
        public void FindCatch_NoHeadClose_Misses()
        {
            var heads = new[] { new Vector3(3f, 1.1f, 0f) };

            Assert.That(LassoThrow.FindCatch(Vector3.zero, heads, catchRadius: 1.5f), Is.EqualTo(-1));
        }
    }
}
