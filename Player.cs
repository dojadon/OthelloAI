using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI
{
    public abstract class Player
    {
        public abstract Move DecideMove(Board board, int stone);
    }

    public class PlayerManual : Player
    {
        public override Move DecideMove(Board board, int stone)
        {
            if(board.GetMoves(stone) == 0)
            {
                return new Move(0UL);
            }

            string s = Console.ReadLine();

            int x = (int) char.GetNumericValue(s[0]);
            int y = (int) char.GetNumericValue(s[1]);

            return new Move(x, y);
        }
    }
}
