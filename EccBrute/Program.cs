using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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

		static void GenerateDb(Span<EccChainEndpoint> endpoints, EllipticCurve curve, long endNum, long stepSize, int numSteps)
		{
			var startNum = endNum - stepSize * (numSteps - 1);

			var startP = curve.ScaleGenerator(startNum);
			var stepP = curve.ScaleGenerator(stepSize);
			endpoints[0] = new EccChainEndpoint(startNum, startP);

			for (int i = 1; i < numSteps; i++)
			{
				startNum += stepSize;
				startP = curve.Add(startP, stepP);
				endpoints[i] = new EccChainEndpoint(startNum, startP);
			}
		}

		static int partition<T>(Span<T> arr, out int nleft, out int nright)
			where T: IComparable<T>
		{
			var pivot = arr[^1];
			var i = 0;

			var max = arr.Length - 1;
			for (int j = 0; j != max; j++)
			{
				if (arr[j].CompareTo(pivot) <= 0)
				{
					(arr[i], arr[j]) = (arr[j], arr[i]);
					i++;
				}
			}

			(arr[i], arr[^1]) = (arr[^1], arr[i]);

			nleft = i;
			nright = arr.Length - i - 1;
			if (nleft + nright != arr.Length - 1) ;
			return i;
		}


		static int HoarePartition<T>(Span<T> arr)
			where T : IComparable<T>
		{
			var pivot = arr[(arr.Length - 1) / 2];

			int i = 0, j = arr.Length - 1;

			while (true)
			{
				while (arr[i].CompareTo(pivot) < 0)
					i++;

				while (arr[j].CompareTo(pivot) > 0)
					j--;

				if (i >= j) return j;

				(arr[i], arr[j]) = (arr[j], arr[i]);
			}
		}

		static async Task qSortAsync<T>(Memory<T> arr, int numTasks, int maxThreads)
			where T: IComparable<T>
		{
			if (arr.Length > 1)
			{
				var pi = HoarePartition(arr.Span);

				var bottom = arr.Slice(0, pi + 1);
				var top = arr.Slice(pi + 1);
				if (numTasks < maxThreads)
				{
					await Task.WhenAll(
						Task.Run(() => qSortAsync(bottom, numTasks + 2, maxThreads)),
						Task.Run(() => qSortAsync(top, numTasks + 2, maxThreads)));
				}
				else
				{
					await qSortAsync(bottom, numTasks, maxThreads);
					await qSortAsync(top, numTasks, maxThreads);
				}
			}
		}

		static async Task qSortAsync1<T>(Memory<T> arr, int numTasks, int maxThreads)
			where T: IComparable<T>
		{
			if (arr.Length > 0)
			{
				var pi = partition(arr.Span, out var nleft, out var nright);

				var bottom = arr.Slice(0, pi);
				var top = arr.Slice(pi + 1);

				if (numTasks < maxThreads)
				{
					await Task.WhenAll(
						Task.Run(() => qSortAsync1(bottom, numTasks + 2, maxThreads)),
						Task.Run(() => qSortAsync1(top, numTasks + 2, maxThreads)));
				}
				else
				{
					await qSortAsync1(bottom, numTasks, maxThreads);
					await qSortAsync1(top, numTasks, maxThreads);
				}
			}
		}

		static async Task Main()
        {
			var workFilePath = "work.ini";

			var workFile = WorkFile.Open(workFilePath);
			var generator = workFile.Curve.Generator;

			var rangePerThread = (workFile.Curve.Order - workFile.Start) / workFile.Threads;
			//Always a multiple of Threads
			var totalRange = rangePerThread * workFile.Threads;

			//Always a multiple of Threads
			var numStepsPerThread = 20000000;// (int)(7 * Math.Sqrt(totalRange) / workFile.Threads);
			long numDbEntries = (long)numStepsPerThread * workFile.Threads;
			var threadEnd = workFile.Curve.Order - 1;

			var swOverall = Stopwatch.StartNew();
			var sw = Stopwatch.StartNew();

			var endpoints = new EccChainEndpoint[numDbEntries];
			var seg = new Memory<EccChainEndpoint>(endpoints);
			List<Task> dbGenTasks = new();

			Console.WriteLine($"Generating a database of {numDbEntries} ECC Points...");

			for (int t = 0; t < workFile.Threads; t++)
			{
				var endNum = threadEnd;
				threadEnd -= rangePerThread;
				var stepSize = rangePerThread / numStepsPerThread;

				var slice = seg.Slice(numStepsPerThread * t, numStepsPerThread);
				dbGenTasks.Add(Task.Run(() => GenerateDb(slice.Span, workFile.Curve, endNum, stepSize, numStepsPerThread)));
			}

			await Task.WhenAll(dbGenTasks);

			sw.Stop();

			Console.WriteLine($"Done Generating DB after {sw.ElapsedMilliseconds} ms");
			Console.WriteLine("Sorting Points");
			var a2 = new EccChainEndpoint[numDbEntries];
			var a3 = new EccChainEndpoint[numDbEntries];
			Array.Copy(endpoints, 0, a2, 0, endpoints.Length);
			Array.Copy(endpoints, 0, a3, 0, endpoints.Length);

			try
			{
				var ttt = new List<Task>();
				sw.Restart();
				await qSortAsync(seg, 0, workFile.Threads);
				sw.Stop();
				Console.WriteLine($"H-Partition: {sw.ElapsedMilliseconds}");
				sw.Restart();
				await qSortAsync1(new Memory<EccChainEndpoint>(a2), 0, workFile.Threads);
				sw.Stop();
				Console.WriteLine($"L-Partition: {sw.ElapsedMilliseconds}");
			}catch (Exception ex)
			{

			}
			sw.Restart();
			Array.Sort(a3);
			sw.Stop();
			Console.WriteLine($"Framework Sort: {sw.ElapsedMilliseconds}");

			for (int i = 0; i < a2.Length; i++)
				if (endpoints[i].CompareTo(a2[i]) != 0) ;

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
