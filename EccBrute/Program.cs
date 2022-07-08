using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EccBrute
{
	static class ListExt
	{
		public static int IndexOf(this List<EccChainEndpoint> list, FastEccPoint point)
			=> IndexOf(list, point, 0, list.Count - 1);

		private static int IndexOf(List<EccChainEndpoint> list, FastEccPoint point, int low, int high)
		{
			if (low > high) return -1;

			int mid = (low + high) / 2;

			var xcomp = point.CompareTo(list[mid]);

			if (xcomp == 0)
				return mid;
			else if (xcomp == 1)
				return IndexOf(list, point, mid + 1, high);
			else
				return IndexOf(list, point, low, mid - 1);
		}
	}

	class EccChainEndpoint : FastEccPoint
	{
		public EccChainEndpoint(long endPrivKey, long x, long y) : base(x, y)
		{
			EndPrivKey = endPrivKey;
		}
		public long EndPrivKey;
	}

	class Program
	{
		static void FindKeys(SemaphoreSlim sema, List<EccChainEndpoint> eccChainEndpoints, FastEccPoint generator, PublicKey publicKey)
		{
			try
			{
				sema.Wait();
				var eccp = new FastEccPoint(publicKey.X, publicKey.Y);
				int index, numAdds = 0;
				while ((index = eccChainEndpoints.IndexOf(eccp)) == -1)
				{
					eccp += generator;
					numAdds++;
				}
				var indexPoint = eccChainEndpoints[index];
				var privKey = indexPoint.EndPrivKey - numAdds;
				var validator = generator * privKey;

				if (validator.X != publicKey.X
					|| validator.Y != publicKey.Y)
				{
					Console.WriteLine($"Private key validation for ({publicKey.X}, {publicKey.Y}) failes!");
					return;
				}
				var priv = new PrivateKey { Key = privKey };
				var message = $"Found Private Key {priv.ToBase64BEString()} for Public Key {publicKey.ToBase64BEString()}";
				Console.WriteLine(message);
			}
			finally
			{
				sema.Release();
			}
		}

		static List<EccChainEndpoint> GenerateDb(FastEccPoint basepoint, long start, long end, long stepSize)
		{
			List<EccChainEndpoint> endpoints = new();
			long endVal = start;
			do
			{
				endVal = Math.Min(end - 1, endVal + stepSize);
				var endPoint = basepoint * endVal;
				var e = new EccChainEndpoint(endVal, endPoint.X, endPoint.Y);
				endpoints.Add(e);
			} while (endVal < end - 1);

			return endpoints;
		}

		static async Task Main(string[] args)
		{
			var workFilePath = "work.ini";

			var workFile = WorkFile.Open(workFilePath);
			var generator = workFile.GeneratorPoint;

			List<EccChainEndpoint> endpoints = new();

			var numDbEntryes = 250000;
			var entriesPerThread = numDbEntryes / workFile.Threads + 1;
			long threadStart = 0, stepSize = workFile.Order / numDbEntryes + 1;
			List<Task<List<EccChainEndpoint>>> dbGenTasks = new();

			Console.WriteLine($"Generating a database of {numDbEntryes} ECC Points...");

			for (int t = 0; t < workFile.Threads; t++)
			{
				var start = threadStart;
				var end = Math.Min(workFile.Order - 1, start + stepSize * entriesPerThread);
				threadStart = end;

				dbGenTasks.Add(Task.Run(() => GenerateDb(generator, start, end, stepSize)));
			}

			var allLists = await Task.WhenAll(dbGenTasks);

			Console.WriteLine("Sorting the database");

			for (int i = 0; i < allLists.Length; i++)
				endpoints.AddRange(allLists[i]);

			endpoints.Sort();

			Console.WriteLine($"\r\n--- BEGIN finding private keys for {workFile.PublicKeys.Count} public keys ---\r\n");

			List<Task> keySearchTasks = new();
			SemaphoreSlim sema = new(workFile.Threads, workFile.Threads);
			foreach (var pk in workFile.PublicKeys)
			{
				keySearchTasks.Add(Task.Run(() => FindKeys(sema, endpoints, generator, pk)));
			}

			await Task.WhenAll(keySearchTasks);
			Console.WriteLine("\r\nDone!");
		}
	}
}
