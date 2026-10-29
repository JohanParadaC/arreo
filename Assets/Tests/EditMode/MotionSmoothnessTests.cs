using System.Collections.Generic;
using Arreo.Net;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class MotionSmoothnessTests
    {
        private const float Frame = 1f / 60f;

        // A horse moving at a steady 10 m/s along z, sampled every frame.
        private static List<Vector3> Steady(int frames, float speed = 10f)
        {
            var samples = new List<Vector3>();
            for (int i = 0; i < frames; i++)
                samples.Add(new Vector3(0f, 0f, i * speed * Frame));
            return samples;
        }

        [Test]
        public void SteadyMotion_HasNoJumpsOrStalls()
        {
            MotionSmoothness.Report report = MotionSmoothness.Analyze(Steady(120), Frame, maxPlausibleSpeed: 18f);

            Assert.That(report.Jumps, Is.EqualTo(0));
            Assert.That(report.Stalls, Is.EqualTo(0));
            Assert.That(report.MaxSpeed, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void ATeleport_CountsAsAJump()
        {
            List<Vector3> samples = Steady(120);
            for (int i = 60; i < samples.Count; i++)
                samples[i] += new Vector3(2f, 0f, 0f);

            Assert.That(MotionSmoothness.Analyze(samples, Frame, 18f).Jumps, Is.EqualTo(1));
        }

        [Test]
        public void FreezingMidRun_CountsAsAStall()
        {
            List<Vector3> samples = Steady(120);
            // Frames 50-54 repeat the same position (packets late), then it snaps back on track.
            for (int i = 50; i <= 54; i++)
                samples[i] = samples[49];

            Assert.That(MotionSmoothness.Analyze(samples, Frame, 18f).Stalls, Is.EqualTo(1));
        }

        [Test]
        public void StandingStill_IsNotAStall()
        {
            var samples = new List<Vector3>();
            for (int i = 0; i < 60; i++)
                samples.Add(Vector3.one);

            Assert.That(MotionSmoothness.Analyze(samples, Frame, 18f).Stalls, Is.EqualTo(0));
        }

        [Test]
        public void SlowFrame_WithNormalMotion_IsNotAJump()
        {
            // 10 m/s throughout, but frame 30 took 50 ms instead of 16.7 ms (the game hitched).
            var positions = new List<Vector3>();
            var times = new List<float>();
            float time = 0f;
            for (int i = 0; i < 60; i++)
            {
                time += i == 30 ? 0.05f : Frame;
                times.Add(time);
                positions.Add(new Vector3(0f, 0f, time * 10f));
            }

            MotionSmoothness.Report report = MotionSmoothness.Analyze(positions, times, maxPlausibleSpeed: 18f);

            Assert.That(report.Jumps, Is.EqualTo(0));
            Assert.That(report.MaxSpeed, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void SpeedChange_ReportsTheWorstFrameToFrameChange()
        {
            var samples = new List<Vector3> { Vector3.zero, new Vector3(0f, 0f, 0.1f), new Vector3(0f, 0f, 0.15f) };

            // 6 m/s then 3 m/s: a 3 m/s change in one frame.
            Assert.That(MotionSmoothness.Analyze(samples, 0.1f / 6f, 18f).MaxSpeedChange, Is.EqualTo(3f).Within(0.01f));
        }
    }
}
