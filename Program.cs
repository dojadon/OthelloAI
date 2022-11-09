using System;
using System.Threading;

namespace OthelloAI
{
    static class Program
    {
        private static ThreadLocal<Random> ThreadLocalRandom { get; } = new ThreadLocal<Random>(() => new Random());
        public static Random Random => ThreadLocalRandom.Value;

        public const int NUM_STAGES = 60;

        public static readonly Weights WEIGHT_EDGE2X = Weights.Create(new BoardHasherMask(0b01000010_11111111UL), NUM_STAGES, "e_edge_x.dat", true);
        public static readonly Weights WEIGHT_EDGE_BLOCK = Weights.Create(new BoardHasherMask(0b00111100_10111101UL), NUM_STAGES, "e_edge_block.dat", true);
        public static readonly Weights WEIGHT_CORNER_BLOCK = Weights.Create(new BoardHasherMask(0b00000111_00000111_00000111UL), NUM_STAGES, "e_corner_block.dat", true);
        public static readonly Weights WEIGHT_CORNER = Weights.Create(new BoardHasherMask(0b00000001_00000001_00000001_00000011_00011111UL), NUM_STAGES, "e_corner.dat", true);
        public static readonly Weights WEIGHT_LINE1 = Weights.Create(new BoardHasherLine1(1), NUM_STAGES, "e_line1.dat", true);
        public static readonly Weights WEIGHT_LINE2 = Weights.Create(new BoardHasherLine1(2), NUM_STAGES, "e_line2.dat", true);

        public static readonly Weights WEIGHT = new WeightsSum(WEIGHT_EDGE2X, WEIGHT_EDGE_BLOCK, WEIGHT_CORNER_BLOCK, WEIGHT_CORNER, WEIGHT_LINE1, WEIGHT_LINE2);
    }
}
