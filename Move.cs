using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI
{
	public struct Move
	{
        public int x, y;
        public ulong move;

		public Move(int x, int y)
		{
			this.x = x;
			this.y = y;
			this.move = Board.Mask(x, y);
		}

		public Move(int x)
		{
			this.x = x / 8;
			this.y = x % 8;
			this.move = Board.Mask(x);
		}

		public Move(ulong move)
		{
			this.move = move;
            x = y = -1;

            if (move == 0)
			{
                return;
			}

			for (int i = 0; i < 64; i++)
			{
				if (((move >> i) & 1) == 1)
				{
					x = i / 8;
					y = i % 8;

					break;
				}
			}
		}

        public override string ToString()
        {
            return $"{x}, {y}";
        }
    }
}
