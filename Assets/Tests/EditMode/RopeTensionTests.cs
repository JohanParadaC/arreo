using Arreo.Rope;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class RopeTensionTests
    {
        private const float Tolerance = 0.001f;

        private static RopeTension CreateTension() => new RopeTension(
            stiffness: 4000f,
            holdForce: 1500f,
            dragSpeedPerNewton: 0.002f,
            maxDragSpeed: 3f);

        [Test]
        public void PathLength_SumsTheSegments()
        {
            var points = new[] { Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 4f, 0f) };

            Assert.That(RopeTension.PathLength(points), Is.EqualTo(7f).Within(Tolerance));
        }

        [Test]
        public void FromLength_Slack_IsZero()
        {
            Assert.That(CreateTension().FromLength(pathLength: 9.5f, restLength: 10f), Is.EqualTo(0f));
        }

        [Test]
        public void FromLength_Stretched_IsStiffnessTimesStretch()
        {
            Assert.That(CreateTension().FromLength(pathLength: 10.5f, restLength: 10f), Is.EqualTo(2000f).Within(Tolerance));
        }

        [Test]
        public void DragSpeed_BelowWhatTheHorseHolds_IsZero()
        {
            Assert.That(CreateTension().DragSpeed(1400f), Is.EqualTo(0f));
        }

        [Test]
        public void DragSpeed_AboveHold_GrowsWithTheExtraPull()
        {
            Assert.That(CreateTension().DragSpeed(2000f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void DragSpeed_IsCapped()
        {
            Assert.That(CreateTension().DragSpeed(100000f), Is.EqualTo(3f).Within(Tolerance));
        }
    }
}
