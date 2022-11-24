using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI
{
    public abstract class Player
    {
        public abstract (int x, int y, ulong move) DecideMove(Board board, int stone);
    }

    public class PlayerRandom : Player
    {
        Random Random { get; }

        public PlayerRandom(Random random)
        {
            Random = random;
        }

        public override (int x, int y, ulong move) DecideMove(Board board, int stone)
        {
            ulong moves = board.GetMoves(stone);

            int n = Board.BitCount(moves);

            if (n == 0)
                return (0, 0, 0);

            int index = Random.Next(n);

            ulong move = 0;

            for(int i = 0; i < index + 1; i++)
            {
                move = Board.NextMove(moves);
                moves = Board.RemoveMove(moves, move);
            }

            (int x, int y) = Board.ToPos(move);

            return (x, y, move);
        }
    }

    public class PlayerManual : Player
    {
        public override (int x, int y, ulong move) DecideMove(Board board, int stone)
        {
            ulong moves = board.GetMoves(stone);

            while (moves != 0)
            {
                string s = Console.ReadLine();

                int x = (int)char.GetNumericValue(s[0]);
                int y = (int)char.GetNumericValue(s[1]);

                ulong move = Board.Mask(x, y);

                if ((move & moves) != 0)
                    return (x, y, move);
            }

            return (-1, -1, 0);
        }
    }
}
