using System.Linq;
using Arreo.Rope;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class VerletRopeTests
    {
        private const float Step = 0.02f;
        private static readonly Vector3 Start = new Vector3(0f, 1.7f, 0f);
        private static readonly Vector3 End = new Vector3(0f, 1.1f, 8f);

        private static VerletRope CreateRope() => new VerletRope(pointCount: 17, iterations: 12, damping: 0.05f);

        private static void Simulate(VerletRope rope, Vector3 start, Vector3 end, float seconds, float ground = 0f)
        {
            for (float t = 0f; t < seconds; t += Step)
                rope.Step(start, end, Step, ground);
        }

        [Test]
        public void Reset_LaysPointsStraightBetweenTheEnds()
        {
            var rope = CreateRope();

            rope.Reset(Start, End, restLength: 9f);

            Assert.That(rope.Points.Count, Is.EqualTo(17));
            Assert.That(rope.Points[0], Is.EqualTo(Start));
            Assert.That(Vector3.Distance(rope.Points[16], End), Is.LessThan(0.0001f));
            Assert.That(rope.SegmentLength, Is.EqualTo(9f / 16f).Within(0.0001f));
        }

        [Test]
        public void Step_SlackRope_SagsUnderGravity()
        {
            var rope = CreateRope();
            rope.Reset(Start, End, restLength: 9f);

            Simulate(rope, Start, End, 2f, ground: -10f);

            float straightMiddleHeight = (Start.y + End.y) / 2f;
            Assert.That(rope.Points[8].y, Is.LessThan(straightMiddleHeight - 0.5f));
        }

        [Test]
        public void Step_EndsFollowWhereTheyAreTied()
        {
            var rope = CreateRope();
            rope.Reset(Start, End, restLength: 9f);
            var movedStart = Start + Vector3.right;
            var movedEnd = End + Vector3.right;

            rope.Step(movedStart, movedEnd, Step, 0f);

            Assert.That(rope.Points[0], Is.EqualTo(movedStart));
            Assert.That(rope.Points[16], Is.EqualTo(movedEnd));
        }

        [Test]
        public void Step_LinksNeverStretchPastTheirLength_WhenTheEndsAreInReach()
        {
            var rope = CreateRope();
            rope.Reset(Start, End, restLength: 9f);

            Simulate(rope, Start, End, 3f, ground: -10f);

            for (int i = 1; i < rope.Points.Count; i++)
                Assert.That(Vector3.Distance(rope.Points[i - 1], rope.Points[i]), Is.LessThanOrEqualTo(rope.SegmentLength + 0.01f));
        }

        [Test]
        public void Step_KeepsPointsAboveTheGround()
        {
            var rope = CreateRope();
            rope.Reset(Start, End, restLength: 12f);

            Simulate(rope, Start, End, 2f, ground: 0f);

            Assert.That(rope.Points.Min(p => p.y), Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void Step_AtRest_SettlesWithoutTrembling()
        {
            var rope = CreateRope();
            rope.Reset(Start, End, restLength: 9f);
            Simulate(rope, Start, End, 5f);
            var before = rope.Points.ToArray();

            rope.Step(Start, End, Step, 0f);

            float maxSpeed = rope.Points.Select((p, i) => Vector3.Distance(p, before[i]) / Step).Max();
            Assert.That(maxSpeed, Is.LessThan(0.01f));
        }
    }
}
