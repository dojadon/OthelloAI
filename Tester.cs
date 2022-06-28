using System;
using System.Collections.Generic;
using System.Text;

namespace OthelloAI
{
    class Tester
    {
        public static Board CreateRnadomGame(Random rand, int num_moves)
        {
            Board Step(Board b)
            {
                Move[] moves = new Move(b).NextMoves();
                Move move = moves[rand.Next(moves.Length)];
                return move.reversed;
            }

            Board board = Board.Init;

            for(int i = 0; i < num_moves; i++)
            {
                board = Step(board);
            }

            return board;
        }
    }
}
