using System;

namespace EccBrute
{
	class PublicKey
	{
		public long X;
		public long Y;
		public int Size;

		public static PublicKey Parse(string beEncoded)
		{
			var bytes = Convert.FromBase64String(beEncoded);

			int size = bytes.Length;

			var b1 = new byte[size / 2];
			var b2 = new byte[size / 2];
			Array.Copy(bytes, 0, b1, 0, b1.Length);
			Array.Copy(bytes, b1.Length, b2, 0, b2.Length);

			var publixX = (long)(new System.Numerics.BigInteger(b1, true, true));
			var publixY = (long)(new System.Numerics.BigInteger(b2, true, true));

			return new PublicKey { X = publixX, Y = publixY, Size = size };
		}

		public string ToBase64BEString()
		{
			var b1 = new System.Numerics.BigInteger(X).ToByteArray(true, true);
			var b2 = new System.Numerics.BigInteger(Y).ToByteArray(true, true);

			var bytes = new byte[Size];

			Array.Copy(b1, 0, bytes, Size / 2 - b1.Length, b1.Length);
			Array.Copy(b2, 0, bytes, Size - b2.Length, b2.Length);

			return Convert.ToBase64String(bytes);
		}
	}
}
