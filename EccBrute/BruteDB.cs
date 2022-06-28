using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EccBrute
{
	class BruteDB
	{
		public WorkFile WorkFile { get; set; }
		public List<PublicKey> PublicKeysToFind { get; set; }
		public List<KeyPair> FoundKeyPairs { get; set; }
		public WorkProgress[] Workers { get; set; }

		public void Save(string path)
		{
			var options = new JsonSerializerOptions { WriteIndented = true };
			var jsonStr = JsonSerializer.Serialize(this, GetType(), options);

			File.WriteAllText(path, jsonStr);
		}

		public static BruteDB OpenOrCreate(WorkFile workFile, string progressFile)
		{
			if (File.Exists(progressFile))
				return Open(workFile, progressFile);
			return CreateNew(workFile, progressFile);
		}

		public static BruteDB Open(WorkFile workFile, string progressFile)
		{
			var jsonStr = File.ReadAllText(progressFile);
			var options = new JsonSerializerOptions();
			var progress = JsonSerializer.Deserialize<BruteDB>(jsonStr, options);

			if (progress.WorkFile.A != workFile.A ||
				progress.WorkFile.B != workFile.B ||
				progress.WorkFile.Q != workFile.Q ||
				progress.WorkFile.Gx != workFile.Gx ||
				progress.WorkFile.Gy != workFile.Gy ||
				progress.WorkFile.Order != workFile.Order)
				throw new Exception($"{nameof(WorkFile)} parameters don't match {nameof(BruteDB)}.{nameof(progress.WorkFile)}");

			return progress;
		}

		public static BruteDB CreateNew(WorkFile workFile, string savePath)
		{
			var progress = new BruteDB
			{
				WorkFile = workFile,
				PublicKeysToFind = new List<PublicKey>(workFile.PublicKeys),
				Workers = new WorkProgress[workFile.Threads],
				FoundKeyPairs = new List<KeyPair>()
			};

			var order = workFile.Order.HasValue ? BigInteger.ValueOf(workFile.Order.Value) : null;

			var E = new FpCurve(BigInteger.ValueOf(workFile.Q),
				BigInteger.ValueOf(workFile.A),
				BigInteger.ValueOf(workFile.B),
				order,
				null);

			var x = BigInteger.ValueOf(workFile.Gx);
			var y = BigInteger.ValueOf(workFile.Gy);
			var edpointBouncy = E.ValidatePoint(x, y);

			var step = (workFile.End - workFile.Start) / workFile.Threads;

			long start = workFile.Start;
			
			for (int i = 0; i < workFile.Threads; i++)
			{
				long endNum = i == workFile.Threads - 1 ? workFile.End : start + step;

				long remainder = (endNum - start) % 4;

				endNum += (4 - remainder);

				var vecStep = (endNum - start) / 4;

				var startPoint0 = edpointBouncy.Multiply(BigInteger.ValueOf(start)).Normalize();
				var startPoint1 = edpointBouncy.Multiply(BigInteger.ValueOf(start + vecStep)).Normalize();
				var startPoint2 = edpointBouncy.Multiply(BigInteger.ValueOf(start + 2 * vecStep)).Normalize();
				var startPoint3 = edpointBouncy.Multiply(BigInteger.ValueOf(start + 3 * vecStep)).Normalize();

				progress.Workers[i] = new WorkProgress
				{
					ThreadID = i,
					CurrentPoint = new FastEccPoint(startPoint0, startPoint1, startPoint2, startPoint3),
					CurrentPosition =  new long[] { start, start + vecStep , start + 2 * vecStep , start + 3 * vecStep },
					Start = start,
					End = endNum
				};

				start = endNum;
			}
			progress.Save(savePath);
			return progress;
		}
	}
}
