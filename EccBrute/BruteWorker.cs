using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;

namespace EccBrute
{
	internal unsafe class BruteWorker : BackgroundWorker
	{
		public delegate PublicKey[] FoundKeyHandler(object sender, KeyPair keyPair);
		public event FoundKeyHandler FoundKey;
		public int ThreadId { get; }
		public long Start { get; }
		public long[] CurrentPosition { get; set; }
		public long End { get; }
		public PublicKey[] PublicKeys { get; private set; }
		public Vector256<long>[] pubkeyX { get; private set; }
		public Vector256<long>[] pubkeyY { get; private set; }
		public FastEccPoint CurrentPoint { get; }
		public FastEccPoint GeneratorPoint { get; }


		Memory<long> QuotXs = new long[4];
		MemoryHandle hQuotXs;
		long* pQuotXs;

		public BruteWorker(int thread, long startNum, long[] currentPosition, long endNum, List<PublicKey> publicKeys, FastEccPoint startPoint, FastEccPoint generatorPoint)
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

			pubkeyX = new Vector256<long>[PublicKeys.Length];
			pubkeyY = new Vector256<long>[PublicKeys.Length];

			for (int i = 0; i < PublicKeys.Length; i++)
			{
				pubkeyX[i] = Vector256.Create(PublicKeys[i].X, PublicKeys[i].X, PublicKeys[i].X, PublicKeys[i].X);
				pubkeyY[i] = Vector256.Create(PublicKeys[i].Y, PublicKeys[i].Y, PublicKeys[i].Y, PublicKeys[i].Y);
			}

			hQuotXs = QuotXs.Pin();
			pQuotXs = (long*)hQuotXs.Pointer;
		}

		protected override void OnDoWork(DoWorkEventArgs e)
		{

			Thread.CurrentThread.Priority = ThreadPriority.Lowest;

			for (; CurrentPosition[3] < End && !CancellationPending; CurrentPosition[0]++, CurrentPosition[1]++, CurrentPosition[2]++, CurrentPosition[3]++ )
			{
				for (int k = 0; k < PublicKeys.Length; k++)
				{
					var anding = Avx2.Or(Avx2.Xor(CurrentPoint.FourPointsX, pubkeyX[k]), Avx2.Xor(CurrentPoint.FourPointsY, pubkeyY[k]));

					Avx.Store(pQuotXs, anding);

					if (*pQuotXs == 0)
					{
						PublicKeys = FoundKey?.Invoke(this, new KeyPair { PrivateKey = CurrentPosition[0], PublicKey = PublicKeys[k] });
						UpdatePubKeys();
					}

					if (*(pQuotXs + 1) == 0)
					{
						PublicKeys = FoundKey?.Invoke(this, new KeyPair { PrivateKey = CurrentPosition[1], PublicKey = PublicKeys[k] });
						UpdatePubKeys();
					}

					if (*(pQuotXs + 2) == 0)
					{
						PublicKeys = FoundKey?.Invoke(this, new KeyPair { PrivateKey = CurrentPosition[2], PublicKey = PublicKeys[k] });
						UpdatePubKeys();
					}

					if (*(pQuotXs + 3) == 0)
					{
						PublicKeys = FoundKey?.Invoke(this, new KeyPair { PrivateKey = CurrentPosition[3], PublicKey = PublicKeys[k] });
						UpdatePubKeys();
					}
				}

				if (CurrentPosition[0] % 100000 == 0)
				{
					ReportProgress((int)((CurrentPosition[0] - Start) / ((double)(End - Start) / 4) * 10000), new WorkerState { CurrentPoint2 = CurrentPoint.Clone(), CurrentPosition2 = (long[])CurrentPosition.Clone() });
				}

				CurrentPoint.Add(GeneratorPoint);
			}
		}

		private void UpdatePubKeys()
		{
			pubkeyX = new Vector256<long>[PublicKeys.Length];
			pubkeyY = new Vector256<long>[PublicKeys.Length];

			for (int i = 0; i < PublicKeys.Length; i++)
			{
				pubkeyX[i] = Vector256.Create(PublicKeys[i].X, PublicKeys[i].X, PublicKeys[i].X, PublicKeys[i].X);
				pubkeyY[i] = Vector256.Create(PublicKeys[i].Y, PublicKeys[i].Y, PublicKeys[i].Y, PublicKeys[i].Y);
			}
		}
	}

	class WorkerState
	{
		public FastEccPoint CurrentPoint2;
		public long[] CurrentPosition2;
	}
}
