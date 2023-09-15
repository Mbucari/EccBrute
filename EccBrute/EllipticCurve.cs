using System;

namespace EccBrute
{

	internal record EllipticCurve
	{
		public long A { get; }
		public long B { get; }
		public long Prime { get; }
		public long Order { get; }
        public FastEccPoint Generator { get; }

		private readonly FastEccPoint[] cachedDoubles;

		public EllipticCurve(long a, long b, long prime, long order, long gx, long gy)
		{
			A = a;
			B = b;
			Prime = prime;
			Order = order;
            Generator = new FastEccPoint(gx, gy);

			if (!PointOnCurve(Generator))
				throw new Exception(@"Generator point {Generator} does not lie on curve.");

			var leadingZeros = 64 - (int)System.Runtime.Intrinsics.X86.Lzcnt.X64.LeadingZeroCount((ulong)Prime);
			cachedDoubles = new FastEccPoint[leadingZeros];
			cachedDoubles[0] = Generator;

			for (int i = 1; i < leadingZeros; i++)
			{
				(long xi, long yi) = Double(cachedDoubles[i - 1].X, cachedDoubles[i - 1].Y, Prime, A);
				cachedDoubles[i] = new FastEccPoint(xi, yi);
			}
		}

		//Double and add method. Cache doubles
		public FastEccPoint ScaleGenerator(long scalar)
		{
			long XM = 0, YM = 0, bitMask = 1;
			int bitPos = 0;

			while (bitMask < scalar)
			{
				if ((bitMask & scalar) == bitMask)
				{
					var cachedPt = cachedDoubles[bitPos];

					(XM, YM)
						= XM == 0 && YM == 0 ? (cachedPt.X, cachedPt.Y)
						: Add(XM, YM, cachedPt.X, cachedPt.Y, Prime, A);
				}
				bitMask <<= 1;
				bitPos++;
			}

			return new FastEccPoint(XM, YM);
		}


		public bool PointOnCurve(FastEccPoint point)
			=> (MulMod(MulMod(point.X, point.X, Prime), point.X, Prime) + MulMod(A, point.X, Prime) + B) % Prime == MulMod(point.Y, point.Y, Prime);

		//Double and Add algorithm
		public FastEccPoint Multiply(FastEccPoint point, long scalar)
		{
			long xi = point.X, yi = point.Y, XM = 0, YM = 0, bitPosition = 1;

			while (true)
			{
				if ((bitPosition & scalar) == bitPosition)
				{
					(XM, YM)
						= XM == 0 && YM == 0 ? (xi, yi)
						: Add(XM, YM, xi, yi, Prime, A);
				}

				bitPosition <<= 1;

				if (bitPosition > scalar)
					break;

				(xi, yi) = Double(xi, yi, Prime, A);
			}

			return new FastEccPoint(XM, YM);
		}

		public FastEccPoint Add(FastEccPoint p, FastEccPoint q)
		{
			var (xr, yr) = Add(p.X, p.Y, q.X, q.Y, Prime, A);
			return new FastEccPoint(xr, yr);
		}

		private static (long XR, long YR) Double(long XP, long YP, long Q, long A)
		{
			var m = MulMod((3 * MulMod(XP, XP, Q) + A) % Q, ModInverse((2 * YP) % Q, Q), Q);

			//Doubling can never result in a point at infinity because field is prime this odd.
			var msquared = MulMod(m, m, Q);

			var xr = ModSubtract(ModSubtract(msquared, XP, Q), XP, Q);
			var negYr = (YP + MulMod(m, ModSubtract(xr, XP, Q), Q)) % Q;
			var yr = ModSubtract(Q, negYr, Q);

			return (xr, yr);
		}

		//ht tps://andrea.corbellini.name/2015/05/23/elliptic-curve-cryptography-finite-fields-and-discrete-logarithms/#algebraic-sum
		private static (long XR, long YR) Add(long XP, long YP, long XQ, long YQ, long Q, long A)
		{
			if (XP == XQ && YP == YQ) return Double(XP, YP, Q, A);

			var m = MulMod(ModSubtract(YP, YQ, Q), ModInverse(ModSubtract(XP, XQ, Q), Q), Q);

			if (m == 0) return (0, 0);

			var msquared = MulMod(m, m, Q);

			var xr = ModSubtract(ModSubtract(msquared, XP, Q), XQ, Q);
			var negYr = (YP + MulMod(m, ModSubtract(xr, XP, Q), Q)) % Q;
			var yr = ModSubtract(Q, negYr, Q);

			return (xr, yr);
		}

		private static long ModSubtract(long x1, long x2, long Q)
			=> x2 > x1 ? x1 - x2 + Q : x1 - x2;

		//Stripped down extended euclidean algorithm
		public static long ModInverse(long a, long n)
		{
			long t = 0, r = n, newt = 1, newr = a;

			while (newr != 0)
			{
				var quotient = r / newr;

				(t, newt) = (newt, t - quotient * newt);
				(r, newr) = (newr, r - quotient * newr);
			}

			if (t < 0)
				t += n;

			return t;
		}

		/// <summary>
		/// Fast mulmod. Works for bitlen(a * b) + bitlen(mod) <= 128.  
		/// If a, b, and mod are same size, max supported size is 42 bits.
		/// </summary>
		private static long MulMod(long a, long b, long mod)
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
	}
}
