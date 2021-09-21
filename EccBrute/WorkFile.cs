using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EccBrute
{
	[Serializable]
	class WorkFile
	{
		public long Q { get; set; }
		public long A { get; set; }
		public long B { get; set; }
		public long? Order { get; set; }
		public long Gx { get; set; }
		public long Gy { get; set; }
		public FastEccPoint GeneratorPoint { get; set; }
		public long Start { get; set; }
		public long End { get; set; }
		public int Threads { get; set; }		
		public List<(long x, long y)> PublicKeys { get; set; }

		public static WorkFile Open(string path)
		{
			var lines = File.ReadAllLines(path);

			long q = 0, a = 0, b = 0, gx = 0, gy = 0, start=0, end=0;
			long? order = null;
			var threads = 1;
			var publicKeys64 = Array.Empty<string>();

			foreach (var line in lines)
			{
				int delimIndex = line.IndexOf('=');
				if (delimIndex < 1)
					continue;

				string key = line.Substring(0, delimIndex).Trim().ToLower();

				string value = line.Substring(delimIndex + 1).Trim();

				if (key == "q" && long.TryParse(value, out q))
					continue;
				if (key == "a" && long.TryParse(value, out a))
					continue;
				if (key == "b" && long.TryParse(value, out b))
					continue;
				if (key == "order" && long.TryParse(value, out var o))
				{
					order = o;
					continue;
				}
				if (key == "gx" && long.TryParse(value, out gx))
					continue;
				if (key == "gy" && long.TryParse(value, out gy))
					continue;
				if (key == "start" && long.TryParse(value, out start))
					continue;
				if (key == "end" && long.TryParse(value, out end))
					continue;
				if (key == "threads" && int.TryParse(value, out threads))
					continue;
				if (key == "publickeys")
					publicKeys64 = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			}
			if (q ==0|| a ==0||b ==0 || gx == 0 || gy == 0)
				throw new Exception("Error reading ECC parameters (Q, A, B, Gx, Gy)");
			if (start == 0 || end == 0)
				throw new Exception("Error reading private key search range (start and end)");
			if (publicKeys64.Length == 0)
				throw new Exception("No public keys to brite force.");

			var publicKeys = new List<(long, long)>();
			foreach (var pk in publicKeys64)
			{
				var bytes = Convert.FromBase64String(pk);

				var b1 = new byte[5];
				var b2 = new byte[5];
				Array.Copy(bytes, 0, b1, 0, 5);
				Array.Copy(bytes, 5, b2, 0, 5);
				var publixX = (long)(new System.Numerics.BigInteger(b1, true, true));
				var publixY = (long)(new System.Numerics.BigInteger(b2, true, true));

				publicKeys.Add((publixX, publixY));
			}

			FastEccPoint.Curve_A = a;
			FastEccPoint.NegCurve_A = -a;
			FastEccPoint.Q = q;
			var ABigInt = BigInteger.ValueOf(a);
			FastEccPoint.BitLengthsDiff = ABigInt.BitLength < ABigInt.Negate().BitLength;

			return new WorkFile
			{
				Q = q,
				A = a,
				B = b,
				Order = order,
				Gx = gx,
				Gy = gy,
				GeneratorPoint = new FastEccPoint { X = gx, Y = gy, Z0 = 1, Z1 = a },
				Start = start,
				End = end,
				Threads = threads,
				PublicKeys = publicKeys
			};
		}
	}
}
