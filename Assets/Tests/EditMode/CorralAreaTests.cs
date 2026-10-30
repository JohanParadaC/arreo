using System.Collections.Generic;
using Arreo.Herd;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class CorralAreaTests
    {
        // 20 m wide (x) by 10 m deep (z), centered at (100, 0, 50).
        private static CorralArea CreateArea(float yaw = 0f) =>
            new CorralArea(new Vector3(100f, 0f, 50f), new Vector2(20f, 10f), yaw);

        [Test]
        public void Contains_PointInside_IsTrue()
        {
            Assert.That(CreateArea().Contains(new Vector3(108f, 0f, 53f)), Is.True);
        }

        [Test]
        public void Contains_PointOutside_IsFalse()
        {
            Assert.That(CreateArea().Contains(new Vector3(100f, 0f, 57f)), Is.False);
        }

        [Test]
        public void Contains_IgnoresHeight()
        {
            Assert.That(CreateArea().Contains(new Vector3(100f, 3f, 50f)), Is.True);
        }

        [Test]
        public void Contains_RotatedArea_UsesItsOwnAxes()
        {
            var rotated = CreateArea(yaw: 90f);

            Assert.That(rotated.Contains(new Vector3(100f, 0f, 58f)), Is.True);
            Assert.That(rotated.Contains(new Vector3(108f, 0f, 50f)), Is.False);
        }

        [Test]
        public void Count_CountsOnlyPointsInside()
        {
            var points = new[]
            {
                new Vector3(100f, 0f, 50f), new Vector3(95f, 0f, 48f), new Vector3(0f, 0f, 0f), new Vector3(130f, 0f, 50f),
            };

            Assert.That(CreateArea().Count(points), Is.EqualTo(2));
        }

        // The fair pen: only cows still in the herd count; a sold one keeps its spot in the list (D29).
        [Test]
        public void CollectInside_ListsOnlyCowsStillInTheHerd()
        {
            var points = new[]
            {
                new Vector3(100f, 0f, 50f), new Vector3(95f, 0f, 48f), new Vector3(0f, 0f, 0f), new Vector3(104f, 0f, 52f),
            };
            var present = new[] { true, false, true, true };
            var inside = new List<int>();

            int count = CreateArea().CollectInside(points, present, inside);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(inside, Is.EqualTo(new[] { 0, 3 }));
        }

        [Test]
        public void CollectInside_ReplacesThePreviousList()
        {
            var inside = new List<int> { 7, 8, 9 };

            CreateArea().CollectInside(new[] { new Vector3(0f, 0f, 0f) }, new[] { true }, inside);

            Assert.That(inside, Is.Empty);
        }

        [Test]
        public void CountPresent_SkipsSoldCows()
        {
            var points = new[] { new Vector3(100f, 0f, 50f), new Vector3(101f, 0f, 50f), new Vector3(102f, 0f, 50f) };

            Assert.That(CreateArea().CountPresent(points, new[] { false, true, false }), Is.EqualTo(1));
        }
    }
}
