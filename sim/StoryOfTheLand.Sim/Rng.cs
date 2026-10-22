namespace StoryOfTheLand.Sim
{
    /// <summary>SplitMix64. The whole generator state is one ulong, stored in GameState.</summary>
    public struct Rng
    {
        public ulong State;
        public Rng(ulong state) { State = state; }

        public ulong NextULong()
        {
            ulong z = State += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);
    }
}
