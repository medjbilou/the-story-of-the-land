#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>Average and percentile frame times (ms), shared by the terrain and visual profiles. A percentile is the
    /// nearest rank: the smallest frame time at or above that share of the frames.</summary>
    public static class FrameStats
    {
        public static (float avg, float p95) Summary(IReadOnlyList<float> frameMs)
        {
            var (avg, _, p95, _) = Percentiles(frameMs);
            return (avg, p95);
        }

        /// <summary>F6: the median, p95 and p99 with the average, for the per-tier budgets (docs/visual-v1.md "F6").</summary>
        public static (float avg, float p50, float p95, float p99) Percentiles(IReadOnlyList<float> frameMs)
        {
            if (frameMs == null) throw new ArgumentNullException(nameof(frameMs));
            if (frameMs.Count == 0) throw new ArgumentException("no frames were measured", nameof(frameMs));
            var sorted = frameMs.OrderBy(x => x).ToList();
            float At(double q) => sorted[Math.Max(0, Math.Min(sorted.Count - 1, (int)Math.Ceiling(sorted.Count * q) - 1))];
            return (sorted.Average(), At(0.50), At(0.95), At(0.99));
        }
    }
}
