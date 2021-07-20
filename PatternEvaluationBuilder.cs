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
                }
            }
        }

        public void Update(Board[] game)
        {
            int result = game[^1].GetStoneCountGap(1);
            float alpha = 0.001F;

            foreach(var board in game)
            {
                var hor = board.HorizontalMirrored();
                var ver = board.VerticalMirrored();
                var hor_ver = hor.VerticalMirrored();

                var tr = board.Transposed();
                var tr_hor = tr.HorizontalMirrored();
                var tr_ver = tr.VerticalMirrored();
                var tr_hor_ver = tr_hor.VerticalMirrored();

                var boards = new MirroredBoards(board);
                MirroredNeededBoards.Create(board, out Board b1, out Board b2, out Board b3, out Board b4);

                float e = result - Patterns.Sum(p => p.EvalForTraining(board, b1, b2, b3, b4));
                Array.ForEach(Patterns, p => Array.ForEach(boards.Boards, b => p.UpdataEvaluation(b, e * alpha)));

                if(board.stoneCount == 40)
                {
                    //Console.WriteLine(e);
                }
            }
        }
    }
}
