using System.Collections.Generic;
using Arreo.Net;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class HerdSnapshotCodecTests
    {
        private static (List<Vector3> positions, List<float> yaws) Decode(byte[] data)
        {
            var positions = new List<Vector3>();
            var yaws = new List<float>();
            HerdSnapshotCodec.Decode(data, positions, yaws);
            return (positions, yaws);
        }

        [Test]
        public void RoundTrip_KeepsPositionsWithinACentimetre()
        {
            var positions = new[] { new Vector3(45.123f, 0.37f, 55.987f), new Vector3(-280.5f, 2.4f, 12.004f) };
            var yaws = new[] { 0f, 0f };

            var (decoded, _) = Decode(HerdSnapshotCodec.Encode(positions, yaws));

            Assert.That(decoded.Count, Is.EqualTo(2));
            for (int i = 0; i < 2; i++)
            {
                Assert.That(Mathf.Abs(decoded[i].x - positions[i].x), Is.LessThanOrEqualTo(0.01f));
                Assert.That(Mathf.Abs(decoded[i].z - positions[i].z), Is.LessThanOrEqualTo(0.01f));
                Assert.That(Mathf.Abs(decoded[i].y - positions[i].y), Is.LessThanOrEqualTo(0.05f));
            }
        }

        [Test]
        public void RoundTrip_KeepsHeadingWithinTwoDegrees_EvenAcrossNorth()
        {
            var positions = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            var yaws = new[] { 10f, 359.5f, 181.3f };

            var (_, decoded) = Decode(HerdSnapshotCodec.Encode(positions, yaws));

            for (int i = 0; i < yaws.Length; i++)
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(decoded[i], yaws[i])), Is.LessThanOrEqualTo(2f));
        }

        [Test]
        public void Size_IsTwoBytesPlusSixPerCow()
        {
            var positions = new Vector3[50];
            var yaws = new float[50];

            Assert.That(HerdSnapshotCodec.Encode(positions, yaws).Length, Is.EqualTo(2 + 6 * 50));
        }

        [Test]
        public void PositionsOutsideTheRange_AreClampedNotWrapped()
        {
            var (decoded, _) = Decode(HerdSnapshotCodec.Encode(new[] { new Vector3(1000f, 50f, -1000f) }, new[] { 0f }));

            Assert.That(decoded[0].x, Is.GreaterThan(300f));
            Assert.That(decoded[0].z, Is.LessThan(-300f));
            Assert.That(decoded[0].y, Is.GreaterThan(20f));
        }

        [Test]
        public void EmptyHerd_RoundTrips()
        {
            var (decoded, _) = Decode(HerdSnapshotCodec.Encode(new Vector3[0], new float[0]));

            Assert.That(decoded, Is.Empty);
        }
    }
}
