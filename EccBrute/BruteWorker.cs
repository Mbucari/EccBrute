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
		public event EventHandler<KeyPair> FoundKey;
		public int ThreadId { get; }
		public long Start { get; }
		public long CurrentPosition { get; set; }
		public long End { get; }
		public PublicKey[] PublicKeys { get; private set; }
		public FastEccPoint CurrentPoint { get; }
		public FastEccPoint GeneratorPoint { get; }

		private object lockObject = new object();
		private PublicKey[] ReplacementPublicKeys;

		public BruteWorker(int thread, long startNum, long currentPosition, long endNum, List<PublicKey> publicKeys, FastEccPoint startPoint, FastEccPoint generatorPoint)
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

		public void ReplacePublicKeyList(PublicKey[] publicKeys)
		{
			lock (lockObject)
			{
				ReplacementPublicKeys = new PublicKey[publicKeys.Length];
				Array.Copy(publicKeys, ReplacementPublicKeys, publicKeys.Length);
			}
		}
		protected override void OnDoWork(DoWorkEventArgs e)
		{
			Thread.CurrentThread.Priority = ThreadPriority.Lowest;

			for (; CurrentPosition < End && !CancellationPending; CurrentPosition++)
			{
				for (int k = 0; k < PublicKeys.Length; k++)
				{
					if (CurrentPoint.X == PublicKeys[k].X && CurrentPoint.Y == PublicKeys[k].Y)
					{
						FoundKey?.Invoke(this, new KeyPair { PrivateKey = CurrentPosition, PublicKey = PublicKeys[k] });
						break;
					}
				}

				if (CurrentPosition % 1000000 == 0)
				{
					lock (lockObject)
					{
						if (ReplacementPublicKeys != null)
						{
							PublicKeys = ReplacementPublicKeys;
							ReplacementPublicKeys = null;
						}
					}
					ReportProgress((int)((CurrentPosition - Start) / (double)(End - Start) * 10000), new WorkerState { CurrentPoint = CurrentPoint.Clone(), CurrentPosition = CurrentPosition });
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
