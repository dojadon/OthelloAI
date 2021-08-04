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
		public readonly int n_moves;
		
		public Move(Board board, ulong move)
        {
			this.move = move;
			reversed = board.Reversed(move);
			moves = reversed.GetMoves();
			n_moves = Board.BitCount(moves);
        }

		public Move(Board reversed)
		{
			move = 0;
			this.reversed = reversed;
			moves = reversed.GetMoves();
			n_moves = Board.BitCount(moves);
		}

		public Move(Board reversed, ulong move, ulong moves, int count)
		{
			this.move = move;
			this.reversed = reversed;
			this.moves = moves;
			this.n_moves = count;
		}

		public Move[] NextMoves()
		{
			ulong moves_tmp = moves;

			Move[] array = new Move[n_moves];
			for (int i = 0; i < array.Length; i++)
			{
				ulong move = Board.NextMove(moves_tmp);
				moves_tmp = Board.RemoveMove(moves_tmp, move);
				array[i] = new Move(reversed, move);
			}
			return array;
		}

		public Move[] OrderedNextMoves()
        {
			Move[] moves = NextMoves();
			Array.Sort(moves);
			return moves;
        }

		public int CompareTo([AllowNull] Move other)
        {
			return n_moves - other.n_moves;
        }
    }
}
