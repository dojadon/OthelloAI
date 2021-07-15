using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI.Patterns
{
    class PatternDiagonal8 : Pattern
    {
        public override int HashLength => 8;

        public override ulong Mask => 0b1000000001000000001000000001000000001000000001000000001000000001;

        public PatternDiagonal8(string filePath) : base(filePath)
        {
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
