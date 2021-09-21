using System;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;

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
		static Progress Progress;
		static string progressPath;
		static DateTime StartTime;

		static void Main(string[] args)
		{
			var workFile = "work.ini";
			progressPath = Path.GetFileNameWithoutExtension(workFile) + ".json";
			Progress = Progress.OpenOrCreate(WorkFile.Open(workFile), progressPath);

			Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle;

			var workers = Progress.Workers;
			Workers = new BruteWorker[workers.Length];
			WorkerProgress = new int[workers.Length];
			WorkerCount = new long[workers.Length];
			AlreadyCompleted = workers.Sum(w => w.CurrentPosition - w.Start);
			StartTime = DateTime.Now;

			for (int i = 0; i < workers.Length; i++)
			{
				Workers[i] = new BruteWorker(i, workers[i].Start, workers[i].CurrentPosition, workers[i].End, Progress.PublicKeys, workers[i].CurrentPoint, Progress.WorkFile.GeneratorPoint);
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

					if (DateTime.Now - lastProgressReport < reportInterval)
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
					Console.WriteLine("\r\nFound all keys!");
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

		private static void Worker_FoundKey(object sender, (long privateKey, long pubX, long pubY) e)
		{
			Progress.FoundKeyPairs.Add(e);

			for (int i = Progress.PublicKeys.Count - 1; i >= 0; i--)
			{
				if (Progress.PublicKeys[i].x == e.pubX || Progress.PublicKeys[i].y == e.pubY)
					Progress.PublicKeys.RemoveAt(i);
			}

			var replacementPublicKeys = Progress.PublicKeys.ToArray();

			foreach (var w in Workers)
			{
				if (replacementPublicKeys.Length == 0)
					w.CancelAsync();
				else
					w.ReplacePublicKeyList(replacementPublicKeys);
			}

			var message = $"Found Private Key {e.privateKey} for Public Key ({e.pubX}, {e.pubY})";
			Message.Add((false, message));

			if (replacementPublicKeys.Length == 0)
				Message.CompleteAdding();
		}

		private static void Worker_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
		{
			var worker = sender as BruteWorker;
			var state = e.UserState as WorkerState;

			Progress.Workers[worker.ThreadId].CurrentPoint = state.CurrentPoint;
			Progress.Workers[worker.ThreadId].CurrentPosition = state.CurrentPosition;

			WorkerProgress[worker.ThreadId] = e.ProgressPercentage;
			WorkerCount[worker.ThreadId] = worker.CurrentPosition - worker.Start;

			var total = Progress.WorkFile.End - Progress.WorkFile.Start - AlreadyCompleted;
			var totalProcessed = WorkerCount.Sum() - AlreadyCompleted;
			var remaining = total - totalProcessed;

			var rate = totalProcessed / (DateTime.Now - StartTime).TotalSeconds;
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
