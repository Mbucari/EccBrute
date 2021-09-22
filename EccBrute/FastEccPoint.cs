using Org.BouncyCastle.Math.EC;
using System;

namespace EccBrute
{
	class FastEccPoint
	{
		public static long Q;

		public long X;
		public long Y;

		public FastEccPoint() { }

		public FastEccPoint(ECPoint eCPoint)
		{
			if (!eCPoint.IsNormalized())
				eCPoint.Normalize();

			X = eCPoint.XCoord.ToBigInteger().LongValueExact;
			Y = eCPoint.YCoord.ToBigInteger().LongValueExact;
		}

		public FastEccPoint Clone()
		{
			return new FastEccPoint { X = this.X, Y = this.Y };
		}

		public void Add(FastEccPoint b)
		{
			long H = ModSubtract(this.X, b.X);
			long R = ModSubtract(this.Y, b.Y);

			var HSquared = MulMod(H , H, Q);
			var G = MulMod(HSquared , H, Q);
			var V = MulMod(HSquared, this.X, Q);

			X = ModSubtract((MulMod(R, R, Q) + G) % Q, MulMod(2L, V, Q)) % Q;
			Y = MultiplyMinusProduct(ModSubtract(V, X), R, G, this.Y);

			var zInv = ModInverse(H);
			var zInv2 = MulMod(zInv, zInv, Q);
			var zInv3 = MulMod(zInv2, zInv, Q);

			X = MulMod(X, zInv2, Q);
			Y = MulMod(Y, zInv3, Q);
		}

		private static long ModSubtract(long x1, long x2)
		{
			if (x2 > x1)
				return x1 - x2 + Q;
			return x1 - x2;
		}

		private static long MultiplyMinusProduct(long baseNum, long b, long x, long y)
		{
			return ModSubtract(MulMod(baseNum, b, Q), MulMod(x, y, Q));
		}

		private static long ModInverse(long num)
		{
			ExtendedEuclideanAlgorithm(num, Q, out var x, out _);

			if (x < 0)
				x += Q;
			return x;
		}

		private static long ExtendedEuclideanAlgorithm(long m, long n, out long bez1, out long bez2)
		{
			long s = 0, t = 1, r = n, old_s = 1, old_t = 0, old_r = m;

			while (r != 0)
			{
				var quotient = old_r / r;

				var tmp = r;
				r = old_r - quotient * r;
				old_r = tmp;

				tmp = s;
				s = old_s - quotient * s;
				old_s = tmp;

				tmp = t;
				t = old_t - quotient * t;
				old_t = tmp;
			}

			bez1 = old_s;
			bez2 = old_t;
			return old_r;
		}

		/// <summary>
		/// Fast mulmod. Works for mod.bitlen <= 128 - (a * b).bitlen.  
		/// If a, b, and mod are same size, max supported size is 42 bits.
		/// </summary>
		static long MulMod(long a, long b, long mod)
		{
			var high = Math.BigMul((ulong)a, (ulong)b, out var low);
			var divTry = high;

			var shiftCount = 0;

			//Count how many bits are in the high qword
			for (; divTry > 0; shiftCount++)
				divTry >>= 1;

			//Shift number to fill high qword
			high = (high << (64 - shiftCount)) | (low >> shiftCount);

			var rem = high % (ulong)mod;
			var mask = (1UL << shiftCount) - 1;
			var newVal = (rem << shiftCount) | (mask & low);

			return (long)(newVal % (ulong)mod);
		}

		public override string ToString()
		{
			return $"({X:x},{Y:x})";
		}
	}
}
