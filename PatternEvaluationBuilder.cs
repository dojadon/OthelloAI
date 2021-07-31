using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;

namespace OthelloAI
{
    class PatternEvaluationBuilder
    {
        public Pattern[] Patterns { get; }

        public PatternEvaluationBuilder(Pattern[] patterns)
        {
            Patterns = patterns;
        }

        public static int Reverse(int value) => (int)Reverse((uint)value);

        public static uint Reverse(uint value)
        {
            return (value & 0xFF) << 24 |
                    ((value >> 8) & 0xFF) << 16 |
                    ((value >> 16) & 0xFF) << 8 |
                    ((value >> 24) & 0xFF);
        }

        public void Load(string file)
        {
            using var reader = new BinaryReader(new FileStream(file, FileMode.Open));
            int gameCount = 0;

            while(reader.BaseStream.Position != reader.BaseStream.Length)
            {
                int count = Reverse(reader.ReadInt32()) / 2;
                Board[] boards = new Board[count];

                for (int i = 0; i < boards.Length; i++)
                {
                    boards[i] = new Board(reader.ReadUInt64(), reader.ReadUInt64());
                    //boards[i].print();
                }

                Update(boards);
                gameCount++;

                if (gameCount % 100 == 0)
                {
                    Console.WriteLine(gameCount);
                }

                if (gameCount % 10000 == 0)
                {
                    Array.ForEach(Patterns, p => p.Save());
                    Console.WriteLine(string.Join(", ", Weight));
                }
            }
        }

        readonly float[] Weight = new float[60];

        public void Update(Board[] game)
        {
            int result = game[^1].GetStoneCountGap(1);
            float alpha = 0.001F;

            foreach(var board in game)
            {
                var boards = new MirroredBoards(board);
                MirroredNeededBoards.Create(board, out Board b1, out Board b2, out Board b3, out Board b4);

                int stage = board.n_stone - 5;
                int move_gap = Board.BitCount(board.GetMoves()) - Board.BitCount(board.GetOpponentMoves());

                float e = result - Patterns.Sum(p => p.EvalForTraining(board, b1, b2, b3, b4)) - move_gap;
                Array.ForEach(Patterns, p => Array.ForEach(boards.Boards, b => p.UpdataEvaluation(b, e * alpha)));

                if (board.n_stone == 40)
                {
                    //Console.WriteLine(e);
                }
            }
        }
    }
}
