﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace EccBrute
{
	class Program
	{
		static string lastMessageText = "";
		static BlockingCollection<(bool repeating, string text)> Message = new BlockingCollection<(bool repeating, string text)>();
		static BruteWorker[] Workers;
		static int[] WorkerProgress;
		static long[] WorkerCount;
		static long AlreadyCompleted;
		static BruteDB Progress;
		static string progressPath;
		static DateTime StartTime;

		static void Main(string[] args)
		{
#if DEBUG
			var workFile = "testwork.ini";
#else
			var workFile = "work.ini";
#endif
			progressPath = Path.GetFileNameWithoutExtension(workFile) + ".json";
			Progress = BruteDB.OpenOrCreate(WorkFile.Open(workFile), progressPath);

			if (Progress.PublicKeysToFind.Count == 0)
			{
				Console.WriteLine($"There are {Progress.FoundKeyPairs.Count} found keys in the database and no more public keys to find!");
				return;
			}

			Thread.CurrentThread.Priority = ThreadPriority.Highest;
			Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle;

			var workers = Progress.Workers;
			Workers = new BruteWorker[workers.Length];
			WorkerProgress = new int[workers.Length];
			WorkerCount = new long[workers.Length];
			AlreadyCompleted = workers.Sum(w => (w.CurrentPosition[0] - w.Start) * 4);
			StartTime = DateTime.Now;

			for (int i = 0; i < workers.Length; i++)
			{
				//Workers[i] = new BruteWorker(i, workers[i].Start, workers[i].CurrentPosition, workers[i].End, Progress.PublicKeysToFind, workers[i].CurrentPoint, Progress.WorkFile.GeneratorPoint);
				Workers[i] = new BruteWorker(i, workers[i].Start, workers[i].CurrentPosition, workers[i].End, Progress.PublicKeysToFind, workers[i].CurrentPoint, Progress.WorkFile.GeneratorPoint);
				Workers[i].ProgressChanged += Worker_ProgressChanged;
				Workers[i].FoundKey += Worker_FoundKey;
				Workers[i].RunWorkerCompleted += Worker_RunWorkerCompleted;
				Workers[i].RunWorkerAsync();
			}

			var lastProgressReport = DateTime.Now;
			var reportInterval = TimeSpan.FromSeconds(1);
			while (true)
			{
				try
				{
					var message = Message.Take();

					if (DateTime.Now - lastProgressReport < reportInterval && message.repeating)
						continue;

					var sb = new StringBuilder();
					sb.Append('\b', lastMessageText.Length);
					sb.Append(message.text);
					sb.Append(' ', Math.Max(0,lastMessageText.Length - message.text.Length));

					if (message.repeating)
					{
						Console.Write(sb);
						lastMessageText = message.text;
					}
					else
					{
						Console.WriteLine(sb); 
						Console.Write(lastMessageText);
					}

					lastProgressReport = DateTime.Now;
					Progress.Save(progressPath);
				}
				catch(InvalidOperationException)
				{
					string status = Progress.PublicKeysToFind.Count == 0 ? "\r\nFound all keys!" : $"\r\nFinished searching. {Progress.PublicKeysToFind.Count} keys not found.";
					Console.WriteLine(status);
					break;
				}
				catch (IOException)
				{
					//Ignore and try writing again on next update
				}
				catch(Exception ex)
				{
					Console.WriteLine($"\r\nError:\t{ex.Message}");
				}
			}

			Console.ReadLine();
		}

		private static void Worker_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
		{
			var worker = sender as BruteWorker;
			WorkerProgress[worker.ThreadId] = 10000;

			if (WorkerProgress.Sum() == WorkerProgress.Length * 10000)
				Message.CompleteAdding();
		}
		private class KeyPairComparer : IEqualityComparer<PublicKey>
		{
			public bool Equals(PublicKey x, PublicKey y)
			{
				return x.X == y.X && x.Y == y.Y;
			}

			public int GetHashCode([DisallowNull] PublicKey obj)
			{
				return obj.GetHashCode();
			}
		}
		private static PublicKey[] Worker_FoundKey(object sender, KeyPair e)
		{
			Progress.FoundKeyPairs.Add(e);

			Progress.PublicKeysToFind = Progress.PublicKeysToFind.Except(Progress.FoundKeyPairs.Select(k=> k.PublicKey), new KeyPairComparer()).ToList();

			if (!Progress.PublicKeysToFind.Any())
			{
				foreach (var w in Workers)
					w.CancelAsync();
			}

			var message = $"Found Private Key {e.PrivateKey} for Public Key ({e.PublicKey.X}, {e.PublicKey.Y})";
			Message.Add((false, message));

			if (!Progress.PublicKeysToFind.Any())
				Message.CompleteAdding();
			return Progress.PublicKeysToFind.ToArray();
		}

		private static void Worker_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
		{
			var worker = sender as BruteWorker;
			var state = e.UserState as WorkerState;

			Progress.Workers[worker.ThreadId].CurrentPoint = state.CurrentPoint2;
			Progress.Workers[worker.ThreadId].CurrentPosition = state.CurrentPosition2;

			WorkerProgress[worker.ThreadId] = e.ProgressPercentage;
			WorkerCount[worker.ThreadId] = (worker.CurrentPosition[0] - worker.Start) * 4;

			var total = Progress.WorkFile.End - Progress.WorkFile.Start - AlreadyCompleted;
			var totalProcessed = WorkerCount.Sum() - AlreadyCompleted;
			var remaining = total - totalProcessed;

			var rate = totalProcessed / (DateTime.Now - StartTime).TotalSeconds;

			if (rate == 0) return;

			var remainingTime = TimeSpan.FromSeconds(remaining / rate);

			var sb = new StringBuilder();

			for (int i = 0; i < WorkerProgress.Length; i++)
			{
				sb.Append($"T{i}:{WorkerProgress[i] / 10000d:P} | ");
			}
			sb.Append($"ETA: {remainingTime:dd\\:hh\\:mm\\:ss}");

			Message.TryAdd((true, sb.ToString()));
		}		
	}
}
