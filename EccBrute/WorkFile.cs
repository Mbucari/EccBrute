using System;
using System.Collections.Generic;
using System.IO;

namespace EccBrute
{
	class WorkFile
	{
		public EllipticCurve Curve { get; private init; }
		public long Start { get; set; }
		public long End { get; set; }
		public int Threads { get; set; }
		public List<PublicKey> PublicKeys { get; set; }

		public static WorkFile Open(string path)
		{
			var lines = File.ReadAllLines(path);

			long q = 0, a = 0, b = 0, gx = 0, gy = 0, start = 0, end = 0, order = 0;
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
				if (key == "order" && long.TryParse(value, out order))
					continue;
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
			if (q == 0 || a == 0 || b == 0 || gx == 0 || gy == 0 || order == 0)
				throw new Exception("Error reading ECC parameters (Q, A, B, Gx, Gy)");
			if (start == 0 || end == 0)
				throw new Exception("Error reading private key search range (start and end)");
			if (publicKeys64.Length == 0)
				throw new Exception("No public keys to brite force.");

			var publicKeys = new List<PublicKey>();

			foreach (var pk in publicKeys64)
			{
				var parsed = PublicKey.Parse(pk);
				publicKeys.Add(parsed);
			}

			return new WorkFile
			{
				Curve = new EllipticCurve(a, b, q,order, gx, gy),
				Start = start,
				End = end,
				Threads = threads,
				PublicKeys = publicKeys
			};
		}
	}
}
