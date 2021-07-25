using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI
{
	public readonly struct Move : IComparable<Move>
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

		public Move(Board reversed)
		{
			move = 0;
			this.reversed = reversed;
			moves = reversed.GetMoves();
			count = Board.BitCount(moves);
		}

		public Move(Board reversed, ulong move, ulong moves, int count)
		{
			this.move = move;
			this.reversed = reversed;
			this.moves = moves;
			this.count = count;
		}

		public int CompareTo([AllowNull] Move other)
        {
			return count - other.count;
        }
    }
}
