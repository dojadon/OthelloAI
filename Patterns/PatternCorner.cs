﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI.Patterns
{
    public class PatternCorner : Pattern
    {
        public override int HashLength => 10;

        public override ulong Mask => 0b0000000100000001000000010000001100011111;

        public PatternCorner(string filePath) : base(filePath)
        {
        }

        public override int GetHashMirrored(int hash)
        {
            int sum = 0;
            sum += ((hash / 19683) % 3) * 243;
            sum += ((hash / 6561) % 3) * 81;
            sum += ((hash / 2187) % 3) * 27;
            sum += ((hash / 729) % 3) * 9;
            sum += ((hash / 243) % 3) * 19683;
            sum += ((hash / 81) % 3) * 6561;
            sum += ((hash / 27) % 3) * 2187;
            sum += ((hash / 9) % 3) * 729;
            sum += ((hash / 3) % 3) * 3;
            sum += (hash % 3);

            return sum;
        }
    }
}
