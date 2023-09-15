using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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

			var xcomp = point.CompareTo(list[mid].EndPoint);

			if (xcomp == 0)
				return mid;
			else if (xcomp == 1)
				return IndexOf(list, point, mid + 1, high);
			else
				return IndexOf(list, point, low, mid - 1);
		}
	}

	struct EccChainEndpoint : IComparable<EccChainEndpoint>
	{
		public long EndPrivKey;
		public FastEccPoint EndPoint;
		public EccChainEndpoint(long endPrivKey, FastEccPoint fastEccPoint)
		{
			EndPrivKey = endPrivKey;
			EndPoint = fastEccPoint;
		}

		public int CompareTo(EccChainEndpoint other)
			=> EndPoint.CompareTo(other.EndPoint);
	}

	class Program
	{
		static int binarySearch(EccChainEndpoint[] points, FastEccPoint point)
		{
			int lo = 0;
			int hi = points.Length - 1;
			while (lo <= hi)
			{
				var i = lo + ((hi - lo) >> 1);
				var c = points[i].EndPoint.CompareTo(point);
				if (c == 0) return i;
				if (c < 0)
				{
					lo = i + 1;
				}
				else
				{
					hi = i - 1;
				}
			}
			return -1;
		}

		static void FindKeys(EllipticCurve curve, EccChainEndpoint[] eccChainEndpoints, PublicKey publicKey)
		{
			var sw = Stopwatch.StartNew();
			var eccp = new FastEccPoint(publicKey.X, publicKey.Y);
			int index, numAdds = 0;
			while ((index = binarySearch(eccChainEndpoints, eccp)) < 0)
			{
				eccp = curve.Add(eccp, curve.Generator);
				numAdds++;
			}
			var indexPoint = eccChainEndpoints[index];
			var privKey = indexPoint.EndPrivKey - numAdds;
			var validator = curve.Multiply(curve.Generator, privKey);

			if (validator.X != publicKey.X
				|| validator.Y != publicKey.Y)
			{
				Console.WriteLine($"Private key validation for ({publicKey.X}, {publicKey.Y}) failed!");
				return;
			}
			sw.Stop();
			var priv = new PrivateKey(privKey);
			var message = $"Found Private Key {priv.ToBase64BEString(5)} for Public Key {publicKey.ToBase64BEString()} in {sw.ElapsedMilliseconds} ms";
			Console.WriteLine(message);
		}

		static void GenerateDb(Span<EccChainEndpoint> endpoints, EllipticCurve curve, long start, long stepSize, int numSteps)
		{
			var end = start - stepSize * (numSteps - 1);

			var startP = curve.ScaleGenerator(end);
			var stepP = curve.ScaleGenerator(stepSize);
			endpoints[0] = new EccChainEndpoint(end, startP);

			for (int i = 1; i < numSteps; i++)
			{
				end += stepSize;
				startP = curve.Add(startP, stepP);
				endpoints[i] = new EccChainEndpoint(end, startP);
			}
		}

		static async Task Main()
        {
			var workFilePath = "work.ini";

			var workFile = WorkFile.Open(workFilePath);
			var generator = workFile.Curve.Generator;

			long numDbEntryes = (long)(7 * Math.Sqrt(workFile.Curve.Order));
			var numStepsPerThread = (numDbEntryes / workFile.Threads) + 1;

			long threadEnd = workFile.Curve.Order - 1;
			long stepSize = ((workFile.Curve.Order - workFile.Start) / numDbEntryes);

			numDbEntryes = (workFile.Threads - 1) * numStepsPerThread + ((threadEnd - workFile.Start - numStepsPerThread * stepSize * (workFile.Threads - 1)) / stepSize);

			var swOverall = Stopwatch.StartNew();

			var endpoints = new EccChainEndpoint[numDbEntryes];
			var seg = new Memory<EccChainEndpoint>(endpoints);
			List<Task> dbGenTasks = new();

			Console.WriteLine($"Generating a database of {numDbEntryes} ECC Points...");

			long numsteps = 0;
			var sw = Stopwatch.StartNew();
			for (int t = 0; t < workFile.Threads; t++)
			{
				var numSteps = (int)Math.Min(numStepsPerThread, (threadEnd - workFile.Start) / stepSize);
				numDbEntryes -= numSteps;

				var slice = seg.Slice((int)numDbEntryes, numSteps);
				numsteps += numSteps;

				var start = threadEnd;

				dbGenTasks.Add(Task.Run(() => GenerateDb(slice.Span, workFile.Curve, start, stepSize, numSteps)));

				threadEnd -= numStepsPerThread * stepSize;
			}

			await Task.WhenAll(dbGenTasks);

			sw.Stop();

			Console.WriteLine($"Done Generating DB after {sw.ElapsedMilliseconds} ms");

			Console.WriteLine("Sorting Points");
			Array.Sort(endpoints);

			//Console.WriteLine("Creating a HashSet");
			//var hashSet = endpoints.Select(ep => ep.EndPoint).ToHashSet();

			Console.WriteLine($"\r\n--- BEGIN finding private keys for {workFile.PublicKeys.Count} public keys ---\r\n");

			List<Task> keySearchTasks = new(workFile.Threads);
			foreach (var pk in workFile.PublicKeys)
			{
				if (keySearchTasks.Count == workFile.Threads)
				{
					var completed = await Task.WhenAny(keySearchTasks);
					keySearchTasks.Remove(completed);
				}
				keySearchTasks.Add(Task.Run(() => FindKeys(workFile.Curve, endpoints, pk)));
			}

			await Task.WhenAll(keySearchTasks);
			swOverall.Stop();
			Console.WriteLine($"\r\nDone! Total Time: {swOverall.ElapsedMilliseconds:F} ms");
		}
	}
}
