using System;
using System.Collections.Generic;
using Arreo.Voice;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class CantoPartsTests
    {
        private const int PartBytes = 8192;
        private const int MaxBytes = 500_000;

        private static byte[] Bytes(int count, int seed = 3)
        {
            var bytes = new byte[count];
            new Random(seed).NextBytes(bytes);
            return bytes;
        }

        private static CantoAssembler CreateAssembler() => new CantoAssembler(MaxBytes, PartBytes);

        [Test]
        public void Split_CutsIntoPartsOfAtMostThePartSize()
        {
            List<CantoPart> parts = CantoParts.Split(Bytes(20000), PartBytes);

            Assert.That(parts, Has.Count.EqualTo(3));
            Assert.That(parts[0].Data, Has.Length.EqualTo(8192));
            Assert.That(parts[1].Data, Has.Length.EqualTo(8192));
            Assert.That(parts[2].Data, Has.Length.EqualTo(20000 - 2 * 8192));
            for (int i = 0; i < parts.Count; i++)
            {
                Assert.That(parts[i].Index, Is.EqualTo(i));
                Assert.That(parts[i].Count, Is.EqualTo(3));
                Assert.That(parts[i].TotalBytes, Is.EqualTo(20000));
            }
        }

        [Test]
        public void Split_SmallerThanOnePart_IsOnePart()
        {
            List<CantoPart> parts = CantoParts.Split(Bytes(100), PartBytes);

            Assert.That(parts, Has.Count.EqualTo(1));
            Assert.That(parts[0].Data, Is.EqualTo(Bytes(100)));
        }

        [Test]
        public void Split_NothingOrABadPartSize_Throws()
        {
            Assert.Throws<ArgumentException>(() => CantoParts.Split(new byte[0], PartBytes));
            Assert.Throws<ArgumentException>(() => CantoParts.Split(null, PartBytes));
            Assert.Throws<ArgumentOutOfRangeException>(() => CantoParts.Split(Bytes(10), 0));
        }

        [Test]
        public void AllPartsInAnyOrder_RebuildTheSameBytes()
        {
            byte[] original = Bytes(30000);
            List<CantoPart> parts = CantoParts.Split(original, PartBytes);
            CantoAssembler assembler = CreateAssembler();

            CantoPartResult last = CantoPartResult.Added;
            foreach (int i in new[] { 2, 0, 3, 1 })
                last = assembler.Add(parts[i].Index, parts[i].Count, parts[i].TotalBytes, parts[i].Data);

            Assert.That(last, Is.EqualTo(CantoPartResult.Complete));
            Assert.That(assembler.IsComplete, Is.True);
            Assert.That(assembler.Result, Is.EqualTo(original));
        }

        [Test]
        public void MissingPart_IsNotComplete()
        {
            List<CantoPart> parts = CantoParts.Split(Bytes(30000), PartBytes);
            CantoAssembler assembler = CreateAssembler();

            foreach (int i in new[] { 0, 1, 3 })
                Assert.That(assembler.Add(parts[i].Index, parts[i].Count, parts[i].TotalBytes, parts[i].Data), Is.EqualTo(CantoPartResult.Added));

            Assert.That(assembler.IsComplete, Is.False);
            Assert.That(assembler.Result, Is.Null);
            Assert.That(assembler.ReceivedParts, Is.EqualTo(3));
        }

        [Test]
        public void SamePartTwice_IsADuplicateAndChangesNothing()
        {
            byte[] original = Bytes(10000);
            List<CantoPart> parts = CantoParts.Split(original, PartBytes);
            CantoAssembler assembler = CreateAssembler();

            assembler.Add(0, parts[0].Count, parts[0].TotalBytes, parts[0].Data);
            CantoPartResult again = assembler.Add(0, parts[0].Count, parts[0].TotalBytes, Bytes(PartBytes, seed: 9));
            assembler.Add(1, parts[1].Count, parts[1].TotalBytes, parts[1].Data);

            Assert.That(again, Is.EqualTo(CantoPartResult.Duplicate));
            Assert.That(assembler.Result, Is.EqualTo(original));
        }

        [Test]
        public void PartOutsideTheCount_IsRejected()
        {
            Assert.That(CreateAssembler().Add(3, 3, 20000, Bytes(100)), Is.EqualTo(CantoPartResult.Rejected));
            Assert.That(CreateAssembler().Add(-1, 3, 20000, Bytes(100)), Is.EqualTo(CantoPartResult.Rejected));
        }

        [Test]
        public void PartsThatDisagreeOnTheSize_AreRejected()
        {
            CantoAssembler assembler = CreateAssembler();
            assembler.Add(0, 3, 20000, Bytes(PartBytes));

            Assert.That(assembler.Add(1, 3, 20001, Bytes(PartBytes)), Is.EqualTo(CantoPartResult.Rejected), "other total");
            Assert.That(assembler.Problem, Is.Not.Empty);
            Assert.That(CreateAssemblerWith(0, 3, 20000).Add(1, 4, 20000, Bytes(PartBytes)), Is.EqualTo(CantoPartResult.Rejected), "other count");
        }

        [Test]
        public void MoreThanTheLimit_IsRejected()
        {
            Assert.That(CreateAssembler().Add(0, 100, MaxBytes + 1, Bytes(PartBytes)), Is.EqualTo(CantoPartResult.Rejected), "total over the limit");
            Assert.That(CreateAssembler().Add(0, 2, 20000, Bytes(PartBytes + 1)), Is.EqualTo(CantoPartResult.Rejected), "part over the part size");
            Assert.That(CreateAssembler().Add(0, 1, 0, new byte[0]), Is.EqualTo(CantoPartResult.Rejected), "empty");
        }

        [Test]
        public void PartsThatDoNotAddUpToTheTotal_AreRejected()
        {
            CantoAssembler assembler = CreateAssembler();
            assembler.Add(0, 2, 10000, Bytes(PartBytes));

            Assert.That(assembler.Add(1, 2, 10000, Bytes(100)), Is.EqualTo(CantoPartResult.Rejected));
            Assert.That(assembler.IsComplete, Is.False);
        }

        [Test]
        public void AfterARejection_EverythingIsRejected()
        {
            byte[] original = Bytes(10000);
            List<CantoPart> parts = CantoParts.Split(original, PartBytes);
            CantoAssembler assembler = CreateAssembler();
            assembler.Add(5, 2, 10000, Bytes(10));

            foreach (CantoPart part in parts)
                Assert.That(assembler.Add(part.Index, part.Count, part.TotalBytes, part.Data), Is.EqualTo(CantoPartResult.Rejected));
            Assert.That(assembler.IsRejected, Is.True);
            Assert.That(assembler.Result, Is.Null);
        }

        [Test]
        public void ACantoSplitAndRebuilt_DecodesToTheSameAudio()
        {
            var samples = new float[16000 * 10];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = 0.6f * (float)Math.Sin(2.0 * Math.PI * 220.0 * i / 16000);
            byte[] wav = WavCodec.Encode(samples, 16000);
            CantoAssembler assembler = new CantoAssembler(MaxBytes, PartBytes);

            List<CantoPart> parts = CantoParts.Split(wav, PartBytes);
            for (int i = parts.Count - 1; i >= 0; i--)
                assembler.Add(parts[i].Index, parts[i].Count, parts[i].TotalBytes, parts[i].Data);

            Assert.That(assembler.IsComplete, Is.True, assembler.Problem);
            Assert.That(WavCodec.TryDecode(assembler.Result, out float[] back, out int rate, out string problem), Is.True, problem);
            Assert.That(rate, Is.EqualTo(16000));
            Assert.That(back, Is.EqualTo(WavCodec.TryDecode(wav, out float[] direct, out _, out _) ? direct : null));
        }

        [Test]
        public void Checksum_SameBytesGiveTheSameSum_OneChangedByteDoesNot()
        {
            byte[] a = Bytes(5000);
            byte[] b = Bytes(5000);
            byte[] c = Bytes(5000);
            c[1234] ^= 1;

            Assert.That(CantoParts.Checksum(a), Is.EqualTo(CantoParts.Checksum(b)));
            Assert.That(CantoParts.Checksum(c), Is.Not.EqualTo(CantoParts.Checksum(a)));
        }

        private static CantoAssembler CreateAssemblerWith(int index, int count, int total)
        {
            CantoAssembler assembler = CreateAssembler();
            assembler.Add(index, count, total, Bytes(PartBytes));
            return assembler;
        }
    }
}
