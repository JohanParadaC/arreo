using System.Collections.Generic;
using Arreo.Net;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class SnapshotBufferTests
    {
        private const float Tolerance = 0.001f;
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<float> _yaws = new List<float>();

        private static SnapshotBuffer CreateBuffer()
        {
            var buffer = new SnapshotBuffer(keepSeconds: 1f);
            buffer.Add(1.0f, new[] { new Vector3(0f, 0f, 0f) }, new[] { 350f });
            buffer.Add(1.1f, new[] { new Vector3(0f, 0f, 1f) }, new[] { 10f });
            return buffer;
        }

        [Test]
        public void Sample_BetweenTwoSnapshots_Interpolates()
        {
            CreateBuffer().Sample(1.05f, _positions, _yaws);

            Assert.That(_positions[0].z, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void Sample_HeadingTakesTheShortWayAround()
        {
            CreateBuffer().Sample(1.05f, _positions, _yaws);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(_yaws[0], 0f)), Is.LessThan(0.5f));
        }

        [Test]
        public void Sample_AfterTheLastSnapshot_HoldsItInsteadOfGuessing()
        {
            CreateBuffer().Sample(5f, _positions, _yaws);

            Assert.That(_positions[0].z, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Sample_BeforeTheFirstSnapshot_UsesTheFirst()
        {
            CreateBuffer().Sample(0.5f, _positions, _yaws);

            Assert.That(_positions[0].z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Add_OlderThanTheLatest_IsIgnored()
        {
            SnapshotBuffer buffer = CreateBuffer();

            buffer.Add(1.05f, new[] { new Vector3(0f, 0f, 99f) }, new[] { 0f });
            buffer.Sample(1.05f, _positions, _yaws);

            Assert.That(_positions[0].z, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void OldSnapshots_AreDroppedAfterKeepSeconds()
        {
            SnapshotBuffer buffer = CreateBuffer();

            buffer.Add(3f, new[] { new Vector3(0f, 0f, 3f) }, new[] { 0f });

            Assert.That(buffer.OldestTime, Is.GreaterThanOrEqualTo(2f));
        }

        [Test]
        public void Empty_SampleReturnsFalse()
        {
            Assert.That(new SnapshotBuffer(1f).Sample(1f, _positions, _yaws), Is.False);
        }
    }
}
