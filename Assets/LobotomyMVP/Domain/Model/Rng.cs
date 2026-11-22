

namespace LobotomyMVP.Domain.Model
{
    /// Детерминированный seeded RNG (MLCG, Lehmer). Один экземпляр на GameState.
    public sealed class Rng
    {
        private ulong _state;

        public Rng(int seed)
        {
            unchecked { _state = (ulong)seed * 2654435761u + 1013904223u; }
            if (_state == 0) _state = 88172645463393265u;
        }

        public int SeedValue { get; private set; }

        public double NextDouble()
        {
            // MINSTD с множителем 48271, период 2^31-2
            unchecked
            {
                _state = (_state * 48271u) % 2147483647u;
                return (_state & 0x7FFFFFFF) / (double)0x7FFFFFFF;
            }
        }

        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive) return minInclusive;
            int range = maxInclusive - minInclusive + 1;
            return minInclusive + (int)(NextDouble() * range);
            // отбрасывание дроби: детерминированно при том же seed
        }

        public void SetStateForRestore(int seed, ulong state)
        {
            SeedValue = seed;
            _state = state;
        }

        public ulong RawState => _state;
    }
}
