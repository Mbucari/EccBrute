using Org.BouncyCastle.Math.EC;
using System;

namespace EccBrute
{
	[Serializable]
	class FastEccPoint
	{
		public static long Q;
		public static long Curve_A;
		public static long NegCurve_A;
		public static bool BitLengthsDiff;

		public long X;
		public long Y;
		public long Z0;
		public long Z1;

		public FastEccPoint() { }

		public FastEccPoint(ECPoint eCPoint)
		{
			X = eCPoint.XCoord.ToBigInteger().LongValueExact;
			Y = eCPoint.YCoord.ToBigInteger().LongValueExact;
			Z0 = eCPoint.GetZCoord(0).ToBigInteger().LongValueExact;
			Z1 = eCPoint.GetZCoord(1).ToBigInteger().LongValueExact;
		}

		public FastEccPoint Clone()
		{
			return new FastEccPoint { X = this.X, Y = this.Y, Z0 = this.Z0, Z1 = this.Z1 };
		}

		public void Add(FastEccPoint b)
		{
			long H = ModSubtract(this.X, b.X);
			long R = ModSubtract(this.Y, b.Y);

			var HSquared = MultMod(H , H, Q);
			var G = MultMod(HSquared , H, Q);
			var V = MultMod(HSquared, this.X, Q);

			X = ModSubtract((MultMod(R, R, Q) + G) % Q, MultMod(2L, V, Q)) % Q;
			Y = MultiplyMinusProduct(ModSubtract(V, X), R, G, this.Y);
			Z0 = H;
			Z1 = CalculateJacobianModifiedW(HSquared); 

			Normalize();
		}

		public void Normalize()
		{
			var zInv = ModInverse(Z0);

			var zInv2 = MultMod(zInv, zInv, Q);
			var zInv3 = MultMod(zInv2, zInv , Q);

			X = MultMod(X , zInv2, Q);
			Y = MultMod(Y, zInv3, Q);
			Z0 = 1;
			Z1 = Curve_A;
		}

		private static long ModSubtract(long x1, long x2)
		{
			if (x2 > x1)
				return x1 - x2 + Q;
			return x1 - x2;
		}
		private static long MultiplyMinusProduct(long baseNum, long b, long x, long y)
		{
			return ModSubtract(MultMod(baseNum , b,  Q), MultMod(x , y, Q));
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

		private static long CalculateJacobianModifiedW(long zSquared)
		{
			var W = MultMod(zSquared , zSquared, Q);

			if (BitLengthsDiff)
			{
				W = -(MultMod(W , NegCurve_A , Q));
			}
			else
			{
				W = MultMod(W , Curve_A, Q);
			}
			return W;
		}

		static long MultMod(long x, long y, long b)
		{
			var high = Math.BigMul((ulong)x, (ulong)y, out var low);
			var divTry = high;

			var shiftCount = 0;

			for (; divTry > 0; shiftCount++)
				divTry >>= 1;

			divTry = (high << (64 - shiftCount)) | (low >> shiftCount);

			var mask = (1UL << shiftCount) - 1;
			var rem = divTry % (ulong)b;
			var newVal = (rem << shiftCount) | (mask & low);

			return (long)(newVal % (ulong)b);
		}

		public override string ToString()
		{
			return $"({X:x},{Y:x},{Z0:x},{Z1:x})";
		}
	}
}
