using System;

namespace EccBrute
{
	class PublicKey
    {
        public long X { get; }
        public long Y { get; }

        public PublicKey(long X, long Y)
        {
            this.X = X;
            this.Y = Y;
        }


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


            return new PublicKey(publixX, publixY);
        }

		public string ToBase64BEString()
		{
			var b1 = new System.Numerics.BigInteger(X).ToByteArray(true, true);
			var b2 = new System.Numerics.BigInteger(Y).ToByteArray(true, true);

			var size = 2 * int.Max(b1.Length, b2.Length);
			var bytes = new byte[size];

			Array.Copy(b1, 0, bytes, size / 2 - b1.Length, b1.Length);
			Array.Copy(b2, 0, bytes, size - b2.Length, b2.Length);

			return Convert.ToBase64String(bytes);
		}
	}
}
