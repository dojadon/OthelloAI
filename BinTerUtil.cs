using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace OthelloAI
{
    static class BinTerUtil
    {
        public static readonly int[] POW3_TABLE = Enumerable.Range(0, 19).Select(i => (int)Math.Pow(3, i)).ToArray();

        public static (uint, uint) ConvertTerToBinPair(int value, int length)
        {
            uint b1 = 0;
            uint b2 = 0;
            for (int i = 0; i < length; i++)
            {
                switch (value % 3)
                {
                    case 1:
                        b1 |= 1U << i;
                        break;

                    case 2:
                        b2 |= 1U << i;
                        break;
                }
                value /= 3;
            }
            return (b1, b2);
        }

        public static int ConvertBinToTer(int value, int length)
        {
            int result = 0;

            for (int i = 0; i < length; i++)
            {
                result += ((value >> i) & 1) * POW3_TABLE[i];
            }
            return result;
        }

        public static int[] CreateTernaryTable(int length)
        {
            int Convert(int b)
            {
                int result = 0;

                for (int i = 0; i < length; i++)
                {
                    result += ((b >> i) & 1) * POW3_TABLE[i];
                }
                return result;
            }

            int[] result = new int[1 << length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Convert(i);
            }
            return result;
        }
    }
}
