using System;

namespace EccBrute
{
	internal class PrivateKey
	{
        public long Key { get; }

        public PrivateKey(long key)
        {
            Key = key;
        }

        public static PrivateKey Parse(string beEncoded)
        {
            var bytes = Convert.FromBase64String(beEncoded);

            var buff = new byte[bytes.Length + 1];
            Array.Copy(bytes, 0, buff, 0, bytes.Length);

            var privKey = (long)new System.Numerics.BigInteger(buff);

            return new PrivateKey(privKey);
        }

        public string ToBase64BEString(int? keySize = null)
        {
            var bytes = new System.Numerics.BigInteger(Key).ToByteArray();

            int btslen = bytes.Length;

            if (bytes[btslen - 1] == 0)
                btslen--;

            var size = keySize ?? btslen;
            var tmp = new byte[size];
            Array.Copy(bytes, 0, tmp, 0, btslen);
            Array.Reverse(tmp);

            var full = new byte[size + 2];
            full[0] = 2;
            full[1] = (byte)size;
            Array.Copy(tmp, 0, full, 2, size);
            return Convert.ToBase64String(full);
        }
    }
}
