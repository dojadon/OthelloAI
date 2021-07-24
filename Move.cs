using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI
{
	public readonly struct Move
	{
		public readonly ulong move;
		public readonly Board reversed;
		public readonly ulong moves;
		public readonly int count;
		
		public Move(Board board, ulong move)
        {
			this.move = move;
			reversed = board.Reversed(move);
			moves = reversed.GetMoves();
			count = Board.BitCount(moves);
        }
    }
}
