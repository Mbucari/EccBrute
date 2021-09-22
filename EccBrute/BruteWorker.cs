using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EccBrute
{
	internal class BruteWorker : BackgroundWorker
	{
		public event EventHandler<(long privateKey, long pubX, long pubY)> FoundKey;
		public int ThreadId { get; }
		public long Start { get; }
		public long CurrentPosition { get { return i; }  set { i = value; } }
		public long End { get; }
		public (long x, long y)[] PublicKeys { get; private set; }
		public FastEccPoint CurrentPoint { get; }
		public FastEccPoint GeneratorPoint { get; }

		private object lockObject = new object();
		private (long x, long y)[] ReplacementPublicKeys;

		private long i;
		public BruteWorker(int thread, long startNum, long currentPosition, long endNum, List<(long x, long y)> publicKeys, FastEccPoint startPoint, FastEccPoint generatorPoint)
		{
			ThreadId = thread;
			Start = startNum;
			CurrentPosition = currentPosition;
			End = endNum;
			CurrentPoint = startPoint;
			GeneratorPoint = generatorPoint;
			WorkerReportsProgress = true;
			WorkerSupportsCancellation = true;

			PublicKeys = publicKeys.ToArray();
		}

		public void ReplacePublicKeyList((long x, long y)[] publicKeys)
		{
			lock (lockObject)
			{
				ReplacementPublicKeys = new (long x, long y)[publicKeys.Length];
				Array.Copy(publicKeys, ReplacementPublicKeys, publicKeys.Length);
			}
		}
		protected override void OnDoWork(DoWorkEventArgs e)
		{
			Thread.CurrentThread.Priority = ThreadPriority.Lowest;

			for (; i < End && !CancellationPending; i++)
			{
				for (int k = 0; k < PublicKeys.Length; k++)
				{
					if (CurrentPoint.X == PublicKeys[k].x && CurrentPoint.Y == PublicKeys[k].y)
					{
						var pubK = PublicKeys[k];

						FoundKey?.Invoke(this, (i, pubK.x, pubK.y));
						break;
					}
				}

				if (i % 1000000 == 0)
				{
					lock (lockObject)
					{
						if (ReplacementPublicKeys != null)
						{
							PublicKeys = new (long x, long y)[ReplacementPublicKeys.Length];
							Array.Copy(ReplacementPublicKeys, PublicKeys, ReplacementPublicKeys.Length);
							ReplacementPublicKeys = null;
						}
					}
					ReportProgress((int)((i - Start) / (double)(End - Start) * 10000), new WorkerState { CurrentPoint = CurrentPoint.Clone(), CurrentPosition = CurrentPosition });
				}

				CurrentPoint.Add(GeneratorPoint);
			}
		}
	}

	class WorkerState
	{
		public FastEccPoint CurrentPoint;
		public long CurrentPosition;
	}
}
