using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI
{
    public readonly struct BitsEnumerable : IEnumerable<ulong>
    {
        ulong Bits { get; }

        public BitsEnumerable(ulong bits)
        {
            this.Bits = bits;
        }

        public IEnumerator<ulong> GetEnumerator()
        {
            return new BitsEnumerator(Bits);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        struct BitsEnumerator : IEnumerator<ulong>
        {
            private ulong Bits { get; set; }
            public ulong Current { get; private set; }
            object IEnumerator.Current => Current;

            public BitsEnumerator(ulong bits)
            {
                Bits = bits;
                Current = 0;
            }

            public bool MoveNext()
            {
                Current = Board.NextMove(Bits);

                if (Current == 0)
                {
                    return false;
                }

                Bits = Board.RemoveMove(Bits, Current);
                return true;
            }

            public void Reset()
            {
                Bits = 0;
                Current = 0;
            }

            public void Dispose()
            {
            }
        }
    }
}
