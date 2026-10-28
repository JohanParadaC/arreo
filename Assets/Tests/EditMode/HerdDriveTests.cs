using System;
using Arreo.Herd;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class HerdDriveTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void NotStarted_ClockDoesNotRun()
        {
            var drive = new HerdDrive(totalCattle: 20);

            drive.Tick(5f, inCorral: 3);

            Assert.That(drive.HasStarted, Is.False);
            Assert.That(drive.ElapsedSeconds, Is.EqualTo(0f));
            Assert.That(drive.InCorral, Is.EqualTo(3));
        }

        [Test]
        public void Started_ClockRuns()
        {
            var drive = new HerdDrive(20);
            drive.Begin();

            drive.Tick(1.5f, 0);
            drive.Tick(2f, 4);

            Assert.That(drive.ElapsedSeconds, Is.EqualTo(3.5f).Within(Tolerance));
            Assert.That(drive.InCorral, Is.EqualTo(4));
            Assert.That(drive.IsComplete, Is.False);
        }

        [Test]
        public void AllCattleInCorral_CompletesAndStopsClock()
        {
            var drive = new HerdDrive(20);
            drive.Begin();
            drive.Tick(10f, 19);

            drive.Tick(2f, 20);
            drive.Tick(30f, 20);

            Assert.That(drive.IsComplete, Is.True);
            Assert.That(drive.ElapsedSeconds, Is.EqualTo(12f).Within(Tolerance));
        }

        [Test]
        public void Completed_StaysCompleteIfCattleWanderOut()
        {
            var drive = new HerdDrive(5);
            drive.Begin();
            drive.Tick(1f, 5);

            drive.Tick(1f, 3);

            Assert.That(drive.IsComplete, Is.True);
            Assert.That(drive.ElapsedSeconds, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Begin_Twice_DoesNotResetClock()
        {
            var drive = new HerdDrive(20);
            drive.Begin();
            drive.Tick(4f, 0);

            drive.Begin();

            Assert.That(drive.ElapsedSeconds, Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void Constructor_NoCattle_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HerdDrive(0));
        }
    }
}
