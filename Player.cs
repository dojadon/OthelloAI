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
