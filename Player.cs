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
}
