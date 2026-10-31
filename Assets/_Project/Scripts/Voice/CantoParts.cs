using System;
using System.Collections.Generic;

namespace Arreo.Voice
{
    /// <summary>One piece of a canto on its way over the network (rule 4: it travels in parts).</summary>
    public readonly struct CantoPart
    {
        public CantoPart(int index, int count, int totalBytes, byte[] data)
        {
            Index = index;
            Count = count;
            TotalBytes = totalBytes;
            Data = data;
        }

        public int Index { get; }
        public int Count { get; }
        public int TotalBytes { get; }
        public byte[] Data { get; }
    }

    public static class CantoParts
    {
        /// <summary>Cuts a canto into parts of <paramref name="partBytes"/> each; the last one gets the rest.</summary>
        public static List<CantoPart> Split(byte[] data, int partBytes)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Nothing to send.", nameof(data));
            if (partBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(partBytes), partBytes, "Must be positive.");

            int count = (data.Length + partBytes - 1) / partBytes;
            var parts = new List<CantoPart>(count);
            for (int i = 0; i < count; i++)
            {
                int length = Math.Min(partBytes, data.Length - i * partBytes);
                var part = new byte[length];
                Buffer.BlockCopy(data, i * partBytes, part, 0, length);
                parts.Add(new CantoPart(i, count, data.Length, part));
            }
            return parts;
        }

        /// <summary>FNV-1a, to tell two cantos apart in tests and reports (not a security check).</summary>
        public static uint Checksum(byte[] data)
        {
            uint hash = 2166136261;
            foreach (byte b in data)
                hash = (hash ^ b) * 16777619;
            return hash;
        }
    }

    public enum CantoPartResult
    {
        Added,
        /// <summary>That part was already here; the first copy is kept.</summary>
        Duplicate,
        /// <summary>That was the last part missing: <see cref="CantoAssembler.Result"/> holds the canto.</summary>
        Complete,
        /// <summary>The parts don't make a canto this game accepts; the assembler gives up for good.</summary>
        Rejected,
    }

    /// <summary>
    /// Puts a canto back together from its parts, in any order. Anything that doesn't fit (a part
    /// out of range, parts that disagree on the size, more than the limit) rejects the whole canto:
    /// it came from another machine, so it's checked before anything is kept.
    /// </summary>
    public sealed class CantoAssembler
    {
        private readonly int _maxBytes;
        private readonly int _maxPartBytes;
        private byte[][] _parts;
        private int _count;
        private int _totalBytes;
        private int _receivedBytes;

        /// <param name="maxBytes">Largest canto accepted (see <see cref="CantoRules.MaxWavBytes"/>).</param>
        /// <param name="maxPartBytes">Largest part accepted (the part size senders use).</param>
        public CantoAssembler(int maxBytes, int maxPartBytes)
        {
            _maxBytes = maxBytes;
            _maxPartBytes = maxPartBytes;
        }

        public bool IsComplete => Result != null;
        public bool IsRejected { get; private set; }
        public byte[] Result { get; private set; }
        public string Problem { get; private set; } = "";
        public int ReceivedParts { get; private set; }

        public CantoPartResult Add(int index, int count, int totalBytes, byte[] data)
        {
            if (IsRejected)
                return CantoPartResult.Rejected;
            if (IsComplete)
                return CantoPartResult.Duplicate;

            if (totalBytes <= 0 || totalBytes > _maxBytes)
                return Reject($"a canto of {totalBytes} bytes (at most {_maxBytes})");
            // At least as many parts as the part size allows, and at least a byte in each.
            long fewestParts = (totalBytes + (long)_maxPartBytes - 1) / _maxPartBytes;
            if (count < fewestParts || count > totalBytes)
                return Reject($"{count} parts for {totalBytes} bytes");
            if (data == null || data.Length == 0 || data.Length > _maxPartBytes)
                return Reject($"a part of {data?.Length ?? 0} bytes (at most {_maxPartBytes})");
            if (index < 0 || index >= count)
                return Reject($"part {index} of {count}");

            if (_parts == null)
            {
                _parts = new byte[count][];
                _count = count;
                _totalBytes = totalBytes;
            }
            else if (count != _count || totalBytes != _totalBytes)
                return Reject($"parts disagree: {count} parts of {totalBytes} bytes, before {_count} of {_totalBytes}");

            if (_parts[index] != null)
                return CantoPartResult.Duplicate;
            if (_receivedBytes + data.Length > _totalBytes)
                return Reject("the parts add up to more than the canto's size");

            _parts[index] = (byte[])data.Clone();
            _receivedBytes += data.Length;
            ReceivedParts++;
            if (ReceivedParts < _count)
                return CantoPartResult.Added;

            if (_receivedBytes != _totalBytes)
                return Reject($"the parts add up to {_receivedBytes} bytes, not {_totalBytes}");
            var result = new byte[_totalBytes];
            int offset = 0;
            foreach (byte[] part in _parts)
            {
                Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }
            Result = result;
            _parts = null;
            return CantoPartResult.Complete;
        }

        private CantoPartResult Reject(string problem)
        {
            IsRejected = true;
            Problem = problem;
            _parts = null;
            return CantoPartResult.Rejected;
        }
    }
}
