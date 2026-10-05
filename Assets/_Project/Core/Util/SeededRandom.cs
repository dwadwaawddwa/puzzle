namespace PuzzleStudio.Core.Util
{
    /// <summary>Small deterministic PRNG (xorshift32) — identical results on every platform/backend.</summary>
    public sealed class SeededRandom
    {
        uint _state;

        public SeededRandom(int seed)
        {
            _state = (uint)seed ^ 0x9E3779B9u;
            if (_state == 0) _state = 0x6C8E9CF5u;
            for (int i = 0; i < 4; i++) NextUInt(); // warm up
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(NextUInt() % (uint)maxExclusive);

        public int Range(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);

        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);
    }

    public static class StableHash
    {
        /// <summary>FNV-1a 32-bit hash; unlike string.GetHashCode it never changes between runs.</summary>
        public static int Fnv1a(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                if (s != null)
                    foreach (char c in s) { h ^= c; h *= 16777619; }
                return (int)h;
            }
        }
    }
}
