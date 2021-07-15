using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI.Patterns
{
    class PatternLine : Pattern
    {
        public override int HashLength => 8;

        public int LineIndex { get; }

        public override ulong Mask { get; }

        public PatternLine(string filePath, int line) : base(filePath)
        {
            LineIndex = line;
            Mask = 0b11111111UL << (8 * LineIndex);
        }

        public override int GetHashMirrored(int hash)
        {
            int sum = 0;
            sum += ((hash / 2187) % 3);
            sum += ((hash / 729) % 3) * 3;
            sum += ((hash / 243) % 3) * 9;
            sum += ((hash / 81) % 3) * 27;
            sum += ((hash / 27) % 3) * 81;
            sum += ((hash / 9) % 3) * 243;
            sum += ((hash / 3) % 3) * 729;
            sum += (hash % 3) * 2187;

            return sum;
        }
    }
}
