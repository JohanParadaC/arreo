using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 8 prototype): where each frame's time goes and how much memory is used, read
    /// from the Profiler's own markers and counters with ProfilerRecorder (most markers only exist
    /// in development builds, like the Network Test Player). Time markers are averaged per frame;
    /// memory counters keep their peak.
    /// </summary>
    internal sealed class ProtoPerfRecorder : IDisposable
    {
        // The main thread's frame, the parts of it that tend to grow with players and cows, and the two waits.
        private static readonly string[] Names =
        {
            "PlayerLoop", "WaitForTargetFPS", "Gfx.WaitForPresentOnGfxThread",
            "FixedUpdate.PhysicsFixedUpdate", "FixedBehaviourUpdate", "BehaviourUpdate", "LateBehaviourUpdate",
            "PostLateUpdate.FinishFrameRendering", "GUI.Repaint", "GC.Collect",
            "Total Used Memory", "System Used Memory", "GC Reserved Memory", "GC Allocated In Frame",
        };

        private readonly List<Entry> _entries = new List<Entry>();
        private int _frames;

        private sealed class Entry
        {
            public string Name;
            public ProfilerRecorder Recorder;
            public ProfilerMarkerDataUnit Unit;
            public double Sum;
            public long Peak;
        }

        public ProtoPerfRecorder()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (ProfilerRecorderHandle handle in handles)
            {
                ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
                if (Array.IndexOf(Names, description.Name) < 0 || _entries.Exists(e => e.Name == description.Name))
                    continue;
                var recorder = new ProfilerRecorder(handle, 1);
                recorder.Start();
                _entries.Add(new Entry { Name = description.Name, Recorder = recorder, Unit = description.UnitType });
            }
        }

        /// <summary>Once per measured frame: adds what the last frame recorded.</summary>
        public void Sample()
        {
            _frames++;
            foreach (Entry entry in _entries)
            {
                long value = entry.Recorder.LastValue;
                entry.Sum += value;
                entry.Peak = Math.Max(entry.Peak, value);
            }
        }

        /// <summary>One line per kind: "time: PlayerLoop=12.3ms ..." and "memory: Total Used Memory=850MB ...".</summary>
        public string Report()
        {
            var time = new StringBuilder("time per frame (ms):");
            var memory = new StringBuilder("memory:");
            foreach (string name in Names)
            {
                Entry entry = _entries.Find(e => e.Name == name);
                if (entry == null)
                    continue;
                double average = _frames > 0 ? entry.Sum / _frames : 0d;
                if (entry.Unit == ProfilerMarkerDataUnit.TimeNanoseconds)
                    time.Append(string.Format(CultureInfo.InvariantCulture, " {0}={1:0.00}", name, average / 1e6));
                else if (name == "GC Allocated In Frame")
                    memory.Append(string.Format(CultureInfo.InvariantCulture, " {0}={1:0.0}KB/frame", name, average / 1024d));
                else
                    memory.Append(string.Format(CultureInfo.InvariantCulture, " {0}(peak)={1:0}MB", name, entry.Peak / (1024d * 1024d)));
            }
            return time + "\n" + memory;
        }

        public void Dispose()
        {
            foreach (Entry entry in _entries)
                entry.Recorder.Dispose();
            _entries.Clear();
        }
    }
}
