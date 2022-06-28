using Org.BouncyCastle.Math.EC;
using System;
using System.Buffers;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace EccBrute
{
	unsafe class FastEccPoint
	{
		public static ulong Q;

		public long[] FourPointsXPub
		{
			get
			{
				var longArr = new long[4];
				longArr[0] = FourPointsX.GetElement(0);
				longArr[1] = FourPointsX.GetElement(1);
				longArr[2] = FourPointsX.GetElement(2);
				longArr[3] = FourPointsX.GetElement(3);
				return longArr;
			}
			set
			{
				FourPointsX = Vector256.Create(value[0], value[1], value[2], value[3]);
			}
		}
		public long[] FourPointsYPub
		{
			get
			{
				var longArr = new long[4];
				longArr[0] = FourPointsY.GetElement(0);
				longArr[1] = FourPointsY.GetElement(1);
				longArr[2] = FourPointsY.GetElement(2);
				longArr[3] = FourPointsY.GetElement(3);
				return longArr;
			}
			set
			{
				FourPointsY = Vector256.Create(value[0], value[1], value[2], value[3]);
			}
		}

		public Vector256<long> FourPointsX;
		public Vector256<long> FourPointsY;

		private readonly Vector256<ulong> Vec64;
		private readonly Vector256<long> Ones;
		private readonly Vector256<long> Twos;
		private readonly Vector256<ulong> UOnes;
		private readonly Vector256<long> QVector;

		private readonly MemoryHandle hbuff256A;
		private readonly MemoryHandle hbuff256B;
		private readonly MemoryHandle hHigh;
		private readonly MemoryHandle hLow;
		private readonly MemoryHandle hShifts;
		private readonly MemoryHandle hRems;
		private readonly MemoryHandle hUNewVals;
		private readonly MemoryHandle hNewVals;
		private readonly MemoryHandle hQuotXr;
		private readonly MemoryHandle hQuotXs;

		private readonly long* pbuff256A;
		private readonly long* pbuff256B;
		private readonly ulong* pHigh;
		private readonly ulong* pLow;
		private readonly ulong* pShifts;
		private readonly ulong* pRems;
		private readonly ulong* pUNewVals;
		private readonly long* pNewVals;
		private readonly long* pQuotXr;
		private readonly long* pQuotXs;

		public FastEccPoint()
		{
			Memory<long> buff256A = new long[4];
			Memory<long> buff256B = new long[4];
			Memory<ulong> high = new ulong[4];
			Memory<ulong> low = new ulong[4];
			Memory<ulong> shifts = new ulong[4];
			Memory<ulong> rems = new ulong[4];
			Memory<ulong> uNewVals = new ulong[4];
			Memory<long> newVals = new long[4];
			Memory<long> QuotXr = new long[4];
			Memory<long> QuotXs = new long[4];

			hbuff256A = buff256A.Pin();
			hbuff256B = buff256B.Pin();
			hHigh = high.Pin();
			hLow = low.Pin();
			hShifts = shifts.Pin();
			hRems = rems.Pin();
			hUNewVals = uNewVals.Pin();
			hNewVals = newVals.Pin();
			hQuotXr = QuotXr.Pin();
			hQuotXs = QuotXs.Pin();

			pbuff256A = (long*)hbuff256A.Pointer;
			pbuff256B = (long*)hbuff256B.Pointer;
			pHigh = (ulong*)hHigh.Pointer;
			pLow = (ulong*)hLow.Pointer;
			pShifts = (ulong*)hShifts.Pointer;
			pRems = (ulong*)hRems.Pointer;
			pUNewVals = (ulong*)hUNewVals.Pointer;
			pNewVals = (long*)hNewVals.Pointer;
			pQuotXr = (long*)hQuotXr.Pointer;
			pQuotXs = (long*)hQuotXs.Pointer;

			Vec64 = Vector256.Create(64UL, 64UL, 64UL, 64UL);
			UOnes = Vector256.Create(1UL, 1UL, 1UL, 1UL);
			Ones = Vector256.Create(1L, 1L, 1L, 1L);
			Twos = Vector256.Create(2L, 2L, 2L, 2L);
			QVector = Vector256.Create((long)Q, (long)Q, (long)Q, (long)Q);
		}
		public FastEccPoint(ECPoint eCPoint1, ECPoint eCPoint2, ECPoint eCPoint3, ECPoint eCPoint4) : this()
		{
			if (!eCPoint1.IsNormalized())
				eCPoint1.Normalize();

			if (!eCPoint2.IsNormalized())
				eCPoint2.Normalize();

			if (!eCPoint3.IsNormalized())
				eCPoint3.Normalize();

			if (!eCPoint4.IsNormalized())
				eCPoint4.Normalize();

			FourPointsX = Vector256.Create
				(
					eCPoint1.XCoord.ToBigInteger().LongValueExact,
					eCPoint2.XCoord.ToBigInteger().LongValueExact,
					eCPoint3.XCoord.ToBigInteger().LongValueExact,
					eCPoint4.XCoord.ToBigInteger().LongValueExact
				);

			FourPointsY = Vector256.Create
				(
					eCPoint1.YCoord.ToBigInteger().LongValueExact,
					eCPoint2.YCoord.ToBigInteger().LongValueExact,
					eCPoint3.YCoord.ToBigInteger().LongValueExact,
					eCPoint4.YCoord.ToBigInteger().LongValueExact
				);
		}

		public FastEccPoint Clone()
		{
			Avx.Store(pQuotXr, FourPointsX);
			Avx.Store(pQuotXs, FourPointsY);

			return new FastEccPoint { FourPointsX = Avx.LoadVector256(pQuotXr), FourPointsY = Avx.LoadVector256(pQuotXs) };
		}

		public void Add(FastEccPoint b)
		{
			var hvec = ModSubtract(FourPointsX, b.FourPointsX);
			var rvec = ModSubtract(FourPointsY, b.FourPointsY);

			var hsquaredvec = MulMod(hvec, hvec, Q);
			var gvec = MulMod(hsquaredvec, hvec, Q);
			var vvec = MulMod(hsquaredvec, FourPointsX, Q);


			FourPointsX = ModSubtract(Mod(Avx2.Add(MulMod(rvec, rvec, Q), gvec), (long)Q), Mod(MulMod(Twos, vvec, Q), (long)Q));
			FourPointsY = MultiplyMinusProduct(ModSubtract(vvec, FourPointsX), rvec, gvec, FourPointsY);

			var ZINV = ModInverse(hvec);
			var ZINV2 = MulMod(ZINV, ZINV, Q);
			var ZINV3 = MulMod(ZINV2, ZINV, Q);

			FourPointsX = MulMod(FourPointsX, ZINV2, Q);
			FourPointsY = MulMod(FourPointsY, ZINV3, Q);
		}
		private Vector256<long> Mod(Vector256<long> x1, long mod)
		{
			Avx.Store(pQuotXr, x1);

			*pNewVals = *pQuotXr % mod;
			*(pNewVals + 1) = *(pQuotXr + 1) % mod;
			*(pNewVals + 2) = *(pQuotXr + 2) % mod;
			*(pNewVals + 3) = *(pQuotXr + 3) % mod;

			return Avx.LoadVector256(pNewVals);
		}

		private Vector256<long> ModSubtract(Vector256<long> x1, Vector256<long> x2)
		{
			var diff = Avx2.Subtract(x1, x2);

			return Avx2.Add(diff, Avx2.And(Avx2.CompareGreaterThan(Vector256<long>.Zero, diff), QVector));
		}

		private Vector256<long> MultiplyMinusProduct(Vector256<long> baseNum, Vector256<long> b, Vector256<long> x, Vector256<long> y)
		{
			return ModSubtract(MulMod(baseNum, b, Q), MulMod(x, y, Q));
		}

		//ExtendedEuclideanAlgorithm
		private Vector256<long> ModInverse2(Vector256<long> m)
		{
			Vector256<long> r = QVector, s = Vector256<long>.Zero, old_r = m, old_s = Ones;

			var pQuotient = pNewVals;
			while (!Avx.TestZ(r, r))
			{
				Avx.Store(pbuff256A, old_r);

				var cmp = Avx2.CompareGreaterThan(Ones, r);

				Avx.Store(pbuff256B, Avx2.Or(cmp, r));

				cmp = Avx2.CompareGreaterThan(r, Vector256<long>.Zero);

				*pQuotient = *pbuff256A / *pbuff256B;
				*(pQuotient + 1) = *(pbuff256A + 1) / *(pbuff256B + 1);
				*(pQuotient + 2) = *(pbuff256A + 2) / *(pbuff256B + 2);
				*(pQuotient + 3) = *(pbuff256A + 3) / *(pbuff256B + 3);

				*pQuotXr = *pQuotient * *pbuff256B;
				*(pQuotXr + 1) = *(pQuotient + 1) * *(pbuff256B + 1);
				*(pQuotXr + 2) = *(pQuotient + 2) * *(pbuff256B + 2);
				*(pQuotXr + 3) = *(pQuotient + 3) * *(pbuff256B + 3);

				var tmp = r;
				r = Avx2.And(cmp, Avx2.Subtract(old_r, Avx2.And(cmp, Avx.LoadVector256(pQuotXr))));
				old_r = tmp;

				cmp = Avx2.CompareGreaterThan(r, Vector256<long>.Zero);

				Avx.Store(pbuff256B, s);

				*pQuotXs = *pQuotient * *pbuff256B;
				*(pQuotXs + 1) = *(pQuotient + 1) * *(pbuff256B + 1);
				*(pQuotXs + 2) = *(pQuotient + 2) * *(pbuff256B + 2);
				*(pQuotXs + 3) = *(pQuotient + 3) * *(pbuff256B + 3);

				tmp = s;
				s = Avx2.Or(Avx2.AndNot(cmp, s), Avx2.And(cmp, Avx2.Subtract(old_s, Avx.LoadVector256(pQuotXs))));
				old_s = tmp;
			}

			var adding = Avx2.And(Avx2.CompareGreaterThan(Vector256<long>.Zero, old_s), QVector);

			return Avx2.Add(old_s, adding);
		}
		//ExtendedEuclideanAlgorithm
		private Vector256<long> ModInverse(Vector256<long> m)
		{
			Vector256<long> r = QVector, s = Vector256<long>.Zero, old_r = m, old_s = Ones;

			var pQuotient = pNewVals;
			while (!Avx.TestZ(r, r))
			{
				Avx.Store(pbuff256A, old_r);
				Avx.Store(pbuff256B, r);

				*pQuotient = *pbuff256B == 0 ? 0 : *pbuff256A / *pbuff256B;
				*(pQuotient + 1) = *(pbuff256B + 1) == 0 ? 0 : *(pbuff256A + 1) / *(pbuff256B + 1);
				*(pQuotient + 2) = *(pbuff256B + 2) == 0 ? 0 : *(pbuff256A + 2) / *(pbuff256B + 2);
				*(pQuotient + 3) = *(pbuff256B + 3) == 0 ? 0 : *(pbuff256A + 3) / *(pbuff256B + 3);

				*pQuotXr = *pQuotient * *pbuff256B;
				*(pQuotXr + 1) = *(pQuotient + 1) * *(pbuff256B + 1);
				*(pQuotXr + 2) = *(pQuotient + 2) * *(pbuff256B + 2);
				*(pQuotXr + 3) = *(pQuotient + 3) * *(pbuff256B + 3);

				var cmp = Avx2.CompareGreaterThan(r, Vector256<long>.Zero);

				var tmp = r;
				r = Avx2.And(cmp, Avx2.Subtract(old_r, Avx.LoadVector256(pQuotXr)));
				old_r = tmp;

				cmp = Avx2.CompareGreaterThan(r, Vector256<long>.Zero);

				Avx.Store(pbuff256B, s);

				*pQuotXs = *pQuotient * *pbuff256B;
				*(pQuotXs + 1) = *(pQuotient + 1) * *(pbuff256B + 1);
				*(pQuotXs + 2) = *(pQuotient + 2) * *(pbuff256B + 2);
				*(pQuotXs + 3) = *(pQuotient + 3) * *(pbuff256B + 3);

				tmp = s;
				s = Avx2.Or(Avx2.AndNot(cmp, s), Avx2.And(cmp, Avx2.Subtract(old_s, Avx.LoadVector256(pQuotXs))));
				old_s = tmp;
			}

			var adding = Avx2.And(Avx2.CompareGreaterThan(Vector256<long>.Zero, old_s), QVector);

			return Avx2.Add(old_s, adding);
		}


		/// <summary>
		/// Fast mulmod. Works for (a * b).bitlen + mod.bitlen <= 128.  
		/// If a, b, and mod are same size, max supported size is 42 bits.
		/// </summary>
		Vector256<long> MulMod(Vector256<long> a, Vector256<long> b, ulong mod)
		{
			Avx.Store(pbuff256A, a);
			Avx.Store(pbuff256B, b);

			var pbuffaU = (ulong*)pbuff256A;
			var pbuffbU = (ulong*)pbuff256B;

			*pHigh = Math.BigMul(*pbuffaU, *pbuffbU, out *pLow);
			*(pHigh + 1) = Math.BigMul(*(pbuffaU + 1), *(pbuffbU + 1), out *(pLow + 1));
			*(pHigh + 2) = Math.BigMul(*(pbuffaU + 2), *(pbuffbU + 2), out *(pLow + 2));
			*(pHigh + 3) = Math.BigMul(*(pbuffaU + 3), *(pbuffbU + 3), out *(pLow + 3));

			var HighVec = Avx.LoadVector256(pHigh);
			var LowVec = Avx.LoadVector256(pLow);

			*pShifts = Lzcnt.X64.LeadingZeroCount(*pHigh);
			*(pShifts + 1) = Lzcnt.X64.LeadingZeroCount(*(pHigh + 1));
			*(pShifts + 2) = Lzcnt.X64.LeadingZeroCount(*(pHigh + 2));
			*(pShifts + 3) = Lzcnt.X64.LeadingZeroCount(*(pHigh + 3));

			var ShiftsVec = Avx.LoadVector256(pShifts);
			var negShifts = Avx2.Subtract(Vec64, ShiftsVec);

			Avx.Store(pHigh, Avx2.Or(Avx2.ShiftLeftLogicalVariable(HighVec, ShiftsVec), Avx2.ShiftRightLogicalVariable(LowVec, negShifts)));

			*pRems = *pHigh % mod;
			*(pRems + 1) = *(pHigh + 1) % mod;
			*(pRems + 2) = *(pHigh + 2) % mod;
			*(pRems + 3) = *(pHigh + 3) % mod;

			var newValues = Avx2.Or(Avx2.ShiftLeftLogicalVariable(Avx.LoadVector256(pRems), negShifts), Avx2.And(Avx2.Subtract(Avx2.ShiftLeftLogicalVariable(UOnes, negShifts), UOnes), LowVec));

			Avx.Store(pUNewVals, newValues);

			var pNewValsU = (ulong*)pNewVals;
			*pNewValsU = *pUNewVals % mod;
			*(pNewValsU + 1) = *(pUNewVals + 1) % mod;
			*(pNewValsU + 2) = *(pUNewVals + 2) % mod;
			*(pNewValsU + 3) = *(pUNewVals + 3) % mod;

			return Avx.LoadVector256(pNewVals);
		}

		~FastEccPoint()
		{
			hbuff256A.Dispose();
			hbuff256B.Dispose();
			hHigh.Dispose();
			hLow.Dispose();
			hShifts.Dispose();
			hRems.Dispose();
			hUNewVals.Dispose();
			hNewVals.Dispose();
			hQuotXr.Dispose();
			hQuotXs.Dispose();
		}

		public override string ToString()
		{
			return $"({FourPointsX},{FourPointsY})";
		}
	}
}
