using System.Collections.Generic;
using System.Linq;
using Arreo.Net;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class HerdSnapshotCodecTests
    {
        private static (List<Vector3> positions, List<float> yaws, List<bool> present, byte generation) Decode(byte[] data)
        {
            var positions = new List<Vector3>();
            var yaws = new List<float>();
            var present = new List<bool>();
            byte generation = HerdSnapshotCodec.Decode(data, positions, yaws, present);
            return (positions, yaws, present, generation);
        }

        private static bool[] AllPresent(int count) => Enumerable.Repeat(true, count).ToArray();

        private static byte[] Encode(Vector3[] positions, float[] yaws) =>
            HerdSnapshotCodec.Encode(positions, yaws, AllPresent(positions.Length), generation: 0);

        [Test]
        public void RoundTrip_KeepsPositionsWithinACentimetre()
        {
            var positions = new[] { new Vector3(45.123f, 0.37f, 55.987f), new Vector3(-280.5f, 2.4f, 12.004f) };
            var yaws = new[] { 0f, 0f };

            var (decoded, _, _, _) = Decode(Encode(positions, yaws));

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

            var (_, decoded, _, _) = Decode(Encode(positions, yaws));

            for (int i = 0; i < yaws.Length; i++)
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(decoded[i], yaws[i])), Is.LessThanOrEqualTo(2f));
        }

        [Test]
        public void RoundTrip_KeepsWhichCowsAreStillInTheHerd()
        {
            // More than 8 cows, so the flags spill into a second byte.
            var present = new[] { true, false, true, true, false, false, true, true, false, true, true };
            var positions = new Vector3[present.Length];
            var yaws = new float[present.Length];

            var (_, _, decoded, _) = Decode(HerdSnapshotCodec.Encode(positions, yaws, present, generation: 0));

            Assert.That(decoded, Is.EqualTo(present));
        }

        [Test]
        public void RoundTrip_KeepsTheRestockGeneration()
        {
            var (_, _, _, generation) = Decode(HerdSnapshotCodec.Encode(new[] { Vector3.zero }, new[] { 0f }, new[] { true }, generation: 201));

            Assert.That(generation, Is.EqualTo(201));
        }

        [Test]
        public void Size_IsThreeBytesPlusSixPerCowPlusABitEach()
        {
            var positions = new Vector3[50];
            var yaws = new float[50];

            // 50 presence flags fit in 7 bytes.
            Assert.That(Encode(positions, yaws).Length, Is.EqualTo(3 + 6 * 50 + 7));
        }

        [Test]
        public void PositionsOutsideTheRange_AreClampedNotWrapped()
        {
            var (decoded, _, _, _) = Decode(Encode(new[] { new Vector3(1000f, 50f, -1000f) }, new[] { 0f }));

            Assert.That(decoded[0].x, Is.GreaterThan(300f));
            Assert.That(decoded[0].z, Is.LessThan(-300f));
            Assert.That(decoded[0].y, Is.GreaterThan(20f));
        }

        [Test]
        public void EmptyHerd_RoundTrips()
        {
            var (decoded, _, present, _) = Decode(Encode(new Vector3[0], new float[0]));

            Assert.That(decoded, Is.Empty);
            Assert.That(present, Is.Empty);
        }
    }
}
