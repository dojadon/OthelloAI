using System;
using System.Collections.Generic;
using System.Text;

namespace OthelloAI
{
    class Tester
    {
        static readonly Random Random = new Random();

        public static Board CreateRnadomGame(int num_moves)
        {
            static Board Step(Board b)
            {
                Move[] moves = new Move(b).NextMoves();
                Move move = moves[Random.Next(moves.Length)];
                return move.reversed;
            }

            Board board = Board.Init;

            for(int i = 0; i < num_moves; i++)
            {
                board = Step(board);
            }

            return board;
        }

        public static void TestInRandomGames(PlayerAI player)
        {

        }
    }
}
