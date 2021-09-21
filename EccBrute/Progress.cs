using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EccBrute
{
	[Serializable]
	class Progress
	{
		public WorkFile WorkFile { get; set; }
		public List<(long x, long y)> PublicKeys { get; set; }
		public List<(long privateKey, long pubX, long pubY)> FoundKeyPairs { get; set; }
		public WorkProgress[] Workers { get; set; }

		public void Save(string path)
		{
			var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
			var jsonStr = JsonSerializer.Serialize(this, GetType(), options);

			File.WriteAllText(path, jsonStr);
		}

		public static Progress OpenOrCreate(WorkFile workFile, string progressFile)
		{
			if (File.Exists(progressFile))
				return Open(workFile, progressFile);
			return CreateNew(workFile, progressFile);
		}

		public static Progress Open(WorkFile workFile, string progressFile)
		{
			var jsonStr = File.ReadAllText(progressFile);
			var options = new JsonSerializerOptions { IncludeFields = true };
			var progress = JsonSerializer.Deserialize<Progress>(jsonStr, options);

			if (progress.WorkFile.A != workFile.A ||
				progress.WorkFile.B != workFile.B ||
				progress.WorkFile.Q != workFile.Q ||
				progress.WorkFile.Gx != workFile.Gx ||
				progress.WorkFile.Gy != workFile.Gy ||
				progress.WorkFile.Order != workFile.Order)
				throw new Exception($"{nameof(WorkFile)} parameters don't match {nameof(Progress)}.{nameof(progress.WorkFile)}");

			return progress;
		}

		public static Progress CreateNew(WorkFile workFile, string savePath)
		{
			var progress = new Progress();

			progress.WorkFile = workFile;
			progress.PublicKeys = workFile.PublicKeys;
			progress.Workers = new WorkProgress[workFile.Threads];
			progress.FoundKeyPairs = new List<(long privateKey, long pubX, long pubY)>();

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

				var startPoint = edpointBouncy.Multiply(BigInteger.ValueOf(start)).Normalize();

				progress.Workers[i] = new WorkProgress();

				progress.Workers[i].ThreadID = i;
				progress.Workers[i].CurrentPoint = new FastEccPoint(startPoint);
				progress.Workers[i].Start = start;
				progress.Workers[i].CurrentPosition = start;
				progress.Workers[i].End = endNum;

				start = endNum;
			}
			progress.Save(savePath);
			return progress;
		}
	}
}
