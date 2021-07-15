using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI
{
    readonly struct U64_4
    {
        public readonly Vector256<ulong> data;

        public U64_4(Vector256<ulong> data)
        {
            this.data = data;
        }

        public U64_4(ulong ul)
        {
            this.data = Vector256.Create(ul);
        }

        public U64_4(ulong ul1, ulong ul2, ulong ul3, ulong ul4)
        {
            this.data = Vector256.Create(ul1, ul2, ul3, ul4);
        }

        public static U64_4 AndNot(in U64_4 left, in U64_4 right) => Avx2.AndNot(left.data, right.data);

        public  U64_4 ShiftLeft(in U64_4 right) => Avx2.ShiftLeftLogicalVariable(this.data, right.data);
        public  U64_4 ShiftRight(in U64_4 right) => Avx2.ShiftRightLogicalVariable(this.data, right.data);

        public static U64_4 NonZero(in U64_4 left) => Avx2.CompareEqual(left.data, Vector256.Create(0UL)) + new U64_4(1);

        public static U64_4 operator >>(in U64_4 l, in int n) => Avx2.ShiftLeftLogical(l.data, (byte) n);
        public static U64_4 operator <<(in U64_4 l, in int n) => Avx2.ShiftRightLogical(l.data, (byte) n);

        public static U64_4 operator &(in U64_4 le, in U64_4 ri) => Avx2.And(le.data, ri.data);
        public static U64_4 operator |(in U64_4 le, in U64_4 ri) => Avx2.Or(le.data, ri.data);

        public static U64_4 operator +(in U64_4 le, in U64_4 ri) => Avx2.Add(le.data, ri.data);
        public static U64_4 operator +(in U64_4 le, in ulong ri) => Avx2.Add(le.data, Vector256.Create(ri));
        public static U64_4 operator -(in U64_4 le, in U64_4 ri) => Avx2.Subtract(le.data, ri.data);
        public static U64_4 operator -(in U64_4 le) => Avx2.Subtract(Vector256.Create(0UL), le.data);

        public static U64_4 operator ~(in U64_4 le) => Avx2.AndNot(Vector256.Create((byte)0xFF).AsUInt64(), le.data);

        public static implicit operator U64_4(in  Vector256<ulong> data) => new U64_4(data);
    }
}
