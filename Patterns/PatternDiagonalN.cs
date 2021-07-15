using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI.Patterns
{
    class PatternDiagonalN : Pattern
    {
        public override int HashLength { get; }

        public override ulong Mask { get; }

        public PatternDiagonalN(string filePath, int length) : base(filePath)
        {
            HashLength = length;

            ulong m = 0x7f7f7f7f7f7f7f7fUL;
            ulong n = 0b1000000001000000001000000001000000001000000001000000001000000001;

            for (int i = 0; i < 8 - HashLength; i++)
            {
                n = m & (n >> 1);
            }
            Mask = n;
        }

        public override int GetHashMirrored(int hash)
        {
            int sum = 0;

            switch (HashLength)
            {
                case 7:
                    sum += ((hash / 729) % 3);
                    sum += ((hash / 243) % 3) * 3;
                    sum += ((hash / 81) % 3) * 9;
                    sum += ((hash / 27) % 3) * 27;
                    sum += ((hash / 9) % 3) * 81;
                    sum += ((hash / 3) % 3) * 243;
                    sum += (hash % 3) * 729;
                    break;

                case 6:
                    sum += ((hash / 243) % 3);
                    sum += ((hash / 81) % 3) * 3;
                    sum += ((hash / 27) % 3) * 9;
                    sum += ((hash / 9) % 3) * 27;
                    sum += ((hash / 3) % 3) * 81;
                    sum += (hash % 3) * 243;
                    break;

                case 5:
                    sum += ((hash / 81) % 3);
                    sum += ((hash / 27) % 3) * 3;
                    sum += ((hash / 9) % 3) * 9;
                    sum += ((hash / 3) % 3) * 27;
                    sum += (hash % 3) * 81;
                    break;

                case 4:
                    sum += ((hash / 27) % 3);
                    sum += ((hash / 9) % 3) * 3;
                    sum += ((hash / 3) % 3) * 9;
                    sum += (hash % 3) * 27;
                    break;
            }

            return sum;
        }
    }
}
