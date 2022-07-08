using System;

namespace EccBrute
{
	internal class PrivateKey
	{
		public long Key;
		public static PrivateKey Parse(string beEncoded)
		{
			var bytes = Convert.FromBase64String(beEncoded);

			var privKey = (long)(new System.Numerics.BigInteger(bytes, true, true));

			return new PrivateKey { Key = privKey };
		}

		public string ToBase64BEString()
		{
			var bytes = new System.Numerics.BigInteger(Key).ToByteArray(false, true);

			var full = new byte[bytes.Length + 2];
			full[0] = 2;
			full[1] = (byte)bytes.Length;
			Array.Copy(bytes, 0, full, 2, bytes.Length);
			return Convert.ToBase64String(full);
		}
	}
}
