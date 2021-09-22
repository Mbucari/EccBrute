using System;

namespace EccBrute
{
	class KeyPair
	{
		public PublicKey PublicKey;
		public long PrivateKey;

		public string PrivateKeyToAsn1String()
		{
			var pkBytes = new System.Numerics.BigInteger(PrivateKey).ToByteArray(true, true);
			Array.Reverse(pkBytes);

			int keySize = Math.Max(PublicKey.Size / 2, pkBytes.Length);

			byte[] provKey = new byte[2 + keySize];
			provKey[0] = 2;
			provKey[1] = (byte)keySize;

			Array.Copy(pkBytes, 0, provKey, keySize - pkBytes.Length + 2, pkBytes.Length);

			var privKey = Convert.ToBase64String(provKey);
			return privKey;
		}
	}
}
