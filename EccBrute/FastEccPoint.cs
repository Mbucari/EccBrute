using System;

namespace EccBrute
{
	struct FastEccPoint : IComparable<FastEccPoint>
	{
		private long _X;
		private long _Y;
		public long X { get => _X; set => _X = value; }
		public long Y { get => _Y; set => _Y = value; }

		public FastEccPoint(long x, long y)
		{
			X = x;
			Y = y;
		}

		public override int GetHashCode()
		{
			HashCode hc =default;
			hc.Add(X);
			hc.Add(Y);
			return hc.ToHashCode();
		}

		public bool PointAtInfinity => X == 0 && Y == 0;

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

		public override string ToString()
		{
			return PointAtInfinity ? "Infinity" : $"({X:x},{Y:x})";
		}
	}
}
