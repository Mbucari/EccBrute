using System;
using System.Numerics;

namespace EccBrute
{
	class FastEccPoint : IComparable<FastEccPoint>
	{
		public static long Prime { get; private set; }
		public static long A { get; private set; }
		public long X { get; }
		public long Y { get; }

		public FastEccPoint(long x, long y)
		{
			X = x;
			Y = y;
		}
		public FastEccPoint(long x, long y, long prime, long a) : this(x, y)
		{
			Prime = prime;
			A = a;
		}

		public int CompareTo(FastEccPoint other)
		{
			var xcomp = X.CompareTo(other.X);
			if (xcomp == 0)
				return Y.CompareTo(other.Y);
			else return xcomp;
		}

		public FastEccPoint Clone()
		{
			return new FastEccPoint(X, Y);
		}

		public static FastEccPoint operator +(FastEccPoint p, FastEccPoint q)
		{
			var (xr, yr) = Add(p.X, p.Y, q.X, q.Y, Prime);
			return new FastEccPoint(xr, yr);
		}
		public static FastEccPoint operator *(FastEccPoint p, long scalar) => p.Multiply(scalar);
		public static FastEccPoint operator *(long scalar, FastEccPoint p) => p.Multiply(scalar);

		//Double and Add algorithm
		private FastEccPoint Multiply(long scalar)
		{
			long xi = X, yi = Y, XM = 0, YM = 0, bitPosition = 1;

			while(bitPosition < scalar)
			{
				if ((bitPosition & scalar) == bitPosition)
				{
					(XM, YM) =
						XM == 0 && YM == 0
						? (xi, yi)
						: Add(XM, YM, xi, yi, Prime);
				}

				(xi, yi) = Double(xi, yi, Prime, A);
				bitPosition <<= 1;
			}

			return new FastEccPoint(XM, YM);
		}

		private static (long XR, long YR) Double(long XP, long YP, long Q, long a)
		{
			var m = MulMod((3 * MulMod(XP, XP, Q) + a) % Q, ModInverse((2 * YP) % Q, Q), Q);
			var msquared = MulMod(m, m, Q);

			var xr = ModSubtract(ModSubtract(msquared, XP, Q), XP, Q);
			var negYr = (YP + MulMod(m, ModSubtract(xr, XP, Q), Q)) % Q;
			var yr = ModSubtract(Q, negYr, Q);

			return (xr, yr);
		}

		//ht tps://andrea.corbellini.name/2015/05/23/elliptic-curve-cryptography-finite-fields-and-discrete-logarithms/#algebraic-sum
		private static (long XR, long YR) Add(long XP, long YP, long XQ, long YQ, long Q)
		{
			var m = MulMod(ModSubtract(YP, YQ, Q), ModInverse(ModSubtract(XP, XQ, Q), Q), Q);
			var msquared = MulMod(m, m, Q);

			var xr = ModSubtract(ModSubtract(msquared, XP, Q), XQ, Q);
			var negYr = (YP + MulMod(m, ModSubtract(xr, XP, Q), Q)) % Q;
			var yr = ModSubtract(Q, negYr, Q);

			return (xr, yr);
		}

		private static long ModSubtract(long x1, long x2, long Q)
			=> x2 > x1 ? x1 - x2 + Q : x1 - x2;

		//Stripped down extended euclidean algorithm
		private static long ModInverse(long num, long Q)
		{
			long s = 0, r = Q, old_s = 1, old_r = num;

			while (r != 0)
			{
				var quotient = old_r / r;

				var tmp = r;
				r = old_r - quotient * r;
				old_r = tmp;

				tmp = s;
				s = old_s - quotient * s;
				old_s = tmp;
			}

			if (old_s < 0)
				old_s += Q;
			return old_s;
		}

		/// <summary>
		/// Fast mulmod. Works for bitlen(a * b) + bitlen(mod) <= 128.  
		/// If a, b, and mod are same size, max supported size is 42 bits.
		/// </summary>
		static long MulMod(long a, long b, long mod)
		{
			var high = Math.BigMul((ulong)a, (ulong)b, out var low);

			//Count how many bits are in the high qword
			var shiftCount = 64 - (int)System.Runtime.Intrinsics.X86.Lzcnt.X64.LeadingZeroCount(high);

			//Shift number to fill high qword
			high = (high << (64 - shiftCount)) | (low >> shiftCount);

			var rem = high % (ulong)mod;
			var mask = (1UL << shiftCount) - 1;
			var newVal = (rem << shiftCount) | (mask & low);

			return (long)(newVal % (ulong)mod);
		}
		
		static long MulMod2(long a, long b, long mod)
		{
			var aBig = new BigInteger(a);
			var bBig = new BigInteger(b);
			return (long)((aBig * bBig) % mod);
		}
		public override string ToString()
		{
			return $"({X:x},{Y:x})";
		}
	}
}
